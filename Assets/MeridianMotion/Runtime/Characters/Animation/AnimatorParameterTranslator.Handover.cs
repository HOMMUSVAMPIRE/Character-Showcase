using System;
using UnityEngine;

namespace MeridianMotion.Characters
{
    // Carrying an Animator's state over to the next model's, by name.
    public partial class AnimatorParameterTranslator
    {
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
