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
    /// Tests of rope swinging: reaching a rope in mid-air catches hold of it, and the character swings on it as a
    /// pendulum about its anchor, its hands staying where they caught hold, carried on by its speed; with no input the
    /// swing never rises higher, pumping it with movement input swings it higher, and never past the largest swing angle;
    /// a wall stops the swing; Jump lets go with the swing's speed and a jump up, leaving the rope swinging by itself;
    /// Crouch lets go, and the same rope isn't caught again straight away; and a grab reach of 0 turns ropes off. The test
    /// camera looks down +Z. Expected values are read from the movement mode's settings.
    /// </summary>
    public class RopeSwingTests
    {
        private CharacterTestRig rig;
        private Rope rope;
        private readonly List<MovementEvents> events = new();

        private static readonly Vector3 Anchor = new Vector3(0f, 10f, 0f);
        private const float RopeLength = 8f;

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

        private bool Swinging => rig.InState(CharacterState.RopeSwinging);
        private float GripHeight => rig.MovementSetting<float>("ropeGripHeight") * rig.StandingHeight;
        private Vector3 Grip => rig.Position + Vector3.up * GripHeight;
        private Vector3 Velocity => rig.Movement.lastFrameMovementData.finalVelocity;
        /// <summary>How far (degrees) the character's grip is swung out from straight below the anchor.</summary>
        private float SwingAngle => Vector3.Angle(Grip - Anchor, Vector3.down);

        /// <summary>
        /// A floor, the rope, and the character in mid-air behind it (-Z, from metres away), its hands at the given height,
        /// leaping at it along +Z at the given speed; run until it catches hold. (Under the character's gravity, a slow
        /// leap has to start close to reach the rope before it reaches the floor.)
        /// </summary>
        private IEnumerator SetUpSwing(float gripHeight = 6f, float speed = 6f, float from = 1.5f)
        {
            rig.Floor();
            rope = rig.Rope(Anchor, RopeLength);
            // Its grip height depends on its size, known once it's spawned.
            yield return rig.SpawnSettled(Vector3.zero, settleSeconds: 0f);
            rig.Teleport(new Vector3(0f, gripHeight - GripHeight, -from));
            rig.Movement.MovementEventRaised += (e, _) => events.Add(e);
            yield return rig.Launch(new Vector3(0f, 0f, speed), CharacterTestRig.Idle());
            for (int step = 0; step < CharacterTestRig.StepsFor(1f) && !Swinging; step++) { yield return rig.Run(1, _ => CharacterTestRig.Idle()); }
            Assert.That(Swinging, Is.True, $"Test setup: the character should have caught hold of the rope (it's at {rig.Position}).");
        }

        /// <summary>Movement input pushing the way the swing is going (along Z), which pumps it.</summary>
        private Inputs Pump(int _) => CharacterTestRig.Move(Vector3.forward * Mathf.Sign(rig.Movement.ropeSwing.velocity.z));

        [UnityTest]
        public IEnumerator ReachingARope_CatchesHold_AndSwingsOnItAboutItsAnchor()
        {
            yield return SetUpSwing();
            Assert.That(rope.IsHeld, Is.True);
            Assert.That(events, Does.Contain(MovementEvents.RopeGrabbed));
            float radius = rig.Movement.ropeSwing.gripDistance;
            float furthestOff = 0f, furthestForward = float.NegativeInfinity, after = float.PositiveInfinity;
            yield return rig.Run(3f, _ => CharacterTestRig.Idle(), _ =>
            {
                furthestOff = Mathf.Max(furthestOff, Mathf.Abs(Vector3.Distance(Grip, Anchor) - radius));
                if (rig.Position.z > furthestForward) { furthestForward = rig.Position.z; after = float.PositiveInfinity; }
                else { after = Mathf.Min(after, rig.Position.z); }
            });
            Assert.That(Swinging, Is.True);
            Assert.That(furthestOff, Is.LessThan(0.03f), "The hands should stay the grip distance from the anchor.");
            Assert.That(furthestForward, Is.GreaterThan(0.5f), "The character's speed should carry it on, swinging forward past the anchor.");
            Assert.That(after, Is.LessThan(furthestForward - 0.5f), "The swing should come back.");
        }

        [UnityTest]
        public IEnumerator WithNoInput_TheSwingNeverRisesHigher()
        {
            yield return SetUpSwing();
            float firstHighest = float.NegativeInfinity, laterHighest = float.NegativeInfinity;
            yield return rig.Run(1.2f, _ => CharacterTestRig.Idle(), _ => firstHighest = Mathf.Max(firstHighest, Grip.y));
            yield return rig.Run(4f, _ => CharacterTestRig.Idle(), _ => laterHighest = Mathf.Max(laterHighest, Grip.y));
            Assert.That(laterHighest, Is.LessThanOrEqualTo(firstHighest + 0.02f), "With no input, the swing shouldn't gain height.");
        }

        [UnityTest]
        public IEnumerator PumpingWithMovementInput_SwingsHigher()
        {
            yield return SetUpSwing(speed: 3f, from: 0.6f);
            float before = 0f, after = 0f;
            yield return rig.Run(1.5f, _ => CharacterTestRig.Idle(), _ => before = Mathf.Max(before, SwingAngle));
            yield return rig.Run(4f, Pump);
            yield return rig.Run(2f, Pump, _ => after = Mathf.Max(after, SwingAngle));
            Assert.That(after, Is.GreaterThan(before + 5f), "Pumping the swing should swing it out further.");
        }

        [UnityTest]
        public IEnumerator TheSwing_GoesNoFurtherThanTheLargestSwingAngle()
        {
            yield return SetUpSwing();
            rig.SetMovementSetting("ropeSwingAcceleration", 40f);
            float widest = 0f;
            yield return rig.Run(6f, Pump, _ => widest = Mathf.Max(widest, SwingAngle));
            float largest = rig.MovementSetting<float>("ropeMaxSwingAngle");
            Assert.That(Swinging, Is.True);
            Assert.That(widest, Is.LessThanOrEqualTo(largest + 0.5f), "The swing shouldn't go past the largest swing angle.");
            Assert.That(widest, Is.GreaterThan(largest - 5f), "Test setup: hard pumping should reach the largest swing angle.");
        }

        [UnityTest]
        public IEnumerator AWall_StopsTheSwing()
        {
            rig.Box("Wall", new Vector3(0f, 6f, 2.5f), new Vector3(6f, 12f, 1f));
            yield return SetUpSwing(speed: 8f);
            float furthest = float.NegativeInfinity;
            yield return rig.Run(2f, _ => CharacterTestRig.Idle(), _ => furthest = Mathf.Max(furthest, rig.Position.z));
            Assert.That(furthest + rig.Radius, Is.LessThanOrEqualTo(2f + 0.05f), "The wall should stop the character.");
            Assert.That(Swinging, Is.True, "The character should still be on the rope.");
        }

        [UnityTest]
        public IEnumerator JumpingOffARope_KeepsTheSwingsSpeed_JumpsUp_AndLeavesTheRopeSwinging()
        {
            yield return SetUpSwing();
            yield return rig.Run(0.3f, _ => CharacterTestRig.Idle());
            Vector3 swing = rig.Movement.ropeSwing.velocity;
            events.Clear();
            yield return rig.Run(1, _ => CharacterTestRig.Jump(0));
            Assert.That(Swinging, Is.False, "Jump should let go of the rope.");
            float jumpUp = rig.MovementSetting<float>("ropeJumpUpSpeed") * rig.Movement.SpeedScale;
            Assert.That(Velocity.z, Is.EqualTo(swing.z).Within(0.6f), "Jumping off should keep the swing's speed.");
            Assert.That(Velocity.y, Is.GreaterThan(swing.y + jumpUp - 0.6f), "Jumping off should jump up, on top of the swing.");
            Assert.That(events, Does.Contain(MovementEvents.Jumped));
            Assert.That(events, Does.Contain(MovementEvents.RopeReleased));
            Assert.That(rope.IsHeld, Is.False);
            Vector3 released = rope.Direction;
            yield return rig.Run(0.3f, _ => CharacterTestRig.Idle());
            Assert.That(Vector3.Angle(rope.Direction, released), Is.GreaterThan(1f), "Let go of, the rope should swing on by itself.");
        }

        [UnityTest]
        public IEnumerator CrouchingOnARope_LetsGo_AndTheSameRopeIsntCaughtAgainStraightAway()
        {
            yield return SetUpSwing(speed: 1f, from: 0.3f);
            yield return rig.Run(0.2f, _ => CharacterTestRig.Idle());
            yield return rig.Run(1, _ => CharacterTestRig.Crouch(0));
            bool caughtAgain = false;
            float delay = rig.MovementSetting<float>("ropeRegrabDelay");
            yield return rig.Run(delay * 0.8f, _ => CharacterTestRig.Idle(), _ => caughtAgain |= Swinging);
            Assert.That(caughtAgain, Is.False, "Crouch should let go, and the rope shouldn't be caught again until the regrab delay is over.");
        }

        [UnityTest]
        public IEnumerator AGrabReachOfZero_TurnsRopesOff()
        {
            rig.Floor();
            rope = rig.Rope(Anchor, RopeLength);
            yield return rig.SpawnSettled(Vector3.zero, settleSeconds: 0f);
            rig.Teleport(new Vector3(0f, 6f - GripHeight, -1.5f));
            rig.SetMovementSetting("ropeGrabReach", 0f);
            bool swung = false;
            yield return rig.Launch(new Vector3(0f, 0f, 6f), CharacterTestRig.Idle());
            yield return rig.Run(1f, _ => CharacterTestRig.Idle(), _ => swung |= Swinging);
            Assert.That(swung, Is.False);
        }
    }
}
