using System.Collections;
using Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CharacterShowcase.Tests
{
    /// <summary>
    /// Tests of jump heights: a tap of Jump rises the min jump height, Jump held to the top of the jump rises the max jump
    /// height, holding longer rises higher in between, letting go past the min height stops the rise at once, the heights
    /// follow the settings (so retuning them retunes the jump), and a max height of 0 turns jumping off. Heights are read
    /// from the movement mode's settings.
    /// </summary>
    public class JumpHeightTests
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

        private float MinHeight => rig.MovementSetting<float>("minJumpHeight") * rig.Movement.HeightScale;
        private float MaxHeight => rig.MovementSetting<float>("maxJumpHeight") * rig.Movement.HeightScale;

        /// <summary>Jumps with Jump held for the given steps (from the step it's pressed), returning how high the feet rose.</summary>
        private IEnumerator JumpHolding(int heldSteps, System.Action<float> risen)
        {
            float start = rig.Position.y, top = start;
            yield return rig.Run(1.5f, i =>
            {
                Inputs inputs = CharacterTestRig.Idle();
                inputs.jump = CharacterTestRig.Held(i);
                inputs.jump.value = i < heldSteps;
                return inputs;
            }, _ => top = Mathf.Max(top, rig.Position.y));
            risen(top - start);
        }

        private IEnumerator OnTheFloor()
        {
            rig.Floor();
            yield return rig.SpawnSettled(Vector3.zero);
        }

        [UnityTest]
        public IEnumerator ATapOfJump_RisesTheMinJumpHeight()
        {
            yield return OnTheFloor();
            float risen = 0f;
            yield return JumpHolding(1, h => risen = h);
            Assert.That(risen, Is.EqualTo(MinHeight).Within(0.01f), "A tap should rise the min jump height.");
        }

        [UnityTest]
        public IEnumerator JumpHeldToTheTop_RisesTheMaxJumpHeight()
        {
            yield return OnTheFloor();
            float risen = 0f;
            yield return JumpHolding(1000, h => risen = h);
            Assert.That(risen, Is.EqualTo(MaxHeight).Within(0.01f), "Jump held should rise the max jump height.");
        }

        [UnityTest]
        public IEnumerator HoldingJumpLonger_RisesHigher_BetweenTheMinAndMax()
        {
            yield return OnTheFloor();
            float previous = 0f;
            foreach (int held in new[] { 1, 4, 7, 10, 1000 })
            {
                float risen = 0f;
                yield return JumpHolding(held, h => risen = h);
                Assert.That(risen, Is.InRange(MinHeight - 0.01f, MaxHeight + 0.01f), $"Held {held} steps, the jump should rise between the min and max heights.");
                Assert.That(risen, Is.GreaterThanOrEqualTo(previous - 0.01f), $"Held {held} steps, the jump should rise at least as high as held for less.");
                previous = risen;
                yield return rig.Run(0.5f, _ => CharacterTestRig.Idle()); // Land and settle.
            }
            Assert.That(previous, Is.EqualTo(MaxHeight).Within(0.01f));
        }

        [UnityTest]
        public IEnumerator LettingGoPastTheMinHeight_StopsTheRiseAtOnce()
        {
            yield return OnTheFloor();
            float start = rig.Position.y, atRelease = float.NaN, top = start;
            bool released = false;
            yield return rig.Run(1.5f, i =>
            {
                Inputs inputs = CharacterTestRig.Idle();
                inputs.jump = CharacterTestRig.Held(i);
                // Held until the feet have risen past halfway from the min height to the max, then let go.
                released |= rig.Position.y - start > 0.5f * (MinHeight + MaxHeight);
                inputs.jump.value = !released;
                if (released && float.IsNaN(atRelease)) { atRelease = rig.Position.y; }
                return inputs;
            }, _ => top = Mathf.Max(top, rig.Position.y));
            Assert.That(released, Is.True, "Test setup: the jump should have been let go of.");
            Assert.That(top - atRelease, Is.LessThan(0.02f), "Let go of past the min height, the jump should stop rising at once.");
        }

        [UnityTest]
        public IEnumerator TheJumpHeights_FollowTheSettings()
        {
            yield return OnTheFloor();
            rig.SetMovementSetting("minJumpHeight", 0.4f);
            rig.SetMovementSetting("maxJumpHeight", 3.5f);
            float tap = 0f, held = 0f;
            yield return JumpHolding(1, h => tap = h);
            yield return rig.Run(1f, _ => CharacterTestRig.Idle());
            yield return JumpHolding(1000, h => held = h);
            Assert.That(tap, Is.EqualTo(0.4f * rig.Movement.HeightScale).Within(0.01f));
            Assert.That(held, Is.EqualTo(3.5f * rig.Movement.HeightScale).Within(0.01f));
        }

        [UnityTest]
        public IEnumerator AMaxJumpHeightOfZero_TurnsJumpingOff()
        {
            yield return OnTheFloor();
            rig.SetMovementSetting("maxJumpHeight", 0f);
            float risen = 0f;
            yield return JumpHolding(1000, h => risen = h);
            Assert.That(risen, Is.LessThan(0.02f));
            Assert.That(rig.Grounded, Is.True);
        }
    }
}
