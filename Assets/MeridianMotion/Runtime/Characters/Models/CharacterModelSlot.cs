using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;

namespace MeridianMotion.Characters
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
    /// Animation lives elsewhere (MMotionAnimator), and hears about new models through ModelLoaded.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Meridian Motion/Models/Character Model Slot")]
    public partial class CharacterModelSlot : MonoBehaviour
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

        [Header("Hitboxes")]
        [SerializeField] [Tooltip("The layer its models' hitboxes are on (see Character Hitboxes): one of the project's layers " +
                                  "(Project Settings > Tags and Layers), kept out of the movement's layers (MMotionMotor's " +
                                  "Movement Layer Mask). The Character Doctor adds the CharacterHitbox layer if the project " +
                                  "hasn't got it, or sets this to a layer it has.")]
        private string hitboxLayer = CharacterHitboxes.LayerName;

        private LoadedCharacterModel model;
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
        public LoadedCharacterModel LoadedModel => model != null && model.Root != null ? model : null;
        /// <summary>The model instance in use, or null while there is none (not loaded yet, or still loading).</summary>
        public GameObject ModelRoot => LoadedModel?.Root;
        /// <summary>The model's Animator. May be null.</summary>
        public Animator Animator => LoadedModel?.Animator;
        /// <summary>IK for the model's hands, feet, elbows and knees, blended over its animations. May be null.</summary>
        public HumanoidIK IK => LoadedModel?.IK;
        /// <summary>
        /// The model's hitboxes (see CharacterHitboxes): what meets the character, and what it reports. Null without a
        /// model, or for one without hitboxes. A model swapped in brings its own.
        /// </summary>
        public CharacterHitboxes Hitboxes => LoadedModel?.Root != null ? LoadedModel.Root.GetComponentInChildren<CharacterHitboxes>(true) : null;
        /// <summary>Whether a model is in use.</summary>
        public bool IsModelReady => LoadedModel != null;
        /// <summary>Whether a model is being loaded (the current one, if any, stays until it's ready).</summary>
        public bool IsLoadingModel => pendingLoad != null;
        /// <summary>The transform models go under; turned to face the character's direction by MMotionAnimator.</summary>
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
        /// it crouches or lies down (see MMotionAnimator), so a camera following it ducks with it. 1 standing.
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

        /// <summary>The name of the layer its models' hitboxes are on (see the field's tooltip).</summary>
        public string HitboxLayer
        {
            get => hitboxLayer;
            set
            {
                hitboxLayer = value;
                Hitboxes?.UseLayer(HitboxLayerIndex);
            }
        }

        /// <summary>The number of the layer its models' hitboxes are on: -1 if the project has no layer by that name.</summary>
        public int HitboxLayerIndex => string.IsNullOrEmpty(hitboxLayer) ? -1 : LayerMask.NameToLayer(hitboxLayer);
        /// <summary>
        /// With Body Fit set to Model's Body, for a humanoid whose source has no Body Definition: the body's width as a
        /// multiple of the model's shoulder width (at least 0.1).
        /// </summary>
        public float BodyWidthMargin { get => bodyWidthMargin; set => bodyWidthMargin = Mathf.Max(0.1f, value); }
        /// <summary>Whether a body fitted to the model is still waiting for room to grow.</summary>
        public bool IsFittingBody => pendingBodyFit;

        // Eye height and model height with the model parent unscaled; the character's size (the model parent's scale,
        // see MMotionCharacter.SetScale) multiplies them. Shoulder width and size likewise (0 if it has no shoulders).
        private float baseEyeHeight, baseModelHeight, baseShoulderWidth;
        private Vector3 baseModelSize;
        // The body the current model wants (at the character's authored size), until there's room for it.
        private bool pendingBodyFit;
        private BodyShape fitBody;
        private MMotionMotor movement;
        private float ScaleY => modelParent != null && modelParent != transform ? modelParent.localScale.y : 1f;

        /// <summary>Raised when a model has been swapped in, with the new model.</summary>
        public event Action<LoadedCharacterModel> ModelLoaded;
        /// <summary>Raised when the model is removed (replaced, unloaded, or the character destroyed), with the old model.</summary>
        public event Action<LoadedCharacterModel> ModelUnloaded;
        /// <summary>
        /// Raised just before the model is replaced by another (then ModelUnloaded and ModelLoaded follow, in the same
        /// frame), with the model going away, while it's still animating as it was: for carrying what it was doing over
        /// to the new one (see ICharacterAnimationDriver.Retire). Not raised when it's unloaded with nothing in its place.
        /// </summary>
        public event Action<LoadedCharacterModel> ModelReplacing;

        private void Awake()
        {
            if (modelParent == null) { modelParent = transform; }
            movement = GetComponent<MMotionMotor>();
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

    /// <summary>How a CharacterModelSlot fits the character's body to its model.</summary>
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
