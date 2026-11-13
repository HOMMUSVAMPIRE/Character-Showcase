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
        [Tooltip("Landing from a fall at least this high (as a share of the character's standing height: from where it " +
                 "last wasn't falling, as at the top of a jump or in a dash, or was on the ground or held by a wall or a " +
                 "ledge) while moving, the character rolls on impact, the way it's going, without the button. 0 turns " +
                 "rolling on landing off")]
        [Min(0f)] [SerializeField] private float landingRollMinFall = 2f;
        [Tooltip("The slowest (m/s, horizontally, at the character's authored size) the character can land moving and " +
                 "still roll on landing; slower, steering counts as moving")]
        [Min(0f)] [SerializeField] private float landingRollMinSpeed = 2f;

        float RollMode.rollSpeed { get => rollSpeed; set => rollSpeed = value; }
        float RollMode.rollDuration { get => rollDuration; set => rollDuration = value; }

        /// <summary>
        /// Starts a roll when the roll button is pressed on the ground, in the direction of movement input (relative to
        /// the camera), or straight ahead without input, or by itself on landing from a high enough fall while moving
        /// (see TryLandingRoll), and carries it on: the character rolls along the ground at the
        /// roll's speed, low, facing the roll, and can't steer, jump or dash until it's over. Rolling off an edge ends
        /// the roll, keeping its momentum. Afterwards the character stands, or stays crouched where there isn't room.
        /// </summary>
        public bool ProcessMovementDataForRoll(ref MovementData movementData, Inputs inputs, MovementController controller)
        {
            RollState roll = controller.roll;
            if (!roll.IsRolling && !TryLandingRoll(movementData, inputs, controller))
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
        /// Starts a roll on the step the character lands, if it fell at least the landing roll's height (see
        /// TrackFall) and is moving (at least the landing roll's speed, horizontally) or steering: the way it's moving
        /// (or, slower, the way it's steered). The cooldown and the button don't matter: it rolls on impact. Not with a
        /// jump pressed for the landing (within the jump's input buffer): that jumps instead.
        /// </summary>
        private bool TryLandingRoll(MovementData movementData, Inputs inputs, MovementController controller)
        {
            RollState roll = controller.roll;
            if (rollDuration <= 0f || landingRollMinFall <= 0f || !movementData.initialGrounded || AbilityInProgress(controller)) { return false; }
            if (roll.landingFall < landingRollMinFall * controller.StandingHeight || IsNewJumpPress(inputs, controller)) { return false; }
            Vector3 inertia = movementData.initialInertia;
            Vector3 moving = new Vector3(inertia.x, 0f, inertia.z);
            Vector3 steer = InputDirection(movementData, inputs);
            Vector3 direction = moving.magnitude >= landingRollMinSpeed * controller.SpeedScale ? moving
                : steer.magnitude >= 0.3f ? steer : Vector3.zero;
            if (direction.sqrMagnitude < 1e-6f) { return false; }
            roll.direction = direction.normalized;
            roll.timeRemaining = roll.duration = rollDuration * controller.DurationScale;
            return true;
        }

        // A character moving down slower than this (m/s, at its authored size) isn't falling yet: a fall is measured from
        // the last step it was (the top of a jump, a dash, which holds it level).
        private const float FallStartSpeed = 0.1f;

        /// <summary>
        /// Keeps track of how far the character falls, for rolling on landing: where the current fall started (its feet,
        /// on the last step it wasn't falling, or was on the ground or held by a wall or a ledge: so a drop from a ledge is
        /// measured from the ledge, and a dash or a jump just before landing starts the fall again from there), and on
        /// the step it lands, how far below that it is. Every step, whichever ability has the character.
        /// </summary>
        private static void TrackFall(MovementData movementData, MovementController controller)
        {
            RollState roll = controller.roll;
            float y = movementData.initialPosition.y;
            bool landed = movementData.initialGrounded && !movementData.previousState.HasFlag(CharacterState.Grounded);
            roll.landingFall = landed && roll.hasFallTop ? Mathf.Max(0f, roll.fallTop - y) : 0f;
            bool held = movementData.initialGrounded || controller.ledge.IsActive || controller.wallClimb.IsClimbing
                        || controller.wallRun.IsWallRunning || controller.wallSlide.IsWallSliding || controller.vault.IsActive;
            bool falling = movementData.initialVelocity.y < -FallStartSpeed * controller.SpeedScale;
            if (held || !falling || !roll.hasFallTop) { roll.fallTop = y; }
            roll.hasFallTop = true;
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
