using System.Collections;
using System.Collections.Generic;
using MeridianMotion.Characters;
using MeridianMotion.Levels;
using NUnit.Framework;
using UnityEngine.TestTools;
using UnityEngine;

namespace MeridianMotion.Tests
{
    /// <summary>
    /// A fixture of tests on an ability that happens in a moment (the movement component T raising MomentEvent, one of
    /// its OneOffEvents): a jump, a bounce, with no state that's under way for a while (AbilityFixture is the kit for
    /// those). It gets the checks every such ability should pass, as its own tests:
    /// - it happens from its inputs (the fixture's own set-up, checked);
    /// - turned off by its toggle, or by its 0 setting (OffSetting), it doesn't happen;
    /// - once it's happened, it doesn't happen again straight away (for RepeatSeconds) from the same inputs: one press,
    ///   one jump;
    /// - saved before it happens and put back, the character steps on exactly as it did, the moment included;
    /// - at another size, it still happens.
    /// A fixture supplies how to set it up (SetUpToStart, spawning with SpawnCharacter), its inputs (InputsFor), its 0
    /// setting (OffSetting) and its event (MomentEvent), then adds the tests about the ability itself. It needs its area's
    /// [Category(TestArea.…)] of its own; [TestTiming] comes with CharacterRigFixture.
    /// </summary>
    public abstract class MomentFixture<T> : AbilityKitBase<T> where T : MovementComponent
    {
        /// <summary>The event the ability raises as it happens: one of its component's OneOffEvents.</summary>
        protected abstract MovementEvents MomentEvent { get; }

        /// <summary>How long after it's happened the same inputs mustn't make it happen again.</summary>
        protected virtual float RepeatSeconds => 0.25f;

        private bool Happened => rig.Count(MomentEvent) > 0;

        /// <summary>Runs the fixture's inputs until the moment happens, or for SecondsToStart.</summary>
        private IEnumerator RunUntilItHappens()
        {
            int limit = CharacterTestRig.StepsFor(SecondsToStart);
            while (!Happened && StepsRun < limit) { yield return RunInputs(1); }
        }

        [UnityTest]
        public IEnumerator Kit_ItHappens_FromItsInputs()
        {
            yield return SetUp();
            Assert.That((Component.OneOffEvents & MomentEvent) != 0, Is.True,
                $"{typeof(T).Name} should declare {MomentEvent} among its OneOffEvents (see MovementComponent.OneOffEvents).");
            yield return RunUntilItHappens();
            Assert.That(Happened, Is.True, $"Test setup: {typeof(T).Name} should raise {MomentEvent} from the fixture's inputs within " +
                                          $"{SecondsToStart} s (at {rig.Position}): every other check of the kit relies on it.");
        }

        [UnityTest]
        public IEnumerator Kit_TurnedOffByItsToggle_ItDoesntHappen()
        {
            yield return SetUp();
            Component.Enabled = false;
            yield return RunInputs(CharacterTestRig.StepsFor(SecondsToStart + 0.25f));
            Assert.That(Happened, Is.False, $"With {typeof(T).Name} turned off, it shouldn't raise {MomentEvent}.");
        }

        [UnityTest]
        public IEnumerator Kit_TurnedOffByItsZeroSetting_ItDoesntHappen()
        {
            if (OffSetting == null) { Assert.Ignore($"{typeof(T).Name} has no setting whose 0 turns it off (OffSetting)."); }
            yield return SetUp();
            TurnOffBySetting();
            yield return RunInputs(CharacterTestRig.StepsFor(SecondsToStart + 0.25f));
            Assert.That(Happened, Is.False, $"With {OffSetting} at 0, {typeof(T).Name} shouldn't raise {MomentEvent}.");
        }

        [UnityTest]
        public IEnumerator Kit_ItHappensOnce_NotAgainStraightAway()
        {
            yield return SetUp();
            yield return RunUntilItHappens();
            Assume.That(Happened, Is.True, "Test setup: it should have happened.");
            yield return RunInputs(CharacterTestRig.StepsFor(RepeatSeconds));
            Assert.That(rig.Count(MomentEvent), Is.EqualTo(1), $"Its inputs carried on, {MomentEvent} shouldn't be raised again within {RepeatSeconds} s.");
        }

        [UnityTest]
        public IEnumerator Kit_SavedBeforeItAndPutBack_StepsOnExactlyAsItDid()
        {
            yield return SetUp();
            MovementStateSnapshot saved = rig.Movement.SaveState();
            WorldStateSnapshot world = WorldStateSnapshot.Save();
            long levelTick = rig.LevelTick;

            int steps = CharacterTestRig.StepsFor(SecondsToStart) + StepsCompared;
            var first = new List<(Vector3, Quaternion, Vector3, bool)>();
            yield return RunInputs(steps, _ => first.Add(Step()));
            Assume.That(Happened, Is.True, "Test setup: it should have happened.");
            Assert.That(rig.Movement.RestoreState(saved), Is.True, "The saved state should be put back.");
            world.Restore();
            rig.LevelTick = levelTick;
            RewindTo(0);
            rig.ClearEvents();
            var again = new List<(Vector3, Quaternion, Vector3, bool)>();
            yield return RunInputs(steps, _ => again.Add(Step()));

            for (int i = 0; i < first.Count; i++)
            {
                Assert.That(again[i], Is.EqualTo(first[i]), $"Put back, step {i} should be exactly as it was (position, rotation, velocity, and whether it happened).");
            }
        }

        // The pose and velocity, and whether the moment happened, this step.
        private (Vector3, Quaternion, Vector3, bool) Step()
        {
            (Vector3 position, Quaternion rotation, Vector3 velocity) = Pose();
            return (position, rotation, velocity, (rig.Movement.lastStep.events & MomentEvent) != 0);
        }

        [UnityTest]
        public IEnumerator Kit_AtAnotherSize_ItStillHappens([Values(0.5f, 2f)] float scale)
        {
            yield return SetUp(scale);
            yield return RunUntilItHappens();
            Assert.That(Happened, Is.True, $"At {scale} times its size, {typeof(T).Name} should still raise {MomentEvent} from the fixture's inputs " +
                                          "(its speeds and distances scale with the character: see docs/adding-an-ability.md).");
        }
    }
}
