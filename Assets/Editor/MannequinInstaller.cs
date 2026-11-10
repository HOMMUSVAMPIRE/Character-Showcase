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
    /// Controller, and sets it up as the Character prefab's model:
    /// - a model prefab: the mannequin with its Animator, CharacterIK and FootPlacementIK configured
    /// - an Animator Parameter Translator that drives the controller's parameters
    /// - a Character Model Definition naming the two, which the Character prefab's AnimationController loads at runtime
    /// The model isn't baked into the Character prefab, so it can be replaced by pointing the prefab at another
    /// definition. Safe to run repeatedly (e.g. after regenerating the model with Tools/Mannequin): IK settings and any
    /// edits to the translator and definition are kept.
    /// </summary>
    public static class MannequinInstaller
    {
        private const string ModelPath = "Assets/Models/Mannequin/Mannequin.fbx";
        private const string ModelPrefabPath = "Assets/Models/Mannequin/Mannequin Model.prefab";
        private const string DefinitionPath = "Assets/Models/Mannequin/Mannequin.asset";
        private const string TranslatorPath = "Assets/Animation/Mannequin Animator Translator.asset";
        private const string PrefabPath = "Assets/Prefabs/Character.prefab";
        private const string ModelParentName = "ModelParent";
        private const string PlaceholderName = "Model";
        private const string ControllerPath = "Assets/Animation/Mannequin.controller";

        // The controller is built with the parameter names the translator uses by default.
        private const string SpeedParameter = AnimatorParameterTranslator.DefaultSpeedParameter;
        private const string VerticalSpeedParameter = AnimatorParameterTranslator.DefaultVerticalSpeedParameter;
        private const string GroundedParameter = AnimatorParameterTranslator.DefaultGroundedParameter;
        private const string DashingParameter = AnimatorParameterTranslator.DefaultDashingParameter;

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
                var animationController = root.GetComponent<AnimationController>();
                if (modelParent == null || animationController == null)
                {
                    Debug.LogError($"[MannequinInstaller] '{PrefabPath}' needs an AnimationController and a '{ModelParentName}' child; nothing was changed.");
                    return;
                }

                // IK settings to keep: from the model prefab if there is one, else from a model baked into the Character
                // prefab by an earlier version of this installer.
                var existingModelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPrefabPath);
                string ikSettings = SettingsOf<CharacterIK>(existingModelPrefab);
                string footPlacementSettings = SettingsOf<FootPlacementIK>(existingModelPrefab);

                // Remove the placeholder capsule and any baked-in model: the model is now loaded at runtime.
                foreach (Transform child in modelParent.Cast<Transform>().ToList())
                {
                    bool isPlaceholder = child.name == PlaceholderName && child.GetComponent<SkinnedMeshRenderer>() == null
                                         && !PrefabUtility.IsAnyPrefabInstanceRoot(child.gameObject);
                    bool isBakedModel = PrefabUtility.GetCorrespondingObjectFromSource(child.gameObject) == modelAsset;
                    if (isBakedModel)
                    {
                        ikSettings ??= SettingsOf<CharacterIK>(child.gameObject);
                        footPlacementSettings ??= SettingsOf<FootPlacementIK>(child.gameObject);
                    }
                    if (isPlaceholder || isBakedModel) { Object.DestroyImmediate(child.gameObject); }
                }

                GameObject modelPrefab = SaveModelPrefab(modelAsset, avatar, controller, ikSettings, footPlacementSettings);
                var translator = LoadOrCreate<AnimatorParameterTranslator>(TranslatorPath);
                var definition = LoadOrCreate<CharacterModelDefinition>(DefinitionPath);
                definition.ModelPrefab = modelPrefab;
                if (definition.Translator == null) { definition.Translator = translator; }
                EditorUtility.SetDirty(definition);
                AssetDatabase.SaveAssets();

                var serialized = new SerializedObject(animationController);
                serialized.FindProperty("modelDefinition").objectReferenceValue = definition;
                serialized.FindProperty("modelParent").objectReferenceValue = modelParent;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);

                var skinned = modelPrefab.GetComponentInChildren<SkinnedMeshRenderer>();
                Debug.Log($"[MannequinInstaller] Installed the mannequin as '{DefinitionPath}' (model '{ModelPrefabPath}', " +
                          $"translator '{AssetDatabase.GetAssetPath(definition.Translator)}') on '{PrefabPath}': humanoid avatar valid, " +
                          $"{skinned?.bones.Length ?? 0} bones, height {skinned?.sharedMesh.bounds.size.y ?? 0f:0.00} m, " +
                          $"controller '{ControllerPath}' with clips {string.Join(", ", LoadClips().Keys)}.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>
        /// Saves the mannequin as a prefab (a variant of the model file, so it follows regenerations of the model) with
        /// its Animator, CharacterIK and FootPlacementIK configured, keeping previous IK settings if given.
        /// </summary>
        private static GameObject SaveModelPrefab(GameObject modelAsset, Avatar avatar, AnimatorController controller,
            string ikSettings, string footPlacementSettings)
        {
            var model = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
            try
            {
                model.name = "Mannequin";
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

                return PrefabUtility.SaveAsPrefabAsset(model, ModelPrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(model);
            }
        }

        private static string SettingsOf<T>(GameObject gameObject) where T : Component =>
            gameObject != null && gameObject.TryGetComponent(out T component) ? EditorJsonUtility.ToJson(component) : null;

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) { return asset; }
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
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
        /// Locomotion (Idle / Walk / Run by Speed) and Airborne (Fall / Rise by VerticalSpeed), switched by Grounded,
        /// plus Dash, entered from any state while Dashing.
        /// </summary>
        private static AnimatorController BuildAnimatorController()
        {
            Dictionary<string, AnimationClip> clips = LoadClips();
            string[] required = { "Idle", "Walk", "Run", "Rise", "Fall", "Dash" };
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
            controller.AddParameter(DashingParameter, AnimatorControllerParameterType.Bool);

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

            // Dash: entered from anywhere almost instantly so it feels immediate, and left for whichever of
            // Locomotion / Airborne matches the character once the dash ends.
            AnimatorState dash = stateMachine.AddState("Dash");
            dash.motion = clips["Dash"];
            AnimatorStateTransition startDash = stateMachine.AddAnyStateTransition(dash);
            startDash.hasExitTime = false;
            startDash.duration = 0.04f;
            startDash.canTransitionToSelf = false;
            startDash.AddCondition(AnimatorConditionMode.If, 0f, DashingParameter);

            AnimatorStateTransition dashToGround = dash.AddTransition(locomotion);
            dashToGround.hasExitTime = false;
            dashToGround.duration = 0.12f;
            dashToGround.AddCondition(AnimatorConditionMode.IfNot, 0f, DashingParameter);
            dashToGround.AddCondition(AnimatorConditionMode.If, 0f, GroundedParameter);

            AnimatorStateTransition dashToAir = dash.AddTransition(airborne);
            dashToAir.hasExitTime = false;
            dashToAir.duration = 0.12f;
            dashToAir.AddCondition(AnimatorConditionMode.IfNot, 0f, DashingParameter);
            dashToAir.AddCondition(AnimatorConditionMode.IfNot, 0f, GroundedParameter);

            // OnAnimatorIK (and so CharacterIK) only runs on layers with IK Pass enabled. Layers are returned by
            // copy, so modify the array and assign it back.
            AnimatorControllerLayer[] layers = controller.layers;
            layers[0].iKPass = true;
            controller.layers = layers;

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
    }
}
