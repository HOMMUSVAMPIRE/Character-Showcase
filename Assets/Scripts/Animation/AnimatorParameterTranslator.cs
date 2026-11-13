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
        public const string DefaultCrouchingParameter = "Crouching";
        public const string DefaultSlidingParameter = "Sliding";
        public const string DefaultRollingParameter = "Rolling";
        public const string DefaultWallRunningParameter = "WallRunning";
        public const string DefaultWallSlidingParameter = "WallSliding";
        public const string DefaultWallClimbingParameter = "WallClimbing";
        public const string DefaultClimbRightParameter = "ClimbRight";
        public const string DefaultClimbUpParameter = "ClimbUp";
        public const string DefaultClimbEnteringParameter = "ClimbEntering";
        public const string DefaultOnLadderParameter = "OnLadder";
        public const string DefaultClimbExitingParameter = "ClimbExiting";
        public const string DefaultClimbStepProgressParameter = "ClimbStepProgress";
        public const string DefaultWallSideParameter = "WallSide";
        public const string DefaultHangingParameter = "Hanging";
        public const string DefaultClimbingParameter = "Climbing";
        public const string DefaultGrabbingParameter = "Grabbing";
        public const string DefaultGrabSpeedParameter = "GrabSpeed";
        public const string DefaultRollProgressParameter = "RollProgress";
        public const string DefaultClimbProgressParameter = "ClimbProgress";
        public const string DefaultVaultingParameter = "Vaulting";
        public const string DefaultVaultProgressParameter = "VaultProgress";
        public const string DefaultRopeSwingingParameter = "RopeSwinging";
        public const string DefaultZipliningParameter = "Ziplining";

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

        [Header("Bool parameters")]
        [Tooltip("Whether the character should be animated as on the ground")]
        [SerializeField] private string groundedParameter = DefaultGroundedParameter;
        [SerializeField] private string jumpingParameter = "";
        [SerializeField] private string dashingParameter = DefaultDashingParameter;
        [SerializeField] private string movingParameter = "";
        [SerializeField] private string crouchingParameter = DefaultCrouchingParameter;
        [SerializeField] private string slidingParameter = DefaultSlidingParameter;
        [SerializeField] private string rollingParameter = DefaultRollingParameter;
        [SerializeField] private string wallRunningParameter = DefaultWallRunningParameter;
        [Tooltip("Sliding down a wall")]
        [SerializeField] private string wallSlidingParameter = DefaultWallSlidingParameter;
        [Tooltip("Climbing a climbable surface (the forward and sideways speed parameters, if set, give the climb's " +
                 "speed up the surface and to the right)")]
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

        [Header("Triggers")]
        [Tooltip("Animator triggers fired by movement events, such as a landing or jump animation")]
        [SerializeField] private EventTrigger[] eventTriggers = Array.Empty<EventTrigger>();

        public override ICharacterAnimationDriver CreateDriver(GameObject model, Animator animator) =>
            animator != null && animator.runtimeAnimatorController != null ? new Driver(this, animator) : null;

        private sealed class Driver : ICharacterAnimationDriver
        {
            // Parameter hashes, or 0 for parameters to skip
            private readonly int speed, verticalSpeed, forwardSpeed, sidewaysSpeed, turnSpeed, wallSide, grabSpeed, rollProgress, climbProgress, vaultProgress, climbRight, climbUp, climbStepProgress;
            private readonly int grounded, jumping, dashing, moving;
            private readonly int crouching, sliding, rolling, wallRunning, wallSliding, wallClimbing, onLadder, climbEntering, climbExiting, hanging, climbing, vaulting, grabbing, ropeSwinging, ziplining;
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
                wallSide = Find(settings.wallSideParameter, AnimatorControllerParameterType.Float);
                grabSpeed = Find(settings.grabSpeedParameter, AnimatorControllerParameterType.Float);
                rollProgress = Find(settings.rollProgressParameter, AnimatorControllerParameterType.Float);
                climbProgress = Find(settings.climbProgressParameter, AnimatorControllerParameterType.Float);
                vaultProgress = Find(settings.vaultProgressParameter, AnimatorControllerParameterType.Float);
                climbRight = Find(settings.climbRightParameter, AnimatorControllerParameterType.Float);
                climbUp = Find(settings.climbUpParameter, AnimatorControllerParameterType.Float);
                climbStepProgress = Find(settings.climbStepProgressParameter, AnimatorControllerParameterType.Float);
                crouching = Find(settings.crouchingParameter, AnimatorControllerParameterType.Bool);
                sliding = Find(settings.slidingParameter, AnimatorControllerParameterType.Bool);
                rolling = Find(settings.rollingParameter, AnimatorControllerParameterType.Bool);
                wallRunning = Find(settings.wallRunningParameter, AnimatorControllerParameterType.Bool);
                wallSliding = Find(settings.wallSlidingParameter, AnimatorControllerParameterType.Bool);
                wallClimbing = Find(settings.wallClimbingParameter, AnimatorControllerParameterType.Bool);
                climbEntering = Find(settings.climbEnteringParameter, AnimatorControllerParameterType.Bool);
                onLadder = Find(settings.onLadderParameter, AnimatorControllerParameterType.Bool);
                climbExiting = Find(settings.climbExitingParameter, AnimatorControllerParameterType.Bool);
                hanging = Find(settings.hangingParameter, AnimatorControllerParameterType.Bool);
                climbing = Find(settings.climbingParameter, AnimatorControllerParameterType.Bool);
                vaulting = Find(settings.vaultingParameter, AnimatorControllerParameterType.Bool);
                grabbing = Find(settings.grabbingParameter, AnimatorControllerParameterType.Bool);
                ropeSwinging = Find(settings.ropeSwingingParameter, AnimatorControllerParameterType.Bool);
                ziplining = Find(settings.zipliningParameter, AnimatorControllerParameterType.Bool);

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
                if (grounded != 0) { animator.SetBool(grounded, state.grounded); }
                if (jumping != 0) { animator.SetBool(jumping, state.jumping); }
                if (dashing != 0) { animator.SetBool(dashing, state.dashing); }
                if (moving != 0) { animator.SetBool(moving, state.moving); }
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
                if (crouching != 0) { animator.SetBool(crouching, state.crouching); }
                if (sliding != 0) { animator.SetBool(sliding, state.sliding); }
                if (rolling != 0) { animator.SetBool(rolling, state.rolling); }
                if (wallRunning != 0) { animator.SetBool(wallRunning, state.wallRunning); }
                if (wallSliding != 0) { animator.SetBool(wallSliding, state.wallSliding); }
                if (wallClimbing != 0) { animator.SetBool(wallClimbing, state.wallClimbing); }
                if (climbEntering != 0) { animator.SetBool(climbEntering, state.climbEntering); }
                if (onLadder != 0) { animator.SetBool(onLadder, state.onLadder); }
                if (climbExiting != 0) { animator.SetBool(climbExiting, state.climbExiting); }
                if (climbStepProgress != 0) { animator.SetFloat(climbStepProgress, state.climbStepProgress); }
                if (hanging != 0) { animator.SetBool(hanging, state.hanging); }
                if (climbing != 0) { animator.SetBool(climbing, state.climbing); }
                if (vaulting != 0) { animator.SetBool(vaulting, state.vaulting); }
                if (grabbing != 0) { animator.SetBool(grabbing, state.grabbing); }
                if (ropeSwinging != 0) { animator.SetBool(ropeSwinging, state.swinging); }
                if (ziplining != 0) { animator.SetBool(ziplining, state.ziplining); }
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
