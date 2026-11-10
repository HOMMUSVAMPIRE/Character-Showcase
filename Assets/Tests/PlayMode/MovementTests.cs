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
            yield return rig.Run(1.5f, _ => CharacterTestRig.Move(Vector3.forward), step =>
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
            yield return rig.Run(1f, _ => CharacterTestRig.Move(Vector3.forward), _ =>
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
            // Wall face at z = 2.
            rig.Box("Wall", new Vector3(0f, 1.5f, 2.5f), new Vector3(10f, 3f, 1f));
            rig.SpawnCharacter(new Vector3(0f, 0.05f, 0f));
            float radius = rig.Character.GetComponent<CapsuleCollider>().radius;

            float furthest = float.NegativeInfinity;
            yield return rig.Run(1f, _ => CharacterTestRig.Move(Vector3.forward), _ => furthest = Mathf.Max(furthest, rig.Position.z));

            Assert.That(furthest, Is.LessThanOrEqualTo(2f - radius + 0.01f), "Character pushed into the wall.");
            Assert.That(rig.Position.z, Is.GreaterThan(2f - radius - 0.1f), "Character stopped well short of the wall.");
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

            Assert.That(apex, Is.GreaterThan(0.5f), "Jump barely left the ground.");
            Assert.That(rig.Grounded, Is.True, "Character should have landed.");
            Assert.That(rig.Position.y, Is.InRange(-0.01f, 0.03f), "Character should land on the floor, not above or inside it.");
        }

        [UnityTest]
        public IEnumerator TapJump_DoesNotGetItsJumpRefundedByCoyoteTime()
        {
            rig.Floor();
            rig.SpawnCharacter(new Vector3(0f, 0.05f, 0f));
            yield return rig.Run(0.3f, _ => CharacterTestRig.Idle());
            int maxJumps = rig.MovementSetting<int>("maxJumps");

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
            int maxJumps = rig.MovementSetting<int>("maxJumps");

            // Walk off, then check the jumps just after leaving the ground.
            int airborneSteps = 0;
            int jumpsShortlyAfterLeaving = -1;
            yield return rig.Run(0.5f, _ => CharacterTestRig.Move(Vector3.forward), _ =>
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
            float walkSpeed = rig.MovementSetting<float>("walkSpeed");

            float dt = Time.fixedDeltaTime;
            float slowestInAir = float.PositiveInfinity;
            Vector3? previous = null;
            yield return rig.Run(1f, _ => CharacterTestRig.Move(Vector3.forward), _ =>
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
            // A landing at 1.2 m, then six 0.2 m steps down along +Z to the floor.
            const float rise = 0.2f, run = 0.35f, top = 1.2f;
            rig.Floor();
            rig.Box("Top Landing", new Vector3(0f, top / 2f, -5f), new Vector3(4f, top, 10f));
            for (int i = 0; i < 6; i++)
            {
                float height = top - rise * (i + 1);
                if (height <= 0f) { break; }
                rig.Box($"Step {i + 1}", new Vector3(0f, height / 2f, i * run + run / 2f), new Vector3(4f, height, run));
            }
            rig.SpawnCharacter(new Vector3(0f, top + 0.05f, -1.5f));
            yield return rig.Run(0.3f, _ => CharacterTestRig.Idle());
            float walkSpeed = rig.MovementSetting<float>("walkSpeed");

            float dt = Time.fixedDeltaTime;
            float slowest = float.PositiveInfinity, biggestGap = 0f;
            Vector3? previous = null;
            yield return rig.Run(0.6f, _ => CharacterTestRig.Move(Vector3.forward), _ =>
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
            Assert.That(biggestGap, Is.LessThan(0.25f), "Character floated off the steps instead of walking down them.");
        }

        [UnityTest]
        public IEnumerator AirSteering_DoesNotStackOnCarriedMomentum()
        {
            rig.Floor();
            rig.SpawnCharacter(new Vector3(0f, 0.05f, 0f));
            yield return rig.Run(0.3f, _ => CharacterTestRig.Idle());
            float walkSpeed = rig.MovementSetting<float>("walkSpeed");

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
            // Use up both jumps, so the buffered press can only be honoured by landing.
            yield return rig.Run(0.15f, step => CharacterTestRig.Jump(step));
            yield return rig.Run(2, _ => CharacterTestRig.Idle());
            yield return rig.Run(0.15f, step => CharacterTestRig.Jump(step));

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
            Assert.That(highestAfterLanding, Is.GreaterThan(0.5f), "A jump pressed just before landing should jump on landing.");
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

        [UnityTest]
        public IEnumerator ClimbsStairs()
        {
            rig.Floor();
            // Six 0.2 m steps (below the 0.35 m step height) starting at z = 1, then a long landing at 1.2 m.
            const float rise = 0.2f, run = 0.35f;
            for (int i = 0; i < 6; i++)
            {
                float height = rise * (i + 1);
                rig.Box($"Step {i + 1}", new Vector3(0f, height / 2f, 1f + i * run + run / 2f), new Vector3(4f, height, run));
            }
            rig.Box("Landing", new Vector3(0f, 0.6f, 1f + 6 * run + 10f), new Vector3(4f, 1.2f, 20f));
            rig.SpawnCharacter(new Vector3(0f, 0.05f, 0f));

            yield return rig.Run(1.5f, _ => CharacterTestRig.Move(Vector3.forward));

            Assert.That(rig.Position.z, Is.GreaterThan(1f + 6 * run), "Character didn't get past the stairs.");
            Assert.That(rig.Position.y, Is.InRange(1.19f, 1.23f), "Character should be standing on the landing.");
        }

        [UnityTest]
        public IEnumerator IdleOnRamp_DoesNotSlide()
        {
            // A 30° ramp rising along +Z from the origin.
            const float angle = 30f;
            Quaternion tilt = Quaternion.Euler(-angle, 0f, 0f);
            rig.Box("Ramp", tilt * new Vector3(0f, -0.25f, 5f), new Vector3(6f, 0.5f, 10f), tilt);
            float surfaceHeight = Mathf.Tan(angle * Mathf.Deg2Rad) * 3f;
            rig.SpawnCharacter(new Vector3(0f, surfaceHeight + 0.4f, 3f));

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
