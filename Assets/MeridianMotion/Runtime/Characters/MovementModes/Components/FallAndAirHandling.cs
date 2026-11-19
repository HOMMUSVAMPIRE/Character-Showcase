using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace MeridianMotion.Characters
{
    /// <summary>
    /// Fall &amp; Air Handling: gravity (and the gentler gravity of coyote time just after walking off an edge), the fall
    /// speed limit, steering in the air, and the ground's friction on the momentum the character carries when it lands
    /// (or stops dashing, rolling...). Gravity acts every step before the abilities (those that move the character
    /// without it override it: see MovementComponent.BeforeAbilities), and the fall speed limit last of all
    /// (FinishStep), whatever has the character; steering and friction only when no ability has it (Locomotion), the
    /// friction before Walking &amp; Running reads the momentum. The abilities that work with gravity (a jump's launch
    /// speed, a rope's swing, a slide down a wall) and the air acceleration read these settings whether or not this is
    /// turned on: turned off, the character doesn't fall, steer in the air, or lose momentum on the ground.
    /// </summary>
    [Serializable]
    [MovementComponentName("Fall & Air Handling")]
    public class FallAndAirHandling : MovementComponent
    {
        [Header("Gravity & Falling")]
        [Tooltip("The base acceleration applied to the character when falling, at its authored size (it scales with size: see the MMotionMotor's Size Scaling)")]
        [SerializeField] [FormerlySerializedAs("gravityStrength")] private float gravity;
        [Tooltip("The maximum falling speed the player can reach. (if 0, do not clamp fall speed)")]
        [SerializeField] private float maxFallSpeed;
        [Tooltip("A curve that determines the impact of gravity during coyote time")]
        [SerializeField] private AnimationCurve coyoteTimeGravityCurve = new();

        [Header("Air Steering")]
        [Tooltip("The maximum strength with which the player can control their character's movement while in the air")]
        [SerializeField] private float airSteeringStrength;
        [Tooltip("A curve that determines how quickly after leaving the ground that the player can steer their character in the air")]
        [SerializeField] [FormerlySerializedAs("airSteeringStrengthCurve")] private AnimationCurve airControlCurve = new();
        [Tooltip("How quickly (m/s²) air steering changes the character's horizontal velocity, before the strength and curve above. Scales with the character's size (see the MMotionMotor's Size Scaling).")]
        [SerializeField] [FormerlySerializedAs("airAcceleration")] private float airSteeringAcceleration = 80f;
        [Tooltip("How quickly (m/s²) the character slows down in the air with no steering input. Scales with the character's size.")]
        [SerializeField] private float airDrag = 4f;
        [Tooltip("Seconds over which air steering comes back after jumping off a wall (a wall run, a slide down a wall, a " +
                 "climb), easing in from none, so the push away from the wall carries the character off it rather than " +
                 "being steered straight back")]
        [Min(0f)] [SerializeField] private float wallJumpSteeringRecovery = 0.6f;

        [Header("Landing")]
        [Tooltip("How quickly the ground's friction takes away the horizontal momentum the character carries onto it (from " +
                 "a landing, a dash, a roll), per second: higher stops it sooner")]
        [SerializeField] [FormerlySerializedAs("groundedDampenHorizontalInertia")] private float groundFriction = 10f;

        /// <summary>The character's gravity (m/s²) at its authored size.</summary>
        public float GravityStrength => gravity;
        /// <summary>How quickly (m/s²) air steering changes the character's horizontal velocity, at its authored size.</summary>
        public float AirAcceleration => airSteeringAcceleration;

        /// <summary>
        /// Applies gravity to the character, and on the ground keeps just enough of it to stay on the ground. Abilities
        /// that move the character without gravity (a dash, a wall run, hanging from a ledge) override the velocity
        /// this gives.
        /// </summary>
        public override void BeforeAbilities(MovementStep step, StepInputs inputs, MMotionMotor motor)
        {
            // How much gravity do we apply
            // The coyote gravity curve only applies after walking off the ground. After a jump, gravity is at full
            // strength, so a jump ended early (a short tap, or bumping a ceiling) doesn't float at its peak.
            float airGravity = motor.HasJumpedSinceGrounded ? 1f : coyoteTimeGravityCurve.Evaluate(step.timeSinceGrounded);
            // Gravity scales with the character's size as accelerations do (see MMotionMotor.AccelerationScale).
            float g = gravity * motor.AccelerationScale * (step.initialGrounded ? 0.5f : airGravity);
            // Apply gravity to the character
            step.velocityChange += Vector3.down * g * step.deltaTime;

            // If we are on the ground, limit the downward velocity - we do want some, so that we stick to the ground, but not too much
            if (step.initialGrounded)
            {
                // Note: g is an acceleration used as a speed here. As a speed it scales with height, not as g does (see
                // AccelerationScale). A jump sets its launch speed outright (see Jumping), so this
                // doesn't add to it. (It once did, giving every jump's first frame g as extra launch speed.)
                float groundedSpeed = g / motor.AccelerationScale * motor.HeightScale;
                step.initialVelocity = new Vector3(step.initialVelocity.x, -groundedSpeed, step.initialVelocity.z);
                // Inertia is what actually drives movement. Without this, gravity piles up in inertia.y every grounded
                // frame (the floor blocks the motion but never resets inertia), so walking off a ledge after standing
                // for a while launches the character downward far faster than a normal fall. This frame's gravity
                // (added via velocityChange above) still applies, which keeps a slight downward push to stay grounded.
                step.initialInertia = new Vector3(step.initialInertia.x, Mathf.Max(step.initialInertia.y, 0f), step.initialInertia.z);
            }
        }

        /// <summary>
        /// On the ground, the friction that takes away the momentum the character carries (before Walking &amp; Running,
        /// which makes up the difference to its speed); in the air, steering.
        /// </summary>
        public override void Locomotion(MovementStep step, StepInputs inputs, MMotionMotor motor)
        {
            if (step.initialGrounded)
            {
                // We are grounded, so to limit sliding, kill horizontal momentum over time to simulate friction
                // A rate per second: over the same share of a stride at any size (see MMotionMotor.DurationScale).
                step.initialInertia = Vector3.Lerp(step.initialInertia, Vector3.up * step.initialInertia.y,
                    groundFriction * step.deltaTime / motor.DurationScale);
                return;
            }

            // We are not grounded, so determine the extent to which we are able to move
            float steeringEffectiveness = motor.speedMultiplier
                                          * airControlCurve.Evaluate(step.timeSinceGrounded) * airSteeringStrength
                                          * WallJumpSteering(motor);
            SteerInAir(step, inputs, motor, steeringEffectiveness);
            step.state |= CharacterState.Moving;
        }

        /// <summary>Limits how fast the character falls.</summary>
        public override void FinishStep(MovementStep step, MMotionMotor motor)
        {
            // If we are falling too fast, limit our fall speed. A maxFallSpeed of 0 disables the clamp (per tooltip).
            // Like every speed, the limit scales with the character's size (see MMotionMotor.SpeedScale).
            float fallLimit = maxFallSpeed * motor.SpeedScale;
            if (maxFallSpeed > 0f && step.finalVelocity.y < -fallLimit)
            {
                Vector3 inertia = step.finalInertia;
                step.ForceMovementVector(new Vector3(step.finalMovementVector.x, fallLimit * step.deltaTime * -1f, step.finalMovementVector.z),
                    new Vector3(inertia.x, Mathf.Max(inertia.y, -fallLimit), inertia.z));
            }
        }

        /// <summary>How much of its air steering the character has after a wall jump: 0 at the jump, easing back to 1.</summary>
        private float WallJumpSteering(MMotionMotor motor)
        {
            WallJumpState jump = motor.wallJump;
            if (!jump.recovering || wallJumpSteeringRecovery <= 0f) { return 1f; }
            return Mathf.SmoothStep(0f, 1f, jump.elapsed / (wallJumpSteeringRecovery * motor.DurationScale));
        }

        /// <summary>
        /// Steers the character's horizontal momentum in the air. Rather than adding walking movement on top of the
        /// momentum carried off the ground (which would double the character's speed), the horizontal velocity is
        /// accelerated toward the steering input at walking speed (or running speed, while sprinting: see Walking &amp;
        /// Running). Any speed beyond that in the steering direction is kept (e.g. from a dash jump), and with no input
        /// the character slowly loses speed.
        /// </summary>
        /// <param name="steeringEffectiveness">How much control the character has right now (0 = none)</param>
        private void SteerInAir(MovementStep step, StepInputs inputs, MMotionMotor motor, float steeringEffectiveness)
        {
            Vector3 inertia = step.initialInertia;
            Vector2 velocity = new Vector2(inertia.x, inertia.z);
            Vector3 steer = step.lookFlat * inputs.movementVector;
            Vector2 steerDirection = new Vector2(steer.x, steer.z);
            float input = Mathf.Clamp01(steerDirection.magnitude);

            if (input > 0.001f && steeringEffectiveness > 0f)
            {
                steerDirection /= steerDirection.magnitude;
                float speedAlongInput = Vector2.Dot(velocity, steerDirection);
                Vector2 target = steerDirection * Mathf.Max(Mode.MoveSpeed(inputs, motor) * motor.SpeedScale * motor.speedMultiplier * input, speedAlongInput);
                velocity = Vector2.MoveTowards(velocity, target, airSteeringAcceleration * motor.AccelerationScale * steeringEffectiveness * step.deltaTime);
                step.targetRotation = Quaternion.Lerp(step.initialRotation, Quaternion.LookRotation(steer, Vector3.up), steeringEffectiveness);
            }
            else if (input <= 0.001f)
            {
                velocity = Vector2.MoveTowards(velocity, Vector2.zero, airDrag * motor.AccelerationScale * step.deltaTime);
            }

            step.initialInertia = new Vector3(velocity.x, inertia.y, velocity.y);
        }
    }
}
