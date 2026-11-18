using System;
using System.Collections;
using System.Collections.Generic;
using Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CharacterShowcase.Tests
{
    /// <summary>
    /// Tests that the movement is deterministic on one build: the same state, input frames and world give the same steps,
    /// bit for bit (pose and velocity), whatever else differs. A long session times things exactly as a fresh one (the
    /// character's tick clock); the same scene built in another order runs the same (stable choices between equally near
    /// floors, walls and ropes); a character saved mid-ability and put back (MovementController.SaveState, RestoreState),
    /// the level's clock with it, steps on exactly as it did; and a run whose world needs Unity's physics steps (a moving
    /// platform) is the same at another frame rate. What prediction in a networked game relies on: each difference
    /// would be a misprediction.
    /// </summary>
    [Category(TestArea.Movement)]
    public class DeterminismTests : CharacterRigFixture
    {
        private readonly struct Pose : IEquatable<Pose>
        {
            public readonly Vector3 position, velocity;
            public readonly Quaternion rotation;

            public Pose(CharacterTestRig rig)
            {
                position = rig.Position;
                rotation = rig.Body.rotation;
                velocity = rig.Velocity;
            }

            public bool Equals(Pose other) => position.Equals(other.position) && rotation.Equals(other.rotation) && velocity.Equals(other.velocity);
            public override string ToString() => $"at {position:F6}, velocity {velocity:F6}";
        }

        /// <summary>The first step two runs differ at (-1 for none), and how.</summary>
        private static string FirstDifference(IReadOnlyList<Pose> a, IReadOnlyList<Pose> b)
        {
            if (a.Count != b.Count) { return $"they ran {a.Count} and {b.Count} steps"; }
            for (int i = 0; i < a.Count; i++)
            {
                if (!a[i].Equals(b[i])) { return $"step {i}: {b[i]}, was {a[i]}"; }
            }
            return null;
        }

        private IEnumerator Record(int steps, Func<int, Inputs> script, List<Pose> into, CharacterState? mustDo = null)
        {
            CharacterState did = CharacterState.Idle;
            yield return rig.Run(steps, script, _ =>
            {
                into.Add(new Pose(rig));
                did |= rig.Movement.lastFrameMovementData.state;
            });
            if (mustDo is { } wanted) { Assume.That(did & wanted, Is.EqualTo(wanted), $"Test setup: the run should do {wanted} (it did {did})."); }
        }

        // ---- A long session ----

        /// <summary>
        /// A character whose clock starts three billion steps in (some 700 days at 50 a second) walks off a ledge, jumps
        /// in its coyote time, dashes and lands exactly as one starting at 0: it times everything in whole steps
        /// (MovementController.StepTick, SecondsSince), so nothing drifts or loses its fractions as the clock grows.
        /// </summary>
        [UnityTest]
        public IEnumerator ALongSession_TimesEverythingExactlyAsAFreshOne()
        {
            var runs = new List<Pose>[2];
            for (int run = 0; run < 2; run++)
            {
                if (run > 0) { yield return ReplaceRig(); }
                rig.Floor(-2f);
                rig.Box("Ledge", new Vector3(0f, -0.5f, 0f), new Vector3(4f, 1f, 4f));
                rig.SpawnCharacter(new Vector3(0f, 0.02f, 0f));
                rig.Movement.SetClock(run == 0 ? 0u : 3_000_000_000u);
                int leftAt = -1;
                Inputs Script(int step)
                {
                    if (step < 15) { return CharacterTestRig.Idle(); }
                    if (leftAt < 0 && !rig.Grounded) { leftAt = step; }
                    if (leftAt >= 0 && step >= leftAt + 2 && step < leftAt + 10) { return CharacterTestRig.Jump(Vector3.forward); }
                    if (leftAt >= 0 && step == leftAt + 25) { return CharacterTestRig.Dash(Vector3.forward); }
                    return CharacterTestRig.Move(Vector3.forward);
                }
                runs[run] = new List<Pose>();
                yield return Record(150, Script, runs[run], CharacterState.Jumping | CharacterState.Dashing);
            }
            string differs = FirstDifference(runs[0], runs[1]);
            Assert.That(differs, Is.Null, $"Three billion steps in, the character should move exactly as at the start: {differs}.");
        }

        // ---- The same scene, built in another order ----

        public enum Scene { CoplanarFloors, ACornerOfTwoWalls, TwoRopes }

        /// <summary>Builds the scene's parts in order, or the other way round.</summary>
        private void Build(Scene scene, bool reversed)
        {
            var parts = new List<Action>();
            switch (scene)
            {
                case Scene.CoplanarFloors:
                    // Two floors, their tops level, overlapping where the character walks: every ground query meets both.
                    parts.Add(() => rig.Box("Floor A", new Vector3(0f, -0.5f, 2f), new Vector3(6f, 1f, 10f)));
                    parts.Add(() => rig.Box("Floor B", new Vector3(0.5f, -0.5f, 4f), new Vector3(4f, 1f, 12f)));
                    break;
                case Scene.ACornerOfTwoWalls:
                    rig.Floor();
                    // A corner ahead and to the right, walked into diagonally: pushed out of both walls at once.
                    parts.Add(() => rig.Box("Wall Ahead", new Vector3(0f, 1.5f, 2.5f), new Vector3(6f, 3f, 0.4f)));
                    parts.Add(() => rig.Box("Wall Right", new Vector3(1.5f, 1.5f, 0f), new Vector3(0.4f, 3f, 6f)));
                    break;
                case Scene.TwoRopes:
                    rig.Floor();
                    // Two ropes as near as each other to the character jumping between them.
                    parts.Add(() => rig.Rope(new Vector3(-0.35f, 4.5f, 2f), 3f));
                    parts.Add(() => rig.Rope(new Vector3(0.35f, 4.5f, 2f), 3f));
                    break;
            }
            if (reversed) { parts.Reverse(); }
            foreach (Action part in parts) { part(); }
            Physics.SyncTransforms();
        }

        private static Inputs SceneScript(Scene scene, int step) => scene switch
        {
            Scene.ACornerOfTwoWalls => CharacterTestRig.Sprint(new Vector3(1f, 0f, 1f).normalized),
            Scene.TwoRopes => step >= 10 && step < 22 ? CharacterTestRig.Jump(Vector3.forward) : CharacterTestRig.Move(step < 10 ? Vector3.forward : Vector3.zero),
            _ => CharacterTestRig.Move(new Vector3(0.3f, 0f, 1f)),
        };

        /// <summary>
        /// The same scene built in another order (its colliders, or its ropes, made the other way round) runs exactly the
        /// same: where two hits are as near as each other (coplanar floors, a corner), or two ropes, the movement chooses by
        /// where they are (StableOrder), not by the order the scene was built in.
        /// </summary>
        [UnityTest]
        public IEnumerator TheSameScene_BuiltInAnotherOrder_RunsTheSame([Values] Scene scene)
        {
            var runs = new List<Pose>[2];
            for (int run = 0; run < 2; run++)
            {
                if (run > 0) { yield return ReplaceRig(); }
                Build(scene, reversed: run > 0);
                yield return rig.SpawnSettled(new Vector3(0f, 0.05f, 0f));
                runs[run] = new List<Pose>();
                yield return Record(100, step => SceneScript(scene, step), runs[run],
                    scene == Scene.TwoRopes ? CharacterState.RopeSwinging : null);
            }
            string differs = FirstDifference(runs[0], runs[1]);
            Assert.That(differs, Is.Null, $"Built in another order, the scene should run the same: {differs}.");
        }

        // ---- Saved and put back ----

        public enum Ability { Jumping, WallClimbing, Ziplining, OnAMovingPlatform, ChangingSize }

        private IEnumerator SetUp(Ability ability)
        {
            switch (ability)
            {
                case Ability.WallClimbing:
                    rig.Floor();
                    yield return rig.SpawnSettled(new Vector3(0f, 0.05f, 0f));
                    rig.ClimbableBox("Wall", new Vector3(0f, 5f, rig.FrontExtent + 0.8f), new Vector3(10f, 10f, 1f));
                    break;
                case Ability.Ziplining:
                    rig.Floor();
                    Level.Zipline line = rig.Zipline(new Vector3(0f, 8f, 0f), new Vector3(0f, 4f, 24f));
                    yield return rig.SpawnSettled(Vector3.zero, settleSeconds: 0f);
                    rig.Teleport(line.PointAt(2f) - Vector3.up * (rig.ZiplineHangHeight - 0.15f));
                    break;
                case Ability.OnAMovingPlatform:
                    rig.Platform(Vector3.zero, new Vector3(4f, 0.3f, 4f), new Vector3(2f, 1f, 0f), 3f, new Vector3(0f, 20f, 0f));
                    yield return rig.SpawnSettled(new Vector3(0f, 0.05f, 0f));
                    break;
                default:
                    rig.Floor();
                    yield return rig.SpawnSettled(new Vector3(0f, 0.05f, 0f));
                    break;
            }
            Physics.SyncTransforms();
        }

        private Inputs AbilityScript(Ability ability, int step)
        {
            switch (ability)
            {
                // A jump every 35 steps, so one's under way, or starts, after either save.
                case Ability.Jumping: return step % 35 >= 5 && step % 35 < 15 ? CharacterTestRig.Jump(Vector3.forward) : CharacterTestRig.Move(Vector3.forward);
                case Ability.WallClimbing: return CharacterTestRig.Move(Vector3.forward);
                case Ability.Ziplining: return CharacterTestRig.Idle();
                case Ability.OnAMovingPlatform: return CharacterTestRig.Move(step < 20 ? Vector3.right * 0.3f : Vector3.zero);
                default:
                    if (step == 3) { rig.Controller.SetScale(new Vector3(1.4f, 1.4f, 1.4f)); }
                    return CharacterTestRig.Move(Vector3.forward * 0.5f);
            }
        }

        private static CharacterState? Doing(Ability ability) => ability switch
        {
            Ability.Jumping => CharacterState.Jumping,
            Ability.WallClimbing => CharacterState.WallClimbing,
            Ability.Ziplining => CharacterState.Ziplining,
            _ => null,
        };

        /// <summary>
        /// A character saved partway through (mid-jump, mid-climb, riding a zipline, on a moving platform, changing size)
        /// and put back, the level's clock with it, then given the same frames again, steps on exactly as it did from
        /// there: the snapshot holds everything a step reads or changes. The save is between steps, at a few ticks.
        /// </summary>
        [UnityTest]
        public IEnumerator SavedMidAbility_AndPutBack_StepsOnExactlyAsItDid([Values] Ability ability, [Values(12, 37)] int savedAt)
        {
            yield return SetUp(ability);
            int at = savedAt;
            var before = new List<Pose>();
            yield return Record(at, step => AbilityScript(ability, step), before);
            MovementStateSnapshot saved = rig.Movement.SaveState();
            long levelTick = rig.LevelTick;
            Assert.That(saved.Summary.position, Is.EqualTo(rig.Position), "The summary should hold the character's pose.");

            var first = new List<Pose>();
            yield return Record(40, step => AbilityScript(ability, at + step), first, Doing(ability));
            Assert.That(rig.Movement.RestoreState(saved), Is.True);
            rig.LevelTick = levelTick;
            Assert.That(rig.Movement.StepTick, Is.EqualTo(saved.StepTick), "Put back, its clock should be where it was.");
            var again = new List<Pose>();
            yield return Record(40, step => AbilityScript(ability, at + step), again);
            string differs = FirstDifference(first, again);
            Assert.That(differs, Is.Null, $"Put back and stepped on, the character should move exactly as it did: {differs}.");
        }

        // ---- At another frame rate ----

        /// <summary>
        /// A run on a moving platform (which needs Unity's own physics steps, so the frames between them are real) is the
        /// same at another frame rate: up to a few physics steps a render frame, or about one. Nothing in the movement reads
        /// the frame's time.
        /// </summary>
        [UnityTest]
        public IEnumerator ARun_AtAnotherFrameRate_IsTheSame()
        {
            var runs = new List<Pose>[2];
            FrameTimeLimit slower = null;
            try
            {
                for (int run = 0; run < 2; run++)
                {
                    if (run > 0)
                    {
                        yield return ReplaceRig();
                        // About one physics step a frame, rather than the rig's up to MaxFrameTime of them.
                        foreach (FrameTimeLimit limit in Object.FindObjectsByType<FrameTimeLimit>()) { Object.Destroy(limit.gameObject); }
                        slower = FrameTimeLimit.Start(CharacterTestRig.TimeScale, Time.fixedDeltaTime * 0.9f);
                    }
                    yield return SetUp(Ability.OnAMovingPlatform);
                    runs[run] = new List<Pose>();
                    yield return Record(60, step => AbilityScript(Ability.OnAMovingPlatform, step), runs[run]);
                }
            }
            finally
            {
                if (slower != null) { Object.Destroy(slower.gameObject); }
            }
            string differs = FirstDifference(runs[0], runs[1]);
            Assert.That(differs, Is.Null, $"At another frame rate, the run should be the same: {differs}.");
        }
    }
}
