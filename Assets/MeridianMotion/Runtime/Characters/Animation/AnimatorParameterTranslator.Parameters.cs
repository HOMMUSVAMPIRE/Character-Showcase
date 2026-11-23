using System;
using UnityEngine;

namespace MeridianMotion.Characters
{
    // The kinds of parameter a translator sets, and finding what each shows by its source.
    public partial class AnimatorParameterTranslator
    {
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

        /// <summary>What a bool parameter's source starts with when it's one of the character's states (State: WallRunning).</summary>
        public const string StatePrefix = "State: ";

        /// <summary>The source of a bool parameter showing the given state of the character (one flag).</summary>
        public static string SourceOf(CharacterState state) => StatePrefix + state;

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
    }
}
