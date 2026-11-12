using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace Character
{
    /// <summary>
    /// Owns the character's model: asks a CharacterModelSource for it at runtime, swaps it in when it's ready, and hands
    /// it back to its source when it's replaced or the character goes away. While there's no model (still loading, or
    /// none could be loaded), it shows a placeholder, and if a model fails to load it falls back to another source.
    ///
    /// New models are created hidden, under an inactive staging transform, and swapped in the same frame the old one is
    /// removed, so two models are never seen at once. It also keeps an eye point at the model's eye height, for anything
    /// that should follow the model's size (the camera's first-person view, say), and warns when a model doesn't fit the
    /// character's collider.
    ///
    /// Animation lives elsewhere (AnimationController), and hears about new models through ModelLoaded.
    /// </summary>
    [DisallowMultipleComponent]
    public class CharacterModelHost : MonoBehaviour
    {
        [Header("Model")]
        [SerializeField] [Tooltip("Where this character's model comes from, and how to animate it")]
        private CharacterModelSource modelSource;
        [SerializeField] [Tooltip("Optional: loaded instead if Model Source fails to load (not if it's replaced first)")]
        private CharacterModelSource fallbackSource;
        [SerializeField] [Tooltip("The child transform the model is created under. It is turned smoothly to face the character's direction.")]
        private Transform modelParent;
        [SerializeField] [Tooltip("Load the model when the character starts. Turn off to load it from code with LoadModelAsync instead.")]
        private bool loadModelOnStart = true;

        [Header("While There's No Model")]
        [SerializeField] [Tooltip("Optional: shown while the character has no model (loading, or none could be loaded), hidden once one is in")]
        private GameObject placeholder;

        [Header("Model Size")]
        [SerializeField] [Tooltip("Optional: kept at the model's eye height above the character's feet, for things that should follow the model's size, such as a first-person camera's eye anchor")]
        private Transform eyePoint;
        [SerializeField] [Tooltip("Eye height (m) used while there's no model, or when the model's eyes can't be found")]
        private float defaultEyeHeight = 1.65f;
        [SerializeField] [Tooltip("Warn when a model is taller than the character's collider, or shorter than this fraction of it")]
        [Range(0f, 1f)] private float minimumHeightFraction = 0.6f;

        [Header("Collider")]
        [SerializeField] [Tooltip("Resize the character's collider to each model as it's swapped in. Off: the collider stays as " +
                                  "authored (models that don't fit it are warned about). Height: as tall as the model, its width " +
                                  "in proportion as authored. Height And Width: as tall as the model, and as wide as its " +
                                  "shoulders (times Collider Width Margin). Growing waits for room, as resizing does.")]
        private ColliderFit colliderFit = ColliderFit.Off;
        [SerializeField] [Tooltip("With Collider Fit set to Height And Width: the collider's width as a multiple of the model's " +
                                  "shoulder width (between its upper arm joints), leaving room for the arms")]
        [Min(0.1f)] private float colliderWidthMargin = 1.4f;

        private CharacterModel model;
        // The source the current model came from, which releases it.
        private CharacterModelSource modelOwner;
        private CancellationTokenSource pendingLoad;
        private Transform staging;
        private readonly List<CharacterModelSource.PreviewMesh> previewMeshes = new();

        /// <summary>The source of the current (or loading) model.</summary>
        public CharacterModelSource ModelSource => modelSource;

        /// <summary>
        /// Chooses the model to load on Start, for a character that's just been created (CharacterSpawner's model
        /// override). It does nothing to a model that's already loaded or loading: use LoadModelAsync to swap models.
        /// </summary>
        internal void SetModelSourceBeforeStart(CharacterModelSource source)
        {
            if (IsModelReady || IsLoadingModel) { return; }
            modelSource = source;
        }
        public CharacterModelSource FallbackSource { get => fallbackSource; set => fallbackSource = value; }
        /// <summary>The model in use, with its Animator, IK and translator, or null while there is none.</summary>
        public CharacterModel CurrentModel => model != null && model.Root != null ? model : null;
        /// <summary>The model instance in use, or null while there is none (not loaded yet, or still loading).</summary>
        public GameObject Model => CurrentModel?.Root;
        /// <summary>The model's Animator. May be null.</summary>
        public Animator Animator => CurrentModel?.Animator;
        /// <summary>IK for the model's hands, feet, elbows and knees, blended over its animations. May be null.</summary>
        public CharacterIK IK => CurrentModel?.IK;
        public bool IsModelReady => CurrentModel != null;
        public bool IsLoadingModel => pendingLoad != null;
        /// <summary>The transform models go under; turned to face the character's direction by AnimationController.</summary>
        public Transform ModelParent => modelParent;
        /// <summary>The transform kept at the model's eye height (see eyePoint), or null if there's none.</summary>
        public Transform EyePoint => eyePoint;
        /// <summary>The object shown while there's no model, or null if there's none.</summary>
        public GameObject Placeholder => placeholder;
        /// <summary>The model's eye height above the character's feet, in meters, at the character's current size.</summary>
        public float EyeHeight => baseEyeHeight * ScaleY;
        /// <summary>The model's height in meters at the character's current size, or 0 while there's no model.</summary>
        public float ModelHeightMeters => baseModelHeight * ScaleY;

        /// <summary>How the character's collider is fitted to its model. See colliderFit. Applies to models loaded after it's set.</summary>
        public ColliderFit ColliderFitting { get => colliderFit; set => colliderFit = value; }
        public float ColliderWidthMargin { get => colliderWidthMargin; set => colliderWidthMargin = Mathf.Max(0.1f, value); }
        /// <summary>Whether a collider fitted to the model is still waiting for room to grow.</summary>
        public bool IsFittingCollider => pendingColliderFit;

        // Eye height and model height with the model parent unscaled; the character's size (the model parent's scale,
        // see ACCharacterController.SetScale) multiplies them. Shoulder width likewise, or 0 if the model has none.
        private float baseEyeHeight, baseModelHeight, baseShoulderWidth;
        // The collider size the current model wants (at the character's authored size), until there's room for it.
        private bool pendingColliderFit;
        private float fitRadius, fitHeight;
        private MovementController movement;
        private float ScaleY => modelParent != null && modelParent != transform ? modelParent.localScale.y : 1f;

        /// <summary>Raised when a model has been swapped in, with the new model.</summary>
        public event Action<CharacterModel> ModelLoaded;
        /// <summary>Raised when the model is removed (replaced, unloaded, or the character destroyed), with the old model.</summary>
        public event Action<CharacterModel> ModelUnloaded;
        /// <summary>
        /// Raised just before the model is replaced by another (then ModelUnloaded and ModelLoaded follow, in the same
        /// frame), with the model going away, while it's still animating as it was: for carrying what it was doing over
        /// to the new one (see ICharacterAnimationDriver.Retire). Not raised when it's unloaded with nothing in its place.
        /// </summary>
        public event Action<CharacterModel> ModelReplacing;

        private void Awake()
        {
            if (modelParent == null) { modelParent = transform; }
            movement = GetComponent<MovementController>();
            baseEyeHeight = defaultEyeHeight;
            PlaceEyePoint();
            ShowPlaceholder(true);
        }

        private void Start()
        {
            if (loadModelOnStart && modelSource != null && !IsModelReady && !IsLoadingModel) { _ = LoadModelAsync(modelSource); }
        }

        private void FixedUpdate()
        {
            // A fitted collider that didn't have room to grow tries again each physics step.
            if (pendingColliderFit) { TryFitCollider(); }
        }

        private void OnDestroy()
        {
            CancelLoad();
            ReleaseModel();
        }

        /// <summary>
        /// Replaces the character's model with one from the given source, which creates it asynchronously. The current
        /// model stays until the new one is ready, so the character is never left invisible mid-swap. If the source
        /// fails, the fallback source is tried. Returns whether a model from either was loaded: false if both failed, or
        /// if another model was asked for (or the character destroyed) first. Given null, removes the model.
        /// </summary>
        public async Awaitable<bool> LoadModelAsync(CharacterModelSource source)
        {
            CancelLoad();
            modelSource = source;
            if (source == null)
            {
                ReleaseModel();
                return false;
            }

            LoadResult result = await CreateModelAsync(source);
            if (result == LoadResult.Failed && fallbackSource != null && fallbackSource != source && this != null)
            {
                Debug.LogWarning($"[{nameof(CharacterModelHost)}] Using the fallback model '{fallbackSource.name}' instead of '{source.name}'.", this);
                result = await CreateModelAsync(fallbackSource);
            }
            return result == LoadResult.Loaded;
        }

        /// <summary>Removes the current model (and cancels any loading one), leaving the character without one.</summary>
        public void UnloadModel()
        {
            CancelLoad();
            ReleaseModel();
        }

        private enum LoadResult { Loaded, Failed, Cancelled }

        private async Awaitable<LoadResult> CreateModelAsync(CharacterModelSource source)
        {
            // Cancelled if another model is asked for, or the character is destroyed, before this one is ready.
            var cancellation = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
            pendingLoad = cancellation;
            CharacterModel created;
            try
            {
                created = await source.CreateModelAsync(new CharacterModelContext(gameObject, Staging()), cancellation.Token);
            }
            catch (OperationCanceledException)
            {
                return LoadResult.Cancelled;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                return cancellation.IsCancellationRequested ? LoadResult.Cancelled : LoadResult.Failed;
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
                if (!wanted) { return LoadResult.Cancelled; }
                Debug.LogWarning($"[{nameof(CharacterModelHost)}] '{source.name}' didn't create a model.", this);
                return LoadResult.Failed;
            }
            if (!wanted)
            {
                // The source finished without noticing it was cancelled; the character no longer wants this model.
                source.ReleaseModel(created);
                return LoadResult.Cancelled;
            }

            SwapIn(created, source);
            return LoadResult.Loaded;
        }

        /// <summary>Removes the old model and shows the new one in the same frame, so they're never seen together.</summary>
        private void SwapIn(CharacterModel created, CharacterModelSource source)
        {
            if (CurrentModel != null)
            {
                ModelReplacing?.Invoke(model);
                // Feet already planted stay planted, rather than fading back in on the new model.
                FootPlacementIK oldFeet = model.Root.GetComponentInChildren<FootPlacementIK>();
                FootPlacementIK newFeet = created.Root.GetComponentInChildren<FootPlacementIK>(true);
                if (oldFeet != null && newFeet != null) { newFeet.ContinueFrom(oldFeet); }
            }
            ReleaseModel();
            model = created;
            modelOwner = source;

            Transform root = created.Root.transform;
            root.SetParent(modelParent, false); // Keeps the placement the source gave it under the staging transform.
            MeasureModel(created);
            created.Root.SetActive(true);
            ShowPlaceholder(false);
            ModelLoaded?.Invoke(created);
        }

        private void ReleaseModel()
        {
            pendingColliderFit = false; // The fit was for the model going away.
            CharacterModel old = model;
            CharacterModelSource owner = modelOwner;
            model = null;
            modelOwner = null;
            if (old == null) { return; }
            if (owner != null) { owner.ReleaseModel(old); }
            else if (old.Root != null) { Destroy(old.Root); }
            if (this == null) { return; } // Being destroyed along with the character.
            baseEyeHeight = defaultEyeHeight;
            baseModelHeight = 0f;
            PlaceEyePoint();
            ShowPlaceholder(true);
            ModelUnloaded?.Invoke(old);
        }

        private void CancelLoad()
        {
            CancellationTokenSource cancellation = pendingLoad;
            pendingLoad = null;
            cancellation?.Cancel();
        }

        /// <summary>The inactive transform models are created under, beneath the model parent.</summary>
        private Transform Staging()
        {
            if (staging != null) { return staging; }
            var holder = new GameObject("Model Staging");
            holder.SetActive(false);
            staging = holder.transform;
            staging.SetParent(modelParent, false);
            return staging;
        }

        private void ShowPlaceholder(bool show)
        {
            if (placeholder != null && placeholder.activeSelf != show) { placeholder.SetActive(show); }
        }

        /// <summary>Works out the model's height and eye height, and warns if it doesn't fit the character's collider.</summary>
        private void MeasureModel(CharacterModel created)
        {
            // Measured as the model stands, at the character's current size, then stored per unit of that size.
            float size = Mathf.Max(ScaleY, 1e-4f);
            baseModelHeight = CharacterModelSource.MeasureHeight(created.Root) / size;
            baseEyeHeight = FindEyeHeight(created) / size;
            baseShoulderWidth = FindShoulderWidth(created);
            PlaceEyePoint();
            if (colliderFit != ColliderFit.Off && baseModelHeight > 0f)
            {
                PlanColliderFit();
                return; // Fitted, so it can't fail to fit.
            }

            if (ModelHeightMeters <= 0f || !TryGetComponent(out CapsuleCollider capsule)) { return; }
            float colliderHeight = capsule.height * transform.lossyScale.y;
            if (ModelHeightMeters > colliderHeight + 0.01f || ModelHeightMeters < colliderHeight * minimumHeightFraction)
            {
                Debug.LogWarning($"[{nameof(CharacterModelHost)}] The model '{created.Root.name}' is {ModelHeightMeters:0.00} m tall, " +
                                 $"but the character's collider is {colliderHeight:0.00} m. Set its source's Height to Fit To Height, " +
                                 "or change its Scale.", this);
            }
        }

        /// <summary>
        /// The eye height above the character's feet: from the eye bones if the model has them, else partway up the
        /// head (eyes sit a little under halfway from the head bone at the skull's base to the top of the head), else
        /// most of the way up the model. Measured as the model was created, before it's animated, so it's the rest pose.
        /// </summary>
        private float FindEyeHeight(CharacterModel created)
        {
            float HeightOf(Vector3 point) => transform.InverseTransformPoint(point).y;
            Animator animator = created.Animator;
            if (animator != null && animator.isHuman)
            {
                Transform leftEye = animator.GetBoneTransform(HumanBodyBones.LeftEye);
                Transform rightEye = animator.GetBoneTransform(HumanBodyBones.RightEye);
                if (leftEye != null && rightEye != null) { return HeightOf(0.5f * (leftEye.position + rightEye.position)); }
                Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
                if (head != null && ModelHeightMeters > 0f)
                {
                    float headHeight = HeightOf(head.position);
                    float top = HeightOf(modelParent.position) + ModelHeightMeters;
                    if (top > headHeight) { return headHeight + 0.45f * (top - headHeight); }
                }
            }
            return ModelHeightMeters > 0f ? 0.93f * ModelHeightMeters : defaultEyeHeight * ScaleY;
        }

        /// <summary>
        /// The distance between the model's upper arm joints, across the character, with the model parent unscaled: how
        /// broad its shoulders are. 0 if it isn't a humanoid.
        /// </summary>
        private float FindShoulderWidth(CharacterModel created)
        {
            Animator animator = created.Animator;
            if (animator == null || !animator.isHuman) { return 0f; }
            Transform left = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm), right = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            if (left == null || right == null) { return 0f; }
            // In the model parent's space, which takes out its scale (the character's size) and its turn.
            Vector3 across = modelParent.InverseTransformPoint(left.position) - modelParent.InverseTransformPoint(right.position);
            return new Vector2(across.x, across.z).magnitude;
        }

        /// <summary>Works out the collider the model wants (see colliderFit), and fits it if there's room.</summary>
        private void PlanColliderFit()
        {
            if (movement == null) { return; }
            fitHeight = baseModelHeight;
            fitRadius = colliderFit == ColliderFit.HeightAndWidth && baseShoulderWidth > 0f
                ? 0.5f * baseShoulderWidth * colliderWidthMargin
                : movement.BaseRadius * fitHeight / Mathf.Max(movement.BaseHeight, 1e-4f);
            pendingColliderFit = true;
            TryFitCollider();
        }

        private void TryFitCollider()
        {
            if (movement == null) { pendingColliderFit = false; return; }
            if (movement.SetBaseCapsule(fitRadius, fitHeight)) { pendingColliderFit = false; }
        }

        /// <summary>
        /// Updates what depends on the character's size (the eye point) after its model parent has been scaled. Called by
        /// ACCharacterController.SetScale.
        /// </summary>
        public void RefreshScale() => PlaceEyePoint();

        private void PlaceEyePoint()
        {
            if (eyePoint != null) { eyePoint.localPosition = new Vector3(eyePoint.localPosition.x, EyeHeight, eyePoint.localPosition.z); }
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

    /// <summary>How a CharacterModelHost fits the character's collider to its model.</summary>
    public enum ColliderFit
    {
        /// <summary>The collider stays as authored.</summary>
        Off,
        /// <summary>As tall as the model, its width in proportion to its height as authored.</summary>
        Height,
        /// <summary>As tall as the model, and as wide as its shoulders, with a margin for its arms.</summary>
        HeightAndWidth,
    }
}
