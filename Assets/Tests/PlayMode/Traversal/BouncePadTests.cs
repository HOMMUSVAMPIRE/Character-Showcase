using System.Collections;
using System.Collections.Generic;
using Character;
using Level;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CharacterShowcase.Tests.Traversal
{
    /// <summary>
    /// Tests of bounce pads: coming down on one launches the character as high as the pad is set to (higher with Jump
    /// held), running across one carries the run on through the launch, a tilted pad launches along its up, the launch
    /// is the character's jump (its air jumps are left), and a height multiplier of 0 turns pads off. The test camera
    /// looks down +Z. Expected values are read from the movement mode's settings and the pads'. The checks every ability of
    /// a moment gets (it happens, turned off it doesn't, once per landing, saved before it and put back, at another size)
    /// come from MomentFixture: falling onto a pad.
    /// </summary>
    [Category(TestArea.Traversal)]
    public class BouncePadTests : MomentFixture<BouncePads>
    {
        private const float PadTop = 0.3f;

        protected override IEnumerator SetUpToStart()
        {
            rig.Floor();
            rig.BouncePad(Vector3.up * PadTop, new Vector3(3f, PadTop, 3f), 3f);
            yield return SpawnCharacter(Vector3.up * (PadTop + 0.3f * Scale), settleSeconds: 0f);
        }

        protected override Inputs InputsFor(int step) => CharacterTestRig.Idle();

        protected override string OffSetting => "bounceHeightMultiplier";

        protected override MovementEvents MomentEvent => MovementEvents.Bounced;
        private float Multiplier => rig.MovementSetting<float>("bounceHeightMultiplier");

        /// <summary>A floor, a pad on it, and the character standing still in the air just above the pad.</summary>
        private IEnumerator SetUpPad(float launchHeight, Vector3 padAt = default, Quaternion? rotation = null)
        {
            rig.Floor();
            BouncePad pad = rig.BouncePad(padAt + Vector3.up * PadTop, new Vector3(3f, PadTop, 3f), launchHeight, rotation);
            yield return rig.SpawnSettled(Vector3.up * (PadTop + 0.3f), settleSeconds: 0f);
            rig.ClearEvents();
        }

        /// <summary>Runs until the character has bounced and come back down to (or below) the pad's top, returning the highest its feet got.</summary>
        private IEnumerator RunThroughABounce(System.Func<int, Inputs> inputs, System.Action<float> highest, float seconds = 4f)
        {
            float top = float.NegativeInfinity;
            bool bounced = false;
            int steps = CharacterTestRig.StepsFor(seconds);
            yield return rig.RunUntil(() => bounced && rig.Position.y < PadTop + 0.5f && top > PadTop + 1f, inputs, _ =>
            {
                bounced |= rig.Count(MovementEvents.Bounced) > 0;
                if (bounced) { top = Mathf.Max(top, rig.Position.y); }
            }, steps * Time.fixedDeltaTime);
            Assert.That(bounced, Is.True, "Test setup: the character should have bounced.");
            highest(top);
        }

        [UnityTest]
        public IEnumerator ComingDownOnABouncePad_LaunchesToItsHeight()
        {
            const float height = 5f;
            yield return SetUpPad(height);
            float top = 0f;
            yield return RunThroughABounce(_ => CharacterTestRig.Idle(), t => top = t);
            float expected = height * Multiplier;
            Assert.That(top - PadTop, Is.EqualTo(expected).Within(0.05f * expected + 0.05f), "The pad should launch the character's feet its launch height above it.");
            Assert.That(rig.Count(MovementEvents.Bounced), Is.EqualTo(1), "One landing should bounce once.");
        }

        [UnityTest]
        public IEnumerator HoldingJumpOnABouncePad_BouncesHigher()
        {
            const float height = 4f;
            yield return SetUpPad(height);
            float top = 0f;
            yield return RunThroughABounce(step => CharacterTestRig.Jump(), t => top = t);
            float expected = height * Multiplier * rig.MovementSetting<float>("bounceJumpBoost");
            Assert.That(top - PadTop, Is.EqualTo(expected).Within(0.05f * expected + 0.05f), "Jump held should bounce higher by the jump boost.");
        }

        [UnityTest]
        public IEnumerator RunningAcrossABouncePad_CarriesTheRunOn()
        {
            rig.Floor();
            rig.BouncePad(new Vector3(0f, PadTop, 4f), new Vector3(3f, PadTop, 3f), 4f);
            yield return rig.SpawnSettled(Vector3.zero);
            rig.ClearEvents();
            Inputs Forward(int _) => CharacterTestRig.Move(Vector3.forward);
            yield return rig.RunUntil(() => rig.Count(MovementEvents.Bounced) > 0, Forward, timeoutSeconds: 3f);
            Assert.That(rig.Count(MovementEvents.Bounced) > 0, Is.True, "Walking onto the pad should bounce.");
            yield return rig.Run(0.2f, _ => CharacterTestRig.Idle());
            Assert.That(rig.Grounded, Is.False);
            Assert.That(rig.HorizontalSpeed, Is.GreaterThan(0.75f * rig.WalkSpeed), "The bounce should keep the character's speed across the pad.");
            Assert.That(rig.Movement.lastFrameMovementData.finalVelocity.z, Is.GreaterThan(0f), "It should carry on the way it was walking.");
        }

        [UnityTest]
        public IEnumerator ATiltedBouncePad_LaunchesAlongItsUp()
        {
            Quaternion tilt = Quaternion.Euler(30f, 0f, 0f); // Its up leans toward +Z.
            yield return SetUpPad(4f, rotation: tilt);
            yield return rig.Run(0.5f, _ => CharacterTestRig.Idle());
            Assert.That(rig.Count(MovementEvents.Bounced) > 0, Is.True, "Test setup: the character should have bounced.");
            Vector3 velocity = rig.Movement.lastFrameMovementData.finalVelocity;
            Assert.That(velocity.z, Is.GreaterThan(1f), "A pad tilted toward +Z should send the character that way.");
        }

        [UnityTest]
        public IEnumerator ABounce_IsTheCharactersJump_LeavingItsAirJumps()
        {
            yield return SetUpPad(4f);
            yield return rig.Run(0.3f, _ => CharacterTestRig.Idle());
            Assert.That(rig.Count(MovementEvents.Bounced) > 0, Is.True, "Test setup: the character should have bounced.");
            Assert.That(rig.Movement.jump.remaining, Is.EqualTo(Mathf.Max(0, rig.Component<Jumping>().MaxJumps - 1)), "The bounce should use the ground jump and leave the air jumps.");
        }

        [UnityTest]
        public IEnumerator AHeightMultiplierOfZero_TurnsBouncePadsOff()
        {
            yield return SetUpPad(5f);
            rig.SetMovementSetting("bounceHeightMultiplier", 0f);
            yield return rig.Run(1f, _ => CharacterTestRig.Idle());
            Assert.That(rig.Count(MovementEvents.Bounced) > 0, Is.False);
            Assert.That(rig.Grounded, Is.True, "The character should stand on the pad.");
        }
    }
}
