using System.Collections;
using MeridianMotion.Levels;
using NUnit.Framework;
using UnityEngine.TestTools;
using UnityEngine;

namespace MeridianMotion.Tests.Levels
{
    /// <summary>
    /// Tests of a CourseTimer's own rules, with no character (TimingGateTests times one): a lap gate starts a lap on the
    /// first pass and ends it on the next (ignoring passes too soon after the start), and only the racer counts once
    /// there is one. Passes are made directly, as a gate makes them.
    /// </summary>
    [Category(TestArea.Levels)]
    [TestTiming]
    public class CourseTimerTests
    {
        private GameObject timerObject;
        private CourseTimer timer;
        private FrameTimeLimit fasterThanRealTime;

        [SetUp]
        public void SetUp()
        {
            timerObject = new GameObject("Course Timer");
            timer = timerObject.AddComponent<CourseTimer>();
            // Sped up as the character tests are (see CharacterTestRig.TimeScale), so waiting out the timer's minimum
            // run time takes a moment, not the whole of it. It's timed in game time, so the waits are the same.
            fasterThanRealTime = FrameTimeLimit.Start(CharacterTestRig.TimeScale, CharacterTestRig.MaxFrameTime);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.Destroy(timerObject);
            Object.Destroy(fasterThanRealTime.gameObject); // Which puts the time scale back.
            yield return null;
        }

        [UnityTest]
        public IEnumerator ALapGate_StartsOnTheFirstPass_AndEndsTheLapOnTheNext()
        {
            Transform racer = new GameObject("Racer").transform;
            try
            {
                timer.SetRacer(racer);
                timer.RegisterGatePass(TimingGateRole.StartAndFinish, racer);
                Assert.IsTrue(timer.IsRunning, "The first pass should start a lap.");
                timer.RegisterGatePass(TimingGateRole.StartAndFinish, racer);
                Assert.AreEqual(0, timer.LapCount, "A pass straight after the start shouldn't end the lap.");

                // Just past the timer's minimum run time (see CourseTimer).
                yield return new WaitForSeconds(timer.MinimumRunTime + 0.2f);
                timer.RegisterGatePass(TimingGateRole.StartAndFinish, racer);
                Assert.AreEqual(1, timer.LapCount, "A pass after the minimum run time should end the lap.");
                Assert.IsTrue(timer.IsRunning, "Ending a lap should start the next.");
                Assert.Greater(timer.LastTime, timer.MinimumRunTime);
            }
            finally
            {
                Object.Destroy(racer.gameObject);
            }
        }

        [UnityTest]
        public IEnumerator OnlyTheRacer_Counts()
        {
            Transform racer = new GameObject("Racer").transform, other = new GameObject("Someone Else").transform;
            Transform part = new GameObject("Racer's Part").transform;
            part.SetParent(racer);
            try
            {
                timer.SetRacer(racer);
                timer.RegisterGatePass(TimingGateRole.Start, other);
                Assert.IsFalse(timer.IsRunning, "A pass by anything but the racer shouldn't start a run.");
                timer.RegisterGatePass(TimingGateRole.Start, part);
                Assert.IsTrue(timer.IsRunning, "A pass by part of the racer should count.");
                yield return null;
            }
            finally
            {
                Object.Destroy(racer.gameObject);
                Object.Destroy(other.gameObject);
            }
        }

        /// <summary>Each finished lap is passed on with its time and whether it's the best yet (the HUD's lap notifications).</summary>
        [UnityTest]
        public IEnumerator AFinishedLap_IsPassedOn_WithItsTimeAndWhetherItsTheBest()
        {
            var laps = new System.Collections.Generic.List<(float time, bool best)>();
            timer.OnLapFinished.AddListener((time, best) => laps.Add((time, best)));
            timer.RegisterGatePass(TimingGateRole.StartAndFinish, null);
            yield return new WaitForSeconds(timer.MinimumRunTime + 0.5f);
            timer.RegisterGatePass(TimingGateRole.StartAndFinish, null);
            yield return new WaitForSeconds(timer.MinimumRunTime + 0.2f);
            timer.RegisterGatePass(TimingGateRole.StartAndFinish, null);
            yield return new WaitForSeconds(timer.MinimumRunTime + 0.8f);
            timer.RegisterGatePass(TimingGateRole.StartAndFinish, null);

            Assert.That(laps.Count, Is.EqualTo(3));
            Assert.That(laps[0].best, Is.True, "The first lap is the best yet.");
            Assert.That(laps[1].best, Is.True, "A faster lap is the new best.");
            Assert.That(laps[2].best, Is.False, "A slower one isn't.");
            Assert.That(laps[1].time, Is.EqualTo(timer.BestTime).Within(1e-5f));
            Assert.That(laps[2].time, Is.EqualTo(timer.LastTime).Within(1e-5f));
        }
    }
}
