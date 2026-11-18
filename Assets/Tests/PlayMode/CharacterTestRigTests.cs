using System.Collections;
using Character;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace CharacterShowcase.Tests
{
    /// <summary>
    /// Tests of the test rig itself (CharacterTestRig), which every character test relies on: a test's change to a
    /// component's settings is to the rig's own copy of the movement mode, not the asset; and a setting asked for as the
    /// wrong type fails the test, where one the mode doesn't have ignores it.
    /// </summary>
    [Category(TestArea.Movement)]
    public class CharacterTestRigTests : CharacterRigFixture
    {
        [UnityTest]
        public IEnumerator AComponentsSettings_ChangedByATest_AreTheRigsOwnCopy()
        {
            yield return rig.StandOnFloor();
            var asset = CharacterTestRig.CharacterPrefab().GetComponent<MovementController>().movementHandler as ComposedMovementMode;
            Assume.That(asset, Is.Not.Null, "Test setup: the character's movement mode should be made of components.");
            float authored = asset.Get<Ziplines>(includeDisabled: true).ZiplineGrabReach;
            rig.SetMovementSetting("ziplineGrabReach", authored + 1f);
            Assert.That(rig.Component<Ziplines>().ZiplineGrabReach, Is.EqualTo(authored + 1f));
            Assert.That(asset.Get<Ziplines>(includeDisabled: true).ZiplineGrabReach, Is.EqualTo(authored), "The asset's component should be left as it was.");
        }

        /// <summary>
        /// A test asking for a setting as the wrong type (an int literal for a float setting) fails, rather than being
        /// ignored as a test of a setting the mode doesn't have, which would quietly stop it testing anything.
        /// </summary>
        [UnityTest]
        public IEnumerator ASettingOfAnotherType_FailsTheTest_WhereAMissingOneIgnoresIt()
        {
            yield return rig.StandOnFloor();
            // Asked for as the wrong type on purpose, through a constant: the hygiene check fails a test that reads a
            // setting by its name as another type than it's declared.
            const string floatSetting = "ziplineGrabReach";
            Assert.Throws<AssertionException>(() => rig.MovementSetting<int>(floatSetting),
                "Reading a float setting as an int should fail the test.");
            Assert.Throws<AssertionException>(() => rig.SetMovementSetting(floatSetting, 0),
                "Setting a float setting with an int should fail the test.");
            // Named through a constant: the hygiene check fails a setting name the tests use that no movement code
            // declares, as this one deliberately isn't.
            const string missing = "noSuchSetting";
            Assert.Throws<IgnoreException>(() => rig.MovementSetting<float>(missing),
                "A setting the mode doesn't have should ignore the test.");
            Assert.That(rig.MovementSetting<float>("ziplineGrabReach"), Is.EqualTo(rig.Component<Ziplines>().ZiplineGrabReach),
                "Asked for as its own type, the setting should be found.");
        }
    }
}
