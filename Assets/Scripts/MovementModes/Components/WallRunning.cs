using System;
using UnityEngine;
using static Character.MovementHelpers;

namespace Character
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
        [Tooltip("How far (m) beside the character a wall can be for it to run along it, at its authored size")]
        [SerializeField] private float wallRunCheckDistance = 0.3f;
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

        /// <summary>How fast (m/s) the character runs along a wall, at its authored size.</summary>
        public float WallRunSpeed { get => wallRunSpeed; set => wallRunSpeed = value; }
        /// <summary>The longest a wall run lasts, in seconds. 0 turns wall-running off.</summary>
        public float WallRunMaxDuration { get => wallRunMaxDuration; set => wallRunMaxDuration = value; }
        /// <summary>The steepest a wall can lean (degrees from vertical) to be run along (and slid down: see WallSliding).</summary>
        public float WallRunMaxWallTilt => wallRunMaxWallTilt;

        public override bool IsActive(MovementController controller) => controller.wallRun.IsWallRunning;

        public override void AdvanceTimers(ref MovementData movementData, Inputs inputs, MovementController controller)
        {
            controller.wallRun.cooldownRemaining = MovementHelpers.CountDown(controller.wallRun.cooldownRemaining, movementData.deltaTime);
            if (movementData.initialGrounded) { controller.wallRun.hasLastWall = false; }
        }

        public override void Interrupt(ref MovementData movementData, MovementController controller)
        {
            if (controller.wallRun.IsWallRunning) { EndWallRun(controller); }
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
        public override bool TakeOver(ref MovementData movementData, Inputs inputs, MovementController controller)
        {
            WallRunState run = controller.wallRun;
            if (!run.IsWallRunning && !TryStartWallRun(ref movementData, inputs, controller)) { return false; }

            Vector3 feet = movementData.initialPosition;
            if (movementData.initialGrounded || !FindWall(controller, feet, -run.wallNormal, out Vector3 normal, out _)
                || !WallReachesFeet(controller, feet, normal))
            {
                EndWallRun(controller);
                return false;
            }
            // Along the wall, at the speed the last step left the character with, so anything in the way slows it down.
            Vector3 inertia = movementData.initialInertia;
            float turn = Vector3.Angle(normal, run.wallNormal);
            float maxTurn = new Vector2(inertia.x, inertia.z).magnitude * movementData.deltaTime
                            / (WallRunMinCurveRadius * controller.HeightScale) * Mathf.Rad2Deg + 1f;
            if (turn > maxTurn)
            {
                EndWallRun(controller); // Round an edge or corner: the wall has ended.
                return false;
            }
            run.wallNormal = normal;
            Vector3 along = Vector3.ProjectOnPlane(new Vector3(inertia.x, 0f, inertia.z), normal);
            along.y = 0f;
            float speed = along.magnitude;
            Vector3 tangent = speed > 1e-3f ? along / speed : Vector3.zero;
            Vector3 steer = InputDirection(movementData, inputs);
            run.elapsed += movementData.deltaTime;

            // Steering off the run (not along the wall, or away from it) only lets go once it's lasted the release delay.
            bool steeringOff = Vector3.Dot(steer, tangent) < 0.1f || Vector3.Dot(steer, normal) > 0.5f;
            run.releaseTime = steeringOff ? run.releaseTime + movementData.deltaTime : 0f;
            bool ended = speed < 0.5f * wallRunMinSpeed * controller.SpeedScale
                         || run.elapsed > wallRunMaxDuration * controller.DurationScale
                         || (steeringOff && run.releaseTime >= wallRunReleaseDelay)
                         || Mode.ConsumeCrouchPress(inputs, controller);
            if (ended)
            {
                EndWallRun(controller);
                return false;
            }

            if (Mode.IsNewJumpPress(inputs, controller))
            {
                WallJump(ref movementData, controller, tangent * speed, normal);
                return true;
            }

            // Run along the wall, falling slowly, pressed lightly against it.
            speed = Mathf.MoveTowards(speed, wallRunSpeed * controller.SpeedScale, Mode.AirAcceleration(controller) * movementData.deltaTime);
            float rise = inertia.y - wallRunGravity * controller.AccelerationScale * movementData.deltaTime;
            movementData.velocityOverride = tangent * speed + Vector3.up * rise - normal * (WallStickSpeed * controller.SpeedScale);
            movementData.directMovementVector = Vector3.zero;
            movementData.targetRotation = Quaternion.LookRotation(tangent, Vector3.up);
            // The wall is on the character's right if its normal points to the character's left.
            run.side = Vector3.Dot(Vector3.Cross(Vector3.up, tangent), normal) < 0f ? 1f : -1f;
            movementData.state |= CharacterState.WallRunning | CharacterState.Moving;
            return true;
        }

        /// <summary>
        /// Starts a wall run if the character is in mid-air, high enough above the ground, moving fast enough along a wall
        /// beside it (not straight into it), and steering along the wall (within the steering angle), the way it's moving.
        /// </summary>
        private bool TryStartWallRun(ref MovementData movementData, Inputs inputs, MovementController controller)
        {
            WallRunState run = controller.wallRun;
            if (wallRunMaxDuration <= 0f || movementData.initialGrounded || run.cooldownRemaining > 0f
                || controller.crouch.IsCrouched || Mode.AbilityInProgress(controller))
            {
                return false;
            }

            Vector3 inertia = movementData.initialInertia;
            Vector3 horizontal = new Vector3(inertia.x, 0f, inertia.z);
            float speed = horizontal.magnitude;
            if (speed < wallRunMinSpeed * controller.SpeedScale) { return false; }
            Vector3 direction = horizontal / speed;
            Vector3 steer = InputDirection(movementData, inputs);
            if (steer.sqrMagnitude < 0.25f || Vector3.Dot(steer, direction) < 0.1f) { return false; }

            // The nearer of a wall on the right and one on the left.
            Vector3 feet = movementData.initialPosition;
            Vector3 right = Vector3.Cross(Vector3.up, direction);
            bool onRight = FindWall(controller, feet, right, out Vector3 rightNormal, out float rightDistance);
            bool onLeft = FindWall(controller, feet, -right, out Vector3 leftNormal, out float leftDistance);
            if (!onRight && !onLeft) { return false; }
            Vector3 normal = onRight && (!onLeft || rightDistance <= leftDistance) ? rightNormal : leftNormal;
            if (run.hasLastWall && Vector3.Angle(normal, run.lastWallNormal) < SameWallAngle) { return false; }
            if (!WallReachesFeet(controller, feet, normal)) { return false; }

            // Running along the wall, not into it: within 60 degrees of it; and steering along it, the way it's moving,
            // within the steering angle of it.
            Vector3 along = Vector3.ProjectOnPlane(direction, normal);
            along.y = 0f;
            if (along.magnitude < 0.5f) { return false; }
            if (Vector3.Angle(steer, along) > wallRunMaxSteerAngle) { return false; }

            // High enough above the ground.
            float minHeight = wallRunMinHeight * controller.HeightScale;
            if (controller.Raycast(feet + Vector3.up * controller.Skin, Vector3.down, minHeight + controller.Skin, out _)) { return false; }

            run.IsWallRunning = true;
            run.elapsed = 0f;
            // Running on a wall counts as touching the ground: the character's jumps and air dashes come back. (The same
            // wall can't be run along twice before landing, so this can't be repeated on one wall.)
            GiveBackAirMoves(controller);
            run.releaseTime = 0f;
            run.wallNormal = normal;
            // A rising jump is cut down, and a fall caught.
            float rise = Mathf.Clamp(inertia.y, 0f, wallRunMaxRiseSpeed * controller.SpeedScale);
            movementData.initialInertia = new Vector3(inertia.x, rise, inertia.z);
            return true;
        }

        /// <summary>
        /// Whether there's a wall to run along beside the character in the given direction (within the check distance,
        /// and upright enough), and its normal, flattened (pointing away from the wall).
        /// </summary>
        private bool FindWall(MovementController controller, Vector3 feet, Vector3 direction, out Vector3 normal, out float distance)
        {
            normal = Vector3.zero;
            distance = 0f;
            float reach = wallRunCheckDistance * controller.HeightScale + controller.Skin;
            if (!controller.SweepCapsule(feet, direction, reach, out RaycastHit hit)) { return false; }
            if (Mathf.Abs(hit.normal.y) > Mathf.Sin(wallRunMaxWallTilt * Mathf.Deg2Rad)) { return false; }
            normal = new Vector3(hit.normal.x, 0f, hit.normal.z);
            if (normal.sqrMagnitude < 1e-6f) { return false; }
            normal.Normalize();
            // Facing the character, not a surface it would only graze.
            if (Vector3.Dot(normal, -direction.normalized) < 0.5f) { return false; }
            distance = hit.distance;
            return true;
        }

        /// <summary>
        /// Whether the wall with the given normal reaches down to the character's feet (to the wall run's foot height
        /// above them), for them to run on: the capsule sweep that finds the wall also finds one beside its upper body,
        /// and the character kept running along a wall with its feet below the bottom edge.
        /// </summary>
        private bool WallReachesFeet(MovementController controller, Vector3 feet, Vector3 normal)
        {
            float reach = controller.Radius + wallRunCheckDistance * controller.HeightScale + 2f * controller.Skin;
            return controller.Raycast(feet + Vector3.up * (wallRunFootHeight * controller.StandingHeight), -normal, reach, out _);
        }

        /// <summary>
        /// Jumps off the wall: away from it and upward, keeping the run's speed along it. Gives back the character's
        /// jumps, less the one this uses, and takes away air steering, which comes back over the mode's wall jump steering recovery time.
        /// </summary>
        private void WallJump(ref MovementData movementData, MovementController controller, Vector3 alongWall, Vector3 normal)
        {
            EndWallRun(controller);
            Vector3 velocity = alongWall + normal * (wallJumpAwaySpeed * controller.SpeedScale) + Vector3.up * (wallJumpUpSpeed * controller.SpeedScale);
            // Facing the way it's launched.
            MovementHelpers.JumpOffWall(ref movementData, controller, velocity, normal, new Vector3(velocity.x, 0f, velocity.z));
        }

        private void EndWallRun(MovementController controller)
        {
            WallRunState run = controller.wallRun;
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
                        Inputs inputs = MovementSimulation.Move(input, true);
                        inputs.jump = MovementSimulation.Held(i);
                        return inputs;
                    }, groundedFrom: 3, ref length, ref drop);
                }
            }
            // And one started level: in mid-air beside the wall, well above the ground, moving along it at the run speed.
            // A run started level (or falling: a fall is caught) sinks the furthest: its drop is the run's.
            {
                simulation.ClearObstacles();
                simulation.Reset(new Vector3(0f, 4f * simulation.Controller.StandingHeight, 0f));
                WallBeside(simulation, 0.1f);
                simulation.Controller.OverrideVelocity(Vector3.forward * (SettingsOf<WalkingAndRunning>().RunSpeed * simulation.Controller.SpeedScale));
                Vector3 input = Quaternion.Euler(0f, 10f, 0f) * Vector3.forward;
                MeasureRun(simulation, _ => MovementSimulation.Move(input, true), groundedFrom: 0, ref length, ref drop);
            }
            simulation.ClearObstacles();
            return (length, drop);
        }

        /// <summary>A long, tall wall along the character's right, its face outFromWall (m) beyond the character's side.</summary>
        private static void WallBeside(MovementSimulation simulation, float outFromWall)
        {
            float face = simulation.Controller.Radius + outFromWall;
            simulation.Box(new Vector3(face + 0.5f, 15f, 200f), new Vector3(1f, 30f, 420f));
        }

        /// <summary>
        /// Steps the simulation with the given inputs until a wall run starts and ends (keeping the longest run along the
        /// wall and the largest fall over one seen so far in length and drop), or it gives up: on the ground from step
        /// groundedFrom on (before it, it may still be taking off), or after the measuring timeout.
        /// </summary>
        private static void MeasureRun(MovementSimulation simulation, Func<int, Inputs> inputsFor, int groundedFrom, ref float length, ref float drop)
        {
            float startZ = float.NaN, startY = float.NaN;
            for (int i = 0; i < MovementSimulation.StepsFor(MovementSimulation.MeasureTimeout); i++)
            {
                Vector3 before = simulation.Position;
                simulation.Step(inputsFor(i));
                bool running = (simulation.State & CharacterState.WallRunning) != 0;
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
        /// <summary>Which side of the character the wall is on: 1 = right, -1 = left (0 when not wall-running).</summary>
        public float side;
        /// <summary>The wall last run along (its normal), which can't be run along again until the character touches the ground.</summary>
        public Vector3 lastWallNormal;
        /// <summary>Whether lastWallNormal holds a wall.</summary>
        public bool hasLastWall;
    }
}
