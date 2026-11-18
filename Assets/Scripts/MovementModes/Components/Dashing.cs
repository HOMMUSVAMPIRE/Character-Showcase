using System;
using UnityEngine;

namespace Character
{
    /// <summary>
    /// Dashing: a short, fast burst of movement on the dash button, in a straight line, on the ground or (a set number of
    /// times before landing) in the air. It runs once the character has walked (see MovementComponent.AfterWalking),
    /// taking over its movement while it lasts; jumping cancels it into the jump (see Jumping, after it in the list).
    /// </summary>
    [Serializable]
    public class Dashing : MovementComponent
    {
        [Tooltip("How fast the character moves during a dash")]
        [SerializeField] private float dashSpeed = 30f;
        [Tooltip("How long a dash lasts, in seconds. 0 turns dashing off")]
        [SerializeField] private float dashDuration = 0.15f;
        [Tooltip("A curve over the dash (0 = start, 1 = end) scaling its speed, so a dash can burst out and ease off")]
        [SerializeField] private AnimationCurve dashSpeedCurve = AnimationCurve.Linear(0f, 1f, 1f, 0.75f);
        [Tooltip("The fraction of the dash's speed the character keeps as momentum once the dash ends")]
        [Range(0f, 1f)] [SerializeField] private float dashEndSpeedRetention = 0.3f;
        [Tooltip("Seconds after a dash starts before another can begin")]
        [SerializeField] private float dashCooldown = 0.4f;
        [Tooltip("How many dashes the character can perform before landing again")]
        [SerializeField] private int maxAirDashes = 1;
        [Tooltip("If the dash button is pressed this many seconds before a dash is available, dash as soon as it is")]
        [SerializeField] private float dashInputBuffer = 0.1f;
        [Tooltip("Jumping during a dash cancels it into the jump. This is the fraction of the dash's speed carried into the jump")]
        [Range(0f, 1f)] [SerializeField] private float dashJumpSpeedRetention = 0.5f;

        /// <summary>How fast (m/s) the character moves during a dash, at its authored size.</summary>
        public float DashSpeed { get => dashSpeed; set => dashSpeed = value; }
        /// <summary>How long a dash lasts, in seconds. 0 turns dashing off.</summary>
        public float DashDuration { get => dashDuration; set => dashDuration = value; }
        /// <summary>How many dashes the character can make in the air before landing again.</summary>
        public int MaxAirDashes { get => maxAirDashes; set => maxAirDashes = value; }

        public override bool IsActive(MovementController controller) => controller.dash.IsDashing;

        public override void Interrupt(MovementData movementData, MovementController controller)
        {
            if (!controller.dash.IsDashing) { return; }
            controller.dash.timeRemaining = 0f;
            movementData.events |= MovementEvents.DashEnded;
        }

        /// <summary>
        /// Determines whether the character can begin a dash this frame: the button has been newly pressed (or was
        /// pressed within the input buffer), the cooldown is over, and they have an air dash left if airborne
        /// </summary>
        private bool CanDash(MovementData data, Inputs inputs, MovementController controller)
        {
            DashState dash = controller.dash;
            bool pressed = dash.press.IsFresh(inputs.dash, dashInputBuffer);
            bool hasAirDash = data.initialGrounded || dash.airDashesUsed < maxAirDashes;
            // A dash with no length would start (raising DashStarted, using the cooldown and an air dash) and be over
            // before its first step, so it would never raise DashEnded: no length means no dashing.
            return pressed && dashDuration > 0f && !dash.IsDashing && dash.cooldownRemaining <= 0f && hasAirDash;
        }

        /// <summary>
        /// Advances the dash's cooldown, gives back air dashes on the ground, and frees the next press once the button is
        /// released. Every step, including while another ability has the character, so its cooldown runs out meanwhile.
        /// </summary>
        public override void AdvanceTimers(MovementData movementData, Inputs inputs, MovementController controller)
        {
            DashState dash = controller.dash;
            dash.cooldownRemaining = MovementHelpers.CountDown(dash.cooldownRemaining, movementData.deltaTime);
            if (movementData.initialGrounded) { dash.airDashesUsed = 0; }
            dash.press.Track(inputs.dash);
        }

        /// <summary>
        /// Reads dash button input and turns it into a short, fast burst of movement. While dashing, the character moves
        /// at the dash's speed in a straight line, ignoring gravity and steering; when the dash ends they keep a fraction
        /// of its speed as momentum, which the grounded friction or the landing then eases out.
        /// </summary>
        public override void AfterWalking(MovementData movementData, Inputs inputs, MovementController controller)
        {
            DashState dash = controller.dash;
            float deltaTime = movementData.deltaTime;

            // Start a dash in the direction of movement input (relative to the camera), or straight ahead without input
            if (CanDash(movementData, inputs, controller))
            {
                Vector3 direction = movementData.lookFlat * inputs.movementVector;
                if (direction.sqrMagnitude < 0.01f) { direction = movementData.initialRotation * Vector3.forward; }
                direction.y = 0f;
                dash.direction = direction.normalized;
                dash.timeRemaining = dashDuration * controller.DurationScale;
                dash.cooldownRemaining = dashCooldown;
                dash.press.Use();
                if (!movementData.initialGrounded) { dash.airDashesUsed++; }
                movementData.events |= MovementEvents.DashStarted;
            }

            if (!dash.IsDashing) { return; }

            // Jumping cancels the dash into the jump, keeping some of the dash's speed; the jump then runs as normal this
            // frame. Only a press made after the dash began counts, so a jump held from before it doesn't cancel it.
            // The dash's speed and length follow the character's size (see MovementController.SpeedScale).
            float duration = dashDuration * controller.DurationScale;
            float topSpeed = dashSpeed * controller.SpeedScale;
            float dashElapsed = duration - dash.timeRemaining;
            bool jumpPressedDuringDash = Mode.IsNewJumpPress(inputs, controller) && inputs.jump.holdDuration < dashElapsed;
            // Not while crouched with no room to stand up for the jump (it's skipped then, so the dash would just stop).
            bool hasJump = (movementData.initialGrounded || controller.jump.remaining > 0) && !controller.crouch.IsCrouched;
            if (jumpPressedDuringDash && hasJump)
            {
                dash.timeRemaining = 0f;
                movementData.events |= MovementEvents.DashEnded;
                Vector3 momentum = dash.direction * (topSpeed * dashJumpSpeedRetention);
                movementData.initialInertia = new Vector3(momentum.x, 0f, momentum.z);
                movementData.directMovementVector = Vector3.zero; // Momentum only, so walking doesn't stack on top of it.
                return;
            }

            // Dash: take over the character's velocity entirely (no gravity, no steering), and face the dash
            float progress = 1f - dash.timeRemaining / Mathf.Max(duration, 1e-4f);
            float speed = topSpeed * dashSpeedCurve.Evaluate(progress);
            dash.timeRemaining = MovementHelpers.CountDown(dash.timeRemaining, deltaTime);

            // On the final frame of the dash, hand over to momentum rather than stopping dead
            if (!dash.IsDashing)
            {
                speed = topSpeed * dashEndSpeedRetention;
                movementData.events |= MovementEvents.DashEnded;
            }

            movementData.velocityOverride = dash.direction * speed;
            movementData.directMovementVector = Vector3.zero;
            movementData.targetRotation = Quaternion.LookRotation(dash.direction, Vector3.up);
            movementData.state = movementData.state | CharacterState.Dashing;
        }
    }

    /// <summary>
    /// A character's dash state, which persists between frames. Lives on the MovementController (one per character),
    /// since movement modes are shared assets.
    /// </summary>
    public class DashState
    {
        /// <summary>Seconds left in the current dash (0 when not dashing).</summary>
        public float timeRemaining;
        /// <summary>Seconds until another dash may start.</summary>
        public float cooldownRemaining;
        /// <summary>Air dashes used since last touching the ground.</summary>
        public int airDashesUsed;
        /// <summary>The direction of the current dash.</summary>
        public Vector3 direction;
        /// <summary>The press of Dash: one press starts one dash, so holding it doesn't repeat, within the Dash Input Buffer.</summary>
        public readonly ButtonPress press = new();

        /// <summary>Whether a dash is in progress.</summary>
        public bool IsDashing => timeRemaining > 0f;
    }

    // This state's place on the character (see MovementController): declared here, beside its ability, so a new
    // ability adds its state in its own file rather than in MovementController.cs.
    public partial class MovementController
    {
        /// <summary>The character's dashing state (Dashing).</summary>
        public DashState dash { get; } = new DashState();
    }
}
