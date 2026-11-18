using System;
using System.Collections.Generic;
using System.Linq;
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
    /// character's body.
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
        [SerializeField] [Tooltip("Warn when a model is taller than the character's body, or shorter than this fraction of it")]
        [Range(0f, 1f)] private float minimumHeightFraction = 0.6f;

        [Header("Body")]
        [SerializeField] [Tooltip("Fit the character's body to each model as it's swapped in. Off: the body stays as authored " +
                                  "(models that don't fit it are warned about). Height: the body as authored, scaled to be as " +
                                  "tall as the model. Model's Body: the model's own (its source's Body Definition), or for a " +
                                  "source without one, fitted to the model as it loads (a humanoid: as tall as it is and as wide " +
                                  "as its shoulders, times Body Width Margin; anything else: a box its size). Growing waits " +
                                  "for room, as resizing does.")]
        [UnityEngine.Serialization.FormerlySerializedAs("colliderFit")]
        private BodyFit bodyFit = BodyFit.Off;
        [SerializeField] [Tooltip("With Body Fit set to Model's Body, for a humanoid model whose source has no Body Definition: " +
                                  "the body's width as a multiple of the model's shoulder width (between its upper arm joints), " +
                                  "leaving room for the arms")]
        [UnityEngine.Serialization.FormerlySerializedAs("colliderWidthMargin")]
        [Min(0.1f)] private float bodyWidthMargin = BodyFitting.DefaultShoulderMargin;

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
        /// <summary>
        /// Whether the model source's model is loaded on Start. Set it before Start (straight after instantiating the
        /// character) to leave the character without a model (a server's, say): it still moves the body its model would
        /// give it (see FitBodyFromSource).
        /// </summary>
        public bool LoadModelOnStart { get => loadModelOnStart; set => loadModelOnStart = value; }

        internal void SetModelSourceBeforeStart(CharacterModelSource source)
        {
            if (IsModelReady || IsLoadingModel) { return; }
            modelSource = source;
        }
        /// <summary>
        /// Loaded instead if the model source fails to load (not if it's replaced first). May be null.
        /// </summary>
        public CharacterModelSource FallbackSource { get => fallbackSource; set => fallbackSource = value; }
        /// <summary>The model in use, with its Animator, IK and translator, or null while there is none.</summary>
        public CharacterModel CurrentModel => model != null && model.Root != null ? model : null;
        /// <summary>The model instance in use, or null while there is none (not loaded yet, or still loading).</summary>
        public GameObject Model => CurrentModel?.Root;
        /// <summary>The model's Animator. May be null.</summary>
        public Animator Animator => CurrentModel?.Animator;
        /// <summary>IK for the model's hands, feet, elbows and knees, blended over its animations. May be null.</summary>
        public CharacterIK IK => CurrentModel?.IK;
        /// <summary>
        /// The model's hitboxes (see CharacterHitboxes): what meets the character, and what it reports. Null without a
        /// model, or for one without hitboxes. A model swapped in brings its own.
        /// </summary>
        public CharacterHitboxes Hitboxes => CurrentModel?.Root != null ? CurrentModel.Root.GetComponentInChildren<CharacterHitboxes>(true) : null;
        /// <summary>Whether a model is in use.</summary>
        public bool IsModelReady => CurrentModel != null;
        /// <summary>Whether a model is being loaded (the current one, if any, stays until it's ready).</summary>
        public bool IsLoadingModel => pendingLoad != null;
        /// <summary>The transform models go under; turned to face the character's direction by AnimationController.</summary>
        public Transform ModelParent => modelParent;
        /// <summary>The transform kept at the model's eye height (see eyePoint), or null if there's none.</summary>
        public Transform EyePoint => eyePoint;
        /// <summary>The object shown while there's no model, or null if there's none.</summary>
        public GameObject Placeholder => placeholder;
        /// <summary>The model's eye height above the character's feet, in meters, at the character's current size.</summary>
        public float EyeHeight => baseEyeHeight * ScaleY;

        // The share of the eye height the eye point is at now (see EyeHeightShare).
        private float eyeHeightShare = 1f;

        /// <summary>
        /// The share (0 to 1) of the eye height (EyeHeight) the eye point is kept at: lowered with the character's body as
        /// it crouches or lies down (see AnimationController), so a camera following it ducks with it. 1 standing.
        /// </summary>
        public float EyeHeightShare
        {
            get => eyeHeightShare;
            set
            {
                float share = Mathf.Clamp01(value);
                if (share == eyeHeightShare) { return; }
                eyeHeightShare = share;
                PlaceEyePoint();
            }
        }
        /// <summary>The model's height in meters at the character's current size, or 0 while there's no model.</summary>
        public float ModelHeightMeters => baseModelHeight * ScaleY;

        /// <summary>How the character's body is fitted to its model. See bodyFit. Applies to models loaded after it's set.</summary>
        public BodyFit BodyFit { get => bodyFit; set => bodyFit = value; }
        /// <summary>
        /// With Body Fit set to Model's Body, for a humanoid whose source has no Body Definition: the body's width as a
        /// multiple of the model's shoulder width (at least 0.1).
        /// </summary>
        public float BodyWidthMargin { get => bodyWidthMargin; set => bodyWidthMargin = Mathf.Max(0.1f, value); }
        /// <summary>Whether a body fitted to the model is still waiting for room to grow.</summary>
        public bool IsFittingBody => pendingBodyFit;

        // Eye height and model height with the model parent unscaled; the character's size (the model parent's scale,
        // see ACCharacterController.SetScale) multiplies them. Shoulder width and size likewise (0 if it has no shoulders).
        private float baseEyeHeight, baseModelHeight, baseShoulderWidth;
        private Vector3 baseModelSize;
        // The body the current model wants (at the character's authored size), until there's room for it.
        private bool pendingBodyFit;
        private BodyShape fitBody;
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
            FitBodyFromSource();
            if (loadModelOnStart && modelSource != null && !IsModelReady && !IsLoadingModel) { _ = LoadModelAsync(modelSource); }
        }

        // The source whose prepared body the character was given before its model loaded (see FitBodyFromSource), or null.
        private CharacterModelSource bodyFromSource;

        /// <summary>
        /// Fits the body the model will want (see bodyFit) from what its source measured when it was prepared (BodyFor),
        /// before the model is loaded, so the character moves its final body from its first step: the same body on a
        /// machine that loads the model later, or not at all (a server). When the model arrives it keeps it (see
        /// MeasureModel). Nothing to do with Body Fit off, or a source never prepared (fitted once the model loads).
        /// </summary>
        private void FitBodyFromSource()
        {
            bodyFromSource = null;
            if (movement == null || BodyFor(modelSource, movement) is not { } body) { return; }
            fitBody = body;
            bodyFromSource = modelSource;
            pendingBodyFit = true;
            TryFitBody();
        }

        // A fitted body that didn't have room to grow tries again each physics step.
        private void FixedUpdate() => TryFitBodyNow();

        /// <summary>
        /// Tries again to fit the body to the model, if it's waiting for room to grow, as each physics step does: for
        /// something that steps the physics itself (the tests' rig), whose steps don't run FixedUpdate.
        /// </summary>
        public void TryFitBodyNow()
        {
            if (pendingBodyFit) { TryFitBody(); }
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
                // What carries on from the old model does (see IModelHandover): feet and paws already planted stay
                // planted, rather than fading back in on the new model.
                foreach (IModelHandover handover in created.Root.GetComponentsInChildren<IModelHandover>(true))
                {
                    handover.ContinueFrom(model.Root);
                }
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
            pendingBodyFit = false; // The fit was for the model going away.
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

        /// <summary>Works out the model's height and eye height, and warns if it doesn't fit the character's body.</summary>
        private void MeasureModel(CharacterModel created)
        {
            // Measured as the model stands, at the character's current size, then stored per unit of that size.
            float size = Mathf.Max(ScaleY, 1e-4f);
            baseModelHeight = CharacterModelSource.MeasureHeight(created.Root) / size;
            // Across and front to back as the character faces (its model parent's space, which also takes out its scale).
            baseModelSize = CharacterModelSource.MeasureSize(created.Root, modelParent);
            baseEyeHeight = FindEyeHeight(created) / size;
            baseShoulderWidth = FindShoulderWidth(created);
            PlaceEyePoint();
            if (bodyFit != BodyFit.Off && baseModelHeight > 0f)
            {
                // Already given its source's prepared body (FitBodyFromSource): kept, not measured again, so the body
                // doesn't change when (or whether) the model arrives. A model from another source (a fallback) is fitted.
                if (bodyFromSource == null || bodyFromSource != modelOwner) { PlanBodyFit(); }
                return; // Fitted, so it can't fail to fit.
            }

            if (ModelHeightMeters <= 0f || movement == null) { return; }
            float bodyHeight = movement.Body.Height * transform.lossyScale.y;
            if (ModelHeightMeters > bodyHeight + 0.01f || ModelHeightMeters < bodyHeight * minimumHeightFraction)
            {
                Debug.LogWarning($"[{nameof(CharacterModelHost)}] The model '{created.Root.name}' is {ModelHeightMeters:0.00} m tall, " +
                                 $"but the character's body is {bodyHeight:0.00} m. Set its source's Height to Fit To Height, " +
                                 "or change its Scale.", this);
            }
        }

        /// <summary>The name of a transform marking a model's eyes, for one that isn't a humanoid (see FindEyeHeight).</summary>
        public const string EyesName = "Eyes";

        /// <summary>
        /// The eye height above the character's feet: from the eye bones if the model has them, else partway up the
        /// head (eyes sit a little under halfway from the head bone at the skull's base to the top of the head), else
        /// from a transform named Eyes on a model that isn't a humanoid (a fox, say), else most of the way up the model. Measured as the model was created, before it's animated, so it's the rest pose.
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
            // A model that isn't a humanoid (a fox, say) can mark where its eyes are with a transform named Eyes.
            Transform marked = created.Root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == EyesName);
            if (marked != null) { return HeightOf(marked.position); }
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

        /// <summary>Works out the body the model wants (see bodyFit), and fits it if there's room.</summary>
        private void PlanBodyFit()
        {
            if (movement == null) { return; }
            BodyShape? body = bodyFit == BodyFit.Height ? BodyFitting.ScaledToHeight(movement.AuthoredBody, baseModelHeight)
                // The source the model came from, not the one asked for: a fallback model has its own body.
                : modelOwner != null && modelOwner.ModelBody is { } own ? own
                : BodyFitting.ForModel(new Vector3(baseModelSize.x, baseModelHeight, baseModelSize.z), baseShoulderWidth, bodyWidthMargin);
            fitBody = body.Value;
            pendingBodyFit = true;
            TryFitBody();
        }

        /// <summary>
        /// The body (as authored, at the character's authored size) a character with this host and the given movement gets
        /// with a model from source, as it would fit its body to a model it loads (see bodyFit), from what source
        /// measured when it was prepared; null for the movement's own (Body Fit off, or the model not prepared). For
        /// working out what a character can do before it's spawned (see MovementSimulation).
        /// </summary>
        public BodyShape? BodyFor(CharacterModelSource source, MovementController movementController)
        {
            if (bodyFit == BodyFit.Off || source == null || !source.HasMeasurements) { return null; }
            if (bodyFit == BodyFit.ModelsBody && source.ModelBody is { } own) { return own; }
            // Without a body of its own (prepared before models had one), as tall as it is.
            return BodyFitting.ScaledToHeight(movementController.AuthoredBody, source.MeasuredHeight);
        }

        private void TryFitBody()
        {
            if (movement == null) { pendingBodyFit = false; return; }
            if (movement.SetBody(fitBody)) { pendingBodyFit = false; }
        }

        /// <summary>
        /// Updates what depends on the character's size (the eye point) after its model parent has been scaled. Called by
        /// ACCharacterController.SetScale.
        /// </summary>
        public void RefreshScale() => PlaceEyePoint();

        private void PlaceEyePoint()
        {
            if (eyePoint != null) { eyePoint.localPosition = new Vector3(eyePoint.localPosition.x, EyeHeight * eyeHeightShare, eyePoint.localPosition.z); }
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

    /// <summary>How a CharacterModelHost fits the character's body to its model.</summary>
    public enum BodyFit
    {
        /// <summary>The body stays as authored.</summary>
        Off,
        /// <summary>The body as authored, scaled to be as tall as the model.</summary>
        Height,
        /// <summary>The model's own body (its source's Body Definition), or one fitted to it as it loads.</summary>
        [InspectorName("Model's Body")] ModelsBody,
    }
}
