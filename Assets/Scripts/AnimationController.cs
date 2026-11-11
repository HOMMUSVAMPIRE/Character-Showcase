using System;
using UnityEngine;

namespace Character
{
    /// <summary>
    /// The character's link between movement and animation. It turns each frame's movement into a
    /// CharacterAnimationState, passes that state and the movement events to the model's animation translator, and
    /// turns the model smoothly to face the character's direction.
    ///
    /// Nothing here depends on a particular model, how it's stored, its Animator Controller or animation system: the
    /// model comes from the CharacterModelHost, and how it's animated from the translator its source names, so a model
    /// can be replaced (or swapped at runtime) without touching the character.
    /// </summary>
    [DisallowMultipleComponent]
    public class AnimationController : MonoBehaviour, ICharacterAnimationStateSource
    {
        [SerializeField] [Tooltip("A reference to the parent character controller")]
        private ACCharacterController characterController;
        [SerializeField] [Tooltip("The character's movement, whose events this passes on. Found automatically if left empty.")]
        private MovementController movementController;
        [SerializeField] [Tooltip("The character's model, which this animates. Found automatically if left empty.")]
        private CharacterModelHost modelHost;

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

        /// <summary>The character's animation state as of the latest frame.</summary>
        public CharacterAnimationState AnimationState { get; private set; }
        /// <summary>The character's model, which this animates.</summary>
        public CharacterModelHost ModelHost => modelHost;

        /// <summary>Raised for each movement event (one flag at a time), with the animation state when it happened. For
        /// effects, sound and anything else that reacts to the character's actions.</summary>
        public event Action<MovementEvents, CharacterAnimationState> MovementEventReceived;

        private void Awake()
        {
            if (movementController == null) { movementController = GetComponent<MovementController>(); }
            if (modelHost == null) { modelHost = GetComponent<CharacterModelHost>(); }
        }

        private void OnEnable()
        {
            if (movementController != null) { movementController.MovementEventRaised += HandleMovementEvent; }
            if (modelHost != null)
            {
                modelHost.ModelLoaded += HandleModelLoaded;
                modelHost.ModelUnloaded += HandleModelUnloaded;
                if (modelHost.CurrentModel != null) { HandleModelLoaded(modelHost.CurrentModel); }
            }
        }

        private void OnDisable()
        {
            if (movementController != null) { movementController.MovementEventRaised -= HandleMovementEvent; }
            if (modelHost != null)
            {
                modelHost.ModelLoaded -= HandleModelLoaded;
                modelHost.ModelUnloaded -= HandleModelUnloaded;
            }
        }

        private void HandleModelLoaded(CharacterModel model)
        {
            driver = model.Translator != null ? model.Translator.CreateDriver(model.Root, model.Animator) : null;
            if (modelHost.ModelParent != null) { lastRotation = modelHost.ModelParent.rotation; }
        }

        private void HandleModelUnloaded(CharacterModel model) => driver = null;

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
            float speedScale = movementController != null ? movementController.SpeedScale : 1f;
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
                moving = horizontalSpeed > movingSpeedThreshold * speedScale, // The threshold is for the authored size.
                jumping = jumping,
                dashing = movementData.state.HasFlag(CharacterState.Dashing),
                scale = modelHost != null && modelHost.ModelParent != null && modelHost.ModelParent != transform
                    ? modelHost.ModelParent.localScale : Vector3.one,
                speedScale = speedScale,
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
            Transform modelParent = modelHost != null ? modelHost.ModelParent : null;
            if (modelParent == null || modelParent == transform) { return; }

            lastRotation = groundRotation * lastRotation;
            // Exponential smoothing keeps the turn speed the same regardless of frame rate.
            float t = 1f - Mathf.Exp(-rotationFixSpeed * deltaTime);
            modelParent.rotation = Quaternion.Slerp(lastRotation, transform.rotation, t);
            lastRotation = modelParent.rotation;
        }
    }
}
