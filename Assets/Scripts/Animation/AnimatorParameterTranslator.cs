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
        public const string DefaultSpeedParameter = "Speed";
        public const string DefaultVerticalSpeedParameter = "VerticalSpeed";
        public const string DefaultGroundedParameter = "Grounded";
        public const string DefaultDashingParameter = "Dashing";

        /// <summary>A trigger to fire when any of the given movement events happens.</summary>
        [Serializable]
        public struct EventTrigger
        {
            [Tooltip("The movement events that fire the trigger")]
            public MovementEvents events;
            [Tooltip("The name of the Animator trigger parameter to fire")]
            public string trigger;
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

        [Header("Bool parameters")]
        [Tooltip("Whether the character should be animated as on the ground")]
        [SerializeField] private string groundedParameter = DefaultGroundedParameter;
        [SerializeField] private string jumpingParameter = "";
        [SerializeField] private string dashingParameter = DefaultDashingParameter;
        [SerializeField] private string movingParameter = "";

        [Header("Triggers")]
        [Tooltip("Animator triggers fired by movement events, such as a landing or jump animation")]
        [SerializeField] private EventTrigger[] eventTriggers = Array.Empty<EventTrigger>();

        public override ICharacterAnimationDriver CreateDriver(GameObject model, Animator animator) =>
            animator != null && animator.runtimeAnimatorController != null ? new Driver(this, animator) : null;

        private sealed class Driver : ICharacterAnimationDriver
        {
            // Parameter hashes, or 0 for parameters to skip
            private readonly int speed, verticalSpeed, forwardSpeed, sidewaysSpeed, turnSpeed;
            private readonly int grounded, jumping, dashing, moving;
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
                grounded = Find(settings.groundedParameter, AnimatorControllerParameterType.Bool);
                jumping = Find(settings.jumpingParameter, AnimatorControllerParameterType.Bool);
                dashing = Find(settings.dashingParameter, AnimatorControllerParameterType.Bool);
                moving = Find(settings.movingParameter, AnimatorControllerParameterType.Bool);

                triggers = new (MovementEvents, int)[settings.eventTriggers.Length];
                for (int i = 0; i < triggers.Length; i++)
                {
                    EventTrigger binding = settings.eventTriggers[i];
                    triggers[i] = (binding.events, Find(binding.trigger, AnimatorControllerParameterType.Trigger));
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
                if (grounded != 0) { animator.SetBool(grounded, state.grounded); }
                if (jumping != 0) { animator.SetBool(jumping, state.jumping); }
                if (dashing != 0) { animator.SetBool(dashing, state.dashing); }
                if (moving != 0) { animator.SetBool(moving, state.moving); }
            }

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
