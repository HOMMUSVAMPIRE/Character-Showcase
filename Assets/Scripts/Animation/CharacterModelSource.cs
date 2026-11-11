using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace Character
{
    /// <summary>
    /// Where a character's model comes from, and how to set it up on the character. The AnimationController asks its
    /// source for a model at runtime with CreateModelAsync, so a character's look is changed by giving it a different
    /// source, and nothing about the character depends on how the model is stored.
    ///
    /// Subclasses decide how the model is obtained: DirectCharacterModelSource instantiates a prefab it references;
    /// others could load one from Addressables or an asset bundle (releasing it again in ReleaseModel), or assemble one
    /// from a character creator's choices. The settings here (placement, IK, the translator that animates the model)
    /// apply however the model is obtained, and SetUpModel applies them, so a subclass only has to produce the model.
    /// </summary>
    public abstract class CharacterModelSource : ScriptableObject
    {
        [Header("Animation")]
        [Tooltip("Turns the character's movement into this model's animations")]
        [SerializeField] private CharacterAnimationTranslator translator;
        [Tooltip("Optional: replaces the model's Animator Controller")]
        [SerializeField] private RuntimeAnimatorController animatorController;
        [Tooltip("Optional: replaces the model's Avatar")]
        [SerializeField] private Avatar avatar;

        [Header("Placement")]
        [Tooltip("Keep the model at its own size (times Scale), or scale it to Fit Height. Either way, the character warns " +
                 "if the model is too tall or too short for its collider.")]
        [SerializeField] private ModelHeight height = ModelHeight.KeepModelSize;
        [Tooltip("With Height set to Fit To Height, the model is scaled to stand this tall, in meters")]
        [Min(0.01f)] [SerializeField] private float fitHeight = 1.8f;
        [Tooltip("Offset of the model from the character's feet, in the character's space")]
        [SerializeField] private Vector3 positionOffset = Vector3.zero;
        [Tooltip("Rotation of the model relative to the character, in degrees")]
        [SerializeField] private Vector3 rotationOffset = Vector3.zero;
        [Tooltip("Uniform scale applied to the model")]
        [Min(0.001f)] [SerializeField] private float scale = 1f;
        [Tooltip("Measure where the model's soles are when it's loaded (once for all copies of the same model), rather " +
                 "than using Ground Offset and Animated Ground Offset. On for sources made by hand or in code, whose " +
                 "models haven't been measured; Prepare Character Models measures its models' and turns this off.")]
        [SerializeField] private bool measureGroundOnLoad = true;
        [Tooltip("How far above its origin the model's soles are as it stands, in the model's own units (before Scale and " +
                 "Fit To Height), with its feet where foot placement holds them (or, for a model without foot placement, " +
                 "where its animation puts them): the model is lowered by this, so its feet rest on the ground. Negative " +
                 "if its feet sink below its origin. Prepare Character Models measures it from the model standing idle.")]
        [SerializeField] private float groundOffset;
        [Tooltip("The same, with the feet where its animation puts them. A model of other proportions than the clips' own " +
                 "has its feet a little higher or lower there than at the IK goals foot placement uses: foot placement " +
                 "makes up the difference while it's off or fading (in the air, dashing).")]
        [SerializeField] private float animatedGroundOffset;
        [Tooltip("Degrees the model is turned about the up axis so it faces the character's forward (+Z), before Rotation " +
                 "Offset: for a model made facing another way, which its animation keeps. Prepare Character Models " +
                 "measures it, to the nearest quarter turn (as do sources measuring on load).")]
        [SerializeField] private float facingCorrection;

        [Header("Setup")]
        [Tooltip("Add CharacterIK to humanoid models that don't have it, so their hands and feet can be posed with IK")]
        [SerializeField] private bool addIK = true;
        [Tooltip("Add FootPlacementIK to humanoid models that don't have it, planting their feet on uneven ground")]
        [SerializeField] private bool addFootPlacement = true;
        [Tooltip("Put the model on the same layer as the character, as cameras and physics queries expect")]
        [SerializeField] private bool matchCharacterLayer = true;
        [Tooltip("Keep animating while the model can't be seen (e.g. hidden in first person), so its shadow still animates")]
        [SerializeField] private bool alwaysAnimate = true;

        public CharacterAnimationTranslator Translator { get => translator; set => translator = value; }
        public RuntimeAnimatorController AnimatorController { get => animatorController; set => animatorController = value; }
        public Avatar Avatar { get => avatar; set => avatar = value; }
        public Vector3 PositionOffset { get => positionOffset; set => positionOffset = value; }
        public Vector3 RotationOffset { get => rotationOffset; set => rotationOffset = value; }
        public float Scale { get => scale; set => scale = Mathf.Max(0.001f, value); }
        public float GroundOffset { get => groundOffset; set => groundOffset = value; }
        public float AnimatedGroundOffset { get => animatedGroundOffset; set => animatedGroundOffset = value; }
        public bool MeasureGroundOnLoad { get => measureGroundOnLoad; set => measureGroundOnLoad = value; }
        public float FacingCorrection { get => facingCorrection; set => facingCorrection = value; }
        public ModelHeight Height { get => height; set => height = value; }
        public float FitHeight { get => fitHeight; set => fitHeight = Mathf.Max(0.01f, value); }

        /// <summary>
        /// Creates the model under the context's parent and sets it up for the character (usually by calling
        /// SetUpModelAsync), then hands it back. It may take as many frames as it needs; the character moves normally
        /// meanwhile, and keeps its current model until this one is ready.
        ///
        /// The context's parent is inactive, so the model is created hidden, and its components don't start, until the
        /// character swaps it in: the old model and the new one are never both seen. Create the model under it (or move
        /// it there) rather than anywhere visible.
        ///
        /// If the token is cancelled (the character was destroyed or given another model first), stop, clean up
        /// anything created or loaded so far (destroy instances, release handles), and throw OperationCanceledException,
        /// which Awaitable.NextFrameAsync(cancellationToken) and friends do for you. Return null if the model couldn't be
        /// created; the character then keeps the model it had.
        /// </summary>
        public abstract Awaitable<CharacterModel> CreateModelAsync(CharacterModelContext context, CancellationToken cancellationToken);

        /// <summary>
        /// Gets rid of a model this source created, when the character is done with it. By default it's destroyed;
        /// a source that loaded it from somewhere else releases it there (Addressables.ReleaseInstance, say).
        /// </summary>
        public virtual void ReleaseModel(CharacterModel model)
        {
            if (model == null || model.Root == null) { return; }
            // Destroy takes effect at the end of the frame; hide it now, so it's gone the moment its replacement appears.
            model.Root.SetActive(false);
            Destroy(model.Root);
        }

        /// <summary>
        /// Prepares a created model for the character: places it under the context's parent, puts it on the
        /// character's layer, applies the Animator overrides, and adds IK to humanoids if asked to. Virtual and
        /// awaitable, so subclasses can add customisation (a character creator's meshes, materials or blend shapes),
        /// waiting for whatever they need to load first.
        /// </summary>
        protected virtual Awaitable SetUpModelAsync(GameObject model, CharacterModelContext context, CancellationToken cancellationToken)
        {
            Transform modelTransform = model.transform;
            if (modelTransform.parent != context.Parent) { modelTransform.SetParent(context.Parent, false); }
            if (matchCharacterLayer) { SetLayerRecursively(model, context.Character.layer); }

            // The model may be inactive (see CreateModelAsync), so include inactive objects in every search.
            Animator modelAnimator = model.GetComponentInChildren<Animator>(true);
            FootPlacementIK feet = null;
            if (modelAnimator != null)
            {
                if (animatorController != null) { modelAnimator.runtimeAnimatorController = animatorController; }
                if (avatar != null) { modelAnimator.avatar = avatar; }
                modelAnimator.applyRootMotion = false; // The character is moved by its MovementController, not by animation.
                if (alwaysAnimate) { modelAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate; }

                // IK works with humanoids only. It must live on the Animator's GameObject, as OnAnimatorIK requires.
                // (Not `??`, which bypasses Unity's null check.)
                if (modelAnimator.isHuman)
                {
                    GameObject animated = modelAnimator.gameObject;
                    if (addIK && !animated.TryGetComponent(out CharacterIK _)) { animated.AddComponent<CharacterIK>(); }
                    if (addFootPlacement && !animated.TryGetComponent(out FootPlacementIK _)) { animated.AddComponent<FootPlacementIK>(); }
                    animated.TryGetComponent(out feet);
                }
            }

            // Where the soles are: as stored (measured by Prepare Character Models), or measured now, set up as it is.
            float soles = groundOffset, animatedSoles = animatedGroundOffset, facing = facingCorrection;
            if (measureGroundOnLoad && modelAnimator != null && TryGetStandingPose(model, modelAnimator, out StandingPose standing))
            {
                bool placesFeet = feet != null && feet.enabled && feet.weight > 0f;
                soles = placesFeet ? standing.PlacedSoleHeight : standing.SoleHeight;
                animatedSoles = standing.SoleHeight;
                facing = standing.FacingCorrection;
            }
            // Foot placement plants the feet by their soles, and makes up for the animation's own sole height when off.
            if (feet != null)
            {
                feet.FloorOffset = soles;
                feet.AnimatedFloorOffset = animatedSoles;
            }

            modelTransform.SetLocalPositionAndRotation(positionOffset, ModelRotation(facing));
            // Measured unscaled, relative to the parent, so the character's own size (the model parent's scale) doesn't
            // count: a model fitted to 1.8 m on a character twice the size is 3.6 m tall, like any other model would be.
            modelTransform.localScale = Vector3.one;
            float ownHeight = height == ModelHeight.FitToHeight ? MeasureHeight(model, context.Parent) : 0f;
            float modelScale = ModelScaleFor(ownHeight);
            modelTransform.localScale = Vector3.one * modelScale;
            // Feet on the ground: lowered by the sole height at the model's size. Being inside the character's model
            // parent, the offset is scaled with the character, so the feet stay on the ground at any size, however
            // stretched.
            modelTransform.localPosition = positionOffset + Vector3.down * (soles * modelScale);
            return Completed();
        }

        /// <summary>
        /// The asset every model this source makes is a copy of (its prefab, say), so that measuring one model (see
        /// Measure Ground On Load) serves for the rest; or null if each may differ, as a character creator's might, so
        /// each is measured as it loads. Measurements are kept until the asset, Animator Controller or Avatar changes,
        /// the source is edited, ForgetMeasurement is called, or play mode starts again.
        /// </summary>
        protected virtual UnityEngine.Object ModelTemplate => null;

        // The last measurement on load, and what it was measured for.
        private struct Measurement
        {
            public UnityEngine.Object template;
            public RuntimeAnimatorController controller;
            public Avatar avatar;
            public int session;
            public bool measured;
            public StandingPose pose;
        }
        [System.NonSerialized] private Measurement lastMeasurement;
        // Counts play mode sessions, so a measurement isn't kept from one to the next (as it would be, with domain
        // reloading turned off), in case the model was changed in between.
        private static int session;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void NewSession() => session++;

        /// <summary>Drops the measurement kept on load (see ModelTemplate), so the next model loaded is measured.</summary>
        public void ForgetMeasurement() => lastMeasurement = default;

        protected virtual void OnValidate() => ForgetMeasurement();

        /// <summary>Measures how the model stands, or reuses the measurement of the last model, if it's a copy of the same.</summary>
        private bool TryGetStandingPose(GameObject model, Animator modelAnimator, out StandingPose pose)
        {
            UnityEngine.Object template = ModelTemplate;
            RuntimeAnimatorController controller = modelAnimator.runtimeAnimatorController;
            Avatar modelAvatar = modelAnimator.avatar;
            Measurement last = lastMeasurement;
            if (template != null && last.template == template && last.controller == controller && last.avatar == modelAvatar
                && last.session == session)
            {
                pose = last.pose;
                return last.measured;
            }

            bool measured = StandingPose.TryMeasure(model, controller, null, out pose);
            lastMeasurement = template != null
                ? new Measurement { template = template, controller = controller, avatar = modelAvatar, session = session, measured = measured, pose = pose }
                : default;
            return measured;
        }

        /// <summary>An Awaitable that's already finished, for overrides with nothing to wait for.</summary>
        protected static Awaitable Completed()
        {
            var completion = new AwaitableCompletionSource();
            completion.SetResult();
            return completion.Awaitable;
        }

        /// <summary>A mesh of the model and where it sits relative to the character's model parent.</summary>
        public struct PreviewMesh
        {
            public Mesh mesh;
            public Matrix4x4 matrix;
        }

        /// <summary>
        /// The model's meshes in their default pose, placed as SetUpModel would place them, for showing the model where
        /// it isn't loaded (in the editor, or a character select screen). Sources whose model isn't at hand without
        /// loading it give none.
        /// </summary>
        public virtual void GetPreviewMeshes(List<PreviewMesh> results) => results.Clear();

        /// <summary>
        /// How tall a model is in meters, as it stands now: from the lowest to the highest point of its meshes. Measured
        /// from the meshes themselves, in their bind pose, so a freshly created model (not yet animated) gives its rest
        /// height. Skinned meshes' renderer bounds aren't used, as they're padded.
        /// </summary>
        public static float MeasureHeight(GameObject model) => MeasureHeight(model, null);

        /// <summary>
        /// The same, measured in the given transform's space (its units, along its up axis) rather than the world's, so
        /// that transform's scale, and its parents', don't count. Null measures in world space.
        /// </summary>
        public static float MeasureHeight(GameObject model, Transform space)
        {
            float bottom = float.PositiveInfinity, top = float.NegativeInfinity;
            Matrix4x4 fromWorld = space != null ? space.worldToLocalMatrix : Matrix4x4.identity;
            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                Mesh mesh = renderer is SkinnedMeshRenderer skinned ? skinned.sharedMesh
                    : renderer.TryGetComponent(out MeshFilter filter) ? filter.sharedMesh : null;
                if (mesh == null) { continue; }
                Bounds local = mesh.bounds;
                Matrix4x4 toWorld = fromWorld * renderer.transform.localToWorldMatrix;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 point = local.center + Vector3.Scale(local.extents,
                        new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
                    float y = toWorld.MultiplyPoint3x4(point).y;
                    bottom = Mathf.Min(bottom, y);
                    top = Mathf.Max(top, y);
                }
            }
            return top > bottom ? top - bottom : 0f;
        }

        /// <summary>
        /// The uniform scale the model is given, for a model this tall unscaled (in meters, from its meshes in their
        /// bind pose; only needed with Fit To Height): Scale, or whatever brings it to Fit Height.
        /// </summary>
        public float ModelScaleFor(float ownHeight) =>
            height == ModelHeight.FitToHeight && ownHeight > 0f ? fitHeight / ownHeight : scale;

        /// <summary>How tall a model this tall unscaled stands once this source has sized it (at the character's authored size).</summary>
        public float StandingHeightFor(float ownHeight) => ownHeight * ModelScaleFor(ownHeight);

        /// <summary>The model's placement relative to the character's model parent, for a model this tall unscaled.</summary>
        protected Matrix4x4 PlacementFor(float ownHeight)
        {
            float modelScale = ModelScaleFor(ownHeight);
            return Matrix4x4.TRS(positionOffset + Vector3.down * (groundOffset * modelScale), ModelRotation(facingCorrection),
                Vector3.one * modelScale);
        }

        /// <summary>The model's rotation relative to the character: turned to face forward, then by Rotation Offset.</summary>
        private Quaternion ModelRotation(float facing) => Quaternion.Euler(rotationOffset) * Quaternion.Euler(0f, facing, 0f);

        private static void SetLayerRecursively(GameObject gameObject, int layer)
        {
            gameObject.layer = layer;
            foreach (Transform child in gameObject.transform) { SetLayerRecursively(child.gameObject, layer); }
        }
    }

    /// <summary>How a CharacterModelSource sizes its model.</summary>
    public enum ModelHeight
    {
        /// <summary>The model's own size, times the source's Scale.</summary>
        KeepModelSize,
        /// <summary>Scaled to stand the source's Fit Height tall, whatever size the model is.</summary>
        FitToHeight,
    }

    /// <summary>What a CharacterModelSource is creating a model for.</summary>
    public readonly struct CharacterModelContext
    {
        public CharacterModelContext(GameObject character, Transform parent)
        {
            Character = character;
            Parent = parent;
        }

        /// <summary>The character the model is for.</summary>
        public GameObject Character { get; }
        /// <summary>
        /// The transform to create the model under: inactive, beneath the character's model parent, so the model is set
        /// up out of sight and appears only when the character swaps it in.
        /// </summary>
        public Transform Parent { get; }
    }

    /// <summary>A model created by a CharacterModelSource for a character, and the parts of it the character uses.</summary>
    public sealed class CharacterModel
    {
        public CharacterModel(GameObject root, CharacterAnimationTranslator translator)
        {
            Root = root;
            Translator = translator;
            // The model is usually still inactive when this is made, so inactive objects are included.
            Animator = root != null ? root.GetComponentInChildren<Animator>(true) : null;
            IK = Animator != null ? Animator.GetComponent<CharacterIK>() : null;
        }

        /// <summary>The model instance.</summary>
        public GameObject Root { get; }
        /// <summary>Turns the character's movement into the model's animations. May be null.</summary>
        public CharacterAnimationTranslator Translator { get; }
        /// <summary>The model's Animator. May be null.</summary>
        public Animator Animator { get; }
        /// <summary>IK for the model's hands, feet, elbows and knees. May be null.</summary>
        public CharacterIK IK { get; }
    }
}
