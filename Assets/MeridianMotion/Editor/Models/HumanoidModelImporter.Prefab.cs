using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MeridianMotion.Characters;
using UnityEditor.Animations;
using UnityEditor;
using UnityEngine;

namespace MeridianMotion.EditorTools
{
    // Saving a model's prefab with its Animator and IK components, and checking a prepared model.
    public static partial class HumanoidModelImporter
    {
        /// <summary>
        /// Why the definition can't be used as the character's model, or null if it can: it must name a model prefab with
        /// a valid Humanoid avatar, an Animator Controller and a translator, as Prepare sets it up.
        /// </summary>
        public static string Validate(PrefabModelSource definition)
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
        public static RuntimeAnimatorController ControllerOf(PrefabModelSource definition)
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
        /// HumanoidIK, and every IK a humanoid gets ([HumanoidIK]: HumanoidFootPlacementIK, LedgeHandIK, WallContactIK, LadderIK,
        /// CableGripIK, and any of your own). IK settings come from an earlier run if there was one, otherwise from the
        /// mannequin's model prefab, so tuning done there carries over to new models. Its hitboxes are fitted to it (see
        /// HitboxFitter), with the settings an earlier run's had.
        /// </summary>
        private static GameObject SaveModelPrefab(GameObject modelAsset, Avatar avatar, AnimatorController controller, string prefabPath)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            var mannequin = AssetDatabase.LoadAssetAtPath<GameObject>(MannequinPrefabPath);
            string ikSettings = SettingsOf<HumanoidIK>(existing) ?? SettingsOf<HumanoidIK>(mannequin);
            // Every IK a humanoid gets ([HumanoidIK]: foot placement, and each ability's hands and feet), in its order, with
            // its settings from the earlier prefab or the mannequin's.
            var abilityIK = TypeCache.GetTypesDerivedFrom<AbilityIK>()
                .Select(type => (type, attribute: (HumanoidIKAttribute)System.Attribute.GetCustomAttribute(type, typeof(HumanoidIKAttribute))))
                .Where(entry => entry.attribute != null && !entry.type.IsAbstract)
                .OrderBy(entry => entry.attribute.Order).ThenBy(entry => entry.type.Name, System.StringComparer.Ordinal)
                .Select(entry => (entry.type, settings: SettingsOf(entry.type, existing) ?? SettingsOf(entry.type, mannequin)))
                .ToArray();

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
                animator.applyRootMotion = false; // Movement is driven by MMotionMotor, not animation.
                // Keep animating while hidden in first person, so the shadow the camera keeps still animates.
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                // IK layered over the animations. Lives on the Animator's GameObject, as OnAnimatorIK requires.
                // (Not `??`: it bypasses Unity's null check, so a "missing" component could be returned instead of added.)
                if (!model.TryGetComponent(out HumanoidIK ik)) { ik = model.AddComponent<HumanoidIK>(); }
                if (ikSettings != null) { EditorJsonUtility.FromJsonOverwrite(ikSettings, ik); }
                // Foot placement on uneven ground, and each ability's hands and feet (a ledge, a wall slid down, a ladder's
                // rungs, a rope or cable), fed through HumanoidIK's procedural channel.
                foreach ((System.Type type, string settings) in abilityIK)
                {
                    Component placement = model.GetComponent(type);
                    if (placement == null) { placement = model.AddComponent(type); }
                    if (settings != null) { EditorJsonUtility.FromJsonOverwrite(settings, placement); }
                }
                // Its hitboxes, for what meets the character (the movement doesn't use them).
                HitboxFitter.Fit(model, settingsFrom: existing != null ? existing.GetComponent<CharacterHitboxes>() : null);

                return PrefabUtility.SaveAsPrefabAsset(model, prefabPath);
            }
            finally
            {
                Object.DestroyImmediate(model);
            }
        }

        private static string SettingsOf<T>(GameObject gameObject) where T : Component => SettingsOf(typeof(T), gameObject);

        private static string SettingsOf(System.Type type, GameObject gameObject) =>
            gameObject != null && gameObject.TryGetComponent(type, out Component component) ? EditorJsonUtility.ToJson(component) : null;
    }
}
