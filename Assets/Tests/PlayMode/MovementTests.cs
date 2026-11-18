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
    [Category(TestArea.Movement)]
    public class MovementTests : CharacterRigFixture
    {


        /// <summary>
        /// The move input is relative to the input's own look (each frame's: see CharacterInputFrame), not to a camera: with
        /// the scene's main camera turned to face -X, input looking along +X and pushing forward walks along +X. So a
        /// character with no camera of its own (an NPC, a server's copy) steers as its input says.
        /// </summary>
        [UnityTest]
        public IEnumerator TheMoveInput_IsRelativeToTheInputsLook_NotTheCamera()
        {
            yield return rig.StandOnFloor();
            Camera.main.transform.rotation = Quaternion.Euler(0f, -90f, 0f);
            rig.Look = Quaternion.Euler(0f, 90f, 0f);
            yield return rig.Run(0.5f, _ => CharacterTestRig.Move(Vector3.forward));
            Vector3 flat = Vector3.ProjectOnPlane(rig.Velocity, Vector3.up);
            Assume.That(flat.magnitude, Is.GreaterThan(0.5f * rig.WalkSpeed), "Test setup: the character should walk.");
            Assert.That(Vector3.Angle(flat, Vector3.right), Is.LessThan(1f), "Pushing forward, the character should walk the way its input looks (+X).");
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
            float maxFall = rig.Gravity * dt * dt * steps * (steps + 1) / 2f;
            Assert.That(fallen, Is.LessThanOrEqualTo(maxFall + 1e-3f), "Character fell faster than gravity allows.");
            Assert.That(fallen, Is.GreaterThan(0.25f * 0.5f * rig.Gravity * seconds * seconds), "Character barely fell; gravity isn't being applied.");
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
            // The grounded tolerance (how far below the feet the floor still counts as ground: the movement's own, with no
            // setting) and the skin width, per step.
            var groundedTolerance = typeof(MovementController).GetProperty("GroundedTolerance",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(groundedTolerance, Is.Not.Null, "Test setup: MovementController.GroundedTolerance should exist.");
            float snapAllowance = ((float)groundedTolerance.GetValue(rig.Movement) + rig.Movement.Skin) / dt;
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
                    float limit = rig.Gravity * (step - dropStartStep + 1) * dt + snapAllowance;
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
            float fullGravityFall = 0.5f * rig.Gravity * (window * dt) * (window * dt);
            Assert.That(yWhenLeft - yAfterCoyoteWindow, Is.LessThan(0.5f * fullGravityFall),
                "Gravity applied at full strength immediately; the coyote gravity curve isn't being used.");
        }

        [UnityTest]
        public IEnumerator WalkingIntoWall_StopsAtWall()
        {
            rig.Floor();
            rig.SpawnCharacter(new Vector3(0f, 0.05f, 0f));
            float radius = rig.FrontExtent, face = radius + 2f;
            rig.Box("Wall", new Vector3(0f, rig.StandingHeight, face + 0.5f), new Vector3(10f, 2f * rig.StandingHeight, 1f));

            float furthest = float.NegativeInfinity;
            yield return rig.Run(rig.SecondsToWalk(2f), _ => CharacterTestRig.Move(Vector3.forward), _ => furthest = Mathf.Max(furthest, rig.Position.z));

            Assert.That(furthest, Is.LessThanOrEqualTo(face - radius + 0.01f), "Character pushed into the wall.");
            Assert.That(rig.Position.z, Is.GreaterThan(face - radius - 0.1f), "Character stopped well short of the wall.");
        }

        [UnityTest]
        public IEnumerator TapJump_DoesNotGetItsJumpRefundedByCoyoteTime()
        {
            rig.Floor();
            rig.SpawnCharacter(new Vector3(0f, 0.05f, 0f));
            yield return rig.Run(0.3f, _ => CharacterTestRig.Idle());
            int maxJumps = rig.Component<Jumping>().MaxJumps;

            // A one-step tap, released while still inside the coyote window.
            yield return rig.Run(1, _ => CharacterTestRig.Jump());
            yield return rig.Run(CharacterTestRig.StepsFor(rig.MovementSetting<float>("coyoteTime")), _ => CharacterTestRig.Idle());

            Assert.That(rig.Grounded, Is.False, "Test setup: the character should be in the air.");
            Assert.That(rig.Movement.jump.remaining, Is.EqualTo(maxJumps - 1), "The jump just used should stay used.");
        }

        [UnityTest]
        public IEnumerator WalkingOffLedge_KeepsCoyoteJump()
        {
            rig.Box("Ledge", new Vector3(0f, -0.5f, -10f), new Vector3(10f, 1f, 20f));
            rig.Floor(-5f);
            rig.SpawnCharacter(new Vector3(0f, 0.05f, -0.8f));
            yield return rig.Run(0.3f, _ => CharacterTestRig.Idle());
            int maxJumps = rig.Component<Jumping>().MaxJumps;

            // Walk off, then check the jumps just after leaving the ground.
            int airborneSteps = 0;
            int jumpsShortlyAfterLeaving = -1;
            yield return rig.Run(rig.SecondsToWalk(0.8f, 0.3f), _ => CharacterTestRig.Move(Vector3.forward), _ =>
            {
                if (rig.Grounded) { return; }
                if (++airborneSteps == 2) { jumpsShortlyAfterLeaving = rig.Movement.jump.remaining; }
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
            float walkSpeed = rig.Component<WalkingAndRunning>().WalkSpeed;

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
        public IEnumerator JumpPressedJustBeforeLanding_JumpsOnLanding()
        {
            rig.Floor();
            rig.SpawnCharacter(new Vector3(0f, 0.05f, 0f));
            yield return rig.Run(0.3f, _ => CharacterTestRig.Idle());
            // Use up every jump, so the buffered press can only be honoured by landing.
            int jumps = rig.Component<Jumping>().MaxJumps;
            for (int i = 0; i < jumps; i++)
            {
                if (i > 0) { yield return rig.Run(2, _ => CharacterTestRig.Idle()); }
                yield return rig.Run(0.15f, step => CharacterTestRig.Jump());
            }

            bool pressing = false, landed = false;
            int pressStart = 0;
            float highestAfterLanding = 0f;
            yield return rig.Run(2.5f, step => pressing ? CharacterTestRig.Jump() : CharacterTestRig.Idle(), step =>
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
            yield return rig.Run(2f, step => CharacterTestRig.Jump(), _ =>
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
            float height = fraction * rig.StepHeight, start = rig.FrontExtent + 1.5f;
            rig.Box("Step", new Vector3(0f, height / 2f, start + 20f), new Vector3(10f, height, 40f));
            yield return rig.Run(rig.SecondsToWalk((start + 1f) / pace, 1.5f), _ => CharacterTestRig.Move(Vector3.forward * pace));

            Assert.That(rig.Position.z, Is.GreaterThan(start + 1f), "The character should have got onto the step.");
            Assert.That(rig.Position.y, Is.EqualTo(height).Within(0.01f), "It should be standing on the step.");
            Assert.That(rig.Grounded, Is.True);
        }

        /// <summary>
        /// A step taller than the step height stops the character, walking or sprinting, straight at it or at an angle.
        /// Whether a step is short enough is judged by where the body touches down on it, not where its feet end up:
        /// perched part way up a taller step's edge, the feet can be under the step height, and from there the character
        /// used to ride up over the edge. (Vaulting is turned off: sprinting at it, Vaulting steps up onto something a
        /// little taller on purpose.)
        /// </summary>
        [UnityTest]
        public IEnumerator DoesNotClimbStepsTallerThanTheStepHeight([Values(false, true)] bool sprint, [Values(0f, 30f)] float angle)
        {
            rig.Floor();
            rig.SpawnCharacter(new Vector3(0f, 0.05f, 0f));
            if (rig.ComposedMode.Get<Vaulting>(includeDisabled: true) is { } vaulting) { vaulting.Enabled = false; }
            float height = 1.15f * rig.StepHeight, start = rig.FrontExtent + 1.5f;
            rig.Box("Step", new Vector3(0f, height / 2f, start + 20f), new Vector3(40f, height, 40f));
            Vector3 heading = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
            float speed = sprint ? rig.RunSpeed : rig.WalkSpeed;
            yield return rig.Run((start + 1f) / speed + 1f, step => sprint ? CharacterTestRig.Sprint(heading) : CharacterTestRig.Move(heading));

            Assert.That(rig.Position.y, Is.EqualTo(0f).Within(0.01f));
            Assert.That(rig.Position.z, Is.LessThan(start), "A step taller than the step height should stop the character.");
        }

        /// <summary>
        /// Falling, the character is on the ground only once its feet are near the floor, and then settles with them on
        /// it, neither hovering above it nor sunk into it; jumping from there, it rises and lands back on the floor. The
        /// ground check once counted it on the ground up to its sphere's radius (plus the grounded tolerance) above the
        /// floor, cancelling its fall while it was still in the air.
        /// </summary>
        [UnityTest]
        public IEnumerator FallingOrJumping_IsntOnTheGround_UntilItsFeetAreNearTheFloor_ThenRestsOnIt()
        {
            rig.Floor();
            rig.SpawnCharacter(new Vector3(0f, 2f, 0f));
            yield return rig.WaitForModel();
            rig.Teleport(new Vector3(0f, 2f, 0f));
            Vector3 previous = rig.Position;
            float highestGrounded = float.NegativeInfinity;
            yield return rig.Run(1.5f, _ => CharacterTestRig.Idle(), _ =>
            {
                // Grounded as the step started: where the last step left it.
                if (rig.Movement.lastFrameMovementData.initialGrounded) { highestGrounded = Mathf.Max(highestGrounded, previous.y); }
                previous = rig.Position;
            });

            Assert.That(rig.Grounded, Is.True, "The character should have landed.");
            Assert.That(highestGrounded, Is.LessThan(0.5f * rig.StepHeight), "The character counted as on the ground while still well above the floor.");
            Assert.That(rig.Position.y, Is.InRange(-0.01f, 0.03f), "Feet should rest on the floor, not hover above or sink into it.");

            // Jumping from there, it rises and lands back on the floor the same way.
            float apex = 0f;
            int holdSteps = CharacterTestRig.StepsFor(0.2f);
            yield return rig.RunUntilBackOnTheGround(step => step < holdSteps ? CharacterTestRig.Jump() : CharacterTestRig.Idle(),
                _ => apex = Mathf.Max(apex, rig.Position.y));

            Assert.That(apex, Is.GreaterThan(rig.StepHeight), "Jump barely left the ground (not even a step's height).");
            Assert.That(rig.Grounded, Is.True, "Character should have landed from the jump.");
            Assert.That(rig.Position.y, Is.InRange(-0.01f, 0.03f), "Character should land on the floor, not above or inside it.");
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
            // Its model loaded first: the body it may take from it (see CharacterModelHost's Body Fit) is the one tested.
            yield return rig.WaitForModel();
            if (sprint && !(rig.RunSpeed > rig.WalkSpeed)) { Assert.Ignore("The character doesn't sprint."); }
            const int steps = 40;
            // About as steep as the levels' stairs (3 up to 4 along), and shallower than the character is wide. (Treads a
            // share of its reach made a slim body's, fitted to its model, steeper than a walkable slope.)
            float rise = 0.85f * rig.StepHeight, run = Mathf.Min(4f / 3f * rise, 0.9f * 2f * rig.FrontExtent), top = steps * rise, width = 40f;
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
            float dt = Time.fixedDeltaTime;
            int airborneSteps = 0;
            float fastest = 0f, slowest = float.PositiveInfinity;
            Vector3 previous = rig.Position;
            yield return rig.Run((1.5f + steps * run) / (speed * heading.z) + 0.5f,
                step => sprint ? CharacterTestRig.Sprint(heading) : CharacterTestRig.Move(heading), _ =>
            {
                Vector3 p = rig.Position, moved = p - previous;
                previous = p;
                if (p.z < 0f || p.z > steps * run) { return; }
                if (!rig.Grounded) { airborneSteps++; }
                fastest = Mathf.Max(fastest, rig.HorizontalSpeed);
                // How far it actually went along the steps, not the velocity it was given: held up at a step's edge, it
                // doesn't get as far as its velocity says.
                slowest = Mathf.Min(slowest, new Vector2(moved.x, moved.z).magnitude / dt);
            });

            Assert.That(rig.Position.y, Is.InRange(-0.01f, 0.03f), "The character should reach the bottom of the stairs.");
            Assert.That(airborneSteps, Is.Zero, "The character left the steps instead of walking down them.");
            Assert.That(fastest, Is.LessThanOrEqualTo(1.02f * speed), "The character sped up going down the stairs.");
            Assert.That(slowest, Is.GreaterThan(0.85f * speed), "The character slowed down at the step edges.");
        }
    }
}
