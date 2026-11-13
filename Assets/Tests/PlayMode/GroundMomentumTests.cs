using System.Collections;
using Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CharacterShowcase.Tests
{
    /// <summary>
    /// Tests of momentum carried onto the ground: walking (or sprinting) only makes up the difference between the momentum
    /// the character carries and its walking speed, so landing from a jump doesn't add walking on top of the air speed,
    /// hopping again and again (a jump the step after each landing) never builds up speed, and momentum faster than walking
    /// (landing from a sprint jump, walking) is carried and eases down to walking, never below it. The test camera looks
    /// down +Z.
    /// </summary>
    public class GroundMomentumTests
    {
        private CharacterTestRig rig;

        [SetUp]
        public void SetUp() => rig = new CharacterTestRig();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            rig.Dispose();
            yield return null;
        }

        private static Inputs Forward(bool sprint, int step) =>
            sprint ? CharacterTestRig.Sprint(Vector3.forward, step + 40) : CharacterTestRig.Move(Vector3.forward);

        private float SpeedFor(bool sprint) => sprint ? rig.RunSpeed : rig.WalkSpeed;

        private IEnumerator RunningStart(bool sprint)
        {
            rig.Floor(width: 400f);
            yield return rig.SpawnSettled(Vector3.zero);
            yield return rig.Run(0.8f, i => Forward(sprint, i));
        }

        [UnityTest]
        public IEnumerator LandingFromAJump_DoesNotAddWalkingOnTopOfTheAirSpeed([Values(false, true)] bool sprint)
        {
            yield return RunningStart(sprint);
            bool leftGround = false, landed = false;
            float fastestOnLanding = 0f;
            int groundSteps = 0;
            yield return rig.Run(1.5f, i =>
            {
                Inputs inputs = Forward(sprint, i);
                inputs.jump = CharacterTestRig.Held(i);
                inputs.jump.value = i < 3;
                return inputs;
            }, _ =>
            {
                leftGround |= !rig.Grounded;
                if (leftGround && rig.Grounded) { landed = true; }
                if (landed && groundSteps++ < 10) { fastestOnLanding = Mathf.Max(fastestOnLanding, rig.HorizontalSpeed); }
            });
            Assert.That(landed, Is.True, "Test setup: the jump should have landed.");
            Assert.That(fastestOnLanding, Is.LessThanOrEqualTo(SpeedFor(sprint) * 1.02f),
                "Landing should keep the speed the character had, without walking added on top of it.");
        }

        [UnityTest]
        public IEnumerator HoppingAgainAndAgain_DoesNotBuildUpSpeed([Values(false, true)] bool sprint)
        {
            yield return RunningStart(sprint);
            int pressAt = 0, held = 0, hops = 0;
            bool wasGrounded = true;
            float fastest = 0f;
            yield return rig.Run(6f, i =>
            {
                Inputs inputs = Forward(sprint, i);
                if (i == pressAt) { held = 0; }
                inputs.jump = CharacterTestRig.Held(held);
                inputs.jump.value = i >= pressAt && held < 12;
                held++;
                return inputs;
            }, i =>
            {
                fastest = Mathf.Max(fastest, rig.HorizontalSpeed);
                if (wasGrounded && !rig.Grounded) { hops++; }
                // A fresh press the step after each landing, as a buffered jump does.
                if (!wasGrounded && rig.Grounded) { pressAt = i + 1; }
                wasGrounded = rig.Grounded;
            });
            Assert.That(hops, Is.GreaterThan(4), "Test setup: the character should have hopped again and again.");
            Assert.That(fastest, Is.LessThanOrEqualTo(SpeedFor(sprint) * 1.02f), "Hopping shouldn't build up speed.");
        }

        [UnityTest]
        public IEnumerator LandingFasterThanWalking_CarriesTheMomentum_EasingDownToWalking()
        {
            yield return RunningStart(sprint: true);
            // A sprint jump, then walking (Sprint let go) from the moment it lands.
            bool leftGround = false, landed = false;
            float onLanding = 0f, slowestAfter = float.PositiveInfinity, latest = 0f;
            int groundSteps = 0;
            yield return rig.Run(2f, i =>
            {
                if (landed) { return CharacterTestRig.Move(Vector3.forward); }
                Inputs inputs = Forward(true, i);
                inputs.jump = CharacterTestRig.Held(i);
                inputs.jump.value = i < 30;
                return inputs;
            }, _ =>
            {
                leftGround |= !rig.Grounded;
                if (!leftGround || !rig.Grounded) { return; }
                if (!landed) { landed = true; onLanding = rig.HorizontalSpeed; return; }
                groundSteps++;
                slowestAfter = Mathf.Min(slowestAfter, rig.HorizontalSpeed);
                latest = rig.HorizontalSpeed;
            });
            Assert.That(landed, Is.True, "Test setup: the jump should have landed.");
            Assert.That(onLanding, Is.GreaterThan(rig.WalkSpeed * 1.5f), "The sprint's momentum should carry through the landing.");
            Assert.That(slowestAfter, Is.GreaterThanOrEqualTo(rig.WalkSpeed * 0.98f), "Walking should never drop below walking speed.");
            Assert.That(latest, Is.EqualTo(rig.WalkSpeed).Within(0.05f * rig.WalkSpeed), "The momentum should ease down to walking.");
        }
    }
}
