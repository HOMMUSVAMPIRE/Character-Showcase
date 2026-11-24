using System.Collections;
using System.Collections.Generic;
using MeridianMotion.Characters;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MeridianMotion.Tests.InputAndDeterminism
{
    /// <summary>
    /// A replay in the game's own loop (MMotionCharacter stepping itself in FixedUpdate, with its model, animation and
    /// Unity's physics, as in play, not the rig's stepping): a run that sprints, slides and jumps, recorded and played back
    /// on the same build, moves exactly as it did, step for step, with keyframes or without. Its keyframes once threw it
    /// off course: read back into a character whose other step record was current, the two records became one, and each
    /// step after reset the step it started from (StateSerializer's reader; see DeterminismTests).
    /// </summary>
    [Category(TestArea.Movement)]
    public class ReplayGameLoopTests : CharacterRigFixture
    {
        protected override bool MovementOnly => false;

        private ReplayRecorder recorder;
        private ScriptedInputSource script;

        [TearDown]
        public void RemoveRecorder()
        {
            if (recorder != null) { Object.Destroy(recorder.gameObject); }
            if (script != null) { Object.Destroy(script.gameObject); }
            ReplayClock.Remove();
        }

        private static ScriptedInputSource.TimedStep Hold(float seconds, Vector2 move, float yaw, InputButtons held) =>
            new() { seconds = seconds, move = move, lookYaw = yaw, held = held };

        [UnityTest]
        public IEnumerator ARunInTheGameLoop_ReplaysExactly_OnTheSameBuild([Values(1000f, 0.5f)] float keyframeSeconds)
        {
            rig.Floor();
            yield return rig.SpawnSettled(new Vector3(0f, 0.05f, 0f));
            script = new GameObject("Script").AddComponent<ScriptedInputSource>();
            script.Steps.AddRange(new[]
            {
                Hold(0.6f, Vector2.up, 0f, InputButtons.None),
                Hold(0.8f, Vector2.up, 20f, InputButtons.Sprint),
                Hold(0.6f, Vector2.up, 40f, InputButtons.Sprint | InputButtons.Crouch),
                Hold(0.2f, Vector2.up, 40f, InputButtons.Sprint | InputButtons.Jump),
                Hold(0.8f, Vector2.up, 70f, InputButtons.Sprint),
                Hold(0.2f, Vector2.up, 90f, InputButtons.Jump),
                Hold(0.6f, new Vector2(0.5f, 0.8f), 110f, InputButtons.None),
            });
            recorder = new GameObject("Replay Recorder").AddComponent<ReplayRecorder>();
            recorder.KeyframeSeconds = keyframeSeconds; // 1000: only the start's, nothing to hide a difference.
            recorder.Attach(rig.Controller);

            var recorded = new List<MovementStateSummary>();
            var replayed = new List<MovementStateSummary>();
            var recordedSteps = new List<(CharacterState previous, MovementEvents events)>();
            var replayedSteps = new List<(CharacterState previous, MovementEvents events)>();
            List<MovementStateSummary> into = recorded;
            List<(CharacterState, MovementEvents)> stepsInto = recordedSteps;
            // Taken after the recorder's: the state each step starts from, a keyframe put back.
            rig.Controller.BeforeStep += (MMotionCharacter character, in CharacterInputFrame frame) =>
            {
                into.Add(character.Motor.Summarize());
                MovementStep last = character.Motor.lastStep;
                stepsInto.Add(last != null ? (last.previousState, last.events) : (CharacterState.None, MovementEvents.None));
            };

            rig.Controller.InputSource = script;
            rig.Controller.enabled = true;
            yield return Waits.WaitUntil(() => script.Finished, 20f);
            int steps = recorder.RecordedFrames;
            Replay run = recorder.CurrentRun();
            Assert.That(steps, Is.GreaterThan(100), "Test setup: the run should be recorded.");

            into = replayed;
            stepsInto = replayedSteps;
            Assert.That(recorder.Play(run), Is.True);
            yield return Waits.WaitUntil(() => recorder.PlaybackComplete, 20f);
            rig.Controller.enabled = false;

            int first = -1;
            for (int i = 0; i < Mathf.Min(steps, replayed.Count); i++)
            {
                MovementStateSummary a = recorded[i], b = replayed[i];
                if (a.position != b.position || a.rotation != b.rotation || a.velocity != b.velocity || a.state != b.state || !a.input.Equals(b.input)
                    || !recordedSteps[i].Equals(replayedSteps[i])) { first = i; break; }
            }
            string DescribeStep(int i, List<(CharacterState previous, MovementEvents events)> steps) => $", its last step's previous state {steps[i].previous}, events {steps[i].events}";
            string Describe(MovementStateSummary s) => $"tick {s.tick}, at {s.position.ToString("F5")}, velocity {s.velocity.ToString("F5")}, state {s.state}, input move {s.input.move} yaw {s.input.lookYaw} held {s.input.held}";
            Assert.That(first, Is.EqualTo(-1), first < 0 ? "" :
                $"The replay first differs before step {first} of {steps}:\n  recorded: {Describe(recorded[first])}{DescribeStep(first, recordedSteps)}\n  replayed: {Describe(replayed[first])}{DescribeStep(first, replayedSteps)}" +
                (first > 0 ? $"\n  a step before, recorded: {Describe(recorded[first - 1])}\n  replayed: {Describe(replayed[first - 1])}" : ""));
        }
    }
}
