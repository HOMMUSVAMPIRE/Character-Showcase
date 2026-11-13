using System.Globalization;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace CharacterShowcase.EditorTools.Level
{
    // Mechanics: the level's fixtures that move or count (moving platforms, pushable crates, timing gates, ladders, ropes,
    // ziplines, bounce pads), and low beams to slide under. The level can't reference runtime code, so a fixture is its geometry, named for what it is
    // with its settings after the name ("Moving Platform (Lift) | travel=0,8,0 | period=5"); the scene's game setup
    // (SceneGameSetup, LevelFixtureSetup) turns it into the real thing. The names' starts must match LevelFixtureSetup's.
    // Fixtures that move are never static.
    public static partial class Graybox
    {
        public const string MovingPlatformPrefix = "Moving Platform";
        public const string PushablePrefix = "Pushable Crate";
        public const string TimingGatePrefix = "Timing Gate";
        public const string TimingDisplayPrefix = "Timing Display";
        // A ladder's name starts "Ladder |": "Ladder (future): …" markers aren't ladders. The same goes for ropes ("Rope
        // Anchor" placeholders and "Rope Swing (future)" markers aren't ropes), ziplines and bounce pads.
        public const string LadderPrefix = "Ladder";
        public const string RopePrefix = "Rope";
        public const string ZiplinePrefix = "Zipline";
        public const string BouncePadPrefix = "Bounce Pad";

        private static string F(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);
        private static string F(Vector3 v) => $"{F(v.x)},{F(v.y)},{F(v.z)}";

        /// <summary>
        /// A moving platform: a steel deck (top centre at top, size across, deep and along) with an orange band round its
        /// edge, which the game setup moves back and forth along travel (in its own axes) over period seconds, pausing
        /// pause seconds at each end, spinning spin degrees a second, phase into its cycle. A box collider; not static.
        /// </summary>
        [Piece]
        public static GameObject MovingPlatform(Transform parent, string label, Vector3 top, Vector3 size, Vector3 travel, float period,
            float pause = 0.75f, Vector3 spin = default, float phase = 0f, bool smooth = true)
        {
            using LevelSpecRecorder.Scope recording = LevelSpecRecorder.Call(nameof(MovingPlatform), parent, label, top, size, travel, period, pause, spin, phase, smooth);
            var platform = new GameObject($"{MovingPlatformPrefix} ({label}) | travel={F(travel)} | period={F(period)} | pause={F(pause)}" +
                                          $" | spin={F(spin)} | phase={F(phase)} | smooth={(smooth ? 1 : 0)}");
            platform.transform.SetParent(parent, false);
            platform.transform.localPosition = top - Vector3.up * (size.y / 2f);
            Vector3 h = size / 2f;
            float band = Settings.mechanics.edgeBand;
            var mesh = new GrayboxMesh();
            mesh.Box(Surface.Steel, -h, new Vector3(h.x, h.y - band, h.z), top: false);
            mesh.Box(Surface.Accent, new Vector3(-h.x, h.y - band, -h.z), new Vector3(h.x, h.y, h.z), bottom: false, top: false);
            mesh.Box(Surface.Steel, new Vector3(-h.x, h.y - 0.001f, -h.z), h, sides: false, ends: false, bottom: false);
            mesh.AddTo(platform, platform.name);
            platform.AddComponent<BoxCollider>().size = size;
            return platform;
        }

        /// <summary>
        /// A crate the character can push (and, if canPull, pull), size each way, standing on a floor at floor at "at":
        /// steel with orange corner bands. A box collider; not static. speed scales how fast it moves.
        /// </summary>
        [Piece]
        public static GameObject PushableCrate(Transform parent, Vector2 at, float floor, float size, bool canPull = true, float speed = 1f)
        {
            using LevelSpecRecorder.Scope recording = LevelSpecRecorder.Call(nameof(PushableCrate), parent, at, floor, size, canPull, speed);
            var crate = new GameObject($"{PushablePrefix} | pull={(canPull ? 1 : 0)} | speed={F(speed)}");
            crate.transform.SetParent(parent, false);
            crate.transform.localPosition = new Vector3(at.x, floor + size / 2f, at.y);
            float h = size / 2f, b = Settings.mechanics.edgeBand;
            var mesh = new GrayboxMesh();
            mesh.Box(Surface.Steel, new Vector3(-h, -h, -h), new Vector3(h, h, h), bottom: false);
            // Orange bands round its middle, standing proud.
            mesh.Box(Surface.Accent, new Vector3(-h - 0.01f, -b, -h - 0.01f), new Vector3(h + 0.01f, b, h + 0.01f), top: false, bottom: false);
            mesh.AddTo(crate, crate.name);
            crate.AddComponent<BoxCollider>().size = Vector3.one * size;
            return crate;
        }

        /// <summary>
        /// A ladder height tall standing on a floor at floor, its back against whatever is at "at" (a wall's face, say),
        /// climbed from the side facing points to: two orange rails width apart with steel rungs between them, every rung
        /// spacing from a spacing up (Ladder.RungCountFor's layout, so the game's Ladder finds them where they are). A box
        /// collider round the whole ladder. The game setup makes it climbable (see LevelFixtureSetup). Its top should be
        /// level with the floor it's climbed onto: a ladder is only climbed off its top onto a floor there.
        /// </summary>
        [Piece]
        public static GameObject Ladder(Transform parent, Vector2 at, float floor, float height, Vector2 facing, float width = 0.6f)
        {
            using LevelSpecRecorder.Scope recording = LevelSpecRecorder.Call(nameof(Ladder), parent, at, floor, height, facing, width);
            GrayboxSettings.MechanicsSettings m = Settings.mechanics;
            float spacing = m.ladderRungSpacing, rail = m.ladderRail, rung = m.ladderRung;
            var ladder = new GameObject($"{LadderPrefix} | height={F(height)} | width={F(width)} | spacing={F(spacing)} | rail={F(rail)} | rung={F(rung)}");
            ladder.transform.SetParent(parent, false);
            Vector2 forward = facing.sqrMagnitude > 1e-8f ? facing.normalized : Vector2.up;
            Vector2 middle = at + forward * (rail / 2f);
            ladder.transform.SetLocalPositionAndRotation(new Vector3(middle.x, floor, middle.y),
                Quaternion.LookRotation(new Vector3(forward.x, 0f, forward.y), Vector3.up));

            // In its own space: up +Y, climbed from +Z, rails at the sides, rungs between them.
            float h = width / 2f, r = rail / 2f, s = rung / 2f;
            var mesh = new GrayboxMesh();
            mesh.Box(Surface.Accent, new Vector3(-h, 0f, -r), new Vector3(-h + rail, height, r), bottom: false);
            mesh.Box(Surface.Accent, new Vector3(h - rail, 0f, -r), new Vector3(h, height, r), bottom: false);
            int rungs = Mathf.Max(0, Mathf.FloorToInt((height - 0.5f * spacing) / spacing + 1e-4f));
            for (int k = 0; k < rungs; k++)
            {
                float y = (k + 1) * spacing;
                mesh.Box(Surface.Steel, new Vector3(-h + rail, y - s, -s), new Vector3(h - rail, y + s, s), ends: false);
            }
            mesh.AddTo(ladder, ladder.name);
            var collider = ladder.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, height / 2f, 0f);
            collider.size = new Vector3(width, height, rail);
            MakeStatic(ladder, occluder: false);
            return ladder;
        }

        /// <summary>
        /// A rope to swing on, hanging length metres from anchor (where it's tied), from a small graphite bracket above
        /// it, which has to hang from something (a gantry's beam, say: the builder's). The rope is orange, hanging
        /// straight down; the game setup makes it swing (see LevelFixtureSetup), drawing it as it swings. No colliders:
        /// nothing collides with a rope. Not static: it moves.
        /// </summary>
        [Piece]
        public static GameObject Rope(Transform parent, Vector3 anchor, float length)
        {
            using LevelSpecRecorder.Scope recording = LevelSpecRecorder.Call(nameof(Rope), parent, anchor, length);
            GrayboxSettings.MechanicsSettings m = Settings.mechanics;
            float b = m.ropeBracket / 2f, t = m.ropeThickness / 2f;
            var bracket = new GameObject("Rope Bracket");
            bracket.transform.SetParent(parent, false);
            bracket.transform.localPosition = anchor;
            var bracketMesh = new GrayboxMesh();
            bracketMesh.Box(Surface.Graphite, new Vector3(-b, 0f, -b), new Vector3(b, 2f * b, b));
            bracketMesh.AddTo(bracket, bracket.name);
            MakeStatic(bracket, occluder: false);

            var rope = new GameObject($"{RopePrefix} | length={F(length)} | thickness={F(m.ropeThickness)}");
            rope.transform.SetParent(parent, false);
            rope.transform.localPosition = anchor;
            var mesh = new GrayboxMesh();
            mesh.Box(Surface.Accent, new Vector3(-t, -length, -t), new Vector3(t, 0f, t), top: false);
            mesh.AddTo(rope, rope.name, uv2: false);
            return rope;
        }

        /// <summary>
        /// A zipline: a steel cable from start to end (start the higher, for a ride down it), on a graphite post at each
        /// end standing on the floor below it there (startFloor, endFloor), just beyond the cable's end. Characters ride it
        /// at up to speed (m/s). The game setup makes it rideable (see LevelFixtureSetup). The posts have box colliders;
        /// the cable none (characters hang from it).
        /// </summary>
        [Piece]
        public static GameObject Zipline(Transform parent, Vector3 start, Vector3 end, float startFloor, float endFloor, float speed = 16f)
        {
            using LevelSpecRecorder.Scope recording = LevelSpecRecorder.Call(nameof(Zipline), parent, start, end, startFloor, endFloor, speed);
            GrayboxSettings.MechanicsSettings m = Settings.mechanics;
            Vector3 along = end - start;
            float length = along.magnitude;
            Vector3 direction = length > 1e-4f ? along / length : Vector3.forward;
            Vector2 flat = new Vector2(direction.x, direction.z);
            flat = flat.sqrMagnitude > 1e-6f ? flat.normalized : Vector2.up;

            // Laid out in its own space: from its origin (the start) along +Z to the end.
            var line = new GameObject($"{ZiplinePrefix} | length={F(length)} | speed={F(speed)}");
            line.transform.SetParent(parent, false);
            line.transform.SetLocalPositionAndRotation(start, Quaternion.LookRotation(direction, Vector3.up));
            float c = m.cableThickness / 2f;
            var mesh = new GrayboxMesh();
            mesh.Box(Surface.Steel, new Vector3(-c, -c, 0f), new Vector3(c, c, length));
            mesh.AddTo(line, line.name, uv2: false);
            MakeStatic(line, occluder: false);

            // A post just beyond each end, up to just above the cable.
            float p = m.ziplinePost, reachOver = 0.2f;
            Transform posts = Group(parent, "Zipline Posts");
            foreach ((Vector3 at, Vector2 outward, float floor) in new[] { (start, -flat, startFloor), (end, flat, endFloor) })
            {
                Vector2 centre = new Vector2(at.x, at.z) + outward * (p / 2f + 0.05f);
                BoxXZ(posts, "Zipline Post", centre, new Vector2(p, p), floor, at.y + reachOver, Mathf.Atan2(flat.x, flat.y) * Mathf.Rad2Deg, Surface.Graphite);
            }
            return line;
        }

        /// <summary>
        /// A bounce pad, size each way, standing on a floor at floor at "at": a steel base with a cyan springy top, which
        /// launches characters height metres up (see LevelFixtureSetup). A box collider round the whole pad; the top
        /// isn't static (it squashes when the pad launches someone).
        /// </summary>
        [Piece]
        public static GameObject BouncePad(Transform parent, Vector2 at, float floor, float size, float height)
        {
            using LevelSpecRecorder.Scope recording = LevelSpecRecorder.Call(nameof(BouncePad), parent, at, floor, size, height);
            GrayboxSettings.MechanicsSettings m = Settings.mechanics;
            float h = size / 2f, tall = m.bouncePad.x, top = Mathf.Min(m.bouncePad.y, tall * 0.8f), inset = 0.1f;
            var pad = new GameObject($"{BouncePadPrefix} | height={F(height)}");
            pad.transform.SetParent(parent, false);
            pad.transform.localPosition = new Vector3(at.x, floor, at.y);
            var mesh = new GrayboxMesh();
            mesh.Box(Surface.Steel, new Vector3(-h, 0f, -h), new Vector3(h, tall - top, h), bottom: false);
            mesh.Box(Surface.Accent, new Vector3(-h - 0.01f, 0f, -h - 0.01f), new Vector3(h + 0.01f, Settings.mechanics.edgeBand, h + 0.01f), top: false, bottom: false);
            mesh.AddTo(pad, pad.name);
            var collider = pad.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, tall / 2f, 0f);
            collider.size = new Vector3(size, tall, size);
            MakeStatic(pad);

            // Its springy top, its origin at its bottom, so it squashes down onto the base.
            var spring = new GameObject("Spring");
            spring.transform.SetParent(pad.transform, false);
            spring.transform.localPosition = new Vector3(0f, tall - top, 0f);
            var springMesh = new GrayboxMesh();
            springMesh.Box(Surface.Cyan, new Vector3(-h + inset, 0f, -h + inset), new Vector3(h - inset, top, h - inset), bottom: false);
            springMesh.AddTo(spring, spring.name);
            GameObjectUtility.SetStaticEditorFlags(spring, 0);
            return pad;
        }

        /// <summary>
        /// A timing gate across a course from a to b (on a floor at floor): graphite posts with cyan strips under an orange
        /// beam, a trigger between them that the game setup makes time course (role: "start-finish", "start" or
        /// "finish"; distance, for a course timed start to finish, its length), and, if display, a sign on the beam showing
        /// the course's times to those coming through it along approach. The posts and beam have colliders.
        /// </summary>
        [Piece]
        public static void TimingGate(Transform parent, string course, string role, Vector2 a, Vector2 b, float floor, Vector2 approach,
            float distance = 0f, bool display = false)
        {
            using LevelSpecRecorder.Scope recording = LevelSpecRecorder.Call(nameof(TimingGate), parent, course, role, a, b, floor, approach, distance, display);
            GrayboxSettings.MechanicsSettings m = Settings.mechanics;
            float width = Vector2.Distance(a, b), half = width / 2f, p = m.gatePost / 2f, top = m.gateHeight;
            Vector2 middle = (a + b) / 2f, across = (b - a) / width;
            // Laid out with +z along approach (the way through it), x across it.
            Vector2 forward = approach.normalized;
            float yaw = Mathf.Atan2(forward.x, forward.y) * Mathf.Rad2Deg;
            var gate = new GameObject($"Gate ({course}, {role})");
            gate.transform.SetParent(parent, false);
            gate.transform.SetLocalPositionAndRotation(new Vector3(middle.x, floor, middle.y), Quaternion.Euler(0f, yaw, 0f));

            var mesh = new GrayboxMesh();
            foreach (float x in new[] { -half - p, half + p })
            {
                mesh.Box(Surface.Graphite, new Vector3(x - p, 0f, -p), new Vector3(x + p, top, p), bottom: false, top: false);
                mesh.Box(Surface.Cyan, new Vector3(x - 0.05f, 0.4f, -p - 0.02f), new Vector3(x + 0.05f, top - 0.6f, -p), top: false, bottom: false);
            }
            mesh.Box(Surface.Accent, new Vector3(-half - 2f * p, top, -p), new Vector3(half + 2f * p, top + 0.4f, p));
            Vector2 panel = m.gatePanel;
            if (display)
            {
                mesh.Box(Surface.Graphite, new Vector3(-panel.x / 2f, top + 0.4f, -0.06f), new Vector3(panel.x / 2f, top + 0.4f + panel.y, 0.06f));
            }
            mesh.AddTo(gate, gate.name);
            foreach (float x in new[] { -half - p, half + p }) { AddBox(gate, new Vector3(x, top / 2f, 0f), new Vector3(2f * p, top, 2f * p)); }
            AddBox(gate, new Vector3(0f, top + (display ? (0.4f + panel.y) / 2f : 0.2f), 0f), new Vector3(width + 4f * p, display ? 0.4f + panel.y : 0.4f, 2f * p));
            MakeStatic(gate, occluder: false);

            var trigger = new GameObject($"{TimingGatePrefix} | course={course} | role={role}{(distance > 0f ? $" | distance={F(distance)}" : "")}");
            trigger.transform.SetParent(gate.transform, false);
            var volume = trigger.AddComponent<BoxCollider>();
            volume.isTrigger = true;
            volume.center = new Vector3(0f, top / 2f, 0f);
            volume.size = new Vector3(width, top, 0.6f);

            if (display)
            {
                // Read by whoever's coming through: facing back along approach.
                TextMeshPro text = Text(gate.transform, $"{TimingDisplayPrefix} | course={course}", $"{course.ToUpperInvariant()}  --:--.--",
                    new Vector3(0f, top + 0.4f + panel.y / 2f, -0.075f), Quaternion.identity, m.gateTextHeight, panel.x - 0.3f, Settings.signs.textColour);
                text.enableAutoSizing = true;
                text.rectTransform.sizeDelta = new Vector2(panel.x - 0.3f, panel.y - 0.15f);
                text.textWrappingMode = TextWrappingModes.Normal;
            }
        }

        /// <summary>
        /// A low beam across from a to b on a floor at floor, clear high underneath (too low to stand under, enough to
        /// crouch, slide or roll under): an orange beam on graphite posts. Box colliders.
        /// </summary>
        [Piece]
        public static void LowBeam(Transform parent, string name, Vector2 a, Vector2 b, float floor, float clear)
        {
            using LevelSpecRecorder.Scope recording = LevelSpecRecorder.Call(nameof(LowBeam), parent, name, a, b, floor, clear);
            float depth = Settings.mechanics.lowBeamDepth, post = 0.3f;
            float length = Vector2.Distance(a, b);
            Vector2 dir = (b - a) / length, middle = (a + b) / 2f;
            float yaw = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;
            Transform beam = Group(parent, name);
            BoxXZ(beam, "Beam", middle, new Vector2(depth, length), floor + clear, floor + clear + 0.5f, yaw, Surface.Accent);
            foreach (Vector2 end in new[] { a + dir * (post / 2f), b - dir * (post / 2f) })
            {
                BoxXZ(beam, "Post", end, new Vector2(post, post), floor, floor + clear, yaw, Surface.Graphite);
            }
        }
    }
}
