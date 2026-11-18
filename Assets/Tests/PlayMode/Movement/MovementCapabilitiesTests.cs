using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CharacterShowcase.Tests.Movement
{
    /// <summary>
    /// Tests of the character's capabilities (MovementController.Capabilities, measured by simulating its movement): they
    /// match what the character itself does in the world (a jump, a sprint jump, a slide, a roll), follow its settings and
    /// its size, leave out what's turned off, and add double jumps when it has air jumps. The test camera looks down +Z.
    /// </summary>
    [Category(TestArea.Movement)]
    public class MovementCapabilitiesTests : CharacterRigFixture
    {

        private MovementCapabilities Capabilities => rig.Movement.Capabilities;
        private float Reach(string move) => Capabilities.Jumps.Concat(Capabilities.Climbs).First(m => m.Name == move).Reach;

        private IEnumerator OnALongFloor()
        {
            rig.Floor(width: 400f);
            yield return rig.SpawnSettled(Vector3.zero);
        }

        /// <summary>How far the character's jump carries it, running (or sprinting) up then Jump held, as the capabilities measure it.</summary>
        private IEnumerator JumpAcross(bool sprint, System.Action<float> distance)
        {
            Inputs Forward(int i) => sprint ? CharacterTestRig.Sprint(Vector3.forward) : CharacterTestRig.Move(Vector3.forward);
            yield return rig.Run(0.8f, Forward);
            float start = rig.Position.z;
            bool left = false, landed = false;
            yield return rig.RunUntil(() => landed, step =>
            {
                Inputs inputs = Forward(step);
                inputs.jump = CharacterTestRig.Held();
                return inputs;
            }, _ => { if (!rig.Grounded) { left = true; } else if (left) { landed = true; } }, 4f);
            distance(landed ? rig.Position.z - start : float.NaN);
        }

        [UnityTest]
        public IEnumerator TheCapabilities_MatchWhatTheCharacterDoes([Values(false, true)] bool sprint)
        {
            yield return OnALongFloor();
            float measured = 0f;
            yield return JumpAcross(sprint, d => measured = d);
            Assert.That(measured, Is.EqualTo(Reach(sprint ? "sprint jump" : "jump")).Within(0.05f),
                "The character's jump should carry it as far as its capabilities say.");
            Assert.That(Capabilities.StepHeight, Is.EqualTo(rig.StepHeight).Within(1e-4f));
            Assert.That(Capabilities.StandHeight, Is.EqualTo(rig.StandingHeight).Within(1e-4f));
            Assert.That(Capabilities.MaxJumpHeight, Is.EqualTo(rig.MovementSetting<float>("maxJumpHeight") * rig.Movement.HeightScale).Within(1e-4f));
        }

        [UnityTest]
        public IEnumerator AMeasurement_IsShared_ByTheSameTuning_AndRemadeForAnyChange()
        {
            yield return OnALongFloor();
            MovementCapabilities first = Capabilities;
            Assert.That(Capabilities, Is.SameAs(first), "Asked again with nothing changed, it shouldn't measure again.");

            // No jumps: the same tuning WhatsTurnedOff_IsLeftOut measures, so the two share one measurement.
            int jumps = rig.MovementSetting<int>("maxJumps");
            rig.SetMovementSetting("maxJumps", 0);
            MovementCapabilities withoutJumps = Capabilities;
            Assert.That(withoutJumps, Is.Not.SameAs(first), "A change of tuning should be measured.");
            rig.SetMovementSetting("maxJumps", jumps);
            Assert.That(Capabilities, Is.SameAs(first), "Tuned back as it was, it should share the measurement made then.");

            // Even a tiny change of size counts. Made without jumps, which a measurement then skips (see
            // BasicMovementMode.MeasureCapabilities: the jumps and climbs are most of its cost), so the size's new
            // measurement is a quick one; the change is still only of size, from the measurement without jumps.
            rig.SetMovementSetting("maxJumps", 0);
            Assume.That(Capabilities, Is.SameAs(withoutJumps), "Test setup: tuned without jumps again, it should share that measurement.");
            rig.Controller.SetScale(new Vector3(1.002f, 1.002f, 1.002f), immediately: true);
            Assert.That(Capabilities, Is.Not.SameAs(withoutJumps), "A change of size should be measured.");
        }

        [UnityTest]
        public IEnumerator TheCapabilities_FollowTheSettings()
        {
            yield return OnALongFloor();
            float jump = Reach("sprint jump"), ledge = Reach("jump to the ledge");
            rig.SetMovementSetting("maxJumpHeight", rig.MovementSetting<float>("maxJumpHeight") + 1f);
            Assert.That(Reach("sprint jump"), Is.GreaterThan(jump + 0.2f), "A higher jump should carry the character further.");
            Assert.That(Reach("jump to the ledge"), Is.EqualTo(ledge + 1f).Within(0.05f), "A jump 1 m higher should reach a ledge 1 m higher.");
        }

        [UnityTest]
        public IEnumerator TheCapabilities_FollowTheCharactersSize()
        {
            yield return OnALongFloor();
            MovementCapabilities authored = Capabilities;
            float jump = Reach("sprint jump"), slide = authored.SlideLength, stand = authored.StandHeight;
            rig.Controller.SetScale(new Vector3(2f, 2f, 2f), immediately: true);
            yield return rig.Run(0.2f, _ => CharacterTestRig.Idle());
            Assume.That(rig.Movement.SizeScaling, Is.EqualTo(MovementSizeScaling.Proportional), "Tests the default, proportional size scaling.");
            Assert.That(Capabilities.StandHeight, Is.EqualTo(2f * stand).Within(1e-3f));
            Assert.That(Reach("sprint jump"), Is.EqualTo(2f * jump).Within(0.02f * jump), "A character twice the size should jump twice as far.");
            Assert.That(Capabilities.SlideLength, Is.EqualTo(2f * slide).Within(0.02f * slide), "A character twice the size should slide twice as far.");
        }

        /// <summary>
        /// Tunes the character with an air jump, and without sliding or wall-running: one tuning, so WhatsTurnedOff_IsLeftOut
        /// and AirJumps_AddDoubleJumps_ReachingFurther share one measurement (a measurement with jumps is the costly kind,
        /// and measurements are shared by everything with the same tuning: see MovementController.Capabilities). Neither
        /// sliding nor wall-running changes a jump across a floor or onto a block (which the measurement makes with
        /// wall-running off anyway), so the double jumps measure as they would with them on.
        /// </summary>
        private void WithAnAirJump_WithoutSlidingOrWallRunning()
        {
            rig.SetMovementSetting("maxJumps", 2);
            rig.SetMovementSetting("slideMaxDuration", 0f);
            rig.SetMovementSetting("wallRunMaxDuration", 0f);
        }

        [UnityTest]
        public IEnumerator WhatsTurnedOff_IsLeftOut()
        {
            yield return OnALongFloor();
            // Without jumps first (the tuning AMeasurement_IsShared… measures too, so they share it).
            rig.SetMovementSetting("maxJumps", 0);
            Assert.That(Capabilities.Jumps, Is.Empty, "Without jumps, nothing is jumped across.");
            Assert.That(Capabilities.Climbs.Select(c => c.Name), Is.EqualTo(new[] { "step up" }), "Without jumps, only steps are climbed.");
            // Then with jumps back, so it's wall-running off, not the jumps, that leaves out the wall run.
            WithAnAirJump_WithoutSlidingOrWallRunning();
            Assume.That(Capabilities.Jumps, Is.Not.Empty, "Test setup: the character should jump again.");
            Assert.That(float.IsNaN(Capabilities.SlideLength), Is.True, "With sliding off, there's no slide length.");
            Assert.That(float.IsNaN(Capabilities.WallRunLength), Is.True, "With wall-running off, there's no wall-run length.");
        }

        [UnityTest]
        public IEnumerator AirJumps_AddDoubleJumps_ReachingFurther()
        {
            yield return OnALongFloor();
            WithAnAirJump_WithoutSlidingOrWallRunning();
            Assert.That(Reach("double jump"), Is.GreaterThan(Reach("sprint jump") + 1f), "A double jump should cross more than a single.");
            Assert.That(Reach("double jump up"), Is.GreaterThan(Reach("jump up") + 0.5f), "A double jump should climb higher than a single.");
        }
    

        /// <summary>
        /// A simulated character saved partway through (MovementSimulation.Save) and put back (Restore) carries on
        /// exactly as it would have, step by step, even after running some other way in between: what lets a measurement
        /// start its attempts from a shared point (a run-up, the steps before a dash) and still measure exactly what it
        /// would from the start. A snapshot can't be put into a character made afresh since (Reset).
        /// </summary>
        [UnityTest]
        public IEnumerator ASavedSimulation_PutBack_CarriesOnExactlyAsItWouldHave()
        {
            yield return rig.StandOnFloor();
            using var simulation = new MovementSimulation(rig.Movement);
            // A sprint, a jump held from step 10, and a dash at step 50: through the air, a landing, and on.
            Inputs Planned(int i)
            {
                Inputs inputs = MovementSimulation.Move(Vector3.forward, sprint: true);
                if (i >= 10) { inputs.jump = MovementSimulation.Held(i - 10); }
                if (i >= 50) { inputs.dash = MovementSimulation.Held(i - 50); }
                return inputs;
            }
            simulation.Run(30, Planned);
            MovementSimulation.Snapshot saved = simulation.Save();
            var expected = new List<(Vector3 position, Vector3 velocity, CharacterState state)>();
            for (int i = 30; i < 150; i++)
            {
                simulation.Step(Planned(i));
                expected.Add((simulation.Position, simulation.Velocity, simulation.State));
            }

            // Somewhere else entirely meanwhile: crouched, turned, rolled.
            Assert.That(simulation.Restore(saved), Is.True, "The snapshot should go back into the character it was saved from.");
            simulation.Run(60, i =>
            {
                Inputs other = MovementSimulation.Move(Vector3.left, sprint: false);
                other.crouch = MovementSimulation.Held(i);
                if (i == 20) { other.roll = MovementSimulation.Held(0); }
                return other;
            });

            Assert.That(simulation.Restore(saved), Is.True);
            for (int i = 30; i < 150; i++)
            {
                simulation.Step(Planned(i));
                var (position, velocity, state) = expected[i - 30];
                Assert.That(simulation.Position, Is.EqualTo(position), $"Step {i}: put back, the character should be exactly where it was the first time.");
                Assert.That(simulation.Velocity, Is.EqualTo(velocity), $"Step {i}: and moving exactly as it was.");
                Assert.That(simulation.State, Is.EqualTo(state), $"Step {i}: and in the same state.");
            }

            simulation.Reset(Vector3.zero);
            Assert.That(simulation.Restore(saved), Is.False, "A snapshot shouldn't go into a character made afresh since.");
        }
    }
}
