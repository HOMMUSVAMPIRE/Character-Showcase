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
    /// there is one; splits are recorded in order (out of order, or with no run under way, ignored) and kept with the
    /// last run and the best run. Passes are made directly, as a gate makes them. The split tests set the level's clock
    /// themselves (LevelClock.TickSource), so their times are exact.
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
            LevelClock.TickSource = null;
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

        private long tick;

        /// <summary>Runs the level's clock from the test (tick), starting at 1000.</summary>
        private void OwnTheClock()
        {
            tick = 1000;
            LevelClock.TickSource = () => tick;
        }

        private void After(float seconds) => tick += Mathf.RoundToInt(seconds / LevelClock.StepLength);

        private float Seconds(float seconds) => Mathf.RoundToInt(seconds / LevelClock.StepLength) * LevelClock.StepLength;

        [Test]
        public void PassingSplits_InOrder_RecordsEach()
        {
            OwnTheClock();
            timer.RegisterGatePass(TimingGateRole.Start, null);
            After(2f);
            timer.RegisterSplit(0, null);
            After(3f);
            timer.RegisterSplit(1, null);
            Assert.That(timer.CurrentSplits, Is.EqualTo(new[] { Seconds(2f), Seconds(5f) }).Within(1e-4f));
        }

        [Test]
        public void PassingASplit_OutOfOrder_OrAgain_IsIgnored()
        {
            OwnTheClock();
            timer.RegisterGatePass(TimingGateRole.Start, null);
            After(1f);
            timer.RegisterSplit(1, null);
            Assert.That(timer.CurrentSplits, Is.Empty, "Split 1 before split 0 should be ignored.");
            timer.RegisterSplit(0, null);
            After(1f);
            timer.RegisterSplit(0, null);
            Assert.That(timer.CurrentSplits.Count, Is.EqualTo(1), "Passing split 0 again should be ignored.");
        }

        [Test]
        public void PassingASplit_WithNoRunUnderWay_IsIgnored()
        {
            OwnTheClock();
            timer.RegisterSplit(0, null);
            Assert.That(timer.CurrentSplits, Is.Empty);
        }

        [Test]
        public void FinishingARun_RaisesRunFinished_WithItsSplits()
        {
            OwnTheClock();
            var runs = new System.Collections.Generic.List<CourseRun>();
            timer.RunFinished += runs.Add;
            timer.RegisterGatePass(TimingGateRole.Start, null);
            After(2f);
            timer.RegisterSplit(0, null);
            After(2f);
            timer.RegisterGatePass(TimingGateRole.Finish, null);

            Assert.That(runs.Count, Is.EqualTo(1));
            Assert.That(runs[0].Time, Is.EqualTo(Seconds(4f)).Within(1e-4f));
            Assert.That(runs[0].Splits, Is.EqualTo(new[] { Seconds(2f) }).Within(1e-4f));
            Assert.That(timer.LastSplits, Is.EqualTo(runs[0].Splits));
            Assert.That(timer.CurrentSplits, Is.Empty, "A finished run's splits shouldn't stay as the run under way's.");
            StringAssert.Contains("SPLITS", timer.Readout());
        }

        [Test]
        public void BestSplits_AreTheBestRunsSplits()
        {
            OwnTheClock();
            void Run(float toSplit, float toFinish)
            {
                timer.RegisterGatePass(TimingGateRole.Start, null);
                After(toSplit);
                timer.RegisterSplit(0, null);
                After(toFinish);
                timer.RegisterGatePass(TimingGateRole.Finish, null);
            }
            Run(1f, 5f); // 6 s, the quicker split.
            Run(3f, 2f); // 5 s, the best run.
            Run(1f, 9f); // 10 s.
            Assert.That(timer.BestTime, Is.EqualTo(Seconds(5f)).Within(1e-4f));
            Assert.That(timer.BestSplits, Is.EqualTo(new[] { Seconds(3f) }).Within(1e-4f), "The best splits should be the best run's, not each split's best.");
            Assert.That(timer.LastSplits, Is.EqualTo(new[] { Seconds(1f) }).Within(1e-4f));
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
