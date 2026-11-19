using System;
using UnityEngine;

namespace MeridianMotion.Characters
{
    // Capabilities: what this movement lets a character do, measured by running it (see MMotionMotor.Capabilities).
    public partial class StandardMovementMode
    {
        // How far (m) each binary search for a height narrows down to.
        private const float MeasureHeightPrecision = 0.01f;

        /// <summary>
        /// Measures the jumps (plain, sprinting, with an air dash, and double jumps if there are air jumps) and the climbs
        /// (stepping up, jumping up, jumping to a ledge, and double-jump versions), which combine several components, by
        /// running each in the simulation (searching for the best timing where a move has a choice of when to dash or jump
        /// again); the jump heights, exact by construction, come from the settings. Then each component measures its own
        /// (see MovementComponent.MeasureCapabilities: Crouching, Rolling and Wall Running).
        /// </summary>
        public override void MeasureCapabilities(MovementSimulation simulation, MovementCapabilities capabilities)
        {
            MMotionMotor copy = simulation.Controller;
            float heightScale = copy.HeightScale * copy.jumpSpeedMultiplier * copy.jumpSpeedMultiplier;
            Jumping jumping = Get<Jumping>();
            float maxJumpHeight = jumping != null ? jumping.MaxJumpHeight : 0f, minJumpHeight = jumping != null ? jumping.MinJumpHeight : 0f;
            int maxJumps = jumping != null ? jumping.MaxJumps : 0;
            Dashing dashing = Get<Dashing>();
            capabilities.MinJumpHeight = maxJumpHeight > 0f ? Mathf.Min(minJumpHeight, maxJumpHeight) * heightScale : float.NaN;
            capabilities.MaxJumpHeight = maxJumpHeight > 0f ? maxJumpHeight * heightScale : float.NaN;

            bool canJump = maxJumpHeight > 0f && maxJumps > 0;
            bool canDash = dashing is { DashDuration: > 0f, MaxAirDashes: > 0 };
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
                StandardMovementMode plain = simulation.Copy(this);
                TurnOff<Ledges>(plain);
                TurnOff<Vaulting>(plain);
                TurnOff<WallSliding>(plain);
                TurnOff<WallRunning>(plain);
                TurnOff<WallClimbing>(plain);
                float top = capabilities.MaxJumpHeight + 2f * capabilities.StepHeight + capabilities.StandHeight;
                MovementCapabilities.Add(capabilities.climbs, "jump up", HighestReached(simulation, plain, top, -1));
                if (airJumps)
                {
                    MovementCapabilities.Add(capabilities.climbs, "double jump up",
                        HighestReached(simulation, plain, 2f * top, Mathf.RoundToInt(DurationOfRise(copy, capabilities) / MovementSimulation.DeltaTime)));
                }
                Ledges ledgeGrabbing = Get<Ledges>();
                if (ledgeGrabbing != null && ledgeGrabbing.LedgeClimbDuration > 0f)
                {
                    // Jumping to a ledge and climbing up onto it (nothing else that could help: no wall slides or runs).
                    StandardMovementMode ledges = simulation.Copy(plain);
                    ledges.Get<Ledges>(includeDisabled: true).Enabled = true;
                    float reach = capabilities.MaxJumpHeight + ledgeGrabbing.LedgeGrabMaxHeight * capabilities.StandHeight + capabilities.StandHeight;
                    MovementCapabilities.Add(capabilities.climbs, "jump to the ledge", HighestReached(simulation, ledges, reach, -1));
                    if (airJumps)
                    {
                        MovementCapabilities.Add(capabilities.climbs, "double jump to the ledge",
                            HighestReached(simulation, ledges, reach + top, Mathf.RoundToInt(DurationOfRise(copy, capabilities) / MovementSimulation.DeltaTime)));
                    }
                }
            }

            // ---- Each ability's own (the crouch height, how far a slide, a roll and a wall run carry the character) ----
            base.MeasureCapabilities(simulation, capabilities);
        }

        /// <summary>Turns off a copy's component of type T, if it has one.</summary>
        private static void TurnOff<T>(StandardMovementMode mode) where T : MovementComponent
        {
            T component = mode.Get<T>(includeDisabled: true);
            if (component != null) { component.Enabled = false; }
        }

        /// <summary>About how long (s) a jump takes to rise to its top, for timing a second jump near it.</summary>
        private float DurationOfRise(MMotionMotor controller, MovementCapabilities capabilities)
        {
            float g = GravityAcceleration(controller);
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
            // The attempts (a jump with a second jump and an air dash at every time worth trying) share their start: the
            // same run-up, and the same steps until their inputs differ. Before its second jump, an attempt is the one
            // without it (the trunk); after it, it's on that second jump's branch; and before its dash, it's the one
            // without a dash. Each attempt starts from the furthest point of its path an earlier attempt saved (see
            // MovementSimulation.KeepOnPath), and saves the point where it leaves it (its dash; or, its second jump), from
            // which the next attempt of a search (a step or two on) carries on, rather than every attempt running from
            // the start. The steps are the same either way: so the measurement is too, exactly.
            string runUp = sprint ? "sprinting run-up" : "walking run-up";
            string trunk = runUp, branch = secondJumpAt >= 0 ? $"{runUp}, second jump at {secondJumpAt}" : null;
            // The state before step i is on the trunk while the second jump hasn't begun (Jump's let go the step before
            // it), and shared only up to the dash.
            string PathOf(int i) => branch == null || i <= secondJumpAt - 1 ? trunk : branch;
            int steps = MovementSimulation.StepsFor(MovementSimulation.MeasureTimeout);
            int shareUpTo = dashAt >= 0 ? dashAt : steps - 1;

            int first = 0;
            float start = 0f;
            bool left = false;
            object extra = null;
            bool restored = branch != null && shareUpTo >= secondJumpAt && simulation.RestoreFurthestOnPath(branch, shareUpTo, out first, out extra);
            if (!restored)
            {
                restored = simulation.RestoreFurthestOnPath(trunk, branch != null ? Mathf.Min(shareUpTo, secondJumpAt - 1) : shareUpTo, out first, out extra);
            }
            if (restored) { (start, left, airtime) = ((float, bool, int))extra; }
            else
            {
                first = 0;
                // Every attempt runs up the same way: run once, and each attempt after starts from where it left the
                // character (see MovementSimulation.Begin).
                simulation.Begin(runUp, () =>
                {
                    simulation.Reset(Vector3.zero);
                    simulation.Run(MovementSimulation.StepsFor(MovementSimulation.MeasureRunUp), _ => MovementSimulation.Move(Vector3.forward, sprint));
                });
                if (!simulation.Grounded) { return float.NaN; }
                start = simulation.Position.z;
            }
            for (int i = first; i < steps; i++)
            {
                // Where this attempt leaves its path: kept for the next attempt of its search.
                if (i > 0 && (i == dashAt || (branch != null && i == secondJumpAt - 1))) { simulation.KeepOnPath(PathOf(i), i, (start, left, airtime)); }
                StepInputs inputs = MovementSimulation.Move(Vector3.forward, sprint);
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

        private float HighestReached(MovementSimulation simulation, StandardMovementMode mode, float upTo, int secondJumpAt)
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
        private bool Reaches(MovementSimulation simulation, StandardMovementMode mode, float height, float back, bool sprint, int secondJumpAt)
        {
            simulation.ClearObstacles();
            simulation.Reset(Vector3.zero, mode);
            float face = simulation.Controller.Body.FrontExtent + back;
            simulation.Box(new Vector3(0f, height / 2f, face + 10f), new Vector3(20f, height, 20f));
            bool airborne = false;
            // Hanging from the top's edge, Jump is let go of for a step and pressed again, to climb up.
            int climbPressAt = -1;
            for (int i = 0; i < MovementSimulation.StepsFor(MovementSimulation.MeasureTimeout); i++)
            {
                StepInputs inputs = MovementSimulation.Move(Vector3.forward, sprint);
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

    }
}
