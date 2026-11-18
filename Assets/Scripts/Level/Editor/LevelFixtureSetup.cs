using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Level;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace Level.EditorTools
{
    /// <summary>
    /// Turns a level's fixtures into the level elements they stand for (this assembly's mechanics: moving platforms,
    /// ropes, water and the rest). A level is built as geometry named for what each part is, with its settings after the
    /// name as "key=value" pairs, and this adds the components. The level builders run it on every level they build,
    /// and Set Up Scene runs it again (it leaves what's set up as it is), for a scene built some other way:
    /// <list type="bullet">
    /// <item>"Moving Platform (…) | travel=x,y,z | period=s | pause=s | spin=x,y,z | phase=f | smooth=0/1" gets a
    /// kinematic Rigidbody and a MovingPlatform.</item>
    /// <item>"Pushable Crate | pull=0/1 | speed=f" gets a Rigidbody and a PushableObject.</item>
    /// <item>"Timing Gate | course=name | role=start-finish/start/finish | distance=m" (a trigger) gets a TimingGate, timed
    /// by a CourseTimer for its course, found or made in the scene's "Course Timers" group (CoursesName). Who's racing is
    /// the game's to say (Set Up Scene connects the spawner).</item>
    /// <item>"Timing Display | course=name" (a TextMeshPro) shows that course's times.</item>
    /// <item>"Ladder | height=m | width=m | spacing=m | rail=m | rung=m" gets a Ladder that keeps the level's mesh (its
    /// rungs where the level put them). Only names starting "Ladder |": "Ladder (future): …" markers aren't ladders.</item>
    /// <item>"Rope | length=m | thickness=m" (at its anchor) gets a Rope, which draws itself as it swings, in the level's
    /// material. Only names starting "Rope |": "Rope Anchor" placeholders and "Rope Swing (future)" markers aren't ropes.</item>
    /// <item>"Zipline | length=m | speed=m/s" (at the cable's start, its +Z along the cable) gets a Zipline that keeps
    /// the level's cable.</item>
    /// <item>"Bounce Pad | height=m | keep=f" gets a BouncePad, squashing its "Spring" child when it launches someone.</item>
    /// <item>"Climbable Panel | speed=f" gets a ClimbableSurface (all of it climbable) with that speed multiplier.</item>
    /// <item>"Water | size=x,y,z | label=name" (at its surface's centre) gets a Water of that width, depth and length that
    /// keeps the level's surface. Only names starting "Water |".</item>
    /// </list>
    /// The names' starts are the constants here, which the level builders name their fixtures by. Spawn markers
    /// (SpawnMarkerPrefix) aren't level elements: Set Up Scene turns them into the spawner's spawn points.
    /// </summary>
    public static class LevelFixtureSetup
    {
        /// <summary>How a moving platform's name starts (Graybox.MovingPlatformPrefix).</summary>
        public const string MovingPlatformPrefix = "Moving Platform";
        /// <summary>How a pushable crate's name starts (Graybox.PushablePrefix).</summary>
        public const string PushablePrefix = "Pushable Crate";
        /// <summary>How a timing gate's name starts (Graybox.TimingGatePrefix).</summary>
        public const string TimingGatePrefix = "Timing Gate";
        /// <summary>How a timing gate's sign's name starts (Graybox.TimingDisplayPrefix).</summary>
        public const string TimingDisplayPrefix = "Timing Display";
        /// <summary>
        /// How a ladder's name starts (Graybox.LadderPrefix): a ladder's is "Ladder |", not "Ladder (future)".
        /// </summary>
        public const string LadderPrefix = "Ladder";
        /// <summary>How a rope's name starts (Graybox.RopePrefix).</summary>
        public const string RopePrefix = "Rope";
        /// <summary>How a zipline's name starts (Graybox.ZiplinePrefix).</summary>
        public const string ZiplinePrefix = "Zipline";
        /// <summary>How a bounce pad's name starts (Graybox.BouncePadPrefix).</summary>
        public const string BouncePadPrefix = "Bounce Pad";
        /// <summary>How a body of water's name starts (Graybox.WaterPrefix).</summary>
        public const string WaterPrefix = "Water";
        /// <summary>The name of a bounce pad's child that squashes when it launches someone.</summary>
        public const string BouncePadSpringName = "Spring";
        /// <summary>How a climbable panel's name starts (Graybox.ClimbablePrefix).</summary>
        public const string ClimbablePrefix = "Climbable Panel";
        /// <summary>The group at the scene's root that the courses' timers are made in.</summary>
        public const string CoursesName = "Course Timers";
        /// <summary>How a spawn marker's name starts: where a character can spawn (Set Up Scene makes them spawn points).</summary>
        public const string SpawnMarkerPrefix = "Spawn Point";

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

        /// <summary>Whether name is a fixture's with settings: the prefix followed by " |" (not a placeholder or marker named for it).</summary>
        public static bool IsFixture(string name, string prefix) => name.StartsWith(prefix + " |", StringComparison.Ordinal);

        /// <summary>Sets a rope up from its name's settings (adding what it needs): it draws itself, in the level's material, as it swings.</summary>
        public static Rope SetUpRope(GameObject obj, bool withUndo)
        {
            Dictionary<string, string> p = Parameters(obj.name);
            GameObjectUtility.SetStaticEditorFlags(obj, 0);
            Rope rope = Need<Rope>(obj, withUndo);
            if (withUndo) { Undo.RecordObject(rope, "Set Up Scene"); }
            rope.Length = Float(p, "length", 8f);
            rope.Thickness = Float(p, "thickness", 0.07f);
            if (obj.TryGetComponent(out MeshRenderer renderer) && renderer.sharedMaterial != null) { rope.Material = renderer.sharedMaterial; }
            rope.BuildsMesh = true;
            return rope;
        }

        /// <summary>Sets a zipline up from its name's settings (adding what it needs), keeping the level's cable.</summary>
        public static Zipline SetUpZipline(GameObject obj, bool withUndo)
        {
            Dictionary<string, string> p = Parameters(obj.name);
            Zipline line = Need<Zipline>(obj, withUndo);
            if (withUndo) { Undo.RecordObject(line, "Set Up Scene"); }
            line.BuildsMesh = false;
            line.LocalEnd = Vector3.forward * Mathf.Max(0.1f, Float(p, "length", 10f));
            line.MaxSpeed = Float(p, "speed", 16f);
            return line;
        }

        /// <summary>Sets a bounce pad up from its name's settings (adding what it needs), its "Spring" child squashing on launches.</summary>
        public static BouncePad SetUpBouncePad(GameObject obj, bool withUndo)
        {
            Dictionary<string, string> p = Parameters(obj.name);
            BouncePad pad = Need<BouncePad>(obj, withUndo);
            if (withUndo) { Undo.RecordObject(pad, "Set Up Scene"); }
            pad.LaunchHeight = Float(p, "height", 6f);
            pad.KeepMomentum = Float(p, "keep", 1f);
            Transform spring = obj.transform.Find(BouncePadSpringName);
            if (spring != null)
            {
                GameObjectUtility.SetStaticEditorFlags(spring.gameObject, 0);
                pad.Spring = spring;
            }
            return pad;
        }

        /// <summary>
        /// Makes a body of water swimmable, from its name's settings (adding what it needs), keeping the level's surface
        /// (its mesh and material).
        /// </summary>
        public static Water SetUpWater(GameObject obj, bool withUndo)
        {
            Dictionary<string, string> p = Parameters(obj.name);
            Water water = Need<Water>(obj, withUndo);
            if (withUndo) { Undo.RecordObject(water, "Set Up Scene"); }
            water.BuildsMesh = false;
            water.Size = Vector(p, "size", new Vector3(10f, 3f, 10f));
            return water;
        }

        /// <summary>Makes a climbable panel climbable, from its name's settings (adding what it needs).</summary>
        public static ClimbableSurface SetUpClimbable(GameObject obj, bool withUndo)
        {
            Dictionary<string, string> p = Parameters(obj.name);
            ClimbableSurface surface = Need<ClimbableSurface>(obj, withUndo);
            if (withUndo) { Undo.RecordObject(surface, "Set Up Scene"); }
            surface.SpeedMultiplier = Float(p, "speed", 1f);
            return surface;
        }

        /// <summary>The role a timing gate's settings give it.</summary>
        public static TimingGateRole Role(Dictionary<string, string> p) =>
            p.TryGetValue("role", out string role) ? role switch { "start" => TimingGateRole.Start, "finish" => TimingGateRole.Finish, _ => TimingGateRole.StartAndFinish }
                : TimingGateRole.StartAndFinish;

        /// <summary>
        /// Sets up the scene's fixtures (what's set up already is left as it is: its components kept, a course's timer
        /// found again), its courses' timers in its Course Timers group. Returns what it set up, for the log ("" if
        /// nothing).
        /// </summary>
        public static string SetUp(Scene scene, bool withUndo)
        {
            List<Transform> all = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToList();

            int platforms = 0, pushables = 0;
            foreach (Transform t in all.Where(t => IsNamed(t.name, MovingPlatformPrefix))) { SetUpMovingPlatform(t.gameObject, withUndo); platforms++; }
            foreach (Transform t in all.Where(t => IsNamed(t.name, PushablePrefix))) { SetUpPushable(t.gameObject, withUndo); pushables++; }
            int ladders = 0;
            foreach (Transform t in all.Where(t => IsLadder(t.name))) { SetUpLadder(t.gameObject, withUndo); ladders++; }
            int ropes = 0, ziplines = 0, pads = 0, climbables = 0, waters = 0;
            foreach (Transform t in all.Where(t => IsFixture(t.name, ClimbablePrefix))) { SetUpClimbable(t.gameObject, withUndo); climbables++; }
            foreach (Transform t in all.Where(t => IsFixture(t.name, RopePrefix))) { SetUpRope(t.gameObject, withUndo); ropes++; }
            foreach (Transform t in all.Where(t => IsFixture(t.name, ZiplinePrefix))) { SetUpZipline(t.gameObject, withUndo); ziplines++; }
            foreach (Transform t in all.Where(t => IsFixture(t.name, BouncePadPrefix))) { SetUpBouncePad(t.gameObject, withUndo); pads++; }
            foreach (Transform t in all.Where(t => IsFixture(t.name, WaterPrefix))) { SetUpWater(t.gameObject, withUndo); waters++; }

            // A timer for each course its gates name: the scene's own, if it has one for the course already, or one made in
            // its Course Timers group.
            var timers = new Dictionary<string, CourseTimer>();
            foreach (CourseTimer found in all.Select(t => t.GetComponent<CourseTimer>()).Where(c => c != null))
            {
                if (!timers.ContainsKey(found.CourseName)) { timers[found.CourseName] = found; }
            }
            CourseTimer TimerFor(string course)
            {
                if (timers.TryGetValue(course, out CourseTimer existing)) { return existing; }
                GameObject group = scene.GetRootGameObjects().FirstOrDefault(root => root.name == CoursesName);
                if (group == null)
                {
                    group = new GameObject(CoursesName);
                    SceneManager.MoveGameObjectToScene(group, scene);
                    if (withUndo) { Undo.RegisterCreatedObjectUndo(group, "Set Up Fixtures"); }
                }
                var timerObject = new GameObject($"Course Timer ({course})");
                timerObject.transform.SetParent(group.transform, false);
                if (withUndo) { Undo.RegisterCreatedObjectUndo(timerObject, "Set Up Fixtures"); }
                var timer = timerObject.AddComponent<CourseTimer>();
                timer.CourseName = course;
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
                    // Wired already (the level set up before): left as it is, not wired twice.
                    if (!ShowsOn(timer.OnDisplay, c, setter.Name))
                    {
                        var show = (UnityAction<string>)Delegate.CreateDelegate(typeof(UnityAction<string>), c, setter);
                        UnityEventTools.AddPersistentListener(timer.OnDisplay, show);
                    }
                    displays++;
                    break;
                }
            }
            var parts = new List<string>();
            if (platforms > 0) { parts.Add($"{platforms} moving platform{(platforms == 1 ? "" : "s")}"); }
            if (pushables > 0) { parts.Add($"{pushables} pushable crate{(pushables == 1 ? "" : "s")}"); }
            if (ladders > 0) { parts.Add($"{ladders} ladder{(ladders == 1 ? "" : "s")}"); }
            if (ropes > 0) { parts.Add($"{ropes} rope{(ropes == 1 ? "" : "s")}"); }
            if (ziplines > 0) { parts.Add($"{ziplines} zipline{(ziplines == 1 ? "" : "s")}"); }
            if (pads > 0) { parts.Add($"{pads} bounce pad{(pads == 1 ? "" : "s")}"); }
            if (climbables > 0) { parts.Add($"{climbables} climbable panel{(climbables == 1 ? "" : "s")}"); }
            if (waters > 0) { parts.Add($"{waters} bod{(waters == 1 ? "y" : "ies")} of water"); }
            if (gates > 0) { parts.Add($"{gates} timing gate{(gates == 1 ? "" : "s")} on {timers.Count} course{(timers.Count == 1 ? "" : "s")} with {displays} display{(displays == 1 ? "" : "s")}"); }
            return string.Join(", ", parts);
        }

        /// <summary>Whether an event already calls the method on the target, as a persistent listener.</summary>
        private static bool ShowsOn(UnityEventBase onEvent, UnityEngine.Object target, string method)
        {
            for (int i = 0; i < onEvent.GetPersistentEventCount(); i++)
            {
                if (onEvent.GetPersistentTarget(i) == target && onEvent.GetPersistentMethodName(i) == method) { return true; }
            }
            return false;
        }

        private static T Need<T>(GameObject obj, bool withUndo) where T : Component
        {
            T existing = obj.GetComponent<T>();
            if (existing != null) { return existing; }
            return withUndo ? Undo.AddComponent<T>(obj) : obj.AddComponent<T>();
        }
    }
}
