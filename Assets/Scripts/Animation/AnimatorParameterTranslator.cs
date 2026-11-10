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
                if (speed != 0) { animator.SetFloat(speed, state.horizontalSpeed, speedDampTime, deltaTime); }
                if (verticalSpeed != 0) { animator.SetFloat(verticalSpeed, state.verticalSpeed); }
                if (forwardSpeed != 0) { animator.SetFloat(forwardSpeed, state.localVelocity.z, speedDampTime, deltaTime); }
                if (sidewaysSpeed != 0) { animator.SetFloat(sidewaysSpeed, state.localVelocity.x, speedDampTime, deltaTime); }
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
        }
    }
}
