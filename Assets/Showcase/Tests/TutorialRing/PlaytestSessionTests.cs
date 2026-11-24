using System.Collections;
using System.IO;
using MeridianMotion.Levels;
using MeridianMotion.Showcase.TutorialRing;
using MeridianMotion.Tests;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MeridianMotion.Showcase.Tests.TutorialRing
{
    /// <summary>
    /// Tests of the playtest session log (PlaytestSessionLog), on the rig's character: a lesson entered and done is logged
    /// with when and how long; respawns are counted toward the lesson last in, and not once the player's been on the
    /// course; a course run is logged with its time,
    /// splits and respawns; the results are written beside the replay; and ending twice writes once.
    /// </summary>
    [Category(TestArea.Levels)]
    public class PlaytestSessionTests : CharacterRigFixture
    {
        private TutorialLessonCatalog catalog;
        private TutorialLessonTracker tracker;
        private PlaytestSessionLog log;
        private string folder;

        private float StepLength => LevelClock.StepLength;

        private IEnumerator SetUpSession()
        {
            folder = Path.Combine(Application.temporaryCachePath, "PlaytestSessionTests");
            if (Directory.Exists(folder)) { Directory.Delete(folder, true); }
            rig.Floor();
            rig.Checkpoint("start", Vector3.zero, new Vector3(4f, 3f, 2f), isStart: true);
            yield return rig.SpawnSettled(new Vector3(0f, 0.05f, 0f));
            catalog = TutorialTestLessons.Catalog();
            tracker = rig.Character.AddComponent<TutorialLessonTracker>();
            tracker.Catalog = catalog;
            tracker.Follow(rig.Movement);
            log = rig.Character.AddComponent<PlaytestSessionLog>();
            log.Begin(tracker, null);
        }

        [TearDown]
        public void TearDown()
        {
            if (catalog != null) { Object.Destroy(catalog); }
            if (folder != null && Directory.Exists(folder)) { Directory.Delete(folder, true); }
        }

        [UnityTest]
        public IEnumerator ALessonEnteredAndDone_IsLoggedWithWhenAndHowLong()
        {
            yield return SetUpSession();
            rig.LevelTick += 50;
            tracker.Enter("jump");
            rig.LevelTick += 100;
            tracker.Heard(MeridianMotion.Characters.MovementEvents.Jumped, default);
            Assert.That(log.Results.lessons.Count, Is.EqualTo(1));
            LessonResult jump = log.Results.lessons[0];
            Assert.That(jump.id, Is.EqualTo("jump"));
            Assert.That(jump.firstEntered, Is.EqualTo(50 * StepLength).Within(1e-4f));
            Assert.That(jump.completedAfter, Is.EqualTo(100 * StepLength).Within(1e-4f));
        }

        [UnityTest]
        public IEnumerator RespawnsInALesson_AreCountedThere()
        {
            yield return SetUpSession();
            tracker.Enter("jump");
            tracker.Leave("jump");
            rig.Movement.levelPresence.RespawnAtCheckpoint(rig.Movement);
            rig.Movement.levelPresence.RespawnAtCheckpoint(rig.Movement);
            yield return rig.Run(2, _ => CharacterTestRig.Idle());
            Assert.That(log.Results.respawns, Is.EqualTo(2));
            Assert.That(log.Results.lessons[0].respawns, Is.EqualTo(2), "Respawns count toward the lesson the player was last in.");
        }

        [UnityTest]
        public IEnumerator RespawnsOnTheCourse_ArentTheLastLessons()
        {
            var timerObject = rig.Track(new GameObject("Course Timer"));
            var timer = timerObject.AddComponent<CourseTimer>();
            yield return SetUpSession();
            tracker.Enter("jump");
            tracker.Leave("jump");
            rig.LevelTick += 10;
            yield return rig.Run(2, _ => CharacterTestRig.Idle());
            timer.RegisterGatePass(TimingGateRole.Start, null);
            yield return rig.Run(2, _ => CharacterTestRig.Idle());
            rig.Movement.levelPresence.RespawnAtCheckpoint(rig.Movement);
            timer.RegisterGatePass(TimingGateRole.Finish, null);
            rig.Movement.levelPresence.RespawnAtCheckpoint(rig.Movement);
            yield return rig.Run(2, _ => CharacterTestRig.Idle());
            Assert.That(log.Results.respawns, Is.EqualTo(2));
            Assert.That(log.Results.lessons[0].respawns, Is.EqualTo(0), "Respawns on the course, or after it, aren't the last lesson's.");
        }

        [UnityTest]
        public IEnumerator ACourseRun_IsLoggedWithItsSplitsAndRespawns()
        {
            var timerObject = rig.Track(new GameObject("Course Timer"));
            var timer = timerObject.AddComponent<CourseTimer>();
            timer.CourseName = "Agility";
            yield return SetUpSession();
            yield return rig.Run(2, _ => CharacterTestRig.Idle());
            timer.RegisterGatePass(TimingGateRole.Start, null);
            yield return rig.Run(2, _ => CharacterTestRig.Idle());
            rig.LevelTick += 100;
            timer.RegisterSplit(0, null);
            rig.Movement.levelPresence.RespawnAtCheckpoint(rig.Movement);
            rig.LevelTick += 100;
            timer.RegisterGatePass(TimingGateRole.Finish, null);

            Assert.That(log.Results.courseRuns.Count, Is.EqualTo(1));
            CourseRunResult run = log.Results.courseRuns[0];
            Assert.That(run.course, Is.EqualTo("Agility"));
            Assert.That(run.splits.Length, Is.EqualTo(1));
            Assert.That(run.time, Is.GreaterThan(run.splits[0]));
            Assert.That(run.respawns, Is.EqualTo(1), "The respawn during the run should be counted with it.");
        }

        [UnityTest]
        public IEnumerator Write_PutsTheResultsBesideTheReplay()
        {
            yield return SetUpSession();
            tracker.Enter("move");
            string replay = Path.Combine(folder, "Tutorial Ring 2026-11-24 10-00-00.mmreplay");
            string path = log.Write(replay);
            Assert.That(path, Is.EqualTo(Path.Combine(folder, "Tutorial Ring 2026-11-24 10-00-00" + PlaytestSessionLog.ResultsSuffix)));
            Assert.That(File.Exists(path), Is.True);
            var read = JsonUtility.FromJson<SessionResults>(File.ReadAllText(path));
            Assert.That(read.replay, Is.EqualTo(Path.GetFileName(replay)));
            Assert.That(read.lessons[0].id, Is.EqualTo("move"));
        }

        [UnityTest]
        public IEnumerator End_Twice_WritesOnce()
        {
            yield return SetUpSession();
            string first = log.End();
            Assert.That(File.Exists(first), Is.True, "Ending should write the results.");
            // With no recorder, they're written where the scene's replays go: taken away again, to see End doesn't write twice.
            File.Delete(first);
            string second = log.End();
            Assert.That(second, Is.EqualTo(first));
            Assert.That(File.Exists(second), Is.False, "Ending again shouldn't write again.");
        }
    }
}
