using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Character;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace CharacterShowcase.EditorTools
{
    // Saving a model's prefab with its Animator and IK components, and checking a prepared model.
    public static partial class CharacterModelImporter
    {
        /// <summary>
        /// Why the definition can't be used as the character's model, or null if it can: it must name a model prefab with
        /// a valid Humanoid avatar, an Animator Controller and a translator, as Prepare sets it up.
        /// </summary>
        public static string Validate(DirectCharacterModelSource definition)
        {
            if (definition == null) { return "There is no definition."; }
            if (definition.ModelPrefab == null) { return "It names no model prefab."; }
            var animator = definition.ModelPrefab.GetComponentInChildren<Animator>(true);
            if (animator == null) { return "Its model has no Animator."; }
            Avatar avatar = definition.Avatar != null ? definition.Avatar : animator.avatar;
            if (avatar == null || !avatar.isValid || !avatar.isHuman) { return "Its model has no valid Humanoid avatar."; }
            RuntimeAnimatorController controller = ControllerOf(definition);
            if (controller == null) { return "Its model has no Animator Controller."; }
            if (controller is AnimatorController editable && editable.layers.Length == 0) { return "Its Animator Controller is empty."; }
            if (definition.Translator == null) { return "It names no animation translator."; }
            return null;
        }

        /// <summary>The Animator Controller the definition's model plays: the definition's override, or its prefab's.</summary>
        public static RuntimeAnimatorController ControllerOf(DirectCharacterModelSource definition)
        {
            if (definition == null) { return null; }
            if (definition.AnimatorController != null) { return definition.AnimatorController; }
            Animator animator = definition.ModelPrefab != null ? definition.ModelPrefab.GetComponentInChildren<Animator>(true) : null;
            return animator != null ? animator.runtimeAnimatorController : null;
        }

        /// <summary>
        /// Why the saved model prefab's Animator isn't set up with the given avatar and controller, or null if it is.
        /// Reimports the prefab first, so this checks what's on disk rather than what's in memory.
        /// </summary>
        private static string SavedAnimatorProblem(string prefabPath, Avatar avatar, AnimatorController controller)
        {
            AssetDatabase.ImportAsset(prefabPath, ImportAssetOptions.ForceUpdate);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Animator animator = prefab != null ? prefab.GetComponentInChildren<Animator>(true) : null;
            if (animator == null) { return "it has no Animator."; }
            if (animator.avatar != avatar) { return "its Animator has no avatar, or a different one."; }
            if (animator.runtimeAnimatorController != controller) { return $"its Animator doesn't use '{ControllerPath}'."; }
            return null;
        }

        /// <summary>
        /// Saves the model as a prefab (a variant of the model file, so it follows changes to the file) with its Animator,
        /// CharacterIK, FootPlacementIK, LedgeHandIK, WallSlideIK, LadderIK and LineGripIK. IK settings come from an earlier run if there was one, otherwise from the
        /// mannequin's model prefab, so tuning done there carries over to new models. Its hitboxes are fitted to it (see
        /// HitboxFitter), with the settings an earlier run's had.
        /// </summary>
        private static GameObject SaveModelPrefab(GameObject modelAsset, Avatar avatar, AnimatorController controller, string prefabPath)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            var mannequin = AssetDatabase.LoadAssetAtPath<GameObject>(MannequinPrefabPath);
            string ikSettings = SettingsOf<CharacterIK>(existing) ?? SettingsOf<CharacterIK>(mannequin);
            string footPlacementSettings = SettingsOf<FootPlacementIK>(existing) ?? SettingsOf<FootPlacementIK>(mannequin);
            string ledgeHandSettings = SettingsOf<LedgeHandIK>(existing) ?? SettingsOf<LedgeHandIK>(mannequin);
            string wallSlideSettings = SettingsOf<WallSlideIK>(existing) ?? SettingsOf<WallSlideIK>(mannequin);
            string ladderSettings = SettingsOf<LadderIK>(existing) ?? SettingsOf<LadderIK>(mannequin);
            string lineGripSettings = SettingsOf<LineGripIK>(existing) ?? SettingsOf<LineGripIK>(mannequin);

            var model = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
            try
            {
                model.name = Path.GetFileNameWithoutExtension(prefabPath).Replace(" Model", "");
                model.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity); // Its origin is at its feet.
                model.transform.localScale = Vector3.one;

                Animator animator = model.GetComponent<Animator>();
                if (animator == null) { animator = model.AddComponent<Animator>(); }
                animator.avatar = avatar;
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false; // Movement is driven by MovementController, not animation.
                // Keep animating while hidden in first person, so the shadow the camera keeps still animates.
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                // IK layered over the animations. Lives on the Animator's GameObject, as OnAnimatorIK requires.
                // (Not `??`: it bypasses Unity's null check, so a "missing" component could be returned instead of added.)
                if (!model.TryGetComponent(out CharacterIK ik)) { ik = model.AddComponent<CharacterIK>(); }
                if (ikSettings != null) { EditorJsonUtility.FromJsonOverwrite(ikSettings, ik); }
                // Automatic foot placement on uneven ground, fed through CharacterIK's procedural channel.
                if (!model.TryGetComponent(out FootPlacementIK footPlacement)) { footPlacement = model.AddComponent<FootPlacementIK>(); }
                if (footPlacementSettings != null) { EditorJsonUtility.FromJsonOverwrite(footPlacementSettings, footPlacement); }
                // Hands on the ledge while hanging from one, through the same channel.
                if (!model.TryGetComponent(out LedgeHandIK ledgeHands)) { ledgeHands = model.AddComponent<LedgeHandIK>(); }
                if (ledgeHandSettings != null) { EditorJsonUtility.FromJsonOverwrite(ledgeHandSettings, ledgeHands); }
                // Hands on the wall and the body against it while sliding down one.
                if (!model.TryGetComponent(out WallSlideIK wallSlide)) { wallSlide = model.AddComponent<WallSlideIK>(); }
                if (wallSlideSettings != null) { EditorJsonUtility.FromJsonOverwrite(wallSlideSettings, wallSlide); }
                // Hands and feet on the rungs while climbing a ladder.
                if (!model.TryGetComponent(out LadderIK ladder)) { ladder = model.AddComponent<LadderIK>(); }
                if (ladderSettings != null) { EditorJsonUtility.FromJsonOverwrite(ladderSettings, ladder); }
                // Hands on the rope or the zipline's cable while swinging on one or riding the other.
                if (!model.TryGetComponent(out LineGripIK lineGrip)) { lineGrip = model.AddComponent<LineGripIK>(); }
                if (lineGripSettings != null) { EditorJsonUtility.FromJsonOverwrite(lineGripSettings, lineGrip); }
                // Its hitboxes, for what meets the character (the movement doesn't use them).
                HitboxFitter.Fit(model, settingsFrom: existing != null ? existing.GetComponent<CharacterHitboxes>() : null);

                return PrefabUtility.SaveAsPrefabAsset(model, prefabPath);
            }
            finally
            {
                Object.DestroyImmediate(model);
            }
        }

        private static string SettingsOf<T>(GameObject gameObject) where T : Component =>
            gameObject != null && gameObject.TryGetComponent(out T component) ? EditorJsonUtility.ToJson(component) : null;
    }
}
