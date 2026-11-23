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
    /// A fixture of tests on one ability (the movement component T), which gets the checks every ability should pass from
    /// deriving this, as its own tests (see docs/adding-an-ability.md, Tests and docs). The checks:
    /// - it starts from its inputs, raising its start event (the fixture's own set-up, checked);
    /// - turned off by its toggle, it doesn't start;
    /// - turned off by its 0 setting (OffSetting), it doesn't start;
    /// - turned off either way, the character does what it should instead (WhatHappensInstead: crouches, sinks), if the
    ///   fixture says;
    /// - turned off while it's under way, it lets go, raising its end event (another ability may take the character then,
    ///   as a wall slide does a ledge's as it falls);
    /// - its states' start and end events come in pairs;
    /// - saved partway through (just after it starts, and well into it) and put back, the character steps on exactly as it
    ///   did (with the level's moving parts);
    /// - at another size, it still starts.
    /// A fixture supplies how to set the ability up (SetUpToStart, spawning the character with SpawnCharacter, so it's at
    /// the size a check asks for), the inputs that start it and keep it going (InputsFor), and the setting whose 0 turns it
    /// off (OffSetting); then adds the tests that are about the ability itself. It still needs its area's
    /// [Category(TestArea.…)] of its own: the hygiene check fails without it. [TestTiming] comes with CharacterRigFixture.
    /// </summary>
    public abstract class AbilityFixture<T> : AbilityKitBase<T> where T : MovementComponent
    {
        /// <summary>
        /// The state the ability is under way in: by default, the first state its component declares (its StateEvents).
        /// </summary>
        protected virtual CharacterState AbilityState => FirstStateEvent.state;

        /// <summary>
        /// The ability's state and the events it raises starting and ending: by default, those its component declares for
        /// AbilityState (its StateEvents). A component that raises its own (Dashing does) overrides this.
        /// </summary>
        protected virtual StateEventPair AbilityEvents => EventsOf(AbilityState);

        /// <summary>How many steps into the ability the save-and-put-back check saves the character.</summary>
        protected virtual int StepsBeforeSaving => 3;

        /// <summary>
        /// How many steps further on the check's later save is: well into the ability, for what builds up as it goes (a
        /// rope's swing, a pushed box's speed, a climb carried along). It may have ended by then: what follows is compared all the same.
        /// </summary>
        protected virtual int StepsBeforeSavingLater => 30;

        private StateEventPair FirstStateEvent
        {
            get
            {
                StateEventPair[] events = Component.StateEvents;
                Assert.That(events, Is.Not.Empty, $"{typeof(T).Name} declares no states (StateEvents): give it one, or override AbilityState and AbilityEvents.");
                return events[0];
            }
        }

        private StateEventPair EventsOf(CharacterState state)
        {
            foreach (StateEventPair pair in Component.StateEvents)
            {
                if (pair.state == state) { return pair; }
            }
            Assert.Fail($"{typeof(T).Name} declares no events for {state} (StateEvents): declare them, or override AbilityEvents.");
            return default;
        }

        private bool UnderWay => rig.InState(AbilityState);

        /// <summary>Runs the fixture's inputs until the ability is under way, or for SecondsToStart.</summary>
        private IEnumerator RunUntilUnderWay()
        {
            int limit = CharacterTestRig.StepsFor(SecondsToStart);
            while (!UnderWay && StepsRun < limit) { yield return RunInputs(1); }
        }

        /// <summary>Runs the fixture's inputs for SecondsToStart (and a little more), returning whether the ability ever got under way.</summary>
        private IEnumerator RunWatching(System.Action<bool> started, Instead instead = null)
        {
            bool was = false;
            yield return RunInputs(CharacterTestRig.StepsFor(SecondsToStart + 0.25f), _ =>
            {
                was |= UnderWay;
                instead?.EachStep?.Invoke();
            });
            started(was);
        }

        [UnityTest]
        public IEnumerator Kit_TheAbility_StartsFromItsInputs_RaisingItsStartEvent()
        {
            yield return SetUp();
            yield return RunUntilUnderWay();
            Assert.That(UnderWay, Is.True, $"Test setup: {typeof(T).Name} should start from the fixture's inputs within {SecondsToStart} s " +
                                           $"(at {rig.Position}): every other check of the kit relies on it.");
            Assert.That(rig.EventsRaised, Does.Contain(AbilityEvents.started), $"Starting, {AbilityState} should raise its start event.");
        }

        [UnityTest]
        public IEnumerator Kit_TurnedOffByItsToggle_ItDoesntStart()
        {
            yield return SetUp();
            Component.Enabled = false;
            Instead instead = WhatHappensInstead(byToggle: true);
            bool started = false;
            yield return RunWatching(s => started = s, instead);
            Assert.That(started, Is.False, $"With {typeof(T).Name} turned off, it shouldn't start.");
            Assert.That(rig.EventsRaised, Has.No.Member(AbilityEvents.started));
            instead?.Afterwards?.Invoke();
        }

        [UnityTest]
        public IEnumerator Kit_TurnedOffByItsZeroSetting_ItDoesntStart()
        {
            if (OffSetting == null) { Assert.Ignore($"{typeof(T).Name} has no setting whose 0 turns it off (OffSetting)."); }
            yield return SetUp();
            TurnOffBySetting();
            Instead instead = WhatHappensInstead(byToggle: false);
            bool started = false;
            yield return RunWatching(s => started = s, instead);
            Assert.That(started, Is.False, $"With {OffSetting} at 0, {typeof(T).Name} shouldn't start.");
            Assert.That(rig.EventsRaised, Has.No.Member(AbilityEvents.started));
            instead?.Afterwards?.Invoke();
        }

        [UnityTest]
        public IEnumerator Kit_TurnedOffWhileUnderWay_ItLetsGo()
        {
            yield return SetUp();
            yield return RunUntilUnderWay();
            Assume.That(UnderWay, Is.True, "Test setup: the ability should be under way.");
            Component.Enabled = false;
            // Within a step or two (the mode ends a component turned off as the next step starts: see
            // AbilityMovementMode.AdvanceComponentTimers), not when it would have ended by itself.
            yield return RunInputs(2);
            Assert.That(Component.IsActive(rig.Movement), Is.False, $"Turned off, {typeof(T).Name} should let go of the character at once.");
            Assert.That(UnderWay, Is.False, $"Turned off, the character shouldn't be {AbilityState} any more.");
            Assert.That(rig.EventsRaised, Does.Contain(AbilityEvents.ended), "Letting go should raise its end event.");
            // Another ability may take the character as it lets go (a ledge's wall slid down, as the character falls against
            // it), as it would in play: what matters is that this one let go.
        }

        [UnityTest]
        public IEnumerator Kit_ItsStatesEvents_ComeInPairs()
        {
            yield return SetUp();
            yield return RunUntilUnderWay();
            Assume.That(UnderWay, Is.True, "Test setup: the ability should be under way.");
            yield return RunInputs(CharacterTestRig.StepsFor(1f));
            Component.Enabled = false;
            yield return RunInputs(CharacterTestRig.StepsFor(0.1f));
            var pairs = new List<StateEventPair>(Component.StateEvents);
            if (!pairs.Exists(p => p.state == AbilityEvents.state)) { pairs.Add(AbilityEvents); }
            foreach (StateEventPair pair in pairs)
            {
                Assert.That(rig.Count(pair.ended), Is.EqualTo(rig.Count(pair.started)),
                    $"{pair.state} started {rig.Count(pair.started)} times and ended {rig.Count(pair.ended)}: each start should have its end.");
            }
            Assert.That(rig.Count(AbilityEvents.started), Is.GreaterThanOrEqualTo(1));
        }

        [UnityTest]
        public IEnumerator Kit_SavedPartwayAndPutBack_StepsOnExactlyAsItDid([Values(false, true)] bool later)
        {
            yield return SetUp();
            yield return RunUntilUnderWay();
            Assume.That(UnderWay, Is.True, "Test setup: the ability should be under way.");
            yield return RunInputs(later ? StepsBeforeSaving + StepsBeforeSavingLater : StepsBeforeSaving);
            MovementStateSnapshot saved = rig.Movement.SaveState();
            WorldStateSnapshot world = WorldStateSnapshot.Save();
            long levelTick = rig.LevelTick;
            int savedAt = StepsRun;

            var first = new List<(Vector3, Quaternion, Vector3)>();
            yield return RunInputs(StepsCompared, _ => first.Add(Pose()));
            Assert.That(rig.Movement.RestoreState(saved), Is.True, "The saved state should be put back.");
            world.Restore();
            rig.LevelTick = levelTick;
            RewindTo(savedAt);
            var again = new List<(Vector3, Quaternion, Vector3)>();
            yield return RunInputs(StepsCompared, _ => again.Add(Pose()));

            for (int i = 0; i < first.Count; i++)
            {
                Assert.That(again[i], Is.EqualTo(first[i]), $"Put back, step {i} should be exactly as it was (position, rotation, velocity).");
            }
        }


        [UnityTest]
        public IEnumerator Kit_AtAnotherSize_ItStillStarts([Values(0.5f, 2f)] float scale)
        {
            yield return SetUp(scale);
            yield return RunUntilUnderWay();
            Assert.That(UnderWay, Is.True, $"At {scale} times its size, {typeof(T).Name} should still start from the fixture's inputs " +
                                           "(its speeds and distances scale with the character: see docs/adding-an-ability.md).");
        }
    }
}
