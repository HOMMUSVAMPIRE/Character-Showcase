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
        [Tooltip("Offset of the model from the character's feet, in the character's space")]
        [SerializeField] private Vector3 positionOffset = Vector3.zero;
        [Tooltip("Rotation of the model relative to the character, in degrees")]
        [SerializeField] private Vector3 rotationOffset = Vector3.zero;
        [Tooltip("Uniform scale applied to the model")]
        [Min(0.001f)] [SerializeField] private float scale = 1f;

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

        /// <summary>
        /// Creates the model under the context's parent and sets it up for the character (usually by calling
        /// SetUpModel), then hands it back. It may take as many frames as it needs; the character moves normally
        /// meanwhile, and keeps its current model until this one is ready.
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
            if (model != null && model.Root != null) { Destroy(model.Root); }
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
            modelTransform.SetLocalPositionAndRotation(positionOffset, Quaternion.Euler(rotationOffset));
            modelTransform.localScale = Vector3.one * scale;
            if (matchCharacterLayer) { SetLayerRecursively(model, context.Character.layer); }

            Animator modelAnimator = model.GetComponentInChildren<Animator>();
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
                }
            }
            return Completed();
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

        /// <summary>The model's placement relative to the character's model parent.</summary>
        protected Matrix4x4 Placement => Matrix4x4.TRS(positionOffset, Quaternion.Euler(rotationOffset), Vector3.one * scale);

        private static void SetLayerRecursively(GameObject gameObject, int layer)
        {
            gameObject.layer = layer;
            foreach (Transform child in gameObject.transform) { SetLayerRecursively(child.gameObject, layer); }
        }
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
        /// <summary>The transform the model belongs under, which turns to face the character's direction.</summary>
        public Transform Parent { get; }
    }

    /// <summary>A model created by a CharacterModelSource for a character, and the parts of it the character uses.</summary>
    public sealed class CharacterModel
    {
        public CharacterModel(GameObject root, CharacterAnimationTranslator translator)
        {
            Root = root;
            Translator = translator;
            Animator = root != null ? root.GetComponentInChildren<Animator>() : null;
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
