using System.Collections;
using MeridianMotion.Characters;
using NUnit.Framework;
using UnityEngine.TestTools;
using UnityEngine;

namespace MeridianMotion.Tests.Movement
{
    /// <summary>
    /// Tests of jump heights: a tap of Jump rises the min jump height, Jump held to the top of the jump rises the max jump
    /// height, holding longer rises higher in between, letting go past the min height stops the rise at once, the heights
    /// follow the settings (so retuning them retunes the jump), and a max height of 0 turns jumping off. Heights are read
    /// from the movement mode's settings. The checks every ability of a moment gets (it happens, turned off it doesn't,
    /// once per press, saved before it and put back, at another size) come from MomentFixture: a press of Jump standing
    /// on a floor.
    /// </summary>
    [Category(TestArea.Movement)]
    public class JumpHeightTests : MomentFixture<Jumping>
    {
        protected override IEnumerator SetUpToStart()
        {
            rig.Floor();
            yield return SpawnCharacter(new Vector3(0f, 0.05f, 0f));
        }

        protected override StepInputs InputsFor(int step) => step == 0 ? CharacterTestRig.Jump() : CharacterTestRig.Idle();

        protected override string OffSetting => "maxJumpHeight";

        protected override MovementEvents MomentEvent => MovementEvents.Jumped;

        private float MinHeight => rig.MovementSetting<float>("minJumpHeight") * rig.Movement.HeightScale;
        private float MaxHeight => rig.MovementSetting<float>("maxJumpHeight") * rig.Movement.HeightScale;

        /// <summary>Jumps with Jump held for the given steps (from the step it's pressed), returning how high the feet rose.</summary>
        private IEnumerator JumpHolding(int heldSteps, System.Action<float> risen)
        {
            float start = rig.Position.y, top = start;
            yield return rig.Run(1.5f, i =>
            {
                StepInputs inputs = CharacterTestRig.Idle();
                inputs.jump = CharacterTestRig.Held();
                inputs.jump.held = i < heldSteps;
                return inputs;
            }, _ => top = Mathf.Max(top, rig.Position.y));
            risen(top - start);
        }

        private IEnumerator OnTheFloor()
        {
            yield return rig.StandOnFloor();
        }

        /// <summary>
        /// A tap of Jump (held one step) rises the min jump height, Jump held to the top of the jump rises the max jump
        /// height, and holding it longer in between rises higher.
        /// </summary>
        [UnityTest]
        public IEnumerator HoldingJumpLonger_RisesHigher_FromTheMinHeightForATap_ToTheMaxHeldToTheTop()
        {
            yield return OnTheFloor();
            float previous = 0f;
            foreach (int held in new[] { 1, 4, 7, 10, 1000 })
            {
                float risen = 0f;
                yield return JumpHolding(held, h => risen = h);
                Assert.That(risen, Is.InRange(MinHeight - 0.01f, MaxHeight + 0.01f), $"Held {held} steps, the jump should rise between the min and max heights.");
                Assert.That(risen, Is.GreaterThanOrEqualTo(previous - 0.01f), $"Held {held} steps, the jump should rise at least as high as held for less.");
                if (held == 1) { Assert.That(risen, Is.EqualTo(MinHeight).Within(0.01f), "A tap should rise the min jump height."); }
                previous = risen;
                yield return rig.Run(0.5f, _ => CharacterTestRig.Idle()); // Land and settle.
            }
            Assert.That(previous, Is.EqualTo(MaxHeight).Within(0.01f), "Jump held should rise the max jump height.");
        }

        [UnityTest]
        public IEnumerator LettingGoPastTheMinHeight_StopsTheRiseAtOnce()
        {
            yield return OnTheFloor();
            float start = rig.Position.y, atRelease = float.NaN, top = start;
            bool released = false;
            yield return rig.Run(1.5f, i =>
            {
                StepInputs inputs = CharacterTestRig.Idle();
                inputs.jump = CharacterTestRig.Held();
                // Held until the feet have risen past halfway from the min height to the max, then let go.
                released |= rig.Position.y - start > 0.5f * (MinHeight + MaxHeight);
                inputs.jump.held = !released;
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
