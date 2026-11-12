using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CharacterShowcase.Tests
{
    /// <summary>
    /// Tests of BasicMovementMode at the edges of its settings: zero speeds, no jumps or many, no gravity or a lot, speeds
    /// fast enough to cross a wall's thickness in one physics step, dashes with no length, speed or cooldown, no air
    /// dashes, and no coyote time or input buffers. Each sets the values it needs on the rig's own copy of the
    /// character's movement mode (CharacterTestRig.SetMovementSetting), so they hold however the asset is tuned and
    /// leave it alone. They check the character keeps moving sensibly (no NaNs, no passing through walls or floors,
    /// no stuck states) and that each setting does what its tooltip says at its extremes.
    /// </summary>
    public class EdgeCaseTuningTests
    {
        private CharacterTestRig rig;
        private readonly List<(int step, MovementEvents movementEvent)> events = new();
        private int step;

        [SetUp]
        public void SetUp()
        {
            rig = new CharacterTestRig();
            events.Clear();
            step = 0;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            rig.Dispose();
            yield return null;
        }

        /// <summary>Spawns the character on a floor, records its movement events, and lets it settle.</summary>
        private IEnumerator Settle(Vector3? at = null)
        {
            rig.Floor();
            rig.SpawnCharacter(at ?? new Vector3(0f, 0.05f, 0f));
            rig.Movement.MovementEventRaised += (movementEvent, _) => events.Add((step, movementEvent));
            yield return Run(0.3f, _ => CharacterTestRig.Idle());
        }

        /// <summary>Runs the rig, counting steps across runs so events can be told apart by when they happened.</summary>
        private IEnumerator Run(float seconds, System.Func<int, Inputs> inputs, System.Action<int> afterStep = null) =>
            Run(CharacterTestRig.StepsFor(seconds), inputs, afterStep);

        private IEnumerator Run(int steps, System.Func<int, Inputs> inputs, System.Action<int> afterStep = null) =>
            rig.Run(steps, i => { step++; return inputs(i); }, afterStep);

        private int Count(MovementEvents movementEvent) => events.Count(e => e.movementEvent == movementEvent);

        private static bool IsFinite(Vector3 v) => float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z);

        private static bool IsFinite(Quaternion q) => float.IsFinite(q.x) && float.IsFinite(q.y) && float.IsFinite(q.z) && float.IsFinite(q.w);

        private void AssertSane(string when)
        {
            Assert.That(IsFinite(rig.Position), Is.True, $"{when}, the character's position became {rig.Position}.");
            Assert.That(IsFinite(rig.Body.rotation), Is.True, $"{when}, the character's rotation became {rig.Body.rotation}.");
            Assert.That(IsFinite(rig.Body.linearVelocity), Is.True, $"{when}, the character's velocity became {rig.Body.linearVelocity}.");
        }

        // ---- Speeds ----

        [UnityTest]
        public IEnumerator ZeroWalkAndRunSpeed_StaysPut_WithoutBreaking()
        {
            yield return Settle();
            rig.SetMovementSetting("walkSpeed", 0f);
            rig.SetMovementSetting("runSpeed", 0f);
            Vector3 start = rig.Position;
            Quaternion facing = rig.Body.rotation;

            yield return Run(0.5f, _ => CharacterTestRig.Move(Vector3.right), _ => AssertSane("Walking at zero speed"));
            yield return Run(0.5f, i => CharacterTestRig.Sprint(Vector3.left, i), _ => AssertSane("Sprinting at zero speed"));

            Vector3 moved = rig.Position - start;
            Assert.That(new Vector2(moved.x, moved.z).magnitude, Is.LessThan(0.01f), "A character with no speed shouldn't move.");
            Assert.That(rig.Grounded, Is.True);
            Assert.That(Quaternion.Angle(facing, rig.Body.rotation), Is.LessThan(1f), "A character with no speed shouldn't turn.");
        }

        [UnityTest]
        public IEnumerator RunSpeedNotAboveWalkSpeed_TurnsSprintingOff([Values(0.5f, 1f)] float runSpeedOfWalkSpeed)
        {
            yield return Settle();
            float walkSpeed = rig.Mode<WalkRunMode>().walkSpeed;
            rig.SetMovementSetting("runSpeed", walkSpeed * runSpeedOfWalkSpeed);

            // Get up to speed, then time a second of sprinting.
            yield return Run(0.5f, i => CharacterTestRig.Sprint(Vector3.forward, i));
            float startZ = rig.Position.z;
            yield return Run(1f, i => CharacterTestRig.Sprint(Vector3.forward, i + 25));
            float speed = (rig.Position.z - startZ) / (CharacterTestRig.StepsFor(1f) * Time.fixedDeltaTime);

            Assert.That(speed, Is.EqualTo(rig.WalkSpeed).Within(0.05f * rig.WalkSpeed),
                "With a run speed no faster than walking, Sprint should walk (as the README says, it turns sprinting off).");
        }

        // ---- Jumps ----

        [UnityTest]
        public IEnumerator NoJumps_NeverLeavesTheGround()
        {
            yield return Settle();
            rig.SetMovementSetting("maxJumps", 0);

            float highest = 0f;
            for (int press = 0; press < 3; press++)
            {
                yield return Run(0.3f, i => CharacterTestRig.Jump(i), _ => highest = Mathf.Max(highest, rig.Position.y));
                yield return Run(0.1f, _ => CharacterTestRig.Idle());
            }

            Assert.That(Count(MovementEvents.Jumped) + Count(MovementEvents.AirJumped), Is.EqualTo(0), "A character with no jumps jumped.");
            Assert.That(highest, Is.LessThan(0.05f), "A character with no jumps left the ground.");
            Assert.That(rig.Grounded, Is.True);
        }

        [UnityTest]
        public IEnumerator ManyJumps_GivesExactlyThatMany()
        {
            yield return Settle();
            const int maxJumps = 6;
            rig.SetMovementSetting("maxJumps", maxJumps);
            // Light gravity, so the character stays up through every press rather than landing and getting its jumps back.
            rig.SetMovementSetting("gravityStrength", rig.MovementSetting<float>("gravityStrength") * 0.05f);

            const int presses = maxJumps + 3;
            for (int press = 0; press < presses; press++)
            {
                yield return Run(0.1f, i => CharacterTestRig.Jump(i));
                yield return Run(0.05f, _ => CharacterTestRig.Idle());
            }

            Assert.That(rig.Grounded, Is.False, "Test setup: the character should still be in the air.");
            Assert.That(Count(MovementEvents.Jumped), Is.EqualTo(1), "The first jump should be from the ground.");
            Assert.That(Count(MovementEvents.AirJumped), Is.EqualTo(maxJumps - 1), $"{presses} presses should give exactly {maxJumps} jumps.");
            Assert.That(rig.Movement.jumpsRemaining, Is.EqualTo(0));
        }

        // ---- Gravity ----

        [UnityTest]
        public IEnumerator NoGravity_AJumpRisesAndDoesntComeDown_WithoutBreaking()
        {
            yield return Settle();
            rig.SetMovementSetting("gravityStrength", 0f);

            float lowestAfterTakeOff = float.PositiveInfinity;
            yield return Run(0.2f, i => CharacterTestRig.Jump(i), _ => AssertSane("Jumping without gravity"));
            float atRelease = rig.Position.y;
            yield return Run(1.5f, _ => CharacterTestRig.Idle(), _ =>
            {
                AssertSane("Drifting without gravity");
                lowestAfterTakeOff = Mathf.Min(lowestAfterTakeOff, rig.Position.y);
            });

            Assert.That(atRelease, Is.GreaterThan(0.1f), "The jump should leave the ground.");
            Assert.That(lowestAfterTakeOff, Is.GreaterThanOrEqualTo(atRelease - 0.01f), "Without gravity, nothing should bring the character back down.");
            Assert.That(rig.Grounded, Is.False);
        }

        [UnityTest]
        public IEnumerator HeavyGravity_FallsFastWithoutPassingThroughTheFloor()
        {
            rig.Floor();
            rig.SpawnCharacter(new Vector3(0f, 30f, 0f));
            // Fifty times the gravity, with no fall speed limit: by the time it lands it falls several metres in each
            // physics step, more than the floor's (1 m) thickness.
            rig.SetMovementSetting("gravityStrength", rig.MovementSetting<float>("gravityStrength") * 50f);
            rig.SetMovementSetting("maxFallSpeed", 0f);

            float lowest = float.PositiveInfinity;
            yield return Run(2f, _ => CharacterTestRig.Idle(), _ =>
            {
                AssertSane("Falling under heavy gravity");
                lowest = Mathf.Min(lowest, rig.Position.y);
            });

            Assert.That(lowest, Is.GreaterThan(-0.01f), "The character went into (or through) the floor.");
            Assert.That(rig.Grounded, Is.True, "The character should have landed.");
            Assert.That(rig.Position.y, Is.InRange(-0.01f, 0.03f), "The character should stand on the floor.");

            // And jumps and lands again under it.
            yield return Run(0.2f, i => CharacterTestRig.Jump(i));
            yield return Run(1f, _ => CharacterTestRig.Idle(), _ => lowest = Mathf.Min(lowest, rig.Position.y));
            Assert.That(lowest, Is.GreaterThan(-0.01f), "Landing from a jump, the character went into the floor.");
            Assert.That(rig.Grounded, Is.True);
        }

        // ---- Very high speeds ----

        /// <summary>
        /// A wall a tenth of a metre thick, met at speeds that cover metres in each physics step: the capsule sweeps
        /// should stop the character at it rather than step it through.
        /// </summary>
        [UnityTest]
        public IEnumerator VeryHighSpeed_DoesntPassThroughAThinWall([Values("walking", "dashing")] string how)
        {
            yield return Settle();
            float radius = rig.Radius;
            const float face = 6f, thickness = 0.1f;
            rig.Box("Thin Wall", new Vector3(0f, rig.Height, face + thickness / 2f), new Vector3(10f, 2f * rig.Height, thickness));
            float furthest = float.NegativeInfinity;
            System.Action<int> track = _ =>
            {
                AssertSane($"{how} into a thin wall");
                furthest = Mathf.Max(furthest, rig.Position.z);
            };

            if (how == "walking")
            {
                rig.SetMovementSetting("walkSpeed", 200f); // 4 m per physics step.
                yield return Run(0.5f, _ => CharacterTestRig.Move(Vector3.forward), track);
            }
            else
            {
                rig.SetMovementSetting("dashSpeed", 500f); // 10 m per physics step.
                yield return Run(0.5f, i => i == 0 ? CharacterTestRig.Dash(0, Vector3.forward) : CharacterTestRig.Idle(), track);
            }

            Assert.That(furthest, Is.LessThanOrEqualTo(face - radius + 0.01f), $"{how} at speed, the character went into or through the wall.");
            Assert.That(rig.Position.z, Is.GreaterThan(face - radius - 0.5f), "The character should have reached the wall.");
        }

        // ---- Dashes ----

        [UnityTest]
        public IEnumerator ZeroDashDuration_DoesntDash()
        {
            yield return Settle();
            rig.SetMovementSetting("dashDuration", 0f);
            Vector3 start = rig.Position;

            // No stick input with the press, which would walk the character a step: a dash would go straight ahead anyway.
            yield return Run(0.3f, i => i == 0 ? CharacterTestRig.Dash(0) : CharacterTestRig.Idle(), _ =>
            {
                AssertSane("Dashing for no time");
                Assert.That(rig.Dashing, Is.False, "A dash that lasts no time shouldn't be dashing.");
            });

            Assert.That(Count(MovementEvents.DashStarted), Is.EqualTo(0), "A dash that lasts no time shouldn't start (it would never end).");
            Assert.That(Count(MovementEvents.DashEnded), Is.EqualTo(0));
            Assert.That(Vector3.Distance(start, rig.Position), Is.LessThan(0.01f));
        }

        [UnityTest]
        public IEnumerator ZeroDashSpeed_HoldsStillForTheDash_ThenCarriesOn()
        {
            yield return Settle();
            rig.SetMovementSetting("dashSpeed", 0f);
            float duration = rig.Mode<DashMode>().dashDuration;
            Vector3 start = rig.Position;

            yield return Run(duration + 0.1f, i => i == 0 ? CharacterTestRig.Dash(0, Vector3.right) : CharacterTestRig.Idle(),
                _ => AssertSane("Dashing at no speed"));

            Assert.That(Count(MovementEvents.DashStarted), Is.EqualTo(1));
            Assert.That(Count(MovementEvents.DashEnded), Is.EqualTo(1), "The dash should still end.");
            Assert.That(rig.Dashing, Is.False);
            Assert.That(Vector3.Distance(start, rig.Position), Is.LessThan(0.01f), "A dash with no speed shouldn't move the character.");

            yield return Run(0.5f, _ => CharacterTestRig.Move(Vector3.forward));
            Assert.That(rig.Position.z - start.z, Is.GreaterThan(0.25f * rig.WalkSpeed * 0.5f), "The character should walk normally afterwards.");
        }

        [UnityTest]
        public IEnumerator EmptyDashSpeedCurve_DashesWithoutBreaking()
        {
            yield return Settle();
            // No keys at all: it evaluates to 0 throughout, leaving only the hand-over to momentum on the last step.
            rig.SetMovementSetting("dashSpeedCurve", new AnimationCurve());
            float duration = rig.Mode<DashMode>().dashDuration;
            float endSpeed = rig.Mode<DashMode>().dashSpeed * rig.MovementSetting<float>("dashEndSpeedRetention");
            Vector3 start = rig.Position;

            yield return Run(duration + 0.02f, i => i == 0 ? CharacterTestRig.Dash(0, Vector3.right) : CharacterTestRig.Idle(),
                _ => AssertSane("Dashing on an empty speed curve"));

            Assert.That(Count(MovementEvents.DashEnded), Is.EqualTo(1), "The dash should still end.");
            Assert.That(rig.Dashing, Is.False);
            Assert.That(rig.Position.x - start.x, Is.LessThan(endSpeed * 3f * Time.fixedDeltaTime + 0.01f),
                "With no speed on its curve, the dash should only move the character by its hand-over to momentum.");
        }

        [UnityTest]
        public IEnumerator NoDashCooldown_StillDashesOncePerPress()
        {
            yield return Settle();
            rig.SetMovementSetting("dashCooldown", 0f);
            float duration = rig.Mode<DashMode>().dashDuration;

            // Held through three dashes' worth of time: one dash.
            yield return Run(3f * duration, i => CharacterTestRig.Dash(i, Vector3.right));
            Assert.That(Count(MovementEvents.DashStarted), Is.EqualTo(1), "Holding dash should dash once, even with no cooldown.");

            // Let go, and press again: straight away, another.
            yield return Run(1, _ => CharacterTestRig.Idle());
            yield return Run(1, _ => CharacterTestRig.Dash(0, Vector3.left));
            Assert.That(Count(MovementEvents.DashStarted), Is.EqualTo(2), "With no cooldown, a new press should dash at once.");
        }

        [UnityTest]
        public IEnumerator NoAirDashes_DashesOnTheGroundOnly()
        {
            yield return Settle();
            rig.SetMovementSetting("maxAirDashes", 0);

            yield return Run(0.15f, i => CharacterTestRig.Jump(i));
            Assert.That(rig.Grounded, Is.False, "Test setup: the character should be in the air.");
            yield return Run(0.1f, i => i == 0 ? CharacterTestRig.Dash(0, Vector3.right) : CharacterTestRig.Idle());
            Assert.That(Count(MovementEvents.DashStarted), Is.EqualTo(0), "A character with no air dashes dashed in the air.");

            yield return Run(2f, _ => CharacterTestRig.Idle());
            Assert.That(rig.Grounded, Is.True, "Test setup: the character should have landed.");
            yield return Run(0.1f, i => i == 0 ? CharacterTestRig.Dash(0, Vector3.right) : CharacterTestRig.Idle());
            Assert.That(Count(MovementEvents.DashStarted), Is.EqualTo(1), "It should still dash on the ground.");
        }

        // ---- Timing windows ----

        [UnityTest]
        public IEnumerator NoJumpInputBuffer_OnlyAFreshPressJumps()
        {
            yield return Settle();
            rig.SetMovementSetting("jumpInputBuffer", 0f);

            // A press already held for a step when it's first seen is outside a buffer of 0.
            yield return Run(0.2f, i => CharacterTestRig.Jump(i + 1));
            Assert.That(Count(MovementEvents.Jumped), Is.EqualTo(0), "With no input buffer, a press held from before shouldn't jump.");

            yield return Run(1, _ => CharacterTestRig.Idle());
            yield return Run(0.1f, i => CharacterTestRig.Jump(i));
            Assert.That(Count(MovementEvents.Jumped), Is.EqualTo(1), "A press on the step it happens should still jump.");
        }

        [UnityTest]
        public IEnumerator NoCoyoteTime_AJumpJustAfterWalkingOffIsAnAirJump()
        {
            rig.Box("Ledge", new Vector3(0f, -0.5f, -10f), new Vector3(10f, 1f, 20f));
            rig.Floor(-5f);
            rig.SpawnCharacter(new Vector3(0f, 0.05f, -0.8f));
            rig.Movement.MovementEventRaised += (movementEvent, _) => events.Add((step, movementEvent));
            rig.SetMovementSetting("coyoteTime", 0f);
            if (rig.Mode<JumpMode>().maxJumps < 2) { rig.SetMovementSetting("maxJumps", 2); }
            yield return Run(0.3f, _ => CharacterTestRig.Idle());

            // Walk off, and press jump two steps after leaving the ground.
            int airborneSteps = 0;
            yield return Run(rig.SecondsToWalk(0.8f, 0.5f), i => airborneSteps == 2 ? CharacterTestRig.Jump(0, Vector3.forward) : CharacterTestRig.Move(Vector3.forward),
                _ => { if (!rig.Grounded || airborneSteps > 0) { airborneSteps++; } });

            Assert.That(airborneSteps, Is.GreaterThan(2), "Test setup: the character should have walked off the ledge.");
            Assert.That(Count(MovementEvents.Jumped), Is.EqualTo(0), "With no coyote time, a jump after leaving the ground isn't from the ground.");
            Assert.That(Count(MovementEvents.AirJumped), Is.EqualTo(1), "It should be an air jump instead.");
        }
    }
}
