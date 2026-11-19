using System;
using UnityEngine;
using static MeridianMotion.Characters.MovementRules;

namespace MeridianMotion.Characters
{
    /// <summary>
    /// Wall-running: running along a wall in mid-air, falling slowly, and jumping off it.
    /// </summary>
    [Serializable]
    public class WallRunning : MovementComponent
    {
        [Tooltip("How fast (m/s) the character runs along a wall, at its authored size. A wall run speeds up or slows " +
                 "down to it at the air acceleration.")]
        [SerializeField] private float wallRunSpeed = 9f;
        [Tooltip("A wall run starts only when moving along the wall at least this fast (m/s), and ends if something slows " +
                 "it to half that")]
        [SerializeField] private float wallRunMinSpeed = 4f;
        [Tooltip("The longest a wall run lasts, in seconds, before the character falls off. 0 turns wall-running off")]
        [SerializeField] private float wallRunMaxDuration = 1.2f;
        [Tooltip("How quickly (m/s²) the character falls during a wall run (instead of the gravity strength), at its " +
                 "authored size")]
        [SerializeField] private float wallRunGravity = 5f;
        [Tooltip("The fastest (m/s) the character can be rising as a wall run starts: a jump's upward speed is cut down to " +
                 "this, and a fall is caught (it starts at 0)")]
        [SerializeField] private float wallRunMaxRiseSpeed = 4f;
        [Tooltip("How far (degrees) from along the wall the steering can point for a wall run to start: a wall run is " +
                 "asked for, by steering along the wall, not something the character falls into heading at it")]
        [Range(0f, 90f)] [SerializeField] private float wallRunMaxSteerAngle = 35f;
        [Tooltip("How far (m) beside the character a wall can be for it to run along it, at its authored size (a body " +
                 "longer than it is wide, as much further as its length reaches beyond its side)")]
        [SerializeField] private float wallRunCheckDistance = 0.3f;
        [Tooltip("How far from the wall the character's middle (its feet, the point it stands on) is held as it runs along " +
                 "one, as a share of its standing height, at least as far as its body reaches to that side: a wall runner " +
                 "leans out from the wall, its feet on it. Less than the body's reach holds it against the wall")]
        [Range(0f, 1f)] [SerializeField] private float wallRunClearance = 0.25f;
        [Tooltip("A wall run only starts this high (m) or more above the ground, at the character's authored size, so " +
                 "brushing a wall in a small hop doesn't start one")]
        [SerializeField] private float wallRunMinHeight = 0.75f;
        [Tooltip("The steepest a wall can lean (degrees from vertical) to be run along")]
        [Range(0f, 45f)] [SerializeField] private float wallRunMaxWallTilt = 20f;
        [Tooltip("Seconds after a wall run ends (or a wall jump) before another can start")]
        [SerializeField] private float wallRunCooldown = 0.35f;
        [Tooltip("How high (above the character's feet, as a share of its standing height) a wall has to reach down to, for " +
                 "the feet to run on it: a wall run doesn't start, or ends, once the feet are below the wall's bottom edge")]
        [SerializeField] private float wallRunFootHeight = 0.2f;
        [Tooltip("Seconds the character has to keep steering away from the wall (or stop steering along it) before it lets " +
                 "go of a wall run, so turning toward the next wall to jump to doesn't drop it off this one. Jumping is at once")]
        [Min(0f)] [SerializeField] private float wallRunReleaseDelay = 0.25f;
        [Tooltip("How fast (m/s) a wall jump pushes the character away from the wall")]
        [SerializeField] private float wallJumpAwaySpeed = 10f;
        [Tooltip("How fast (m/s) a wall jump launches the character upward")]
        [SerializeField] private float wallJumpUpSpeed = 10f;

        // The tightest a wall can curve (its radius, m, at the authored size) and still be run along: where its facing
        // turns faster than that for the distance run, it's an edge or a corner (the end of the wall), and the run ends
        // there. Without this the run carried on round the end of a wall (the sweep finding its corner), once steering
        // off the wall no longer ended it at once (see wallRunReleaseDelay).
        private const float WallRunMinCurveRadius = 2f;
        // Seconds (at the authored size) over which a wall run closes in on, or eases out to, how far from the wall it's
        // held (see wallRunClearance); and the fastest (m/s, at the authored size) it eases out.
        private const float WallRunHoldTime = 0.1f, WallRunMaxEaseOut = 4f;
        // The furthest out a wall run is held from the wall, as a share of the check distance (beyond which it's lost).
        private const float WallRunMaxHoldShare = 0.75f;

        /// <summary>How fast (m/s) the character runs along a wall, at its authored size.</summary>
        public float WallRunSpeed { get => wallRunSpeed; set => wallRunSpeed = value; }
        /// <summary>The longest a wall run lasts, in seconds. 0 turns wall-running off.</summary>
        public float WallRunMaxDuration { get => wallRunMaxDuration; set => wallRunMaxDuration = value; }
        /// <summary>The steepest a wall can lean (degrees from vertical) to be run along (and slid down: see WallSliding).</summary>
        public float WallRunMaxWallTilt => wallRunMaxWallTilt;

        /// <summary>The events of a moment it raises (see MovementComponent.OneOffEvents): a jump off the wall (MovementRules.JumpOffWall).</summary>
        public override MovementEvents OneOffEvents => MovementEvents.WallJumped;
        public override bool IsActive(MMotionMotor motor) => motor.wallRun.IsWallRunning;
        /// <summary>Running along it, the wall holds the character up.</summary>
        public override AbilityTraits Traits => AbilityTraits.Supported;
        /// <summary>The events of WallRunning starting and ending (see MovementComponent.StateEvents).</summary>
        public override StateEventPair[] StateEvents => stateEvents;
        private static readonly StateEventPair[] stateEvents =
        {
            new(CharacterState.WallRunning, MovementEvents.WallRunStarted, MovementEvents.WallRunEnded),
        };

        public override void AdvanceTimers(MovementStep step, StepInputs inputs, MMotionMotor motor)
        {
            motor.wallRun.cooldownRemaining = MovementRules.CountDown(motor.wallRun.cooldownRemaining, step.deltaTime);
            if (step.initialGrounded) { motor.wallRun.hasLastWall = false; }
        }

        public override void Interrupt(MovementStep step, MMotionMotor motor)
        {
            if (motor.wallRun.IsWallRunning) { EndWallRun(motor); }
        }

        /// <summary>
        /// Starts a wall run when the character, in mid-air and high enough above the ground, moves fast enough along a
        /// wall beside it (reaching down to its feet) while steering along it, and carries it on: the character runs
        /// along the wall, falling slowly, until the wall ends (or its feet drop below the wall's bottom edge), the run
        /// lasts its longest, it's slowed down, steering has stopped pointing along the wall (or pointed away from it)
        /// for the release delay, Crouch is pressed, or it lands. Jumping jumps off the wall, away from it and upward,
        /// gives back the character's air jumps, and takes away air steering for a moment. The same wall can't be run along twice without touching the ground
        /// in between (another wall can, after a short cooldown: wall jumps can chain between facing walls).
        /// </summary>
        public override bool TakeOver(MovementStep step, StepInputs inputs, MMotionMotor motor)
        {
            WallRunState run = motor.wallRun;
            if (!run.IsWallRunning && !TryStartWallRun(step, inputs, motor)) { return false; }

            Vector3 feet = step.initialPosition;
            if (step.initialGrounded || !FindWall(motor, feet, -run.wallNormal, out Vector3 normal, out _, out Vector3 wallPoint)
                || !WallReachesFeet(motor, feet, normal))
            {
                EndWallRun(motor);
                return false;
            }
            // Along the wall, at the speed the last step left the character with, so anything in the way slows it down.
            Vector3 inertia = step.initialInertia;
            float turn = Vector3.Angle(normal, run.wallNormal);
            float maxTurn = new Vector2(inertia.x, inertia.z).magnitude * step.deltaTime
                            / (WallRunMinCurveRadius * motor.HeightScale) * Mathf.Rad2Deg + 1f;
            if (turn > maxTurn)
            {
                EndWallRun(motor); // Round an edge or corner: the wall has ended.
                return false;
            }
            run.wallNormal = normal;
            run.wallPoint = wallPoint;
            Vector3 along = Vector3.ProjectOnPlane(new Vector3(inertia.x, 0f, inertia.z), normal);
            along.y = 0f;
            float speed = along.magnitude;
            Vector3 tangent = speed > 1e-3f ? along / speed : Vector3.zero;
            Vector3 steer = InputDirection(step, inputs);
            run.elapsed += step.deltaTime;

            // Steering off the run (not along the wall, or away from it) only lets go once it's lasted the release delay.
            bool steeringOff = Vector3.Dot(steer, tangent) < 0.1f || Vector3.Dot(steer, normal) > 0.5f;
            run.releaseTime = steeringOff ? run.releaseTime + step.deltaTime : 0f;
            bool ended = speed < 0.5f * wallRunMinSpeed * motor.SpeedScale
                         || run.elapsed > wallRunMaxDuration * motor.DurationScale
                         || (steeringOff && run.releaseTime >= wallRunReleaseDelay)
                         || Mode.ConsumeCrouchPress(inputs, motor);
            if (ended)
            {
                EndWallRun(motor);
                return false;
            }

            if (Mode.HasFreshJumpPress(inputs, motor))
            {
                WallJump(step, motor, tangent * speed, normal);
                return true;
            }

            // Run along the wall, falling slowly, pressed lightly against it, or held its clearance out from it.
            speed = Mathf.MoveTowards(speed, wallRunSpeed * motor.SpeedScale, Mode.AirAcceleration(motor) * step.deltaTime);
            float rise = inertia.y - wallRunGravity * motor.AccelerationScale * step.deltaTime;
            step.velocityOverride = tangent * speed + Vector3.up * rise - normal * TowardWall(motor, AlongsideGap(motor, feet, wallPoint, normal));
            step.directMovementVector = Vector3.zero;
            step.targetRotation = Quaternion.LookRotation(tangent, Vector3.up);
            // The wall is on the character's right if its normal points to the character's left.
            run.side = Vector3.Dot(Vector3.Cross(Vector3.up, tangent), normal) < 0f ? 1f : -1f;
            step.state |= CharacterState.WallRunning | CharacterState.Moving;
            return true;
        }

        /// <summary>
        /// Starts a wall run if the character is in mid-air, high enough above the ground, moving fast enough along a wall
        /// beside it (not straight into it), and steering along the wall (within the steering angle), the way it's moving.
        /// </summary>
        private bool TryStartWallRun(MovementStep step, StepInputs inputs, MMotionMotor motor)
        {
            WallRunState run = motor.wallRun;
            if (wallRunMaxDuration <= 0f || step.initialGrounded || run.cooldownRemaining > 0f
                || motor.crouch.IsCrouched || Mode.AbilityInProgress(motor))
            {
                return false;
            }

            Vector3 inertia = step.initialInertia;
            Vector3 horizontal = new Vector3(inertia.x, 0f, inertia.z);
            float speed = horizontal.magnitude;
            if (speed < wallRunMinSpeed * motor.SpeedScale) { return false; }
            Vector3 direction = horizontal / speed;
            Vector3 steer = InputDirection(step, inputs);
            if (steer.sqrMagnitude < 0.25f || Vector3.Dot(steer, direction) < 0.1f) { return false; }

            // The nearer of a wall on the right and one on the left.
            Vector3 feet = step.initialPosition;
            Vector3 right = Vector3.Cross(Vector3.up, direction);
            bool onRight = FindWall(motor, feet, right, out Vector3 rightNormal, out float rightDistance, out Vector3 rightPoint);
            bool onLeft = FindWall(motor, feet, -right, out Vector3 leftNormal, out float leftDistance, out Vector3 leftPoint);
            if (!onRight && !onLeft) { return false; }
            bool useRight = onRight && (!onLeft || rightDistance <= leftDistance);
            Vector3 normal = useRight ? rightNormal : leftNormal;
            // Within reach of the wall as the body will be running along it, its side to the wall (see Reach): not only by
            // a corner of a long body turned toward the wall, coming at it at an angle. (A fox's box reached the wall with
            // its front corner, started a run, turned along the wall, and found it out of reach, ending the run in the next
            // step.)
            if (AlongsideGap(motor, feet, useRight ? rightPoint : leftPoint, normal) > Reach(motor)) { return false; }
            if (run.hasLastWall && Vector3.Angle(normal, run.lastWallNormal) < SameWallAngle) { return false; }
            if (!WallReachesFeet(motor, feet, normal)) { return false; }

            // Running along the wall, not into it: within 60 degrees of it; and steering along it, the way it's moving,
            // within the steering angle of it.
            Vector3 along = Vector3.ProjectOnPlane(direction, normal);
            along.y = 0f;
            if (along.magnitude < 0.5f) { return false; }
            if (Vector3.Angle(steer, along) > wallRunMaxSteerAngle) { return false; }

            // High enough above the ground.
            float minHeight = wallRunMinHeight * motor.HeightScale;
            if (motor.Raycast(feet + Vector3.up * motor.Skin, Vector3.down, minHeight + motor.Skin, out _)) { return false; }

            run.IsWallRunning = true;
            run.elapsed = 0f;
            // Running on a wall counts as touching the ground: the character's jumps and air dashes come back. (The same
            // wall can't be run along twice before landing, so this can't be repeated on one wall.)
            GiveBackAirMoves(motor);
            run.releaseTime = 0f;
            run.wallNormal = normal;
            // A rising jump is cut down, and a fall caught.
            float rise = Mathf.Clamp(inertia.y, 0f, wallRunMaxRiseSpeed * motor.SpeedScale);
            step.initialInertia = new Vector3(inertia.x, rise, inertia.z);
            return true;
        }

        /// <summary>
        /// How far (m) out from the wall, beyond the body's side, the character is held as it runs along one: the clearance
        /// (a share of its standing height) less how far its body reaches to the side; 0 if that's less (it's pressed
        /// against the wall). No more than most of the check distance, so the wall it's held out from is still found.
        /// </summary>
        private float HoldGap(MMotionMotor motor) =>
            Mathf.Clamp(wallRunClearance * motor.Body.StandingHeight - motor.Body.SideExtent, 0f,
                WallRunMaxHoldShare * wallRunCheckDistance * motor.HeightScale);

        /// <summary>
        /// How far (m) beside the body a wall can be to run along it: the check distance, and for a body longer than it is
        /// wide (a fox's box), as much further as its length reaches beyond its side. Coming at a wall steeply, such a body
        /// meets it with a corner while its side is still that far off it; it starts a run there, turns along the wall,
        /// and is drawn in to its clearance (see TowardWall), the wall kept within reach as it is. (A capsule reaches as
        /// far ahead as to the side: no further.)
        /// </summary>
        private float Reach(MMotionMotor motor)
        {
            CharacterBody body = motor.Body;
            return wallRunCheckDistance * motor.HeightScale + Mathf.Max(0f, Mathf.Max(body.FrontExtent, body.BackExtent) - body.SideExtent);
        }

        /// <summary>
        /// How far (m) the body's side is from the wall (through point, its normal given) as it runs along it, alongside
        /// it: the character's middle's distance from the wall's face, less how far its body reaches to the side. Not the
        /// distance a sweep of the body finds, turned as it is: a long body coming at the wall at an angle reaches it with a
        /// corner, and held out by that, it was pushed off the wall as it turned along it. (For a body that reaches as far
        /// to the side however it's turned, a capsule, they're the same.)
        /// </summary>
        private static float AlongsideGap(MMotionMotor motor, Vector3 feet, Vector3 wallPoint, Vector3 normal) =>
            Vector3.Dot(feet - wallPoint, normal) - motor.Body.SideExtent;

        /// <summary>
        /// How fast (m/s) to move toward the wall this step, the body's side gap (m) from it: pressed lightly against it,
        /// or, held out from it (see HoldGap), closing in on that gap (or easing out to it) over the hold time.
        /// </summary>
        private float TowardWall(MMotionMotor motor, float gap)
        {
            float stick = WallStickSpeed * motor.SpeedScale;
            float hold = HoldGap(motor);
            if (hold <= motor.Skin) { return stick; }
            return Mathf.Clamp((gap - hold) / (WallRunHoldTime * motor.DurationScale), -WallRunMaxEaseOut * motor.SpeedScale, stick);
        }

        /// <summary>
        /// Whether there's a wall to run along beside the character in the given direction (within the check distance,
        /// and upright enough), and its normal, flattened (pointing away from the wall).
        /// </summary>
        private bool FindWall(MMotionMotor motor, Vector3 feet, Vector3 direction, out Vector3 normal, out float distance) =>
            FindWall(motor, feet, direction, out normal, out distance, out _);

        /// <summary>The same, and the point on the wall the body's sweep hit (distance includes the skin width).</summary>
        private bool FindWall(MMotionMotor motor, Vector3 feet, Vector3 direction, out Vector3 normal, out float distance, out Vector3 point)
        {
            normal = Vector3.zero;
            distance = 0f;
            point = Vector3.zero;
            float reach = Reach(motor) + motor.Skin;
            if (!MovementRules.FindUprightWall(motor, feet, direction, reach, wallRunMaxWallTilt, out RaycastHit hit, out normal)) { return false; }
            // Facing the character, not a surface it would only graze.
            if (Vector3.Dot(normal, -direction.normalized) < 0.5f) { return false; }
            distance = hit.distance;
            point = hit.point;
            return true;
        }

        /// <summary>
        /// Whether the wall with the given normal reaches down to the character's feet (to the wall run's foot height
        /// above them), for them to run on: the capsule sweep that finds the wall also finds one beside its upper body,
        /// and the character kept running along a wall with its feet below the bottom edge.
        /// </summary>
        private bool WallReachesFeet(MMotionMotor motor, Vector3 feet, Vector3 normal)
        {
            float reach = motor.Body.SideExtent + Reach(motor) + 2f * motor.Skin;
            return motor.Raycast(feet + Vector3.up * (wallRunFootHeight * motor.Body.StandingHeight), -normal, reach, out _);
        }

        /// <summary>
        /// Jumps off the wall: away from it and upward, keeping the run's speed along it. Gives back the character's
        /// jumps, less the one this uses, and takes away air steering, which comes back over the mode's wall jump steering recovery time.
        /// </summary>
        private void WallJump(MovementStep step, MMotionMotor motor, Vector3 alongWall, Vector3 normal)
        {
            EndWallRun(motor);
            Vector3 velocity = alongWall + normal * (wallJumpAwaySpeed * motor.SpeedScale) + Vector3.up * (wallJumpUpSpeed * motor.SpeedScale);
            // Facing the way it's launched.
            MovementRules.JumpOffWall(step, motor, velocity, normal, new Vector3(velocity.x, 0f, velocity.z));
        }

        private void EndWallRun(MMotionMotor motor)
        {
            WallRunState run = motor.wallRun;
            run.IsWallRunning = false;
            run.cooldownRemaining = wallRunCooldown;
            run.side = 0f;
            run.lastWallNormal = run.wallNormal;
            run.hasLastWall = true;
        }

        /// <summary>
        /// How far a wall run carries the character along a wall, and how far it falls over one, if it runs on walls (a
        /// run is started by jumping at a wall, so not without jumps).
        /// </summary>
        public override void MeasureCapabilities(MovementSimulation simulation, MovementCapabilities capabilities)
        {
            if (wallRunMaxDuration <= 0f || Mode.Get<Jumping>() is not { MaxJumpHeight: > 0f, MaxJumps: > 0 }) { return; }
            (float length, float drop) = WallRun(simulation);
            capabilities.WallRunLength = length;
            capabilities.WallRunDrop = drop;
        }

        /// <summary>
        /// How far (m) along a wall a full wall run carries the character, and how far (m) it falls over one: sprinting
        /// along a long wall and jumping, steering into it, from a few distances out from it and steering at a few angles,
        /// so it meets the wall at different points of the jump. The length is the longest run; the drop is the largest
        /// fall, which is a run's started falling or level (a fall is caught as a run starts and it then sinks slowly;
        /// one started rising carries on up for a while). NaN if no way starts one.
        /// </summary>
        private (float length, float drop) WallRun(MovementSimulation simulation)
        {
            float length = float.NaN, drop = float.NaN;
            foreach (float outFromWall in new[] { 0.1f, 1f, 2f, 3f })
            {
                foreach (float steer in new[] { 10f, 20f, 30f })
                {
                    simulation.ClearObstacles();
                    simulation.Reset(Vector3.zero);
                    WallBeside(simulation, outFromWall);
                    Vector3 input = Quaternion.Euler(0f, steer, 0f) * Vector3.forward;
                    simulation.Run(MovementSimulation.StepsFor(MovementSimulation.MeasureRunUp), _ => MovementSimulation.Move(Vector3.forward, true));
                    MeasureRun(simulation, i =>
                    {
                        StepInputs inputs = MovementSimulation.Move(input, true);
                        inputs.jump = MovementSimulation.Held(i);
                        return inputs;
                    }, groundedFrom: 3, ref length, ref drop);
                }
            }
            // And one started level: in mid-air beside the wall, well above the ground, moving along it at the run speed.
            // A run started level (or falling: a fall is caught) sinks the furthest: its drop is the run's.
            {
                simulation.ClearObstacles();
                simulation.Reset(new Vector3(0f, 4f * simulation.Motor.Body.StandingHeight, 0f));
                WallBeside(simulation, 0.1f);
                simulation.Motor.OverrideVelocity(Vector3.forward * (SettingsOf<WalkingAndRunning>().RunSpeed * simulation.Motor.SpeedScale));
                Vector3 input = Quaternion.Euler(0f, 10f, 0f) * Vector3.forward;
                MeasureRun(simulation, _ => MovementSimulation.Move(input, true), groundedFrom: 0, ref length, ref drop);
            }
            simulation.ClearObstacles();
            return (length, drop);
        }

        /// <summary>A long, tall wall along the character's right, its face outFromWall (m) beyond the character's side.</summary>
        private static void WallBeside(MovementSimulation simulation, float outFromWall)
        {
            float face = simulation.Motor.Body.SideExtent + outFromWall;
            simulation.Box(new Vector3(face + 0.5f, 15f, 200f), new Vector3(1f, 30f, 420f));
        }

        /// <summary>
        /// Steps the simulation with the given inputs until a wall run starts and ends (keeping the longest run along the
        /// wall and the largest fall over one seen so far in length and drop), or it gives up: on the ground from step
        /// groundedFrom on (before it, it may still be taking off), or after the measuring timeout.
        /// </summary>
        private static void MeasureRun(MovementSimulation simulation, Func<int, StepInputs> inputsFor, int groundedFrom, ref float length, ref float drop)
        {
            float startZ = float.NaN, startY = float.NaN;
            for (int i = 0; i < MovementSimulation.StepsFor(MovementSimulation.MeasureTimeout); i++)
            {
                Vector3 before = simulation.Position;
                simulation.Step(inputsFor(i));
                bool running = simulation.State.HasAny(CharacterState.WallRunning);
                if (running && float.IsNaN(startZ)) { startZ = before.z; startY = before.y; }
                if (!running && !float.IsNaN(startZ))
                {
                    float run = simulation.Position.z - startZ, fall = startY - simulation.Position.y;
                    if (float.IsNaN(length) || run > length) { length = run; }
                    if (float.IsNaN(drop) || fall > drop) { drop = fall; }
                    return;
                }
                if (simulation.Grounded && i >= groundedFrom) { return; }
            }
        }
    }

    /// <summary>A wall run in progress, and the cooldown before the next.</summary>
    public class WallRunState
    {
        /// <summary>Whether the character is running along a wall.</summary>
        public bool IsWallRunning;
        /// <summary>Seconds since the wall run started.</summary>
        public float elapsed;
        /// <summary>How long, in seconds, the character has been steering off the wall (see the release delay).</summary>
        public float releaseTime;
        /// <summary>Seconds until another wall run may start.</summary>
        public float cooldownRemaining;
        /// <summary>The wall's normal (horizontal, unit length, pointing away from the wall toward the character).</summary>
        public Vector3 wallNormal;
        /// <summary>A point on the wall's face, where the character's body was nearest it (for the feet on it: see WallContactIK).</summary>
        public Vector3 wallPoint;
        /// <summary>Which side of the character the wall is on: 1 = right, -1 = left (0 when not wall-running).</summary>
        public float side;
        /// <summary>The wall last run along (its normal), which can't be run along again until the character touches the ground.</summary>
        public Vector3 lastWallNormal;
        /// <summary>Whether lastWallNormal holds a wall.</summary>
        public bool hasLastWall;
    }

    // This state's place on the character (see MMotionMotor): declared here, beside its ability, so a new
    // ability adds its state in its own file rather than in MMotionMotor.cs.
    public partial class MMotionMotor
    {
        /// <summary>The character's wall running state (WallRunning).</summary>
        public WallRunState wallRun { get; } = new WallRunState();
    }
}
