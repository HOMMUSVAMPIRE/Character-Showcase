using System.Collections;
using System.Collections.Generic;
using Character;
using Level;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CharacterShowcase.Tests
{
    /// <summary>
    /// Tests of a movement mode's components (ComposedMovementMode): a component turned off, or removed, is left out of
    /// the movement as if it weren't there; one turned off while it has the character lets go of it; and a test's change
    /// to a component's settings is to the rig's own copy, not the asset. Bounce pads and ziplines stand in for any
    /// component that takes over the character; jumping and dashing, which act on top of walking, and walking and falling
    /// themselves, are turned off too.
    /// </summary>
    public class MovementComponentTests
    {
        private CharacterTestRig rig;
        private readonly List<MovementEvents> events = new();

        [SetUp]
        public void SetUp()
        {
            rig = new CharacterTestRig();
            events.Clear();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            rig.Dispose();
            yield return null;
        }

        private bool Riding => rig.InState(CharacterState.Ziplining);
        private float HangHeight => rig.MovementSetting<float>("ziplineHangHeight") * rig.StandingHeight;

        /// <summary>A floor, a downhill zipline, and the character in mid-air with its hands just above the cable.</summary>
        private IEnumerator UnderAZipline()
        {
            rig.Floor();
            Zipline line = rig.Zipline(new Vector3(0f, 8f, 0f), new Vector3(0f, 4f, 24f));
            yield return rig.SpawnSettled(Vector3.zero, settleSeconds: 0f);
            rig.Teleport(line.PointAt(2f) - Vector3.up * (HangHeight - 0.15f));
            rig.Movement.MovementEventRaised += (e, _) => events.Add(e);
        }

        /// <summary>A floor, a bounce pad on it, and the character just above the pad.</summary>
        private IEnumerator AboveABouncePad()
        {
            rig.Floor();
            rig.BouncePad(Vector3.up * 0.3f, new Vector3(3f, 0.3f, 3f), 5f);
            yield return rig.SpawnSettled(Vector3.up * 0.6f, settleSeconds: 0f);
            rig.Movement.MovementEventRaised += (e, _) => events.Add(e);
        }

        [UnityTest]
        public IEnumerator AComponentTurnedOff_IsLeftOut()
        {
            yield return UnderAZipline();
            rig.Component<Ziplines>().Enabled = false;
            bool rode = false;
            yield return rig.Run(1f, _ => CharacterTestRig.Idle(), _ => rode |= Riding);
            Assert.That(rode, Is.False, "With Ziplines turned off, the character shouldn't catch hold of a cable.");
            Assert.That(rig.Grounded, Is.True, "It should fall past the cable to the floor.");
        }

        [UnityTest]
        public IEnumerator AComponentRemoved_IsLeftOut()
        {
            yield return AboveABouncePad();
            Assume.That(rig.ComposedMode.Remove<BouncePads>(), Is.True, "Test setup: the mode should have had bounce pads.");
            yield return rig.Run(1f, _ => CharacterTestRig.Idle());
            Assert.That(events, Has.No.Member(MovementEvents.Bounced), "Without BouncePads, a pad shouldn't launch the character.");
            Assert.That(rig.Grounded, Is.True, "The character should stand on the pad.");
        }

        [UnityTest]
        public IEnumerator AComponentTurnedOffWhileItHasTheCharacter_LetsGoOfIt()
        {
            yield return UnderAZipline();
            for (int step = 0; step < CharacterTestRig.StepsFor(0.5f) && !Riding; step++) { yield return rig.Run(1, _ => CharacterTestRig.Idle()); }
            Assume.That(Riding, Is.True, "Test setup: the character should have caught hold of the cable.");
            rig.Component<Ziplines>().Enabled = false;
            yield return rig.Run(0.1f, _ => CharacterTestRig.Idle());
            Assert.That(rig.Movement.zipline.IsRiding, Is.False, "Turned off, Ziplines should let go of the cable.");
            Assert.That(events, Does.Contain(MovementEvents.ZiplineEnded));
            Assert.That(rig.ComposedMode.AbilityInProgress(rig.Movement), Is.False, "Nothing should have the character any more.");
        }

        [UnityTest]
        public IEnumerator JumpingTurnedOff_TheCharacterDoesntJump()
        {
            rig.Floor();
            yield return rig.SpawnSettled(Vector3.zero);
            rig.Movement.MovementEventRaised += (e, _) => events.Add(e);
            rig.Component<Jumping>().Enabled = false;
            bool leftTheGround = false;
            yield return rig.Run(0.5f, step => CharacterTestRig.Jump(step), _ => leftTheGround |= !rig.Grounded);
            Assert.That(leftTheGround, Is.False, "With Jumping turned off, Jump shouldn't leave the ground.");
            Assert.That(events, Has.No.Member(MovementEvents.Jumped));
            Assert.That(rig.Movement.maxJumps, Is.EqualTo(0), "Without Jumping, the character has no jumps.");
        }

        [UnityTest]
        public IEnumerator DashingTurnedOff_TheCharacterDoesntDash()
        {
            rig.Floor();
            yield return rig.SpawnSettled(Vector3.zero);
            rig.Movement.MovementEventRaised += (e, _) => events.Add(e);
            rig.Component<Dashing>().Enabled = false;
            yield return rig.Run(0.5f, step => CharacterTestRig.Dash(step, Vector3.forward));
            Assert.That(events, Has.No.Member(MovementEvents.DashStarted), "With Dashing turned off, Dash shouldn't dash.");
            Assert.That(rig.Movement.dash.IsDashing, Is.False);
        }

        [UnityTest]
        public IEnumerator WalkingAndRunningTurnedOff_TheCharacterDoesntWalk_ButStillJumps()
        {
            rig.Floor();
            yield return rig.SpawnSettled(Vector3.zero);
            rig.Movement.MovementEventRaised += (e, _) => events.Add(e);
            rig.Component<WalkingAndRunning>().Enabled = false;
            Vector3 start = rig.Position;
            yield return rig.Run(0.5f, step => CharacterTestRig.Sprint(Vector3.forward, step));
            Vector3 moved = rig.Position - start;
            Assert.That(new Vector2(moved.x, moved.z).magnitude, Is.LessThan(0.05f), "With Walking & Running turned off, movement input shouldn't move the character.");
            yield return rig.Run(0.3f, step => CharacterTestRig.Jump(step));
            Assert.That(events, Does.Contain(MovementEvents.Jumped), "Jumping doesn't need walking.");
        }

        [UnityTest]
        public IEnumerator FallAndAirHandlingTurnedOff_TheCharacterDoesntFall()
        {
            rig.Floor();
            yield return rig.SpawnSettled(Vector3.zero);
            rig.Component<FallAndAirHandling>().Enabled = false;
            Vector3 above = Vector3.up * 3f;
            rig.Teleport(above);
            yield return rig.Run(0.5f, _ => CharacterTestRig.Idle());
            Assert.That(rig.Position.y, Is.EqualTo(above.y).Within(0.01f), "With Fall & Air Handling turned off, there's no gravity.");
            rig.Component<FallAndAirHandling>().Enabled = true;
            yield return rig.Run(1.5f, _ => CharacterTestRig.Idle());
            Assert.That(rig.Grounded, Is.True, "Turned back on, the character should fall to the floor.");
        }

        [UnityTest]
        public IEnumerator AComponentsSettings_ChangedByATest_AreTheRigsOwnCopy()
        {
            yield return UnderAZipline();
            var asset = CharacterTestRig.CharacterPrefab().GetComponent<MovementController>().movementHandler as ComposedMovementMode;
            Assume.That(asset, Is.Not.Null, "Test setup: the character's movement mode should be made of components.");
            float authored = asset.Get<Ziplines>(includeDisabled: true).ZiplineGrabReach;
            rig.SetMovementSetting("ziplineGrabReach", authored + 1f);
            Assert.That(rig.Component<Ziplines>().ZiplineGrabReach, Is.EqualTo(authored + 1f));
            Assert.That(asset.Get<Ziplines>(includeDisabled: true).ZiplineGrabReach, Is.EqualTo(authored), "The asset's component should be left as it was.");
        }
    }
}
