using System.Collections.Generic;
using System.Linq;
using Character;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace CharacterShowcase.EditorTools
{
    /// <summary>
    /// Configures the generated mannequin model as a Humanoid with looping in-place clips, builds its Animator
    /// Controller, and swaps the model in for the placeholder capsule on the Character prefab.
    /// Safe to run repeatedly (e.g. after regenerating the model with Tools/Mannequin).
    /// </summary>
    public static class MannequinInstaller
    {
        private const string ModelPath = "Assets/Models/Mannequin/Mannequin.fbx";
        private const string PrefabPath = "Assets/Prefabs/Character.prefab";
        private const string ModelParentName = "ModelParent";
        private const string PlaceholderName = "Model";
        private const string ControllerPath = "Assets/Animation/Mannequin.controller";

        // Animator parameter names come from the runtime component that drives them, so the two can't drift apart.
        private const string SpeedParameter = AnimationController.SpeedParameter;
        private const string VerticalSpeedParameter = AnimationController.VerticalSpeedParameter;
        private const string GroundedParameter = AnimationController.GroundedParameter;

        // Blend tree thresholds in m/s. The clips' stride lengths are authored for these speeds
        // (see Tools/Mannequin/mannequin_animations.py), so the feet don't skate at those speeds.
        private const float WalkSpeed = 1.6f;
        private const float RunSpeed = 10f;
        private const float FallThreshold = -8f;
        private const float RiseThreshold = 6f;

        [MenuItem("Tools/Character Showcase/Install Mannequin Model")]
        public static void Install()
        {
            if (!ConfigureImporter(out Avatar avatar)) { return; }
            AnimatorController controller = BuildAnimatorController();
            if (controller == null) { return; }

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
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false; // Movement is driven by MovementController, not animation.
                // Keep animating while hidden in first person, so the shadow the camera keeps still animates.
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

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
                          $"{skinned?.bones.Length ?? 0} bones, height {skinned?.sharedMesh.bounds.size.y ?? 0f:0.00} m, " +
                          $"controller '{ControllerPath}' with clips {string.Join(", ", LoadClips().Keys)}.");
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
            Set(importer.importAnimation, true, v => importer.importAnimation = v);
            Set(importer.importCameras, false, v => importer.importCameras = v);
            Set(importer.importLights, false, v => importer.importLights = v);
            if (changed)
            {
                // Reimport first so the FBX's takes are known before configuring clips from them.
                importer.SaveAndReimport();
                changed = false;
            }

            ModelImporterClipAnimation[] clips = BuildClipSettings(importer.defaultClipAnimations);
            if (!ClipSettingsMatch(importer.clipAnimations, clips))
            {
                importer.clipAnimations = clips;
                changed = true;
            }
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

        /// <summary>
        /// One looping, in-place clip per FBX take. Blender names takes "Armature|Walk"; Unity clips get the short name.
        /// Root motion is baked into the pose: the character is moved by MovementController, not by animation.
        /// </summary>
        private static ModelImporterClipAnimation[] BuildClipSettings(ModelImporterClipAnimation[] takes)
        {
            return takes.Select(take =>
            {
                var clip = take;
                clip.name = take.takeName.Split('|').Last();
                clip.loopTime = true;
                clip.loopPose = false; // The clips are authored to loop seamlessly already.
                clip.lockRootRotation = true;
                clip.keepOriginalOrientation = true;
                clip.lockRootHeightY = true;
                clip.keepOriginalPositionY = true; // Keep the authored hip bob relative to the feet.
                clip.heightFromFeet = false;
                clip.lockRootPositionXZ = true;
                clip.keepOriginalPositionXZ = true;
                return clip;
            }).ToArray();
        }

        private static bool ClipSettingsMatch(ModelImporterClipAnimation[] current, ModelImporterClipAnimation[] wanted)
        {
            if (current.Length != wanted.Length) { return false; }
            for (int i = 0; i < current.Length; i++)
            {
                ModelImporterClipAnimation a = current[i], b = wanted[i];
                if (a.name != b.name || a.takeName != b.takeName || a.loopTime != b.loopTime || a.loopPose != b.loopPose
                    || a.lockRootRotation != b.lockRootRotation || a.keepOriginalOrientation != b.keepOriginalOrientation
                    || a.lockRootHeightY != b.lockRootHeightY || a.keepOriginalPositionY != b.keepOriginalPositionY
                    || a.heightFromFeet != b.heightFromFeet || a.lockRootPositionXZ != b.lockRootPositionXZ
                    || a.keepOriginalPositionXZ != b.keepOriginalPositionXZ
                    || !Mathf.Approximately(a.firstFrame, b.firstFrame) || !Mathf.Approximately(a.lastFrame, b.lastFrame))
                {
                    return false;
                }
            }
            return true;
        }

        private static Dictionary<string, AnimationClip> LoadClips() =>
            AssetDatabase.LoadAllAssetsAtPath(ModelPath)
                .OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview__"))
                .ToDictionary(c => c.name);

        /// <summary>
        /// Builds the controller from scratch each time so it always matches this code:
        /// Locomotion (Idle / Walk / Run by Speed) and Airborne (Fall / Rise by VerticalSpeed), switched by Grounded.
        /// </summary>
        private static AnimatorController BuildAnimatorController()
        {
            Dictionary<string, AnimationClip> clips = LoadClips();
            string[] required = { "Idle", "Walk", "Run", "Rise", "Fall" };
            string[] missing = required.Where(n => !clips.ContainsKey(n)).ToArray();
            if (missing.Length > 0)
            {
                Debug.LogError($"[MannequinInstaller] '{ModelPath}' is missing clips: {string.Join(", ", missing)}. " +
                               "Regenerate it with Tools/Mannequin/generate_mannequin.py. The prefab was not changed.");
                return null;
            }

            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(ControllerPath));
            AssetDatabase.DeleteAsset(ControllerPath);
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.AddParameter(SpeedParameter, AnimatorControllerParameterType.Float);
            controller.AddParameter(VerticalSpeedParameter, AnimatorControllerParameterType.Float);
            controller.AddParameter(new AnimatorControllerParameter
            {
                name = GroundedParameter,
                type = AnimatorControllerParameterType.Bool,
                defaultBool = true,
            });

            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;

            BlendTree locomotionTree = CreateBlendTree(controller, "Locomotion", SpeedParameter,
                (clips["Idle"], 0f), (clips["Walk"], WalkSpeed), (clips["Run"], RunSpeed));
            BlendTree airborneTree = CreateBlendTree(controller, "Airborne", VerticalSpeedParameter,
                (clips["Fall"], FallThreshold), (clips["Rise"], RiseThreshold));

            AnimatorState locomotion = stateMachine.AddState("Locomotion");
            locomotion.motion = locomotionTree;
            AnimatorState airborne = stateMachine.AddState("Airborne");
            airborne.motion = airborneTree;
            stateMachine.defaultState = locomotion;

            AnimatorStateTransition takeOff = locomotion.AddTransition(airborne);
            takeOff.hasExitTime = false;
            takeOff.duration = 0.12f;
            takeOff.AddCondition(AnimatorConditionMode.IfNot, 0f, GroundedParameter);

            AnimatorStateTransition land = airborne.AddTransition(locomotion);
            land.hasExitTime = false;
            land.duration = 0.08f;
            land.AddCondition(AnimatorConditionMode.If, 0f, GroundedParameter);

            AssetDatabase.SaveAssets();
            return controller;
        }

        private static BlendTree CreateBlendTree(AnimatorController controller, string name, string parameter,
            params (Motion motion, float threshold)[] children)
        {
            var tree = new BlendTree
            {
                name = name,
                blendType = BlendTreeType.Simple1D,
                blendParameter = parameter,
                useAutomaticThresholds = false,
                hideFlags = HideFlags.HideInHierarchy,
            };
            foreach (var (motion, threshold) in children) { tree.AddChild(motion, threshold); }
            AssetDatabase.AddObjectToAsset(tree, controller);
            return tree;
        }

        private static void SetLayerRecursively(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform) { SetLayerRecursively(child.gameObject, layer); }
        }
    }
}
