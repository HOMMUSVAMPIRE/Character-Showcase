using System.Collections;
using Level;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CharacterShowcase.Tests
{
    /// <summary>
    /// Tests of timed courses (CourseTimer and TimingGate): the character sprinting from a start gate to a finish gate
    /// is timed at the course's length over its run speed, a lap gate starts a lap on the first pass and ends it on
    /// the next (ignoring passes too soon after the start), and only the racer counts once there is one. The run
    /// speed is read from the movement mode, so retuning it doesn't break the tests.
    /// </summary>
    public class TimingGateTests
    {
        private CharacterTestRig rig;
        private GameObject timerObject;
        private CourseTimer timer;

        [SetUp]
        public void SetUp()
        {
            rig = new CharacterTestRig();
            timerObject = new GameObject("Course Timer");
            timer = timerObject.AddComponent<CourseTimer>();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.Destroy(timerObject);
            rig.Dispose();
            yield return null;
        }

        /// <summary>A gate across the course (along x, 2 m deep) at z, a trigger like the level's.</summary>
        private TimingGate Gate(float z, TimingGateRole role)
        {
            GameObject volume = rig.Box($"Gate ({role})", new Vector3(0f, 2f, z), new Vector3(10f, 4f, 0.6f));
            Object.Destroy(volume.GetComponent<MeshRenderer>());
            var gate = volume.AddComponent<TimingGate>();
            gate.Timer = timer;
            gate.Role = role;
            return gate;
        }

        [UnityTest]
        public IEnumerator SprintingFromStartToFinish_IsTimedAtTheRunSpeed()
        {
            const float length = 20f;
            rig.Floor(0f, 200f);
            timer.Distance = length;
            Gate(0f, TimingGateRole.Start);
            Gate(length, TimingGateRole.Finish);
            string shown = null;
            timer.OnDisplay.AddListener(text => shown = text);
            yield return rig.SpawnSettled(new Vector3(0f, 0.05f, -12f));
            timer.SetRacer(rig.Character.transform);

            // Up to speed well before the start gate, then on through the finish.
            yield return rig.Run(length / rig.RunSpeed + 2f, step => CharacterTestRig.Sprint(Vector3.forward, step));

            Assert.AreEqual(1, timer.Completed, "Sprinting through the start and finish gates should finish one run.");
            Assert.IsFalse(timer.IsRunning);
            float expected = length / rig.RunSpeed;
            // Gates are seen on the physics step the character enters them: a step either way.
            Assert.That(timer.LastTime, Is.EqualTo(expected).Within(2f * Time.fixedDeltaTime + 0.02f * expected),
                $"A {length} m run at {rig.RunSpeed:0.0} m/s should take about {expected:0.00} s.");
            Assert.AreEqual(timer.LastTime, timer.BestTime);
            StringAssert.Contains("m/s", shown, "The display should show the run's speed once the course has a distance.");
        }

        [UnityTest]
        public IEnumerator ALapGate_StartsOnTheFirstPass_AndEndsTheLapOnTheNext()
        {
            Transform racer = new GameObject("Racer").transform;
            try
            {
                timer.SetRacer(racer);
                timer.Pass(TimingGateRole.StartAndFinish, racer);
                Assert.IsTrue(timer.IsRunning, "The first pass should start a lap.");
                timer.Pass(TimingGateRole.StartAndFinish, racer);
                Assert.AreEqual(0, timer.Completed, "A pass straight after the start shouldn't end the lap.");

                yield return new WaitForSeconds(1.2f);
                timer.Pass(TimingGateRole.StartAndFinish, racer);
                Assert.AreEqual(1, timer.Completed, "A pass after the minimum run time should end the lap.");
                Assert.IsTrue(timer.IsRunning, "Ending a lap should start the next.");
                Assert.Greater(timer.LastTime, 1f);
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
                timer.Pass(TimingGateRole.Start, other);
                Assert.IsFalse(timer.IsRunning, "A pass by anything but the racer shouldn't start a run.");
                timer.Pass(TimingGateRole.Start, part);
                Assert.IsTrue(timer.IsRunning, "A pass by part of the racer should count.");
                yield return null;
            }
            finally
            {
                Object.Destroy(racer.gameObject);
                Object.Destroy(other.gameObject);
            }
        }
    }
}
