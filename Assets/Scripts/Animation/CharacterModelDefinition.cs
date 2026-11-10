using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Character
{
    /// <summary>
    /// Describes a character model: what to instantiate, how to place and set it up on a character, and which
    /// translator animates it. The AnimationController loads it at runtime, so changing a character's look is a
    /// matter of pointing it at a different definition.
    ///
    /// Loading and setup are both coroutines, and both virtual. The default load instantiates the model prefab
    /// asynchronously; a subclass can load it another way (from Addressables or an asset bundle, say) by overriding
    /// LoadModel, or apply customisation (a character creator's choices of meshes, materials or blend shapes) by
    /// overriding SetupModel, waiting on whatever it needs to without stalling the game.
    /// </summary>
    [CreateAssetMenu(fileName = "CharacterModelDefinition", menuName = "Character Showcase/Character Model Definition")]
    public class CharacterModelDefinition : ScriptableObject
    {
        [Header("Model")]
        [Tooltip("The model to instantiate: a model file or a prefab. Its origin should be at its feet, facing +Z.")]
        [SerializeField] private GameObject modelPrefab;
        [Tooltip("Optional: replaces the model's Animator Controller")]
        [SerializeField] private RuntimeAnimatorController animatorController;
        [Tooltip("Optional: replaces the model's Avatar")]
        [SerializeField] private Avatar avatar;
        [Tooltip("Turns the character's movement into this model's animations")]
        [SerializeField] private CharacterAnimationTranslator translator;

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

        public GameObject ModelPrefab { get => modelPrefab; set => modelPrefab = value; }
        public RuntimeAnimatorController AnimatorController { get => animatorController; set => animatorController = value; }
        public Avatar Avatar { get => avatar; set => avatar = value; }
        public CharacterAnimationTranslator Translator { get => translator; set => translator = value; }
        public Vector3 PositionOffset { get => positionOffset; set => positionOffset = value; }
        public Vector3 RotationOffset { get => rotationOffset; set => rotationOffset = value; }
        public float Scale { get => scale; set => scale = Mathf.Max(0.001f, value); }

        /// <summary>
        /// Loads the model and hands it to the request with SetResult. Runs as a coroutine, so it may take as many frames
        /// as it needs. By default, instantiates the model prefab asynchronously under the request's parent.
        ///
        /// Overrides should register cleanup with request.OnCancel (e.g. releasing an Addressables handle), which runs
        /// if the character is destroyed or given another model before loading finishes.
        /// </summary>
        public virtual IEnumerator LoadModel(ModelLoadRequest request)
        {
            if (modelPrefab == null)
            {
                Debug.LogWarning($"[{nameof(CharacterModelDefinition)}] '{name}' has no model prefab.", this);
                yield break;
            }

            AsyncInstantiateOperation<GameObject> operation = InstantiateAsync(modelPrefab, request.Parent);
            request.OnCancel(() => { if (!operation.isDone) { operation.Cancel(); } });
            yield return operation;

            if (request.IsCancelled || operation.Result == null || operation.Result.Length == 0) { yield break; }
            request.SetResult(operation.Result[0]);
        }

        /// <summary>
        /// Prepares a loaded model for the character: places it, puts it on the character's layer, applies the Animator
        /// overrides, and adds IK to humanoids if asked to. Runs as a coroutine, so overrides can wait for things they
        /// need (such as customisation assets) before the character starts using the model.
        /// </summary>
        /// <param name="model">The loaded model instance</param>
        /// <param name="parent">The transform the model belongs under, which turns to face the character's direction</param>
        public virtual IEnumerator SetupModel(GameObject model, Transform parent)
        {
            Transform modelTransform = model.transform;
            if (modelTransform.parent != parent) { modelTransform.SetParent(parent, false); }
            modelTransform.SetLocalPositionAndRotation(positionOffset, Quaternion.Euler(rotationOffset));
            modelTransform.localScale = Vector3.one * scale;
            if (matchCharacterLayer) { SetLayerRecursively(model, parent.gameObject.layer); }

            Animator animator = model.GetComponentInChildren<Animator>();
            if (animator == null) { yield break; }

            if (animatorController != null) { animator.runtimeAnimatorController = animatorController; }
            if (avatar != null) { animator.avatar = avatar; }
            animator.applyRootMotion = false; // The character is moved by its MovementController, not by animation.
            if (alwaysAnimate) { animator.cullingMode = AnimatorCullingMode.AlwaysAnimate; }

            // IK works with humanoids only. It must live on the Animator's GameObject, as OnAnimatorIK requires.
            // (Not `??`, which bypasses Unity's null check.)
            if (animator.isHuman)
            {
                GameObject animated = animator.gameObject;
                if (addIK && !animated.TryGetComponent(out CharacterIK _)) { animated.AddComponent<CharacterIK>(); }
                if (addFootPlacement && !animated.TryGetComponent(out FootPlacementIK _)) { animated.AddComponent<FootPlacementIK>(); }
            }
        }

        /// <summary>A mesh of the model and where it sits relative to the character's model parent.</summary>
        public struct PreviewMesh
        {
            public Mesh mesh;
            public Matrix4x4 matrix;
        }

        /// <summary>
        /// The model's meshes in their default pose, placed as SetupModel would place them, for previewing the model
        /// where it isn't loaded (in the editor, or in a character select screen).
        /// </summary>
        public void GetPreviewMeshes(List<PreviewMesh> results)
        {
            results.Clear();
            if (modelPrefab == null) { return; }

            Matrix4x4 placement = Matrix4x4.TRS(positionOffset, Quaternion.Euler(rotationOffset), Vector3.one * scale);
            Matrix4x4 fromWorld = modelPrefab.transform.worldToLocalMatrix;
            foreach (SkinnedMeshRenderer skinned in modelPrefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (skinned.sharedMesh == null) { continue; }
                results.Add(new PreviewMesh { mesh = skinned.sharedMesh, matrix = placement * fromWorld * skinned.transform.localToWorldMatrix });
            }
            foreach (MeshFilter filter in modelPrefab.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null) { continue; }
                results.Add(new PreviewMesh { mesh = filter.sharedMesh, matrix = placement * fromWorld * filter.transform.localToWorldMatrix });
            }
        }

        private static void SetLayerRecursively(GameObject gameObject, int layer)
        {
            gameObject.layer = layer;
            foreach (Transform child in gameObject.transform) { SetLayerRecursively(child.gameObject, layer); }
        }
    }

    /// <summary>
    /// One request to load a model, passed to CharacterModelDefinition.LoadModel. The loader hands over the loaded
    /// model with SetResult. If the request is cancelled first (the character was destroyed or given another model),
    /// cleanup registered with OnCancel runs, and any model handed over is destroyed rather than used.
    /// </summary>
    public sealed class ModelLoadRequest
    {
        private Action cancelled;

        public ModelLoadRequest(Transform parent) => Parent = parent;

        /// <summary>The transform the model should be created under.</summary>
        public Transform Parent { get; }
        /// <summary>The loaded model, once the loader has handed it over.</summary>
        public GameObject Result { get; private set; }
        public bool IsCancelled { get; private set; }

        public void SetResult(GameObject model)
        {
            if (IsCancelled)
            {
                if (model != null) { Object.Destroy(model); }
                return;
            }
            Result = model;
        }

        /// <summary>Registers cleanup to run if the request is cancelled. Runs at once if it already has been.</summary>
        public void OnCancel(Action cleanup)
        {
            if (IsCancelled) { cleanup?.Invoke(); }
            else { cancelled += cleanup; }
        }

        /// <summary>Abandons the request: runs the registered cleanup and destroys any model already handed over.</summary>
        public void Cancel()
        {
            if (IsCancelled) { return; }
            IsCancelled = true;
            cancelled?.Invoke();
            cancelled = null;
            if (Result != null) { Object.Destroy(Result); }
            Result = null;
        }
    }
}
