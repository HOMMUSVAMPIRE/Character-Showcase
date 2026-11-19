using UnityEngine;

namespace MeridianMotion.Characters
{
    /// <summary>
    /// Small pieces of movement maths and rules shared by movement modes and their components (see MovementComponent):
    /// nothing here reads settings, and what it changes is the character's own state (on its MMotionMotor).
    /// </summary>
    public static class MovementRules
    {
        /// <summary>
        /// A vector flattened onto the ground plane (y dropped) and normalized: false, leaving it unnormalized, if it's
        /// nearly vertical, with no direction across the ground to speak of.
        /// </summary>
        public static bool TryFlatten(Vector3 vector, out Vector3 flat)
        {
            flat = new Vector3(vector.x, 0f, vector.z);
            if (flat.sqrMagnitude < 1e-6f) { return false; }
            flat.Normalize();
            return true;
        }

        /// <summary>
        /// Sweeps the body from feet in direction, up to reach, for a wall: something upright (its normal within
        /// maxTiltDegrees of level, either way), as a wall run or a slide down a wall needs. Gives the hit and the wall's
        /// normal, flattened (pointing away from the wall: see TryFlatten). False if nothing upright is in reach.
        /// </summary>
        public static bool FindUprightWall(MMotionMotor motor, Vector3 feet, Vector3 direction, float reach,
            float maxTiltDegrees, out RaycastHit hit, out Vector3 normal)
        {
            normal = Vector3.zero;
            if (!motor.SweepBody(feet, direction, reach, out hit)) { return false; }
            if (Mathf.Abs(hit.normal.y) > Mathf.Sin(maxTiltDegrees * Mathf.Deg2Rad)) { return false; }
            return TryFlatten(hit.normal, out normal);
        }

        // How close (as a share of a physics step) a timer must come to its end to count as there: see CountDown.
        private const float TimerTolerance = 1e-3f;

        /// <summary>
        /// A countdown (a duration or a cooldown, in seconds) one physics step on, never below 0. Subtracting a step's
        /// time over and over from a duration that's a whole number of steps can leave a sliver rather than 0 (0.2 s less
        /// ten 0.02 s steps isn't exactly 0 in floating point), which made what it times last a step longer than it was
        /// set to (a 0.2 s dash took 11 steps); within a thousandth of a step of 0 counts as run out.
        /// </summary>
        public static float CountDown(float remaining, float deltaTime)
        {
            float left = remaining - deltaTime;
            return left > deltaTime * TimerTolerance ? left : 0f;
        }

        /// <summary>
        /// Whether a timer counting up has reached a duration, allowing for the same floating point slivers as
        /// CountDown (ten 0.02 s steps can add up to just under 0.2 s, which would take a step more).
        /// </summary>
        public static bool HasElapsed(float elapsed, float duration, float deltaTime) =>
            elapsed >= duration - deltaTime * TimerTolerance;

        /// <summary>The movement input in world space, relative to the input's look (its yaw): horizontal, and no longer than 1.</summary>
        public static Vector3 InputDirection(MovementStep step, StepInputs inputs)
        {
            Vector3 direction = step.lookFlat * inputs.movementVector;
            direction.y = 0f;
            return Vector3.ClampMagnitude(direction, 1f);
        }

        /// <summary>The way the character faces, flattened onto the ground (unit length).</summary>
        public static Vector3 Facing(MovementStep step)
        {
            Vector3 forward = step.initialRotation * Vector3.forward;
            forward.y = 0f;
            return forward.sqrMagnitude > 1e-6f ? forward.normalized : Vector3.forward;
        }

        /// <summary>
        /// How many jumps the character gets before landing again: its movement mode's Jumping's Max Jumps, or none if it
        /// has no Jumping turned on.
        /// </summary>
        public static int MaxJumps(MMotionMotor motor) =>
            motor.movementMode is AbilityMovementMode mode && mode.Get<Jumping>() is { } jumping ? jumping.MaxJumps : 0;

        /// <summary>Gives back all of the character's jumps (see MaxJumps), as touching the ground does.</summary>
        public static void GiveBackJumps(MMotionMotor motor) => motor.jump.remaining = MaxJumps(motor);

        /// <summary>
        /// Uses up one of the character's jumps, if it has one left, returning whether it did (and noting when, for
        /// MMotionMotor.HasJumpedSinceGrounded). Jumps off walls, ropes and ziplines use one up too.
        /// </summary>
        public static bool TryUseJump(MMotionMotor motor)
        {
            JumpState jump = motor.jump;
            if (jump.remaining <= 0) { return false; }
            jump.remaining--;
            jump.lastJumpTick = motor.StepIndex;
            return true;
        }

        /// <summary>
        /// Jumps the character off whatever was holding it (a rope, a zipline, a wall): uses up the press of Jump, gives
        /// back its jumps less the one this uses (unless givesBackJumps is false), and launches it at the given velocity,
        /// outright, with no walking on top, raising the given event (Jumped, or WallJumped).
        /// </summary>
        public static void JumpOff(MovementStep step, MMotionMotor motor, Vector3 velocity,
            MovementEvents jumpEvent, bool givesBackJumps = true)
        {
            motor.jump.press.Use();
            if (givesBackJumps)
            {
                GiveBackJumps(motor);
                TryUseJump(motor);
            }
            step.velocityOverride = velocity;
            step.directMovementVector = Vector3.zero;
            step.events |= jumpEvent;
        }

        /// <summary>
        /// Jumps the character off a wall (a wall run, a climb, a slide down it), as JumpOff does, facing the given way
        /// (if there is one): it also remembers the wall (see JumpedOffThisWallLast) and starts air steering coming back
        /// gradually (see StartWallJumpSteeringRecovery), so the push away carries the character off the wall.
        /// </summary>
        public static void JumpOffWall(MovementStep step, MMotionMotor motor, Vector3 velocity, Vector3 wallNormal,
            Vector3 facing, bool givesBackJumps = true)
        {
            JumpOff(step, motor, velocity, MovementEvents.WallJumped, givesBackJumps);
            RememberWallJumpedOff(motor, wallNormal);
            StartWallJumpSteeringRecovery(motor);
            if (facing.sqrMagnitude > 1e-6f) { step.targetRotation = Quaternion.LookRotation(facing, Vector3.up); }
            step.state |= CharacterState.Moving;
        }

        /// <summary>
        /// Gives back the character's jumps and air dashes, as touching the ground does. For the abilities that count as
        /// touching it (starting a wall run, taking hold of a climbable surface, a ledge, a rope or a zipline, vaulting), and
        /// for a kick off a wall slid down that gives them back (off a different wall from the last one jumped off).
        /// </summary>
        public static void GiveBackAirMoves(MMotionMotor motor)
        {
            GiveBackJumps(motor);
            motor.dash.airDashesUsed = 0;
        }

        /// <summary>
        /// What catching hold of a rope or a zipline does besides: it counts as touching the ground (the character's jumps
        /// and air dashes come back), it ends whatever else had the character (a dash, or a slide down a wall: see
        /// AbilityMovementMode.InterruptOthers), and it's done standing (a character crouched in the air stands).
        /// </summary>
        public static void AttachToLine(MovementStep step, MMotionMotor motor, MovementComponent holder)
        {
            GiveBackAirMoves(motor);
            if (holder.Mode != null) { holder.Mode.InterruptOthers(step, motor, holder); }
            motor.crouch.IsCrouched = false;
            motor.crouch.wantsToStand = false;
        }

        /// <summary>
        /// A wall facing within this many degrees of the last one run along, slid down or jumped off counts as the same
        /// wall, which can't be run along (or slid down, or given jumps back by) again before touching the ground (so a
        /// wall run's longest time means something).
        /// </summary>
        public const float SameWallAngle = 30f;

        /// <summary>
        /// How fast (m/s, at the authored size) the character is pressed toward a wall it's running along or sliding down,
        /// so it stays against it as the wall curves. Collision removes it each step, so it never builds up.
        /// </summary>
        public const float WallStickSpeed = 1f;

        /// <summary>
        /// Takes away air steering after a jump off a wall (a wall run, a slide down a wall, a climb), to come back over
        /// the mode's wall jump steering recovery time: otherwise steering would cancel the push away from the wall at
        /// once, back onto it. Also counted as leaving the ground, so the air steering curve starts again too.
        /// </summary>
        public static void StartWallJumpSteeringRecovery(MMotionMotor motor)
        {
            motor.lastGroundedTick = motor.StepIndex;
            motor.wallJump.recovering = true;
            motor.wallJump.elapsed = 0f;
        }

        /// <summary>Whether the wall with this normal is the last one jumped off (from a wall run, slide or climb) since landing.</summary>
        public static bool JumpedOffThisWallLast(MMotionMotor motor, Vector3 normal) =>
            motor.wallJump.hasLastWall && Vector3.Angle(normal, motor.wallJump.lastWallNormal) < SameWallAngle;

        /// <summary>Remembers the wall with this normal as the last one jumped off.</summary>
        public static void RememberWallJumpedOff(MMotionMotor motor, Vector3 normal)
        {
            motor.wallJump.lastWallNormal = normal;
            motor.wallJump.hasLastWall = true;
        }

        /// <summary>
        /// The upward launch speed that rises height metres under gravity g, moving in physics steps of deltaTime. Each
        /// step moves at the step's speed, and gravity takes effect from the step after the launch, so a launch at
        /// sqrt(2gh) rises about half a step's travel too high; this solves for the stepped rise, h = v²/2g + v·dt/2.
        /// </summary>
        public static float LaunchSpeedFor(float height, float g, float deltaTime)
        {
            if (g <= 0f) { return 0f; }
            float half = 0.5f * g * deltaTime;
            return -half + Mathf.Sqrt(half * half + 2f * g * height);
        }

        /// <summary>
        /// The square of the distance between two line segments (p1 to q1, and p2 to q2), at their closest points: how far
        /// along each (0 to 1) those are, in s and t. (Ericson, Real-Time Collision Detection, 5.1.9.)
        /// </summary>
        public static float SqrDistanceBetweenSegments(Vector3 p1, Vector3 q1, Vector3 p2, Vector3 q2, out float s, out float t)
        {
            Vector3 d1 = q1 - p1, d2 = q2 - p2, r = p1 - p2;
            float a = Vector3.Dot(d1, d1), e = Vector3.Dot(d2, d2), f = Vector3.Dot(d2, r);
            const float Epsilon = 1e-8f;
            if (a <= Epsilon && e <= Epsilon)
            {
                s = t = 0f;
                return r.sqrMagnitude;
            }
            if (a <= Epsilon)
            {
                s = 0f;
                t = Mathf.Clamp01(f / e);
            }
            else
            {
                float c = Vector3.Dot(d1, r);
                if (e <= Epsilon)
                {
                    t = 0f;
                    s = Mathf.Clamp01(-c / a);
                }
                else
                {
                    float b = Vector3.Dot(d1, d2), denominator = a * e - b * b;
                    s = denominator > Epsilon ? Mathf.Clamp01((b * f - c * e) / denominator) : 0f;
                    t = (b * s + f) / e;
                    if (t < 0f) { t = 0f; s = Mathf.Clamp01(-c / a); }
                    else if (t > 1f) { t = 1f; s = Mathf.Clamp01((b - c) / a); }
                }
            }
            return (p1 + d1 * s - (p2 + d2 * t)).sqrMagnitude;
        }
    }

    /// <summary>
    /// Air steering coming back after a jump off a wall (a wall run or a wall slide), and the last wall jumped off since
    /// landing (a kick off a wall slid down gives back air jumps only off a different one).
    /// </summary>
    public class WallJumpState
    {
        /// <summary>Whether air steering is still coming back after a jump off a wall (cleared on landing).</summary>
        public bool recovering;
        /// <summary>Seconds since the jump.</summary>
        public float elapsed;
        /// <summary>The normal of the last wall run along, slid down or jumped off since landing (see SameWallAngle).</summary>
        public Vector3 lastWallNormal;
        /// <summary>Whether lastWallNormal holds a wall (cleared on landing).</summary>
        public bool hasLastWall;
    }

    // This state's place on the character (see MMotionMotor): declared here, beside its ability, so a new
    // ability adds its state in its own file rather than in MMotionMotor.cs.
    public partial class MMotionMotor
    {
        /// <summary>The state shared by the wall jumps of every ability that wall jumps (see MovementRules).</summary>
        public WallJumpState wallJump { get; } = new WallJumpState();
    }
}
