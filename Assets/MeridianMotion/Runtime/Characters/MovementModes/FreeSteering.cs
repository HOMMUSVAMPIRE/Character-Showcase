using UnityEngine;

namespace MeridianMotion.Characters
{
    /// <summary>
    /// Steering free of the ground, in all three dimensions: swimming underwater now (see Swimming), and a way of moving
    /// through the air later. The movement input points the way the input looks (MovementStep.look: the player's camera,
    /// for the player), pitch and all (looking down and pushing forward heads down); holding Jump rises and Crouch sinks,
    /// whichever way it looks. The character turns
    /// (about the vertical) to face the way it heads across, and its body pitches along the way it heads, up or down, as
    /// far as a component allows (see MMotionMotor.BodyPitch). Like MovementRules, nothing here reads settings:
    /// the component using it passes its own speeds and limits.
    /// </summary>
    public static class FreeSteering
    {
        // Below this (of full input, squared), the movement input isn't steering.
        private const float SteeringThreshold = 1e-4f;

        /// <summary>
        /// The way the movement input steers, in the world, no longer than 1: the stick (flat, relative to the look) turned
        /// by the whole of the look, so its pitch tips it up or down.
        /// </summary>
        public static Vector3 Direction(MovementStep step, StepInputs inputs) =>
            Vector3.ClampMagnitude(step.look * inputs.movementVector, 1f);

        /// <summary>Whether the direction (see Direction) steers anywhere.</summary>
        public static bool IsSteering(Vector3 direction) => direction.sqrMagnitude >= SteeringThreshold;

        /// <summary>Up (1) while Jump is held, down (-1) while Crouch is, otherwise (or both) 0.</summary>
        public static float UpOrDown(StepInputs inputs) => (Held(inputs.jump) ? 1f : 0f) - (Held(inputs.crouch) ? 1f : 0f);

        private static bool Held(InputButton button) => button != null && button.held;

        /// <summary>
        /// The velocity (m/s) to head for: along the direction at speed (its length a share of full input); rising or
        /// sinking at verticalSpeed instead, whichever way the direction points, while upOrDown asks; and, with neither,
        /// rising at idleRise (a swimmer's buoyancy; negative sinks).
        /// </summary>
        public static Vector3 TargetVelocity(Vector3 direction, float upOrDown, float speed, float verticalSpeed, float idleRise)
        {
            Vector3 target = direction * speed;
            if (upOrDown != 0f) { target.y = upOrDown * verticalSpeed; }
            else if (!IsSteering(direction)) { target.y = idleRise; }
            return target;
        }

        /// <summary>Turns the character (about the vertical) to face the way the direction heads across, if it heads across at all.</summary>
        public static void FaceAcross(MovementStep step, Vector3 direction)
        {
            Vector3 across = new Vector3(direction.x, 0f, direction.z);
            if (across.sqrMagnitude > SteeringThreshold) { step.targetRotation = Quaternion.LookRotation(across.normalized, Vector3.up); }
        }

        /// <summary>
        /// How steeply (degrees, positive up) heading means climbing or diving: its angle above the horizontal, from -90
        /// (straight down) to 90 (straight up); 0 for no heading.
        /// </summary>
        public static float ClimbAngle(Vector3 heading)
        {
            float across = new Vector2(heading.x, heading.z).magnitude;
            return across < 1e-6f && Mathf.Abs(heading.y) < 1e-6f ? 0f : Mathf.Atan2(heading.y, across) * Mathf.Rad2Deg;
        }

        /// <summary>
        /// Pitches the character's body along heading (see ClimbAngle), up or down by no more than maxPitch degrees, for
        /// this step (see MovementStep.targetPitch: unless held so each step, the body levels out). Only along what of it
        /// the world let the character go last step: heading down onto a floor it's on (or up into a ceiling), it stays
        /// level that way. Otherwise its nose, pitched down into the floor ahead, was stepped up onto, which let it pitch
        /// further and lifted it, step by step, off a floor it was being steered into.
        /// </summary>
        public static void PitchAlong(MovementStep step, Vector3 heading, float maxPitch)
        {
            CollisionFlags blocked = step.previousCollisions;
            if ((heading.y < 0f && (blocked & CollisionFlags.Below) != 0) || (heading.y > 0f && (blocked & CollisionFlags.Above) != 0)) { heading.y = 0f; }
            float limit = Mathf.Clamp(maxPitch, 0f, 90f);
            step.targetPitch = Mathf.Clamp(ClimbAngle(heading), -limit, limit);
        }
    }
}
