using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace Character
{
    /// <summary>
    /// A character model source that references its model directly: a prefab or model file in the project, which is
    /// built into the game with whatever uses this asset. Its model is instantiated asynchronously, so even a heavy one
    /// doesn't stall the game, and can be previewed in the editor without loading anything.
    /// </summary>
    [CreateAssetMenu(fileName = "CharacterModel", menuName = "Character Showcase/Character Model (Direct Reference)")]
    public class DirectCharacterModelSource : CharacterModelSource
    {
        [Header("Model")]
        [Tooltip("The model to instantiate: a model file or a prefab. Its origin should be at its feet, facing +Z.")]
        [SerializeField] private GameObject modelPrefab;

        public GameObject ModelPrefab { get => modelPrefab; set => modelPrefab = value; }

        public override async Awaitable<CharacterModel> CreateModelAsync(CharacterModelContext context, CancellationToken cancellationToken)
        {
            if (modelPrefab == null)
            {
                Debug.LogWarning($"[{nameof(DirectCharacterModelSource)}] '{name}' has no model prefab.", this);
                return null;
            }

            AsyncInstantiateOperation<GameObject> operation = InstantiateAsync(modelPrefab, context.Parent);
            GameObject model = null;
            try
            {
                while (!operation.isDone) { await Awaitable.NextFrameAsync(cancellationToken); }
                model = operation.Result != null && operation.Result.Length > 0 ? operation.Result[0] : null;
                if (model == null) { return null; }
                cancellationToken.ThrowIfCancellationRequested();
                await SetUpModelAsync(model, context, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                return new CharacterModel(model, Translator);
            }
            catch (OperationCanceledException)
            {
                // Nothing created for a character that no longer wants it is left behind.
                if (!operation.isDone) { operation.Cancel(); }
                if (model != null) { Destroy(model); }
                throw;
            }
        }

        public override void GetPreviewMeshes(List<PreviewMesh> results)
        {
            results.Clear();
            if (modelPrefab == null) { return; }

            Matrix4x4 placement = Placement;
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
    }
}
