using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace CharacterShowcase.Tests
{
    /// <summary>
    /// A stress test of the movement: many characters stepped together on a course of walls and ledges, sprinting in
    /// turning directions, jumping and dashing at staggered times, so the abilities' checks for walls and ledges have
    /// something to find. It reports how long a step of all of them takes (the average and the worst, per character) in
    /// the test's output and the Console, and fails only far over budget, as a guard against a step becoming many times
    /// slower: timings vary between machines, so it's for spotting a change, not for exact numbers. For where a step's
    /// time goes, run it with the Profiler open: each part of a step has a marker (see docs/testing.md, Performance).
    /// </summary>
    [Category(TestArea.Movement)]
    [Category("Performance")]
    public class MovementPerformanceTests : CharacterRigFixture
    {
        // How many characters step together, how long they settle before timing starts, and how long they're timed.
        private const int CharacterCount = 50;
        private const float WarmUpSeconds = 1f;
        private const float TimedSeconds = 4f;
        // Far over what a character's step takes (well under a tenth of this on a desktop): only a step many times
        // slower than usual fails.
        private const double BudgetMillisecondsPerCharacter = 2.0;
        // The characters stand in a grid this far apart, in rows of this many.
        private const float Spacing = 5f;
        private const int PerRow = 10;

        // The extra characters and their copies of the movement mode (the rig cleans up its own).
        private readonly List<Object> created = new();
        private readonly List<MovementController> characters = new();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (Object obj in created) { if (obj != null) { Object.Destroy(obj); } }
            created.Clear();
            characters.Clear();
            yield return null;
        }

        /// <summary>The floor, a wall and a ledge beside each character's spot, and the characters.</summary>
        private void BuildCourse()
        {
            rig.Floor(width: 200f);
            for (int i = 0; i < CharacterCount; i++)
            {
                Vector3 spot = Spot(i);
                rig.Box($"Wall {i}", spot + new Vector3(2f, 2f, 0f), new Vector3(0.5f, 4f, 3f));
                rig.Box($"Ledge {i}", spot + new Vector3(-2f, 1f, 1.5f), new Vector3(1.5f, 2f, 1.5f));
            }

            rig.SpawnCharacter(Spot(0) + Vector3.up * 0.05f);
            characters.Add(rig.Movement);
            GameObject prefab = CharacterTestRig.CharacterPrefab();
            for (int i = 1; i < CharacterCount; i++)
            {
                GameObject character = Object.Instantiate(prefab, Spot(i) + Vector3.up * 0.05f, Quaternion.identity);
                created.Add(character);
                character.GetComponent<ACCharacterController>().enabled = false;
                var movement = character.GetComponent<MovementController>();
                // Its own copy of the movement mode, as the rig's character has, so nothing changes the asset.
                if (movement.movementHandler != null)
                {
                    movement.movementHandler = Object.Instantiate(movement.movementHandler);
                    created.Add(movement.movementHandler);
                }
                characters.Add(movement);
            }
        }

        private static Vector3 Spot(int i) => new((i % PerRow - PerRow / 2) * Spacing * 2f, 0f, (i / PerRow) * Spacing * 2f);

        /// <summary>
        /// What character i does on a step: sprints in a direction that turns slowly (so it runs at and along its wall
        /// and ledge), jumping and dashing now and then, each at its own time.
        /// </summary>
        private static Inputs InputsFor(int i, int step)
        {
            float angle = (step * 2f + i * 37f) % 360f;
            Vector3 direction = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
            int jumpAt = (step + i * 7) % 45, dashAt = (step + i * 11) % 120;
            if (jumpAt < 8) { Inputs jump = CharacterTestRig.Jump(jumpAt, direction); jump.sprint = Held(); return jump; }
            if (dashAt == 0) { return CharacterTestRig.Dash(0, direction); }
            return CharacterTestRig.Sprint(direction, step);
        }

        private static InputButton Held() => new() { value = true, holdDuration = 1f };

        [UnityTest]
        public IEnumerator ManyCharactersMoving_StepWithinBudget()
        {
            BuildCourse();
            int warmUp = CharacterTestRig.StepsFor(WarmUpSeconds), timed = CharacterTestRig.StepsFor(TimedSeconds);
            var stepTimes = new List<double>(timed);
            var stopwatch = new Stopwatch();
            // What the characters did while timed: a stress test of characters standing still would measure nothing.
            CharacterState seen = CharacterState.Idle;
            for (int step = 0; step < warmUp + timed; step++)
            {
                yield return new WaitForFixedUpdate();
                stopwatch.Restart();
                for (int i = 0; i < characters.Count; i++)
                {
                    characters[i].ProcessMovementInputs(InputsFor(i, step));
                    characters[i].MovementCleanup();
                }
                stopwatch.Stop();
                if (step < warmUp) { continue; }
                stepTimes.Add(stopwatch.Elapsed.TotalMilliseconds);
                foreach (MovementController character in characters) { seen |= character.lastFrameMovementData.state; }
            }
            yield return new WaitForFixedUpdate();

            foreach (MovementController character in characters)
            {
                Vector3 p = character.GetComponent<Rigidbody>().position;
                Assert.That(float.IsFinite(p.x) && float.IsFinite(p.y) && float.IsFinite(p.z), Is.True, $"{character.name} ended up at {p}.");
            }
            Assert.That(seen.HasFlag(CharacterState.Moving) && seen.HasFlag(CharacterState.Jumping) && seen.HasFlag(CharacterState.Dashing), Is.True,
                $"Test setup: the characters should run, jump and dash while they're timed (they did: {seen}).");
            double average = stepTimes.Average() / CharacterCount, worst = stepTimes.Max() / CharacterCount;
            string report = $"[Movement performance] {CharacterCount} characters, {timed} steps: a character's step takes " +
                            $"{average * 1000.0:0} µs on average, {worst * 1000.0:0} µs at worst (all {CharacterCount}: " +
                            $"{stepTimes.Average():0.00} ms on average, {stepTimes.Max():0.00} ms at worst), doing: {seen}.";
            Debug.Log(report);
            TestContext.WriteLine(report);
            Assert.That(average, Is.LessThan(BudgetMillisecondsPerCharacter),
                $"A character's step averaged {average:0.000} ms, many times its usual cost: profile it (see docs/testing.md, Performance).");
        }
    }
}
