using System.Collections;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace CharacterShowcase.Tests
{
    /// <summary>
    /// A fixture of tests on the character: each test gets a new CharacterTestRig (rig), disposed after it. NUnit runs this
    /// set-up before a fixture's own [SetUp] and this tear-down after its own tear-down, so a fixture adds only what's its
    /// own (clearing a list of events, say).
    /// </summary>
    [TestTiming]
    public abstract class CharacterRigFixture
    {
        /// <summary>The test's rig: the character, its world and its stepping (see CharacterTestRig).</summary>
        protected CharacterTestRig rig;

        /// <summary>
        /// Whether the fixture's tests are of the movement alone, so the rig may step the physics itself, far faster
        /// (see CharacterTestRig.StepsPhysicsItself). A fixture whose tests need Unity's frames (animation, IK, the
        /// model's or camera's per-frame updates) overrides this to false.
        /// </summary>
        protected virtual bool MovementOnly => true;

        [SetUp]
        public void CreateRig() => rig = NewRig();

        /// <summary>
        /// Starts again with a new rig, made as the test's first was (stepping the physics itself or not), for a test
        /// comparing two characters, one after the other: the first is disposed and its world cleared (a frame passes for
        /// that) before the second is made.
        /// </summary>
        protected IEnumerator ReplaceRig()
        {
            rig.Dispose();
            yield return null;
            rig = NewRig();
        }

        private CharacterTestRig NewRig() => new() { StepsPhysicsItself = MovementOnly };

        [UnityTearDown]
        public IEnumerator DisposeRig()
        {
            rig.Dispose();
            yield return null; // Let Destroy finish before the next test builds its world.
        }
    }
}
