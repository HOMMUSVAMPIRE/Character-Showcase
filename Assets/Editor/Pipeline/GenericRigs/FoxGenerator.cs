using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MeridianMotion.Characters;
using MeridianMotion.EditorTools;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MeridianMotion.Showcase.Pipeline
{
    /// <summary>
    /// Generates the fox, the first generic-rig character that isn't a humanoid: a wolf-sized, low-poly fox with flat-coloured regions,
    /// and a character to play it with. Tools > Meridian Motion > Showcase > Generate Fox (or GenerateFromCommandLine):
    /// - runs Tools/Fox/generate_fox.py in Blender, which builds, rigs (a Generic rig, not a Humanoid), skins and
    ///   animates it (Tools/Fox/fox_animations.py), writing Fox.fbx (skipped with -skipBlender, rebuilding the rest from
    ///   the Fox.fbx that's there);
    /// - writes, if they aren't there, the fox's Generic Rig Setup (its bones, which this generator knows, its paws' contacts
    ///   at its toes, and its layout, Fox Layout.asset), its body and its movement mode (the humanoid's, with what a fox
    ///   can't do turned off and tuned for a fox), which are then hand-edited sources;
    /// - then prepares it as any generic-rig character is (GenericRigModelImporter, which ships: its model prefab, paw placement,
    ///   hitboxes, Animator Controller, translator and model source), and saves Fox.prefab as New Character makes a
    ///   generic-rig character's prefab (NewCharacter.SaveVariant): put it in a scene's character spawner to play the fox there.
    /// Everything after Blender is the shipped generic-rig character path: only making the model is this generator's own.
    /// </summary>
    public static class FoxGenerator
    {
        private const string Folder = GenericRigModelImporter.ModelsFolder + "/Fox";
        public const string ModelPath = Folder + "/Fox.fbx";
        public const string SetupPath = Folder + "/Fox Setup.asset";
        public const string BodyPath = Folder + "/Fox Body.asset";
        public const string MovementPath = "Assets/ScriptableObjects/Fox Movement.asset";
        public const string PrefabPath = "Assets/Prefabs/Fox.prefab";
        // The fox's Animator Controller's layout (its states, clips and transitions, edited there): built after its clips.
        public const string LayoutPath = Folder + "/Fox Layout.asset";
        private const string HumanoidMovementPath = "Assets/ScriptableObjects/StandardMovementMode.asset";
        private const string GeneratorScript = "Tools/Fox/generate_fox.py";
        private const string LogName = nameof(FoxGenerator);

        // ---- The fox's setup, written only where it's missing (then a hand-edited source: see Fox Setup.asset) ----

        // Where its eyes are, in the model's space (on its head bone), and which bone that is.
        private static readonly Vector3 EyesPosition = new(0f, 0.98f, 0.64f);
        private const string HeadBone = "Head";
        // The bone the body hangs from, and each leg's upper, lower and foot bones (see LegPlacementIK), and how far ahead
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
        // How far (m) the tail's tip is kept above the ground under it (see LegPlacementIK's Ground Clearance): its
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

        // The speed (m/s) the Swim clip's paddle is authored for (fox_animations.py's SWIM_SPEED): the fox's Swim Speed, set
        // when its movement is first made. Its layout's Swim threshold is the same (Fox Layout.asset: change them together).
        private const float SwimClipSpeed = 2.5f;

        private static readonly string[] Clips = { "Idle", "Walk", "Trot", "Gallop", "Jump Rise", "Jump Fall", "Crouch Idle", "Crouch Walk" };

        [MenuItem("Tools/Meridian Motion/Showcase/Generate Fox", priority = 3)]
        public static void GenerateFromMenu()
        {
            if (!BlenderRunner.FindOrAsk("Generate Fox", "the fox", "Fox.fbx", out string blender)) { return; }
            Generate(blender);
        }

        /// <summary>
        /// Generates the fox from the command line (-executeMethod MeridianMotion.Showcase.Pipeline.FoxGenerator.GenerateFromCommandLine):
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

        /// <summary>Generates the fox (in Blender, unless blenderPath is null), then prepares it. Returns whether it succeeded.</summary>
        public static bool Generate(string blenderPath)
        {
            Directory.CreateDirectory(Folder);
            if (blenderPath != null && !BlenderRunner.RunScript(blenderPath, GeneratorScript, ModelPath, "fox", "Generate Fox", LogName))
            {
                return false;
            }
            // The generic-rig character postprocessor stands aside while this runs: the fox is prepared here, once its setup is written.
            using (HumanoidModelImporter.Busy())
            {
                // Blender wrote the file (and maybe its folder) outside Unity: found and imported afresh, its takes (which
                // may have changed) made its clips again.
                AssetDatabase.Refresh();
                AssetDatabase.ImportAsset(ModelPath, ImportAssetOptions.ForceUpdate);
                if (!GenericRigModelImporter.ConfigureImport(ModelPath, takesAnew: true))
                {
                    Debug.LogError($"[{LogName}] '{ModelPath}' isn't there, or isn't a model. Generate the fox with Blender.");
                    return false;
                }
                string[] clips = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<AnimationClip>().Select(c => c.name).ToArray();
                string[] missing = Clips.Where(c => !clips.Contains(c)).ToArray();
                if (missing.Length > 0)
                {
                    Debug.LogError($"[{LogName}] '{ModelPath}' is missing clips: {string.Join(", ", missing)}. Generate the fox with Blender.");
                    return false;
                }

                GenericRigSetup setup = LoadOrWriteSetup();
                if (setup == null) { return false; }
                LoadOrCreateBody();
                GenericRigModelImporter.Result result = GenericRigModelImporter.Prepare(setup);
                if (!result.Succeeded)
                {
                    Debug.LogError($"[{LogName}] {result}");
                    return false;
                }
                AbilityMovementMode movement = LoadOrCreateMovement();
                if (movement == null) { return false; }
                NewCharacter.SaveVariant("Fox", PrefabPath, result.source, movement);
                AssetDatabase.SaveAssets();
                GenericRigModelImporter.MarkPrepared(ModelPath, setup);
            }
            Debug.Log($"[{LogName}] Generated the fox: '{PrefabPath}' (put it in a scene's character spawner to play it).");
            return true;
        }

        /// <summary>
        /// The fox's Generic Rig Setup, written if it isn't there (then left as it's edited): its bones as generate_fox.py
        /// names them, each paw's contact at its toes beside its leg (set from the model as it stands, in its bind pose),
        /// the legs' bend directions and wall clearances, the tail and neck turned back against the pitch, the spine, neck,
        /// head and tail bent into turns, its hitbox bones, and its layout.
        /// </summary>
        private static GenericRigSetup LoadOrWriteSetup()
        {
            var setup = AssetDatabase.LoadAssetAtPath<GenericRigSetup>(SetupPath);
            if (setup != null) { return setup; }
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            var layout = AssetDatabase.LoadAssetAtPath<AnimatorLayout>(LayoutPath);
            if (layout == null)
            {
                Debug.LogError($"[{LogName}] There's no Animator Layout at '{LayoutPath}' to build the fox's controller from.");
                return null;
            }
            Transform[] bones = model.GetComponentsInChildren<Transform>(true);
            setup = ScriptableObject.CreateInstance<GenericRigSetup>();
            setup.model = model;
            setup.rootBone = HipsBone;
            setup.legs = LegBones.Select(leg =>
            {
                Transform foot = bones.FirstOrDefault(t => t.name == leg.foot);
                float across = foot != null ? model.transform.InverseTransformPoint(foot.position).x : 0f;
                return new GenericRigSetup.Leg
                {
                    upper = leg.upper, lower = leg.lower, foot = leg.foot,
                    // A front leg's elbow points back, a hind leg's stifle forward.
                    bendDirection = leg.front ? Vector3.back : Vector3.forward,
                    // Each paw touches the floor at its toes, beside its leg.
                    contact = new Vector3(across, 0f, leg.front ? FrontToesAhead : HindToesAhead),
                    wallClearance = leg.front ? FrontPawWallClearance : HindPawWallClearance,
                };
            }).ToArray();
            setup.eyeBone = HeadBone;
            setup.eyePosition = EyesPosition;
            setup.counterPitchBones = CounterPitchBones
                .Select(c => new GenericRigSetup.CounterPitchBone { bone = c.bone, noseUpShare = c.up, noseDownShare = c.down, groundClearance = c.clearance }).ToArray();
            setup.turnBendBones = TurnBendBones.Select(b => new GenericRigSetup.TurnBendBone { bone = b.bone, share = b.share }).ToArray();
            setup.hitboxBones = HitboxBones.ToArray();
            setup.layout = layout;
            setup.notes = "The fox's, written by Generate Fox where it was missing: its bones are generate_fox.py's, its paws' " +
                          "contacts at its toe joints (ftoe, htoe), which its bones don't record.";
            AssetDatabase.CreateAsset(setup, SetupPath);
            return setup;
        }

        /// <summary>
        /// The fox's body, made if it isn't there (then left as it's tuned), not refitted to the model: rounded, so it has
        /// no corners to catch on things, and held above its legs (they're under it, over steps: see Hover Over Steps).
        /// Its back is two capsules lying along it, one above the other (a body narrower than it's deep), from its rump
        /// to its chest, the lower one's bottom a step above its paws; its head a sphere ahead of them, as high as its head
        /// is (not its ears or tail). Made before preparing, which would otherwise fit one from the mesh (much like it).
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
        private static AbilityMovementMode LoadOrCreateMovement()
        {
            var mode = AssetDatabase.LoadAssetAtPath<AbilityMovementMode>(MovementPath);
            if (mode != null) { return mode; }
            var humanoid = AssetDatabase.LoadAssetAtPath<AbilityMovementMode>(HumanoidMovementPath);
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
            Set<Crouching>(mode, "canSlide", false);
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

        private static void Set<T>(AbilityMovementMode mode, string field, object value) where T : MovementComponent
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
    }
}
