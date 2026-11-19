using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using MeridianMotion.Characters;
using NUnit.Framework.Interfaces;
using NUnit.Framework;
using Unity.Profiling.LowLevel.Unsafe;
using Unity.Profiling;
using UnityEngine.TestTools;
using Debug = UnityEngine.Debug;

namespace MeridianMotion.Tests
{
    /// <summary>
    /// Logs, at the end of each test of the fixture it's on (and fixtures deriving from it), how long the test ran and
    /// the three profiler markers it spent the most time in: one line, "[Test timing] Fixture.Test: … s; most time in …",
    /// for finding the longest tests and what makes them long. The markers are the project's own (Movement.Step,
    /// Movement.Mode, Movement.Collision, each movement component's Movement.&lt;Component&gt;,
    /// Movement.MeasureCapabilities, Animation.Update, Camera.Update) and the engine's physics step (Physics.Simulate), recorded while the
    /// test runs. A marker's time includes the markers inside it (Movement.Step includes Movement.Mode). Every test
    /// fixture carries it (the hygiene check makes sure).
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = true)]
    public sealed class TestTimingAttribute : NUnitAttribute, IOuterUnityTestAction
    {
        // The most frames a marker's time is kept for in one test: far more than any test runs for.
        private const int MaxFrames = 50000;

        private static string[] markers;
        private readonly Stopwatch stopwatch = new();
        private readonly List<(string name, ProfilerRecorder recorder)> recorders = new();

        /// <summary>The markers recorded: the project's, every movement component's, and the engine's physics step.</summary>
        private static string[] Markers()
        {
            if (markers != null) { return markers; }
            var names = new List<string>
            {
                "Movement.Step", "Movement.Mode", "Movement.Collision", "Movement.MeasureCapabilities",
                "Animation.Update", "Camera.Update", "Physics.Simulate",
            };
            names.AddRange(typeof(MovementComponent).Assembly.GetTypes()
                .Where(t => !t.IsAbstract && typeof(MovementComponent).IsAssignableFrom(t))
                .Select(t => $"Movement.{t.Name}"));
            markers = names.ToArray();
            return markers;
        }

        public IEnumerator BeforeTest(ITest test)
        {
            recorders.Clear();
            string[] names = Markers();
            // The project's markers are made here too, so one that hasn't run yet (a component's, made on first use) is
            // recorded; then each is found by name among the profiler's, in whichever category it's in (the engine's
            // physics step is in Physics).
            foreach (string name in names.Where(n => !n.StartsWith("Physics."))) { _ = new ProfilerMarker(name); }
            var available = new List<ProfilerRecorderHandle>();
            ProfilerRecorderHandle.GetAvailable(available);
            var byName = new Dictionary<string, ProfilerRecorderHandle>();
            foreach (ProfilerRecorderHandle handle in available) { byName.TryAdd(ProfilerRecorderHandle.GetDescription(handle).Name, handle); }
            foreach (string name in names)
            {
                if (!byName.TryGetValue(name, out ProfilerRecorderHandle handle)) { continue; }
                var recorder = new ProfilerRecorder(handle, MaxFrames, ProfilerRecorderOptions.SumAllSamplesInFrame);
                recorder.Start();
                recorders.Add((name, recorder));
            }
            stopwatch.Restart();
            yield break;
        }

        public IEnumerator AfterTest(ITest test)
        {
            stopwatch.Stop();
            // A frame more, so the test's last frame's times are recorded too.
            yield return null;
            var totals = new List<(string name, double seconds)>();
            foreach (var (name, recorder) in recorders)
            {
                if (!recorder.Valid) { continue; }
                long nanoseconds = 0;
                for (int i = 0; i < recorder.Count; i++) { nanoseconds += recorder.GetSample(i).Value; }
                recorder.Dispose();
                if (nanoseconds > 0) { totals.Add((name, nanoseconds * 1e-9)); }
            }
            recorders.Clear();
            string most = totals.Count == 0 ? "no marked time"
                : "most time in " + string.Join(", ", totals.OrderByDescending(t => t.seconds).Take(3).Select(t => $"{t.name} {t.seconds:0.000} s"));
            Debug.Log($"[Test timing] {test.TypeInfo?.Name}.{test.Name}: {stopwatch.Elapsed.TotalSeconds:0.000} s; {most}");
        }
    }
}
