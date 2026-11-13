using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Character;
using Level;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace CharacterShowcase.EditorTools
{
    /// <summary>
    /// Turns a level's fixtures into the runtime objects they stand for, as part of a scene's game setup (see
    /// SceneGameSetup). The level builders can't reference runtime code (the level knows nothing about the game), so
    /// they build each fixture's geometry and name it, with its settings after its name as "key=value" pairs:
    /// <list type="bullet">
    /// <item>"Moving Platform (…) | travel=x,y,z | period=s | pause=s | spin=x,y,z | phase=f | smooth=0/1" gets a
    /// kinematic Rigidbody and a MovingPlatform.</item>
    /// <item>"Pushable Crate | pull=0/1 | speed=f" gets a Rigidbody and a PushableObject.</item>
    /// <item>"Timing Gate | course=name | role=start-finish/start/finish | distance=m" (a trigger) gets a TimingGate, timed
    /// by a CourseTimer for its course, made under the game setup and told who the racer is by the spawner.</item>
    /// <item>"Timing Display | course=name" (a TextMeshPro) shows that course's times.</item>
    /// <item>"Ladder | height=m | width=m | spacing=m | rail=m | rung=m" gets a Ladder that keeps the level's mesh (its
    /// rungs where the level put them). Only names starting "Ladder |": "Ladder (future): …" markers aren't ladders.</item>
    /// </list>
    /// The names' starts must match the level builders' (Graybox), which can't reference these.
    /// </summary>
    public static class LevelFixtureSetup
    {
        public const string MovingPlatformPrefix = "Moving Platform";
        public const string PushablePrefix = "Pushable Crate";
        public const string TimingGatePrefix = "Timing Gate";
        public const string TimingDisplayPrefix = "Timing Display";
        public const string LadderPrefix = "Ladder";
        public const string CoursesName = "Course Timers";

        /// <summary>Whether name is a fixture's: the prefix alone, or followed by a space ("Moving Platform (Lift) | …").</summary>
        public static bool IsNamed(string name, string prefix) =>
            name == prefix || name.StartsWith(prefix + " ", StringComparison.Ordinal);

        /// <summary>The settings after a fixture's name: its "key=value" pairs, between " | ".</summary>
        public static Dictionary<string, string> Parameters(string name)
        {
            var parameters = new Dictionary<string, string>();
            foreach (string part in name.Split('|').Skip(1))
            {
                int equals = part.IndexOf('=');
                if (equals > 0) { parameters[part[..equals].Trim()] = part[(equals + 1)..].Trim(); }
            }
            return parameters;
        }

        private static float Float(Dictionary<string, string> p, string key, float fallback) =>
            p.TryGetValue(key, out string v) && float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out float f) ? f : fallback;

        private static bool Bool(Dictionary<string, string> p, string key, bool fallback) =>
            p.TryGetValue(key, out string v) ? v == "1" || v.Equals("true", StringComparison.OrdinalIgnoreCase) : fallback;

        private static Vector3 Vector(Dictionary<string, string> p, string key, Vector3 fallback)
        {
            if (!p.TryGetValue(key, out string v)) { return fallback; }
            string[] xyz = v.Split(',');
            if (xyz.Length != 3) { return fallback; }
            var r = new float[3];
            for (int i = 0; i < 3; i++)
            {
                if (!float.TryParse(xyz[i], NumberStyles.Float, CultureInfo.InvariantCulture, out r[i])) { return fallback; }
            }
            return new Vector3(r[0], r[1], r[2]);
        }

        /// <summary>Sets a moving platform up from its name's settings (adding what it needs).</summary>
        public static MovingPlatform SetUpMovingPlatform(GameObject obj, bool withUndo)
        {
            Dictionary<string, string> p = Parameters(obj.name);
            GameObjectUtility.SetStaticEditorFlags(obj, 0);
            Need<Rigidbody>(obj, withUndo);
            MovingPlatform platform = Need<MovingPlatform>(obj, withUndo);
            if (withUndo) { Undo.RecordObject(platform, "Set Up Scene"); }
            platform.travel = Vector(p, "travel", Vector3.zero);
            platform.period = Mathf.Max(0.1f, Float(p, "period", 6f));
            platform.pauseAtEnds = Mathf.Max(0f, Float(p, "pause", 0.75f));
            platform.spinDegreesPerSecond = Vector(p, "spin", Vector3.zero);
            platform.phaseOffset = Mathf.Clamp01(Float(p, "phase", 0f));
            platform.smooth = Bool(p, "smooth", true);
            return platform;
        }

        /// <summary>Sets a pushable crate up from its name's settings (adding what it needs).</summary>
        public static PushableObject SetUpPushable(GameObject obj, bool withUndo)
        {
            Dictionary<string, string> p = Parameters(obj.name);
            GameObjectUtility.SetStaticEditorFlags(obj, 0);
            Need<Rigidbody>(obj, withUndo);
            PushableObject pushable = Need<PushableObject>(obj, withUndo);
            if (withUndo) { Undo.RecordObject(pushable, "Set Up Scene"); }
            pushable.CanBePulled = Bool(p, "pull", true);
            pushable.SpeedMultiplier = Float(p, "speed", 1f);
            return pushable;
        }

        /// <summary>Whether name is a ladder's: "Ladder | …" (not a "Ladder (future)" marker).</summary>
        public static bool IsLadder(string name) => name.StartsWith(LadderPrefix + " |", StringComparison.Ordinal);

        /// <summary>Sets a ladder up from its name's settings (adding what it needs), keeping the level's mesh.</summary>
        public static Ladder SetUpLadder(GameObject obj, bool withUndo)
        {
            Dictionary<string, string> p = Parameters(obj.name);
            Ladder ladder = Need<Ladder>(obj, withUndo);
            if (withUndo) { Undo.RecordObject(ladder, "Set Up Scene"); }
            ladder.BuildsMesh = false;
            ladder.Height = Float(p, "height", 4f);
            ladder.Width = Float(p, "width", 0.6f);
            ladder.RungSpacing = Float(p, "spacing", 0.3f);
            ladder.Depth = Float(p, "rail", 0.06f);
            ladder.RungSize = Float(p, "rung", 0.035f);
            return ladder;
        }

        /// <summary>The role a timing gate's settings give it.</summary>
        public static TimingGateRole Role(Dictionary<string, string> p) =>
            p.TryGetValue("role", out string role) ? role switch { "start" => TimingGateRole.Start, "finish" => TimingGateRole.Finish, _ => TimingGateRole.StartAndFinish }
                : TimingGateRole.StartAndFinish;

        /// <summary>
        /// Sets up the scene's fixtures (outside its game setup, under which course timers are made), the spawner telling
        /// the timers who's racing. Returns what it set up, for the log ("" if nothing).
        /// </summary>
        public static string SetUp(Scene scene, Transform setup, CharacterSpawner spawner, bool withUndo)
        {
            List<Transform> all = scene.GetRootGameObjects().Where(root => root.transform != setup)
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToList();

            int platforms = 0, pushables = 0;
            foreach (Transform t in all.Where(t => IsNamed(t.name, MovingPlatformPrefix))) { SetUpMovingPlatform(t.gameObject, withUndo); platforms++; }
            foreach (Transform t in all.Where(t => IsNamed(t.name, PushablePrefix))) { SetUpPushable(t.gameObject, withUndo); pushables++; }
            int ladders = 0;
            foreach (Transform t in all.Where(t => IsLadder(t.name))) { SetUpLadder(t.gameObject, withUndo); ladders++; }

            // A timer for each course its gates name, under the game setup.
            var timers = new Dictionary<string, CourseTimer>();
            CourseTimer TimerFor(string course)
            {
                if (timers.TryGetValue(course, out CourseTimer existing)) { return existing; }
                Transform group = setup.Find(CoursesName);
                if (group == null)
                {
                    group = new GameObject(CoursesName).transform;
                    group.SetParent(setup, false);
                }
                var timerObject = new GameObject($"Course Timer ({course})");
                timerObject.transform.SetParent(group, false);
                var timer = timerObject.AddComponent<CourseTimer>();
                timer.CourseName = course;
                if (spawner != null) { UnityEventTools.AddPersistentListener(spawner.OnCharacterSpawned, timer.SetRacer); }
                return timers[course] = timer;
            }
            int gates = 0;
            foreach (Transform t in all.Where(t => IsNamed(t.name, TimingGatePrefix)))
            {
                Dictionary<string, string> p = Parameters(t.name);
                CourseTimer timer = TimerFor(p.TryGetValue("course", out string course) ? course : "Lap");
                timer.Distance = Mathf.Max(timer.Distance, Float(p, "distance", 0f));
                if (t.GetComponent<Collider>() == null) { Debug.LogWarning($"[{nameof(LevelFixtureSetup)}] '{t.name}' has no collider to be a gate: skipped."); continue; }
                TimingGate gate = Need<TimingGate>(t.gameObject, withUndo);
                if (withUndo) { Undo.RecordObject(gate, "Set Up Scene"); }
                gate.Timer = timer;
                gate.Role = Role(p);
                t.GetComponent<Collider>().isTrigger = true;
                gates++;
            }
            int displays = 0;
            foreach (Transform t in all.Where(t => IsNamed(t.name, TimingDisplayPrefix)))
            {
                Dictionary<string, string> p = Parameters(t.name);
                if (!timers.TryGetValue(p.TryGetValue("course", out string course) ? course : "Lap", out CourseTimer timer)) { continue; }
                // The display's text: a component with a text property (TextMeshPro), set through its setter, so this
                // needn't reference TextMeshPro.
                foreach (Component c in t.GetComponents<Component>())
                {
                    var setter = c.GetType().GetProperty("text")?.GetSetMethod();
                    if (setter == null || setter.GetParameters()[0].ParameterType != typeof(string)) { continue; }
                    var show = (UnityAction<string>)Delegate.CreateDelegate(typeof(UnityAction<string>), c, setter);
                    UnityEventTools.AddPersistentListener(timer.OnDisplay, show);
                    displays++;
                    break;
                }
            }
            var parts = new List<string>();
            if (platforms > 0) { parts.Add($"{platforms} moving platform{(platforms == 1 ? "" : "s")}"); }
            if (pushables > 0) { parts.Add($"{pushables} pushable crate{(pushables == 1 ? "" : "s")}"); }
            if (ladders > 0) { parts.Add($"{ladders} ladder{(ladders == 1 ? "" : "s")}"); }
            if (gates > 0) { parts.Add($"{gates} timing gate{(gates == 1 ? "" : "s")} on {timers.Count} course{(timers.Count == 1 ? "" : "s")} with {displays} display{(displays == 1 ? "" : "s")}"); }
            return string.Join(", ", parts);
        }

        private static T Need<T>(GameObject obj, bool withUndo) where T : Component
        {
            T existing = obj.GetComponent<T>();
            if (existing != null) { return existing; }
            return withUndo ? Undo.AddComponent<T>(obj) : obj.AddComponent<T>();
        }
    }
}
