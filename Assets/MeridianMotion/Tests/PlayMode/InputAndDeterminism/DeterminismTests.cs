using System;
using System.Collections;
using System.Collections.Generic;
using MeridianMotion.Characters;
using NUnit.Framework;
using UnityEngine.TestTools;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MeridianMotion.Tests.InputAndDeterminism
{
    /// <summary>
    /// Tests that the movement is deterministic on one build: the same state, input frames and world give the same steps,
    /// bit for bit (pose and velocity), whatever else differs. A long session times things exactly as a fresh one (the
    /// character's tick clock); the same scene built in another order runs the same (stable choices between equally near
    /// floors, walls and ropes); a character saved mid-ability and put back (MMotionMotor.SaveState, RestoreState),
    /// the level's clock with it, steps on exactly as it did, and so does a new character its state's written to bytes and
    /// read into (StateSerializer); and a run whose world needs Unity's physics steps (a moving
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

        private IEnumerator Record(int steps, Func<int, StepInputs> script, List<Pose> into, CharacterState? mustDo = null)
        {
            CharacterState did = CharacterState.None;
            yield return rig.Run(steps, script, _ =>
            {
                into.Add(new Pose(rig));
                did |= rig.Movement.lastStep.state;
            });
            if (mustDo is { } wanted) { Assume.That(did & wanted, Is.EqualTo(wanted), $"Test setup: the run should do {wanted} (it did {did})."); }
        }

        // ---- A long session ----

        /// <summary>
        /// A character whose clock starts three billion steps in (some 700 days at 50 a second) walks off a ledge, jumps
        /// in its coyote time, dashes and lands exactly as one starting at 0: it times everything in whole steps
        /// (MMotionMotor.StepIndex, SecondsSince), so nothing drifts or loses its fractions as the clock grows.
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
                StepInputs Script(int step)
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

        private static StepInputs SceneScript(Scene scene, int step) => scene switch
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

        public enum Ability { Jumping, WallClimbing, Ziplining, OnAMovingPlatform, ChangingSize, RopeSwinging, Pushing }

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
                    MeridianMotion.Levels.ZiplineCable line = rig.Zipline(new Vector3(0f, 8f, 0f), new Vector3(0f, 4f, 24f));
                    yield return rig.SpawnSettled(Vector3.zero, settleSeconds: 0f);
                    rig.Teleport(line.PointAt(2f) - Vector3.up * (rig.ZiplineHangHeight - 0.15f));
                    break;
                case Ability.OnAMovingPlatform:
                    rig.Platform(Vector3.zero, new Vector3(4f, 0.3f, 4f), new Vector3(2f, 1f, 0f), 3f, new Vector3(0f, 20f, 0f));
                    yield return rig.SpawnSettled(new Vector3(0f, 0.05f, 0f));
                    break;
                case Ability.RopeSwinging:
                    rig.Floor();
                    rig.Rope(new Vector3(0f, 4.5f, 2f), 3f);
                    yield return rig.SpawnSettled(new Vector3(0f, 0.05f, 0f));
                    break;
                case Ability.Pushing:
                    rig.Floor();
                    yield return rig.SpawnSettled(new Vector3(0f, 0.05f, 0f));
                    // As tall as PushPullTests' (too tall to vault), just ahead.
                    rig.PushableBox(new Vector3(0f, 0.75f, rig.Position.z + rig.FrontExtent + 0.1f + 0.75f), Vector3.one * 1.5f, 1f, true);
                    break;
                default:
                    rig.Floor();
                    yield return rig.SpawnSettled(new Vector3(0f, 0.05f, 0f));
                    break;
            }
            Physics.SyncTransforms();
        }

        private StepInputs AbilityScript(Ability ability, int step)
        {
            switch (ability)
            {
                // A jump every 35 steps, so one's under way, or starts, after either save.
                case Ability.Jumping: return step % 35 >= 5 && step % 35 < 15 ? CharacterTestRig.Jump(Vector3.forward) : CharacterTestRig.Move(Vector3.forward);
                case Ability.WallClimbing: return CharacterTestRig.Move(Vector3.forward);
                case Ability.Ziplining: return CharacterTestRig.Idle();
                case Ability.OnAMovingPlatform: return CharacterTestRig.Move(step < 20 ? Vector3.right * 0.3f : Vector3.zero);
                // Into the rope, then hanging on and swinging: it swings by itself, so its own state matters.
                case Ability.RopeSwinging: return step >= 10 && step < 22 ? CharacterTestRig.Jump(Vector3.forward) : CharacterTestRig.Move(step < 10 ? Vector3.forward : Vector3.zero);
                // Taking hold of the box and pushing it, then pulling it back.
                case Ability.Pushing: return CharacterTestRig.Interact(step < 50 ? Vector3.forward : Vector3.back);
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
            Ability.RopeSwinging => CharacterState.RopeSwinging,
            Ability.Pushing => CharacterState.Grabbing,
            _ => null,
        };

        /// <summary>
        /// A character saved partway through (mid-jump, mid-climb, riding a zipline, on a moving platform, changing size,
        /// swinging on a rope, pushing a box) and put back, the level with it (its ropes' and pushables' own state:
        /// WorldStateSnapshot; its clock), then given the same frames again, steps on exactly as it did from there: the
        /// snapshots hold everything a step reads or changes. The save is between steps, at a few ticks.
        /// </summary>
        [UnityTest]
        public IEnumerator SavedMidAbility_AndPutBack_StepsOnExactlyAsItDid([Values] Ability ability, [Values(12, 37)] int savedAt)
        {
            yield return SetUp(ability);
            int at = savedAt;
            var before = new List<Pose>();
            yield return Record(at, step => AbilityScript(ability, step), before);
            MovementStateSnapshot saved = rig.Movement.SaveState();
            WorldStateSnapshot world = WorldStateSnapshot.Save();
            long levelTick = rig.LevelTick;
            Assert.That(saved.Summary.position, Is.EqualTo(rig.Position), "The summary should hold the character's pose.");

            var first = new List<Pose>();
            yield return Record(40, step => AbilityScript(ability, at + step), first, Doing(ability));
            Assert.That(rig.Movement.RestoreState(saved), Is.True);
            world.Restore();
            rig.LevelTick = levelTick;
            Assert.That(rig.Movement.StepIndex, Is.EqualTo(saved.StepIndex), "Put back, its clock should be where it was.");
            var again = new List<Pose>();
            yield return Record(40, step => AbilityScript(ability, at + step), again);
            string differs = FirstDifference(first, again);
            Assert.That(differs, Is.Null, $"Put back and stepped on, the character should move exactly as it did: {differs}.");
        }

        // ---- Written to bytes, and read into a new character ----

        /// <summary>
        /// A character's state and the level's, written to bytes partway through an ability (StateSerializer), read into a
        /// new character in the same level built again (the level's objects found by StableId, the character's own by
        /// their paths from it), with the level's clock set as it was, steps on exactly as the first did from there, bit
        /// for bit: what a networked game sends a client (or a player joining) to put a character where the server has it.
        /// </summary>
        [UnityTest]
        public IEnumerator WrittenToBytes_AndReadIntoANewCharacter_StepsOnExactlyAsTheFirst([Values] Ability ability)
        {
            const int at = 24;
            yield return SetUp(ability);
            yield return Record(at, step => AbilityScript(ability, step), new List<Pose>());
            byte[] character = StateSerializer.WriteCharacter(rig.Movement);
            byte[] world = StateSerializer.WriteWorld();
            long levelTick = rig.LevelTick;
            var first = new List<Pose>();
            yield return Record(40, step => AbilityScript(ability, at + step), first, Doing(ability));

            yield return ReplaceRig();
            yield return SetUp(ability);
            StateSerializer.ReadCharacter(rig.Movement, character);
            StateSerializer.ReadWorld(world);
            rig.LevelTick = levelTick;
            var again = new List<Pose>();
            yield return Record(40, step => AbilityScript(ability, at + step), again);
            string differs = FirstDifference(first, again);
            TestContext.WriteLine($"The character's state was {character.Length} bytes, the level's {world.Length}.");
            Assert.That(differs, Is.Null, $"Read into a new character, the state should step on exactly as it did: {differs}.");
        }

        /// <summary>A state of the kind an ability made in a project keeps (MMotionMotor.StateOf), for the test below.</summary>
        private sealed class ProjectAbilityState
        {
            public int count;
            public readonly List<int> history = new();
        }

        /// <summary>
        /// The state an ability made outside the package keeps on the character (MMotionMotor.StateOf: the package's own
        /// abilities' are parts of the motor) is one object per character, and saved and put back, and written to bytes
        /// and read into a new character, with the rest of its state: so such an ability is deterministic as the package's are.
        /// </summary>
        [UnityTest]
        public IEnumerator AProjectAbilitysState_IsSavedAndPutBack_AndWrittenToBytes_WithTheRest()
        {
            yield return SetUp(Ability.Jumping);
            ProjectAbilityState state = rig.Movement.StateOf<ProjectAbilityState>();
            Assert.That(rig.Movement.StateOf<ProjectAbilityState>(), Is.SameAs(state), "Asked for again, it's the same object.");
            state.count = 5;
            state.history.Add(1);
            MovementStateSnapshot saved = rig.Movement.SaveState();
            state.count = 9;
            state.history.Add(2);
            rig.Movement.RestoreState(saved);
            Assert.That(rig.Movement.StateOf<ProjectAbilityState>().count, Is.EqualTo(5), "Put back, it's as it was saved.");
            Assert.That(rig.Movement.StateOf<ProjectAbilityState>().history, Is.EqualTo(new[] { 1 }));

            byte[] bytes = StateSerializer.WriteCharacter(rig.Movement);
            yield return ReplaceRig();
            yield return SetUp(Ability.Jumping);
            StateSerializer.ReadCharacter(rig.Movement, bytes);
            ProjectAbilityState read = rig.Movement.StateOf<ProjectAbilityState>();
            Assert.That(read.count, Is.EqualTo(5), "Read into a new character (which hadn't asked for it), it's as it was written.");
            Assert.That(read.history, Is.EqualTo(new[] { 1 }));
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
