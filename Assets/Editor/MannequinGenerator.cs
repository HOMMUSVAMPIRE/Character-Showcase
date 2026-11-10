using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Character;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace CharacterShowcase.EditorTools
{
    /// <summary>
    /// Generates the mannequin and the animations every character model shares:
    /// - runs Tools/Mannequin/generate_mannequin.py in Blender to build the mannequin's mesh, rig and animation clips
    ///   (Blender's location is asked for once and remembered)
    /// - imports it as a Humanoid with looping, in-place clips
    /// - builds the Animator Controller from those clips, and the Animator Parameter Translator that drives it
    /// - prepares the mannequin like any other character model (CharacterModelImporter), and makes it the Character
    ///   prefab's model
    /// Other models play these animations through Humanoid retargeting. Safe to run repeatedly: IK settings and edits to
    /// the translator and definition are kept.
    /// </summary>
    public static class MannequinGenerator
    {
        public const string ModelPath = CharacterModelImporter.ModelsFolder + "/Mannequin/Mannequin.fbx";
        public const string ModelPrefabPath = CharacterModelImporter.ModelsFolder + "/Mannequin/Mannequin Model.prefab";
        private const string ControllerPath = CharacterModelImporter.ControllerPath;
        private const string TranslatorPath = CharacterModelImporter.TranslatorPath;
        private const string GeneratorScript = "Tools/Mannequin/generate_mannequin.py";
        private const string PrefabPath = "Assets/Prefabs/Character.prefab";
        private const string ModelParentName = "ModelParent";
        private const string BlenderPathPreference = "CharacterShowcase.BlenderPath";
        private static readonly string[] CommonBlenderPaths =
        {
            "/Applications/Blender.app/Contents/MacOS/Blender",
            "/usr/bin/blender",
            "/snap/bin/blender",
        };

        // The controller is built with the parameter names the translator uses by default.
        private const string SpeedParameter = AnimatorParameterTranslator.DefaultSpeedParameter;
        private const string VerticalSpeedParameter = AnimatorParameterTranslator.DefaultVerticalSpeedParameter;
        private const string GroundedParameter = AnimatorParameterTranslator.DefaultGroundedParameter;
        private const string DashingParameter = AnimatorParameterTranslator.DefaultDashingParameter;

        // Blend tree thresholds in m/s. The clips' strides are authored for these speeds
        // (see Tools/Mannequin/mannequin_animations.py), so the feet don't skate at those speeds.
        private const float WalkSpeed = 1.6f;
        private const float RunSpeed = 10f;
        private const float FallThreshold = -8f;
        private const float RiseThreshold = 6f;

        [MenuItem("Tools/Character Showcase/Generate Mannequin", priority = 1)]
        public static void GenerateFromMenu()
        {
            string blender = FindBlender();
            if (blender == null)
            {
                int choice = EditorUtility.DisplayDialogComplex("Generate Mannequin",
                    "Blender is needed to generate the mannequin, but wasn't found. Locate it, or rebuild the animations and " +
                    "prefabs from the Mannequin.fbx that's already in the project.",
                    "Locate Blender…", "Cancel", "Use Existing Mannequin.fbx");
                if (choice == 1) { return; }
                if (choice == 0)
                {
                    blender = LocateBlender();
                    if (blender == null) { return; }
                }
            }
            if (Generate(blender))
            {
                EditorUtility.DisplayDialog("Generate Mannequin", "The mannequin and its animations are ready. " +
                    "Run Tools > Character Showcase > Prepare Character Models to update the other models' prefabs too.", "OK");
            }
        }

        /// <summary>
        /// For -executeMethod. Pass -blender [path] to use a particular Blender, or -skipBlender to rebuild from the
        /// existing Mannequin.fbx. Exits with code 1 on failure.
        /// </summary>
        public static void GenerateFromCommandLine()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            int blenderArg = System.Array.IndexOf(args, "-blender");
            string blender = args.Contains("-skipBlender") ? null
                : blenderArg >= 0 && blenderArg + 1 < args.Length ? args[blenderArg + 1] : FindBlender();
            if (blender == null && !args.Contains("-skipBlender"))
            {
                Debug.LogError("[MannequinGenerator] Blender wasn't found. Pass -blender <path>, or -skipBlender to use the existing Mannequin.fbx.");
                EditorApplication.Exit(1);
                return;
            }
            if (!Generate(blender)) { EditorApplication.Exit(1); }
        }

        /// <summary>Generates the mannequin with Blender (or, given null, reuses the existing file) and sets it all up.</summary>
        public static bool Generate(string blenderPath)
        {
            if (blenderPath != null && !RunBlender(blenderPath)) { return false; }
            AssetDatabase.Refresh();
            if (!ConfigureImporter()) { return false; }
            AnimatorController controller = BuildAnimatorController();
            if (controller == null) { return false; }
            var translator = LoadOrCreate<AnimatorParameterTranslator>(TranslatorPath);

            CharacterModelImporter.Result mannequin = CharacterModelImporter.Prepare(ModelPath, controller, translator);
            if (!mannequin.Succeeded)
            {
                Debug.LogError($"[MannequinGenerator] {mannequin}");
                return false;
            }
            EditorUtility.SetDirty(translator);
            AssetDatabase.SaveAssets();

            if (!UseOnCharacterPrefab(mannequin.definition)) { return false; }
            Debug.Log($"[MannequinGenerator] Generated the mannequin ('{ModelPath}', definition " +
                      $"'{AssetDatabase.GetAssetPath(mannequin.definition)}', {mannequin.height:0.00} m tall) and the shared animations: controller '{ControllerPath}' with clips " +
                      $"{string.Join(", ", LoadClips().Keys)}, translator '{TranslatorPath}'. It's the Character prefab's model.");
            return true;
        }

        /// <summary>
        /// The mannequin is the Character prefab's own model, which the tests spawn. Scenes choose their own model with
        /// the Character Model window, as an override on their instance of the prefab.
        /// </summary>
        private static bool UseOnCharacterPrefab(DirectCharacterModelSource definition)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                Transform modelParent = root.transform.Find(ModelParentName);
                var animationController = root.GetComponent<AnimationController>();
                if (modelParent == null || animationController == null)
                {
                    Debug.LogError($"[MannequinGenerator] '{PrefabPath}' needs an AnimationController and a '{ModelParentName}' child; it was not changed.");
                    return false;
                }
                var serialized = new SerializedObject(animationController);
                serialized.FindProperty("modelSource").objectReferenceValue = definition;
                serialized.FindProperty("modelParent").objectReferenceValue = modelParent;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static string FindBlender()
        {
            string saved = EditorPrefs.GetString(BlenderPathPreference, "");
            if (File.Exists(saved)) { return saved; }
            return CommonBlenderPaths.FirstOrDefault(File.Exists);
        }

        private static string LocateBlender()
        {
            string picked = EditorUtility.OpenFilePanel("Locate Blender", Application.platform == RuntimePlatform.OSXEditor ? "/Applications" : "", "");
            if (string.IsNullOrEmpty(picked)) { return null; }
            // On macOS the file panel picks the app bundle; the executable is inside it.
            if (picked.EndsWith(".app")) { picked = Path.Combine(picked, "Contents/MacOS/Blender"); }
            if (!File.Exists(picked))
            {
                EditorUtility.DisplayDialog("Generate Mannequin", $"'{picked}' isn't the Blender program.", "OK");
                return null;
            }
            EditorPrefs.SetString(BlenderPathPreference, picked);
            return picked;
        }

        /// <summary>Runs the generator script in Blender, writing the mannequin's FBX. Returns whether it succeeded.</summary>
        private static bool RunBlender(string blenderPath)
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            string output = Path.Combine(projectRoot, ModelPath);
            var start = new ProcessStartInfo
            {
                FileName = blenderPath,
                Arguments = $"-b --factory-startup -P \"{GeneratorScript}\" -- \"{output}\"",
                WorkingDirectory = projectRoot,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            start.EnvironmentVariables["PYTHONDONTWRITEBYTECODE"] = "1"; // Keep __pycache__ out of Tools/Mannequin.

            var log = new StringBuilder();
            try
            {
                if (!Application.isBatchMode) { EditorUtility.DisplayProgressBar("Generate Mannequin", "Generating the mannequin in Blender…", 0.3f); }
                using Process blender = Process.Start(start);
                blender.OutputDataReceived += (_, e) => { if (e.Data != null) { lock (log) { log.AppendLine(e.Data); } } };
                blender.ErrorDataReceived += (_, e) => { if (e.Data != null) { lock (log) { log.AppendLine(e.Data); } } };
                blender.BeginOutputReadLine();
                blender.BeginErrorReadLine();
                blender.WaitForExit();
                string text;
                lock (log) { text = log.ToString(); }
                if (blender.ExitCode != 0 || !text.Contains("[mannequin] exported"))
                {
                    Debug.LogError($"[MannequinGenerator] Blender couldn't generate the mannequin (exit code {blender.ExitCode}):\n{Tail(text, 40)}");
                    return false;
                }
                Debug.Log($"[MannequinGenerator] Blender:\n{string.Join("\n", text.Split('\n').Where(l => l.StartsWith("[mannequin]")))}");
                return true;
            }
            catch (System.ComponentModel.Win32Exception e)
            {
                Debug.LogError($"[MannequinGenerator] Couldn't run Blender at '{blenderPath}': {e.Message}");
                return false;
            }
            finally
            {
                if (!Application.isBatchMode) { EditorUtility.ClearProgressBar(); }
            }
        }

        private static string Tail(string text, int lines) => string.Join("\n", text.Split('\n').TakeLast(lines));

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) { return asset; }
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static bool ConfigureImporter()
        {
            if (AssetImporter.GetAtPath(ModelPath) is not ModelImporter importer)
            {
                Debug.LogError($"[MannequinGenerator] No model found at '{ModelPath}'. Generate it with Blender.");
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
                Debug.LogError($"[MannequinGenerator] '{ModelPath}' is missing clips: {string.Join(", ", missing)}. " +
                               "Run Tools > Character Showcase > Generate Mannequin with Blender to regenerate it. Nothing was changed.");
                return null;
            }

            AnimatorController controller = LoadEmptyController();
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

        /// <summary>
        /// The controller asset, emptied of its parameters, layers, states and blend trees so it can be rebuilt. It's
        /// emptied in place rather than deleted and created again, because every character model's prefab refers to it:
        /// deleting it would leave them all without a controller until it was recreated, and a recreated asset can be
        /// given a new GUID, which they wouldn't find.
        /// </summary>
        private static AnimatorController LoadEmptyController()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(ControllerPath));
                controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            }
            // Everything stored in the asset besides the controller itself: state machines, states, transitions, blend trees.
            foreach (Object part in AssetDatabase.LoadAllAssetsAtPath(ControllerPath))
            {
                if (part != null && part != controller) { Object.DestroyImmediate(part, true); }
            }
            controller.parameters = System.Array.Empty<AnimatorControllerParameter>();
            controller.layers = System.Array.Empty<AnimatorControllerLayer>();
            controller.AddLayer("Base Layer");
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
