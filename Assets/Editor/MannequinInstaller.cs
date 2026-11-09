using System.Linq;
using Character;
using UnityEditor;
using UnityEngine;

namespace CharacterShowcase.EditorTools
{
    /// <summary>
    /// Configures the generated mannequin model as a Humanoid and swaps it in for the placeholder capsule on the
    /// Character prefab. Safe to run repeatedly (e.g. after regenerating the model with Tools/Mannequin).
    /// </summary>
    public static class MannequinInstaller
    {
        private const string ModelPath = "Assets/Models/Mannequin/Mannequin.fbx";
        private const string PrefabPath = "Assets/Prefabs/Character.prefab";
        private const string ModelParentName = "ModelParent";
        private const string PlaceholderName = "Model";

        [MenuItem("Tools/Character Showcase/Install Mannequin Model")]
        public static void Install()
        {
            if (!ConfigureImporter(out Avatar avatar)) { return; }

            var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                Transform modelParent = root.transform.Find(ModelParentName);
                if (modelParent == null)
                {
                    Debug.LogError($"[MannequinInstaller] '{PrefabPath}' has no '{ModelParentName}' child; nothing was changed.");
                    return;
                }

                // Remove the placeholder capsule and any previous install, so re-running replaces rather than stacks.
                foreach (Transform child in modelParent.Cast<Transform>().ToList())
                {
                    bool isPlaceholder = child.name == PlaceholderName && child.GetComponent<SkinnedMeshRenderer>() == null
                                         && !PrefabUtility.IsAnyPrefabInstanceRoot(child.gameObject);
                    bool isPreviousInstall = PrefabUtility.GetCorrespondingObjectFromSource(child.gameObject) == modelAsset;
                    if (isPlaceholder || isPreviousInstall) { Object.DestroyImmediate(child.gameObject); }
                }

                var model = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset, modelParent);
                model.name = "Mannequin";
                model.transform.localPosition = Vector3.zero; // The model's origin is at its feet, like the character's.
                model.transform.localRotation = Quaternion.identity;
                model.transform.localScale = Vector3.one;
                SetLayerRecursively(model, modelParent.gameObject.layer);

                Animator animator = model.GetComponent<Animator>();
                if (animator == null) { animator = model.AddComponent<Animator>(); }
                animator.avatar = avatar;
                animator.applyRootMotion = false; // Movement is driven by MovementController, not animation.

                var animationController = root.GetComponent<AnimationController>();
                if (animationController != null)
                {
                    var serialized = new SerializedObject(animationController);
                    serialized.FindProperty("animator").objectReferenceValue = animator;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);

                var skinned = model.GetComponentInChildren<SkinnedMeshRenderer>();
                Debug.Log($"[MannequinInstaller] Installed mannequin on '{PrefabPath}': humanoid avatar valid, " +
                          $"{skinned?.bones.Length ?? 0} bones, height {skinned?.sharedMesh.bounds.size.y ?? 0f:0.00} m.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static bool ConfigureImporter(out Avatar avatar)
        {
            avatar = null;
            if (AssetImporter.GetAtPath(ModelPath) is not ModelImporter importer)
            {
                Debug.LogError($"[MannequinInstaller] No model found at '{ModelPath}'. Generate it with Tools/Mannequin/generate_mannequin.py.");
                return false;
            }

            bool changed = false;
            void Set<T>(T current, T wanted, System.Action<T> apply)
            {
                if (!Equals(current, wanted)) { apply(wanted); changed = true; }
            }
            Set(importer.animationType, ModelImporterAnimationType.Human, v => importer.animationType = v);
            Set(importer.avatarSetup, ModelImporterAvatarSetup.CreateFromThisModel, v => importer.avatarSetup = v);
            Set(importer.importAnimation, false, v => importer.importAnimation = v);
            Set(importer.importCameras, false, v => importer.importCameras = v);
            Set(importer.importLights, false, v => importer.importLights = v);
            if (changed) { importer.SaveAndReimport(); }

            avatar = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<Avatar>().FirstOrDefault();
            if (avatar == null || !avatar.isValid || !avatar.isHuman)
            {
                Debug.LogError($"[MannequinInstaller] '{ModelPath}' did not produce a valid Humanoid avatar. Open the model's " +
                               "Rig tab and press Configure to see which bones failed to map. The prefab was not changed.");
                return false;
            }
            return true;
        }

        private static void SetLayerRecursively(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform) { SetLayerRecursively(child.gameObject, layer); }
        }
    }
}
