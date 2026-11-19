using System.Collections;
using MeridianMotion.Characters;
using NUnit.Framework;
using UnityEngine;

namespace MeridianMotion.Tests
{
    /// <summary>
    /// What the ability test kits share (AbilityFixture, for an ability that's under way for a while; MomentFixture, for
    /// one that happens in a moment): the fixture's set-up (SetUpToStart, at the size a check asks for: SpawnCharacter),
    /// its inputs (InputsFor, run as a script that carries on where it was), and the setting whose 0 turns its ability
    /// off. See docs/adding-an-ability.md, Tests and docs.
    /// </summary>
    public abstract class AbilityKitBase<T> : CharacterRigFixture where T : MovementComponent
    {
        /// <summary>
        /// Builds the world the ability needs and spawns the character (with SpawnCharacter) where the inputs (InputsFor)
        /// start it within SecondsToStart. Called once a check has set the size it wants.
        /// </summary>
        protected abstract IEnumerator SetUpToStart();

        /// <summary>The inputs for each step (counted from the end of SetUpToStart) that start the ability and keep it going.</summary>
        protected abstract StepInputs InputsFor(int step);

        /// <summary>
        /// The ability's setting a value of 0 turns it off (a duration, a reach: the convention for every ability), as
        /// rig.SetMovementSetting names it; null for an ability without one (its check is then ignored, saying so).
        /// </summary>
        protected abstract string OffSetting { get; }

        /// <summary>How long, at most, the inputs take to start the ability once it's set up.</summary>
        protected virtual float SecondsToStart => 1f;

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

        /// <summary>The ability's component (the rig's own copy of the mode's).</summary>
        protected T Component => rig.Component<T>();

        /// <summary>Sets up (at the given size), then clears the events so a check counts only its own.</summary>
        protected IEnumerator SetUp(float scale = 1f)
        {
            Scale = scale;
            StepsRun = 0;
            yield return SetUpToStart();
            rig.ClearEvents();
        }

        /// <summary>Runs the fixture's inputs for the given steps, carrying on its script.</summary>
        protected IEnumerator RunInputs(int steps, System.Action<int> afterStep = null)
        {
            int from = StepsRun;
            StepsRun += steps;
            yield return rig.Run(steps, step => InputsFor(from + step), afterStep);
        }

        /// <summary>Puts the script back to the given step (with the character's state put back there too).</summary>
        protected void RewindTo(int step) => StepsRun = step;

        /// <summary>Sets OffSetting to 0 (a float setting: a fixture whose setting is of another type overrides this).</summary>
        protected virtual void TurnOffBySetting() => rig.SetMovementSetting(OffSetting, 0f);

        /// <summary>The character's pose and velocity, for comparing two runs step by step.</summary>
        protected (Vector3, Quaternion, Vector3) Pose() => (rig.Position, rig.Body.rotation, rig.Velocity);
    }
}
