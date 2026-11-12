using UnityEngine;

namespace Character
{
    // Rolling: a quick, committed roll along the ground, low (at the crouch height).
    public partial class BasicMovementMode
    {
        [Header("Rolling")]
        [Tooltip("How fast (m/s) the character rolls, at its authored size, before the curve below")]
        [SerializeField] private float rollSpeed = 8f;
        [Tooltip("How long a roll lasts, in seconds. 0 turns rolling off")]
        [SerializeField] private float rollDuration = 0.55f;
        [Tooltip("A curve over the roll (0 = start, 1 = end) scaling its speed, so a roll can burst out and slow down")]
        [SerializeField] private AnimationCurve rollSpeedCurve = AnimationCurve.Linear(0f, 1f, 1f, 0.6f);
        [Tooltip("Seconds after a roll ends before another can begin")]
        [SerializeField] private float rollCooldown = 0.2f;
        [Tooltip("If the roll button is pressed this many seconds before a roll is possible, roll as soon as it is")]
        [SerializeField] private float rollInputBuffer = 0.15f;

        float RollMode.rollSpeed { get => rollSpeed; set => rollSpeed = value; }
        float RollMode.rollDuration { get => rollDuration; set => rollDuration = value; }

        /// <summary>
        /// Starts a roll when the roll button is pressed on the ground, in the direction of movement input (relative to
        /// the camera), or straight ahead without input, and carries it on: the character rolls along the ground at the
        /// roll's speed, low, facing the roll, and can't steer, jump or dash until it's over. Rolling off an edge ends
        /// the roll, keeping its momentum. Afterwards the character stands, or stays crouched where there isn't room.
        /// </summary>
        public bool ProcessMovementDataForRoll(ref MovementData movementData, Inputs inputs, MovementController controller)
        {
            RollState roll = controller.roll;
            if (!roll.IsRolling)
            {
                if (rollDuration <= 0f || !movementData.initialGrounded || AbilityInProgress(controller)
                    || roll.cooldownRemaining > 0f || !roll.press.Consume(inputs.roll, rollInputBuffer))
                {
                    return false;
                }
                Vector3 direction = InputDirection(movementData, inputs);
                roll.direction = direction.sqrMagnitude > 0.01f ? direction.normalized : Facing(movementData);
                roll.timeRemaining = roll.duration = rollDuration * controller.DurationScale;
            }

            // Rolled off an edge: the roll's momentum (already the character's inertia) carries into the air.
            if (!movementData.initialGrounded)
            {
                EndRoll(controller);
                return false;
            }

            // The roll's speed and length follow the character's size (see MovementController.SpeedScale).
            float duration = rollDuration * controller.DurationScale;
            float progress = 1f - roll.timeRemaining / Mathf.Max(duration, 1e-4f);
            float speed = rollSpeed * controller.SpeedScale * rollSpeedCurve.Evaluate(progress);
            roll.timeRemaining = Mathf.Max(0f, roll.timeRemaining - movementData.deltaTime);
            if (!roll.IsRolling) { roll.cooldownRemaining = rollCooldown; }

            // Momentum rather than a velocity override, so gravity keeps the character on the ground (down slopes too),
            // and what's left of it when the roll ends eases out with grounded friction.
            movementData.initialInertia = new Vector3(roll.direction.x * speed, movementData.initialInertia.y, roll.direction.z * speed);
            movementData.directMovementVector = Vector3.zero;
            movementData.targetRotation = Quaternion.LookRotation(roll.direction, Vector3.up);
            movementData.state |= CharacterState.Rolling | CharacterState.Moving;
            if (!roll.IsRolling) { HandOverToWalking(ref movementData, inputs, controller, speed); }
            return true;
        }

        /// <summary>
        /// On a roll's last step, with movement input held, hands the roll straight over to walking (or sprinting): the
        /// character moves as it will walk from here on, keeping only whatever speed the roll has beyond that, which
        /// grounded friction then eases out. Otherwise the roll's leftover momentum would stack on top of the walk,
        /// a sudden burst of speed (and a lurch in the animations) as the roll ends. Without input, the momentum is
        /// kept, to ease out.
        /// </summary>
        private void HandOverToWalking(ref MovementData movementData, Inputs inputs, MovementController controller, float rollSpeedNow)
        {
            Vector3 steer = InputDirection(movementData, inputs);
            if (steer.sqrMagnitude < 0.01f) { return; }
            float walkSpeed = MoveSpeed(inputs, controller) * controller.SpeedScale * controller.movementSpeedMultiplier * steer.magnitude;
            Vector3 walkDirection = steer.normalized;
            float beyond = Mathf.Max(0f, Vector3.Dot(controller.roll.direction * rollSpeedNow, walkDirection) - walkSpeed);
            Vector3 inertia = movementData.initialInertia;
            movementData.initialInertia = new Vector3(walkDirection.x * beyond, inertia.y, walkDirection.z * beyond);
            movementData.directMovementVector = walkDirection * (walkSpeed * movementData.deltaTime);
            movementData.targetRotation = Quaternion.LookRotation(walkDirection, Vector3.up);
        }

        private void EndRoll(MovementController controller)
        {
            controller.roll.timeRemaining = 0f;
            controller.roll.cooldownRemaining = rollCooldown;
        }
    }
}
