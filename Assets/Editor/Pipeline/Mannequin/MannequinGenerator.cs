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
    public static partial class MannequinGenerator
    {
        /// <summary>Where the generated mannequin's file (mesh, rig and clips) is saved.</summary>
        public const string ModelPath = CharacterModelImporter.MannequinModelPath;
        /// <summary>
        /// Where the mannequin's model prefab is saved, as Prepare Character Models saves every model's.
        /// </summary>
        public const string ModelPrefabPath = CharacterModelImporter.MannequinPrefabPath;
        private const string ControllerPath = CharacterModelImporter.ControllerPath;
        private const string TranslatorPath = CharacterModelImporter.TranslatorPath;
        private const string GeneratorScript = "Tools/Mannequin/generate_mannequin.py";
        private const string PrefabPath = SceneGameSetup.CharacterPrefabPath;
        private const string ModelParentName = "ModelParent";

        // The controller is built with the parameter names the translator uses by default.
        private const string SpeedParameter = AnimatorParameterTranslator.DefaultSpeedParameter;
        private const string VerticalSpeedParameter = AnimatorParameterTranslator.DefaultVerticalSpeedParameter;
        private const string GroundedParameter = AnimatorParameterTranslator.DefaultGroundedParameter;
        private const string DashingParameter = AnimatorParameterTranslator.DefaultDashingParameter;
        private const string CrouchingParameter = AnimatorParameterTranslator.DefaultCrouchingParameter;
        private const string SlidingParameter = AnimatorParameterTranslator.DefaultSlidingParameter;
        private const string RollingParameter = AnimatorParameterTranslator.DefaultRollingParameter;
        private const string WallRunningParameter = AnimatorParameterTranslator.DefaultWallRunningParameter;
        private const string WallSlidingParameter = AnimatorParameterTranslator.DefaultWallSlidingParameter;
        private const string WallClimbingParameter = AnimatorParameterTranslator.DefaultWallClimbingParameter;
        private const string ClimbRightParameter = AnimatorParameterTranslator.DefaultClimbRightParameter;
        private const string ClimbUpParameter = AnimatorParameterTranslator.DefaultClimbUpParameter;
        private const string ClimbEnteringParameter = AnimatorParameterTranslator.DefaultClimbEnteringParameter;
        private const string OnLadderParameter = AnimatorParameterTranslator.DefaultOnLadderParameter;
        private const string ClimbExitingParameter = AnimatorParameterTranslator.DefaultClimbExitingParameter;
        private const string ClimbStepProgressParameter = AnimatorParameterTranslator.DefaultClimbStepProgressParameter;
        private const string HangingParameter = AnimatorParameterTranslator.DefaultHangingParameter;
        private const string ClimbingParameter = AnimatorParameterTranslator.DefaultClimbingParameter;
        private const string GrabbingParameter = AnimatorParameterTranslator.DefaultGrabbingParameter;
        private const string GrabSpeedParameter = AnimatorParameterTranslator.DefaultGrabSpeedParameter;
        private const string WallSideParameter = AnimatorParameterTranslator.DefaultWallSideParameter;
        private const string RollProgressParameter = AnimatorParameterTranslator.DefaultRollProgressParameter;
        private const string ClimbProgressParameter = AnimatorParameterTranslator.DefaultClimbProgressParameter;
        private const string VaultingParameter = AnimatorParameterTranslator.DefaultVaultingParameter;
        private const string VaultProgressParameter = AnimatorParameterTranslator.DefaultVaultProgressParameter;
        private const string RopeSwingingParameter = AnimatorParameterTranslator.DefaultRopeSwingingParameter;
        private const string ZipliningParameter = AnimatorParameterTranslator.DefaultZipliningParameter;
        private const string SwimmingParameter = AnimatorParameterTranslator.DefaultSwimmingParameter;
        private const string DivingParameter = AnimatorParameterTranslator.DefaultDivingParameter;
        // Past the end of the Locomotion, Crouch and Wall Climb blend trees, the translator plays the fastest loop faster
        // through these speed multipliers, from the speeds each loop is made for, which the controller carries as the
        // TopSpeed parameters' defaults (see AnimatorParameterTranslator's Playback section). So no top speed, which a
        // retuned character could outrun, is fixed here.
        private const string LocomotionPlaybackParameter = AnimatorParameterTranslator.DefaultLocomotionPlaybackParameter;
        private const string LocomotionTopSpeedParameter = AnimatorParameterTranslator.DefaultLocomotionTopSpeedParameter;
        private const string CrouchPlaybackParameter = AnimatorParameterTranslator.DefaultCrouchPlaybackParameter;
        private const string CrouchTopSpeedParameter = AnimatorParameterTranslator.DefaultCrouchTopSpeedParameter;
        private const string ClimbPlaybackParameter = AnimatorParameterTranslator.DefaultClimbPlaybackParameter;
        private const string ClimbUpTopSpeedParameter = AnimatorParameterTranslator.DefaultClimbUpTopSpeedParameter;
        private const string ClimbDownTopSpeedParameter = AnimatorParameterTranslator.DefaultClimbDownTopSpeedParameter;
        private const string ClimbLeftTopSpeedParameter = AnimatorParameterTranslator.DefaultClimbLeftTopSpeedParameter;
        private const string ClimbRightTopSpeedParameter = AnimatorParameterTranslator.DefaultClimbRightTopSpeedParameter;
        // Fired by the translator on every jump, from the ground, in the air, off a wall or a rope or a zipline, and on
        // every launch off a bounce pad (with the Quaternius libraries' jump).
        private const string JumpTrigger = "Jump";
        private const MovementEvents JumpEvents = MovementEvents.Jumped | MovementEvents.AirJumped | MovementEvents.WallJumped | MovementEvents.Bounced;
        // The libraries' landing plays this far (normalized) before handing back, or until the character moves faster than this (m/s).
        private const float LandingLength = 0.45f, MoveOnFromLanding = 0.5f;
        // The Quaternius libraries' clips used, where they're installed.
        private static readonly string[] LibraryClips = { "Walk_Loop", "Jog_Fwd_Loop", "Sprint_Loop", "Jump_Start", "Jump_Loop", "Jump_Land" };
        // The libraries' clips for the movement abilities, each used where it's installed (the mannequin's own fallback
        // otherwise; see BuildAbilityStates). Of them, those standing on the floor are raised onto the mannequin's, as
        // the locomotion clips are (see StandLibraryOnMannequinsFloor).
        private static readonly string[] AbilityLibraryClips = { "Crouch_Idle_Loop", "Crouch_Fwd_Loop", "Slide_Loop", "Roll", "Push_Loop", "ClimbUp_1m", "SafetyVault", "WallRun_L_Loop", "WallRun_R_Loop",
            "Climb_Idle_Loop", "Climb_Up_Loop", "Climb_Down_Loop", "Climb_Left_Loop", "Climb_Right_Loop", "Climb_Enter", "Climb_Exit", "ClimbLedge",
            "Swim_Idle_Loop", "Swim_Fwd_Loop" };
        private static readonly string[] GroundedAbilityLibraryClips = { "Crouch_Idle_Loop", "Crouch_Fwd_Loop", "Push_Loop" };
        // A climbing loop's speed (m/s) if it can't be measured on the mannequin (see MotionCaptureLocomotion.ClimbSpeed).
        private const float ClimbLoopFallbackSpeed = 1f;
        // Treading water, how far (m, on the mannequin) its head's pivot (the top of its neck) stands above the surface: the
        // chin just out of the water. Swimming along, how far below the surface its hips' pivot is: the back just breaking
        // it, the head at the water line (see FloatLibrarySwimsAtTheSwimDepth).
        private const float HeadAboveWater = 0.15f, HipsBelowWater = 0.05f;
        // The swimming speed (m/s, at the authored size) from which the Swim state shows the library's swim stroke fully:
        // below it, it blends from treading water. The stroke is on the spot (it has no strides to measure), so it's
        // shown at any swimming speed past this.
        private const float SwimStrokeSpeed = 1.5f;
        // How fast (a share of its own pace) Swim Underwater plays the swim stroke while the character drifts without
        // swimming: an easy stroke, lying flat as the body does underwater, rather than treading water upright.
        private const float UnderwaterDriftStrokePace = 0.35f;
        // How far through a roll (RollProgress) it starts blending into walking or running, if the character is moving
        // on, and how long that blend takes (s): it's done as the roll ends, so the roll flows into the stride.
        private const float RollBlendOutAt = 0.75f, RollBlendOutTime = 0.15f;
        // Seconds the Animator blends into a vault over (from a run, say): at 0.05, the stride's feet snapped to the vault's.
        private const float VaultBlendInTime = 0.12f;

        // The take Tools/Mannequin/mannequin_animations.py exports first so the file's default pose is the rest pose
        private const string BindPoseTake = "!BindPose";
        // The brisk walk is also shown past the speed its strides are made for, up to this many times it, played up to
        // the second this many times faster: so it quickens a little and its planted feet slide a little (it reaches
        // further than it's sped up). Few people walk much faster than a brisk walk (they break into a run), so a walk
        // past it must hurry or slide; this keeps both small. The run takes over past it, so a walking speed beyond it
        // shows the run. Both are shares of the clip's own speed, so they hold whatever it's measured at.
        private const float BriskWalkReach = 1.5f, BriskWalkPlayback = 1.25f;
        // Airborne, by vertical speed (m/s): rising at take-off (a jump leaves the ground faster than RiseThreshold), tucked at the top,
        // falling. Falling locks in quickly, so walking off a ledge only passes through the tuck.
        private const float FallThreshold = -5f;
        private const float RiseThreshold = 8f;

        /// <summary>
        /// Generates the mannequin in Blender and builds everything from it. If Blender isn't found, offers to locate
        /// it or to rebuild from the Mannequin.fbx already in the project.
        /// </summary>
        [MenuItem("Tools/Character Showcase/Generate Mannequin", priority = 1)]
        public static void GenerateFromMenu()
        {
            if (!BlenderRunner.FindOrAsk("Generate Mannequin", "the mannequin and its animations", "Mannequin.fbx", out string blender)) { return; }
            if (Generate(blender))
            {
                EditorUtility.DisplayDialog("Generate Mannequin", "The mannequin and its animations are ready. " +
                    "Run Tools > Character Showcase > Prepare Character Models to update the other models' prefabs too.", "OK");
            }
        }

        /// <summary>
        /// For -executeMethod. Pass -blender [path] to use a particular Blender, or -skipBlender to rebuild from the
        /// existing Mannequin.fbx. Exits when it's done, with code 1 on failure.
        /// </summary>
        public static void GenerateFromCommandLine()
        {
            if (!BlenderRunner.FromCommandLine(nameof(MannequinGenerator), "Mannequin.fbx", out string blender))
            {
                CommandLine.Finish(false);
                return;
            }
            CommandLine.Finish(Generate(blender));
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
            if (blenderPath != null
                && !BlenderRunner.RunScript(blenderPath, GeneratorScript, ModelPath, "mannequin", "Generate Mannequin", nameof(MannequinGenerator)))
            {
                return false;
            }
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
                // Bound by an earlier version, before wall jumps or bounce pads: they're jumps too. (Saved as a long: see
                // AnimatorParameterTranslator.EventTrigger.)
                SerializedProperty events = existing.FindPropertyRelative("events");
                var bound = (MovementEvents)events.longValue;
                if ((bound & MovementEvents.Jumped) != 0 && (bound & JumpEvents) != JumpEvents)
                {
                    events.longValue = (long)(bound | JumpEvents);
                    settings.ApplyModifiedPropertiesWithoutUndo();
                }
                return;
            }
            triggers.arraySize++;
            SerializedProperty binding = triggers.GetArrayElementAtIndex(triggers.arraySize - 1);
            binding.FindPropertyRelative("events").longValue = (long)JumpEvents;
            binding.FindPropertyRelative("trigger").stringValue = JumpTrigger;
            settings.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Sets up the Character prefab's model: a CharacterModelHost loading the mannequin (also its fallback, should
        /// another model fail to load), with a placeholder shown while there's no model and an eye point for the camera.
        /// The prefab has no collider for its body (see RemoveBodyColliders): its model's hitboxes are what meets it.
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
                RemoveBodyColliders(root);

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

        /// <summary>
        /// Takes the character's own primitive colliders off its root (the capsule an earlier version had for its body), and
        /// clears its MovementController's Body Collider: the movement needs no collider (its body is a shape: see
        /// CharacterBody), and what meets the character meets its model's hitboxes (see CharacterHitboxes). Its Body
        /// Definition is its body.
        /// </summary>
        public static void RemoveBodyColliders(GameObject character)
        {
            foreach (Collider collider in character.GetComponents<Collider>())
            {
                if (collider is CapsuleCollider || collider is BoxCollider || collider is SphereCollider) { Object.DestroyImmediate(collider); }
            }
            if (character.TryGetComponent(out MovementController movement))
            {
                var settings = new SerializedObject(movement);
                settings.FindProperty("bodyCollider").objectReferenceValue = null;
                settings.ApplyModifiedPropertiesWithoutUndo();
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
        /// A plain capsule the size of the character's body, shown while the character has no model. It lives under
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
            // The character's body as authored (its Body Definition's, or its collider's), standing.
            BodyShape body = root.TryGetComponent(out MovementController movement) ? movement.AuthoredBody : BodyShape.Default;
            GameObject placeholder = GameObject.CreatePrimitive(body.kind switch
            {
                BodyShapeKind.Box => PrimitiveType.Cube,
                BodyShapeKind.Sphere => PrimitiveType.Sphere,
                _ => PrimitiveType.Capsule,
            });
            placeholder.name = PlaceholderName;
            Object.DestroyImmediate(placeholder.GetComponent<Collider>()); // Only something to see: the body is the movement's.
            placeholder.transform.SetParent(modelParent, false);
            placeholder.layer = root.gameObject.layer;
            placeholder.transform.localPosition = body.center;
            // Unity's capsule mesh is 2 m tall and 1 m wide, along Y; its cube and sphere 1 m across.
            placeholder.transform.localScale = body.kind switch
            {
                BodyShapeKind.Box => body.size,
                BodyShapeKind.Sphere => Vector3.one * (2f * body.radius),
                _ => new Vector3(body.radius * 2f, Mathf.Max(body.height, 2f * body.radius) / 2f, body.radius * 2f),
            };
            if (body.kind == BodyShapeKind.Capsule && body.direction != 1)
            {
                placeholder.transform.localRotation = Quaternion.FromToRotation(Vector3.up, body.direction == 0 ? Vector3.right : Vector3.forward);
            }
            return placeholder;
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) { return asset; }
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }
    }
}
