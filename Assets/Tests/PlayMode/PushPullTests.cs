using System.Collections;
using Character;
using Level;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CharacterShowcase.Tests
{
    /// <summary>
    /// Tests of pushing and pulling large objects (PushableObject): holding grab by one and steering into it pushes it,
    /// and away pulls it, at the push and pull speed times the object's own multiplier; an object that can't be pulled
    /// stays put; objects stop at walls and the character with them; pushed off an edge, an object falls and the
    /// character lets go; and without holding grab, an object is just an obstacle. Expected values are read from the
    /// movement mode's settings.
    /// </summary>
    public class PushPullTests
    {
        private CharacterTestRig rig;
        private PushableObject box;
        private const float BoxSize = 1.5f;

        [SetUp]
        public void SetUp() => rig = new CharacterTestRig();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            rig.Dispose();
            yield return null;
        }

        private float PushPullSpeed => rig.Mode<PushPullMode>().pushPullSpeed * rig.Movement.SpeedScale;
        private bool Grabbing => rig.InState(CharacterState.Grabbing);
        /// <summary>The box's side facing the character (its -Z side).</summary>
        private float BoxNearSide => box.Position.z - BoxSize / 2f;

        /// <summary>The character on a floor (at floorTop), facing +Z, with a box just in front of it.</summary>
        private IEnumerator SetUpBox(float speedMultiplier = 1f, bool canBePulled = true, float floorTop = 0f, float floorWidth = 100f)
        {
            rig.Floor(floorTop, floorWidth);
            yield return rig.SpawnSettled(new Vector3(0f, floorTop + 0.05f, 0f));
            float near = rig.Position.z + rig.Radius + 0.1f;
            box = rig.PushableBox(new Vector3(0f, floorTop + BoxSize / 2f, near + BoxSize / 2f), Vector3.one * BoxSize, speedMultiplier, canBePulled);
            yield return rig.Run(0.1f, _ => CharacterTestRig.Idle());
        }

        /// <summary>Holds grab and steers in a direction for the given time (after taking hold), measuring how far the box and the character moved.</summary>
        private IEnumerator HoldAndSteer(Vector3 direction, float seconds, System.Action<float, float> moved)
        {
            yield return rig.Run(0.2f, step => CharacterTestRig.Grab(step, direction));
            float boxStart = box.Position.z, characterStart = rig.Position.z;
            yield return rig.Run(seconds, step => CharacterTestRig.Grab(step + 10, direction));
            moved(box.Position.z - boxStart, rig.Position.z - characterStart);
        }

        [UnityTest]
        public IEnumerator HoldingGrab_AndSteeringIntoABox_PushesIt_AtThePushSpeed()
        {
            yield return SetUpBox();
            const float seconds = 1f;
            float boxMoved = 0f, characterMoved = 0f;
            yield return HoldAndSteer(Vector3.forward, seconds, (b, c) => { boxMoved = b; characterMoved = c; });

            Assert.That(Grabbing, Is.True, "The character should be holding the box.");
            Assert.That(boxMoved, Is.EqualTo(PushPullSpeed * seconds).Within(0.1f * PushPullSpeed * seconds), "The box should be pushed at the push speed.");
            Assert.That(characterMoved, Is.EqualTo(boxMoved).Within(0.02f), "The character should move with the box.");
            Assert.That(rig.Position.z + rig.Radius, Is.LessThanOrEqualTo(BoxNearSide + 0.02f), "The character shouldn't go into the box.");
            Assert.That(Vector3.Angle(rig.Body.rotation * Vector3.forward, Vector3.forward), Is.LessThan(1f), "The character should face the box.");
        }

        [UnityTest]
        public IEnumerator SteeringAwayWhileHoldingABox_PullsIt()
        {
            yield return SetUpBox();
            const float seconds = 1f;
            float boxMoved = 0f, characterMoved = 0f;
            yield return HoldAndSteer(Vector3.back, seconds, (b, c) => { boxMoved = b; characterMoved = c; });

            Assert.That(-boxMoved, Is.EqualTo(PushPullSpeed * seconds).Within(0.1f * PushPullSpeed * seconds), "The box should be pulled at the pull speed.");
            Assert.That(characterMoved, Is.EqualTo(boxMoved).Within(0.02f), "The box should follow the character.");
            Assert.That(Vector3.Angle(rig.Body.rotation * Vector3.forward, Vector3.forward), Is.LessThan(1f), "The character should keep facing the box.");
        }

        [UnityTest]
        public IEnumerator AHeavierBox_MovesSlower()
        {
            yield return SetUpBox(speedMultiplier: 0.5f);
            const float seconds = 1f;
            float boxMoved = 0f;
            yield return HoldAndSteer(Vector3.forward, seconds, (b, _) => boxMoved = b);
            Assert.That(boxMoved, Is.EqualTo(0.5f * PushPullSpeed * seconds).Within(0.05f * PushPullSpeed * seconds),
                "A box's speed multiplier should scale how fast it's pushed.");
        }

        [UnityTest]
        public IEnumerator ABoxThatCantBePulled_StaysPut_AndSoDoesTheCharacter()
        {
            yield return SetUpBox(canBePulled: false);
            float boxMoved = 1f, characterMoved = 1f;
            yield return HoldAndSteer(Vector3.back, 0.5f, (b, c) => { boxMoved = b; characterMoved = c; });
            Assert.That(Grabbing, Is.True);
            Assert.That(Mathf.Abs(boxMoved), Is.LessThan(0.01f), "A box that can't be pulled shouldn't move.");
            Assert.That(Mathf.Abs(characterMoved), Is.LessThan(0.01f), "The character holding it shouldn't move either.");
        }

        [UnityTest]
        public IEnumerator PushingABoxIntoAWall_StopsBothAtTheWall()
        {
            yield return SetUpBox();
            float wall = box.Position.z + BoxSize / 2f + 0.5f;
            rig.Box("Wall", new Vector3(0f, 2f, wall + 0.5f), new Vector3(10f, 4f, 1f));
            yield return HoldAndSteer(Vector3.forward, 1.5f, (_, _) => { });

            Assert.That(box.Position.z + BoxSize / 2f, Is.LessThanOrEqualTo(wall + 0.005f), "The box shouldn't go into the wall.");
            Assert.That(box.Position.z + BoxSize / 2f, Is.GreaterThan(wall - 0.05f), "The box should be pushed up to the wall.");
            Assert.That(rig.Position.z + rig.Radius, Is.LessThanOrEqualTo(BoxNearSide + 0.02f), "The character shouldn't go into the box.");
        }

        [UnityTest]
        public IEnumerator LettingGo_LeavesTheBox_WhichBlocksTheCharacterAgain()
        {
            yield return SetUpBox();
            yield return HoldAndSteer(Vector3.forward, 0.3f, (_, _) => { });
            float boxZ = box.Position.z;
            yield return rig.Run(0.6f, _ => CharacterTestRig.Move(Vector3.forward));
            Assert.That(Grabbing, Is.False, "Letting go of grab should let go of the box.");
            Assert.That(box.Position.z, Is.EqualTo(boxZ).Within(0.001f), "Walking into a box without holding it shouldn't move it.");
            Assert.That(rig.Position.z + rig.Radius, Is.LessThanOrEqualTo(BoxNearSide + 0.01f), "The box should block the character.");
        }

        [UnityTest]
        public IEnumerator PushingABoxOffAnEdge_ItFalls_AndTheCharacterLetsGo()
        {
            // A raised floor that ends a little past the box.
            const float raised = 2f;
            rig.Floor(-raised, 100f);
            yield return SetUpBox(floorTop: 0f, floorWidth: 6f);
            float edgeZ = 3f;
            Assert.That(box.Position.z + BoxSize / 2f, Is.LessThan(edgeZ), "Test setup: the box should start on the raised floor.");

            bool letGo = false;
            yield return rig.Run((edgeZ + BoxSize) / PushPullSpeed + 1f, step => CharacterTestRig.Grab(step, Vector3.forward),
                _ => { if (box.Position.y < BoxSize / 2f - 0.5f && !Grabbing) { letGo = true; } });
            yield return rig.Run(1f, _ => CharacterTestRig.Idle());

            Assert.That(box.Position.y, Is.EqualTo(-raised + BoxSize / 2f).Within(0.05f), "Pushed off the edge, the box should fall to the floor below.");
            Assert.That(letGo, Is.True, "The character should let go of a box that falls.");
        }

        [UnityTest]
        public IEnumerator WithoutHoldingGrab_ABoxIsJustAnObstacle()
        {
            yield return SetUpBox();
            float boxZ = box.Position.z;
            bool grabbed = false;
            yield return rig.Run(0.6f, _ => CharacterTestRig.Move(Vector3.forward), _ => grabbed |= Grabbing);
            Assert.That(grabbed, Is.False);
            Assert.That(box.Position.z, Is.EqualTo(boxZ).Within(0.001f), "Walking into a box shouldn't move it.");
        }

        [UnityTest]
        public IEnumerator JumpingWhileHoldingABox_DoesNothing()
        {
            yield return SetUpBox();
            yield return rig.Run(0.2f, step => CharacterTestRig.Grab(step));
            float startY = rig.Position.y;
            float highest = startY;
            yield return rig.Run(0.4f, step =>
            {
                Inputs inputs = CharacterTestRig.Grab(step + 10);
                inputs.jump = CharacterTestRig.Held(step);
                return inputs;
            }, _ => highest = Mathf.Max(highest, rig.Position.y));
            Assert.That(highest - startY, Is.LessThan(0.01f), "The character shouldn't jump while holding a box.");
            Assert.That(Grabbing, Is.True);
        }
    }
}
