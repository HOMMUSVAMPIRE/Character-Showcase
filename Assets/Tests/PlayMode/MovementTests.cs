using System.Collections;
using Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CharacterShowcase.Tests
{
    /// <summary>
    /// Physics-level tests of the character controller, using the real Character prefab and movement tuning.
    /// Several guard against specific regressions: hovering above the ground, falling faster than gravity, and
    /// gravity accumulating while standing so walking off a ledge drops like a stone.
    /// </summary>
    public class MovementTests
    {
        private CharacterTestRig rig;

        [SetUp]
        public void SetUp() => rig = new CharacterTestRig();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            rig.Dispose();
            yield return null; // Let Destroy finish before the next test builds its world.
        }

        private float Gravity => rig.MovementSetting<float>("gravityStrength");

        [UnityTest]
        public IEnumerator SettlesOnGround_WithoutHoveringOrSinking()
        {
            rig.Floor();
            rig.SpawnCharacter(new Vector3(0f, 0.5f, 0f));

            yield return rig.Run(2f, _ => CharacterTestRig.Idle());

            Assert.That(rig.Grounded, Is.True, "Character should be grounded after settling.");
            Assert.That(rig.Position.y, Is.InRange(-0.01f, 0.03f), "Feet should rest on the floor, not hover above or sink into it.");
        }

        [UnityTest]
        public IEnumerator StandingStill_DoesNotDrift()
        {
            rig.Floor();
            rig.SpawnCharacter(new Vector3(0f, 0.05f, 0f));
            yield return rig.Run(0.5f, _ => CharacterTestRig.Idle());
            Vector3 settled = rig.Position;

            yield return rig.Run(2f, _ => CharacterTestRig.Idle());

            Assert.That(Vector3.Distance(rig.Position, settled), Is.LessThan(0.01f), "Idle character should not move.");
        }

        [UnityTest]
        public IEnumerator FreeFall_StaysWithinGravityLimits()
        {
            // Never grounded, so no coyote-time easing: full gravity from the first step.
            rig.SpawnCharacter(new Vector3(0f, 50f, 0f));
            int steps = CharacterTestRig.StepsFor(0.6f);

            yield return rig.Run(steps, _ => CharacterTestRig.Idle());

            float fallen = 50f - rig.Position.y;
            float dt = Time.fixedDeltaTime;
            float seconds = steps * dt;
            // Exact fall for this integration (velocity updated before position, each step): g·dt²·n(n+1)/2.
            float maxFall = Gravity * dt * dt * steps * (steps + 1) / 2f;
            Assert.That(fallen, Is.LessThanOrEqualTo(maxFall + 1e-3f), "Character fell faster than gravity allows.");
            Assert.That(fallen, Is.GreaterThan(0.25f * 0.5f * Gravity * seconds * seconds), "Character barely fell; gravity isn't being applied.");
        }

        [UnityTest]
        public IEnumerator WalkingOffLedge_AfterStanding_FallsNoFasterThanGravity()
        {
            // Raised ledge ending at z = 0, with lower ground 1 m below beyond it.
            rig.Box("Ledge", new Vector3(0f, -0.5f, -10f), new Vector3(10f, 1f, 20f));
            rig.Floor(-1f);
            rig.SpawnCharacter(new Vector3(0f, 0.05f, -1.5f));

            // Stand still for a while first: gravity used to accumulate while grounded and release all at once on
            // leaving the ground, launching the character downward.
            yield return rig.Run(1.5f, _ => CharacterTestRig.Idle());
            Assert.That(rig.Grounded, Is.True);

            // From the moment the character starts dropping, its downward speed can't exceed what gravity could
            // build up in that time, plus one step of ground snap while the capsule rolls over the edge.
            float dt = Time.fixedDeltaTime;
            float snapAllowance = (0.1f + 0.02f) / dt; // GroundedTolerance + skin width, per step.
            float? previousY = null;
            int dropStartStep = -1;
            float worstMargin = float.PositiveInfinity, worstSpeed = 0f, worstLimit = 0f;
            bool reachedBottom = false;
            yield return rig.Run(rig.SecondsToWalk(1.5f, 1f), _ => CharacterTestRig.Move(Vector3.forward), step =>
            {
                float y = rig.Position.y;
                if (dropStartStep < 0 && y < -0.001f) { dropStartStep = step; }
                if (previousY.HasValue && dropStartStep >= 0)
                {
                    float downwardSpeed = -(y - previousY.Value) / dt;
                    float limit = Gravity * (step - dropStartStep + 1) * dt + snapAllowance;
                    if (limit - downwardSpeed < worstMargin)
                    {
                        worstMargin = limit - downwardSpeed;
                        worstSpeed = downwardSpeed;
                        worstLimit = limit;
                    }
                }
                previousY = y;
                reachedBottom |= y < -0.9f;
            });

            Assert.That(reachedBottom, Is.True, "Character never fell to the lower ground.");
            Assert.That(worstMargin, Is.GreaterThanOrEqualTo(0f),
                $"Character fell at {worstSpeed:0.0} m/s where gravity allows at most {worstLimit:0.0} m/s.");
        }

        [UnityTest]
        public IEnumerator WalkingOffLedge_EasesIntoGravityDuringCoyoteTime()
        {
            rig.Box("Ledge", new Vector3(0f, -0.5f, -10f), new Vector3(10f, 1f, 20f));
            rig.Floor(-5f);
            rig.SpawnCharacter(new Vector3(0f, 0.05f, -1.5f));
            yield return rig.Run(0.5f, _ => CharacterTestRig.Idle());

            float dt = Time.fixedDeltaTime;
            int airborneSteps = 0;
            float yWhenLeft = float.NaN, yAfterCoyoteWindow = float.NaN, timeSinceGroundedAfterWindow = float.NaN;
            int window = CharacterTestRig.StepsFor(0.1f);
            yield return rig.Run(rig.SecondsToWalk(1.5f, 1f), _ => CharacterTestRig.Move(Vector3.forward), _ =>
            {
                if (rig.Grounded) { return; }
                if (airborneSteps == 0) { yWhenLeft = rig.Position.y; }
                airborneSteps++;
                if (airborneSteps == window + 1)
                {
                    yAfterCoyoteWindow = rig.Position.y;
                    timeSinceGroundedAfterWindow = rig.Movement.lastFrameMovementData.timeSinceGrounded;
                }
            });

            Assert.That(airborneSteps, Is.GreaterThan(window), "Character never left the ledge.");
            // Time since grounded must keep counting in the air (it used to be time since the game started).
            Assert.That(timeSinceGroundedAfterWindow, Is.EqualTo((window + 1) * dt).Within(2f * dt),
                "timeSinceGrounded should measure time since leaving the ground.");
            // The coyote gravity curve eases gravity in after leaving the ground, so the first 0.1 s of the drop is
            // much shallower than full-gravity free fall (which would be ~0.2 m).
            float fullGravityFall = 0.5f * Gravity * (window * dt) * (window * dt);
            Assert.That(yWhenLeft - yAfterCoyoteWindow, Is.LessThan(0.5f * fullGravityFall),
                "Gravity applied at full strength immediately; the coyote gravity curve isn't being used.");
        }

        [UnityTest]
        public IEnumerator WalkingIntoWall_StopsAtWall()
        {
            rig.Floor();
            rig.SpawnCharacter(new Vector3(0f, 0.05f, 0f));
            float radius = rig.Radius, face = radius + 2f;
            rig.Box("Wall", new Vector3(0f, rig.Height, face + 0.5f), new Vector3(10f, 2f * rig.Height, 1f));

            float furthest = float.NegativeInfinity;
            yield return rig.Run(rig.SecondsToWalk(2f), _ => CharacterTestRig.Move(Vector3.forward), _ => furthest = Mathf.Max(furthest, rig.Position.z));

            Assert.That(furthest, Is.LessThanOrEqualTo(face - radius + 0.01f), "Character pushed into the wall.");
            Assert.That(rig.Position.z, Is.GreaterThan(face - radius - 0.1f), "Character stopped well short of the wall.");
        }

        [UnityTest]
        public IEnumerator Jump_RisesAndLandsBackOnGround()
        {
            rig.Floor();
            rig.SpawnCharacter(new Vector3(0f, 0.05f, 0f));
            yield return rig.Run(0.5f, _ => CharacterTestRig.Idle());

            float apex = 0f;
            int holdSteps = CharacterTestRig.StepsFor(0.2f);
            yield return rig.Run(3f, step => step < holdSteps ? CharacterTestRig.Jump(step) : CharacterTestRig.Idle(),
                _ => apex = Mathf.Max(apex, rig.Position.y));

            Assert.That(apex, Is.GreaterThan(rig.StepHeight), "Jump barely left the ground (not even a step's height).");
            Assert.That(rig.Grounded, Is.True, "Character should have landed.");
            Assert.That(rig.Position.y, Is.InRange(-0.01f, 0.03f), "Character should land on the floor, not above or inside it.");
        }

        [UnityTest]
        public IEnumerator TapJump_DoesNotGetItsJumpRefundedByCoyoteTime()
        {
            rig.Floor();
            rig.SpawnCharacter(new Vector3(0f, 0.05f, 0f));
            yield return rig.Run(0.3f, _ => CharacterTestRig.Idle());
            int maxJumps = rig.Mode<JumpMode>().maxJumps;

            // A one-step tap, released while still inside the coyote window.
            yield return rig.Run(1, _ => CharacterTestRig.Jump(0));
            yield return rig.Run(CharacterTestRig.StepsFor(rig.MovementSetting<float>("coyoteTime")), _ => CharacterTestRig.Idle());

            Assert.That(rig.Grounded, Is.False, "Test setup: the character should be in the air.");
            Assert.That(rig.Movement.jumpsRemaining, Is.EqualTo(maxJumps - 1), "The jump just used should stay used.");
        }

        [UnityTest]
        public IEnumerator WalkingOffLedge_KeepsCoyoteJump()
        {
            rig.Box("Ledge", new Vector3(0f, -0.5f, -10f), new Vector3(10f, 1f, 20f));
            rig.Floor(-5f);
            rig.SpawnCharacter(new Vector3(0f, 0.05f, -0.8f));
            yield return rig.Run(0.3f, _ => CharacterTestRig.Idle());
            int maxJumps = rig.Mode<JumpMode>().maxJumps;

            // Walk off, then check the jumps just after leaving the ground.
            int airborneSteps = 0;
            int jumpsShortlyAfterLeaving = -1;
            yield return rig.Run(rig.SecondsToWalk(0.8f, 0.3f), _ => CharacterTestRig.Move(Vector3.forward), _ =>
            {
                if (rig.Grounded) { return; }
                if (++airborneSteps == 2) { jumpsShortlyAfterLeaving = rig.Movement.jumpsRemaining; }
            });

            Assert.That(jumpsShortlyAfterLeaving, Is.EqualTo(maxJumps), "Walking off a ledge should keep every jump.");
        }

        [UnityTest]
        public IEnumerator WalkingOffLedge_KeepsHorizontalSpeed()
        {
            rig.Box("Ledge", new Vector3(0f, -0.5f, -10f), new Vector3(10f, 1f, 20f));
            rig.Floor(-2f);
            rig.SpawnCharacter(new Vector3(0f, 0.05f, -1.5f));
            yield return rig.Run(0.3f, _ => CharacterTestRig.Idle());
            float walkSpeed = rig.Mode<WalkRunMode>().walkSpeed;

            float dt = Time.fixedDeltaTime;
            float slowestInAir = float.PositiveInfinity;
            Vector3? previous = null;
            yield return rig.Run(rig.SecondsToWalk(1.5f, 1f), _ => CharacterTestRig.Move(Vector3.forward), _ =>
            {
                Vector3 p = rig.Position;
                if (previous.HasValue && !rig.Grounded && p.y > -1.9f)
                {
                    Vector3 d = p - previous.Value;
                    slowestInAir = Mathf.Min(slowestInAir, new Vector2(d.x, d.z).magnitude / dt);
                }
                previous = p;
            });

            Assert.That(slowestInAir, Is.Not.EqualTo(float.PositiveInfinity), "Test setup: the character should have been in the air.");
            Assert.That(slowestInAir, Is.GreaterThan(0.9f * walkSpeed), "Character halted at the edge instead of carrying its speed off it.");
        }

        [UnityTest]
        public IEnumerator WalkingDownStairs_KeepsSpeed_AndFollowsTheSteps()
        {
            // A landing, then six steps down along +Z to the floor, each a little over half the step height.
            rig.Floor();
            rig.SpawnCharacter(new Vector3(0f, 0.05f, -1.5f));
            float rise = 0.55f * rig.StepHeight, run = 0.35f, top = 6f * rise;
            rig.Box("Top Landing", new Vector3(0f, top / 2f, -5f), new Vector3(4f, top, 10f));
            for (int i = 0; i < 6; i++)
            {
                float height = top - rise * (i + 1);
                if (height <= 1e-4f) { break; }
                rig.Box($"Step {i + 1}", new Vector3(0f, height / 2f, i * run + run / 2f), new Vector3(4f, height, run));
            }
            rig.Body.position = new Vector3(0f, top + 0.05f, -1.5f);
            yield return rig.Run(0.3f, _ => CharacterTestRig.Idle());
            float walkSpeed = rig.Mode<WalkRunMode>().walkSpeed;

            float dt = Time.fixedDeltaTime;
            float slowest = float.PositiveInfinity, biggestGap = 0f;
            Vector3? previous = null;
            yield return rig.Run(rig.SecondsToWalk(1.5f + 6f * run, 0.2f), _ => CharacterTestRig.Move(Vector3.forward), _ =>
            {
                Vector3 p = rig.Position;
                if (previous.HasValue && p.z > -0.5f && p.z < 6 * run + 0.5f)
                {
                    Vector3 d = p - previous.Value;
                    slowest = Mathf.Min(slowest, new Vector2(d.x, d.z).magnitude / dt);
                    if (Physics.Raycast(p + Vector3.up * 0.1f, Vector3.down, out RaycastHit hit, 5f))
                    {
                        biggestGap = Mathf.Max(biggestGap, hit.distance - 0.1f);
                    }
                }
                previous = p;
            });

            Assert.That(rig.Position.y, Is.InRange(-0.01f, 0.03f), "Character should reach the bottom of the stairs.");
            Assert.That(slowest, Is.GreaterThan(0.85f * walkSpeed), "Character slowed down at the step edges.");
            Assert.That(biggestGap, Is.LessThan(1.25f * rise), "Character floated off the steps instead of walking down them.");
        }

        [UnityTest]
        public IEnumerator AirSteering_DoesNotStackOnCarriedMomentum()
        {
            rig.Floor();
            rig.SpawnCharacter(new Vector3(0f, 0.05f, 0f));
            yield return rig.Run(0.3f, _ => CharacterTestRig.Idle());
            float walkSpeed = rig.Mode<WalkRunMode>().walkSpeed;

            // Run, jump while running, and keep steering forward through the jump.
            yield return rig.Run(0.3f, _ => CharacterTestRig.Move(Vector3.forward));
            float fastest = 0f;
            yield return rig.Run(0.8f, step => step < 5 ? CharacterTestRig.Jump(step, Vector3.forward) : CharacterTestRig.Move(Vector3.forward), _ =>
            {
                Vector3 v = rig.Movement.lastFrameMovementData.finalVelocity;
                if (!rig.Grounded) { fastest = Mathf.Max(fastest, new Vector2(v.x, v.z).magnitude); }
            });

            Assert.That(fastest, Is.GreaterThan(0.9f * walkSpeed), "Jumping while running should keep the running speed.");
            Assert.That(fastest, Is.LessThan(1.05f * walkSpeed), "Air steering stacked on top of the carried momentum.");
        }

        [UnityTest]
        public IEnumerator JumpPressedJustBeforeLanding_JumpsOnLanding()
        {
            rig.Floor();
            rig.SpawnCharacter(new Vector3(0f, 0.05f, 0f));
            yield return rig.Run(0.3f, _ => CharacterTestRig.Idle());
            // Use up every jump, so the buffered press can only be honoured by landing.
            int jumps = rig.Mode<JumpMode>().maxJumps;
            for (int i = 0; i < jumps; i++)
            {
                if (i > 0) { yield return rig.Run(2, _ => CharacterTestRig.Idle()); }
                yield return rig.Run(0.15f, step => CharacterTestRig.Jump(step));
            }

            bool pressing = false, landed = false;
            int pressStart = 0;
            float highestAfterLanding = 0f;
            yield return rig.Run(2.5f, step => pressing ? CharacterTestRig.Jump(step - pressStart) : CharacterTestRig.Idle(), step =>
            {
                Vector3 v = rig.Movement.lastFrameMovementData.finalVelocity;
                // Press jump while falling, shortly before touching down.
                if (!pressing && !landed && v.y < 0f && rig.Position.y < 0.25f) { pressing = true; pressStart = step + 1; }
                // Started the step on the ground. (Not rig.Grounded: a jump clears the Grounded state on the step it starts.)
                landed |= rig.Movement.lastFrameMovementData.initialGrounded;
                if (landed) { highestAfterLanding = Mathf.Max(highestAfterLanding, rig.Position.y); }
            });

            Assert.That(pressing, Is.True, "Test setup: jump should have been pressed before landing.");
            Assert.That(highestAfterLanding, Is.GreaterThan(rig.StepHeight), "A jump pressed just before landing should jump on landing.");
        }

        [UnityTest]
        public IEnumerator HoldingJumpThroughLanding_DoesNotJumpAgain()
        {
            rig.Floor();
            rig.SpawnCharacter(new Vector3(0f, 2f, 0f));

            // Holding jump the whole time, pressed while airborne with no jumps available.
            bool landed = false;
            float highestAfterLanding = 0f;
            yield return rig.Run(2f, step => CharacterTestRig.Jump(step), _ =>
            {
                landed |= rig.Grounded;
                if (landed) { highestAfterLanding = Mathf.Max(highestAfterLanding, rig.Position.y); }
            });

            Assert.That(landed, Is.True);
            Assert.That(highestAfterLanding, Is.LessThan(0.05f), "Holding jump should not start a new jump on landing.");
        }

        /// <summary>
        /// Any step up to the step height is climbed, at walking pace as well as running. Steps that are a large fraction
        /// of the capsule's radius used to leave it perched on the step's edge, which the ground check saw as too steep,
        /// so it slid back off: nothing over about 0.25 m was climbed, though the step height is 0.35 m.
        /// </summary>
        [UnityTest]
        public IEnumerator ClimbsAnyStepUpToTheStepHeight([Values(0.7f, 0.85f, 0.97f)] float fraction, [Values(0.3f, 1f)] float pace)
        {
            rig.Floor();
            rig.SpawnCharacter(new Vector3(0f, 0.05f, 0f));
            float height = fraction * rig.StepHeight, start = rig.Radius + 1.5f;
            rig.Box("Step", new Vector3(0f, height / 2f, start + 20f), new Vector3(10f, height, 40f));
            yield return rig.Run(rig.SecondsToWalk((start + 1f) / pace, 1.5f), _ => CharacterTestRig.Move(Vector3.forward * pace));

            Assert.That(rig.Position.z, Is.GreaterThan(start + 1f), "The character should have got onto the step.");
            Assert.That(rig.Position.y, Is.EqualTo(height).Within(0.01f), "It should be standing on the step.");
            Assert.That(rig.Grounded, Is.True);
        }

        [UnityTest]
        public IEnumerator DoesNotClimbStepsTallerThanTheStepHeight()
        {
            rig.Floor();
            rig.SpawnCharacter(new Vector3(0f, 0.05f, 0f));
            float height = 1.15f * rig.StepHeight, start = rig.Radius + 1.5f;
            rig.Box("Step", new Vector3(0f, height / 2f, start + 20f), new Vector3(10f, height, 40f));
            yield return rig.Run(rig.SecondsToWalk(start + 1f, 1f), _ => CharacterTestRig.Move(Vector3.forward));

            Assert.That(rig.Position.y, Is.EqualTo(0f).Within(0.01f));
            Assert.That(rig.Position.z, Is.LessThan(start), "A step taller than the step height should stop the character.");
        }

        [UnityTest]
        public IEnumerator ClimbsStairs()
        {
            rig.Floor();
            rig.SpawnCharacter(new Vector3(0f, 0.05f, 0f));
            // Six steps, each a little over half the step height, starting just ahead, then a long landing.
            float rise = 0.55f * rig.StepHeight, run = 0.35f, start = rig.Radius + 0.5f, top = 6f * rise;
            for (int i = 0; i < 6; i++)
            {
                float height = rise * (i + 1);
                rig.Box($"Step {i + 1}", new Vector3(0f, height / 2f, start + i * run + run / 2f), new Vector3(4f, height, run));
            }
            rig.Box("Landing", new Vector3(0f, top / 2f, start + 6 * run + 10f), new Vector3(4f, top, 20f));

            yield return rig.Run(rig.SecondsToWalk(start + 6f * run + 1f), _ => CharacterTestRig.Move(Vector3.forward));

            Assert.That(rig.Position.z, Is.GreaterThan(start + 6 * run), "Character didn't get past the stairs.");
            Assert.That(rig.Position.y, Is.InRange(top - 0.01f, top + 0.03f), "Character should be standing on the landing.");
        }

        [UnityTest]
        public IEnumerator WalkingAndSprintingDownALongFlightOfStairsNearTheStepHeight_StaysOnTheSteps_AtAnEvenSpeed(
            [Values(false, true)] bool sprint, [Values(0f, 0.137f)] float startOffset, [Values(0f, 25f)] float inputAngle)
        {
            // Forty steps down along +Z, each nearly the step height and shallower than the character is wide (as the
            // level's are), so it rests on their edges all the way down. Long, because trouble used to show only part
            // way down (where the character's middle happened to come down exactly on the line between two treads), and
            // from different starting points and headings, so the character's path crosses the treads differently.
            rig.Floor(0f, 200f);
            rig.SpawnCharacter(new Vector3(0f, 0.05f, -1.5f));
            if (sprint && !(rig.RunSpeed > rig.WalkSpeed)) { Assert.Ignore("The character doesn't sprint."); }
            const int steps = 40;
            float rise = 0.85f * rig.StepHeight, run = 0.9f * rig.Radius, top = steps * rise, width = 40f;
            rig.Box("Top Landing", new Vector3(0f, top / 2f, -5f), new Vector3(width, top, 10f));
            for (int i = 0; i < steps - 1; i++)
            {
                float height = top - rise * (i + 1);
                rig.Box($"Step {i + 1}", new Vector3(0f, height / 2f, i * run + run / 2f), new Vector3(width, height, run));
            }
            rig.Body.position = new Vector3(0f, top + 0.05f, -1.5f - startOffset);
            yield return rig.Run(0.3f, _ => CharacterTestRig.Idle());
            Assert.That(rig.Grounded, Is.True, "Test setup: the character should stand at the top of the stairs.");

            Vector3 heading = Quaternion.Euler(0f, inputAngle, 0f) * Vector3.forward;
            float speed = sprint ? rig.RunSpeed : rig.WalkSpeed;
            int airborneSteps = 0;
            float fastest = 0f;
            yield return rig.Run((1.5f + steps * run) / (speed * heading.z) + 0.5f,
                step => sprint ? CharacterTestRig.Sprint(heading, step) : CharacterTestRig.Move(heading), _ =>
            {
                if (rig.Position.z < 0f || rig.Position.z > steps * run) { return; }
                if (!rig.Grounded) { airborneSteps++; }
                fastest = Mathf.Max(fastest, rig.HorizontalSpeed);
            });

            Assert.That(rig.Position.y, Is.InRange(-0.01f, 0.03f), "The character should reach the bottom of the stairs.");
            Assert.That(airborneSteps, Is.Zero, "The character left the steps instead of walking down them.");
            Assert.That(fastest, Is.LessThanOrEqualTo(1.02f * speed), "The character sped up going down the stairs.");
        }

        [UnityTest]
        public IEnumerator IdleOnRamp_DoesNotSlide()
        {
            // A ramp a little over half as steep as the steepest walkable slope, rising along +Z from the origin. (The
            // character is spawned first, to read its slope limit, then put on the ramp.)
            rig.SpawnCharacter(new Vector3(0f, 50f, 0f));
            float angle = 0.55f * rig.MaxSlopeAngle;
            Quaternion tilt = Quaternion.Euler(-angle, 0f, 0f);
            rig.Box("Ramp", tilt * new Vector3(0f, -0.25f, 5f), new Vector3(6f, 0.5f, 10f), tilt);
            float surfaceHeight = Mathf.Tan(angle * Mathf.Deg2Rad) * 3f;
            rig.Body.position = new Vector3(0f, surfaceHeight + 0.4f, 3f);

            yield return rig.Run(1f, _ => CharacterTestRig.Idle());
            Assert.That(rig.Grounded, Is.True, "Character should be standing on the ramp.");
            Vector3 settled = rig.Position;

            yield return rig.Run(2f, _ => CharacterTestRig.Idle());

            Vector3 drift = rig.Position - settled;
            Assert.That(new Vector2(drift.x, drift.z).magnitude, Is.LessThan(0.02f), "Character slid down the ramp while idle.");
            Assert.That(Mathf.Abs(drift.y), Is.LessThan(0.02f), "Character slid down the ramp while idle.");
        }
    }
}
