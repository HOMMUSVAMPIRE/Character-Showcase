using System;
using UnityEngine;

namespace MeridianMotion.Characters
{
    // Setting an Animator's parameters each frame from the animation state.
    public partial class AnimatorParameterTranslator
    {
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
    }
}
