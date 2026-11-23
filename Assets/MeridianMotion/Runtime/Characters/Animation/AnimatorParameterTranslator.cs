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
    public partial class AnimatorParameterTranslator : AnimationTranslator
    {
        /// <summary>The translator's float parameters (Float Parameters, in its inspector), for code setting them up.</summary>
        public FloatParameter[] FloatParameters { get => floatParameters; set => floatParameters = value ?? Array.Empty<FloatParameter>(); }

        /// <summary>The translator's playback parameters (Playback Parameters, in its inspector).</summary>
        public PlaybackParameter[] PlaybackParameters { get => playbackParameters; set => playbackParameters = value ?? Array.Empty<PlaybackParameter>(); }

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
    }
}
