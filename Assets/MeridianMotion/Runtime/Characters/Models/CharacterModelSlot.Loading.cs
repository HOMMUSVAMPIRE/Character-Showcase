using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;

namespace MeridianMotion.Characters
{
    // Loading a model from its source and swapping it in, the current one kept until the new one's ready.
    public partial class CharacterModelSlot
    {
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
                Debug.LogWarning($"[{nameof(CharacterModelSlot)}] Using the fallback model '{fallbackSource.name}' instead of '{source.name}'.", this);
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
            LoadedCharacterModel created;
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
                Debug.LogWarning($"[{nameof(CharacterModelSlot)}] '{source.name}' didn't create a model.", this);
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
        private void SwapIn(LoadedCharacterModel created, CharacterModelSource source)
        {
            if (LoadedModel != null)
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
            created.Root.GetComponentInChildren<CharacterHitboxes>(true)?.UseLayer(HitboxLayerIndex);
            MeasureModel(created);
            created.Root.SetActive(true);
            ShowPlaceholder(false);
            ModelLoaded?.Invoke(created);
        }

        private void ReleaseModel()
        {
            pendingBodyFit = false; // The fit was for the model going away.
            LoadedCharacterModel old = model;
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
    }
}
