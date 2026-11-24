using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using MeridianMotion.Characters;
using MeridianMotion.Levels;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace MeridianMotion.Tests.InputAndDeterminism
{
    /// <summary>
    /// Tests of replays (Replay, ReplayRecorder, ReplayClock): a run recorded from its first step (frames and keyframes),
    /// written to a file and read back the same; played back, the character put back where the run began and moving
    /// exactly as it did, step for step, then standing idle and saying it's finished; a run knocked off course put back at
    /// the next keyframe; stopping a replay giving the character back and recording from there; the newest replay of a
    /// scene found; and the level's clock offset to a replay's ticks and put back.
    /// </summary>
    [Category(TestArea.Movement)]
    public class ReplayTests : CharacterRigFixture
    {
        private const int Steps = 160;
        private const float KeyframeSeconds = 0.5f;
        private ReplayRecorder recorder;

        [TearDown]
        public void RemoveRecorder()
        {
            if (recorder != null) { Object.Destroy(recorder.gameObject); }
        }

        private IEnumerator BuildAndSpawn()
        {
            rig.Floor();
            rig.Box("Block", new Vector3(0f, 0.25f, 6f), new Vector3(4f, 0.5f, 2f));
            yield return rig.SpawnSettled(new Vector3(0f, 0.05f, 0f));
            recorder = new GameObject("Replay Recorder").AddComponent<ReplayRecorder>();
            recorder.KeyframeSeconds = KeyframeSeconds;
            recorder.Attach(rig.Controller);
        }

        // Walks, jumps onto the block, sprints and dashes, turning as it goes.
        private static StepInputs Script(int step)
        {
            Vector3 heading = Quaternion.Euler(0f, step * 0.5f, 0f) * Vector3.forward;
            if (step >= 20 && step < 30) { return CharacterTestRig.Jump(heading); }
            if (step >= 40 && step < 80) { return CharacterTestRig.Sprint(heading); }
            if (step == 90) { return CharacterTestRig.Dash(heading); }
            return CharacterTestRig.Move(heading);
        }

        private (Vector3 position, Quaternion rotation, Vector3 velocity) Pose() =>
            (rig.Position, rig.Body.rotation, rig.Movement.lastStep.finalVelocity);

        private IEnumerator Record(List<(Vector3, Quaternion, Vector3)> poses)
        {
            yield return rig.Run(Steps, Script, _ => poses.Add(Pose()));
        }

        [UnityTest]
        public IEnumerator ARecordedRun_ReplaysExactly_FromWhereItBegan_ThenSaysItsFinished()
        {
            yield return BuildAndSpawn();
            Vector3 start = rig.Position;
            var first = new List<(Vector3, Quaternion, Vector3)>();
            yield return Record(first);
            Assert.That(recorder.RecordedFrames, Is.EqualTo(Steps), "Every step should be recorded.");
            Replay run = recorder.CurrentRun();
            Assert.That(run.Keyframes.Count, Is.EqualTo(Mathf.CeilToInt(Steps / Mathf.Round(KeyframeSeconds / Time.fixedDeltaTime))),
                "A keyframe at the start, and one every Keyframe Seconds.");

            // Through a file and back.
            var written = new MemoryStream();
            run.Write(written);
            Replay read = Replay.Read(new MemoryStream(written.ToArray()));
            var again = new MemoryStream();
            read.Write(again);
            Assert.That(again.ToArray(), Is.EqualTo(written.ToArray()), "A replay read back should write the same bytes.");

            bool finished = false;
            recorder.PlaybackFinished += _ => finished = true;
            Assert.That(recorder.Play(read), Is.True);
            Assert.That(Vector3.Distance(rig.Position, start), Is.LessThan(0.001f), "Played, the character's put back where the run began.");
            Assert.That(recorder.IsRecording, Is.False, "It doesn't record its own replay.");
            int step = 0, differed = -1;
            yield return rig.RunWithSource(Steps, recorder.PlaybackSource, _ =>
            {
                if (differed < 0 && !Pose().Equals(first[step])) { differed = step; }
                step++;
            });
            Assert.That(differed, Is.EqualTo(-1), $"The replay should move exactly as the run did (it first differed at step {differed}).");
            Assert.That(recorder.PlaybackComplete, Is.False);
            yield return rig.RunWithSource(1, recorder.PlaybackSource);
            yield return null;
            Assert.That((recorder.PlaybackComplete, finished), Is.EqualTo((true, true)), "Past its last frame, it's finished, and says so.");
        }

        [UnityTest]
        public IEnumerator AReplayKnockedOffCourse_IsPutBackAtTheNextKeyframe()
        {
            yield return BuildAndSpawn();
            var first = new List<(Vector3, Quaternion, Vector3)>();
            yield return Record(first);
            Replay run = recorder.CurrentRun();
            int every = Mathf.RoundToInt(KeyframeSeconds / Time.fixedDeltaTime);

            recorder.Play(run);
            int step = 0;
            Vector3 knockedAt = default;
            yield return rig.RunWithSource(every * 2 + 1, recorder.PlaybackSource, _ =>
            {
                if (step == every + 2)
                {
                    rig.Body.position += new Vector3(1.5f, 0f, 0f);
                    knockedAt = rig.Body.position;
                }
                step++;
            });
            Assert.That(knockedAt, Is.Not.EqualTo(default(Vector3)), "Test setup: it was knocked aside.");
            Assert.That(Pose(), Is.EqualTo(first[every * 2]), "At the next keyframe it's back on the run's path, exactly.");
        }

        [UnityTest]
        public IEnumerator StoppingAReplay_GivesTheCharacterBack_AndRecordsFromThere()
        {
            yield return BuildAndSpawn();
            yield return Record(new List<(Vector3, Quaternion, Vector3)>());
            recorder.Play(recorder.CurrentRun());
            Assert.That(rig.Controller.InputSource, Is.SameAs(recorder.PlaybackSource), "Playing, the replay drives the character.");
            yield return rig.RunWithSource(20, recorder.PlaybackSource);
            recorder.StopPlayback();
            Assert.That(recorder.IsPlaying, Is.False);
            Assert.That(rig.Controller.InputSource, Is.Null, "Stopped, the character's its owner's (the player's) again.");
            Assert.That(recorder.IsRecording, Is.True, "...and a fresh recording starts...");
            yield return rig.Run(10, step => CharacterTestRig.Move(Vector3.forward));
            Assert.That(recorder.RecordedFrames, Is.EqualTo(10), "...from where it was stopped.");
        }

        [Test]
        public void TheNewestReplayOfAScene_IsFound()
        {
            string folder = Path.Combine(Application.temporaryCachePath, "Replay Tests " + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                string older = Path.Combine(folder, "Basin 2026-01-01 10-00-00" + Replay.FileExtension);
                string newer = Path.Combine(folder, "Basin 2026-01-02 10-00-00" + Replay.FileExtension);
                string other = Path.Combine(folder, "Complex 2026-01-03 10-00-00" + Replay.FileExtension);
                foreach (string path in new[] { older, newer, other }) { File.WriteAllText(path, ""); }
                File.SetLastWriteTimeUtc(older, new DateTime(2026, 1, 1));
                File.SetLastWriteTimeUtc(newer, new DateTime(2026, 1, 2));
                File.SetLastWriteTimeUtc(other, new DateTime(2026, 1, 3));
                Assert.That(Replay.LatestFor("Basin", folder), Is.EqualTo(newer));
                Assert.That(Replay.LatestFor("Nowhere", folder), Is.Null);
            }
            finally { Directory.Delete(folder, true); }
        }

        [Test]
        public void TheReplayClock_HoldsTheTickUntilTheStep_ThenGoesOnFromIt_AndIsPutBack()
        {
            Func<long> before = LevelClock.TickSource;
            long source = 1000;
            LevelClock.TickSource = () => source;
            try
            {
                ReplayClock.Install(40);
                Assert.That(LevelClock.Tick, Is.EqualTo(40), "Until its step, the replay's tick.");
                source = 1003;
                ReplayClock.SyncTo(40);
                source = 1005;
                Assert.That(LevelClock.Tick, Is.EqualTo(42), "Then on from it, a tick a step.");
                ReplayClock.Remove();
                Assert.That(LevelClock.Tick, Is.EqualTo(1005), "Removed, the clock there was before.");
            }
            finally
            {
                ReplayClock.Remove();
                LevelClock.TickSource = before;
            }
        }
    }
}
