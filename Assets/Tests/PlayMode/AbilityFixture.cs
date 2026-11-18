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
    /// A fixture of tests on one ability (the movement component T), which gets the checks every ability should pass from
    /// deriving this, as its own tests (see docs/adding-an-ability.md, Tests and docs). The checks:
    /// - it starts from its inputs, raising its start event (the fixture's own set-up, checked);
    /// - turned off by its toggle, it doesn't start;
    /// - turned off by its 0 setting (OffSetting), it doesn't start;
    /// - turned off while it's under way, it lets go, raising its end event, and nothing has the character any more;
    /// - its states' start and end events come in pairs;
    /// - saved partway through and put back, the character steps on exactly as it did (with the level's moving parts);
    /// - at another size, it still starts.
    /// A fixture supplies how to set the ability up (SetUpToStart, spawning the character with SpawnCharacter, so it's at
    /// the size a check asks for), the inputs that start it and keep it going (InputsFor), and the setting whose 0 turns it
    /// off (OffSetting); then adds the tests that are about the ability itself. It still needs its area's
    /// [Category(TestArea.…)] of its own: the hygiene check fails without it. [TestTiming] comes with CharacterRigFixture.
    /// </summary>
    public abstract class AbilityFixture<T> : CharacterRigFixture where T : MovementComponent
    {
        /// <summary>
        /// Builds the world the ability needs and spawns the character (with SpawnCharacter) where the inputs (InputsFor)
        /// start it within SecondsToStart. Called once a check has set the size it wants.
        /// </summary>
        protected abstract IEnumerator SetUpToStart();

        /// <summary>The inputs for each step (counted from the end of SetUpToStart) that start the ability and keep it going.</summary>
        protected abstract Inputs InputsFor(int step);

        /// <summary>
        /// The ability's setting a value of 0 turns it off (a duration, a reach: the convention for every ability), as
        /// rig.SetMovementSetting names it; null for an ability without one (its check is then ignored, saying so).
        /// </summary>
        protected abstract string OffSetting { get; }

        /// <summary>
        /// The state the ability is under way in: by default, the first state its component declares (its StateEvents).
        /// </summary>
        protected virtual CharacterState AbilityState => FirstStateEvent.state;

        /// <summary>
        /// The ability's state and the events it raises starting and ending: by default, those its component declares for
        /// AbilityState (its StateEvents). A component that raises its own (Dashing does) overrides this.
        /// </summary>
        protected virtual StateEvent AbilityEvents => EventsOf(AbilityState);

        /// <summary>How long, at most, the inputs take to start the ability once it's set up.</summary>
        protected virtual float SecondsToStart => 1f;

        /// <summary>How many steps into the ability the save-and-put-back check saves the character.</summary>
        protected virtual int StepsBeforeSaving => 3;

        /// <summary>How many steps the save-and-put-back check compares, from where it saved.</summary>
        protected virtual int StepsCompared => 30;

        /// <summary>The size (a uniform scale) the character is spawned at: 1 unless a check asks for another.</summary>
        protected float Scale { get; private set; } = 1f;

        /// <summary>The steps run with InputsFor since the set-up (so a script carries on where it was, put back or not).</summary>
        protected int StepsRun { get; private set; }

        /// <summary>
        /// For SetUpToStart: spawns the character at position, at the check's size (Scale), then lets it stand for
        /// settleSeconds (none: it stays where it's put, in mid-air say, until the check runs it).
        /// </summary>
        protected IEnumerator SpawnCharacter(Vector3 position, float settleSeconds = 0.3f)
        {
            yield return rig.SpawnSettled(position, settleSeconds: 0f);
            if (!Mathf.Approximately(Scale, 1f)) { Assert.That(rig.Controller.SetScale(Vector3.one * Scale, immediately: true), Is.True, "Test setup: the character should resize."); }
            if (settleSeconds > 0f) { yield return rig.Run(settleSeconds, _ => CharacterTestRig.Idle()); }
        }

        private T Component => rig.Component<T>();

        private StateEvent FirstStateEvent
        {
            get
            {
                StateEvent[] events = Component.StateEvents;
                Assert.That(events, Is.Not.Empty, $"{typeof(T).Name} declares no states (StateEvents): give it one, or override AbilityState and AbilityEvents.");
                return events[0];
            }
        }

        private StateEvent EventsOf(CharacterState state)
        {
            foreach (StateEvent pair in Component.StateEvents)
            {
                if (pair.state == state) { return pair; }
            }
            Assert.Fail($"{typeof(T).Name} declares no events for {state} (StateEvents): declare them, or override AbilityEvents.");
            return default;
        }

        private bool UnderWay => rig.InState(AbilityState);

        /// <summary>Sets up (at the given size), then clears the events so a check counts only its own.</summary>
        private IEnumerator SetUp(float scale = 1f)
        {
            Scale = scale;
            StepsRun = 0;
            yield return SetUpToStart();
            rig.ClearEvents();
        }

        /// <summary>Runs the fixture's inputs for the given steps, carrying on its script.</summary>
        private IEnumerator RunInputs(int steps, System.Action<int> afterStep = null)
        {
            int from = StepsRun;
            StepsRun += steps;
            yield return rig.Run(steps, step => InputsFor(from + step), afterStep);
        }

        /// <summary>Runs the fixture's inputs until the ability is under way, or for SecondsToStart.</summary>
        private IEnumerator RunUntilUnderWay()
        {
            int limit = CharacterTestRig.StepsFor(SecondsToStart);
            while (!UnderWay && StepsRun < limit) { yield return RunInputs(1); }
        }

        /// <summary>Runs the fixture's inputs for SecondsToStart (and a little more), returning whether the ability ever got under way.</summary>
        private IEnumerator RunWatching(System.Action<bool> started)
        {
            bool was = false;
            yield return RunInputs(CharacterTestRig.StepsFor(SecondsToStart + 0.25f), _ => was |= UnderWay);
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
            bool started = false;
            yield return RunWatching(s => started = s);
            Assert.That(started, Is.False, $"With {typeof(T).Name} turned off, it shouldn't start.");
            Assert.That(rig.EventsRaised, Has.No.Member(AbilityEvents.started));
        }

        [UnityTest]
        public IEnumerator Kit_TurnedOffByItsZeroSetting_ItDoesntStart()
        {
            if (OffSetting == null) { Assert.Ignore($"{typeof(T).Name} has no setting whose 0 turns it off (OffSetting)."); }
            yield return SetUp();
            TurnOffBySetting();
            bool started = false;
            yield return RunWatching(s => started = s);
            Assert.That(started, Is.False, $"With {OffSetting} at 0, {typeof(T).Name} shouldn't start.");
            Assert.That(rig.EventsRaised, Has.No.Member(AbilityEvents.started));
        }

        /// <summary>Sets OffSetting to 0 (a float setting: a fixture whose setting is of another type overrides this).</summary>
        protected virtual void TurnOffBySetting() => rig.SetMovementSetting(OffSetting, 0f);

        [UnityTest]
        public IEnumerator Kit_TurnedOffWhileUnderWay_ItLetsGo()
        {
            yield return SetUp();
            yield return RunUntilUnderWay();
            Assume.That(UnderWay, Is.True, "Test setup: the ability should be under way.");
            Component.Enabled = false;
            // Within a step or two (the mode ends a component turned off as the next step starts: see
            // ComposedMovementMode.AdvanceComponentTimers), not when it would have ended by itself.
            yield return RunInputs(2);
            Assert.That(Component.IsActive(rig.Movement), Is.False, $"Turned off, {typeof(T).Name} should let go of the character at once.");
            Assert.That(UnderWay, Is.False, $"Turned off, the character shouldn't be {AbilityState} any more.");
            Assert.That(rig.EventsRaised, Does.Contain(AbilityEvents.ended), "Letting go should raise its end event.");
            Assert.That(rig.ComposedMode.AbilityInProgress(rig.Movement), Is.False, "Nothing should have the character any more.");
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
            var pairs = new List<StateEvent>(Component.StateEvents);
            if (!pairs.Exists(p => p.state == AbilityEvents.state)) { pairs.Add(AbilityEvents); }
            foreach (StateEvent pair in pairs)
            {
                Assert.That(rig.Count(pair.ended), Is.EqualTo(rig.Count(pair.started)),
                    $"{pair.state} started {rig.Count(pair.started)} times and ended {rig.Count(pair.ended)}: each start should have its end.");
            }
            Assert.That(rig.Count(AbilityEvents.started), Is.GreaterThanOrEqualTo(1));
        }

        [UnityTest]
        public IEnumerator Kit_SavedPartwayAndPutBack_StepsOnExactlyAsItDid()
        {
            yield return SetUp();
            yield return RunUntilUnderWay();
            Assume.That(UnderWay, Is.True, "Test setup: the ability should be under way.");
            yield return RunInputs(StepsBeforeSaving);
            MovementStateSnapshot saved = rig.Movement.SaveState();
            WorldStateSnapshot world = WorldStateSnapshot.Save();
            long levelTick = rig.LevelTick;
            int savedAt = StepsRun;

            var first = new List<(Vector3, Quaternion, Vector3)>();
            yield return RunInputs(StepsCompared, _ => first.Add(Pose()));
            Assert.That(rig.Movement.RestoreState(saved), Is.True, "The saved state should be put back.");
            world.Restore();
            rig.LevelTick = levelTick;
            StepsRun = savedAt;
            var again = new List<(Vector3, Quaternion, Vector3)>();
            yield return RunInputs(StepsCompared, _ => again.Add(Pose()));

            for (int i = 0; i < first.Count; i++)
            {
                Assert.That(again[i], Is.EqualTo(first[i]), $"Put back, step {i} should be exactly as it was (position, rotation, velocity).");
            }
        }

        private (Vector3, Quaternion, Vector3) Pose() => (rig.Position, rig.Body.rotation, rig.Velocity);

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
