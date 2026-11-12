using System.Collections;
using Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CharacterShowcase.Tests
{
    /// <summary>
    /// Tests of sprinting: holding Sprint moves the character at its movement mode's run speed instead of its walk
    /// speed, on the ground and steering in the air, and letting go walks again. Speeds are read from the movement
    /// mode, so retuning them doesn't break the tests.
    /// </summary>
    public class SprintTests
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

        private float HorizontalSpeed
        {
            get
            {
                Vector3 v = rig.Movement.lastFrameMovementData.finalVelocity;
                return new Vector2(v.x, v.z).magnitude;
            }
        }

        /// <summary>Sets up a floor and the character on it, standing still, and skips the test if it can't sprint.</summary>
        private IEnumerator Stand()
        {
            rig.Floor();
            rig.SpawnCharacter(new Vector3(0f, 0.05f, 0f));
            if (!(rig.RunSpeed > rig.WalkSpeed))
            {
                Assert.Ignore($"The character's run speed ({rig.RunSpeed:0.00} m/s) isn't faster than its walk speed ({rig.WalkSpeed:0.00} m/s), so it doesn't sprint.");
            }
            yield return rig.Run(0.3f, _ => CharacterTestRig.Idle());
        }

        /// <summary>The character's mean horizontal speed over the given time, with the given inputs.</summary>
        private IEnumerator MeanSpeed(float seconds, System.Func<int, Inputs> inputs, System.Action<float> result)
        {
            float total = 0f;
            int steps = 0;
            yield return rig.Run(seconds, inputs, _ =>
            {
                total += HorizontalSpeed;
                steps++;
            });
            result(total / steps);
        }

        [UnityTest]
        public IEnumerator Sprinting_MovesAtRunSpeed_AndWalkSpeedOnceReleased()
        {
            yield return Stand();

            yield return rig.Run(0.3f, step => CharacterTestRig.Sprint(Vector3.forward, step));
            float sprinting = 0f;
            yield return MeanSpeed(0.3f, step => CharacterTestRig.Sprint(Vector3.forward, step + 100), s => sprinting = s);
            Assert.That(sprinting, Is.EqualTo(rig.RunSpeed).Within(0.05f * rig.RunSpeed), "Holding Sprint should move the character at its run speed.");

            yield return rig.Run(0.3f, _ => CharacterTestRig.Move(Vector3.forward));
            float walking = 0f;
            yield return MeanSpeed(0.3f, _ => CharacterTestRig.Move(Vector3.forward), s => walking = s);
            Assert.That(walking, Is.EqualTo(rig.WalkSpeed).Within(0.05f * rig.WalkSpeed), "Letting go of Sprint should walk again at the walk speed.");
        }

        [UnityTest]
        public IEnumerator SprintingWithoutMovementInput_StaysStill()
        {
            yield return Stand();
            Vector3 start = rig.Position;
            yield return rig.Run(0.5f, step => CharacterTestRig.Sprint(Vector3.zero, step));
            Vector3 moved = rig.Position - start;
            Assert.That(new Vector2(moved.x, moved.z).magnitude, Is.LessThan(0.05f), "Holding Sprint alone shouldn't move the character.");
        }

        [UnityTest]
        public IEnumerator SprintJump_KeepsRunSpeedInTheAir_WithoutStacking()
        {
            yield return Stand();
            yield return rig.Run(0.4f, step => CharacterTestRig.Sprint(Vector3.forward, step));

            // Jump while sprinting, and keep sprinting forward through the jump.
            float fastest = 0f;
            yield return rig.Run(0.8f, step =>
            {
                Inputs inputs = step < 5 ? CharacterTestRig.Jump(step, Vector3.forward) : CharacterTestRig.Move(Vector3.forward);
                inputs.sprint = CharacterTestRig.Sprint(Vector3.forward, step + 100).sprint;
                return inputs;
            }, _ =>
            {
                if (!rig.Grounded) { fastest = Mathf.Max(fastest, HorizontalSpeed); }
            });

            Assert.That(fastest, Is.GreaterThan(0.9f * rig.RunSpeed), "Jumping while sprinting should keep the sprinting speed.");
            Assert.That(fastest, Is.LessThan(1.05f * rig.RunSpeed), "Air steering stacked on top of the carried sprint momentum.");
        }
    }
}
