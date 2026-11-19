using System.Collections;
using MeridianMotion.Levels;
using NUnit.Framework;
using UnityEngine.TestTools;
using UnityEngine;

namespace MeridianMotion.Tests.Levels
{
    /// <summary>
    /// Tests of timed courses with the character (CourseTimer and TimingGate): sprinting from a start gate to a finish
    /// gate is timed at the course's length over its run speed. The run speed is read from the movement mode, so
    /// retuning it doesn't break the tests. (The timer's own rules, without a character, are CourseTimerTests'.)
    /// </summary>
    [Category(TestArea.Levels)]
    public class TimingGateTests : CharacterRigFixture
    {
        // Its tests need Unity's frames (the model's, animation's or IK's per-frame updates): see CharacterRigFixture.MovementOnly.
        protected override bool MovementOnly => false;

        private GameObject timerObject;
        private CourseTimer timer;

        [SetUp]
        public void SetUp()
        {
            timerObject = new GameObject("Course Timer");
            timer = timerObject.AddComponent<CourseTimer>();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.Destroy(timerObject);
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
            timer.OnTextChanged.AddListener(text => shown = text);
            yield return rig.SpawnSettled(new Vector3(0f, 0.05f, -12f));
            timer.SetRacer(rig.Character.transform);

            // Up to speed well before the start gate, then on through the finish.
            yield return rig.Run(length / rig.RunSpeed + 2f, step => CharacterTestRig.Sprint(Vector3.forward));

            Assert.AreEqual(1, timer.LapCount, "Sprinting through the start and finish gates should finish one run.");
            Assert.IsFalse(timer.IsRunning);
            float expected = length / rig.RunSpeed;
            // Gates are seen on the physics step the character enters them: a step either way.
            Assert.That(timer.LastTime, Is.EqualTo(expected).Within(2f * Time.fixedDeltaTime + 0.02f * expected),
                $"A {length} m run at {rig.RunSpeed:0.0} m/s should take about {expected:0.00} s.");
            Assert.AreEqual(timer.LastTime, timer.BestTime);
            StringAssert.Contains("m/s", shown, "The display should show the run's speed once the course has a distance.");
        }
    }
}
