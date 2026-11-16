using System.Collections;
using System.Linq;
using Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CharacterShowcase.Tests
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
            Inputs Forward(int i) => sprint ? CharacterTestRig.Sprint(Vector3.forward, i + 40) : CharacterTestRig.Move(Vector3.forward);
            yield return rig.Run(0.8f, Forward);
            float start = rig.Position.z;
            bool left = false, landed = false;
            yield return rig.RunUntil(() => landed, step =>
            {
                Inputs inputs = Forward(step);
                inputs.jump = CharacterTestRig.Held(step);
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
            Assert.That(Capabilities, Is.Not.SameAs(first), "A change of tuning should be measured.");
            rig.SetMovementSetting("maxJumps", jumps);
            Assert.That(Capabilities, Is.SameAs(first), "Tuned back as it was, it should share the measurement made then.");

            // Even a tiny change of size counts.
            rig.Controller.SetScale(new Vector3(1.002f, 1.002f, 1.002f), immediately: true);
            Assert.That(Capabilities, Is.Not.SameAs(first), "A change of size should be measured.");
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

        [UnityTest]
        public IEnumerator WhatsTurnedOff_IsLeftOut()
        {
            yield return OnALongFloor();
            // Without jumps first (the tuning AMeasurement_IsShared… measures too, so they share it).
            int jumps = rig.MovementSetting<int>("maxJumps");
            rig.SetMovementSetting("maxJumps", 0);
            Assert.That(Capabilities.Jumps, Is.Empty, "Without jumps, nothing is jumped across.");
            Assert.That(Capabilities.Climbs.Select(c => c.Name), Is.EqualTo(new[] { "step up" }), "Without jumps, only steps are climbed.");
            // Then with jumps back, so it's wall-running off, not the jumps, that leaves out the wall run.
            rig.SetMovementSetting("maxJumps", jumps);
            rig.SetMovementSetting("slideMaxDuration", 0f);
            rig.SetMovementSetting("wallRunMaxDuration", 0f);
            Assume.That(Capabilities.Jumps, Is.Not.Empty, "Test setup: the character should jump again.");
            Assert.That(float.IsNaN(Capabilities.SlideLength), Is.True, "With sliding off, there's no slide length.");
            Assert.That(float.IsNaN(Capabilities.WallRunLength), Is.True, "With wall-running off, there's no wall-run length.");
        }

        [UnityTest]
        public IEnumerator AirJumps_AddDoubleJumps_ReachingFurther()
        {
            yield return OnALongFloor();
            rig.SetMovementSetting("maxJumps", 2);
            Assert.That(Reach("double jump"), Is.GreaterThan(Reach("sprint jump") + 1f), "A double jump should cross more than a single.");
            Assert.That(Reach("double jump up"), Is.GreaterThan(Reach("jump up") + 0.5f), "A double jump should climb higher than a single.");
        }
    }
}
