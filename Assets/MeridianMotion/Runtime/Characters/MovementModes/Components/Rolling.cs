using System;
using UnityEngine;
using static MeridianMotion.Characters.MovementRules;

namespace MeridianMotion.Characters
{
    /// <summary>
    /// Rolling: a quick, committed roll along the ground, low (at the crouch height: see Crouching), by pressing Roll or
    /// by itself on landing from a high enough fall.
    /// </summary>
    [Serializable]
    public class Rolling : MovementComponent
    {
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

        /// <summary>How fast (m/s) the character rolls, at its authored size, before the speed curve.</summary>
        public float RollSpeed { get => rollSpeed; set => rollSpeed = value; }
        /// <summary>How long a roll lasts, in seconds. 0 turns rolling off.</summary>
        public float RollDuration { get => rollDuration; set => rollDuration = value; }

        public override bool IsActive(MMotionMotor motor) => motor.roll.IsRolling;
        /// <summary>A roll is on the ground: the character is neither held up nor holding on.</summary>
        public override AbilityTraits Traits => AbilityTraits.None;
        /// <summary>The events of Rolling starting and ending (see MovementComponent.StateEvents).</summary>
        public override StateEventPair[] StateEvents => stateEvents;
        private static readonly StateEventPair[] stateEvents =
        {
            new(CharacterState.Rolling, MovementEvents.RollStarted, MovementEvents.RollEnded),
        };

        /// <summary>Rolling, low at the crouch height (Crouching's, whether or not it's turned on).</summary>
        public override BodyPosture? Posture(MMotionMotor motor) =>
            motor.roll.IsRolling ? BodyPosture.Low(SettingsOf<Crouching>().CrouchHeight) : null;

        /// <summary>Frees the next press of Roll once it's released, advances the cooldown, and keeps track of falls (see TrackFall).</summary>
        public override void AdvanceTimers(MovementStep step, StepInputs inputs, MMotionMotor motor)
        {
            motor.roll.press.Track(inputs.roll);
            motor.roll.cooldownRemaining = MovementRules.CountDown(motor.roll.cooldownRemaining, step.deltaTime);
            TrackFall(step, motor);
        }

        public override void Interrupt(MovementStep step, MMotionMotor motor)
        {
            if (motor.roll.IsRolling) { EndRoll(motor); }
        }

        /// <summary>
        /// Starts a roll when the roll button is pressed on the ground, in the direction of movement input (relative to
        /// the input's look), or straight ahead without input, or by itself on landing from a high enough fall while moving
        /// (see TryLandingRoll), and carries it on: the character rolls along the ground at the
        /// roll's speed, low, facing the roll, and can't steer, jump or dash until it's over. Rolling off an edge ends
        /// the roll, keeping its momentum. Afterwards the character stands, or stays crouched where there isn't room.
        /// </summary>
        public override bool TakeOver(MovementStep step, StepInputs inputs, MMotionMotor motor)
        {
            RollState roll = motor.roll;
            if (!roll.IsRolling && !TryLandingRoll(step, inputs, motor))
            {
                if (rollDuration <= 0f || !step.initialGrounded || Mode.AbilityInProgress(motor)
                    || roll.cooldownRemaining > 0f || !roll.press.Consume(inputs.roll, rollInputBuffer))
                {
                    return false;
                }
                Vector3 direction = InputDirection(step, inputs);
                roll.direction = direction.sqrMagnitude > 0.01f ? direction.normalized : Facing(step);
                roll.timeRemaining = roll.duration = rollDuration * motor.DurationScale;
            }

            // Rolled off an edge: the roll's momentum (already the character's inertia) carries into the air.
            if (!step.initialGrounded)
            {
                EndRoll(motor);
                return false;
            }

            // The roll's speed and length follow the character's size (see MMotionMotor.SpeedScale).
            float duration = rollDuration * motor.DurationScale;
            float progress = 1f - roll.timeRemaining / Mathf.Max(duration, 1e-4f);
            float speed = rollSpeed * motor.SpeedScale * rollSpeedCurve.Evaluate(progress);
            roll.timeRemaining = MovementRules.CountDown(roll.timeRemaining, step.deltaTime);
            if (!roll.IsRolling) { roll.cooldownRemaining = rollCooldown; }

            // Momentum rather than a velocity override, so gravity keeps the character on the ground (down slopes too),
            // and what's left of it when the roll ends eases out with grounded friction.
            step.initialInertia = new Vector3(roll.direction.x * speed, step.initialInertia.y, roll.direction.z * speed);
            step.directMovementVector = Vector3.zero;
            step.targetRotation = Quaternion.LookRotation(roll.direction, Vector3.up);
            step.state |= CharacterState.Rolling | CharacterState.Moving;
            if (!roll.IsRolling) { HandOverToWalking(step, inputs, motor, speed); }
            return true;
        }

        /// <summary>
        /// Starts a roll on the step the character lands, if it fell at least the landing roll's height (see
        /// TrackFall) and is moving (at least the landing roll's speed, horizontally) or steering: the way it's moving
        /// (or, slower, the way it's steered). The cooldown and the button don't matter: it rolls on impact. Not with a
        /// jump pressed for the landing (within the jump's input buffer): that jumps instead.
        /// </summary>
        private bool TryLandingRoll(MovementStep step, StepInputs inputs, MMotionMotor motor)
        {
            RollState roll = motor.roll;
            if (rollDuration <= 0f || landingRollMinFall <= 0f || !step.initialGrounded || Mode.AbilityInProgress(motor)) { return false; }
            if (roll.landingFall < landingRollMinFall * motor.Body.StandingHeight || Mode.HasFreshJumpPress(inputs, motor)) { return false; }
            Vector3 inertia = step.initialInertia;
            Vector3 moving = new Vector3(inertia.x, 0f, inertia.z);
            Vector3 steer = InputDirection(step, inputs);
            Vector3 direction = moving.magnitude >= landingRollMinSpeed * motor.SpeedScale ? moving
                : steer.magnitude >= 0.3f ? steer : Vector3.zero;
            if (direction.sqrMagnitude < 1e-6f) { return false; }
            roll.direction = direction.normalized;
            roll.timeRemaining = roll.duration = rollDuration * motor.DurationScale;
            return true;
        }

        // A character moving down slower than this (m/s, at its authored size) isn't falling yet: a fall is measured from
        // the last step it was (the top of a jump, a dash, which holds it level).
        private const float FallStartSpeed = 0.1f;

        /// <summary>
        /// Keeps track of how far the character falls, for rolling on landing: where the current fall started (its feet,
        /// on the last step it wasn't falling, or was on the ground or held up by an ability (AbilityTraits.Supported: a wall,
        /// a ledge, a rope, a cable, water): so a drop from a ledge is measured from the ledge, and a dash or a jump just
        /// before landing starts the fall again from there), and on the step it lands, how far below that it is. Every
        /// step, whichever ability has the character.
        /// </summary>
        private void TrackFall(MovementStep step, MMotionMotor motor)
        {
            RollState roll = motor.roll;
            float y = step.initialPosition.y;
            bool landed = step.initialGrounded && !step.previousState.HasAny(CharacterState.Grounded);
            roll.landingFall = landed && roll.hasFallTop ? Mathf.Max(0f, roll.fallTop - y) : 0f;
            // Whatever holds the character up (see AbilityTraits.Supported): a ride down a sloped zipline, say, isn't a fall.
            bool held = step.initialGrounded || Mode.AbilityInProgressWith(AbilityTraits.Supported, motor);
            bool falling = step.initialVelocity.y < -FallStartSpeed * motor.SpeedScale;
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
        private void HandOverToWalking(MovementStep step, StepInputs inputs, MMotionMotor motor, float rollSpeedNow)
        {
            Vector3 steer = InputDirection(step, inputs);
            if (steer.sqrMagnitude < 0.01f) { return; }
            float walkSpeed = Mode.MoveSpeed(inputs, motor) * motor.SpeedScale * motor.speedMultiplier * steer.magnitude;
            Vector3 walkDirection = steer.normalized;
            float beyond = Mathf.Max(0f, Vector3.Dot(motor.roll.direction * rollSpeedNow, walkDirection) - walkSpeed);
            Vector3 inertia = step.initialInertia;
            step.initialInertia = new Vector3(walkDirection.x * beyond, inertia.y, walkDirection.z * beyond);
            step.directMovementVector = walkDirection * (walkSpeed * step.deltaTime);
            step.targetRotation = Quaternion.LookRotation(walkDirection, Vector3.up);
        }

        private void EndRoll(MMotionMotor motor)
        {
            motor.roll.timeRemaining = 0f;
            motor.roll.cooldownRemaining = rollCooldown;
        }

        /// <summary>How far a roll carries the character, if it rolls.</summary>
        public override void MeasureCapabilities(MovementSimulation simulation, MovementCapabilities capabilities)
        {
            if (rollDuration > 0f) { capabilities.RollLength = RollLength(simulation); }
        }

        /// <summary>How far (m) a roll carries the character, from where it starts to where it ends.</summary>
        private static float RollLength(MovementSimulation simulation)
        {
            simulation.Reset(Vector3.zero);
            simulation.Run(MovementSimulation.StepsFor(MovementSimulation.MeasureRunUp), _ => MovementSimulation.Move(Vector3.forward));
            float start = float.NaN;
            for (int i = 0; i < MovementSimulation.StepsFor(MovementSimulation.MeasureTimeout); i++)
            {
                float before = simulation.Position.z;
                StepInputs inputs = MovementSimulation.Move(Vector3.forward);
                inputs.roll = MovementSimulation.Held(i);
                simulation.Step(inputs);
                bool rolling = simulation.State.HasAny(CharacterState.Rolling);
                if (rolling && float.IsNaN(start)) { start = before; }
                if (!rolling && !float.IsNaN(start)) { return simulation.Position.z - start; }
            }
            return float.NaN;
        }
    }

    /// <summary>A roll in progress, and the cooldown between rolls.</summary>
    public class RollState
    {
        /// <summary>Seconds left in the current roll (0 when not rolling).</summary>
        public float timeRemaining;
        /// <summary>Seconds until another roll may start.</summary>
        public float cooldownRemaining;
        /// <summary>The direction of the current roll (horizontal, unit length).</summary>
        public Vector3 direction;
        /// <summary>How long the current roll lasts, in seconds.</summary>
        public float duration;
        /// <summary>The Roll button's presses, so each press starts at most one roll.</summary>
        public readonly PressBuffer press = new();
        /// <summary>
        /// Where the character's current fall started (its feet's y, m: where it last wasn't falling, or was on the
        /// ground or held by a wall or a ledge), for rolling on landing.
        /// </summary>
        public float fallTop;
        /// <summary>Whether fallTop holds the start of a fall.</summary>
        public bool hasFallTop;
        /// <summary>On the step the character lands, how far (m) it fell from fallTop (0 on every other step): for rolling on landing.</summary>
        public float landingFall;

        /// <summary>Whether a roll is in progress.</summary>
        public bool IsRolling => timeRemaining > 0f;
        /// <summary>How far through the roll the character is: 0 at its start, 1 at its end.</summary>
        public float Progress => duration > 0f ? Mathf.Clamp01(1f - timeRemaining / duration) : 0f;
    }

    // This state's place on the character (see MMotionMotor): declared here, beside its ability, so a new
    // ability adds its state in its own file rather than in MMotionMotor.cs.
    public partial class MMotionMotor
    {
        /// <summary>The character's rolling state (Rolling).</summary>
        public RollState roll { get; } = new RollState();
    }
}
