using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace Character
{
    /// <summary>
    /// The character's link between movement and visuals. It gets the character's model at runtime from a
    /// CharacterModelSource, turns each frame's movement into a CharacterAnimationState, and passes that state and
    /// the movement events to the model's animation translator.
    ///
    /// Nothing here depends on a particular model, how it's stored, its Animator Controller or animation system: those
    /// all come from the model source, so a model can be replaced (or swapped at runtime) without touching the
    /// character. The character moves normally while its model is still loading.
    /// </summary>
    [DisallowMultipleComponent]
    public class AnimationController : MonoBehaviour, ICharacterAnimationStateSource
    {
        [SerializeField] [Tooltip("A reference to the parent character controller")]
        private ACCharacterController characterController;
        [SerializeField] [Tooltip("The character's movement, whose events this passes on. Found automatically if left empty.")]
        private MovementController movementController;

        [Header("Model")]
        [SerializeField] [Tooltip("Where this character's model comes from, and how to animate it")]
        private CharacterModelSource modelSource;
        [SerializeField] [Tooltip("The child transform the model is created under. It is turned smoothly to face the character's direction.")]
        private Transform modelParent;
        [SerializeField] [Tooltip("Load the model when the character starts. Turn off to load it from code with LoadModelAsync instead.")]
        private bool loadModelOnStart = true;

        [Header("Animation State")]
        [SerializeField] [Tooltip("Seconds the character must be off the ground before animating as airborne (unless they jumped), so stepping off small ledges or bumps doesn't flash the falling pose")]
        private float airborneDelay = 0.12f;
        [SerializeField] [Tooltip("Horizontal speed (m/s) above which the character counts as moving")]
        private float movingSpeedThreshold = 0.1f;

        [Header("Smooth Rotation Variables")]
        [SerializeField] [Tooltip("The speed at which the character model smoothly rotates to match the parent object's rotation")]
        private float rotationFixSpeed = 10f;

        // The last known world space rotation of the character model
        private Quaternion lastRotation = Quaternion.identity;
        // State carried between frames to build the animation state
        private float airTime;
        private Quaternion lastFacing = Quaternion.identity;
        private bool hasLastFacing;

        private ICharacterAnimationDriver driver;
        private CharacterModel model;
        // The source the current model came from, which releases it.
        private CharacterModelSource modelOwner;
        private CancellationTokenSource pendingLoad;
        private readonly List<CharacterModelSource.PreviewMesh> previewMeshes = new();

        /// <summary>The character's animation state as of the latest frame.</summary>
        public CharacterAnimationState AnimationState { get; private set; }

        /// <summary>The source of the current (or loading) model.</summary>
        public CharacterModelSource ModelSource => modelSource;
        /// <summary>The loaded model instance, or null while there is none (not loaded yet, or still loading).</summary>
        public GameObject Model => model != null && model.Root != null ? model.Root : null;
        /// <summary>The loaded model's Animator. May be null.</summary>
        public Animator Animator => Model != null ? model.Animator : null;
        /// <summary>IK for the loaded model's hands, feet, elbows and knees, blended over its animations. May be null.</summary>
        public CharacterIK IK => Model != null ? model.IK : null;
        public bool IsModelReady => Model != null;
        public bool IsLoadingModel => pendingLoad != null;

        /// <summary>Raised when a model has been loaded and set up, with the new model.</summary>
        public event Action<GameObject> ModelLoaded;
        /// <summary>Raised for each movement event (one flag at a time), with the animation state when it happened. For
        /// effects, sound and anything else that reacts to the character's actions.</summary>
        public event Action<MovementEvents, CharacterAnimationState> MovementEventReceived;

        private void Awake()
        {
            if (movementController == null) { movementController = GetComponent<MovementController>(); }
            if (modelParent == null) { modelParent = transform; }
        }

        private void OnEnable()
        {
            if (movementController != null) { movementController.MovementEventRaised += HandleMovementEvent; }
        }

        private void OnDisable()
        {
            if (movementController != null) { movementController.MovementEventRaised -= HandleMovementEvent; }
        }

        private void Start()
        {
            if (loadModelOnStart && modelSource != null && Model == null && !IsLoadingModel) { _ = LoadModelAsync(modelSource); }
        }

        private void OnDestroy()
        {
            CancelLoad();
            ReleaseModel();
        }

        /// <summary>
        /// Replaces the character's model with one from the given source, which creates it asynchronously. The current
        /// model stays until the new one is ready, so the character is never left invisible mid-swap. Returns whether
        /// the new model was loaded: false if it failed, or if another model was asked for (or the character destroyed)
        /// before it finished. Given null, removes the model.
        /// </summary>
        public async Awaitable<bool> LoadModelAsync(CharacterModelSource source)
        {
            CancelLoad();
            modelSource = source;
            if (source == null)
            {
                UnloadModel();
                return false;
            }

            // Cancelled if another model is asked for, or the character is destroyed, before this one is ready.
            var cancellation = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
            pendingLoad = cancellation;
            CharacterModel created;
            try
            {
                created = await source.CreateModelAsync(new CharacterModelContext(gameObject, modelParent), cancellation.Token);
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                return false;
            }
            finally
            {
                if (pendingLoad == cancellation) { pendingLoad = null; }
            }

            bool wanted = !cancellation.IsCancellationRequested && this != null;
            // A cancelled source may still be running its cancellation callbacks, so only an uncancelled one is disposed
            // here; a cancelled one is left for the garbage collector.
            if (!cancellation.IsCancellationRequested) { cancellation.Dispose(); }
            if (created == null || created.Root == null)
            {
                if (wanted) { Debug.LogWarning($"[{nameof(AnimationController)}] '{source.name}' didn't create a model.", this); }
                return false;
            }
            if (!wanted)
            {
                // The source finished without noticing it was cancelled; the character no longer wants this model.
                source.ReleaseModel(created);
                return false;
            }

            ReleaseModel();
            model = created;
            modelOwner = source;
            driver = created.Translator != null ? created.Translator.CreateDriver(created.Root, created.Animator) : null;
            lastRotation = modelParent.rotation;
            ModelLoaded?.Invoke(created.Root);
            return true;
        }

        /// <summary>Removes the current model, leaving the character without one.</summary>
        public void UnloadModel()
        {
            CancelLoad();
            ReleaseModel();
        }

        private void ReleaseModel()
        {
            if (model != null && modelOwner != null) { modelOwner.ReleaseModel(model); }
            else if (Model != null) { Destroy(Model); }
            model = null;
            modelOwner = null;
            driver = null;
        }

        private void CancelLoad()
        {
            CancellationTokenSource cancellation = pendingLoad;
            pendingLoad = null;
            cancellation?.Cancel();
        }

        /// <summary>
        /// Works out the character's animation state from their latest movement and passes it to the model's animation.
        /// Call this every rendered frame with the most recently completed movement data.
        /// </summary>
        /// <param name="movementData">The latest completed movement data, or null if none yet</param>
        /// <param name="deltaTime"></param>
        public void UpdateAnimator(MovementData movementData, float deltaTime)
        {
            if (movementData == null) { return; }
            AnimationState = BuildState(movementData, deltaTime);
            driver?.UpdateAnimation(AnimationState, deltaTime);
        }

        private void HandleMovementEvent(MovementEvents movementEvent, MovementData movementData)
        {
            // The state at the moment of the event (no time passes, so the frame-to-frame tracking is unaffected).
            CharacterAnimationState state = BuildState(movementData, 0f);
            driver?.HandleMovementEvent(movementEvent, state);
            MovementEventReceived?.Invoke(movementEvent, state);
        }

        /// <summary>The layer between movement and animation: everything animations need, and nothing about how movement works.</summary>
        private CharacterAnimationState BuildState(MovementData movementData, float deltaTime)
        {
            Vector3 velocity = movementData.finalVelocity;
            Quaternion facing = Quaternion.Euler(0f, movementData.finalRotation.eulerAngles.y, 0f);
            bool touchingGround = movementData.state.HasFlag(CharacterState.Grounded);
            bool jumping = movementData.state.HasFlag(CharacterState.Jumping);

            airTime = touchingGround ? 0f : airTime + deltaTime;
            float turnSpeed = AnimationState.turnSpeed;
            if (deltaTime > 0f)
            {
                turnSpeed = hasLastFacing ? Mathf.DeltaAngle(lastFacing.eulerAngles.y, facing.eulerAngles.y) / deltaTime : 0f;
                lastFacing = facing;
                hasLastFacing = true;
            }

            float horizontalSpeed = new Vector2(velocity.x, velocity.z).magnitude;
            return new CharacterAnimationState
            {
                velocity = velocity,
                localVelocity = Quaternion.Inverse(facing) * velocity,
                horizontalSpeed = horizontalSpeed,
                verticalSpeed = velocity.y,
                turnSpeed = turnSpeed,
                facing = facing,
                grounded = touchingGround || (!jumping && airTime < airborneDelay),
                touchingGround = touchingGround,
                timeInAir = airTime,
                moving = horizontalSpeed > movingSpeedThreshold,
                jumping = jumping,
                dashing = movementData.state.HasFlag(CharacterState.Dashing),
            };
        }

        /// <summary>
        /// Rotates the visual representation of the character smoothly to match the current facing direction
        /// this allows the character's movement to feel snappy without the character's visuals looking unnatural when rapidly changing direction.
        /// Call this every rendered frame (not from FixedUpdate), so the rotation updates smoothly rather than in physics-rate steps.
        /// </summary>
        /// <param name="deltaTime"></param>
        /// <param name="groundRotation">Rotation applied to the whole character by the ground they stand on since the
        /// last call (e.g. a spinning platform). The visuals take it on immediately instead of smoothing toward it, so
        /// they turn with the platform rather than lagging behind.</param>
        public void SmoothRotateVisuals(float deltaTime, Quaternion groundRotation)
        {
            // Turning the character's own transform would turn the whole character, not just its visuals.
            if (modelParent == null || modelParent == transform) { return; }

            lastRotation = groundRotation * lastRotation;
            // Exponential smoothing keeps the turn speed the same regardless of frame rate.
            float t = 1f - Mathf.Exp(-rotationFixSpeed * deltaTime);
            modelParent.rotation = Quaternion.Slerp(lastRotation, transform.rotation, t);
            lastRotation = modelParent.rotation;
        }

        /// <summary>Shows the model in the editor, where it isn't loaded, so the character can still be seen and placed.</summary>
        private void OnDrawGizmos()
        {
            if (modelSource == null || IsModelReady) { return; }
            Transform parent = modelParent != null ? modelParent : transform;
            modelSource.GetPreviewMeshes(previewMeshes);
            Gizmos.color = new Color(0.75f, 0.78f, 0.85f, 1f);
            foreach (CharacterModelSource.PreviewMesh preview in previewMeshes)
            {
                Gizmos.matrix = parent.localToWorldMatrix * preview.matrix;
                Gizmos.DrawMesh(preview.mesh);
            }
            Gizmos.matrix = Matrix4x4.identity;
        }
    }
}
