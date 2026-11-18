using System;
using System.Collections;
using System.Collections.Generic;
using Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CharacterShowcase.Tests.Movement
{
    /// <summary>
    /// How steadily the character keeps to the ground, for each character (the tested one, and the fox): standing, walking,
    /// sprinting and turning on level ground, standing on a step's edge, over a hill of ramps, up and down stairs, along a
    /// wall and crouched. Each run is recorded step by step (see StepTrace) and written to Logs/StepTraces, and is held to
    /// what steady contact means there: never leaving the ground, never overlapping the world, never being pushed out of
    /// it or lifted back out of the ground, and on level ground, never moving up or down at all. They're the measure of
    /// the jitter a change to collision is meant to fix, so a trace of each run before and after can be compared. Each
    /// character runs as it is (hovering over steps) and with its body standing on its feet (Hover Over Steps off);
    /// hovering, the character keeps its feet on the ground over the hill, on the stairs and at the step's edge too (the
    /// fox's long, flat body still rides the steps and slopes ahead of its feet).
    /// </summary>
    [Category(TestArea.Movement)]
    public class GroundContactTests : CharacterRigFixture
    {
        private const string FoxPrefabPath = "Assets/Prefabs/Fox.prefab";

        // How far (m) the character's height may move in a step on level ground: rounding, not movement.
        private const float Level = 1e-4f;

        // A character's name with this after it is run with Hover Over Steps off: its body standing on its feet.
        private const string OnItsFeet = "_OnItsFeet";

        /// <summary>
        /// The characters the runs are made with: each as it is (hovering over steps), and with its body standing on its
        /// feet (Hover Over Steps off).
        /// </summary>
        /// (The fox's body is held above its legs, which only hovering over steps stands on: it has no other way.)
        public static IEnumerable<string> Characters() => new[] { "Character", "Fox", "Character" + OnItsFeet };

        private static bool Hovering(string character) => !character.EndsWith(OnItsFeet);

        /// <summary>
        /// Whether the character, as it's run, keeps its feet on the ground over slopes, steps and edges: hovering over
        /// steps, with a body that doesn't reach far out over the ground ahead and behind. A long, flat body (the fox's
        /// box) still rides the steps and slopes ahead of its feet.
        /// </summary>
        private static bool KeepsItsFeetOnTheGround(string character) => Hovering(character);

        /// <summary>
        /// Holds a run to the character's feet staying on the ground under them, if they should (see
        /// KeepsItsFeetOnTheGround), on ground rising or falling at the given gradient (rise over run): unless its body
        /// reaches out over it further than its lift above the feet clears (the back of a long body over the steps behind,
        /// going down stairs), when its body rests on the ground there, holding its feet as far above the ground under
        /// them as it reaches out beyond that.
        /// </summary>
        private void AssertFeetOnTheGround(string character, StepTrace trace, float gradient = 0f)
        {
            if (!KeepsItsFeetOnTheGround(character)) { return; }
            float reach = Mathf.Max(rig.FrontExtent, rig.BackExtent);
            float resting = Mathf.Max(0f, reach * gradient - rig.Movement.BodyLift);
            Assert.That(trace.HighestFeet, Is.LessThan(StepTrace.FeetAboveGround + resting),
                $"Hovering over steps, the character's feet should stay on the ground. {trace.Summary()}");
        }

        private GameObject PrefabOf(string character)
        {
            if (!character.StartsWith("Fox")) { return CharacterTestRig.CharacterPrefab(); }
            GameObject fox = CharacterTestRig.LoadPrefab(FoxPrefabPath);
            if (fox == null) { Assert.Ignore($"There's no fox ('{FoxPrefabPath}'): generate it with Tools > Character Showcase > Generate Fox."); }
            return fox;
        }

        /// <summary>The character, standing on a floor at the origin, its model loaded.</summary>
        private IEnumerator Spawn(string character)
        {
            rig.Floor(0f, 200f);
            yield return rig.SpawnSettled(new Vector3(0f, 0.05f, 0f), PrefabOf(character), settleSeconds: 0f);
            rig.Movement.HoverOverSteps = Hovering(character);
            yield return rig.Run(0.3f, _ => CharacterTestRig.Idle());
        }

        /// <summary>Puts the character's feet at feet (in the world just built) and lets it settle there.</summary>
        private IEnumerator SettleAt(Vector3 feet, float seconds = 0.5f)
        {
            Physics.SyncTransforms();
            rig.Teleport(feet);
            yield return rig.Run(seconds, _ => CharacterTestRig.Idle());
        }

        /// <summary>Runs the character with the given inputs, recording each step, and writes the record out.</summary>
        private IEnumerator Record(string name, float seconds, Func<int, Inputs> inputs, Action<StepTrace> done)
        {
            var trace = new StepTrace(rig);
            yield return rig.Run(seconds, inputs, trace.Record);
            string file = trace.Write(name);
            string summary = trace.Summary();
            TestContext.WriteLine($"{name}: {summary}\n{file}");
            Debug.Log($"[GroundContact] {name}: {summary}");
            done(trace);
        }

        /// <summary>Holds a run to steady contact: on the ground, overlapping nothing, never pushed out of it.</summary>
        private static void AssertSteady(StepTrace trace)
        {
            string summary = trace.Summary();
            Assert.That(trace.AirborneSteps, Is.Zero, $"The character left the ground. {summary}");
            Assert.That(trace.Overlaps, Is.Zero, $"The character's body overlapped the world. {summary}");
            Assert.That(trace.Depenetrations, Is.Zero, $"The character's body was pushed out of the world. {summary}");
        }

        /// <summary>Holds a run on level ground to never moving up or down.</summary>
        private static void AssertLevel(StepTrace trace)
        {
            AssertSteady(trace);
            Assert.That(trace.LargestRise, Is.LessThan(Level), $"The character moved up or down on level ground. {trace.Summary()}");
            Assert.That(trace.HighestFeet, Is.LessThan(StepTrace.FeetAboveGround), $"On level ground, the character's feet were off the ground. {trace.Summary()}");
        }

        // ---- Level ground ----

        [UnityTest]
        public IEnumerator StandingStill_OnLevelGround_HoldsItsHeight([ValueSource(nameof(Characters))] string character)
        {
            yield return Spawn(character);
            yield return Record($"StandingStill_{character}", 2f, _ => CharacterTestRig.Idle(), AssertLevel);
        }

        [UnityTest]
        public IEnumerator WalkingAndSprinting_OnLevelGround_HoldsItsHeight([ValueSource(nameof(Characters))] string character, [Values(false, true)] bool sprint)
        {
            yield return Spawn(character);
            yield return Record($"{(sprint ? "Sprinting" : "Walking")}_{character}", 3f,
                step => sprint ? CharacterTestRig.Sprint(Vector3.forward) : CharacterTestRig.Move(Vector3.forward), AssertLevel);
        }

        [UnityTest]
        public IEnumerator WalkingInCircles_OnLevelGround_HoldsItsHeight([ValueSource(nameof(Characters))] string character)
        {
            yield return Spawn(character);
            // The heading turns a quarter turn a second, then sharply back the other way: turning on the spot, and in an arc.
            float dt = Time.fixedDeltaTime;
            yield return Record($"WalkingInCircles_{character}", 4f, step =>
            {
                float t = step * dt;
                float angle = t < 2f ? 90f * t : 180f - 120f * (t - 2f);
                return CharacterTestRig.Move(Quaternion.Euler(0f, angle, 0f) * Vector3.forward);
            }, AssertLevel);
        }

        [UnityTest]
        public IEnumerator CrouchWalking_OnLevelGround_HoldsItsHeight([ValueSource(nameof(Characters))] string character)
        {
            yield return Spawn(character);
            yield return rig.Run(1, _ => CharacterTestRig.Crouch());
            yield return rig.Run(0.5f, _ => CharacterTestRig.Idle());
            Assume.That(rig.InState(CharacterState.Crouching), "The character should be crouched.");
            yield return Record($"CrouchWalking_{character}", 2f, _ => CharacterTestRig.Move(Vector3.forward), AssertLevel);
        }

        [UnityTest]
        public IEnumerator WalkingAlongAWall_SteeringIntoIt_HoldsItsHeight_AndKeepsToTheWall([ValueSource(nameof(Characters))] string character)
        {
            yield return Spawn(character);
            // A wall just off the character's right side, walked along while steering half into it.
            float face = rig.SideExtent + 0.3f;
            rig.Box("Wall", new Vector3(face + 0.5f, 1.5f, 10f), new Vector3(1f, 3f, 40f));
            yield return SettleAt(new Vector3(0f, 0.02f, 0f));
            Vector3 heading = new Vector3(0.5f, 0f, 1f).normalized;
            StepTrace walked = null;
            yield return Record($"AlongAWall_{character}", 3f, _ => CharacterTestRig.Move(heading), trace => walked = trace);
            AssertLevel(walked);
            Assert.That(walked.SideContactChanges, Is.LessThanOrEqualTo(1), $"Once at the wall, the character should keep to it. {walked.Summary()}");
        }

        // ---- Edges, slopes and stairs ----

        [UnityTest]
        public IEnumerator StandingOnAStepsEdge_HoldsStill([ValueSource(nameof(Characters))] string character)
        {
            yield return Spawn(character);
            // A step a little over half the step height, its edge at z = 0; the character's feet a little past the edge,
            // so the front of its body is out over the floor below.
            float height = 0.6f * rig.StepHeight;
            rig.Box("Step", new Vector3(0f, height / 2f, -5f), new Vector3(10f, height, 10f));
            Physics.SyncTransforms();
            rig.Teleport(new Vector3(0f, height + 0.02f, 0.25f * rig.FrontExtent));
            yield return Record($"OnAStepsEdge_Settling_{character}", 1f, _ => CharacterTestRig.Idle(), _ => { });
            yield return Record($"OnAStepsEdge_{character}", 2f, _ => CharacterTestRig.Idle(), trace =>
            {
                AssertSteady(trace);
                Assert.That(trace.LargestRise, Is.LessThan(Level), $"Standing still on a step's edge, the character moved up or down. {trace.Summary()}");
                AssertFeetOnTheGround(character, trace);
            });
        }

        [UnityTest]
        public IEnumerator WalkingOverAHillOfRamps_FollowsItEvenly([ValueSource(nameof(Characters))] string character)
        {
            yield return Spawn(character);
            // Up a ramp a little over half as steep as the steepest walkable slope, across a plateau, and down another.
            float angle = 0.55f * rig.MaxSlopeAngle, rise = 1f;
            float run = rise / Mathf.Tan(angle * Mathf.Deg2Rad);
            float start = rig.FrontExtent + 1f, plateau = 3f;
            Ramp(new Vector3(0f, 0f, start), new Vector3(0f, rise, start + run));
            rig.Box("Plateau", new Vector3(0f, rise / 2f, start + run + plateau / 2f), new Vector3(6f, rise, plateau));
            Ramp(new Vector3(0f, rise, start + run + plateau), new Vector3(0f, 0f, start + 2f * run + plateau));
            yield return SettleAt(new Vector3(0f, 0.02f, 0f));
            float length = start + 2f * run + plateau + rig.BackExtent + 1f;
            yield return Record($"OverAHill_{character}", rig.SecondsToWalk(length), _ => CharacterTestRig.Move(Vector3.forward), trace =>
            {
                AssertSteady(trace);
                // Up, then down: its height turns once.
                Assert.That(trace.HeightReversals, Is.LessThanOrEqualTo(1), $"The character wobbled up and down along the ramps. {trace.Summary()}");
                AssertFeetOnTheGround(character, trace, Mathf.Tan(angle * Mathf.Deg2Rad));
            });
            float over = start + 2f * run + plateau;
            Assert.That(rig.Position.z, Is.GreaterThan(over), "The character should have walked over the hill.");
            Assert.That(rig.Position.y, Is.EqualTo(0f).Within(0.02f), "It should be back down on the floor.");
        }

        [UnityTest]
        public IEnumerator WalkingUpAndDownStairs_KeepsToTheSteps([ValueSource(nameof(Characters))] string character, [Values(true, false)] bool up)
        {
            yield return Spawn(character);
            // Twelve steps as steep as the levels' (3 up to 4 along), each a little over half the step height.
            const int steps = 12;
            float rise = 0.6f * rig.StepHeight, run = 4f / 3f * rise, top = steps * rise, start = rig.FrontExtent + 1f;
            for (int i = 0; i < steps - 1; i++)
            {
                float height = up ? rise * (i + 1) : top - rise * (i + 1);
                rig.Box($"Step {i + 1}", new Vector3(0f, height / 2f, start + i * run + run / 2f), new Vector3(6f, height, run));
            }
            float landing = up ? start + (steps - 1) * run + 5f : start - 5f;
            rig.Box("Landing", new Vector3(0f, top / 2f, landing), new Vector3(6f, top, 10f));
            yield return SettleAt(new Vector3(0f, (up ? 0f : top) + 0.02f, 0f));
            float end = start + steps * run + rig.BackExtent + 1f;
            yield return Record($"{(up ? "UpStairs" : "DownStairs")}_{character}", rig.SecondsToWalk(end), _ => CharacterTestRig.Move(Vector3.forward), trace =>
            {
                AssertSteady(trace);
                // Steadily up, or down: its height never turns.
                Assert.That(trace.HeightReversals, Is.Zero, $"The character bobbed up and down on the stairs. {trace.Summary()}");
                AssertFeetOnTheGround(character, trace, rise / run);
            });
            Assert.That(rig.Position.z, Is.GreaterThan(start + steps * run), up ? "The character should have walked up the flight." : "The character should have walked down the flight.");
            Assert.That(rig.Position.y, Is.EqualTo(up ? top : 0f).Within(0.02f), up ? "It should be on the landing at the top." : "It should be on the floor at the bottom.");
        }

        /// <summary>A walkable ramp, 1 m thick and 6 m wide, its top from one point to another.</summary>
        private void Ramp(Vector3 from, Vector3 to)
        {
            Vector3 along = to - from;
            Quaternion tilt = Quaternion.LookRotation(along.normalized, Vector3.up);
            rig.Box("Ramp", (from + to) / 2f - tilt * Vector3.up * 0.5f, new Vector3(6f, 1f, along.magnitude), tilt);
        }
    }
}
