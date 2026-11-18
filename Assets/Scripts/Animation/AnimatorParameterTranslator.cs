using System;
using UnityEngine;

namespace Character
{
    /// <summary>
    /// Drives an Animator Controller from the character's animation state: each value goes to the Animator parameter
    /// named for it, and movement events can fire triggers. Parameters left blank, or that the controller doesn't have,
    /// are skipped, so one translator works with controllers that only use some of them.
    /// </summary>
    [CreateAssetMenu(fileName = "AnimatorParameterTranslator", menuName = "Character Showcase/Animation/Animator Parameter Translator")]
    public class AnimatorParameterTranslator : CharacterAnimationTranslator
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

        /// <summary>A trigger to fire when any of the given movement events happens.</summary>
        [Serializable]
        public struct EventTrigger
        {
            // The events, as a long: MovementEvents is 64 bits wide, and Unity doesn't save enum fields that wide (it drops
            // them, silently). Shown in the inspector as the events, by EventTriggerDrawer. Saved as an int before it
            // outgrew 32 bits, under the same name, which loads as is.
            [Tooltip("The movement events that fire the trigger")]
            [SerializeField] private long events;
            [Tooltip("The name of the Animator trigger parameter to fire")]
            public string trigger;

            /// <summary>The movement events that fire the trigger.</summary>
            public MovementEvents Events { get => (MovementEvents)events; set => events = (long)value; }

            /// <summary>A trigger parameter, by name, fired by any of the given events.</summary>
            public EventTrigger(MovementEvents events, string trigger)
            {
                this.events = (long)events;
                this.trigger = trigger;
            }
        }

        [Header("Float parameters")]
        [Tooltip("Horizontal speed in m/s")]
        [SerializeField] private string speedParameter = DefaultSpeedParameter;
        [Tooltip("Seconds over which the speed is smoothed, so starting and stopping ease between clips")]
        [SerializeField] private float speedDampTime = 0.1f;
        [Tooltip("Vertical speed in m/s (positive = rising)")]
        [SerializeField] private string verticalSpeedParameter = DefaultVerticalSpeedParameter;
        [Tooltip("Speed in m/s in the direction the character faces (for strafing blend trees)")]
        [SerializeField] private string forwardSpeedParameter = "";
        [Tooltip("Speed in m/s to the character's right (for strafing blend trees)")]
        [SerializeField] private string sidewaysSpeedParameter = "";
        [Tooltip("Turn rate in degrees per second (for leaning or turn-on-the-spot animations)")]
        [SerializeField] private string turnSpeedParameter = "";
        [Tooltip("Which side the wall is on during a wall run: 1 = right, -1 = left. Kept after a wall run ends, so an animation leaving it doesn't swing through the middle")]
        [SerializeField] private string wallSideParameter = DefaultWallSideParameter;
        [Tooltip("While holding an object: speed in m/s toward it (positive pushing, negative pulling); 0 otherwise")]
        [SerializeField] private string grabSpeedParameter = DefaultGrabSpeedParameter;
        [Tooltip("While climbing a climbable surface: speed in m/s to the right across it, as the model would climb at its authored size; 0 otherwise")]
        [SerializeField] private string climbRightParameter = DefaultClimbRightParameter;
        [Tooltip("While climbing a climbable surface: speed in m/s up it (negative climbing down), as the model would climb at its authored size; 0 otherwise")]
        [SerializeField] private string climbUpParameter = DefaultClimbUpParameter;
        [Tooltip("How far through stepping onto a climbable surface from the ground, or off its bottom onto the ground (0 to 1), for animations timed to it")]
        [SerializeField] private string climbStepProgressParameter = DefaultClimbStepProgressParameter;
        [Tooltip("How far through a roll (0 to 1), for a roll animation timed to it")]
        [SerializeField] private string rollProgressParameter = DefaultRollProgressParameter;
        [Tooltip("How far through a climb onto a ledge (0 to 1), for a climbing animation timed to it")]
        [SerializeField] private string climbProgressParameter = DefaultClimbProgressParameter;
        [Tooltip("About how far through a vault over something low (0 to 1), for a vaulting animation timed to it")]
        [SerializeField] private string vaultProgressParameter = DefaultVaultProgressParameter;

        [Header("Playback past the fastest loop")]
        [Tooltip("Set to how much faster than its fastest loop is made for the character moves (at least 1), for the " +
                 "Locomotion state's speed multiplier: so past the end of its blend tree, the fastest loop plays faster, its feet " +
                 "keeping up with the ground, however fast the character goes")]
        [SerializeField] private string locomotionPlaybackParameter = DefaultLocomotionPlaybackParameter;
        [Tooltip("Read, not set: a parameter whose default value is the speed (m/s) the Locomotion state's fastest loop is made " +
                 "for, written there by whatever built the Animator Controller (Generate Mannequin does)")]
        [SerializeField] private string locomotionTopSpeedParameter = DefaultLocomotionTopSpeedParameter;
        [Tooltip("As Locomotion Playback, for the crouch walk (from the same speed)")]
        [SerializeField] private string crouchPlaybackParameter = DefaultCrouchPlaybackParameter;
        [Tooltip("Read, not set: a parameter whose default value is the speed (m/s) the crouch walk is made for")]
        [SerializeField] private string crouchTopSpeedParameter = DefaultCrouchTopSpeedParameter;
        [Tooltip("As Locomotion Playback, for climbing a climbable surface (from Climb Right and Climb Up): past the speed a " +
                 "climbing loop is made for, the way the character climbs, it plays faster")]
        [SerializeField] private string climbPlaybackParameter = DefaultClimbPlaybackParameter;
        [Tooltip("Read, not set: a parameter whose default value is the speed (m/s) the climbing-up loop is made for")]
        [SerializeField] private string climbUpTopSpeedParameter = DefaultClimbUpTopSpeedParameter;
        [Tooltip("Read, not set: a parameter whose default value is the speed (m/s) the climbing-down loop is made for")]
        [SerializeField] private string climbDownTopSpeedParameter = DefaultClimbDownTopSpeedParameter;
        [Tooltip("Read, not set: a parameter whose default value is the speed (m/s) the climbing-left loop is made for")]
        [SerializeField] private string climbLeftTopSpeedParameter = DefaultClimbLeftTopSpeedParameter;
        [Tooltip("Read, not set: a parameter whose default value is the speed (m/s) the climbing-right loop is made for")]
        [SerializeField] private string climbRightTopSpeedParameter = DefaultClimbRightTopSpeedParameter;

        [Header("Bool parameters")]
        [Tooltip("Whether the character should be animated as on the ground")]
        [SerializeField] private string groundedParameter = DefaultGroundedParameter;
        [Tooltip("Rising from a jump (until it starts falling or is cut short)")]
        [SerializeField] private string jumpingParameter = "";
        [Tooltip("Dashing")]
        [SerializeField] private string dashingParameter = DefaultDashingParameter;
        [Tooltip("The character's own movement ran this step (walking on the ground, even standing still, or steering in the air) rather than an ability taking it over")]
        [SerializeField] private string movingParameter = "";
        [Tooltip("Crouched (not while sliding or rolling)")]
        [SerializeField] private string crouchingParameter = DefaultCrouchingParameter;
        [Tooltip("Sliding along the ground")]
        [SerializeField] private string slidingParameter = DefaultSlidingParameter;
        [Tooltip("Rolling")]
        [SerializeField] private string rollingParameter = DefaultRollingParameter;
        [Tooltip("Running along a wall")]
        [SerializeField] private string wallRunningParameter = DefaultWallRunningParameter;
        [Tooltip("Sliding down a wall")]
        [SerializeField] private string wallSlidingParameter = DefaultWallSlidingParameter;
        [Tooltip("Climbing a climbable surface (the Climb Right and Climb Up parameters give the climb's speed across and " +
                 "up it)")]
        [SerializeField] private string wallClimbingParameter = DefaultWallClimbingParameter;
        [Tooltip("Climbing a ladder (WallClimbing is set too)")]
        [SerializeField] private string onLadderParameter = DefaultOnLadderParameter;
        [Tooltip("Stepping onto a climbable surface from standing on the ground")]
        [SerializeField] private string climbEnteringParameter = DefaultClimbEnteringParameter;
        [Tooltip("Stepping off the bottom of a climbable surface onto the ground")]
        [SerializeField] private string climbExitingParameter = DefaultClimbExitingParameter;
        [Tooltip("Hanging from a ledge")]
        [SerializeField] private string hangingParameter = DefaultHangingParameter;
        [Tooltip("Climbing up from a ledge onto it")]
        [SerializeField] private string climbingParameter = DefaultClimbingParameter;
        [Tooltip("Vaulting over something low")]
        [SerializeField] private string vaultingParameter = DefaultVaultingParameter;
        [Tooltip("Holding an object to push or pull")]
        [SerializeField] private string grabbingParameter = DefaultGrabbingParameter;
        [Tooltip("Swinging on a rope")]
        [SerializeField] private string ropeSwingingParameter = DefaultRopeSwingingParameter;
        [Tooltip("Riding a zipline")]
        [SerializeField] private string zipliningParameter = DefaultZipliningParameter;
        [Tooltip("Swimming at the surface of deep water")]
        [SerializeField] private string swimmingParameter = DefaultSwimmingParameter;
        [Tooltip("Swimming under the surface (Swimming is set too)")]
        [SerializeField] private string divingParameter = DefaultDivingParameter;

        [Header("Triggers")]
        [Tooltip("Animator triggers fired by movement events, such as a landing or jump animation")]
        [SerializeField] private EventTrigger[] eventTriggers = Array.Empty<EventTrigger>();

        /// <summary>Reads a flag of the animation state (by reference: the state is large, and read 20-odd times a frame).</summary>
        private delegate bool StateFlag(in CharacterAnimationState state);

        /// <summary>
        /// The bool parameters: each one's name setting (above, under Bool parameters) and the flag of the animation
        /// state it shows, set as they are each frame. A new one is a row here and its name setting; a float parameter,
        /// whose value is worked out (scaled, smoothed), is set by hand in Driver.UpdateAnimation.
        /// </summary>
        private static readonly (System.Func<AnimatorParameterTranslator, string> name, StateFlag value)[] BoolParameters =
        {
            (t => t.groundedParameter, (in CharacterAnimationState s) => s.grounded),
            (t => t.jumpingParameter, (in CharacterAnimationState s) => s.jumping),
            (t => t.dashingParameter, (in CharacterAnimationState s) => s.dashing),
            (t => t.movingParameter, (in CharacterAnimationState s) => s.moving),
            (t => t.crouchingParameter, (in CharacterAnimationState s) => s.crouching),
            (t => t.slidingParameter, (in CharacterAnimationState s) => s.sliding),
            (t => t.rollingParameter, (in CharacterAnimationState s) => s.rolling),
            (t => t.wallRunningParameter, (in CharacterAnimationState s) => s.wallRunning),
            (t => t.wallSlidingParameter, (in CharacterAnimationState s) => s.wallSliding),
            (t => t.wallClimbingParameter, (in CharacterAnimationState s) => s.wallClimbing),
            (t => t.climbEnteringParameter, (in CharacterAnimationState s) => s.climbEntering),
            (t => t.onLadderParameter, (in CharacterAnimationState s) => s.onLadder),
            (t => t.climbExitingParameter, (in CharacterAnimationState s) => s.climbExiting),
            (t => t.hangingParameter, (in CharacterAnimationState s) => s.hanging),
            (t => t.climbingParameter, (in CharacterAnimationState s) => s.climbing),
            (t => t.vaultingParameter, (in CharacterAnimationState s) => s.vaulting),
            (t => t.grabbingParameter, (in CharacterAnimationState s) => s.grabbing),
            (t => t.ropeSwingingParameter, (in CharacterAnimationState s) => s.swinging),
            (t => t.zipliningParameter, (in CharacterAnimationState s) => s.ziplining),
            (t => t.swimmingParameter, (in CharacterAnimationState s) => s.swimming),
            (t => t.divingParameter, (in CharacterAnimationState s) => s.diving),
        };

        public override ICharacterAnimationDriver CreateDriver(GameObject model, Animator animator) =>
            animator != null && animator.runtimeAnimatorController != null ? new Driver(this, animator) : null;

        private sealed class Driver : ICharacterAnimationDriver
        {
            // Parameter hashes, or 0 for parameters to skip
            private readonly int speed, verticalSpeed, forwardSpeed, sidewaysSpeed, turnSpeed, wallSide, grabSpeed, rollProgress, climbProgress, vaultProgress, climbRight, climbUp, climbStepProgress;
            // The bool parameters' hashes, in BoolParameters' order (0 to skip one).
            private readonly int[] boolHashes = new int[BoolParameters.Length];
            private readonly int locomotionPlayback, crouchPlayback, climbPlayback;
            // The speeds (m/s) the controller's fastest loops are made for (its parameters' defaults), or 0 if it has none.
            private readonly float locomotionTop, crouchTop, climbUpTop, climbDownTop, climbLeftTop, climbRightTop;
            private readonly (MovementEvents events, int trigger)[] triggers;
            private readonly float speedDampTime;
            private readonly Animator animator;

            public Driver(AnimatorParameterTranslator settings, Animator animator)
            {
                this.animator = animator;
                speedDampTime = settings.speedDampTime;
                AnimatorControllerParameter[] parameters = animator.parameters;

                int Find(string name, AnimatorControllerParameterType type)
                {
                    if (string.IsNullOrEmpty(name)) { return 0; }
                    foreach (AnimatorControllerParameter parameter in parameters)
                    {
                        if (parameter.name == name && parameter.type == type) { return parameter.nameHash; }
                    }
                    return 0;
                }

                speed = Find(settings.speedParameter, AnimatorControllerParameterType.Float);
                verticalSpeed = Find(settings.verticalSpeedParameter, AnimatorControllerParameterType.Float);
                forwardSpeed = Find(settings.forwardSpeedParameter, AnimatorControllerParameterType.Float);
                sidewaysSpeed = Find(settings.sidewaysSpeedParameter, AnimatorControllerParameterType.Float);
                turnSpeed = Find(settings.turnSpeedParameter, AnimatorControllerParameterType.Float);
                wallSide = Find(settings.wallSideParameter, AnimatorControllerParameterType.Float);
                grabSpeed = Find(settings.grabSpeedParameter, AnimatorControllerParameterType.Float);
                rollProgress = Find(settings.rollProgressParameter, AnimatorControllerParameterType.Float);
                climbProgress = Find(settings.climbProgressParameter, AnimatorControllerParameterType.Float);
                vaultProgress = Find(settings.vaultProgressParameter, AnimatorControllerParameterType.Float);
                climbRight = Find(settings.climbRightParameter, AnimatorControllerParameterType.Float);
                climbUp = Find(settings.climbUpParameter, AnimatorControllerParameterType.Float);
                climbStepProgress = Find(settings.climbStepProgressParameter, AnimatorControllerParameterType.Float);

                for (int i = 0; i < boolHashes.Length; i++) { boolHashes[i] = Find(BoolParameters[i].name(settings), AnimatorControllerParameterType.Bool); }

                locomotionPlayback = Find(settings.locomotionPlaybackParameter, AnimatorControllerParameterType.Float);
                crouchPlayback = Find(settings.crouchPlaybackParameter, AnimatorControllerParameterType.Float);
                climbPlayback = Find(settings.climbPlaybackParameter, AnimatorControllerParameterType.Float);
                // A parameter's default value, as the controller was built with it (not what it's been set to since).
                float DefaultOf(string name)
                {
                    if (string.IsNullOrEmpty(name)) { return 0f; }
                    foreach (AnimatorControllerParameter parameter in parameters)
                    {
                        if (parameter.name == name && parameter.type == AnimatorControllerParameterType.Float) { return Mathf.Max(0f, parameter.defaultFloat); }
                    }
                    return 0f;
                }
                locomotionTop = DefaultOf(settings.locomotionTopSpeedParameter);
                crouchTop = DefaultOf(settings.crouchTopSpeedParameter);
                climbUpTop = DefaultOf(settings.climbUpTopSpeedParameter);
                climbDownTop = DefaultOf(settings.climbDownTopSpeedParameter);
                climbLeftTop = DefaultOf(settings.climbLeftTopSpeedParameter);
                climbRightTop = DefaultOf(settings.climbRightTopSpeedParameter);

                triggers = new (MovementEvents, int)[settings.eventTriggers.Length];
                for (int i = 0; i < triggers.Length; i++)
                {
                    EventTrigger binding = settings.eventTriggers[i];
                    triggers[i] = (binding.Events, Find(binding.trigger, AnimatorControllerParameterType.Trigger));
                }
            }

            public void UpdateAnimation(in CharacterAnimationState state, float deltaTime)
            {
                // Speeds are given as the authored model would move: a scaled model's strides are scaled too, so playing
                // the clips for its speed divided by its stride scale keeps its planted feet in place at any size.
                Vector3 scale = state.scale == Vector3.zero ? Vector3.one : state.scale;
                if (speed != 0) { animator.SetFloat(speed, state.horizontalSpeed / state.StrideScale, speedDampTime, deltaTime); }
                // Jump speeds scale with the character's speeds (see MovementController.SpeedScale), so this keeps the
                // rising and falling poses at the same points of a jump at any size.
                float speedScale = state.speedScale > 0f ? state.speedScale : 1f;
                if (verticalSpeed != 0) { animator.SetFloat(verticalSpeed, state.verticalSpeed / speedScale); }
                if (forwardSpeed != 0) { animator.SetFloat(forwardSpeed, state.localVelocity.z / scale.z, speedDampTime, deltaTime); }
                if (sidewaysSpeed != 0) { animator.SetFloat(sidewaysSpeed, state.localVelocity.x / scale.x, speedDampTime, deltaTime); }
                if (turnSpeed != 0) { animator.SetFloat(turnSpeed, state.turnSpeed, speedDampTime, deltaTime); }
                if (wallSide != 0 && state.wallRunning && state.wallSide != 0f) { animator.SetFloat(wallSide, state.wallSide); }
                // As the authored model would push or pull (see speed, above).
                if (grabSpeed != 0) { animator.SetFloat(grabSpeed, state.grabbing ? state.localVelocity.z / scale.z : 0f, speedDampTime, deltaTime); }
                if (rollProgress != 0) { animator.SetFloat(rollProgress, state.rollProgress); }
                if (climbProgress != 0) { animator.SetFloat(climbProgress, state.climbProgress); }
                if (vaultProgress != 0) { animator.SetFloat(vaultProgress, state.vaultProgress); }
                // Climbing, the character faces the surface, so its velocity relative to its facing is the climb: x to the
                // right, y up. As the authored model would climb (see speed, above), so its hands keep their holds.
                if (climbRight != 0) { animator.SetFloat(climbRight, state.wallClimbing ? state.localVelocity.x / scale.x : 0f, speedDampTime, deltaTime); }
                if (climbUp != 0) { animator.SetFloat(climbUp, state.wallClimbing ? state.localVelocity.y / scale.y : 0f, speedDampTime, deltaTime); }
                SetPlayback(state);
                if (climbStepProgress != 0) { animator.SetFloat(climbStepProgress, state.climbStepProgress); }
                for (int i = 0; i < boolHashes.Length; i++)
                {
                    if (boolHashes[i] != 0) { animator.SetBool(boolHashes[i], BoolParameters[i].value(state)); }
                }
            }

            /// <summary>
            /// How much faster than their fastest loops the speed-driven states play: a blend tree ends at the speed its
            /// fastest loop is made for, and past it the loop plays faster by however much faster the character moves, so
            /// its feet keep up with the ground at any speed (rather than up to a top speed fixed when the controller was
            /// built, which a retuned character could outrun). From the speeds as the animator shows them (smoothed).
            /// </summary>
            private void SetPlayback(in CharacterAnimationState state)
            {
                float shownSpeed = speed != 0 ? animator.GetFloat(speed) : state.horizontalSpeed / state.StrideScale;
                if (locomotionPlayback != 0) { animator.SetFloat(locomotionPlayback, Past(shownSpeed, locomotionTop)); }
                if (crouchPlayback != 0) { animator.SetFloat(crouchPlayback, Past(shownSpeed, crouchTop)); }
                if (climbPlayback != 0)
                {
                    // Past the loop the climb is heading toward: whichever of across and up it's further past.
                    float right = climbRight != 0 ? animator.GetFloat(climbRight) : 0f, up = climbUp != 0 ? animator.GetFloat(climbUp) : 0f;
                    float across = Past(Mathf.Abs(right), right >= 0f ? climbRightTop : climbLeftTop);
                    float along = Past(Mathf.Abs(up), up >= 0f ? climbUpTop : climbDownTop);
                    animator.SetFloat(climbPlayback, Mathf.Max(across, along));
                }
            }

            /// <summary>How many times faster than a loop's own speed (top) the given speed is, never below 1 (and 1 with no top).</summary>
            private static float Past(float value, float top) => top > 0f ? Mathf.Max(1f, value / top) : 1f;

            public void HandleMovementEvent(MovementEvents movementEvent, in CharacterAnimationState state)
            {
                foreach (var (events, trigger) in triggers)
                {
                    if (trigger != 0 && (events & movementEvent) != 0) { animator.SetTrigger(trigger); }
                }
            }

            // What the Animator was doing when its model was replaced, for the next model's driver to carry on from.
            private Handover handover;

            public void Retire() => handover = Handover.Capture(animator);

            public void ContinueFrom(ICharacterAnimationDriver previous)
            {
                if (previous is Driver { handover: not null } old) { old.handover.Restore(animator); }
            }
        }

        /// <summary>
        /// An Animator's state at a moment: its parameters, and each layer's weight, state, how far through it, and the
        /// transition it's in. Restored onto another Animator by name, so controllers that share some state and parameter
        /// names (as variants of one controller do) carry those on, and the rest start as they were made.
        /// </summary>
        private sealed class Handover
        {
            private struct Layer
            {
                public string name;
                public float weight;
                public int state;
                public float time;
                // The transition the layer was in, if any: to which state, how far through that state, and how far
                // through the transition, which lasts duration (seconds, or of the state it's leaving if not fixed).
                public bool transitioning;
                public int nextState;
                public float nextTime, nextSeconds, duration, progress;
                public bool fixedDuration;
            }

            private AnimatorControllerParameter[] parameters;
            private float[] floats;
            private int[] ints;
            private bool[] bools;
            private Layer[] layers;

            public static Handover Capture(Animator animator)
            {
                if (animator == null || !animator.isActiveAndEnabled || !animator.isInitialized
                    || animator.runtimeAnimatorController == null) { return null; }
                var handover = new Handover { parameters = animator.parameters };
                int count = handover.parameters.Length;
                handover.floats = new float[count];
                handover.ints = new int[count];
                handover.bools = new bool[count];
                for (int i = 0; i < count; i++)
                {
                    AnimatorControllerParameter parameter = handover.parameters[i];
                    switch (parameter.type)
                    {
                        case AnimatorControllerParameterType.Float: handover.floats[i] = animator.GetFloat(parameter.nameHash); break;
                        case AnimatorControllerParameterType.Int: handover.ints[i] = animator.GetInteger(parameter.nameHash); break;
                        default: handover.bools[i] = animator.GetBool(parameter.nameHash); break; // Bools and triggers
                    }
                }

                handover.layers = new Layer[animator.layerCount];
                for (int i = 0; i < handover.layers.Length; i++)
                {
                    AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(i);
                    var layer = new Layer
                    {
                        name = animator.GetLayerName(i),
                        weight = animator.GetLayerWeight(i),
                        state = current.fullPathHash,
                        time = current.normalizedTime,
                        transitioning = animator.IsInTransition(i),
                    };
                    if (layer.transitioning)
                    {
                        AnimatorStateInfo next = animator.GetNextAnimatorStateInfo(i);
                        AnimatorTransitionInfo transition = animator.GetAnimatorTransitionInfo(i);
                        layer.nextState = next.fullPathHash;
                        layer.nextTime = next.normalizedTime;
                        layer.nextSeconds = next.normalizedTime * next.length;
                        layer.duration = transition.duration;
                        layer.fixedDuration = transition.durationUnit == DurationUnit.Fixed;
                        layer.progress = transition.normalizedTime;
                    }
                    handover.layers[i] = layer;
                }
                return handover;
            }

            public void Restore(Animator animator)
            {
                if (animator == null || !animator.isActiveAndEnabled || !animator.isInitialized
                    || animator.runtimeAnimatorController == null) { return; }
                // Values first, so the states below are entered with them; triggers last (see below).
                AnimatorControllerParameter[] own = animator.parameters;
                for (int i = 0; i < parameters.Length; i++)
                {
                    AnimatorControllerParameter parameter = parameters[i];
                    if (parameter.type == AnimatorControllerParameterType.Trigger || !Has(own, parameter)) { continue; }
                    switch (parameter.type)
                    {
                        case AnimatorControllerParameterType.Float: animator.SetFloat(parameter.nameHash, floats[i]); break;
                        case AnimatorControllerParameterType.Int: animator.SetInteger(parameter.nameHash, ints[i]); break;
                        default: animator.SetBool(parameter.nameHash, bools[i]); break;
                    }
                }

                // Each layer into the state it was in (the base layer by position, as its name is often left as is),
                // and how far through. Play takes effect at the Animator's next update.
                var targets = new int[layers.Length];
                bool anyTransitions = false;
                for (int i = 0; i < layers.Length; i++)
                {
                    Layer layer = layers[i];
                    int index = i == 0 ? 0 : animator.GetLayerIndex(layer.name);
                    targets[i] = index;
                    if (index < 0 || index >= animator.layerCount) { continue; }
                    if (index > 0) { animator.SetLayerWeight(index, layer.weight); }
                    if (!animator.HasState(index, layer.state)) { targets[i] = -1; continue; }
                    animator.Play(layer.state, index, layer.time);
                    anyTransitions |= layer.transitioning && animator.HasState(index, layer.nextState);
                }

                // A layer mid-transition is put back in it: the state it was leaving has to be entered first (Unity only
                // keeps the last Play or CrossFade on a layer per update), so the Animator is updated by no time, then
                // the transition started part-way through, as it was.
                if (anyTransitions)
                {
                    animator.Update(0f);
                    for (int i = 0; i < layers.Length; i++)
                    {
                        Layer layer = layers[i];
                        int index = targets[i];
                        if (index < 0 || !layer.transitioning || !animator.HasState(index, layer.nextState)) { continue; }
                        if (layer.fixedDuration)
                        {
                            animator.CrossFadeInFixedTime(layer.nextState, layer.duration, index,
                                layer.nextSeconds, layer.progress);
                        }
                        else
                        {
                            animator.CrossFade(layer.nextState, layer.duration, index, layer.nextTime, layer.progress);
                        }
                    }
                }

                // Triggers set but not yet used, set after the update above so it doesn't use them before the new model's
                // first real update would.
                for (int i = 0; i < parameters.Length; i++)
                {
                    AnimatorControllerParameter parameter = parameters[i];
                    if (parameter.type == AnimatorControllerParameterType.Trigger && bools[i] && Has(own, parameter))
                    {
                        animator.SetTrigger(parameter.nameHash);
                    }
                }
            }

            private static bool Has(AnimatorControllerParameter[] parameters, AnimatorControllerParameter wanted)
            {
                foreach (AnimatorControllerParameter parameter in parameters)
                {
                    if (parameter.nameHash == wanted.nameHash && parameter.type == wanted.type) { return true; }
                }
                return false;
            }
        }
    }
}
