using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CharacterShowcase.Tests
{
    /// <summary>Tests that the character rides moving and spinning platforms and keeps their momentum when leaving.</summary>
    public class MovingGroundTests
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

        [UnityTest]
        public IEnumerator StandingOnSlidingPlatform_MovesWithIt()
        {
            // Travels 6 m out over 3 s at a constant 2 m/s (period 6 s, no easing or pauses).
            var platform = rig.Platform(Vector3.zero, new Vector3(4f, 0.3f, 4f), new Vector3(6f, 0f, 0f), 6f, Vector3.zero);
            Rigidbody platformBody = platform.GetComponent<Rigidbody>();
            rig.SpawnCharacter(new Vector3(0f, 0.05f, 0f));

            yield return rig.Run(0.3f, _ => CharacterTestRig.Idle());
            Assert.That(rig.Grounded, Is.True, "Character should be standing on the platform.");
            Vector3 offset = rig.Position - platformBody.position;
            float platformStart = platformBody.position.x;

            yield return rig.Run(2f, _ => CharacterTestRig.Idle());

            Assert.That(platformBody.position.x - platformStart, Is.GreaterThan(2f), "Test setup: the platform should have moved.");
            Vector3 drift = rig.Position - platformBody.position - offset;
            Assert.That(drift.magnitude, Is.LessThan(0.05f), "Character slid relative to the platform instead of riding it.");
        }

        [UnityTest]
        public IEnumerator StandingOnElevator_RidesUp()
        {
            // Rises 3 m over 3 s.
            var platform = rig.Platform(Vector3.zero, new Vector3(4f, 0.3f, 4f), new Vector3(0f, 3f, 0f), 6f, Vector3.zero);
            Rigidbody platformBody = platform.GetComponent<Rigidbody>();
            rig.SpawnCharacter(new Vector3(0f, 0.05f, 0f));

            yield return rig.Run(0.3f, _ => CharacterTestRig.Idle());
            float heightAbovePlatform = rig.Position.y - platformBody.position.y;

            bool alwaysGrounded = true;
            yield return rig.Run(2f, _ => CharacterTestRig.Idle(), _ => alwaysGrounded &= rig.Grounded);

            Assert.That(alwaysGrounded, Is.True, "Character lost contact with the rising elevator.");
            Assert.That(rig.Position.y - platformBody.position.y, Is.EqualTo(heightAbovePlatform).Within(0.03f),
                "Character should stay on top of the elevator.");
        }

        [UnityTest]
        public IEnumerator StandingOnSpinningPlatform_IsCarriedAroundAndTurned()
        {
            const float degreesPerSecond = 90f;
            var platform = rig.Platform(Vector3.zero, new Vector3(6f, 0.3f, 6f), Vector3.zero, 6f, new Vector3(0f, degreesPerSecond, 0f));
            Rigidbody platformBody = platform.GetComponent<Rigidbody>();
            rig.SpawnCharacter(new Vector3(1.5f, 0.05f, 0f));

            yield return rig.Run(0.3f, _ => CharacterTestRig.Idle());
            Vector3 startOffset = Vector3.ProjectOnPlane(rig.Position - platformBody.position, Vector3.up);
            float startYaw = rig.Body.rotation.eulerAngles.y;

            const float seconds = 1f;
            yield return rig.Run(seconds, _ => CharacterTestRig.Idle());

            Vector3 endOffset = Vector3.ProjectOnPlane(rig.Position - platformBody.position, Vector3.up);
            float carriedAngle = Vector3.SignedAngle(startOffset, endOffset, Vector3.up);
            float turnedAngle = Mathf.DeltaAngle(startYaw, rig.Body.rotation.eulerAngles.y);
            float expected = degreesPerSecond * seconds;

            Assert.That(endOffset.magnitude, Is.EqualTo(startOffset.magnitude).Within(0.05f),
                "Character should stay the same distance from the platform's center.");
            Assert.That(carriedAngle, Is.EqualTo(expected).Within(5f), "Character should be carried around with the platform.");
            Assert.That(turnedAngle, Is.EqualTo(expected).Within(5f), "Character should turn with the platform.");
        }

        [UnityTest]
        public IEnumerator JumpingOffMovingPlatform_KeepsItsMomentum()
        {
            // A long, fast platform: 20 m out over 4 s = 5 m/s.
            var platform = rig.Platform(Vector3.zero, new Vector3(4f, 0.3f, 4f), new Vector3(20f, 0f, 0f), 8f, Vector3.zero);
            rig.SpawnCharacter(new Vector3(0f, 0.05f, 0f));
            yield return rig.Run(0.4f, _ => CharacterTestRig.Idle());
            Assert.That(rig.Grounded, Is.True);

            float startX = rig.Position.x;
            int holdSteps = CharacterTestRig.StepsFor(0.15f);
            const float airTime = 0.3f;
            yield return rig.Run(airTime, step => step < holdSteps ? CharacterTestRig.Jump(step) : CharacterTestRig.Idle());

            float travelled = rig.Position.x - startX;
            const float platformSpeed = 5f;
            Assert.That(travelled, Is.GreaterThan(0.6f * platformSpeed * airTime),
                "Character stopped dead on leaving the platform instead of keeping its velocity.");
            Assert.That(travelled, Is.LessThan(1.4f * platformSpeed * airTime), "Character gained far more speed than the platform had.");
        }
    }
}
