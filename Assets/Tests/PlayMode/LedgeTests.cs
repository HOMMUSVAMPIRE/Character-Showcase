using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CharacterShowcase.Tests
{
    /// <summary>
    /// Tests of ledges: a character in mid-air heading toward a ledge within reach of its hands catches hold and hangs
    /// below it; Jump climbs up onto it (crouched, if there's only room to crouch on top), and Crouch or steering away
    /// lets go; ledges out of reach, or passed while rising fast, aren't grabbed; a moving ledge carries the character
    /// hanging from it; and a jump up to a ledge grabs it. Expected values are read from the movement mode's settings.
    /// </summary>
    public class LedgeTests
    {
        private CharacterTestRig rig;
        // The ledge's top (y) and its wall's face (z), which faces -Z, toward the character.
        private float top, face;

        [SetUp]
        public void SetUp() => rig = new CharacterTestRig();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            rig.Dispose();
            yield return null;
        }

        private float HangHeight => rig.MovementSetting<float>("ledgeHangHeight") * rig.StandingHeight;
        private float ClimbDuration => rig.Mode<LedgeMode>().ledgeClimbDuration * rig.Movement.DurationScale;
        private bool Hanging => rig.InState(CharacterState.LedgeHanging);
        private bool Climbing => rig.InState(CharacterState.LedgeClimbing);

        /// <summary>
        /// A floor, the character in mid-air facing +Z (its model loaded), and in front of it a block whose top is
        /// ledgeAbove standing heights above the character's feet, its face just within reach.
        /// </summary>
        private IEnumerator SetUpLedge(float ledgeAbove = 1f)
        {
            yield return SpawnInMidAir();
            BuildLedge(ledgeAbove);
        }

        private IEnumerator SpawnInMidAir()
        {
            rig.Floor();
            yield return rig.SpawnSettled(new Vector3(0f, 3f, 0f), settleSeconds: 0f);
        }

        /// <summary>The block in front of the character, its top ledgeAbove standing heights above the character's feet.</summary>
        private void BuildLedge(float ledgeAbove)
        {
            top = rig.Position.y + ledgeAbove * rig.StandingHeight;
            face = rig.Position.z + rig.Radius + 0.15f;
            rig.Box("Ledge Block", new Vector3(0f, top / 2f, face + 2f), new Vector3(10f, top, 4f));
        }

        /// <summary>Lets the character fall toward the ledge, steering into it, until it has settled into hanging.</summary>
        private IEnumerator GrabAndHang()
        {
            yield return rig.Run(0.5f, _ => CharacterTestRig.Move(Vector3.forward));
            Assert.That(Hanging, Is.True, "Test setup: the character should be hanging from the ledge.");
        }

        [UnityTest]
        public IEnumerator FallingInFrontOfALedge_GrabsIt_AndHangsBelowIt()
        {
            yield return SetUpLedge();
            yield return GrabAndHang();
            Vector3 hanging = rig.Position;
            yield return rig.Run(1f, _ => CharacterTestRig.Idle());

            Assert.That(Hanging, Is.True, "The character should keep hanging.");
            Assert.That(rig.Position.y, Is.EqualTo(top - HangHeight).Within(0.02f), "The character should hang its hang height below the ledge.");
            Assert.That(rig.Position.z + rig.Radius, Is.EqualTo(face).Within(0.06f), "The character should hang against the ledge's wall.");
            Assert.That(Vector3.Distance(rig.Position, hanging), Is.LessThan(0.01f), "Hanging should hold still, without falling.");
            Assert.That(Vector3.Angle(rig.Body.rotation * Vector3.forward, Vector3.forward), Is.LessThan(1f), "The character should face the wall.");
        }

        [UnityTest]
        public IEnumerator JumpingWhileHanging_ClimbsUpOntoTheLedge()
        {
            yield return SetUpLedge();
            yield return GrabAndHang();

            int climbingSteps = 0;
            yield return rig.Run(ClimbDuration + 0.5f, step => step == 0 ? CharacterTestRig.Jump(0) : CharacterTestRig.Idle(),
                _ => { if (Climbing) { climbingSteps++; } });

            Assert.That(climbingSteps, Is.EqualTo(CharacterTestRig.StepsFor(ClimbDuration)).Within(1), "A climb should take its duration.");
            Assert.That(rig.Grounded, Is.True, "The character should stand on top of the ledge.");
            Assert.That(rig.Position.y, Is.EqualTo(top).Within(0.03f), "The character should stand on the ledge's top.");
            Assert.That(rig.Position.z - rig.Radius, Is.GreaterThan(face), "The character should be fully on top of the ledge.");
            Assert.That(rig.InState(CharacterState.Crouching), Is.False, "With room on top, the character should stand.");
        }

        [UnityTest]
        public IEnumerator PressingCrouchWhileHanging_LetsGo_AndFalls()
        {
            yield return SetUpLedge();
            yield return GrabAndHang();
            float hangY = rig.Position.y;
            yield return rig.Run(0.3f, step => step == 0 ? CharacterTestRig.Crouch(0) : CharacterTestRig.Idle());
            Assert.That(Hanging, Is.False, "Pressing Crouch should let go of the ledge.");
            Assert.That(rig.Position.y, Is.LessThan(hangY - 0.2f), "Letting go should fall.");
        }

        [UnityTest]
        public IEnumerator SteeringAwayWhileHanging_LetsGo()
        {
            yield return SetUpLedge();
            yield return GrabAndHang();
            yield return rig.Run(0.2f, _ => CharacterTestRig.Move(Vector3.back));
            Assert.That(Hanging, Is.False, "Steering away from the wall should let go of the ledge.");
        }

        [UnityTest]
        public IEnumerator ALedgeAboveReach_IsNotGrabbed()
        {
            yield return SpawnInMidAir();
            BuildLedge(1.15f * rig.MovementSetting<float>("ledgeGrabMaxHeight"));
            bool hung = false;
            yield return rig.Run(0.8f, _ => CharacterTestRig.Move(Vector3.forward), _ => hung |= Hanging);
            Assert.That(hung, Is.False, "A ledge above the character's reach shouldn't be grabbed.");
        }

        [UnityTest]
        public IEnumerator RisingFastPastALedge_DoesNotGrabIt()
        {
            yield return SetUpLedge();
            float maxRise = rig.MovementSetting<float>("ledgeMaxGrabRiseSpeed") * rig.Movement.SpeedScale;
            yield return rig.Launch(Vector3.up * (maxRise + 10f), CharacterTestRig.Move(Vector3.forward));
            bool hungWhileRisingFast = false;
            yield return rig.Run(0.15f, _ => CharacterTestRig.Move(Vector3.forward), _ =>
            {
                if (Hanging && rig.Movement.lastFrameMovementData.initialVelocity.y > maxRise) { hungWhileRisingFast = true; }
            });
            Assert.That(hungWhileRisingFast, Is.False, "A ledge shouldn't be grabbed while rising fast.");
        }

        [UnityTest]
        public IEnumerator WithRoomOnlyToCrouchOnTop_ClimbsUpCrouched()
        {
            yield return SetUpLedge();
            float crouchHeight = rig.Mode<CrouchMode>().crouchHeight;
            float clearance = rig.StandingHeight * Mathf.Lerp(crouchHeight, 1f, 0.5f);
            // A ceiling over the ledge's top (not over the air in front of its wall).
            rig.Box("Low Ceiling", new Vector3(0f, top + clearance + 0.5f, face + 2f), new Vector3(10f, 1f, 4f));
            yield return GrabAndHang();

            yield return rig.Run(ClimbDuration + 0.5f, step => step == 0 ? CharacterTestRig.Jump(0) : CharacterTestRig.Idle());
            Assert.That(rig.Grounded, Is.True, "The character should be on top of the ledge.");
            Assert.That(rig.Position.y, Is.EqualTo(top).Within(0.03f));
            Assert.That(rig.Position.z - rig.Radius, Is.GreaterThan(face));
            Assert.That(rig.InState(CharacterState.Crouching), Is.True, "With only room to crouch, the climb should end crouched.");
            Assert.That(rig.CurrentHeight, Is.LessThan(clearance));
        }

        [UnityTest]
        public IEnumerator HangingFromAMovingLedge_MovesWithIt()
        {
            rig.Floor();
            yield return rig.SpawnSettled(new Vector3(0f, 3f, 0f), settleSeconds: 0f);
            top = 3f + rig.StandingHeight;
            face = rig.Radius + 0.15f;
            // A platform sliding along X, its -Z side the ledge.
            var platform = rig.Platform(new Vector3(0f, top, face + 2f), new Vector3(6f, 1f, 4f), new Vector3(3f, 0f, 0f), 4f, Vector3.zero);
            yield return GrabAndHang();

            Rigidbody body = platform.GetComponent<Rigidbody>();
            float offset = rig.Position.x - body.position.x;
            float travelled = 0f, startX = body.position.x;
            yield return rig.Run(1f, _ => CharacterTestRig.Idle(), _ => travelled = Mathf.Max(travelled, Mathf.Abs(body.position.x - startX)));

            Assert.That(travelled, Is.GreaterThan(0.5f), "Test setup: the platform should have moved.");
            Assert.That(Hanging, Is.True, "The character should keep hanging from the moving ledge.");
            Assert.That(rig.Position.x - body.position.x, Is.EqualTo(offset).Within(0.1f), "The character should move with the ledge.");
        }

        [UnityTest]
        public IEnumerator JumpingUpToALedge_GrabsIt_ThenClimbsOntoIt()
        {
            rig.Floor();
            yield return rig.SpawnSettled(new Vector3(0f, 0.05f, 0f));
            // How high the character's feet get in a full jump.
            float apex = 0f;
            yield return rig.Run(0.6f, step => CharacterTestRig.Jump(step), _ => apex = Mathf.Max(apex, rig.Position.y));
            yield return rig.Run(1.5f, _ => CharacterTestRig.Idle());
            Assert.That(rig.Grounded, Is.True, "Test setup: the character should have landed.");

            // A wall whose top is a standing height above the top of the jump.
            top = apex + rig.StandingHeight;
            face = rig.Position.z + rig.Radius + 0.15f;
            rig.Box("Tall Wall", new Vector3(0f, top / 2f, face + 2f), new Vector3(10f, top, 4f));

            var events = new List<MovementEvents>();
            rig.Movement.MovementEventRaised += (movementEvent, _) => events.Add(movementEvent);
            bool hung = false;
            yield return rig.Run(1f, step => CharacterTestRig.Jump(step, Vector3.forward), _ => hung |= Hanging);
            Assert.That(hung, Is.True, "Jumping at a wall whose top is within reach at the top of the jump should grab it.");

            // Let go of Jump (held since the jump), and press it again to climb.
            yield return rig.Run(ClimbDuration + 0.5f, step => step == 1 ? CharacterTestRig.Jump(0) : CharacterTestRig.Idle());
            Assert.That(rig.Grounded, Is.True);
            Assert.That(rig.Position.y, Is.EqualTo(top).Within(0.03f), "The character should have climbed onto the wall.");
            foreach (MovementEvents expected in new[] { MovementEvents.LedgeGrabbed, MovementEvents.LedgeReleased, MovementEvents.LedgeClimbStarted, MovementEvents.LedgeClimbEnded })
            {
                Assert.That(events.Count(e => e == expected), Is.EqualTo(1), $"{expected} should be raised once.");
            }
        }

        [UnityTest]
        public IEnumerator ALedgeAtTheFootOfRisingStairs_ClimbsUpOntoTheStairs()
        {
            yield return SetUpLedge();
            // Steps rising away from the ledge's edge, nearly the step height each and shallower than the character is
            // wide: there's no room to stand at the edge's own height, but there is on the stairs.
            float rise = 0.85f * rig.StepHeight, run = 0.9f * rig.Radius;
            for (int i = 1; i <= 6; i++)
            {
                rig.Box($"Step {i}", new Vector3(0f, top + i * rise / 2f, face + i * run + 2f), new Vector3(10f, i * rise, 4f));
            }
            yield return GrabAndHang();

            yield return rig.Run(ClimbDuration + 0.6f, step => step == 0 ? CharacterTestRig.Jump(0) : CharacterTestRig.Idle());
            Assert.That(Hanging || Climbing, Is.False, "The character should have climbed up off the ledge.");
            Assert.That(rig.Grounded, Is.True, "The character should stand on the stairs.");
            Assert.That(rig.Position.y, Is.GreaterThanOrEqualTo(top - 0.01f), "The character should be up on the stairs.");
            Assert.That(rig.Position.z, Is.GreaterThan(face), "The character should be past the ledge's edge.");
        }

        [UnityTest]
        public IEnumerator WhenTheLedgesObjectIsDeactivated_TheCharacterLetsGo()
        {
            yield return SpawnInMidAir();
            BuildLedge(1f);
            yield return GrabAndHang();
            GameObject.Find("Ledge Block").SetActive(false);
            yield return rig.Run(0.2f, _ => CharacterTestRig.Idle());
            Assert.That(Hanging, Is.False, "With its ledge gone, the character shouldn't keep hanging in mid-air.");
        }

        [UnityTest]
        public IEnumerator ALedgeClimbDurationOfZero_TurnsLedgeGrabbingOff()
        {
            yield return SetUpLedge();
            rig.SetMovementSetting("ledgeClimbDuration", 0f);
            bool hung = false;
            yield return rig.Run(0.5f, _ => CharacterTestRig.Move(Vector3.forward), _ => hung |= Hanging);
            Assert.That(hung, Is.False);
        }
    }
}
