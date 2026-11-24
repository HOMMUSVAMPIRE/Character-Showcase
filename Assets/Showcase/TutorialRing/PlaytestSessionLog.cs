using System;
using System.Collections.Generic;
using System.IO;
using MeridianMotion.Characters;
using MeridianMotion.Levels;
using UnityEngine;

namespace MeridianMotion.Showcase.TutorialRing
{
    /// <summary>A lesson's part of a play session: when it was first entered, how long it took to do, and the respawns in it.</summary>
    [Serializable]
    public class LessonResult
    {
        public string id;
        /// <summary>Seconds into the session it was first entered.</summary>
        public float firstEntered;
        /// <summary>Seconds from first entering it to doing it (-1: not done).</summary>
        public float completedAfter = -1f;
        /// <summary>How many times the player was respawned while it was the lesson they were last in.</summary>
        public int respawns;
    }

    /// <summary>A course run of a play session: its time, its splits and the respawns during it.</summary>
    [Serializable]
    public class CourseRunResult
    {
        public string course;
        public float time;
        public float[] splits;
        public int respawns;
        /// <summary>Seconds into the session it finished.</summary>
        public float finishedAt;
    }

    /// <summary>What a play session of the Tutorial Ring leaves behind, beside its replay: written as JSON.</summary>
    [Serializable]
    public class SessionResults
    {
        /// <summary>The replay it goes with (its file name).</summary>
        public string replay;
        /// <summary>The scene played.</summary>
        public string scene;
        /// <summary>How long the session had run (s) when it was written.</summary>
        public float seconds;
        /// <summary>Every respawn, in any lesson or none.</summary>
        public int respawns;
        public List<LessonResult> lessons = new();
        public List<CourseRunResult> courseRuns = new();
    }

    /// <summary>
    /// Logs a play session of the Tutorial Ring for afterwards, on the player's character (added by TutorialRingSetup):
    /// the time to each lesson and the respawns in it (from its TutorialLessonTracker, and Checkpoint.Respawned), and the
    /// time, splits and respawns of every course run (each CourseTimer's RunFinished). Times are the level's clock
    /// (LevelClock), in seconds from the session's start. Whenever the scene's ReplayRecorder saves the session's replay
    /// (the pause menu's Save Replay, its Quit in a build, End), the results are written beside it:
    /// "[replay without its extension].results.json". A respawn counts toward the lesson the player was last in, unless
    /// they've been on the course since (a run under way, or started after it). What happens while a replay plays isn't
    /// logged.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Meridian Motion/Showcase/Playtest Session Log")]
    public class PlaytestSessionLog : MonoBehaviour
    {
        /// <summary>What's added to a replay's name (less its extension) for its results file.</summary>
        public const string ResultsSuffix = ".results.json";

        private readonly SessionResults results = new();
        private readonly Dictionary<string, LessonResult> byLesson = new();
        private readonly List<(CourseTimer timer, Action<CourseRun> listener)> timers = new();
        private TutorialLessonTracker tracker;
        private ReplayRecorder recorder;
        private IRespawnable character;
        private long startTick;
        private int respawnsAtRunStart;
        // When (session seconds) a lesson zone was last entered, and a course run last started: a respawn after a run
        // started, with no lesson entered since, is the course's.
        private float lessonEnteredAt = float.NegativeInfinity, runStartedAt = float.NegativeInfinity;
        private bool runUnderWay;
        private string endedPath;

        /// <summary>The session's results so far.</summary>
        public SessionResults Results => results;
        /// <summary>The results file last written (null before one is).</summary>
        public string LastWrittenPath { get; private set; }

        private float Now => (float)LevelClock.SecondsAt(LevelClock.Tick - startTick);

        /// <summary>
        /// Starts logging: the lessons of the tracker, the respawns of its character, the runs of every course timer in the
        /// scene, written whenever the recorder saves (none: only End writes, beside no replay).
        /// </summary>
        public void Begin(TutorialLessonTracker lessons, ReplayRecorder replays)
        {
            Stop();
            startTick = LevelClock.Tick;
            tracker = lessons;
            recorder = replays;
            character = tracker != null ? tracker.Character : null;
            results.scene = gameObject.scene.name;
            if (tracker != null)
            {
                tracker.Entered += OnEntered;
                tracker.Completed += OnCompleted;
            }
            if (recorder != null) { recorder.Saved += OnSaved; }
            Checkpoint.Respawned += OnRespawned;
            foreach (CourseTimer timer in FindObjectsByType<CourseTimer>(FindObjectsInactive.Include)) { Listen(timer); }
        }

        /// <summary>Logs a course timer's runs too (Begin listens to those in the scene).</summary>
        public void Listen(CourseTimer timer)
        {
            if (timer == null || timers.Exists(t => t.timer == timer)) { return; }
            Action<CourseRun> listener = run => OnRunFinished(timer, run);
            timer.RunFinished += listener;
            timers.Add((timer, listener));
        }

        private void Stop()
        {
            if (tracker != null)
            {
                tracker.Entered -= OnEntered;
                tracker.Completed -= OnCompleted;
            }
            if (recorder != null) { recorder.Saved -= OnSaved; }
            Checkpoint.Respawned -= OnRespawned;
            foreach ((CourseTimer timer, Action<CourseRun> listener) in timers) { if (timer != null) { timer.RunFinished -= listener; } }
            timers.Clear();
        }

        private bool Replaying => recorder != null && recorder.IsPlaying;

        private void OnDestroy() => Stop();

        private LessonResult ResultFor(TutorialLesson lesson)
        {
            if (byLesson.TryGetValue(lesson.id, out LessonResult found)) { return found; }
            var result = new LessonResult { id = lesson.id, firstEntered = Now };
            byLesson[lesson.id] = result;
            results.lessons.Add(result);
            return result;
        }

        private void OnEntered(TutorialLesson lesson)
        {
            ResultFor(lesson);
            lessonEnteredAt = Now;
        }

        private void OnCompleted(TutorialLesson lesson)
        {
            LessonResult result = ResultFor(lesson);
            if (result.completedAfter < 0f) { result.completedAfter = Now - result.firstEntered; }
        }

        private void OnRespawned(IRespawnable who, Checkpoint point)
        {
            if ((character != null && !ReferenceEquals(who, character)) || Replaying) { return; }
            results.respawns++;
            bool onTheCourse = runUnderWay || runStartedAt > lessonEnteredAt;
            if (!onTheCourse && tracker != null && tracker.Last != null) { ResultFor(tracker.Last).respawns++; }
        }

        private void OnRunFinished(CourseTimer timer, CourseRun run)
        {
            if (Replaying) { return; }
            results.courseRuns.Add(new CourseRunResult
            {
                course = timer != null ? timer.CourseName : "",
                time = run.Time,
                splits = run.Splits,
                respawns = results.respawns - respawnsAtRunStart,
                finishedAt = Now,
            });
            respawnsAtRunStart = results.respawns;
        }

        private void Update()
        {
            // Respawns count toward a run from its start: noted while no run's under way.
            bool running = false;
            foreach ((CourseTimer timer, _) in timers) { running |= timer != null && timer.IsRunning; }
            if (running && !runUnderWay) { runStartedAt = Now; }
            runUnderWay = running;
            if (!running) { respawnsAtRunStart = results.respawns; }
        }

        private void OnSaved(string replayPath) => Write(replayPath);

        /// <summary>
        /// Writes the results beside a replay (path: the replay's file), returning the results file's path. Called when
        /// the recorder saves; call it with any path to write them yourself.
        /// </summary>
        public string Write(string replayPath)
        {
            if (string.IsNullOrEmpty(replayPath)) { return null; }
            results.replay = Path.GetFileName(replayPath);
            results.seconds = Now;
            string folder = Path.GetDirectoryName(replayPath) ?? "";
            if (folder.Length > 0) { Directory.CreateDirectory(folder); }
            string path = Path.Combine(folder, Path.GetFileNameWithoutExtension(replayPath) + ResultsSuffix);
            File.WriteAllText(path, JsonUtility.ToJson(results, prettyPrint: true));
            LastWrittenPath = path;
            return path;
        }

        /// <summary>
        /// Ends the session: saves its replay (the recorder's Save, which writes the results beside it) and returns the
        /// results' path (with no recorder, they're written where a replay of the scene would go). Called again, it writes nothing more and returns the same path.
        /// </summary>
        public string End()
        {
            if (endedPath != null) { return endedPath; }
            string replay = recorder != null ? recorder.Save() : null;
            // Saved, the recorder's Saved wrote them; with no recorder (or nothing recorded), they're written where a
            // replay of the scene would go.
            endedPath = replay != null && LastWrittenPath != null ? LastWrittenPath : Write(replay ?? Replay.NewPath(gameObject.scene.name));
            return endedPath;
        }
    }
}
