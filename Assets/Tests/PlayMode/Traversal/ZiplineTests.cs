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
    /// Tests of ziplines: reaching a cable in mid-air catches hold of it, and the character rides it downhill, hanging
    /// below it facing the way it rides, speeding up to no more than the zipline's top speed, to its end, where it lets go
    /// carrying on at the ride's speed; a level cable still carries it, at the slowest ride speed; Jump lets go with a jump
    /// up; Crouch lets go; and a grab reach of 0 turns ziplines off. The test camera looks down +Z. Expected values are
    /// read from the movement mode's settings and the ziplines'.
    /// </summary>
    [Category(TestArea.Traversal)]
    public class ZiplineTests : CharacterRigFixture
    {
        private Zipline line;

        private float SlowestSpeed => rig.MovementSetting<float>("ziplineMinSpeed") * rig.Movement.SpeedScale;
        private Vector3 Forward => rig.Body.rotation * Vector3.forward;

        /// <summary>
        /// A floor, a zipline from start to end, and the character in mid-air just under the cable a little way along it,
        /// its hands a little above the cable, falling onto it; run until it catches hold.
        /// </summary>
        private IEnumerator SetUpRide(Vector3 start, Vector3 end, float maxSpeed = 16f)
        {
            rig.Floor();
            line = rig.Zipline(start, end, maxSpeed);
            // Its hang height depends on its size, known once it's spawned.
            yield return rig.SpawnSettled(Vector3.zero, settleSeconds: 0f);
            rig.Teleport(line.PointAt(2f) - Vector3.up * (rig.ZiplineHangHeight - 0.15f));
            rig.ClearEvents();
            yield return rig.RunUntil(() => rig.Ziplining, _ => CharacterTestRig.Idle(), timeoutSeconds: 0.5f);
            Assert.That(rig.Ziplining, Is.True, $"Test setup: the character should have caught hold of the cable (it's at {rig.Position}).");
        }

        [UnityTest]
        public IEnumerator CatchingACable_RidesItDownhillToItsEnd_AndLetsGo()
        {
            yield return SetUpRide(new Vector3(0f, 8f, 0f), new Vector3(0f, 4f, 24f));
            yield return rig.RunUntil(() => !rig.Ziplining, _ => CharacterTestRig.Idle(), timeoutSeconds: 8f);
            Assert.That(rig.Ziplining, Is.False, "The ride should end at the cable's end.");
            Assert.That(rig.Position.z, Is.GreaterThan(line.End.z - 1f), "The character should have ridden the cable to its end.");
            Assert.That(rig.Velocity.z, Is.GreaterThan(SlowestSpeed * 0.9f), "Off the end, it should carry on at the ride's speed.");
            Assert.That(rig.EventsRaised, Does.Contain(MovementEvents.ZiplineStarted));
            Assert.That(rig.EventsRaised, Does.Contain(MovementEvents.ZiplineEnded));
        }

        [UnityTest]
        public IEnumerator RidingAZipline_HangsBelowTheCable_FacingTheWayItRides()
        {
            yield return SetUpRide(new Vector3(0f, 8f, 0f), new Vector3(0f, 4f, 24f));
            yield return rig.Run(1f, _ => CharacterTestRig.Idle());
            Assert.That(rig.Ziplining, Is.True);
            Vector3 cable = line.PointAbove(rig.Position);
            Assert.That(cable.y - rig.Position.y, Is.EqualTo(rig.ZiplineHangHeight).Within(0.05f), "The character's feet should hang the hang height below the cable.");
            Assert.That(rig.Position.x, Is.EqualTo(0f).Within(0.02f), "The character should hang straight below the cable.");
            Assert.That(Vector3.Angle(Forward, Vector3.forward), Is.LessThan(2f), "The character should face the way it rides.");
        }

        [UnityTest]
        public IEnumerator ASteepRide_SpeedsUp_ToNoMoreThanTheZiplinesTopSpeed()
        {
            const float topSpeed = 9f;
            yield return SetUpRide(new Vector3(0f, 30f, 0f), new Vector3(0f, 5f, 40f), topSpeed);
            // The ride's own speed along the cable (the character's velocity also closes in on hanging below it, at first).
            float early = Mathf.Abs(rig.Movement.zipline.speed);
            float fastest = 0f;
            yield return rig.RunUntil(() => !rig.Ziplining, _ => CharacterTestRig.Idle(),
                _ => { if (rig.Ziplining) { fastest = Mathf.Max(fastest, Mathf.Abs(rig.Movement.zipline.speed)); } }, 3f);
            Assert.That(fastest, Is.GreaterThan(early + 1f), "Riding downhill should speed the character up.");
            Assert.That(fastest, Is.LessThanOrEqualTo(topSpeed + 0.05f), "The ride should go no faster than the zipline's top speed.");
        }

        [UnityTest]
        public IEnumerator ALevelCable_StillCarriesTheRider_AtTheSlowestRideSpeed()
        {
            yield return SetUpRide(new Vector3(0f, 6f, 0f), new Vector3(0f, 6f, 30f));
            yield return rig.Run(1f, _ => CharacterTestRig.Idle());
            Assert.That(rig.Ziplining, Is.True);
            Assert.That(rig.Velocity.z, Is.EqualTo(SlowestSpeed).Within(0.1f), "A level cable should carry the character at the slowest ride speed.");
        }

        [UnityTest]
        public IEnumerator JumpingOffAZipline_JumpsUp_KeepingTheRidesSpeed()
        {
            yield return SetUpRide(new Vector3(0f, 10f, 0f), new Vector3(0f, 6f, 40f));
            yield return rig.Run(1f, _ => CharacterTestRig.Idle());
            Vector3 ride = rig.Velocity;
            rig.ClearEvents();
            yield return rig.Run(1, _ => CharacterTestRig.Jump());
            Assert.That(rig.Ziplining, Is.False, "Jump should let go of the cable.");
            float jumpUp = rig.MovementSetting<float>("ziplineJumpUpSpeed") * rig.Movement.SpeedScale;
            Assert.That(rig.Velocity.y, Is.EqualTo(ride.y + jumpUp).Within(0.5f), "Jumping off should send the character up on top of the ride's speed.");
            Assert.That(rig.Velocity.z, Is.EqualTo(ride.z).Within(0.5f), "Jumping off should keep the ride's speed along the cable.");
            Assert.That(rig.EventsRaised, Does.Contain(MovementEvents.Jumped));
        }

        [UnityTest]
        public IEnumerator CrouchingOnAZipline_LetsGo()
        {
            yield return SetUpRide(new Vector3(0f, 10f, 0f), new Vector3(0f, 6f, 40f));
            yield return rig.Run(0.5f, _ => CharacterTestRig.Idle());
            yield return rig.Run(1, _ => CharacterTestRig.Crouch());
            yield return rig.Run(0.2f, _ => CharacterTestRig.Idle());
            Assert.That(rig.Ziplining, Is.False, "Crouch should let go of the cable.");
        }

        [UnityTest]
        public IEnumerator AGrabReachOfZero_TurnsZiplinesOff()
        {
            rig.Floor();
            line = rig.Zipline(new Vector3(0f, 8f, 0f), new Vector3(0f, 4f, 24f));
            yield return rig.SpawnSettled(Vector3.zero, settleSeconds: 0f);
            rig.Teleport(line.PointAt(2f) - Vector3.up * (rig.ZiplineHangHeight - 0.15f));
            rig.SetMovementSetting("ziplineGrabReach", 0f);
            bool rode = false;
            yield return rig.Run(1f, _ => CharacterTestRig.Idle(), _ => rode |= rig.Ziplining);
            Assert.That(rode, Is.False);
        }
    }
}
