using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Character;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CharacterShowcase.EditorTools
{
    /// <summary>
    /// Generates the fox, the first creature that isn't a humanoid: a wolf-sized, low-poly fox with flat-coloured regions,
    /// and a character to play it with. Tools > Character Showcase > Generate Fox (or GenerateFromCommandLine):
    /// - runs Tools/Fox/generate_fox.py in Blender, which builds, rigs (a Generic rig, not a Humanoid), skins and
    ///   animates it (Tools/Fox/fox_animations.py), writing Fox.fbx (skipped with -skipBlender, rebuilding the rest from
    ///   the Fox.fbx that's there);
    /// - imports it as a Generic rig with its clips looping;
    /// - builds its Animator Controller: Locomotion (Idle, or Turn Step as it turns standing, by TurnSpeed; then Walk, Trot,
    ///   Gallop by Speed), Swim (Swim Idle and Swim by Speed, while Swimming), Wall Run (the gaits, while WallRunning), Crouch (Crouch Idle and Crouch
    ///   Walk by Speed, while Crouching) and Airborne (Jump Fall and Jump Rise by VerticalSpeed, while not Grounded);
    /// - saves its model prefab (with an "Eyes" point on its head, which the character's eye point and camera follow, and
    ///   paw placement, PawPlacementIK, planting its paws on uneven ground), an Animator parameter translator for it, and
    ///   its model source;
    /// - makes, if they aren't there, its body (a box, about its body's size: see BodyDefinition) and its movement mode
    ///   (the humanoid's, with what a fox can't do turned off and tuned for a fox), which are then the user's to tune;
    /// - and saves Fox.prefab, a variant of the Character prefab with the fox's body, movement and model: put it in a
    ///   scene's character spawner (its Character Prefab) to play the fox there.
    /// </summary>
    public static class FoxGenerator
    {
        private const string Folder = "Assets/Models/Creatures/Fox";
        public const string ModelPath = Folder + "/Fox.fbx";
        public const string ModelPrefabPath = Folder + "/Fox Model.prefab";
        public const string SourcePath = Folder + "/Fox.asset";
        public const string BodyPath = Folder + "/Fox Body.asset";
        public const string ControllerPath = Folder + "/Fox.controller";
        public const string TranslatorPath = Folder + "/Fox Animator Translator.asset";
        public const string MovementPath = "Assets/ScriptableObjects/Fox Movement.asset";
        public const string PrefabPath = "Assets/Prefabs/Fox.prefab";
        private const string CharacterPrefabPath = SceneGameSetup.CharacterPrefabPath;
        private const string HumanoidMovementPath = "Assets/ScriptableObjects/BasicMovementMode.asset";
        private const string GeneratorScript = "Tools/Fox/generate_fox.py";
        private const string BindPoseTake = "!BindPose";
        private const string LogName = nameof(FoxGenerator);

        // The transform on the fox's head its eye point follows (see CharacterModelHost.EyesName).
        private const string EyesName = CharacterModelHost.EyesName;
        // Where its eyes are, in the model's space (on its head bone), and which bone that is.
        private static readonly Vector3 EyesPosition = new(0f, 0.98f, 0.64f);
        private const string HeadBone = "Head";
        // The bone the body hangs from, and each leg's upper, lower and foot bones (see PawPlacementIK), and how far ahead
        // of the model's origin (m) its toes touch the floor standing: generate_fox.py's front and hind toe joints (ftoe,
        // htoe), whose places the bones don't record (they end at the paws' joints).
        private const string HipsBone = "Hips";
        private const float FrontToesAhead = 0.38f, HindToesAhead = -0.34f;
        // How far (m) each paw's mesh reaches beyond its contact toward a wall it's put on, wall-running (measured on the
        // mesh: the hind paws, longer, reach further).
        private const float FrontPawWallClearance = 0.015f, HindPawWallClearance = 0.045f;
        // The bones turned back against the body's pitch on slopes, and by how much of it, nose up and nose down: the tail
        // mostly going up (pitched up a steep ramp with the body, it swings down into it) and not at all going down (held
        // level, it reaches into the slope behind), the neck by half (the head looks ahead, not at the sky or its paws).
        private static readonly (string bone, float up, float down, float clearance)[] CounterPitchBones = { ("Tail1", 0.8f, 0f, TailClearance), ("Neck", 0.5f, 0.5f, 0f) };
        // How far (m) the tail's tip is kept above the ground under it (see PawPlacementIK's Ground Clearance): its
        // thickness there (its brush is thick), and a little more.
        private const float TailClearance = 0.12f;
        // The bones bent into a turn, and their shares of the bend: the spine, neck and head toward it, the tail away (its
        // tip swinging round toward the turn too), so the fox curves into it.
        private static readonly (string bone, float share)[] TurnBendBones =
        {
            ("Spine1", 0.25f), ("Spine2", 0.3f), ("Neck", 0.25f), ("Head", 0.2f), ("Tail1", -0.4f), ("Tail2", -0.3f),
        };
        private static readonly (string upper, string lower, string foot, bool front)[] LegBones =
        {
            ("FrontUpperLeg_L", "FrontLowerLeg_L", "FrontPaw_L", true),
            ("FrontUpperLeg_R", "FrontLowerLeg_R", "FrontPaw_R", true),
            ("HindUpperLeg_L", "HindLowerLeg_L", "HindFoot_L", false),
            ("HindUpperLeg_R", "HindLowerLeg_R", "HindFoot_R", false),
        };

        // The speed (m/s) the Swim clip's paddle is authored for (fox_animations.py's SWIM_SPEED): the fox's Swim Speed, set
        // when its movement is first made. Its layout's Swim threshold is the same (Fox Layout.asset: change them together).
        private const float SwimClipSpeed = 2.5f;
        // How fast the character turns (degrees a second), which the translator drives and the standing turn blends by.
        private const string TurnSpeedParameter = "TurnSpeed";

        private static readonly string[] Clips = { "Idle", "Walk", "Trot", "Gallop", "Jump Rise", "Jump Fall", "Crouch Idle", "Crouch Walk" };

        [MenuItem("Tools/Character Showcase/Generate Fox", priority = 3)]
        public static void GenerateFromMenu()
        {
            if (!BlenderRunner.FindOrAsk("Generate Fox", "the fox", "Fox.fbx", out string blender)) { return; }
            Generate(blender);
        }

        /// <summary>
        /// Generates the fox from the command line (-executeMethod CharacterShowcase.EditorTools.FoxGenerator.GenerateFromCommandLine):
        /// -blender &lt;path&gt; to say where Blender is, or -skipBlender to rebuild from the Fox.fbx that's there.
        /// </summary>
        public static void GenerateFromCommandLine()
        {
            if (!BlenderRunner.FromCommandLine(LogName, "Fox.fbx", out string blender))
            {
                CommandLine.Finish(false);
                return;
            }
            CommandLine.Finish(Generate(blender));
        }

        /// <summary>Generates the fox (in Blender, unless blenderPath is null) and its setup. Returns whether it succeeded.</summary>
        public static bool Generate(string blenderPath)
        {
            Directory.CreateDirectory(Folder);
            if (blenderPath != null && !BlenderRunner.RunScript(blenderPath, GeneratorScript, ModelPath, "fox", "Generate Fox", LogName))
            {
                return false;
            }
            // Blender wrote the file (and maybe its folder) outside Unity: found and imported afresh.
            AssetDatabase.Refresh();
            AssetDatabase.ImportAsset(ModelPath, ImportAssetOptions.ForceUpdate);
            if (!ConfigureImport()) { return false; }

            Dictionary<string, AnimationClip> clips = LoadClips();
            string[] missing = Clips.Where(c => !clips.ContainsKey(c)).ToArray();
            if (missing.Length > 0)
            {
                Debug.LogError($"[{LogName}] '{ModelPath}' is missing clips: {string.Join(", ", missing)}. Generate the fox with Blender.");
                return false;
            }

            var layout = AssetDatabase.LoadAssetAtPath<AnimatorLayout>(LayoutPath);
            if (layout == null)
            {
                Debug.LogError($"[{LogName}] There's no Animator Layout at '{LayoutPath}' to build the fox's controller from.");
                return false;
            }
            AnimatorController controller = AnimatorControllerBuilder.Build(layout);
            if (controller == null) { return false; }
            GameObject modelPrefab = SaveModelPrefab(controller);
            var translator = LoadOrCreate<AnimatorParameterTranslator>(TranslatorPath);
            // The mannequin's floats, and its turn speed, which stepping round on the spot blends by.
            translator.FloatParameters = AnimatorParameterTranslator.DefaultFloatParameters()
                .Append(new AnimatorParameterTranslator.FloatParameter("Turn Speed", TurnSpeedParameter, smoothed: true)).ToArray();
            EditorUtility.SetDirty(translator);
            BodyDefinition body = LoadOrCreateBody();
            DirectCharacterModelSource source = SaveSource(modelPrefab, translator, body);
            ComposedMovementMode movement = LoadOrCreateMovement();
            if (movement == null) { return false; }
            SavePrefab(source, body, movement);
            AssetDatabase.SaveAssets();
            Debug.Log($"[{LogName}] Generated the fox: '{PrefabPath}' (put it in a scene's character spawner to play it).");
            return true;
        }

        // ---- The model ----

        /// <summary>Imports the fox as a Generic rig (its own avatar), each clip looping, without the bind pose take.</summary>
        private static bool ConfigureImport()
        {
            if (AssetImporter.GetAtPath(ModelPath) is not ModelImporter importer)
            {
                Debug.LogError($"[{LogName}] '{ModelPath}' isn't there, or isn't a model. Generate the fox with Blender.");
                return false;
            }
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;
            importer.clipAnimations = importer.defaultClipAnimations
                .Where(take => take.takeName.Split('|').Last() != BindPoseTake)
                .Select(take =>
                {
                    take.name = take.takeName.Split('|').Last();
                    take.loopTime = true;
                    take.loopPose = false;
                    return take;
                })
                .ToArray();
            importer.SaveAndReimport();
            return true;
        }

        private static Dictionary<string, AnimationClip> LoadClips() =>
            AssetDatabase.LoadAllAssetsAtPath(ModelPath)
                .OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview__"))
                .ToDictionary(c => c.name);

        // The fox's Animator Controller's layout (its states, clips and transitions, edited there): built after its clips.
        public const string LayoutPath = Folder + "/Fox Layout.asset";

        /// <summary>
        /// Gives the model paw placement (PawPlacementIK), its legs found by their bones' names and their paws touching the
        /// floor at their toes, set on the model as it stands, in its bind pose. Its other settings are PawPlacementIK's defaults: the model
        /// prefab is built anew each time, so tune them there.
        /// </summary>
        private static void AddPawPlacement(GameObject model)
        {
            Transform[] bones = model.GetComponentsInChildren<Transform>(true);
            Transform Bone(string name) => bones.FirstOrDefault(t => t.name == name);
            var legs = new PawPlacementIK.Leg[LegBones.Length];
            for (int i = 0; i < LegBones.Length; i++)
            {
                var (upper, lower, foot, front) = LegBones[i];
                legs[i] = new PawPlacementIK.Leg
                {
                    upper = Bone(upper), lower = Bone(lower), foot = Bone(foot),
                    // A front leg's elbow points back, a hind leg's stifle forward.
                    bendDirection = front ? Vector3.back : Vector3.forward,
                    // Running along a wall, how far each paw's mesh reaches beyond its contact toward the wall.
                    wallClearance = front ? FrontPawWallClearance : HindPawWallClearance,
                };
                if (legs[i].upper == null || legs[i].lower == null || legs[i].foot == null)
                {
                    Debug.LogWarning($"[{LogName}] The fox is missing a bone of the leg {upper} > {lower} > {foot}: it has no paw placement.");
                    return;
                }
            }
            Transform hips = Bone(HipsBone);
            if (hips == null) { Debug.LogWarning($"[{LogName}] The fox has no '{HipsBone}' bone: it has no paw placement."); return; }
            if (!model.TryGetComponent(out PawPlacementIK paws)) { paws = model.AddComponent<PawPlacementIK>(); }
            paws.SetLegs(hips, legs);
            paws.SetCounterPitchBones(CounterPitchBones
                .Select(c => new PawPlacementIK.CounterPitchBone { bone = Bone(c.bone), noseUpShare = c.up, noseDownShare = c.down, groundClearance = c.clearance })
                .Where(c => c.bone != null)
                .ToArray());
            paws.SetTurnBendBones(TurnBendBones
                .Select(b => new PawPlacementIK.TurnBendBone { bone = Bone(b.bone), share = b.share })
                .Where(b => b.bone != null)
                .ToArray());
            // Each paw touches the floor at its toes, beside its leg.
            for (int i = 0; i < legs.Length; i++)
            {
                float across = model.transform.InverseTransformPoint(legs[i].foot.position).x;
                paws.SetContact(i, new Vector3(across, 0f, LegBones[i].front ? FrontToesAhead : HindToesAhead));
            }
        }

        /// <summary>
        /// The bones the fox's hitboxes are on (see HitboxFitter): its body, head and tail, and each leg's three. Its
        /// muzzle and ears go to its head's, its hind paws to its hind feet's.
        /// </summary>
        public static readonly string[] HitboxBones =
        {
            "Hips", "Spine1", "Spine2", "Neck", "Head", "Tail1", "Tail2", "Tail3", "Tail4",
            "FrontUpperLeg_L", "FrontLowerLeg_L", "FrontPaw_L", "FrontUpperLeg_R", "FrontLowerLeg_R", "FrontPaw_R",
            "HindUpperLeg_L", "HindLowerLeg_L", "HindFoot_L", "HindUpperLeg_R", "HindLowerLeg_R", "HindFoot_R",
        };

        /// <summary>
        /// The fox's model prefab: the model, its Animator playing the controller, its eyes on its head, its paw placement,
        /// and its hitboxes (with the settings its earlier prefab's had).
        /// </summary>
        private static GameObject SaveModelPrefab(AnimatorController controller)
        {
            var earlier = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPrefabPath);
            CharacterHitboxes earlierHitboxes = earlier != null ? earlier.GetComponent<CharacterHitboxes>() : null;
            var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath));
            try
            {
                PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                model.name = "Fox Model";
                Animator animator = model.GetComponent<Animator>();
                if (animator == null) { animator = model.AddComponent<Animator>(); }
                animator.avatar = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<Avatar>().FirstOrDefault();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false; // Movement is driven by MovementController, not animation.
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                Transform head = model.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == HeadBone);
                if (head != null)
                {
                    var eyes = new GameObject(EyesName).transform;
                    eyes.SetParent(head, false);
                    eyes.position = model.transform.TransformPoint(EyesPosition);
                }
                else { Debug.LogWarning($"[{LogName}] The fox has no '{HeadBone}' bone to put its eyes on."); }
                AddPawPlacement(model);
                HitboxFitter.Fit(model, HitboxBones, earlierHitboxes);
                return PrefabUtility.SaveAsPrefabAsset(model, ModelPrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(model);
            }
        }

        /// <summary>
        /// The fox's body, made if it isn't there (then left as it's tuned), not refitted to the model: rounded, so it has
        /// no corners to catch on things, and held above its legs (they're under it, over steps: see Hover Over Steps).
        /// Its back is two capsules lying along it, one above the other (a body narrower than it's deep), from its rump
        /// to its chest, the lower one's bottom a step above its paws; its head a sphere ahead of them, as high as its head
        /// is (not its ears or tail).
        /// </summary>
        private static BodyDefinition LoadOrCreateBody()
        {
            var body = AssetDatabase.LoadAssetAtPath<BodyDefinition>(BodyPath);
            if (body != null) { return body; }
            body = ScriptableObject.CreateInstance<BodyDefinition>();
            body.Shape = BackCapsule(0.52f);
            body.ExtraParts.Add(BackCapsule(0.66f));
            BodyShape head = BodyShape.Sphere(0.15f);
            head.center = new Vector3(0f, 0.8f, 0.56f);
            body.ExtraParts.Add(head);
            body.RefitOnPrepare = false;
            AssetDatabase.CreateAsset(body, BodyPath);
            return body;
        }

        /// <summary>A capsule lying along the fox's back (0.34 m across, 0.95 m long), its middle at the given height.</summary>
        private static BodyShape BackCapsule(float height) => new BodyShape
        {
            kind = BodyShapeKind.Capsule, direction = 2, radius = 0.17f, height = 0.95f, center = new Vector3(0f, height, -0.05f),
        };

        private static DirectCharacterModelSource SaveSource(GameObject modelPrefab, AnimatorParameterTranslator translator, BodyDefinition body)
        {
            var source = LoadOrCreate<DirectCharacterModelSource>(SourcePath);
            source.ModelPrefab = modelPrefab;
            source.Translator = translator;
            source.Height = ModelHeight.KeepModelSize;
            source.Scale = 1f;
            // Built standing on its paws at its origin, facing forward: nothing to measure as it loads.
            source.MeasureGroundOnLoad = false;
            source.GroundOffset = 0f;
            source.AnimatedGroundOffset = 0f;
            source.FacingCorrection = 0f;
            source.BodyDefinition = body;
            source.RecordMeasurements(CharacterModelSource.MeasureHeight(modelPrefab, modelPrefab.transform));
            EditorUtility.SetDirty(source);
            return source;
        }

        // ---- The character ----

        // How deep (a share of the fox's height) its feet hang below the surface swimming: its back at the waterline.
        private const float SwimDepth = 0.8f;
        // How far out from a wall (a share of its height) the fox's middle is held running along it: near enough that its
        // body need only roll a little toward the wall for its paws to reach it.
        private const float WallRunClearance = 0.25f;

        // The humanoid's movement components a fox can't use (yet): turned off in its movement mode.
        private static readonly Type[] HumanOnly =
        {
            typeof(Ziplines), typeof(RopeSwinging), typeof(Ledges), typeof(Vaulting), typeof(PushingAndPulling), typeof(WallClimbing),
            typeof(WallSliding), typeof(Rolling), typeof(Dashing),
        };

        /// <summary>
        /// The fox's movement mode, made if it isn't there (then left as it's tuned): the humanoid's, with what the fox
        /// can't do turned off, sliding off, one jump, a fox's speeds (it trots as it walks, and gallops sprinting), and
        /// swimming and wall-running tuned for a fox.
        /// </summary>
        private static ComposedMovementMode LoadOrCreateMovement()
        {
            var mode = AssetDatabase.LoadAssetAtPath<ComposedMovementMode>(MovementPath);
            if (mode != null) { return mode; }
            var humanoid = AssetDatabase.LoadAssetAtPath<ComposedMovementMode>(HumanoidMovementPath);
            if (humanoid == null)
            {
                Debug.LogError($"[{LogName}] '{HumanoidMovementPath}' isn't there to start the fox's movement from.");
                return null;
            }
            mode = Object.Instantiate(humanoid);
            foreach (Type type in HumanOnly)
            {
                if (mode.Get(type, includeDisabled: true) is { } component) { component.Enabled = false; }
            }
            Set<WalkingAndRunning>(mode, "walkSpeed", 3.5f);
            Set<WalkingAndRunning>(mode, "runSpeed", 10f);
            Set<Jumping>(mode, "maxJumps", 1);
            Set<Jumping>(mode, "minJumpHeight", 0.5f);
            Set<Jumping>(mode, "maxJumpHeight", 1.4f);
            Set<Crouching>(mode, "crouchSpeed", 1f);
            Set<Crouching>(mode, "crouchHeight", 0.65f);
            Set<Crouching>(mode, "slidingEnabled", false);
            // Swimming at the surface only (a fox doesn't dive), its back at the waterline, its head up out of the water;
            // a fox's paddle is slower than a person's crawl, and it jumps out onto lower edges.
            Set<Swimming>(mode, "swimDepth", SwimDepth);
            Set<Swimming>(mode, "swimSpeed", SwimClipSpeed);
            Set<Swimming>(mode, "swimSprintSpeed", 3.5f);
            Set<Swimming>(mode, "swimJumpHeight", 0.8f);
            Set<Swimming>(mode, "swimVaultHeight", 0f);
            Set<Swimming>(mode, "swimDiveSpeed", 0f);
            // Wall-running held a little out from the wall (its body rolled toward it, its paws on it), from lower jumps.
            Set<WallRunning>(mode, "wallRunClearance", WallRunClearance);
            Set<WallRunning>(mode, "wallRunMinHeight", 0.5f);
            AssetDatabase.CreateAsset(mode, MovementPath);
            return mode;
        }

        private static void Set<T>(ComposedMovementMode mode, string field, object value) where T : MovementComponent
        {
            MovementComponent component = mode.Get(typeof(T), includeDisabled: true);
            FieldInfo info = typeof(T).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (component == null || info == null)
            {
                Debug.LogWarning($"[{LogName}] Couldn't set {typeof(T).Name}'s '{field}' for the fox.");
                return;
            }
            info.SetValue(component, value);
        }

        /// <summary>
        /// Fox.prefab: a variant of the Character prefab with the fox's body (a box: its Body Definition, taken as defined,
        /// not fitted to the model, held above its paws over steps: Hover Over Steps), movement mode and model, and an eye
        /// height for while its model loads. Like the
        /// Character prefab, it has no collider of its own: its body is a shape, and its model's hitboxes are what meets it.
        /// </summary>
        private static void SavePrefab(DirectCharacterModelSource source, BodyDefinition body, ComposedMovementMode movement)
        {
            var basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterPrefabPath);
            var fox = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
            try
            {
                fox.name = "Fox";
                var movementController = fox.GetComponent<MovementController>();
                // No collider for its body (one an earlier version added is removed).
                MannequinGenerator.RemoveBodyColliders(fox);
                movementController.movementHandler = movement;

                var moved = new SerializedObject(movementController);
                moved.FindProperty("bodyCollider").objectReferenceValue = null;
                // Its long body held above its paws, over steps, so it never rests on a step's edge or its corners.
                moved.FindProperty("hoverOverSteps").boolValue = true;
                moved.FindProperty("bodyDefinition").objectReferenceValue = body;
                moved.ApplyModifiedPropertiesWithoutUndo();

                var host = new SerializedObject(fox.GetComponent<CharacterModelHost>());
                host.FindProperty("modelSource").objectReferenceValue = source;
                host.FindProperty("bodyFit").enumValueIndex = (int)BodyFit.Off;
                host.FindProperty("defaultEyeHeight").floatValue = EyesPosition.y;
                host.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(fox, PrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(fox);
            }
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) { return asset; }
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }
    }
}
