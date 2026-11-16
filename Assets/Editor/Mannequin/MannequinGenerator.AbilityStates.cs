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
    // The Animator Controller's states for the movement abilities (see BuildAbilityStates).
    public static partial class MannequinGenerator
    {
        /// <summary>
        /// The states for the movement abilities: Crouch (standing still and crouch-walking, by Speed), Slide, Roll
        /// (timed by RollProgress), Wall Run (the library's wall run for a wall on that side, by WallSide, or the sprint;
        /// at the run's speed), Wall Slide, Wall Climb (the library's climbing loops, by ClimbRight and ClimbUp), Ladder (posed
        /// by LadderIK), Climb Enter and Exit, Hang, Climb
        /// (timed by ClimbProgress), Vault (timed by VaultProgress) and
        /// Push (pushing, or played backward pulling, by GrabSpeed). Each is entered from any state while its
        /// parameter is set, and left for Locomotion, Crouch or Airborne, whichever matches the character, once it's
        /// cleared. The Quaternius libraries' crouch, slide, roll, climb, vault, push and wall runs are used where they're installed, and
        /// the mannequin's own clips otherwise (its crouch, its dash for the slide, its tucked jump for the roll, its
        /// rise for the climb and the vault, and its walk for the push). Hanging, sliding down a wall, swinging on a rope
        /// (Rope Swing) and riding a zipline (Zipline) are always the mannequin's own.
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

            // Crouch: still, then crouch-walking at the speed its strides are made for, played faster past it (see
            // PlaysPastItsTop).
            AnimatorState crouch = stateMachine.AddState("Crouch");
            float crouchStrides = crouchWalk != crouchIdle ? Strides(crouchWalk) : 0f;
            if (crouchStrides > 0f)
            {
                crouch.motion = CreateBlendTree(controller, "Crouch", SpeedParameter, (crouchIdle, 0f, 1f, 0f), (crouchWalk, crouchStrides, 1f, 0f));
                PlaysPastItsTop(controller, crouch, CrouchPlaybackParameter, (CrouchTopSpeedParameter, crouchStrides));
            }
            else { crouch.motion = crouchIdle; }

            AnimatorState slide = stateMachine.AddState("Slide");
            slide.motion = Library("Slide_Loop", clips["Dash"]);

            // The roll and the climb play through once over the roll or climb, however long it's tuned to take.
            AnimatorState roll = stateMachine.AddState("Roll");
            roll.motion = Library("Roll", clips["Apex"]);
            roll.timeParameterActive = true;
            roll.timeParameter = RollProgressParameter;

            // Running along a wall: the library's wall runs (the Source edition of Universal Animation Library 2 has them),
            // one for a wall on each side, chosen by WallSide (-1 left, 1 right: their feet are toward the wall, and the
            // body leans away from it); or else sprinting. At the speed the character runs.
            AnimatorState wallRun = stateMachine.AddState("Wall Run");
            AnimationClip wallOnLeft = Library("WallRun_L_Loop", null), wallOnRight = Library("WallRun_R_Loop", null);
            AnimationClip wallRunStride = sprint;
            if (wallOnLeft != null && wallOnRight != null)
            {
                wallRun.motion = CreateBlendTree(controller, "Wall Run", WallSideParameter, (wallOnLeft, -1f, 1f, 0f), (wallOnRight, 1f, 1f, 0f));
                wallRunStride = wallOnRight;
            }
            else { wallRun.motion = sprint; }
            float wallRunStrides = Strides(wallRunStride);
            if (wallRunStrides > 0f)
            {
                wallRun.speedParameterActive = true;
                wallRun.speedParameter = SpeedParameter;
                wallRun.speed = 1f / wallRunStrides;
            }

            AnimatorState hang = stateMachine.AddState("Hang");
            hang.motion = clips["Hang"];

            AnimatorState wallSlide = stateMachine.AddState("Wall Slide");
            wallSlide.motion = clips["Wall Slide"];

            // Swinging on a rope and riding a zipline: the mannequin's own hangs (the libraries have neither), which
            // LineGripIK puts the hands of on the rope or the cable.
            AnimatorState ropeSwing = stateMachine.AddState("Rope Swing");
            ropeSwing.motion = clips["Rope Swing"];
            AnimatorState zipline = stateMachine.AddState("Zipline");
            zipline.motion = clips["Zipline"];

            // Climbing a climbable surface: the library's climbing loops (the Pro edition of Universal Animation Library 1
            // has them), blended by which way and how fast the character climbs (ClimbRight and ClimbUp, m/s): each loop at
            // the speed its holding hands are made for, played faster past it (see PlaysPastItsTop), and the climbing idle
            // in the middle. Without them,
            // the mannequin's hang, held still.
            AnimatorState wallClimb = stateMachine.AddState("Wall Climb");
            AnimationClip climbIdle = Library("Climb_Idle_Loop", null);
            var climbLoops = new (AnimationClip clip, Vector2 direction)[]
            {
                (Library("Climb_Up_Loop", null), Vector2.up), (Library("Climb_Down_Loop", null), Vector2.down),
                (Library("Climb_Left_Loop", null), Vector2.left), (Library("Climb_Right_Loop", null), Vector2.right),
            };
            if (climbIdle != null && climbLoops.All(l => l.clip != null))
            {
                var children = new List<(Motion, Vector2, float)> { (climbIdle, Vector2.zero, 1f) };
                var measured = new List<string>();
                var topSpeeds = new List<(string, float)>();
                string[] topSpeedParameters = { ClimbUpTopSpeedParameter, ClimbDownTopSpeedParameter, ClimbLeftTopSpeedParameter, ClimbRightTopSpeedParameter };
                for (int i = 0; i < climbLoops.Length; i++)
                {
                    var (clip, direction) = climbLoops[i];
                    float loopSpeed = MotionCaptureLocomotion.ClimbSpeed(measuringModel, clip, new Vector3(direction.x, direction.y, 0f));
                    if (!float.IsFinite(loopSpeed) || loopSpeed < 0.05f) { loopSpeed = ClimbLoopFallbackSpeed; }
                    children.Add((clip, direction * loopSpeed, 1f));
                    topSpeeds.Add((topSpeedParameters[i], loopSpeed));
                    measured.Add($"{clip.name} {loopSpeed:0.00} m/s");
                }
                wallClimb.motion = CreateBlendTree2D(controller, "Wall Climb", ClimbRightParameter, ClimbUpParameter, children.ToArray());
                PlaysPastItsTop(controller, wallClimb, ClimbPlaybackParameter, topSpeeds.ToArray());
                Debug.Log($"[MannequinGenerator] Climbing loops' speeds: {string.Join(", ", measured)}.");
            }
            else { wallClimb.motion = clips["Hang"]; }

            // Climbing a ladder: the library's climbing idle (or the mannequin's hang) as the pose, which LadderIK puts the
            // hands and feet of on the rungs, moving them rung to rung as the character climbs.
            AnimatorState ladderState = stateMachine.AddState("Ladder");
            ladderState.motion = climbIdle != null ? climbIdle : clips["Hang"];

            // Stepping onto a climbable surface from standing on the ground, and off its bottom onto the ground: the
            // library's climbing enter and exit (on the spot: the feet step onto the wall, or back down), played through
            // once over however long it's tuned to take. Without them, none: straight into the climb, or out of it.
            AnimationClip climbEnterClip = Library("Climb_Enter", null), climbExitClip = Library("Climb_Exit", null);
            AnimatorState climbEnter = null, climbExit = null;
            if (climbEnterClip != null && climbExitClip != null)
            {
                climbEnter = stateMachine.AddState("Climb Enter");
                climbEnter.motion = climbEnterClip;
                climbEnter.timeParameterActive = true;
                climbEnter.timeParameter = ClimbStepProgressParameter;
                climbExit = stateMachine.AddState("Climb Exit");
                climbExit.motion = climbExitClip;
                climbExit.timeParameterActive = true;
                climbExit.timeParameter = ClimbStepProgressParameter;
            }

            // Climbing up onto a ledge, from hanging or from climbing a wall below it: the library's climb onto a ledge
            // (from a climbing pose, as the hang's), or else its 1 m climb, or the mannequin's rise.
            AnimatorState climb = stateMachine.AddState("Climb");
            climb.motion = Library("ClimbLedge", Library("ClimbUp_1m", clips["Rise"]));
            climb.timeParameterActive = true;
            climb.timeParameter = ClimbProgressParameter;

            // Vaulting over something low: the library's safety vault (a hand on the top, the legs swung over beside
            // it), or else the mannequin's rise, played through once over the vault.
            AnimatorState vault = stateMachine.AddState("Vault");
            vault.motion = Library("SafetyVault", clips["Rise"]);
            vault.timeParameterActive = true;
            vault.timeParameter = VaultProgressParameter;

            // Pushing at the speed the push's strides are made for, and pulling the same played backward; standing
            // still holding the object, held still.
            AnimatorState pushPull = stateMachine.AddState("Push");
            pushPull.motion = push;
            float pushStrides = Strides(push);
            pushPull.speedParameterActive = true;
            pushPull.speedParameter = GrabSpeedParameter;
            pushPull.speed = pushStrides > 0f ? 1f / pushStrides : 1f;

            var abilityStates = new List<(AnimatorState state, string parameter, float enter)>
            {
                (crouch, CrouchingParameter, 0.15f), (slide, SlidingParameter, 0.1f), (roll, RollingParameter, 0.05f),
                (wallRun, WallRunningParameter, 0.12f), (wallSlide, WallSlidingParameter, 0.1f), (wallClimb, WallClimbingParameter, 0.15f), (ladderState, OnLadderParameter, 0.15f),
                (hang, HangingParameter, 0.1f), (climb, ClimbingParameter, 0.05f),
                (vault, VaultingParameter, 0.05f), (pushPull, GrabbingParameter, 0.15f),
                (ropeSwing, RopeSwingingParameter, 0.1f), (zipline, ZipliningParameter, 0.1f),
            };
            if (climbEnter != null)
            {
                abilityStates.Add((climbEnter, ClimbEnteringParameter, 0.1f));
                abilityStates.Add((climbExit, ClimbExitingParameter, 0.1f));
            }
            foreach (var (state, parameter, enter) in abilityStates)
            {
                AnimatorStateTransition start = stateMachine.AddAnyStateTransition(state);
                start.hasExitTime = false;
                start.duration = enter;
                start.canTransitionToSelf = false;
                start.AddCondition(AnimatorConditionMode.If, 0f, parameter);
                // Not back into the roll from walking on out of the end of one (see below), while it's still rolling.
                if (state == roll) { start.AddCondition(AnimatorConditionMode.Less, 0.5f, RollProgressParameter); }
                // Still climbing while stepping on or off, so the climb waits for them. Once stepped on, it takes over at
                // once (Any State transitions come first), rather than the step's own way back to the ground.
                if ((state == wallClimb || state == ladderState) && climbEnter != null)
                {
                    start.AddCondition(AnimatorConditionMode.IfNot, 0f, ClimbEnteringParameter);
                    start.AddCondition(AnimatorConditionMode.IfNot, 0f, ClimbExitingParameter);
                }
                // A ladder has a state of its own.
                if (state == wallClimb) { start.AddCondition(AnimatorConditionMode.IfNot, 0f, OnLadderParameter); }
                if (state == crouch)
                {
                    // The character can be crouched while doing something else (dashing, climbing onto a low ledge, or
                    // falling): that shows instead, or two Any State transitions would keep swapping the states.
                    start.AddCondition(AnimatorConditionMode.If, 0f, GroundedParameter);
                    foreach (string other in new[] { DashingParameter, SlidingParameter, RollingParameter, WallRunningParameter, WallSlidingParameter, WallClimbingParameter, OnLadderParameter, ClimbEnteringParameter, ClimbExitingParameter, HangingParameter, ClimbingParameter, VaultingParameter, GrabbingParameter, RopeSwingingParameter, ZipliningParameter })
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

                    // A roll from a crouch ends crouched: blend into the crouch over its end in the same way, rather
                    // than standing up out of it and crouching again.
                    AnimatorStateTransition crouchOn = state.AddTransition(crouch);
                    crouchOn.hasExitTime = false;
                    crouchOn.hasFixedDuration = true;
                    crouchOn.duration = RollBlendOutTime;
                    crouchOn.AddCondition(AnimatorConditionMode.Greater, RollBlendOutAt, RollProgressParameter);
                    crouchOn.AddCondition(AnimatorConditionMode.If, 0f, CrouchingParameter);
                    crouchOn.AddCondition(AnimatorConditionMode.If, 0f, GroundedParameter);
                }

                // A crouched character falling (walked off an edge crouched) shows the air too.
                AnimatorStateTransition toAir = state.AddTransition(airborne);
                toAir.hasExitTime = false;
                toAir.duration = 0.15f;
                if (state != crouch) { toAir.AddCondition(AnimatorConditionMode.IfNot, 0f, parameter); }
                toAir.AddCondition(AnimatorConditionMode.IfNot, 0f, GroundedParameter);
            }
        }
    }
}
