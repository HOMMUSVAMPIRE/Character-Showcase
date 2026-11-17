using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Character;
using Level;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CharacterShowcase.Tests
{
    /// <summary>
    /// Golden paths: the character's exact movement, step by step, through scripted runs over every part of its movement
    /// (walking, jumping, stairs and ramps, crouching, sliding, rolling, dashing, ledges, vaulting, wall running and
    /// climbing, ladders, bounce pads, ziplines, ropes, swimming, diving and wading, moving platforms, and other sizes),
    /// checked against the paths recorded in GoldenPaths/. They're the safety net for changes meant to leave the movement
    /// as it is (a refactor): any change to where the character is, how it faces and moves, what state it's in or what
    /// happens to it, at any step, fails, naming the first step and what differs.
    ///
    /// The runs use their own tuning, recorded with the paths (GoldenPaths/Tuning.json: every movement component's
    /// settings, and the MovementController's and its collider's), not the movement asset's, so retuning the asset
    /// doesn't fail them. When a change to the movement is meant to change the paths, record them again (all of them at
    /// once, so they share one tuning): run this fixture with the environment variable CHARACTER_SHOWCASE_RECORD_GOLDEN_PATHS=1
    /// (see docs/testing.md). Each run must also visit the states and raise the events it's there for, recording or
    /// checking, so a path that no longer exercises what it should is caught too.
    /// </summary>
    [Category(TestArea.Movement)]
    public class GoldenPathTests : CharacterRigFixture
    {
        /// <summary>The environment variable that, set to 1, records the paths (and their tuning) instead of checking them.</summary>
        public const string RecordVariable = "CHARACTER_SHOWCASE_RECORD_GOLDEN_PATHS";

        // How far a step may differ from the recorded one and still count as the same: well under anything visible, well
        // over floating-point noise.
        private const float PositionTolerance = 1e-3f, VelocityTolerance = 1e-2f, YawTolerance = 0.05f, HeightTolerance = 1e-3f;

        private static string Folder => Path.Combine(Application.dataPath, "Tests", "PlayMode", "GoldenPaths");
        private static string TuningPath => Path.Combine(Folder, "Tuning.json");
        // Where a path that differs is written, to compare with the recorded one (not Temp, which Unity clears as it quits).
        private static string ActualFolder => Path.Combine(Application.dataPath, "..", "Logs", "GoldenPaths");
        private static bool Recording => Environment.GetEnvironmentVariable(RecordVariable) == "1";

        // ---- The runs ----

        /// <summary>A scripted run: the world round the character, where it starts, and its inputs, phase by phase.</summary>
        private sealed class Scenario
        {
            public string name;
            /// <summary>Builds the world round the character (spawned, at its pinned size), returning where its feet start.</summary>
            public Func<CharacterTestRig, Vector3> build;
            /// <summary>The character's velocity as the run starts (from a launch), or zero.</summary>
            public Func<CharacterTestRig, Vector3> launch;
            /// <summary>The character's size (see ACCharacterController.SetScale).</summary>
            public float scale = 1f;
            /// <summary>The inputs: each phase's length (s) and its inputs, by the step within it.</summary>
            public (float seconds, Func<int, Inputs> inputs)[] phases;
            /// <summary>The states the run is there to exercise: it must be in each at some step.</summary>
            public CharacterState[] states = Array.Empty<CharacterState>();
            /// <summary>The events it's there to raise.</summary>
            public MovementEvents[] events = Array.Empty<MovementEvents>();
        }

        private static readonly Vector3 Forward = Vector3.forward, Right = Vector3.right;
        private static Inputs Idle(int _) => CharacterTestRig.Idle();
        private static Func<int, Inputs> Walk(Vector3 direction) => _ => CharacterTestRig.Move(direction);
        private static Func<int, Inputs> Sprint(Vector3 direction) => step => CharacterTestRig.Sprint(direction, step + 1);
        // A press: the button down for the phase's steps (from its first), with the given movement.
        private static Func<int, Inputs> Jump(Vector3 direction = default) => step => CharacterTestRig.Jump(step, direction);
        private static Func<int, Inputs> Crouch(Vector3 direction = default) => step => CharacterTestRig.Crouch(step, direction);
        private static Func<int, Inputs> Dash(Vector3 direction = default) => step => CharacterTestRig.Dash(step, direction);
        private static Func<int, Inputs> Roll(Vector3 direction = default) => step => CharacterTestRig.Roll(step, direction);
        private static Func<int, Inputs> SprintAndCrouch(Vector3 direction) => step =>
        {
            Inputs inputs = CharacterTestRig.Crouch(step, direction);
            inputs.sprint = CharacterTestRig.Held(step + 10);
            return inputs;
        };
        // One physics step, at the project's step (a press lasting a single step).
        private const float OneStep = 0.02f;

        private static Scenario[] scenarios;

        private static Scenario[] Scenarios => scenarios ??= new[]
        {
            new Scenario
            {
                name = "WalkSprintTurn",
                build = rig => { rig.Floor(); return new Vector3(0f, 0.05f, 0f); },
                phases = new (float, Func<int, Inputs>)[] { (0.3f, Idle), (1.5f, Walk(Forward)), (1f, Walk(Right)), (1.5f, Sprint((Forward + Right).normalized)), (1f, Idle) },
                states = new[] { CharacterState.Moving },
            },
            new Scenario
            {
                name = "Jumps",
                build = rig => { rig.Floor(); return new Vector3(0f, 0.05f, 0f); },
                phases = new (float, Func<int, Inputs>)[]
                {
                    (0.3f, Idle), (2 * OneStep, Jump()), (1f, Idle),       // A tap.
                    (0.5f, Jump(Forward)), (0.8f, Walk(Forward)),           // Held, moving.
                    (OneStep, Jump(Forward)), (OneStep, Walk(Forward)),     // And again, with a press in
                    (OneStep, Jump(Forward)), (1.5f, Walk(Forward)),        // the air (as MovementEventTests).
                },
                states = new[] { CharacterState.Jumping },
                events = new[] { MovementEvents.Jumped, MovementEvents.AirJumped, MovementEvents.Landed },
            },
            new Scenario
            {
                name = "StairsAndRamps",
                build = rig =>
                {
                    rig.Floor();
                    const float rise = 0.25f, run = 0.5f;
                    for (int i = 0; i < 8; i++)
                    {
                        rig.Box($"Step {i}", new Vector3(0f, (i + 1) * rise / 2f, 3f + i * run + run / 2f), new Vector3(4f, (i + 1) * rise, run));
                    }
                    rig.Box("Landing", new Vector3(0f, 1f, 9.5f), new Vector3(4f, 2f, 5f));
                    Ramp(rig, new Vector3(0f, 2f, 12f), new Vector3(0f, 0f, 17.5f), 4f);
                    return new Vector3(0f, 0.05f, 0f);
                },
                phases = new (float, Func<int, Inputs>)[] { (0.3f, Idle), (5f, Walk(Forward)), (1f, Idle) },
                states = new[] { CharacterState.Grounded },
            },
            new Scenario
            {
                name = "CrouchSlideRoll",
                build = rig =>
                {
                    rig.Floor();
                    // A tunnel too low to stand in, high enough to crouch in.
                    float clear = Mathf.Lerp(rig.CrouchHeight, 1f, 0.5f) * rig.StandingHeight;
                    rig.Box("Tunnel Ceiling", new Vector3(0f, clear + 0.5f, 6f), new Vector3(4f, 1f, 4f));
                    return new Vector3(0f, 0.05f, 0f);
                },
                phases = new (float, Func<int, Inputs>)[]
                {
                    (0.3f, Idle), (OneStep, Crouch()), (0.2f, Idle), (4.5f, Walk(Forward)),  // Crouched, through the tunnel.
                    (OneStep, Crouch()), (0.5f, Idle),                                       // Stood up.
                    (1.2f, Sprint(Forward)), (2 * OneStep, SprintAndCrouch(Forward)), (1f, Sprint(Forward)), // A slide.
                    (OneStep, Crouch()), (0.8f, Idle), (2 * OneStep, Roll(Forward)), (1.5f, Walk(Forward)), // A roll.
                },
                states = new[] { CharacterState.Crouching, CharacterState.Sliding, CharacterState.Rolling },
            },
            new Scenario
            {
                name = "Dashes",
                build = rig => { rig.Floor(); return new Vector3(0f, 0.05f, 0f); },
                phases = new (float, Func<int, Inputs>)[]
                {
                    (0.3f, Idle), (OneStep, Dash(Forward)), (0.8f, Walk(Forward)),
                    (2 * OneStep, Jump(Forward)), (0.15f, Walk(Forward)), (OneStep, Dash(Forward)), (1.5f, Walk(Forward)),
                },
                states = new[] { CharacterState.Dashing },
                events = new[] { MovementEvents.DashStarted, MovementEvents.DashEnded },
            },
            new Scenario
            {
                name = "LedgeHangAndClimb",
                build = rig =>
                {
                    rig.Floor();
                    // As LedgeTests: in mid-air before a block whose top is a standing height above the feet.
                    const float feet = 3f;
                    float top = feet + rig.StandingHeight, face = rig.FrontExtent + 0.15f;
                    rig.Box("Ledge Block", new Vector3(0f, top / 2f, face + 2f), new Vector3(10f, top, 4f));
                    return new Vector3(0f, feet, 0f);
                },
                phases = new (float, Func<int, Inputs>)[] { (0.6f, Walk(Forward)), (2 * OneStep, Jump()), (1.5f, Walk(Forward)) },
                states = new[] { CharacterState.LedgeHanging, CharacterState.LedgeClimbing },
            },
            new Scenario
            {
                name = "Vault",
                build = rig =>
                {
                    rig.Floor();
                    float top = Mathf.Lerp(rig.StepHeight, rig.MovementSetting<float>("ledgeGrabMinHeight") * rig.StandingHeight, 0.5f);
                    rig.Box("Low Wall", new Vector3(0f, top / 2f, 3.2f), new Vector3(10f, top, 0.4f));
                    return new Vector3(0f, 0.05f, 0f);
                },
                phases = new (float, Func<int, Inputs>)[] { (0.3f, Idle), (2.5f, Sprint(Forward)), (0.5f, Idle) },
                states = new[] { CharacterState.Vaulting },
            },
            new Scenario
            {
                name = "WallRunAndJump",
                build = rig =>
                {
                    rig.Floor();
                    // As WallRunTests: in mid-air beside a long wall, launched along it.
                    float face = rig.FrontExtent + 0.1f;
                    rig.Box("Wall", new Vector3(face + 0.5f, 20f, 5f), new Vector3(1f, 40f, 60f));
                    return new Vector3(0f, 12f, -25f);
                },
                launch = rig => Forward * (rig.Component<WallRunning>().WallRunSpeed * rig.Movement.SpeedScale),
                phases = new (float, Func<int, Inputs>)[] { (0.6f, Walk(Forward)), (2 * OneStep, Jump(Forward)), (3f, Walk(Forward)) },
                states = new[] { CharacterState.WallRunning },
                events = new[] { MovementEvents.WallJumped },
            },
            new Scenario
            {
                name = "WallClimbOverTheTop",
                build = rig =>
                {
                    rig.Floor();
                    float face = rig.FrontExtent + 0.3f;
                    rig.ClimbableBox("Wall", new Vector3(0f, 1.5f, face + 1f), new Vector3(10f, 3f, 2f));
                    return new Vector3(0f, 0.05f, 0f);
                },
                phases = new (float, Func<int, Inputs>)[] { (0.3f, Idle), (4f, Walk(Forward)) },
                states = new[] { CharacterState.WallClimbing, CharacterState.LedgeClimbing },
            },
            new Scenario
            {
                name = "LadderToAPlatform",
                build = rig =>
                {
                    rig.Floor();
                    // As LadderTests: a ladder up the face of a platform, climbed off onto it.
                    const float height = 4f;
                    float face = rig.FrontExtent + 0.3f;
                    rig.Ladder(new Vector3(0f, 0f, face + 0.03f), height);
                    rig.Box("Platform", new Vector3(0f, height / 2f, face + 0.06f + 15f), new Vector3(6f, height, 30f));
                    return new Vector3(0f, 0.05f, 0f);
                },
                phases = new (float, Func<int, Inputs>)[] { (0.3f, Idle), (5f, Walk(Forward)) },
                states = new[] { CharacterState.WallClimbing },
            },
            new Scenario
            {
                name = "BouncePad",
                build = rig =>
                {
                    rig.Floor();
                    rig.BouncePad(new Vector3(0f, 0.3f, 3f), new Vector3(2f, 0.3f, 2f), 5f);
                    return new Vector3(0f, 0.05f, 0f);
                },
                phases = new (float, Func<int, Inputs>)[] { (0.3f, Idle), (0.6f, Walk(Forward)), (2.5f, Idle) },
                events = new[] { MovementEvents.Bounced },
            },
            new Scenario
            {
                name = "Zipline",
                build = rig =>
                {
                    rig.Floor();
                    // As ZiplineTests: just under the cable, falling onto it.
                    Zipline line = rig.Zipline(new Vector3(0f, 8f, 0f), new Vector3(0f, 4f, 24f));
                    return line.PointAt(2f) - Vector3.up * (rig.ZiplineHangHeight - 0.15f);
                },
                phases = new (float, Func<int, Inputs>)[] { (1f, Idle), (2 * OneStep, Jump()), (2f, Idle) },
                states = new[] { CharacterState.Ziplining },
                events = new[] { MovementEvents.Jumped },
            },
            new Scenario
            {
                name = "RopeSwing",
                build = rig =>
                {
                    rig.Floor();
                    // As RopeSwingTests: launched at a rope at its grip height.
                    rig.Rope(new Vector3(0f, 10f, 0f), 8f);
                    return new Vector3(0f, 6f - rig.MovementSetting<float>("ropeGripHeight") * rig.StandingHeight, -1.5f);
                },
                launch = _ => new Vector3(0f, 0f, 6f),
                phases = new (float, Func<int, Inputs>)[] { (2f, Idle), (2 * OneStep, Jump(Forward)), (2f, Walk(Forward)) },
                states = new[] { CharacterState.RopeSwinging },
            },
            new Scenario
            {
                name = "SwimDiveAndGetOut",
                build = rig =>
                {
                    // A deep pool, its side ahead a little above the water, with somewhere to stand beyond.
                    rig.Floor(-5f);
                    rig.Water(Vector3.zero, new Vector3(40f, 5f, 40f));
                    rig.Box("Pool Side", new Vector3(0f, (0.4f - 5f) / 2f, 12f + 15f), new Vector3(40f, 5.4f, 30f));
                    return new Vector3(0f, 1f, 0f);
                },
                phases = new (float, Func<int, Inputs>)[]
                {
                    (1.5f, Idle), (1f, Walk(Forward)),                     // Fallen in, and swimming.
                    (OneStep, Crouch()), (0.8f, step => CharacterTestRig.Crouch(step + 1)), // Dived and sunk.
                    (1f, Walk(Forward)), (1.2f, step => CharacterTestRig.Jump(step)),   // Along, and back up.
                    (5f, Walk(Forward)),                                    // Out over the side.
                },
                states = new[] { CharacterState.Swimming, CharacterState.Diving, CharacterState.Vaulting },
                events = new[] { MovementEvents.SwimStarted, MovementEvents.DiveStarted, MovementEvents.DiveEnded, MovementEvents.SwimEnded },
            },
            new Scenario
            {
                name = "WadeAndVault",
                build = rig =>
                {
                    rig.Floor();
                    // Shallow water ahead, too shallow to swim in, with a low wall in it.
                    float depth = 0.5f * rig.SwimDepth;
                    rig.Water(new Vector3(0f, depth, 12f), new Vector3(20f, depth + 1f, 20f));
                    float top = Mathf.Lerp(rig.StepHeight, rig.MovementSetting<float>("ledgeGrabMinHeight") * rig.StandingHeight, 0.5f);
                    rig.Box("Low Wall", new Vector3(0f, top / 2f, 9f), new Vector3(10f, top, 0.3f));
                    return new Vector3(0f, 0.05f, 0f);
                },
                phases = new (float, Func<int, Inputs>)[] { (0.3f, Idle), (5f, Walk(Forward)) },
                states = new[] { CharacterState.Wading, CharacterState.Vaulting },
            },
            new Scenario
            {
                name = "MovingPlatform",
                build = rig =>
                {
                    rig.Floor();
                    rig.Platform(new Vector3(0f, 1f, 4f), new Vector3(4f, 0.5f, 4f), new Vector3(0f, 0f, 6f), 4f, new Vector3(0f, 20f, 0f));
                    return new Vector3(0f, 1.05f, 4f);
                },
                phases = new (float, Func<int, Inputs>)[] { (3f, Idle), (1f, Walk(Forward)), (1f, Idle) },
                states = new[] { CharacterState.Grounded },
            },
            ScaledRun("Small", 0.5f),
            ScaledRun("Large", 1.6f),
        };

        /// <summary>Walking, jumping and crouching through a tunnel at another size.</summary>
        private static Scenario ScaledRun(string name, float scale) => new()
        {
            name = "Scaled" + name,
            scale = scale,
            build = rig =>
            {
                rig.Floor();
                float clear = Mathf.Lerp(rig.CrouchHeight, 1f, 0.5f) * rig.StandingHeight;
                rig.Box("Tunnel Ceiling", new Vector3(0f, clear + 0.5f * scale, 8f * scale), new Vector3(4f * scale, scale, 4f * scale));
                return new Vector3(0f, 0.05f * scale, 0f);
            },
            phases = new (float, Func<int, Inputs>)[]
            {
                (0.3f, Idle), (1f, Walk(Forward)), (0.4f, Jump(Forward)), (1f, Walk(Forward)),
                (OneStep, Crouch(Forward)), (3f, Walk(Forward)), (OneStep, Crouch()), (0.5f, Idle),
            },
            states = new[] { CharacterState.Jumping, CharacterState.Crouching },
        };

        /// <summary>A walkable ramp, 1 m thick, its top from one point to another, width wide.</summary>
        private static void Ramp(CharacterTestRig rig, Vector3 from, Vector3 to, float width)
        {
            Vector3 along = to - from;
            Quaternion tilt = Quaternion.LookRotation(along.normalized, Vector3.up);
            rig.Box("Ramp", (from + to) / 2f - tilt * Vector3.up * 0.5f, new Vector3(width, 1f, along.magnitude), tilt);
        }

        private static IEnumerable<string> ScenarioNames() => Scenarios.Select(s => s.name);

        // ---- The test ----

        /// <summary>
        /// Each run, with the character's collider, and without it: the movement doesn't need one (see CharacterBody),
        /// so the character must follow the same path either way. Recorded with it.
        /// </summary>
        [UnityTest]
        public IEnumerator FollowsItsRecordedPath([ValueSource(nameof(ScenarioNames))] string name, [Values(true, false)] bool withCollider)
        {
            if (Recording && !withCollider) { Assert.Ignore("The paths are recorded with the character's collider (and checked without it)."); }
            Scenario scenario = Scenarios.Single(s => s.name == name);
            string pathFile = Path.Combine(Folder, name + ".csv");
            if (!Recording && (!File.Exists(pathFile) || !File.Exists(TuningPath)))
            {
                Assert.Inconclusive($"No recorded path for '{name}': record them with {RecordVariable}=1 (see docs/testing.md).");
            }

            // Spawned far above where anything's built, then given the recorded tuning (or, recording, the tuning
            // recorded), sized, its world built round it, and put where it starts.
            yield return rig.SpawnSettled(Vector3.up * 50f, settleSeconds: 0f);
            if (!withCollider) { rig.RemoveCollider(); }
            if (Recording) { WriteTuning(); } else { ApplyTuning(); }
            if (!Mathf.Approximately(scenario.scale, 1f)) { rig.Controller.SetScale(Vector3.one * scenario.scale, immediately: true); }
            Vector3 start = scenario.build(rig);
            Physics.SyncTransforms();
            rig.Teleport(start);
            if (scenario.launch != null) { rig.Movement.OverrideVelocity(scenario.launch(rig)); }

            var trace = new List<string> { Header };
            CharacterState visited = 0;
            MovementEvents raised = 0;
            int[] phaseStarts = PhaseStarts(scenario, out int steps);
            yield return rig.Run(steps, step => InputsAt(scenario, phaseStarts, step), step =>
            {
                MovementData data = rig.Movement.lastFrameMovementData;
                visited |= data.state;
                raised |= data.events;
                trace.Add(Line(step, data));
            });

            string[] missingStates = scenario.states.Where(s => (visited & s) != s).Select(s => s.ToString()).ToArray();
            string[] missingEvents = scenario.events.Where(e => (raised & e) != e).Select(e => e.ToString()).ToArray();
            Assert.That(missingStates.Concat(missingEvents), Is.Empty,
                $"'{name}' should exercise what it's there for (its geometry or inputs may need changing).");

            if (Recording)
            {
                Directory.CreateDirectory(Folder);
                File.WriteAllLines(pathFile, trace);
                Assert.Pass($"Recorded '{name}' ({steps} steps).");
            }
            Compare(withCollider ? name : name + "_WithoutCollider", File.ReadAllLines(pathFile), trace);
        }

        // ---- Phases and inputs ----

        private static int[] PhaseStarts(Scenario scenario, out int steps)
        {
            var starts = new int[scenario.phases.Length];
            steps = 0;
            for (int i = 0; i < scenario.phases.Length; i++)
            {
                starts[i] = steps;
                steps += Mathf.Max(1, CharacterTestRig.StepsFor(scenario.phases[i].seconds));
            }
            return starts;
        }

        private static Inputs InputsAt(Scenario scenario, int[] phaseStarts, int step)
        {
            int phase = phaseStarts.Length - 1;
            while (phase > 0 && phaseStarts[phase] > step) { phase--; }
            return scenario.phases[phase].inputs(step - phaseStarts[phase]);
        }

        // ---- The path, step by step ----

        private const string Header = "step,x,y,z,yaw,vx,vy,vz,state,events,height";

        private string Line(int step, MovementData data)
        {
            Vector3 p = rig.Position, v = data.finalVelocity;
            float yaw = rig.Body.rotation.eulerAngles.y;
            return string.Join(",", step.ToString(CultureInfo.InvariantCulture), F(p.x), F(p.y), F(p.z), F(yaw), F(v.x), F(v.y), F(v.z),
                ((int)data.state).ToString(CultureInfo.InvariantCulture), ((long)data.events).ToString(CultureInfo.InvariantCulture),
                F(rig.CurrentHeight / rig.StandingHeight));
        }

        private static string F(float value) => value.ToString("0.#####", CultureInfo.InvariantCulture);

        private static void Compare(string name, string[] expected, List<string> actual)
        {
            string failure = FirstDifference(expected, actual);
            if (failure == null) { return; }
            Directory.CreateDirectory(ActualFolder);
            string actualFile = Path.GetFullPath(Path.Combine(ActualFolder, name + ".csv"));
            File.WriteAllLines(actualFile, actual);
            Assert.Fail($"'{name}' left its recorded path: {failure}\nThis run's path: {actualFile}\n" +
                        $"If the change is meant to change the movement, record the paths again with {RecordVariable}=1 (see docs/testing.md).");
        }

        private static string FirstDifference(string[] expected, List<string> actual)
        {
            if (expected.Length == 0 || expected[0] != Header) { return "the recorded path is in an old format (record it again)."; }
            for (int i = 1; i < Math.Min(expected.Length, actual.Count); i++)
            {
                string[] e = expected[i].Split(','), a = actual[i].Split(',');
                string field = DifferentField(e, a);
                if (field != null)
                {
                    return $"at step {e[0]}, {field} differs.\n  Recorded: {expected[i]}\n  This run: {actual[i]}\n  ({Header})";
                }
            }
            return expected.Length != actual.Count ? $"it has {actual.Count - 1} steps; {expected.Length - 1} were recorded." : null;
        }

        private static string DifferentField(string[] e, string[] a)
        {
            float N(string[] s, int i) => float.Parse(s[i], CultureInfo.InvariantCulture);
            for (int i = 1; i <= 3; i++) { if (Mathf.Abs(N(e, i) - N(a, i)) > PositionTolerance) { return "the position"; } }
            if (Mathf.Abs(Mathf.DeltaAngle(N(e, 4), N(a, 4))) > YawTolerance) { return "the facing"; }
            for (int i = 5; i <= 7; i++) { if (Mathf.Abs(N(e, i) - N(a, i)) > VelocityTolerance) { return "the velocity"; } }
            if (e[8] != a[8]) { return $"the state ({(CharacterState)int.Parse(e[8])} recorded, {(CharacterState)int.Parse(a[8])} now)"; }
            if (e[9] != a[9]) { return $"the events ({(MovementEvents)long.Parse(e[9])} recorded, {(MovementEvents)long.Parse(a[9])} now)"; }
            if (Mathf.Abs(N(e, 10) - N(a, 10)) > HeightTolerance) { return "the collider's height"; }
            return null;
        }

        // ---- The tuning the paths are recorded with ----

        // The MovementController's settings that shape the movement (by field name: a field renamed or moved fails the
        // tests loudly, as the harness must follow it).
        private static readonly string[] ControllerSettings =
        {
            "maxStepHeight", "maxSlopeAngle", "skinWidth", "groundSnapDistance", "groundCheckRadius", "movementSpeedMultiplier",
            "jumpStrengthModifier", "sizeScaling", "moveWithGround", "rotateWithGround", "inheritGroundVelocity", "maxGroundCarryPerStep",
        };

        [Serializable] private sealed class PinnedTuning
        {
            public List<PinnedComponent> components = new();
            public List<PinnedSetting> controller = new();
            public float colliderRadius, colliderHeight;
            // Its lying thickness (see BodyShape): 0 lies it down as thick as it is wide.
            public float colliderLyingThickness;
        }

        [Serializable] private sealed class PinnedComponent { public string type; public string settings; }
        [Serializable] private sealed class PinnedSetting { public string field; public string value; }

        private static FieldInfo ControllerField(string name)
        {
            FieldInfo field = typeof(MovementController).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (field == null)
            {
                Assert.Fail($"The golden paths pin the MovementController's '{name}', which no longer exists: update GoldenPathTests.ControllerSettings.");
            }
            return field;
        }

        /// <summary>Records the tuning the paths are recorded with: the rig's copy of the mode, the controller and its collider.</summary>
        private void WriteTuning()
        {
            var pinned = new PinnedTuning
            {
                colliderRadius = rig.Movement.AuthoredBody.radius,
                colliderHeight = rig.Movement.AuthoredBody.height,
                colliderLyingThickness = rig.Movement.AuthoredBody.lyingThickness,
            };
            foreach (MovementComponent component in rig.ComposedMode.Components)
            {
                if (component == null) { continue; }
                pinned.components.Add(new PinnedComponent { type = component.GetType().FullName, settings = JsonUtility.ToJson(component) });
            }
            foreach (string name in ControllerSettings)
            {
                object value = ControllerField(name).GetValue(rig.Movement);
                pinned.controller.Add(new PinnedSetting { field = name, value = Convert.ToString(value, CultureInfo.InvariantCulture) });
            }
            Directory.CreateDirectory(Folder);
            File.WriteAllText(TuningPath, JsonUtility.ToJson(pinned, prettyPrint: true));
        }

        /// <summary>Gives the rig the tuning the paths were recorded with.</summary>
        private void ApplyTuning()
        {
            var pinned = JsonUtility.FromJson<PinnedTuning>(File.ReadAllText(TuningPath));
            ComposedMovementMode mode = rig.ComposedMode;
            var pinnedTypes = new HashSet<string>();
            foreach (PinnedComponent part in pinned.components)
            {
                pinnedTypes.Add(part.type);
                Type type = typeof(MovementComponent).Assembly.GetType(part.type);
                MovementComponent component = type != null ? mode.Get(type, includeDisabled: true) : null;
                if (component == null)
                {
                    Assert.Fail($"The golden paths were recorded with a {part.type} the movement mode no longer has: record them again.");
                }
                JsonUtility.FromJsonOverwrite(part.settings, component);
            }
            string[] unpinned = mode.Components.Where(c => c != null && !pinnedTypes.Contains(c.GetType().FullName)).Select(c => c.GetType().Name).ToArray();
            if (unpinned.Length > 0)
            {
                Assert.Fail($"The movement mode has components the golden paths weren't recorded with ({string.Join(", ", unpinned)}): record them again.");
            }
            foreach (PinnedSetting setting in pinned.controller)
            {
                FieldInfo field = ControllerField(setting.field);
                object value = field.FieldType.IsEnum ? Enum.Parse(field.FieldType, setting.value)
                    : Convert.ChangeType(setting.value, field.FieldType, CultureInfo.InvariantCulture);
                field.SetValue(rig.Movement, value);
            }
            Assert.That(rig.Movement.SetBody(BodyShape.Capsule(pinned.colliderRadius, pinned.colliderHeight, pinned.colliderLyingThickness)), Is.True,
                "The golden paths' collider should fit where the character is spawned.");
        }
    }
}
