using System.Collections;
using Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CharacterShowcase.Tests
{
    /// <summary>
    /// Tests of sprinting: holding Sprint moves the character at its movement mode's run speed instead of its walk
    /// speed, and letting go walks again (keeping the sprint's speed in the air is in GroundMomentumTests). Speeds are
    /// read from the movement mode, so retuning them doesn't break the tests.
    /// </summary>
    [Category(TestArea.Movement)]
    public class SprintTests : CharacterRigFixture
    {

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


        [UnityTest]
        public IEnumerator Sprinting_MovesAtRunSpeed_AndWalkSpeedOnceReleased()
        {
            yield return Stand();

            yield return rig.Run(0.3f, step => CharacterTestRig.Sprint(Vector3.forward));
            float sprinting = 0f;
            yield return rig.MeanSpeed(0.3f, step => CharacterTestRig.Sprint(Vector3.forward), s => sprinting = s);
            Assert.That(sprinting, Is.EqualTo(rig.RunSpeed).Within(0.05f * rig.RunSpeed), "Holding Sprint should move the character at its run speed.");

            yield return rig.Run(0.3f, _ => CharacterTestRig.Move(Vector3.forward));
            float walking = 0f;
            yield return rig.MeanSpeed(0.3f, _ => CharacterTestRig.Move(Vector3.forward), s => walking = s);
            Assert.That(walking, Is.EqualTo(rig.WalkSpeed).Within(0.05f * rig.WalkSpeed), "Letting go of Sprint should walk again at the walk speed.");
        }

        [UnityTest]
        public IEnumerator SprintingWithoutMovementInput_StaysStill()
        {
            yield return Stand();
            Vector3 start = rig.Position;
            yield return rig.Run(0.5f, step => CharacterTestRig.Sprint(Vector3.zero));
            Vector3 moved = rig.Position - start;
            Assert.That(new Vector2(moved.x, moved.z).magnitude, Is.LessThan(0.05f), "Holding Sprint alone shouldn't move the character.");
        }
    }
}
