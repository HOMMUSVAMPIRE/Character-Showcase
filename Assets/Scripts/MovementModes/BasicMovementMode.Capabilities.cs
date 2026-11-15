using System;
using UnityEngine;

namespace Character
{
    // Capabilities: what this movement lets a character do, measured by running it (see MovementController.Capabilities).
    public partial class BasicMovementMode
    {
        // How far (m) each binary search for a height narrows down to, and how long (s) a measurement may run at most.
        private const float MeasureHeightPrecision = 0.01f;
        private const float MeasureTimeout = 8f;
        // How long (s) the character runs up before a jump is measured: long enough to be at full speed.
        private const float MeasureRunUp = 0.8f;

        /// <summary>
        /// Measures the jumps (plain, sprinting, with an air dash, and double jumps if there are air jumps), the climbs
        /// (stepping up, jumping up, jumping to a ledge, and double-jump versions), and how far a slide, a roll and a wall
        /// run carry the character, by running each in the simulation (searching for the best timing where a move has a
        /// choice of when to dash or jump again). Heights that are exact by construction (the jump heights, the crouch
        /// height) come from the settings.
        /// </summary>
        public override void MeasureCapabilities(MovementSimulation simulation, MovementCapabilities capabilities)
        {
            MovementController copy = simulation.Controller;
            float heightScale = copy.HeightScale * copy.jumpStrengthModifier * copy.jumpStrengthModifier;
            capabilities.MinJumpHeight = maxJumpHeight > 0f ? Mathf.Min(minJumpHeight, maxJumpHeight) * heightScale : float.NaN;
            capabilities.MaxJumpHeight = maxJumpHeight > 0f ? maxJumpHeight * heightScale : float.NaN;
            capabilities.CrouchHeight = crouchHeight * capabilities.StandHeight;

            bool canJump = maxJumpHeight > 0f && maxJumps > 0;
            bool canDash = dashDuration > 0f && maxAirDashes > 0;
            bool airJumps = canJump && maxJumps > 1;

            // ---- Jumps: gaps crossed at the same height ----
            if (canJump)
            {
                MovementCapabilities.Add(capabilities.jumps, "jump", JumpAcross(simulation, sprint: false));
                float sprintJump = JumpAcross(simulation, sprint: true, airtime: out int air);
                MovementCapabilities.Add(capabilities.jumps, "sprint jump", sprintJump);
                if (canDash)
                {
                    MovementCapabilities.Add(capabilities.jumps, "sprint jump and air dash",
                        Best(1, air, dash => JumpAcross(simulation, true, dashAt: dash)));
                }
                if (airJumps)
                {
                    float doubleJump = Best(2, air, again => JumpAcross(simulation, true, secondJumpAt: again));
                    MovementCapabilities.Add(capabilities.jumps, "double jump", doubleJump);
                    if (canDash)
                    {
                        MovementCapabilities.Add(capabilities.jumps, "double jump and air dash",
                            Best(2, air, again => Best(again + 1, again + air, dash => JumpAcross(simulation, true, dashAt: dash, secondJumpAt: again), 2), 2));
                    }
                }
            }

            // ---- Climbs: heights reached (stood on) ----
            MovementCapabilities.Add(capabilities.climbs, "step up", capabilities.StepHeight);
            if (canJump)
            {
                // Jumping up onto something, without catching its edge as a ledge (or vaulting, sliding down it...).
                BasicMovementMode plain = simulation.Copy(this);
                plain.ledgeClimbDuration = 0f;
                plain.vaultRiseDuration = 0f;
                plain.wallSlideMaxDuration = 0f;
                plain.wallRunMaxDuration = 0f;
                plain.wallClimbSpeed = 0f;
                float top = capabilities.MaxJumpHeight + 2f * capabilities.StepHeight + capabilities.StandHeight;
                MovementCapabilities.Add(capabilities.climbs, "jump up", HighestReached(simulation, plain, top, -1));
                if (airJumps)
                {
                    MovementCapabilities.Add(capabilities.climbs, "double jump up",
                        HighestReached(simulation, plain, 2f * top, Mathf.RoundToInt(DurationOfRise(copy, capabilities) / MovementSimulation.DeltaTime)));
                }
                if (ledgeClimbDuration > 0f)
                {
                    // Jumping to a ledge and climbing up onto it (nothing else that could help: no wall slides or runs).
                    BasicMovementMode ledges = simulation.Copy(plain);
                    ledges.ledgeClimbDuration = ledgeClimbDuration;
                    float reach = capabilities.MaxJumpHeight + ledgeGrabMaxHeight * capabilities.StandHeight + capabilities.StandHeight;
                    MovementCapabilities.Add(capabilities.climbs, "jump to the ledge", HighestReached(simulation, ledges, reach, -1));
                    if (airJumps)
                    {
                        MovementCapabilities.Add(capabilities.climbs, "double jump to the ledge",
                            HighestReached(simulation, ledges, reach + top, Mathf.RoundToInt(DurationOfRise(copy, capabilities) / MovementSimulation.DeltaTime)));
                    }
                }
            }

            // ---- Abilities' reach ----
            capabilities.SlideLength = slideMaxDuration > 0f ? SlideLength(simulation) : float.NaN;
            capabilities.RollLength = rollDuration > 0f ? RollLength(simulation) : float.NaN;
            if (wallRunMaxDuration > 0f && canJump)
            {
                (float length, float drop) = WallRun(simulation);
                capabilities.WallRunLength = length;
                capabilities.WallRunDrop = drop;
            }
        }

        /// <summary>About how long (s) a jump takes to rise to its top, for timing a second jump near it.</summary>
        private float DurationOfRise(MovementController controller, MovementCapabilities capabilities)
        {
            float g = gravityStrength * controller.AccelerationScale;
            return g > 0f && capabilities.MaxJumpHeight > 0f ? Mathf.Sqrt(2f * capabilities.MaxJumpHeight / g) : 0.3f;
        }

        /// <summary>The greatest of measure(i) for i from first to last (every step'th), ignoring NaNs; NaN if none.</summary>
        private static float Best(int first, int last, Func<int, float> measure, int step = 1)
        {
            float best = float.NaN;
            for (int i = first; i <= last; i += Mathf.Max(1, step))
            {
                float value = measure(i);
                if (!float.IsNaN(value) && (float.IsNaN(best) || value > best)) { best = value; }
            }
            return best;
        }

        private float JumpAcross(MovementSimulation simulation, bool sprint, int dashAt = -1, int secondJumpAt = -1) =>
            JumpAcross(simulation, sprint, out _, dashAt, secondJumpAt);

        /// <summary>
        /// How far (m, along the way it runs) a jump carries the character, from where it takes off to where it touches
        /// down at the same height: running (or sprinting) up, then Jump held, a dash on step dashAt of the jump and a
        /// second press of Jump on step secondJumpAt (none if negative). NaN if it never lands. Also how many steps it was
        /// in the air.
        /// </summary>
        private float JumpAcross(MovementSimulation simulation, bool sprint, out int airtime, int dashAt = -1, int secondJumpAt = -1)
        {
            airtime = 0;
            simulation.Reset(Vector3.zero);
            simulation.Run(MovementSimulation.StepsFor(MeasureRunUp), _ => MovementSimulation.Move(Vector3.forward, sprint));
            if (!simulation.Grounded) { return float.NaN; }
            float start = simulation.Position.z;
            bool left = false;
            for (int i = 0; i < MovementSimulation.StepsFor(MeasureTimeout); i++)
            {
                Inputs inputs = MovementSimulation.Move(Vector3.forward, sprint);
                // Jump held from the first step; let go for a step before a second press, then held again.
                inputs.jump = secondJumpAt < 0 || i < secondJumpAt - 1 ? MovementSimulation.Held(i)
                    : i == secondJumpAt - 1 ? MovementSimulation.Held(-1) : MovementSimulation.Held(i - secondJumpAt);
                if (dashAt >= 0 && i >= dashAt) { inputs.dash = MovementSimulation.Held(i - dashAt); }
                simulation.Step(inputs);
                if (!simulation.Grounded) { left = true; airtime++; }
                else if (left) { return simulation.Position.z - start; }
            }
            return float.NaN;
        }

        /// <summary>
        /// The highest top (m above the floor) the character stands on after jumping at it (moving with the given mode),
        /// from a few distances back from its face, walking or sprinting, with a second press of Jump on step secondJumpAt
        /// (none if negative): a binary search up to upTo. NaN if not even the lowest is reached.
        /// </summary>
        private float HighestReached(MovementSimulation simulation, BasicMovementMode mode, float upTo, int secondJumpAt)
        {
            float best = float.NaN;
            foreach (bool sprint in new[] { false, true })
            {
                foreach (float back in new[] { 0.2f, 0.8f, 1.6f })
                {
                    float low = 0f, high = upTo;
                    if (!Reaches(simulation, mode, MeasureHeightPrecision, back, sprint, secondJumpAt)) { continue; }
                    while (high - low > MeasureHeightPrecision)
                    {
                        float mid = (low + high) / 2f;
                        if (Reaches(simulation, mode, mid, back, sprint, secondJumpAt)) { low = mid; } else { high = mid; }
                    }
                    if (float.IsNaN(best) || low > best) { best = low; }
                }
            }
            return best;
        }

        /// <summary>Whether a jump at a block of the given height, from back metres before its face, ends standing on it.</summary>
        private bool Reaches(MovementSimulation simulation, BasicMovementMode mode, float height, float back, bool sprint, int secondJumpAt)
        {
            simulation.ClearObstacles();
            simulation.Reset(Vector3.zero, mode);
            float face = simulation.Controller.Radius + back;
            simulation.Box(new Vector3(0f, height / 2f, face + 10f), new Vector3(20f, height, 20f));
            bool airborne = false;
            // Hanging from the top's edge, Jump is let go of for a step and pressed again, to climb up.
            int climbPressAt = -1;
            for (int i = 0; i < MovementSimulation.StepsFor(MeasureTimeout); i++)
            {
                Inputs inputs = MovementSimulation.Move(Vector3.forward, sprint);
                inputs.jump = secondJumpAt < 0 || i < secondJumpAt - 1 ? MovementSimulation.Held(i)
                    : i == secondJumpAt - 1 ? MovementSimulation.Held(-1) : MovementSimulation.Held(i - secondJumpAt);
                if (climbPressAt < 0 && (simulation.State & CharacterState.LedgeHanging) != 0) { climbPressAt = i + 1; }
                if (climbPressAt >= 0) { inputs.jump = MovementSimulation.Held(i - climbPressAt); }
                simulation.Step(inputs);
                if (!simulation.Grounded) { airborne = true; }
                bool busy = (simulation.State & (CharacterState.LedgeHanging | CharacterState.LedgeClimbing)) != 0;
                if (!simulation.Grounded || busy) { continue; }
                // Standing on the top past its face: reached. Back on the floor after leaving it: not.
                if (simulation.Position.y > height - 0.05f && simulation.Position.z > face) { simulation.ClearObstacles(); return true; }
                if (airborne) { simulation.ClearObstacles(); return false; }
            }
            simulation.ClearObstacles();
            return false;
        }

        /// <summary>How far (m) a slide from a sprint carries the character, from where it starts to where it ends.</summary>
        private float SlideLength(MovementSimulation simulation)
        {
            simulation.Reset(Vector3.zero);
            simulation.Run(MovementSimulation.StepsFor(MeasureRunUp), _ => MovementSimulation.Move(Vector3.forward, true));
            float start = float.NaN;
            for (int i = 0; i < MovementSimulation.StepsFor(MeasureTimeout); i++)
            {
                float before = simulation.Position.z;
                Inputs inputs = MovementSimulation.Move(Vector3.forward, true);
                inputs.crouch = MovementSimulation.Held(i);
                simulation.Step(inputs);
                bool sliding = (simulation.State & CharacterState.Sliding) != 0;
                if (sliding && float.IsNaN(start)) { start = before; }
                if (!sliding && !float.IsNaN(start)) { return simulation.Position.z - start; }
            }
            return float.NaN;
        }

        /// <summary>How far (m) a roll carries the character, from where it starts to where it ends.</summary>
        private float RollLength(MovementSimulation simulation)
        {
            simulation.Reset(Vector3.zero);
            simulation.Run(MovementSimulation.StepsFor(MeasureRunUp), _ => MovementSimulation.Move(Vector3.forward));
            float start = float.NaN;
            for (int i = 0; i < MovementSimulation.StepsFor(MeasureTimeout); i++)
            {
                float before = simulation.Position.z;
                Inputs inputs = MovementSimulation.Move(Vector3.forward);
                inputs.roll = MovementSimulation.Held(i);
                simulation.Step(inputs);
                bool rolling = (simulation.State & CharacterState.Rolling) != 0;
                if (rolling && float.IsNaN(start)) { start = before; }
                if (!rolling && !float.IsNaN(start)) { return simulation.Position.z - start; }
            }
            return float.NaN;
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
                    float face = simulation.Controller.Radius + outFromWall;
                    simulation.Box(new Vector3(face + 0.5f, 15f, 200f), new Vector3(1f, 30f, 420f));
                    Vector3 input = Quaternion.Euler(0f, steer, 0f) * Vector3.forward;
                    simulation.Run(MovementSimulation.StepsFor(MeasureRunUp), _ => MovementSimulation.Move(Vector3.forward, true));
                    float startZ = float.NaN, startY = float.NaN;
                    for (int i = 0; i < MovementSimulation.StepsFor(MeasureTimeout); i++)
                    {
                        Vector3 before = simulation.Position;
                        Inputs inputs = MovementSimulation.Move(input, true);
                        inputs.jump = MovementSimulation.Held(i);
                        simulation.Step(inputs);
                        bool running = (simulation.State & CharacterState.WallRunning) != 0;
                        if (running && float.IsNaN(startZ)) { startZ = before.z; startY = before.y; }
                        if (!running && !float.IsNaN(startZ))
                        {
                            float run = simulation.Position.z - startZ, fall = startY - simulation.Position.y;
                            if (float.IsNaN(length) || run > length) { length = run; }
                            if (float.IsNaN(drop) || fall > drop) { drop = fall; }
                            break;
                        }
                        if (simulation.Grounded && i > 2) { break; }
                    }
                }
            }
            // And one started level: in mid-air beside the wall, well above the ground, moving along it at the run speed.
            // A run started level (or falling: a fall is caught) sinks the furthest: its drop is the run's.
            {
                simulation.ClearObstacles();
                simulation.Reset(new Vector3(0f, 4f * simulation.Controller.StandingHeight, 0f));
                float face = simulation.Controller.Radius + 0.1f;
                simulation.Box(new Vector3(face + 0.5f, 15f, 200f), new Vector3(1f, 30f, 420f));
                simulation.Controller.OverrideVelocity(Vector3.forward * (runSpeed * simulation.Controller.SpeedScale));
                Vector3 input = Quaternion.Euler(0f, 10f, 0f) * Vector3.forward;
                float startZ = float.NaN, startY = float.NaN;
                for (int i = 0; i < MovementSimulation.StepsFor(MeasureTimeout); i++)
                {
                    Vector3 before = simulation.Position;
                    simulation.Step(MovementSimulation.Move(input, true));
                    bool running = (simulation.State & CharacterState.WallRunning) != 0;
                    if (running && float.IsNaN(startZ)) { startZ = before.z; startY = before.y; }
                    if (!running && !float.IsNaN(startZ))
                    {
                        float run = simulation.Position.z - startZ, fall = startY - simulation.Position.y;
                        if (float.IsNaN(length) || run > length) { length = run; }
                        if (float.IsNaN(drop) || fall > drop) { drop = fall; }
                        break;
                    }
                    if (simulation.Grounded) { break; }
                }
            }
            simulation.ClearObstacles();
            return (length, drop);
        }
    }
}
