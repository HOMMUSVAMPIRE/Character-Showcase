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
    /// - builds the Animator Controller, and the Animator Parameter Translator that drives it, from the mannequin's own
    ///   procedurally authored clips (Tools/Mannequin/mannequin_animations.py) and, where they're installed, the
    ///   Quaternius Universal Animation Libraries' walk, jog, sprint and jump (QuaterniusAnimationImporter): standing,
    ///   walking, walking briskly, running, jogging, sprinting, jumping, landing and dashing, and crouching, sliding,
    ///   rolling, wall-running, hanging, climbing and pushing (the libraries' clips for those too, where installed).
    ///   Each loop's speed and phase are measured on the mannequin (MotionCaptureLocomotion's StrideSpeed and TouchDownPhase).
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
        private const string CrouchingParameter = AnimatorParameterTranslator.DefaultCrouchingParameter;
        private const string SlidingParameter = AnimatorParameterTranslator.DefaultSlidingParameter;
        private const string RollingParameter = AnimatorParameterTranslator.DefaultRollingParameter;
        private const string WallRunningParameter = AnimatorParameterTranslator.DefaultWallRunningParameter;
        private const string HangingParameter = AnimatorParameterTranslator.DefaultHangingParameter;
        private const string ClimbingParameter = AnimatorParameterTranslator.DefaultClimbingParameter;
        private const string GrabbingParameter = AnimatorParameterTranslator.DefaultGrabbingParameter;
        private const string GrabSpeedParameter = AnimatorParameterTranslator.DefaultGrabSpeedParameter;
        private const string RollProgressParameter = AnimatorParameterTranslator.DefaultRollProgressParameter;
        private const string ClimbProgressParameter = AnimatorParameterTranslator.DefaultClimbProgressParameter;
        // Fired by the translator on every jump, from the ground, in the air or off a wall (with the Quaternius libraries' jump).
        private const string JumpTrigger = "Jump";
        private const MovementEvents JumpEvents = MovementEvents.Jumped | MovementEvents.AirJumped | MovementEvents.WallJumped;
        // The libraries' landing plays this far (normalized) before handing back, or until the character moves faster than this (m/s).
        private const float LandingLength = 0.45f, MoveOnFromLanding = 0.5f;
        // The Quaternius libraries' clips used, where they're installed.
        private static readonly string[] LibraryClips = { "Walk_Loop", "Jog_Fwd_Loop", "Sprint_Loop", "Jump_Start", "Jump_Loop", "Jump_Land" };
        // The libraries' clips for the movement abilities, each used where it's installed (the mannequin's own fallback
        // otherwise; see BuildAbilityStates). Of them, those standing on the floor are raised onto the mannequin's, as
        // the locomotion clips are (see StandLibraryOnMannequinsFloor).
        private static readonly string[] AbilityLibraryClips = { "Crouch_Idle_Loop", "Crouch_Fwd_Loop", "Slide_Loop", "Roll", "Push_Loop", "ClimbUp_1m" };
        private static readonly string[] GroundedAbilityLibraryClips = { "Crouch_Idle_Loop", "Crouch_Fwd_Loop", "Push_Loop" };
        // The top of the Crouch blend tree, in m/s: well above BasicMovementMode's crouch speed (2 m/s). Past the crouch
        // walk's own speed, it plays sped up to reach it.
        private const float CrouchTopSpeed = 3f;
        // How far through a roll (RollProgress) it starts blending into walking or running, if the character is moving
        // on, and how long that blend takes (s): it's done as the roll ends, so the roll flows into the stride.
        private const float RollBlendOutAt = 0.75f, RollBlendOutTime = 0.15f;

        // The take Tools/Mannequin/mannequin_animations.py exports first so the file's default pose is the rest pose
        private const string BindPoseTake = "!BindPose";
        // The top of the Locomotion blend tree, in m/s: well above the character's sprint (BasicMovementMode's run
        // speed), for the momentum a dash leaves it with. Past the sprint loop's own speed, the loop plays sped up to
        // reach it, so its planted feet keep up with the ground.
        public const float TopSpeed = 10f;
        // The brisk walk is shown up to past the character's default speed (BasicMovementMode's walk speed, 3 m/s),
        // rather than handing over to the run: it reaches this speed played this much faster than its strides are made
        // for, so it quickens a little and its planted feet slide a little (about a tenth of the speed at 3 m/s). No one
        // walks at 3 m/s (people break into a run at about 2), so a walk there must hurry or slide; this keeps both small.
        // The run takes over past it.
        private const float BriskWalkUpTo = 3.2f, BriskWalkUpToPlayback = 1.25f;
        // Airborne, by vertical speed (m/s): rising at take-off (the jump leaves the ground at 15 m/s), tucked at the top,
        // falling. Falling locks in quickly, so walking off a ledge only passes through the tuck.
        private const float FallThreshold = -5f;
        private const float RiseThreshold = 8f;

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
            using (CharacterModelImporter.Busy()) { return GenerateWhileBusy(blenderPath); }
        }

        /// <summary>
        /// Whether the mannequin's file has changed since its clips and controller were last built from it (regenerated
        /// with Blender by hand, say), so they need building again.
        /// </summary>
        public static bool NeedsRebuild() => File.Exists(ModelPath) && CharacterModelImporter.HasChangedSincePrepared(ModelPath);

        private static bool GenerateWhileBusy(string blenderPath)
        {
            if (blenderPath != null && !RunBlender(blenderPath)) { return false; }
            AssetDatabase.Refresh();
            if (!ConfigureImporter()) { return false; }
            AnimatorController controller = BuildAnimatorController();
            if (controller == null) { return false; }
            var translator = LoadOrCreate<AnimatorParameterTranslator>(TranslatorPath);
            BindJumpTrigger(translator, controller);

            CharacterModelImporter.Result mannequin = CharacterModelImporter.Prepare(ModelPath, controller, translator);
            if (!mannequin.Succeeded)
            {
                Debug.LogError($"[MannequinGenerator] {mannequin}");
                return false;
            }
            EditorUtility.SetDirty(translator);
            AssetDatabase.SaveAssets();

            if (!UseOnCharacterPrefab(mannequin.definition)) { return false; }
            CharacterModelImporter.MarkPrepared(ModelPath, controller);
            Debug.Log($"[MannequinGenerator] Generated the mannequin ('{ModelPath}', definition " +
                      $"'{AssetDatabase.GetAssetPath(mannequin.definition)}', {mannequin.height:0.00} m tall) and the shared animations: controller '{ControllerPath}' with clips " +
                      $"{string.Join(", ", LoadClips().Keys)}, translator '{TranslatorPath}'. It's the Character prefab's model.");
            return true;
        }

        /// <summary>
        /// Has the translator fire the controller's jump trigger on every jump, if the controller has one and the
        /// translator doesn't already (other edits to the translator are kept).
        /// </summary>
        private static void BindJumpTrigger(AnimatorParameterTranslator translator, AnimatorController controller)
        {
            if (!controller.parameters.Any(p => p.name == JumpTrigger && p.type == AnimatorControllerParameterType.Trigger)) { return; }
            var settings = new SerializedObject(translator);
            SerializedProperty triggers = settings.FindProperty("eventTriggers");
            for (int i = 0; i < triggers.arraySize; i++)
            {
                SerializedProperty existing = triggers.GetArrayElementAtIndex(i);
                if (existing.FindPropertyRelative("trigger").stringValue != JumpTrigger) { continue; }
                // Bound by an earlier version, before wall jumps: they're jumps too.
                SerializedProperty events = existing.FindPropertyRelative("events");
                if ((events.intValue & (int)MovementEvents.Jumped) != 0 && (events.intValue & (int)MovementEvents.WallJumped) == 0)
                {
                    events.intValue |= (int)MovementEvents.WallJumped;
                    settings.ApplyModifiedPropertiesWithoutUndo();
                }
                return;
            }
            triggers.arraySize++;
            SerializedProperty binding = triggers.GetArrayElementAtIndex(triggers.arraySize - 1);
            binding.FindPropertyRelative("events").intValue = (int)JumpEvents;
            binding.FindPropertyRelative("trigger").stringValue = JumpTrigger;
            settings.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Sets up the Character prefab's model: a CharacterModelHost loading the mannequin (also its fallback, should
        /// another model fail to load), with a placeholder shown while there's no model and an eye point for the camera.
        /// The mannequin is the prefab's own model, which the tests spawn; scenes choose their own with the Character
        /// Model window, as an override on their instance of the prefab.
        /// </summary>
        private static bool UseOnCharacterPrefab(DirectCharacterModelSource mannequin)
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
                if (!root.TryGetComponent(out CharacterModelHost host)) { host = root.AddComponent<CharacterModelHost>(); }

                var hostSettings = new SerializedObject(host);
                hostSettings.FindProperty("modelSource").objectReferenceValue = mannequin;
                hostSettings.FindProperty("fallbackSource").objectReferenceValue = mannequin;
                hostSettings.FindProperty("modelParent").objectReferenceValue = modelParent;
                hostSettings.FindProperty("placeholder").objectReferenceValue = FindOrCreatePlaceholder(root.transform, modelParent);
                hostSettings.FindProperty("eyePoint").objectReferenceValue = FindOrCreateChild(root.transform, EyePointName).transform;
                hostSettings.ApplyModifiedPropertiesWithoutUndo();

                var animationSettings = new SerializedObject(animationController);
                animationSettings.FindProperty("modelHost").objectReferenceValue = host;
                animationSettings.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private const string EyePointName = "Eye Point";
        private const string PlaceholderName = "Model Placeholder";

        private static GameObject FindOrCreateChild(Transform parent, string name)
        {
            Transform child = parent.Find(name);
            if (child != null) { return child.gameObject; }
            var created = new GameObject(name);
            created.transform.SetParent(parent, false);
            return created;
        }

        /// <summary>
        /// A plain capsule the size of the character's collider, shown while the character has no model. It lives under
        /// the model parent, so it's resized with the character (see ACCharacterController.SetScale). Created once; after
        /// that it's left as it is, so it can be restyled (one made by an earlier version, under the character's root,
        /// is moved under the model parent).
        /// </summary>
        private static GameObject FindOrCreatePlaceholder(Transform root, Transform modelParent)
        {
            Transform existing = modelParent.Find(PlaceholderName) ?? root.Find(PlaceholderName);
            if (existing != null)
            {
                if (existing.parent != modelParent) { existing.SetParent(modelParent, true); }
                return existing.gameObject;
            }
            GameObject placeholder = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            placeholder.name = PlaceholderName;
            Object.DestroyImmediate(placeholder.GetComponent<Collider>()); // The character's own collider does the colliding.
            placeholder.transform.SetParent(modelParent, false);
            placeholder.layer = root.gameObject.layer;
            if (root.TryGetComponent(out CapsuleCollider capsule))
            {
                // Unity's capsule mesh is 2 m tall and 1 m wide.
                placeholder.transform.localPosition = capsule.center;
                placeholder.transform.localScale = new Vector3(capsule.radius * 2f, capsule.height / 2f, capsule.radius * 2f);
            }
            return placeholder;
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
            // The bind pose take only sets the file's default pose (see mannequin_animations.py); it isn't a clip.
            return takes.Where(take => take.takeName.Split('|').Last() != BindPoseTake).Select(take =>
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
        /// Locomotion (Idle / Walk / Brisk Walk / Run / Sprint by Speed) and Airborne (Fall / Apex / Rise by VerticalSpeed), switched by Grounded,
        /// plus Dash, entered from any state while Dashing, and the movement abilities' states (see BuildAbilityStates).
        /// </summary>
        /// <summary>
        /// The Quaternius libraries' clips the controller uses, set up first if they aren't already
        /// (QuaterniusAnimationImporter), or null if the libraries aren't installed or any clip is missing.
        /// </summary>
        private static Dictionary<string, AnimationClip> LoadLibraryClips()
        {
            if (!QuaterniusAnimationImporter.FindLibraryFiles().Any()) { return null; }
            List<string> problems = QuaterniusAnimationImporter.PrepareAll();
            if (problems.Count > 0) { return null; }
            var clips = LibraryClips.ToDictionary(name => name, QuaterniusAnimationImporter.FindClip);
            string[] missing = clips.Where(c => c.Value == null).Select(c => c.Key).ToArray();
            if (missing.Length > 0)
            {
                Debug.LogWarning($"[MannequinGenerator] The Quaternius libraries are missing {string.Join(", ", missing)}: using the mannequin's own clips.");
                return null;
            }
            return clips;
        }

        /// <summary>
        /// Stands the libraries' clips the controller uses on the mannequin's floor: where its own idle stands its feet.
        /// Played on the mannequin, each clip's lowest foot (its planted one) should be as low as the idle's, as foot
        /// placement keeps each foot's height above its clip's floor; the libraries' clips stood about 3 cm lower, so
        /// the feet went through the floor. Each is raised by the difference (its root height offset, stored in the
        /// clip as the library's mannequin is sized, so it scales to every model). The jump's loop is raised as its
        /// start is (it has no floor of its own: its feet are in the air).
        /// </summary>
        private static void StandLibraryOnMannequinsFloor(GameObject measuringModel, AnimationClip idle)
        {
            float idleFloor = MotionCaptureLocomotion.LowestFoot(measuringModel, idle);
            // How the library's mannequin's sizes compare with this one's: the offsets are in its units.
            float scale = LibraryHumanScale() / HumanScale(measuringModel);
            for (int pass = 0; pass < 2; pass++)
            {
                var raise = new Dictionary<string, float>();
                foreach (string name in new[] { "Walk_Loop", "Jog_Fwd_Loop", "Sprint_Loop", "Jump_Start", "Jump_Land" }.Concat(GroundedAbilityLibraryClips))
                {
                    AnimationClip clip = QuaterniusAnimationImporter.FindClip(name);
                    if (clip == null) { continue; } // An ability's clip that isn't installed: the mannequin's own is used.
                    raise[name] = (idleFloor - MotionCaptureLocomotion.LowestFoot(measuringModel, clip)) * scale;
                }
                raise["Jump_Loop"] = raise["Jump_Start"];
                Debug.Log($"[MannequinGenerator] Standing the libraries' clips on the mannequin's floor: raising " +
                          string.Join(", ", raise.Select(r => $"{r.Key} {r.Value * 100f:0.0} cm")));
                if (raise.Values.All(r => Mathf.Abs(r) < 0.002f) || !QuaterniusAnimationImporter.RaiseClips(raise)) { break; }
            }
        }

        private static float HumanScale(GameObject model)
        {
            GameObject instance = Object.Instantiate(model);
            try
            {
                Animator animator = instance.GetComponentInChildren<Animator>();
                return animator != null && animator.isHuman ? animator.humanScale : 1f;
            }
            finally { Object.DestroyImmediate(instance); }
        }

        private static float LibraryHumanScale()
        {
            string path = QuaterniusAnimationImporter.FindLibraryFiles().First();
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            GameObject instance = Object.Instantiate(model);
            try
            {
                Animator animator = instance.GetComponentInChildren<Animator>() ?? instance.AddComponent<Animator>();
                animator.avatar = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().First();
                return animator.humanScale;
            }
            finally { Object.DestroyImmediate(instance); }
        }

        private static AnimatorController BuildAnimatorController()
        {
            Dictionary<string, AnimationClip> clips = LoadClips();
            string[] required = { "Idle", "Walk", "Brisk Walk", "Run", "Sprint", "Rise", "Apex", "Fall", "Dash", "Crouch", "Hang" };
            string[] missing = required.Where(n => !clips.ContainsKey(n)).ToArray();
            if (missing.Length > 0)
            {
                Debug.LogError($"[MannequinGenerator] '{ModelPath}' is missing clips: {string.Join(", ", missing)}. " +
                               "Run Tools > Character Showcase > Generate Mannequin with Blender to regenerate it. Nothing was changed.");
                return null;
            }

            // Measured on the mannequin, as the clips play on it: its prepared prefab if there is one yet, else the file.
            var measuringModel = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPrefabPath);
            if (measuringModel == null) { measuringModel = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath); }

            // The Quaternius libraries' walk, jog, sprint and jump, where they're installed (see QuaterniusAnimationImporter);
            // otherwise the mannequin's own.
            Dictionary<string, AnimationClip> library = LoadLibraryClips();
            bool useLibrary = library != null;
            if (useLibrary)
            {
                StandLibraryOnMannequinsFloor(measuringModel, clips["Idle"]);
                library = LoadLibraryClips();
            }
            Debug.Log(useLibrary
                ? $"[MannequinGenerator] Using the Quaternius libraries' {string.Join(", ", library.Keys)}, with the mannequin's own clips between them."
                : $"[MannequinGenerator] The Quaternius libraries aren't in {QuaterniusAnimationImporter.LibraryFolder}: using the mannequin's own clips.");

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
            if (useLibrary) { controller.AddParameter(JumpTrigger, AnimatorControllerParameterType.Trigger); }
            foreach (string ability in new[] { CrouchingParameter, SlidingParameter, RollingParameter, WallRunningParameter, HangingParameter, ClimbingParameter, GrabbingParameter })
            {
                controller.AddParameter(ability, AnimatorControllerParameterType.Bool);
            }
            foreach (string value in new[] { GrabSpeedParameter, RollProgressParameter, ClimbProgressParameter })
            {
                controller.AddParameter(value, AnimatorControllerParameterType.Float);
            }

            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;

            // Each loop at the speed its strides are made for (measured on the mannequin), so its planted feet stay put
            // there, from walking to sprinting. The brisk walk is also sped up to BriskWalkUpTo, so the character's
            // default speed looks like a walk (see BriskWalkUpTo); past the fastest loop's own speed, it's sped up to
            // match, up to the top speed. Each loop is offset to land its left foot at the same moment as the others
            // (see TouchDownPhase), so blending between them never mixes one foot's stride with the other's.
            (AnimationClip clip, float speed, float phase) Measure(AnimationClip clip)
            {
                var loop = (clip, MotionCaptureLocomotion.StrideSpeed(measuringModel, clip), MotionCaptureLocomotion.TouchDownPhase(measuringModel, clip));
                Debug.Log($"[MannequinGenerator] {clip.name}: strides made for {loop.Item2:0.00} m/s, left foot landing " +
                          $"{loop.Item3:0.000} of the way through, on '{measuringModel.name}'");
                return loop;
            }
            var brisk = Measure(clips["Brisk Walk"]);
            var loops = new List<(AnimationClip clip, float speed, float playback, float phase)>();
            void Add((AnimationClip clip, float speed, float phase) loop, float speed = 0f, float playback = 1f) =>
                loops.Add((loop.clip, speed > 0f ? speed : loop.speed, playback, loop.phase));
            if (useLibrary)
            {
                // The libraries' walk is slow and their jog (what sprinting shows) quick: the mannequin's brisk walk
                // and run fill the speeds between.
                Add(Measure(library["Walk_Loop"]));
                Add(brisk);
                Add(brisk, BriskWalkUpTo, BriskWalkUpToPlayback);
                Add(Measure(clips["Run"]));
                Add(Measure(library["Jog_Fwd_Loop"]));
                var sprint = Measure(library["Sprint_Loop"]);
                Add(sprint);
                Add(sprint, TopSpeed, TopSpeed / sprint.speed);
            }
            else
            {
                Add(Measure(clips["Walk"]));
                Add(brisk);
                Add(brisk, BriskWalkUpTo, BriskWalkUpToPlayback);
                Add(Measure(clips["Run"]));
                var sprint = Measure(clips["Sprint"]);
                Add(sprint);
                Add(sprint, TopSpeed, TopSpeed / sprint.speed);
            }
            for (int i = 1; i < loops.Count; i++)
            {
                if (!(loops[i - 1].speed < loops[i].speed) || !(loops[0].speed > 0.1f))
                {
                    Debug.LogError("[MannequinGenerator] The locomotion speeds are out of order: " +
                                   string.Join(", ", loops.Select(l => $"{l.clip.name} at {l.speed:0.00} m/s")) + ". Nothing was changed.");
                    return null;
                }
            }
            var locomotionChildren = new List<(Motion, float, float, float)> { (clips["Idle"], 0f, 1f, 0f) };
            locomotionChildren.AddRange(loops.Select(l => ((Motion)l.clip, l.speed, l.playback, l.phase)));
            BlendTree locomotionTree = CreateBlendTree(controller, "Locomotion", SpeedParameter, locomotionChildren.ToArray());

            // No Foot IK on the states: on a model stretched along one axis, Unity's Foot IK puts the feet where it
            // reports their goals, which is off by the stretch (see CharacterUtilities.AnimatedIKGoalPosition). Foot
            // placement plants them at the goals, corrected, instead.
            AnimatorState locomotion = stateMachine.AddState("Locomotion");
            locomotion.motion = locomotionTree;
            AnimatorState airborne = stateMachine.AddState("Airborne");
            stateMachine.defaultState = locomotion;

            if (useLibrary)
            {
                // In the air, the libraries' jump: a jump starts from its take-off (skipping its crouch, so the jump
                // shows the moment it's pressed) and carries on into its loop; walking off an edge goes straight to
                // the loop; landing plays the start of its landing, which hands back to walking or running as soon as
                // the character moves.
                airborne.motion = library["Jump_Loop"];
                AnimationClip jumpStart = library["Jump_Start"];
                float takeOff = 0f;
                if (MotionCaptureLocomotion.TryFindFlight(measuringModel, jumpStart, out int takeOffFrame, out _, out _))
                {
                    takeOff = takeOffFrame / (jumpStart.length * jumpStart.frameRate);
                }
                AnimatorState jump = stateMachine.AddState("Jump");
                jump.motion = jumpStart;
                jump.cycleOffset = takeOff;
                AnimatorState landing = stateMachine.AddState("Land");
                landing.motion = library["Jump_Land"];

                // Any jump, from the ground or in the air, starts the jump over.
                AnimatorStateTransition jumped = stateMachine.AddAnyStateTransition(jump);
                jumped.hasExitTime = false;
                jumped.duration = 0.06f;
                jumped.canTransitionToSelf = true;
                jumped.AddCondition(AnimatorConditionMode.If, 0f, JumpTrigger);

                AnimatorStateTransition rising = jump.AddTransition(airborne);
                rising.hasExitTime = true;
                rising.exitTime = Mathf.Lerp(takeOff, 1f, 0.6f);
                rising.duration = 0.2f;

                AnimatorStateTransition walkedOff = locomotion.AddTransition(airborne);
                walkedOff.hasExitTime = false;
                walkedOff.duration = 0.15f;
                walkedOff.AddCondition(AnimatorConditionMode.IfNot, 0f, GroundedParameter);

                // Landing as soon as it's on the ground and no longer rising (at the start of a jump it's still on the
                // ground, rising). Not by exit time: the Animator only checks one of those as it passes the exit time,
                // so a jump landing after that stayed in its jump, sinking through the floor.
                foreach (AnimatorState inAir in new[] { airborne, jump })
                {
                    AnimatorStateTransition touchDown = inAir.AddTransition(landing);
                    touchDown.hasExitTime = false;
                    touchDown.duration = 0.06f;
                    touchDown.AddCondition(AnimatorConditionMode.If, 0f, GroundedParameter);
                    touchDown.AddCondition(AnimatorConditionMode.Less, 0.5f, VerticalSpeedParameter);
                }

                AnimatorStateTransition recovered = landing.AddTransition(locomotion);
                recovered.hasExitTime = true;
                recovered.exitTime = LandingLength;
                recovered.duration = 0.2f;
                AnimatorStateTransition movedOn = landing.AddTransition(locomotion);
                movedOn.hasExitTime = false;
                movedOn.duration = 0.15f;
                movedOn.AddCondition(AnimatorConditionMode.Greater, MoveOnFromLanding, SpeedParameter);
                AnimatorStateTransition fellAgain = landing.AddTransition(airborne);
                fellAgain.hasExitTime = false;
                fellAgain.duration = 0.1f;
                fellAgain.AddCondition(AnimatorConditionMode.IfNot, 0f, GroundedParameter);
            }
            else
            {
                // Rising, tucked at the top, and falling, by vertical speed.
                airborne.motion = CreateBlendTree(controller, "Airborne", VerticalSpeedParameter,
                    (clips["Fall"], FallThreshold, 1f, 0f), (clips["Apex"], 0f, 1f, 0f), (clips["Rise"], RiseThreshold, 1f, 0f));

                AnimatorStateTransition takeOff = locomotion.AddTransition(airborne);
                takeOff.hasExitTime = false;
                takeOff.duration = 0.12f;
                takeOff.AddCondition(AnimatorConditionMode.IfNot, 0f, GroundedParameter);

                AnimatorStateTransition land = airborne.AddTransition(locomotion);
                land.hasExitTime = false;
                land.duration = 0.08f;
                land.AddCondition(AnimatorConditionMode.If, 0f, GroundedParameter);
            }

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

            BuildAbilityStates(controller, stateMachine, locomotion, airborne, clips, useLibrary ? library["Sprint_Loop"] : clips["Sprint"], measuringModel);

            // OnAnimatorIK (and so CharacterIK) only runs on layers with IK Pass enabled. Layers are returned by
            // copy, so modify the array and assign it back.
            AnimatorControllerLayer[] layers = controller.layers;
            layers[0].iKPass = true;
            controller.layers = layers;

            AssetDatabase.SaveAssets();
            return controller;
        }

        /// <summary>
        /// The states for the movement abilities: Crouch (standing still and crouch-walking, by Speed), Slide, Roll
        /// (timed by RollProgress), Wall Run (the sprint, at the run's speed), Hang, Climb (timed by ClimbProgress) and
        /// Push (pushing, or played backward pulling, by GrabSpeed). Each is entered from any state while its
        /// parameter is set, and left for Locomotion, Crouch or Airborne, whichever matches the character, once it's
        /// cleared. The Quaternius libraries' crouch, slide, roll, climb and push are used where they're installed, and
        /// the mannequin's own clips otherwise (its crouch, its dash for the slide, its tucked jump for the roll, its
        /// rise for the climb and its walk for the push). Hanging is always the mannequin's own.
        /// </summary>
        private static void BuildAbilityStates(AnimatorController controller, AnimatorStateMachine stateMachine,
            AnimatorState locomotion, AnimatorState airborne, Dictionary<string, AnimationClip> clips, AnimationClip sprint,
            GameObject measuringModel)
        {
            AnimationClip Library(string name, AnimationClip fallback)
            {
                AnimationClip clip = QuaterniusAnimationImporter.FindLibraryFiles().Any() ? QuaterniusAnimationImporter.FindClip(name) : null;
                return clip != null ? clip : fallback;
            }
            // The speed (m/s) a loop's strides are made for, measured on the mannequin; 0 if it can't be measured.
            float Strides(AnimationClip clip)
            {
                float speed = MotionCaptureLocomotion.StrideSpeed(measuringModel, clip);
                return float.IsFinite(speed) && speed > 0.05f ? speed : 0f;
            }

            AnimationClip crouchIdle = Library("Crouch_Idle_Loop", clips["Crouch"]);
            AnimationClip crouchWalk = Library("Crouch_Fwd_Loop", clips["Crouch"]);
            AnimationClip push = Library("Push_Loop", clips["Walk"]);

            // Crouch: still, then crouch-walking at the speed its strides are made for, sped up past it to the top.
            AnimatorState crouch = stateMachine.AddState("Crouch");
            float crouchStrides = crouchWalk != crouchIdle ? Strides(crouchWalk) : 0f;
            if (crouchStrides > 0f && crouchStrides < CrouchTopSpeed)
            {
                crouch.motion = CreateBlendTree(controller, "Crouch", SpeedParameter,
                    (crouchIdle, 0f, 1f, 0f), (crouchWalk, crouchStrides, 1f, 0f), (crouchWalk, CrouchTopSpeed, CrouchTopSpeed / crouchStrides, 0f));
            }
            else { crouch.motion = crouchIdle; }

            AnimatorState slide = stateMachine.AddState("Slide");
            slide.motion = Library("Slide_Loop", clips["Dash"]);

            // The roll and the climb play through once over the roll or climb, however long it's tuned to take.
            AnimatorState roll = stateMachine.AddState("Roll");
            roll.motion = Library("Roll", clips["Apex"]);
            roll.timeParameterActive = true;
            roll.timeParameter = RollProgressParameter;

            // Running along a wall is sprinting, at the speed the character runs.
            AnimatorState wallRun = stateMachine.AddState("Wall Run");
            wallRun.motion = sprint;
            float sprintStrides = Strides(sprint);
            if (sprintStrides > 0f)
            {
                wallRun.speedParameterActive = true;
                wallRun.speedParameter = SpeedParameter;
                wallRun.speed = 1f / sprintStrides;
            }

            AnimatorState hang = stateMachine.AddState("Hang");
            hang.motion = clips["Hang"];

            AnimatorState climb = stateMachine.AddState("Climb");
            climb.motion = Library("ClimbUp_1m", clips["Rise"]);
            climb.timeParameterActive = true;
            climb.timeParameter = ClimbProgressParameter;

            // Pushing at the speed the push's strides are made for, and pulling the same played backward; standing
            // still holding the object, held still.
            AnimatorState pushPull = stateMachine.AddState("Push");
            pushPull.motion = push;
            float pushStrides = Strides(push);
            pushPull.speedParameterActive = true;
            pushPull.speedParameter = GrabSpeedParameter;
            pushPull.speed = pushStrides > 0f ? 1f / pushStrides : 1f;

            foreach (var (state, parameter, enter) in new[]
            {
                (crouch, CrouchingParameter, 0.15f), (slide, SlidingParameter, 0.1f), (roll, RollingParameter, 0.05f),
                (wallRun, WallRunningParameter, 0.12f), (hang, HangingParameter, 0.1f), (climb, ClimbingParameter, 0.05f),
                (pushPull, GrabbingParameter, 0.15f),
            })
            {
                AnimatorStateTransition start = stateMachine.AddAnyStateTransition(state);
                start.hasExitTime = false;
                start.duration = enter;
                start.canTransitionToSelf = false;
                start.AddCondition(AnimatorConditionMode.If, 0f, parameter);
                // Not back into the roll from walking on out of the end of one (see below), while it's still rolling.
                if (state == roll) { start.AddCondition(AnimatorConditionMode.Less, 0.5f, RollProgressParameter); }
                if (state == crouch)
                {
                    // The character can be crouched while doing something else (dashing, climbing onto a low ledge, or
                    // falling): that shows instead, or two Any State transitions would keep swapping the states.
                    start.AddCondition(AnimatorConditionMode.If, 0f, GroundedParameter);
                    foreach (string other in new[] { DashingParameter, SlidingParameter, RollingParameter, WallRunningParameter, HangingParameter, ClimbingParameter, GrabbingParameter })
                    {
                        start.AddCondition(AnimatorConditionMode.IfNot, 0f, other);
                    }
                }

                // Back on the ground, standing or crouched (crouching is entered from any state, above), or in the air.
                AnimatorStateTransition toGround = state.AddTransition(locomotion);
                toGround.hasExitTime = false;
                toGround.duration = 0.15f;
                toGround.AddCondition(AnimatorConditionMode.IfNot, 0f, parameter);
                toGround.AddCondition(AnimatorConditionMode.If, 0f, GroundedParameter);
                if (state != crouch) { toGround.AddCondition(AnimatorConditionMode.IfNot, 0f, CrouchingParameter); }

                // Moving on out of a roll, blend into walking or running over its end, rather than after it.
                if (state == roll)
                {
                    AnimatorStateTransition walkOn = state.AddTransition(locomotion);
                    walkOn.hasExitTime = false;
                    walkOn.hasFixedDuration = true;
                    walkOn.duration = RollBlendOutTime;
                    walkOn.AddCondition(AnimatorConditionMode.Greater, RollBlendOutAt, RollProgressParameter);
                    walkOn.AddCondition(AnimatorConditionMode.Greater, MoveOnFromLanding, SpeedParameter);
                    walkOn.AddCondition(AnimatorConditionMode.If, 0f, GroundedParameter);
                    walkOn.AddCondition(AnimatorConditionMode.IfNot, 0f, CrouchingParameter);
                }

                // A crouched character falling (walked off an edge crouched) shows the air too.
                AnimatorStateTransition toAir = state.AddTransition(airborne);
                toAir.hasExitTime = false;
                toAir.duration = 0.15f;
                if (state != crouch) { toAir.AddCondition(AnimatorConditionMode.IfNot, 0f, parameter); }
                toAir.AddCondition(AnimatorConditionMode.IfNot, 0f, GroundedParameter);
            }
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
            params (Motion motion, float threshold, float timeScale, float cycleOffset)[] children)
        {
            var tree = new BlendTree
            {
                name = name,
                blendType = BlendTreeType.Simple1D,
                blendParameter = parameter,
                useAutomaticThresholds = false,
                hideFlags = HideFlags.HideInHierarchy,
            };
            foreach (var (motion, threshold, _, _) in children) { tree.AddChild(motion, threshold); }
            // Playback speeds and cycle offsets can only be set on the children array, which is returned by copy.
            ChildMotion[] motions = tree.children;
            for (int i = 0; i < motions.Length; i++)
            {
                motions[i].timeScale = children[i].timeScale;
                motions[i].cycleOffset = children[i].cycleOffset;
            }
            tree.children = motions;
            AssetDatabase.AddObjectToAsset(tree, controller);
            return tree;
        }
    }
}
