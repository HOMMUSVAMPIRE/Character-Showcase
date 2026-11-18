using System;
using UnityEngine;

namespace Character
{
    /// <summary>
    /// Walking and running: on the ground, with no ability having the character, movement input moves it the way the input
    /// points (relative to the camera) at the walk speed, or the run speed while Sprint is held (the crouch speed while
    /// crouched: see Crouching), and turns it to face that way. It acts in the mode's Locomotion, after Fall &amp; Air
    /// Handling's ground friction (see MovementComponent.Locomotion). Steering in the air is Fall &amp; Air Handling's,
    /// toward these speeds, as are a roll's steering (see Rolling): turned off, these speeds still shape them.
    /// </summary>
    [Serializable]
    [MovementComponentName("Walking & Running")]
    public class WalkingAndRunning : MovementComponent
    {
        [Tooltip("How fast (m/s) the character moves at full movement input, at its authored size (it scales with size: see the MovementController's Size Scaling)")]
        [SerializeField] private float walkSpeed;
        [Tooltip("How fast (m/s) the character moves while the Sprint button is held, at its authored size. At or below Walk Speed, sprinting does nothing.")]
        [SerializeField] private float runSpeed;
        [Tooltip("Seconds walking takes to pick up from a standstill to the run speed after an ability hands the character " +
                 "back on the ground slower than it walks (the end of a vault, or of a climb up onto a ledge): it speeds up to " +
                 "its walking speed at that rate, from the speed it was going, rather than snapping to it. Otherwise walking " +
                 "is at its speed at once. 0 = at once after abilities too")]
        [Min(0f)] [SerializeField] private float abilityPickUpTime = 0.3f;

        // The abilities that hand the character back to walking on the ground slower than it walks, which it then picks
        // up speed from (see abilityPickUpTime).
        private const CharacterState SlowHandOvers = CharacterState.Vaulting | CharacterState.LedgeClimbing;

        /// <summary>How fast (m/s) the character walks at full movement input, at its authored size.</summary>
        public float WalkSpeed { get => walkSpeed; set => walkSpeed = value; }
        /// <summary>How fast (m/s) the character runs (Sprint held), at its authored size.</summary>
        public float RunSpeed { get => runSpeed; set => runSpeed = value; }

        /// <summary>
        /// The speed to move at for these inputs, at full movement input and the character's authored size: the run
        /// speed while Sprint is held (if there is one, faster than walking), otherwise the walk speed.
        /// </summary>
        public float MoveSpeed(Inputs inputs) =>
            inputs.sprint != null && inputs.sprint.value && runSpeed > walkSpeed ? runSpeed : walkSpeed;

        /// <summary>
        /// On the ground, moves the character the way the movement input points, at walking speed (see the mode's
        /// MoveSpeed), facing that way.
        /// </summary>
        public override void Locomotion(ref MovementData movementData, Inputs inputs, MovementController controller)
        {
            // In the air, the character steers instead (see FallAndAirHandling), and isn't picking up speed after an
            // ability any more (nor once it stops: see PickingUp).
            if (!movementData.initialGrounded)
            {
                controller.walk.pickingUp = false;
                return;
            }
            float steeringEffectiveness = controller.movementSpeedMultiplier;

            // With movement input, move the way it points (relative to the camera) at walking speed (or running, while
            // sprinting), facing that way. Walking only makes up the difference between the momentum the character is
            // carrying that way (from a landing, say) and that speed: it moves at whichever is faster, momentum or
            // walking, and the momentum fades with the ground's friction (see FallAndAirHandling, which comes first).
            // Walking used to be added on top: just after a landing the character moved at its air speed plus its
            // walking speed, and leaving the ground again before the friction had taken the momentum away (a jump right
            // after landing, or touching a ledge's lip for a step or two) carried both into the air, where air steering
            // keeps speed beyond walking. Every short touch of the ground then added walking speed again, and hopping or
            // clipping ledges built up to several times it.
            bool steering = inputs.movementVector.sqrMagnitude > 0.001f && steeringEffectiveness > 0;
            if (!steering && (movementData.previousState & SlowHandOvers) == 0) { controller.walk.pickingUp = false; }
            if (steering)
            {
                Vector3 steer = movementData.cameraLookFlat * inputs.movementVector;
                steer.y = 0f;
                float stick = Mathf.Min(steer.magnitude, 1f);
                float walking = stick * steeringEffectiveness * Mode.MoveSpeed(inputs, controller) * controller.SpeedScale;
                // No speed to walk at (a speed of 0), no walking: nor turning to face the input.
                if (stick > 1e-4f && walking > 1e-6f)
                {
                    Vector3 direction = steer.normalized;
                    walking = PickingUp(ref movementData, controller, direction, walking);
                    Vector3 carried = movementData.initialInertia;
                    float carriedAlong = Mathf.Max(0f, Vector3.Dot(new Vector3(carried.x, 0f, carried.z), direction));
                    movementData.directMovementVector = direction * (Mathf.Max(0f, walking - carriedAlong) * movementData.deltaTime);
                    movementData.targetRotation = Quaternion.Lerp(movementData.initialRotation, Quaternion.LookRotation(direction, Vector3.up), steeringEffectiveness);
                }
            }

            movementData.state |= CharacterState.Moving;
        }

        /// <summary>
        /// The speed (m/s) to walk at this step, toward walking (its full speed): after an ability hands the character back
        /// slower than that (see SlowHandOvers), up from the speed it was going (the way it's heading) at the pick-up
        /// rate, until it's caught up; otherwise walking. A vault ending standing on the top, or a climb onto a ledge,
        /// snapped from the speed it left with (or a near standstill) to walking or running in a step.
        /// </summary>
        private float PickingUp(ref MovementData movementData, MovementController controller, Vector3 direction, float walking)
        {
            WalkState walk = controller.walk;
            if ((movementData.previousState & SlowHandOvers) != 0) { walk.pickingUp = abilityPickUpTime > 0f; }
            if (!walk.pickingUp) { return walking; }
            Vector3 was = movementData.initialVelocity;
            float wasAlong = Mathf.Max(0f, Vector3.Dot(new Vector3(was.x, 0f, was.z), direction));
            float rate = Mathf.Max(runSpeed, walkSpeed) * controller.SpeedScale / (abilityPickUpTime * controller.DurationScale);
            float now = wasAlong + rate * movementData.deltaTime;
            if (now >= walking) { walk.pickingUp = false; return walking; }
            return now;
        }
    }

    /// <summary>The character picking up speed to walk after an ability (see WalkingAndRunning).</summary>
    public class WalkState
    {
        /// <summary>Whether walking is picking up speed after an ability handed the character back slower than it walks.</summary>
        public bool pickingUp;
    }

    // This state's place on the character (see MovementController): declared here, beside its ability, so a new
    // ability adds its state in its own file rather than in MovementController.cs.
    public partial class MovementController
    {
        /// <summary>The character's walking state (Walking &amp; Running): picking up speed after an ability.</summary>
        public WalkState walk { get; } = new WalkState();
    }
}
