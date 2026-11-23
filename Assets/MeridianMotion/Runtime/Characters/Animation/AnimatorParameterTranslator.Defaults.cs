using System;
using UnityEngine;

namespace MeridianMotion.Characters
{
    // What a new translator starts with: the mannequin's controller's parameter names, and the animation state's values and bools by name.
    public partial class AnimatorParameterTranslator
    {
        // The parameter names the mannequin's Animator Controller is built with
        /// <summary>
        /// The Speed parameter's default name (float): horizontal speed (m/s), as the model would move at its authored
        /// size.
        /// </summary>
        public const string DefaultSpeedParameter = "Speed";
        /// <summary>
        /// The VerticalSpeed parameter's default name (float): vertical speed (m/s, positive rising), as at the
        /// authored size.
        /// </summary>
        public const string DefaultVerticalSpeedParameter = "VerticalSpeed";
        /// <summary>The Grounded parameter's default name (bool): animated as on the ground.</summary>
        public const string DefaultGroundedParameter = "Grounded";
        /// <summary>The Dashing parameter's default name (bool): dashing.</summary>
        public const string DefaultDashingParameter = "Dashing";
        /// <summary>The Crouching parameter's default name (bool): crouched.</summary>
        public const string DefaultCrouchingParameter = "Crouching";
        /// <summary>The Sliding parameter's default name (bool): sliding.</summary>
        public const string DefaultSlidingParameter = "Sliding";
        /// <summary>The Rolling parameter's default name (bool): rolling.</summary>
        public const string DefaultRollingParameter = "Rolling";
        /// <summary>The WallRunning parameter's default name (bool): running along a wall.</summary>
        public const string DefaultWallRunningParameter = "WallRunning";
        /// <summary>The WallSliding parameter's default name (bool): sliding down a wall.</summary>
        public const string DefaultWallSlidingParameter = "WallSliding";
        /// <summary>The WallClimbing parameter's default name (bool): climbing a climbable surface or ladder.</summary>
        public const string DefaultWallClimbingParameter = "WallClimbing";
        /// <summary>
        /// The ClimbRight parameter's default name (float): climbing speed (m/s) to the right across the surface.
        /// </summary>
        public const string DefaultClimbRightParameter = "ClimbRight";
        /// <summary>The ClimbUp parameter's default name (float): climbing speed (m/s) up the surface.</summary>
        public const string DefaultClimbUpParameter = "ClimbUp";
        /// <summary>
        /// The ClimbEntering parameter's default name (bool): stepping onto a climbable surface from the ground.
        /// </summary>
        public const string DefaultClimbEnteringParameter = "ClimbEntering";
        /// <summary>The OnLadder parameter's default name (bool): climbing a ladder.</summary>
        public const string DefaultOnLadderParameter = "OnLadder";
        /// <summary>
        /// The ClimbExiting parameter's default name (bool): stepping off a climbable surface's bottom onto the ground.
        /// </summary>
        public const string DefaultClimbExitingParameter = "ClimbExiting";
        /// <summary>
        /// The ClimbStepProgress parameter's default name (float): how far through stepping onto or off a climbable
        /// surface (0 to 1).
        /// </summary>
        public const string DefaultClimbStepProgressParameter = "ClimbStepProgress";
        /// <summary>The WallSide parameter's default name (float): the wall run's side (1 right, -1 left).</summary>
        public const string DefaultWallSideParameter = "WallSide";
        /// <summary>The Hanging parameter's default name (bool): hanging from a ledge.</summary>
        public const string DefaultHangingParameter = "Hanging";
        /// <summary>The Climbing parameter's default name (bool): climbing up from a ledge onto it.</summary>
        public const string DefaultClimbingParameter = "Climbing";
        /// <summary>The Grabbing parameter's default name (bool): holding an object to push or pull.</summary>
        public const string DefaultGrabbingParameter = "Grabbing";
        /// <summary>
        /// The GrabSpeed parameter's default name (float): push (positive) or pull (negative) speed (m/s) while holding
        /// an object.
        /// </summary>
        public const string DefaultGrabSpeedParameter = "GrabSpeed";
        /// <summary>The RollProgress parameter's default name (float): how far through a roll (0 to 1).</summary>
        public const string DefaultRollProgressParameter = "RollProgress";
        /// <summary>
        /// The ClimbProgress parameter's default name (float): how far through a climb onto a ledge (0 to 1).
        /// </summary>
        public const string DefaultClimbProgressParameter = "ClimbProgress";
        /// <summary>The Vaulting parameter's default name (bool): vaulting over something low.</summary>
        public const string DefaultVaultingParameter = "Vaulting";
        /// <summary>
        /// The VaultProgress parameter's default name (float): about how far through a vault (0 to 1).
        /// </summary>
        public const string DefaultVaultProgressParameter = "VaultProgress";
        /// <summary>The RopeSwinging parameter's default name (bool): swinging on a rope.</summary>
        public const string DefaultRopeSwingingParameter = "RopeSwinging";
        /// <summary>The Ziplining parameter's default name (bool): riding a zipline.</summary>
        public const string DefaultZipliningParameter = "Ziplining";
        /// <summary>The Swimming parameter's default name (bool): swimming at the surface of deep water.</summary>
        public const string DefaultSwimmingParameter = "Swimming";
        /// <summary>The Diving parameter's default name (bool): swimming under the surface (Swimming is set too).</summary>
        public const string DefaultDivingParameter = "Diving";
        /// <summary>The LocomotionPlayback parameter's default name (float): how much faster than its fastest loop the Locomotion state plays (see the Playback section).</summary>
        public const string DefaultLocomotionPlaybackParameter = "LocomotionPlayback";
        /// <summary>The LocomotionTopSpeed parameter's default name (float, read, not set): the speed (m/s) the Locomotion state's fastest loop is made for.</summary>
        public const string DefaultLocomotionTopSpeedParameter = "LocomotionTopSpeed";
        /// <summary>The CrouchPlayback parameter's default name (float): how much faster than the crouch walk is made for the Crouch state plays.</summary>
        public const string DefaultCrouchPlaybackParameter = "CrouchPlayback";
        /// <summary>The CrouchTopSpeed parameter's default name (float, read, not set): the speed (m/s) the crouch walk is made for.</summary>
        public const string DefaultCrouchTopSpeedParameter = "CrouchTopSpeed";
        /// <summary>The ClimbPlayback parameter's default name (float): how much faster than its climbing loops the Wall Climb state plays.</summary>
        public const string DefaultClimbPlaybackParameter = "ClimbPlayback";
        /// <summary>The ClimbUpTopSpeed parameter's default name (float, read, not set): the speed (m/s) the climbing-up loop is made for.</summary>
        public const string DefaultClimbUpTopSpeedParameter = "ClimbUpTopSpeed";
        /// <summary>The ClimbDownTopSpeed parameter's default name (float, read, not set): the speed (m/s) the climbing-down loop is made for.</summary>
        public const string DefaultClimbDownTopSpeedParameter = "ClimbDownTopSpeed";
        /// <summary>The ClimbLeftTopSpeed parameter's default name (float, read, not set): the speed (m/s) the climbing-left loop is made for.</summary>
        public const string DefaultClimbLeftTopSpeedParameter = "ClimbLeftTopSpeed";
        /// <summary>The ClimbRightTopSpeed parameter's default name (float, read, not set): the speed (m/s) the climbing-right loop is made for.</summary>
        public const string DefaultClimbRightTopSpeedParameter = "ClimbRightTopSpeed";

        /// <summary>
        /// The animation state's own values a float parameter can show, by name (its source). Speeds are given as the
        /// authored model would move: a scaled model's strides are scaled too, so playing its clips for its speed divided by
        /// its stride scale keeps its planted feet in place at any size. NaN leaves the parameter as it is (Wall Side, out
        /// of a wall run, keeps the side it was, so an animation leaving the wall doesn't swing through the middle). A value
        /// an ability publishes is a source too, with no row here (AbilityPrefix).
        /// </summary>
        public static readonly (string name, StateValue value)[] AnimationFloats =
        {
            ("Speed", (in CharacterAnimationState s) => s.horizontalSpeed / s.StrideScale),
            // Jump speeds scale with the character's speeds (see MMotionMotor.SpeedScale), so this keeps the rising
            // and falling poses at the same points of a jump at any size.
            ("Vertical Speed", (in CharacterAnimationState s) => s.verticalSpeed / (s.speedScale > 0f ? s.speedScale : 1f)),
            ("Forward Speed", (in CharacterAnimationState s) => s.localVelocity.z / ModelScale(s).z),
            ("Sideways Speed", (in CharacterAnimationState s) => s.localVelocity.x / ModelScale(s).x),
            ("Turn Speed", (in CharacterAnimationState s) => s.turnSpeed),
            // As the authored model would push or pull.
            ("Grab Speed", (in CharacterAnimationState s) => s.grabbing ? s.localVelocity.z / ModelScale(s).z : 0f),
            ("Roll Progress", (in CharacterAnimationState s) => s.rollProgress),
            ("Climb Progress", (in CharacterAnimationState s) => s.climbProgress),
            ("Vault Progress", (in CharacterAnimationState s) => s.vaultProgress),
            // Climbing, the character faces the surface, so its velocity relative to its facing is the climb: x to the
            // right, y up. As the authored model would climb, so its hands keep their holds.
            ("Climb Right", (in CharacterAnimationState s) => s.wallClimbing ? s.localVelocity.x / ModelScale(s).x : 0f),
            ("Climb Up", (in CharacterAnimationState s) => s.wallClimbing ? s.localVelocity.y / ModelScale(s).y : 0f),
            ("Climb Step Progress", (in CharacterAnimationState s) => s.climbStepProgress),
            // Only during a wall run, and kept after it ends (NaN), so an animation leaving the wall doesn't swing through the middle.
            ("Wall Side", (in CharacterAnimationState s) => s.wallRunning && s.wallSide != 0f ? s.wallSide : float.NaN),
        };

        /// <summary>
        /// A new translator's float parameters: the mannequin's Animator Controller's (each named its Default...Parameter),
        /// each from the animation state's value of that name; the speeds smoothed.
        /// </summary>
        public static FloatParameter[] DefaultFloatParameters() => new[]
        {
            new FloatParameter("Speed", DefaultSpeedParameter, smoothed: true),
            new FloatParameter("Vertical Speed", DefaultVerticalSpeedParameter),
            new FloatParameter("Wall Side", DefaultWallSideParameter),
            new FloatParameter("Grab Speed", DefaultGrabSpeedParameter, smoothed: true),
            new FloatParameter("Climb Right", DefaultClimbRightParameter, smoothed: true),
            new FloatParameter("Climb Up", DefaultClimbUpParameter, smoothed: true),
            new FloatParameter("Climb Step Progress", DefaultClimbStepProgressParameter),
            new FloatParameter("Roll Progress", DefaultRollProgressParameter),
            new FloatParameter("Climb Progress", DefaultClimbProgressParameter),
            new FloatParameter("Vault Progress", DefaultVaultProgressParameter),
        };

        /// <summary>
        /// A new translator's playback parameters: the mannequin's Animator Controller's Locomotion and Crouch (by Speed),
        /// and Wall Climb (by Climb Right and Climb Up, each way's loop made for its own speed).
        /// </summary>
        public static PlaybackParameter[] DefaultPlaybackParameters() => new[]
        {
            new PlaybackParameter(DefaultLocomotionPlaybackParameter, new PlaybackAxis(DefaultSpeedParameter, DefaultLocomotionTopSpeedParameter)),
            new PlaybackParameter(DefaultCrouchPlaybackParameter, new PlaybackAxis(DefaultSpeedParameter, DefaultCrouchTopSpeedParameter)),
            new PlaybackParameter(DefaultClimbPlaybackParameter,
                new PlaybackAxis(DefaultClimbRightParameter, DefaultClimbRightTopSpeedParameter, DefaultClimbLeftTopSpeedParameter),
                new PlaybackAxis(DefaultClimbUpParameter, DefaultClimbUpTopSpeedParameter, DefaultClimbDownTopSpeedParameter)),
        };

        /// <summary>The model's scale in the animation state (unscaled if it gives none).</summary>
        private static Vector3 ModelScale(in CharacterAnimationState state) => state.scale == Vector3.zero ? Vector3.one : state.scale;

        /// <summary>
        /// The animation state's own bools a bool parameter can show, by name (its source): each worked out from the
        /// movement as the animation should show it (grounded keeps a grace period over small step-offs, crouching counts a
        /// roll that ends crouched, ...: see MMotionAnimator.BuildState). A character's state as the movement has it is
        /// a source too, with no row here (StatePrefix).
        /// </summary>
        public static readonly (string name, StateFlag value)[] AnimationBools =
        {
            ("Grounded", (in CharacterAnimationState s) => s.grounded),
            ("Jumping", (in CharacterAnimationState s) => s.jumping),
            ("Dashing", (in CharacterAnimationState s) => s.dashing),
            ("Moving", (in CharacterAnimationState s) => s.moving),
            ("Crouching", (in CharacterAnimationState s) => s.crouching),
            ("Sliding", (in CharacterAnimationState s) => s.sliding),
            ("Rolling", (in CharacterAnimationState s) => s.rolling),
            ("Wall Running", (in CharacterAnimationState s) => s.wallRunning),
            ("Wall Sliding", (in CharacterAnimationState s) => s.wallSliding),
            ("Wall Climbing", (in CharacterAnimationState s) => s.wallClimbing),
            ("Climb Entering", (in CharacterAnimationState s) => s.climbEntering),
            ("On Ladder", (in CharacterAnimationState s) => s.onLadder),
            ("Climb Exiting", (in CharacterAnimationState s) => s.climbExiting),
            ("Hanging", (in CharacterAnimationState s) => s.hanging),
            ("Climbing", (in CharacterAnimationState s) => s.climbing),
            ("Vaulting", (in CharacterAnimationState s) => s.vaulting),
            ("Grabbing", (in CharacterAnimationState s) => s.grabbing),
            ("Rope Swinging", (in CharacterAnimationState s) => s.swinging),
            ("Ziplining", (in CharacterAnimationState s) => s.ziplining),
            ("Swimming", (in CharacterAnimationState s) => s.swimming),
            ("Diving", (in CharacterAnimationState s) => s.diving),
        };

        /// <summary>
        /// A new translator's bool parameters: the mannequin's Animator Controller's (each named its Default...Parameter),
        /// each from the animation state's bool of that name.
        /// </summary>
        public static BoolParameter[] DefaultBoolParameters() => new[]
        {
            new BoolParameter("Grounded", DefaultGroundedParameter),
            new BoolParameter("Dashing", DefaultDashingParameter),
            new BoolParameter("Crouching", DefaultCrouchingParameter),
            new BoolParameter("Sliding", DefaultSlidingParameter),
            new BoolParameter("Rolling", DefaultRollingParameter),
            new BoolParameter("Wall Running", DefaultWallRunningParameter),
            new BoolParameter("Wall Sliding", DefaultWallSlidingParameter),
            new BoolParameter("Wall Climbing", DefaultWallClimbingParameter),
            new BoolParameter("On Ladder", DefaultOnLadderParameter),
            new BoolParameter("Climb Entering", DefaultClimbEnteringParameter),
            new BoolParameter("Climb Exiting", DefaultClimbExitingParameter),
            new BoolParameter("Hanging", DefaultHangingParameter),
            new BoolParameter("Climbing", DefaultClimbingParameter),
            new BoolParameter("Vaulting", DefaultVaultingParameter),
            new BoolParameter("Grabbing", DefaultGrabbingParameter),
            new BoolParameter("Rope Swinging", DefaultRopeSwingingParameter),
            new BoolParameter("Ziplining", DefaultZipliningParameter),
            new BoolParameter("Swimming", DefaultSwimmingParameter),
            new BoolParameter("Diving", DefaultDivingParameter),
        };
    }
}
