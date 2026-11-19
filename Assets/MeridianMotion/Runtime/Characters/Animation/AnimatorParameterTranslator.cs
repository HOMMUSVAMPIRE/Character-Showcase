using System;
using UnityEngine;

namespace MeridianMotion.Characters
{
    /// <summary>
    /// Drives an Animator Controller from the character's animation state: each value goes to the Animator parameter
    /// named for it, and movement events can fire triggers. Parameters left blank, or that the controller doesn't have,
    /// are skipped, so one translator works with controllers that only use some of them.
    /// </summary>
    [CreateAssetMenu(fileName = "Animator Parameter Translator", menuName = "Meridian Motion/Animation/Animator Parameter Translator")]
    public class AnimatorParameterTranslator : AnimationTranslator
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
            // Shown in the inspector as a choice of the events by name, by EventTriggerDrawer. Saved as a long (and before
            // that an int) while MovementEvents was an enum, under the same name: the translator assets were written again
            // as a MovementEvents when it became one.
            [Tooltip("The movement events that fire the trigger")]
            [SerializeField] private MovementEvents events;
            [Tooltip("The name of the Animator trigger parameter to fire")]
            public string trigger;

            /// <summary>The movement events that fire the trigger.</summary>
            public MovementEvents Events { get => events; set => events = value; }

            /// <summary>A trigger parameter, by name, fired by any of the given events.</summary>
            public EventTrigger(MovementEvents events, string trigger)
            {
                this.events = events;
                this.trigger = trigger;
            }
        }

        /// <summary>
        /// A bool Animator parameter, and what it shows: one of the animation state's own bools (AnimationBools, by name),
        /// or one of the character's states (StatePrefix and a CharacterState flag's name, as SourceOf gives it). Kept
        /// as text, so a state's bit can change without it changing what it names.
        /// </summary>
        [Serializable]
        public struct BoolParameter
        {
            [Tooltip("What the parameter shows: a value of the animation state, or one of the character's states (State: ...)")]
            public string source;
            [Tooltip("The name of the Animator bool parameter it sets")]
            public string parameter;

            /// <summary>A bool parameter, by name, showing the given source (see AnimatorParameterTranslator.SourceOf).</summary>
            public BoolParameter(string source, string parameter)
            {
                this.source = source;
                this.parameter = parameter;
            }
        }

        /// <summary>
        /// A float Animator parameter, and what it shows: one of the animation state's own values (AnimationFloats, by
        /// name), or a value an ability publishes (AbilityPrefix and its name: see AbilityValues). A source with nothing to
        /// show this frame (NaN: Wall Side out of a wall run, an ability value not yet set) leaves the parameter as it is.
        /// </summary>
        [Serializable]
        public struct FloatParameter
        {
            [Tooltip("What the parameter shows: a value of the animation state, or a value an ability publishes (Ability: ...)")]
            public string source;
            [Tooltip("The name of the Animator float parameter it sets")]
            public string parameter;
            [Tooltip("Whether it's eased toward its value over Speed Damp Time, so changes blend rather than snap")]
            public bool smoothed;

            /// <summary>A float parameter, by name, showing the given source.</summary>
            public FloatParameter(string source, string parameter, bool smoothed = false)
            {
                this.source = source;
                this.parameter = parameter;
                this.smoothed = smoothed;
            }
        }

        /// <summary>One way a playback parameter measures how fast the character goes: a speed and the speeds its loops are made for.</summary>
        [Serializable]
        public struct PlaybackAxis
        {
            [Tooltip("The float parameter giving the speed (m/s) this way, read as the Animator shows it (smoothed)")]
            public string speedParameter;
            [Tooltip("Read, not set: a parameter whose default value is the speed (m/s) the loop for a positive speed is made for, written there by whatever built the Animator Controller")]
            public string topSpeedParameter;
            [Tooltip("As Top Speed Parameter, for a negative speed (climbing down, or left); empty for the same")]
            public string negativeTopSpeedParameter;

            public PlaybackAxis(string speedParameter, string topSpeedParameter, string negativeTopSpeedParameter = "")
            {
                this.speedParameter = speedParameter;
                this.topSpeedParameter = topSpeedParameter;
                this.negativeTopSpeedParameter = negativeTopSpeedParameter;
            }
        }

        /// <summary>
        /// A playback parameter: set to how much faster than its loops are made for the character goes (at least 1), for a
        /// speed-driven state's speed multiplier, so past the end of its blend tree its fastest loop plays faster, its feet
        /// keeping up with the ground, however fast the character goes. With more than one axis (climbing across and up),
        /// whichever it's furthest past.
        /// </summary>
        [Serializable]
        public struct PlaybackParameter
        {
            [Tooltip("The name of the Animator float parameter set, a state's speed multiplier")]
            public string parameter;
            [Tooltip("The speeds it's worked out from, and the speeds their loops are made for")]
            public PlaybackAxis[] axes;

            public PlaybackParameter(string parameter, params PlaybackAxis[] axes)
            {
                this.parameter = parameter;
                this.axes = axes;
            }
        }

        /// <summary>What a float parameter's source starts with when it's a value an ability publishes (Ability: Glide Tilt).</summary>
        public const string AbilityPrefix = "Ability: ";

        /// <summary>The source of a float parameter showing the ability value of the given name.</summary>
        public static string AbilitySourceOf(string valueName) => AbilityPrefix + valueName;

        /// <summary>The translator's float parameters (Float Parameters, in its inspector), for code setting them up.</summary>
        public FloatParameter[] FloatParameters { get => floatParameters; set => floatParameters = value ?? Array.Empty<FloatParameter>(); }

        /// <summary>The translator's playback parameters (Playback Parameters, in its inspector).</summary>
        public PlaybackParameter[] PlaybackParameters { get => playbackParameters; set => playbackParameters = value ?? Array.Empty<PlaybackParameter>(); }

        /// <summary>What a bool parameter's source starts with when it's one of the character's states (State: WallRunning).</summary>
        public const string StatePrefix = "State: ";

        /// <summary>The source of a bool parameter showing the given state of the character (one flag).</summary>
        public static string SourceOf(CharacterState state) => StatePrefix + state;

        /// <summary>The translator's bool parameters (Bool Parameters, in its inspector), for code setting them up.</summary>
        public BoolParameter[] BoolParameters { get => boolParameters; set => boolParameters = value ?? Array.Empty<BoolParameter>(); }

        [Header("Float parameters")]
        [Tooltip("The float parameters set each frame, each from what it shows: a value of the animation state (Speed, " +
                 "Vault Progress, ...: worked out from the movement as the animation should show it), or a value an ability " +
                 "publishes (Ability: ..., with no code). Parameters the controller doesn't have are skipped")]
        [SerializeField] private FloatParameter[] floatParameters = DefaultFloatParameters();
        [Tooltip("Seconds over which the smoothed float parameters are eased, so starting and stopping blend between clips")]
        [SerializeField] private float speedDampTime = 0.1f;

        [Header("Playback past the fastest loop")]
        [Tooltip("Parameters set to how much faster than their loops are made for the character goes (at least 1), for " +
                 "speed-driven states' speed multipliers: past the end of a blend tree, its fastest loop plays faster, its " +
                 "feet keeping up with the ground, however fast the character goes")]
        [SerializeField] private PlaybackParameter[] playbackParameters = DefaultPlaybackParameters();

        [Header("Bool parameters")]
        [Tooltip("The bool parameters set each frame, each from what it shows: a value of the animation state (Grounded, " +
                 "Crouching, ...: worked out from the movement as the animation should show it), or one of the character's " +
                 "states as the movement has it (State: ..., for an ability's own state, with no code). Parameters the " +
                 "controller doesn't have are skipped")]
        [SerializeField] private BoolParameter[] boolParameters = DefaultBoolParameters();

        [Header("Triggers")]
        [Tooltip("Animator triggers fired by movement events, such as a landing or jump animation")]
        [SerializeField] private EventTrigger[] eventTriggers = Array.Empty<EventTrigger>();

        /// <summary>Reads a flag of the animation state (by reference: the state is large, and read 20-odd times a frame).</summary>
        public delegate bool StateFlag(in CharacterAnimationState state);


        /// <summary>Works out a float parameter's value from the animation state (by reference, as StateFlag).</summary>
        public delegate float StateValue(in CharacterAnimationState state);

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

        /// <summary>
        /// Where a float parameter's source is found: the index of one of AnimationFloats, or an ability value (index -1),
        /// or neither (false: an unknown source, skipped).
        /// </summary>
        public static bool TryResolveFloat(string source, out int animationFloat, out AbilityValueId abilityValue)
        {
            animationFloat = -1;
            abilityValue = default;
            if (string.IsNullOrEmpty(source)) { return false; }
            if (source.StartsWith(AbilityPrefix, StringComparison.Ordinal))
            {
                string name = source.Substring(AbilityPrefix.Length);
                abilityValue = new AbilityValueId(name);
                return name.Length > 0;
            }
            for (int i = 0; i < AnimationFloats.Length; i++)
            {
                if (AnimationFloats[i].name == source) { animationFloat = i; return true; }
            }
            return false;
        }

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

        /// <summary>
        /// Where a bool parameter's source is found: the index of one of AnimationBools, or a CharacterState flag (index
        /// -1), or neither (false: an unknown source, skipped).
        /// </summary>
        public static bool TryResolve(string source, out int animationBool, out CharacterState state)
        {
            animationBool = -1;
            state = CharacterState.None;
            if (string.IsNullOrEmpty(source)) { return false; }
            if (source.StartsWith(StatePrefix, StringComparison.Ordinal))
            {
                return CharacterState.TryParse(source.Substring(StatePrefix.Length), out state) && state != CharacterState.None;
            }
            for (int i = 0; i < AnimationBools.Length; i++)
            {
                if (AnimationBools[i].name == source) { animationBool = i; return true; }
            }
            return false;
        }

        public override ICharacterAnimationDriver CreateDriver(GameObject model, Animator animator) =>
            animator != null && animator.runtimeAnimatorController != null ? new Driver(this, animator) : null;

        private sealed class Driver : ICharacterAnimationDriver
        {
            // The float parameters, resolved: each one's hash, its source (an AnimationFloats index, else an ability value)
            // and whether it's smoothed. Those the controller doesn't have, or whose source is unknown, are left out.
            private readonly (int hash, int animationFloat, AbilityValueId abilityValue, bool smoothed)[] floats;
            // The bool parameters, resolved: each one's hash, and its source (an AnimationBools index, else a state's flag).
            private readonly (int hash, int animationBool, CharacterState state)[] bools;
            // The playback parameters, resolved: each one's hash and its axes (each the hash of the speed it reads back, and
            // the speeds, m/s, its loops are made for each way: the top speed parameters' defaults, or 0 for none).
            private readonly (int hash, (int speed, float top, float negativeTop)[] axes)[] playbacks;
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
                void Unknown(string kind, string parameter, string source) =>
                    Debug.LogWarning($"[{nameof(AnimatorParameterTranslator)}] '{settings.name}': the {kind} parameter '{parameter}' shows '{source}', " +
                                     $"which is neither a value of the animation state nor {(kind == "bool" ? "one of the character's states" : "an ability value")}, " +
                                     "so it isn't set.", settings);

                var resolvedFloats = new System.Collections.Generic.List<(int, int, AbilityValueId, bool)>();
                foreach (FloatParameter entry in settings.floatParameters)
                {
                    int hash = Find(entry.parameter, AnimatorControllerParameterType.Float);
                    if (hash == 0) { continue; }
                    if (!TryResolveFloat(entry.source, out int animationFloat, out AbilityValueId abilityValue)) { Unknown("float", entry.parameter, entry.source); continue; }
                    resolvedFloats.Add((hash, animationFloat, abilityValue, entry.smoothed));
                }
                floats = resolvedFloats.ToArray();

                var resolvedBools = new System.Collections.Generic.List<(int, int, CharacterState)>();
                foreach (BoolParameter entry in settings.boolParameters)
                {
                    int hash = Find(entry.parameter, AnimatorControllerParameterType.Bool);
                    if (hash == 0) { continue; }
                    if (!TryResolve(entry.source, out int animationBool, out CharacterState state)) { Unknown("bool", entry.parameter, entry.source); continue; }
                    resolvedBools.Add((hash, animationBool, state));
                }
                bools = resolvedBools.ToArray();

                var resolvedPlaybacks = new System.Collections.Generic.List<(int, (int, float, float)[])>();
                foreach (PlaybackParameter entry in settings.playbackParameters)
                {
                    int hash = Find(entry.parameter, AnimatorControllerParameterType.Float);
                    if (hash == 0 || entry.axes == null) { continue; }
                    var axes = new (int, float, float)[entry.axes.Length];
                    for (int i = 0; i < axes.Length; i++)
                    {
                        PlaybackAxis axis = entry.axes[i];
                        float top = DefaultOf(axis.topSpeedParameter);
                        axes[i] = (Find(axis.speedParameter, AnimatorControllerParameterType.Float), top,
                                   string.IsNullOrEmpty(axis.negativeTopSpeedParameter) ? top : DefaultOf(axis.negativeTopSpeedParameter));
                    }
                    resolvedPlaybacks.Add((hash, axes));
                }
                playbacks = resolvedPlaybacks.ToArray();

                triggers = new (MovementEvents, int)[settings.eventTriggers.Length];
                for (int i = 0; i < triggers.Length; i++)
                {
                    EventTrigger binding = settings.eventTriggers[i];
                    triggers[i] = (binding.Events, Find(binding.trigger, AnimatorControllerParameterType.Trigger));
                }
            }

            public void UpdateAnimation(in CharacterAnimationState state, float deltaTime)
            {
                for (int i = 0; i < floats.Length; i++)
                {
                    (int hash, int animationFloat, AbilityValueId abilityValue, bool smoothed) = floats[i];
                    float value = animationFloat >= 0 ? AnimationFloats[animationFloat].value(state)
                        : state.abilityValues != null && state.abilityValues.TryGet(abilityValue, out float published) ? published : float.NaN;
                    if (float.IsNaN(value)) { continue; } // Nothing to show: left as it is.
                    if (smoothed) { animator.SetFloat(hash, value, speedDampTime, deltaTime); }
                    else { animator.SetFloat(hash, value); }
                }
                // After the speeds they're worked out from (as the Animator shows them, smoothed).
                SetPlayback();
                for (int i = 0; i < bools.Length; i++)
                {
                    (int hash, int animationBool, CharacterState flags) = bools[i];
                    animator.SetBool(hash, animationBool >= 0 ? AnimationBools[animationBool].value(state) : state.state.HasAny(flags));
                }
            }

            /// <summary>
            /// How much faster than their fastest loops the speed-driven states play: a blend tree ends at the speed its
            /// fastest loop is made for, and past it the loop plays faster by however much faster the character moves, so
            /// its feet keep up with the ground at any speed (rather than up to a top speed fixed when the controller was
            /// built, which a retuned character could outrun). From the speeds as the animator shows them (smoothed); with
            /// several axes (climbing across and up), whichever it's furthest past.
            /// </summary>
            private void SetPlayback()
            {
                for (int i = 0; i < playbacks.Length; i++)
                {
                    (int hash, (int speed, float top, float negativeTop)[] axes) = playbacks[i];
                    float playback = 1f;
                    for (int a = 0; a < axes.Length; a++)
                    {
                        float speed = axes[a].speed != 0 ? animator.GetFloat(axes[a].speed) : 0f;
                        playback = Mathf.Max(playback, Past(Mathf.Abs(speed), speed >= 0f ? axes[a].top : axes[a].negativeTop));
                    }
                    animator.SetFloat(hash, playback);
                }
            }

            /// <summary>How many times faster than a loop's own speed (top) the given speed is, never below 1 (and 1 with no top).</summary>
            private static float Past(float value, float top) => top > 0f ? Mathf.Max(1f, value / top) : 1f;

            public void HandleMovementEvent(MovementEvents movementEvent, in CharacterAnimationState state)
            {
                foreach (var (events, trigger) in triggers)
                {
                    if (trigger != 0 && events.HasAny(movementEvent)) { animator.SetTrigger(trigger); }
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
