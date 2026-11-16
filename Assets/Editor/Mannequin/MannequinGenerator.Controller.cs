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
    // Building the shared Animator Controller: its parameters, locomotion blend tree, air states and dash, and the blend trees they use.
    public static partial class MannequinGenerator
    {
        private static AnimatorController BuildAnimatorController()
        {
            Dictionary<string, AnimationClip> clips = LoadClips();
            string[] required = { "Idle", "Walk", "Brisk Walk", "Run", "Sprint", "Rise", "Apex", "Fall", "Dash", "Crouch", "Hang", "Wall Slide", "Rope Swing", "Zipline", "Swim" };
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
                FloatLibrarySwimsAtTheSwimDepth(measuringModel);
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
            foreach (string ability in new[] { CrouchingParameter, SlidingParameter, RollingParameter, WallRunningParameter, WallSlidingParameter, WallClimbingParameter, OnLadderParameter, ClimbEnteringParameter, ClimbExitingParameter, HangingParameter, ClimbingParameter, VaultingParameter, GrabbingParameter, RopeSwingingParameter, ZipliningParameter, SwimmingParameter, DivingParameter })
            {
                controller.AddParameter(ability, AnimatorControllerParameterType.Bool);
            }
            foreach (string value in new[] { GrabSpeedParameter, RollProgressParameter, ClimbProgressParameter, VaultProgressParameter, WallSideParameter, ClimbRightParameter, ClimbUpParameter, ClimbStepProgressParameter })
            {
                controller.AddParameter(value, AnimatorControllerParameterType.Float);
            }

            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;

            // Each loop at the speed its strides are made for (measured on the mannequin), so its planted feet stay put
            // there, from walking to sprinting. The brisk walk is also shown a little past its own speed (see
            // BriskWalkReach); past the fastest loop's own speed, the translator plays it faster to match, however fast
            // (see SetPlayback). Each loop is offset to land its left foot at the same moment as the others
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
                Add(brisk, brisk.speed * BriskWalkReach, BriskWalkPlayback);
                Add(Measure(clips["Run"]));
                Add(Measure(library["Jog_Fwd_Loop"]));
                Add(Measure(library["Sprint_Loop"]));
            }
            else
            {
                Add(Measure(clips["Walk"]));
                Add(brisk);
                Add(brisk, brisk.speed * BriskWalkReach, BriskWalkPlayback);
                Add(Measure(clips["Run"]));
                Add(Measure(clips["Sprint"]));
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
            PlaysPastItsTop(controller, locomotion, LocomotionPlaybackParameter, (LocomotionTopSpeedParameter, loops[^1].speed));
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
        /// The controller asset, emptied of its parameters, layers, states and blend trees so it can be rebuilt. It's
        /// emptied in place rather than deleted and created again, because every character model's prefab refers to it:
        /// deleting it would leave them all without a controller until it was recreated, and a recreated asset can be
        /// given a new GUID, which they wouldn't find.
        /// </summary>
        /// <summary>
        /// Lets a speed-driven state play faster than its fastest loop, by however much faster the character moves (see
        /// AnimatorParameterTranslator's Playback section): its speed is multiplied by the playback parameter, which the
        /// translator sets from the speeds its loops are made for, carried as the given parameters' defaults. The playback
        /// parameter defaults to 1, so the state plays as it should if nothing sets it.
        /// </summary>
        private static void PlaysPastItsTop(AnimatorController controller, AnimatorState state, string playbackParameter,
            params (string name, float speed)[] topSpeeds)
        {
            AddFloat(controller, playbackParameter, 1f);
            foreach (var (name, speed) in topSpeeds) { AddFloat(controller, name, speed); }
            state.speedParameterActive = true;
            state.speedParameter = playbackParameter;
        }

        /// <summary>Adds a float parameter with the given default value, or sets the default of the one the controller has.</summary>
        private static void AddFloat(AnimatorController controller, string name, float value)
        {
            AnimatorControllerParameter[] parameters = controller.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].name != name) { continue; }
                parameters[i].defaultFloat = value;
                controller.parameters = parameters;
                return;
            }
            controller.AddParameter(new AnimatorControllerParameter { name = name, type = AnimatorControllerParameterType.Float, defaultFloat = value });
        }

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

        /// <summary>A 2D blend tree (freeform directional) over two parameters, each child at a position and playback speed.</summary>
        private static BlendTree CreateBlendTree2D(AnimatorController controller, string name, string parameterX, string parameterY,
            params (Motion motion, Vector2 position, float timeScale)[] children)
        {
            var tree = new BlendTree
            {
                name = name,
                blendType = BlendTreeType.FreeformDirectional2D,
                blendParameter = parameterX,
                blendParameterY = parameterY,
                hideFlags = HideFlags.HideInHierarchy,
            };
            foreach (var (motion, position, _) in children) { tree.AddChild(motion, position); }
            ChildMotion[] motions = tree.children;
            for (int i = 0; i < motions.Length; i++) { motions[i].timeScale = children[i].timeScale; }
            tree.children = motions;
            AssetDatabase.AddObjectToAsset(tree, controller);
            return tree;
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
