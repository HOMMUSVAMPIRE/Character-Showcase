using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using static CharacterShowcase.EditorTools.Level.Graybox;

namespace CharacterShowcase.EditorTools.Level
{
    /// <summary>
    /// Builds the Proving Ground: a level for testing the movement code, one circuit through six zones from a start
    /// hall, each pushing one family of moves, every gap, run and ledge set against what the character can do (from the
    /// movement mode's tuning, measured with the real movement code; see the notes on each zone, and revise them with the
    /// tuning). Built like the Meridian Complex (GrayboxScene, Graybox's pieces); its moving platforms, pushable crates
    /// and timing gates are fixtures the scene's game setup turns into the real thing (Set Up Scene: LevelFixtureSetup).
    ///
    /// The circuit: P0 Start Hall (±0, the lap gate) → P1 Sprint Line east (a 100 m speed trap; slide gates and kerbs) →
    /// P2 Gap Run north (gaps graded to the jumps) → P3 Wall-Run Canyon west (wall runs over a pit) → P4 Ledge Tower (ledges
    /// graded to the ledge reach, a chimney, a stair down) → P5 Low Works (crawl, slide, roll, push) → P6 Moving Ground
    /// (moving platforms over a pit) → P0. Every challenge has a way back up. Units are metres, +Z north.
    /// </summary>
    public static class ProvingGroundBuilder
    {
        public const string ScenePath = "Assets/Scenes/ProvingGround/ProvingGround.unity";

        // ---- What the character can do (from the tuning, measured: revise with it) ----
        // A sprint jump crosses 6.2 m (6.8 m before jumps were set by height), with an air dash 9.7 m; a double jump 11.8 m, with an air dash 14.5 m. A jump rises
        // 2.2 m, a double 3.9 m. A ledge is caught 1.4 to 2.6 m above the feet: 4.8 m up from a jump, 6.5 m from a double.
        // A slide goes 5.7 m, a roll 3.7 m, both under 1.1 m; a wall run 11 m, falling 3.7 m.

        // ---- The layout (tunable) ----
        private const float GroundHalf = 175f;
        private const float Run = 4f, RunUp = 8f;                    // The gap run's platforms; the canyon's run-up.
        private const float CanyonFloor = -4f, PitFloor = -6f;       // The canyon's pit, and the moving ground's.
        private const float LowWorksCeiling = 12f;
        private const float Clear = 1.2f;                            // Under low beams and hoods: over a crouch (1.1 m), under a stand (2 m).

        // P2's platforms (z from, to, along x 136 to 148) and the gaps between them, graded to the jumps.
        private static readonly (string name, float z0, float z1)[] GapPlatforms =
        {
            ("A", 23f, 39f), ("B", 43f, 55f), ("C", 61f, 73f), ("D", 81.5f, 93.5f), ("E", 104.5f, 120.5f), ("F", 134f, 150f),
        };

        // P3's canyon: between walls along z 140-142 and 146-148, from x 28 to 116.
        private static readonly UnityEngine.Rect CanyonHole = UnityEngine.Rect.MinMaxRect(16f, 140f, 136f, 148f);
        private static readonly UnityEngine.Rect MovingGroundPit = UnityEngine.Rect.MinMaxRect(-30f, -130f, 50f, -62f);
        private static readonly UnityEngine.Rect LowWorksHall = UnityEngine.Rect.MinMaxRect(-140f, -70f, -60f, -20f);

        internal static readonly GrayboxScene Scene = new("Proving Ground", ScenePath,
            ("P0 Start Hall", BuildStartHall),
            ("P1 Sprint Line", BuildSprintLine),
            ("P2 Gap Run", BuildGapRun),
            ("P3 Wall-Run Canyon", BuildCanyon),
            ("P4 Ledge Tower", BuildLedgeTower),
            ("P5 Low Works", BuildLowWorks),
            ("P6 Moving Ground", BuildMovingGround),
            ("the paths and park", BuildPaths),
            ("the ground", (root, material) =>
            {
                Begin(material);
                Ground(root, GroundHalf, new[] { CanyonHole, MovingGroundPit, LowWorksHall });
            }));

        /// <summary>Builds the Proving Ground in the background (see LevelBuildWindow). Opens the scene when it's done; then Set Up Scene.</summary>
        [MenuItem("Tools/Character Showcase/Level/Build Proving Ground")]
        public static void BuildFromMenu() => LevelBuildWindow.BuildFromMenu(Scene);

        /// <summary>For -executeMethod: the whole build at once, waiting for the bakes.</summary>
        public static void BuildFromCommandLine() => Scene.Build();

        private static Transform Zone(Transform root, Material material, string name)
        {
            Begin(material);
            return Group(root, name);
        }

        // ---- P0 Start Hall: an open octagonal plaza, the lap gate east ----

        private static void BuildStartHall(Transform root, Material material)
        {
            Transform hall = Zone(root, material, "P0 Start Hall");
            Prism(hall, "Plaza (±0)", Octagon(18f), -0.5f, 0.05f);
            Prism(hall, "Dais (+0.6)", Octagon(3.4f), 0f, 0.6f);
            Tree(hall, Vector2.zero, 0.6f, new System.Random(Settings.foliage.seed + 7));
            GuideLine(hall, "Guide Ring (Orange)", Octagon(6.5f), 0.05f, Surface.Accent, closed: true);
            GuideLine(hall, "Guide Ring (White)", Octagon(7.1f), 0.05f, Surface.White, closed: true);
            Transform spawns = Group(hall, "Spawn Points");
            foreach (Vector2 at in new[] { new Vector2(-6f, -11f), new Vector2(6f, -11f), new Vector2(-6f, 11f), new Vector2(6f, 11f) })
            {
                SpawnMarker(spawns, $"Start {(at.y < 0 ? "South" : "North")} {(at.x < 0 ? "West" : "East")}", at, 0.1f, 90f);
            }
            Vector2 se = new Vector2(1f, -1f).normalized * 16f;
            Bollards(hall, Octagon(16f), 0.05f, (new Vector2(16f, 0f), 16f), (se, 9f));
            AreaSign(hall, new Vector2(-14f, 0f), 1.2f, Vector2.right, "P0", "Start Hall", freeStanding: true);
            // The lap gate: the circuit starts and ends through it, across the start of the sprint line.
            TimingGate(hall, "Lap", "start-finish", new Vector2(20f, -7f), new Vector2(20f, 7f), 0f, Vector2.right, display: true);
        }

        // ---- P1 Sprint Line: 130 m east; a clean speed-trap lane north, slide gates and kerbs south ----

        private static void BuildSprintLine(Transform root, Material material)
        {
            Transform line = Zone(root, material, "P1 Sprint Line");
            GuideLine(line, "Lane Edge (North)", new[] { new Vector2(18f, 7f), new Vector2(136f, 7f) }, 0f, Surface.Accent);
            GuideLine(line, "Lane Edge (South)", new[] { new Vector2(18f, -7f), new Vector2(148f, -7f) }, 0f, Surface.Accent);
            GuideLine(line, "Lane Divider", new[] { new Vector2(22f, 0f), new Vector2(130f, 0f) }, 0f, Surface.White);
            // The speed trap: 100 m, at a 10 m/s sprint 10 s.
            TimingGate(line, "Speed Trap", "start", new Vector2(28f, 0f), new Vector2(28f, 7f), 0f, Vector2.right, distance: 100f);
            TimingGate(line, "Speed Trap", "finish", new Vector2(128f, 0f), new Vector2(128f, 7f), 0f, Vector2.right, distance: 100f, display: true);
            Ruler(line, "Ruler (Speed Trap)", new Vector2(28f, 7.6f), Vector2.right, 100f, 0f, labelEvery: 10f);
            // Slide gates 14 m apart: more than one slide (5.7 m), so it's slide, stand, slide.
            foreach (float x in new[] { 60f, 74f })
            {
                LowBeam(line, "Slide Gate", new Vector2(x, -7f), new Vector2(x, 0f), 0f, Clear);
                FloorLabel(line, $"{Clear:0.0} m", new Vector2(x - 2.5f, -3.5f), 0f, Vector2.right);
            }
            // Kerbs either side of the 0.35 m step height: hop the first, run up the second.
            Block(line, "Kerb (0.40 m)", 94f, 100f, -7f, 0f, 0f, 0.4f);
            Block(line, "Kerb (0.30 m)", 108f, 114f, -7f, 0f, 0f, 0.3f);
            FloorLabel(line, "0.40 m", new Vector2(91.5f, -3.5f), 0f, Vector2.right);
            FloorLabel(line, "0.30 m", new Vector2(105.5f, -3.5f), 0f, Vector2.right);
            AreaSign(line, new Vector2(34f, -10f), 1.2f, Vector2.up, "P1", "Sprint Line", freeStanding: true);
        }

        // ---- P2 Gap Run: platforms at +4 heading north across gaps graded to the jumps, over a lawned pit ----

        private static void BuildGapRun(Transform root, Material material)
        {
            Transform run = Zone(root, material, "P2 Gap Run");
            Transform stairs = Group(run, "Stairs");
            const float x0 = 136f, x1 = 148f;
            Ramp(run, "Ramp (1:4, ±0 to +4)", new Vector2(142f, 7f), Vector2.up, 12f, 16f, 0f, Run);
            for (int i = 0; i < GapPlatforms.Length; i++)
            {
                (string name, float z0, float z1) = GapPlatforms[i];
                Platform(run, $"Platform {name} (+4)", x0, x1, z0, z1, 0f, Run, coping: Surface.Accent);
                if (i == 0) { continue; }
                // The gap before it, striped on both edges, and its width painted on the landing.
                float before = GapPlatforms[i - 1].z1;
                HazardStripes(run, new Vector2(x0, before), new Vector2(x1, before), Vector2.down, Run);
                HazardStripes(run, new Vector2(x0, z0), new Vector2(x1, z0), Vector2.up, Run);
                FloorLabel(run, $"{z0 - before:0.0} m", new Vector2(142f, z0 + 2f), Run, Vector2.up);
                // A stair back up from the pit, on its west side.
                Flight(stairs, $"Stair (Pit to {name})", new Vector2(x0 - FlightLength(0f, Run), z0 + 5f), Vector2.right, 6f, 0f, Run);
            }
            UnityEngine.Rect pit = UnityEngine.Rect.MinMaxRect(118f, 23f, 162f, 134f);
            Lawns(run, pit, new[] { 0f }, 20);
            ScatterTrees(run, pit, 0f, 21, spread: 1.4f);
            AreaSign(run, new Vector2(130f, 10f), 1.2f, Vector2.right, "P2", "Gap Run", freeStanding: true);
        }

        // ---- P3 Wall-Run Canyon: a canyon 4 m wide between walls to +14, over a pit at -4 ----

        private static void BuildCanyon(Transform root, Material material)
        {
            Transform canyon = Zone(root, material, "P3 Wall-Run Canyon");
            Transform stairs = Group(canyon, "Stairs");
            // Up from platform F (+4) to the run-up (+8): a jump into the first wall run from there lands, after two runs
            // (each falling 3.7 m) and a wall jump between them, on the first landing at +4.
            Flight(stairs, "Stair (+4 to +8)", new Vector2(136f + FlightLength(Run, RunUp), 144f), Vector2.left, 6f, Run, RunUp);
            Platform(canyon, "Run-up (+8)", 116f, 136f, 136f, 152f, CanyonFloor - 0.5f, RunUp, coping: Surface.Accent);
            Block(canyon, "Wall (South, +14)", 28f, 116f, 140f, 142f, CanyonFloor - 0.5f, 14f, Surface.Panel);
            Block(canyon, "Wall (North, +14)", 28f, 116f, 146f, 148f, CanyonFloor - 0.5f, 14f, Surface.Panel);
            Block(canyon, "Pit Floor (-4)", 28f, 116f, 142f, 146f, CanyonFloor - 0.5f, CanyonFloor);
            Platform(canyon, "Landing L1 (+4)", 84f, 96f, 142f, 146f, CanyonFloor - 0.5f, Run, coping: Surface.Accent);
            Platform(canyon, "Landing L2 (+4)", 60f, 72f, 142f, 146f, CanyonFloor - 0.5f, Run, coping: Surface.Accent);
            Platform(canyon, "Canyon Exit (+4)", 16f, 28f, 136f, 152f, CanyonFloor - 0.5f, Run, coping: Surface.Accent);
            // The runs: 10 m (inside the 11 m a wall run lasts) and 12 m (past it: the run must end in a jump), painted
            // on the landings before them; the pit's stairs back up to each landing, under the runs.
            FloorLabel(canyon, "WALL RUN 10 m + 10 m", new Vector2(126f, 144f), RunUp, Vector2.left);
            FloorLabel(canyon, "WALL RUN 12 m", new Vector2(90f, 144f), Run, Vector2.left);
            FloorLabel(canyon, "WALL RUN 10 + 10 + 12 m", new Vector2(66f, 144f), Run, Vector2.left);
            foreach ((float landing, string name) in new[] { (96f, "L1"), (72f, "L2"), (28f, "Exit") })
            {
                Flight(stairs, $"Stair (Pit to {name})", new Vector2(landing + FlightLength(CanyonFloor, Run), 144f), Vector2.left, 4f, CanyonFloor, Run, CanyonFloor - 0.5f);
            }
            foreach (float x in new[] { 50f, 94f })
            {
                Banner(canyon, new Vector2(x, 148f), Vector2.up, 13.4f, 6f, "P3");
            }
            AreaSign(canyon, new Vector2(130f, 151f), RunUp + 1.2f, Vector2.left, "P3", "Wall-Run Canyon", freeStanding: true);
        }

        // ---- P4 Ledge Tower: ledges graded to the ledge reach, up to +24; a chimney; a stair down ----

        private static void BuildLedgeTower(Transform root, Material material)
        {
            Transform tower = Zone(root, material, "P4 Ledge Tower");
            Transform rails = Group(tower, "Guardrails"), stairs = Group(tower, "Stairs");
            Walkway(Group(tower, "Bridge from the Canyon (+4)"), rails, new Vector2(16f, 144f), Vector2.left, 46f, 6f, Run, suspended: true);
            Platform(tower, "Tower Deck (+4)", -46f, -30f, 124f, 150f, 0f, Run);
            // Each ledge up from the one before: +4.5 (a jump catches 4.8 m), +6.0 (a double catches 6.5 m), +4.0, +5.5.
            Platform(tower, "Ledge T1 (+8.5)", -58f, -46f, 132f, 144f, 0f, 8.5f);
            Platform(tower, "Ledge T2 (+14.5)", -66f, -54f, 120f, 132f, 0f, 14.5f);
            Platform(tower, "Ledge T3 (+18.5)", -58f, -46f, 108f, 120f, 0f, 18.5f);
            Platform(tower, "Summit (+24)", -46f, -30f, 104f, 116f, 0f, 24f);
            FloorLabel(tower, "UP 4.5 m: JUMP", new Vector2(-43f, 138f), Run, Vector2.left);
            FloorLabel(tower, "UP 6.0 m: DOUBLE JUMP", new Vector2(-56f, 134.5f), 8.5f, Vector2.down);
            FloorLabel(tower, "UP 4.0 m: JUMP", new Vector2(-56f, 122.5f), 14.5f, Vector2.down);
            FloorLabel(tower, "UP 5.5 m: DOUBLE", new Vector2(-48.5f, 112f), 18.5f, Vector2.right);
            TowerBands(tower, -46f, -30f, 104f, 116f, 0f, 22f, Vector2.down);
            Banner(tower, new Vector2(-38f, 104f), Vector2.down, 21f, 10f, "P4");
            RailAround(rails, Rect(-46f, 104f, -30f, 116f), 24f, (new Vector2(-46f, 110f), 12f), (new Vector2(-30f, 110f), 6f));

            // A chimney north of the deck: a slot 2.5 m wide between walls to +14.5, to climb by wall jumps.
            Block(tower, "Chimney (West)", -40f, -36.25f, 150f, 158f, 0f, 14.5f, Surface.Panel);
            Block(tower, "Chimney (East)", -33.75f, -30f, 150f, 158f, 0f, 14.5f, Surface.Panel);
            Block(tower, "Chimney (Back)", -36.25f, -33.75f, 157f, 158f, 0f, 14.5f, Surface.Panel);
            Block(tower, "Chimney Floor (+4)", -36.25f, -33.75f, 150f, 157f, 0f, Run);
            FloorLabel(tower, "CHIMNEY 2.5 m", new Vector2(-35f, 148f), Run, Vector2.up);

            // The way down: a stair east off the summit to a landing at +4, and on down to the ground.
            float upper = FlightLength(Run, 24f), lower = FlightLength(0f, Run);
            Flight(stairs, "Stair (Summit to +4)", new Vector2(-30f + upper, 110f), Vector2.left, 6f, Run, 24f);
            Block(tower, "Landing (+4)", -30f + upper, -30f + upper + 4f, 107f, 113f, 0f, Run);
            Flight(stairs, "Stair (+4 to ±0)", new Vector2(-30f + upper + 4f + lower, 110f), Vector2.left, 6f, 0f, Run);
            AreaSign(tower, new Vector2(-34f, 128f), Run + 1.2f, Vector2.right, "P4", "Ledge Tower", freeStanding: true);
        }

        // ---- P5 Low Works: an enclosed, lit hall: a crawl tunnel, slide gates, a roll lane, crates and a high ledge ----

        private static void BuildLowWorks(Transform root, Material material)
        {
            Transform works = Zone(root, material, "P5 Low Works");
            UnityEngine.Rect a = LowWorksHall;
            var cells = new List<Cell> { new("Low Works Hall", a.xMin, a.yMin, a.xMax, a.yMax, 0f, LowWorksCeiling) };
            var doors = new[] { UnityEngine.Rect.MinMaxRect(-84f, -20f, -76f, -19f), UnityEngine.Rect.MinMaxRect(-60f, -47f, -59f, -43f) };
            Cells(Group(works, "Hall"), cells, doors, -1f);
            // Portals open the hall's walls full height: headers close them down to doors 4 m high.
            Block(works, "Door Header (North)", -84f, -76f, -20f, -19.5f, 4f, LowWorksCeiling + 0.5f, Surface.Panel);
            Block(works, "Door Header (East)", -60f, -59.5f, -47f, -43f, 4f, LowWorksCeiling + 0.5f, Surface.Panel);
            CellLighting(Group(works, "Lighting"), cells);
            CellUtilities(Group(works, "Utilities"), cells);
            UtilityStation(works, new Vector2(a.xMin, -45f), Vector2.right, 0f);

            // A crawl tunnel, 1.2 m clear for 36 m: crouched all the way.
            Block(works, "Crawl Tunnel Wall (South)", -136f, -100f, -66.5f, -66f, 0f, Clear, Surface.Panel);
            Block(works, "Crawl Tunnel Wall (North)", -136f, -100f, -62f, -61.5f, 0f, Clear, Surface.Panel);
            Block(works, "Crawl Tunnel Roof", -136f, -100f, -66.5f, -61.5f, Clear, Clear + 0.5f, Surface.Panel);
            FloorLabel(works, "CRAWL 1.2 m", new Vector2(-97f, -64f), 0f, Vector2.left);
            // Slide gates 12 m apart (a slide is 5.7 m).
            foreach (float x in new[] { -106f, -118f, -130f })
            {
                LowBeam(works, "Slide Gate", new Vector2(x, -58f), new Vector2(x, -52f), 0f, Clear);
            }
            FloorLabel(works, "SLIDE", new Vector2(-102f, -55f), 0f, Vector2.left);
            // A roll lane: hoods 3.5 m deep, 1.2 m clear (a roll is 3.7 m), 4.5 m apart.
            foreach (float x in new[] { -96f, -88f, -80f })
            {
                Block(works, "Roll Hood", x, x + 3.5f, -58f, -52f, Clear, Clear + 0.5f, Surface.Graphite);
                foreach (float z in new[] { -58f, -52.3f }) { Block(works, "Roll Hood Post", x, x + 0.3f, z, z + 0.3f, 0f, Clear, Surface.Graphite); }
            }
            FloorLabel(works, "ROLL 3.5 m", new Vector2(-74f, -55f), 0f, Vector2.left);
            // A ledge at +7: out of reach from the floor (a double jump catches 6.5 m) and from a crate with a jump (2 +
            // 4.8 m), in reach from a crate with a double jump (2 + 6.5 m).
            Platform(works, "Crate Ledge (+7)", -136f, -116f, -40f, -26f, 0f, 7f, coping: Surface.Accent);
            FloorLabel(works, "UP 7.0 m: PUSH A CRATE", new Vector2(-112f, -33f), 0f, Vector2.left);
            PushableCrate(works, new Vector2(-104f, -34f), 0f, 2f);
            // A crate in an alcove against the north wall: it has to be pulled out.
            Block(works, "Alcove Wall", -98.6f, -98.3f, -25f, -20f, 0f, 2.5f, Surface.Panel);
            Block(works, "Alcove Wall", -95.7f, -95.4f, -25f, -20f, 0f, 2.5f, Surface.Panel);
            PushableCrate(works, new Vector2(-97f, -21.2f), 0f, 2f, speed: 0.8f);
            FloorLabel(works, "PULL", new Vector2(-97f, -27f), 0f, Vector2.up);
            AreaSign(works, new Vector2(-80f, -21f), 1.2f, Vector2.down, "P5", "Low Works", freeStanding: true);
        }

        // ---- P6 Moving Ground: a pit at -6 crossed on moving platforms and islands ----

        private static void BuildMovingGround(Transform root, Material material)
        {
            Transform ground = Zone(root, material, "P6 Moving Ground");
            Transform platforms = Group(ground, "Moving Platforms"), stairs = Group(ground, "Stairs");
            UnityEngine.Rect p = MovingGroundPit;
            Block(ground, "Pit Floor (-6)", p.xMin, p.xMax, p.yMin, p.yMax, PitFloor - 0.5f, PitFloor);
            // The pit's walls stop under the ground's slab (-1), whose edge is the pit's top.
            const float t = 0.5f, under = -1f;
            Block(ground, "Pit Wall (North)", p.xMin - t, p.xMax + t, p.yMax, p.yMax + t, PitFloor - 0.5f, under, Surface.Panel);
            Block(ground, "Pit Wall (South)", p.xMin - t, p.xMax + t, p.yMin - t, p.yMin, PitFloor - 0.5f, under, Surface.Panel);
            Block(ground, "Pit Wall (West)", p.xMin - t, p.xMin, p.yMin, p.yMax, PitFloor - 0.5f, under, Surface.Panel);
            Block(ground, "Pit Wall (East)", p.xMax, p.xMax + t, p.yMin, p.yMax, PitFloor - 0.5f, under, Surface.Panel);
            HazardStripes(ground, new Vector2(p.xMin, p.yMax), new Vector2(p.xMax, p.yMax), Vector2.up, 0f);
            HazardStripes(ground, new Vector2(p.xMin, p.yMin), new Vector2(p.xMax, p.yMin), Vector2.down, 0f);

            // Islands, standing up from the pit floor to ±0.
            Platform(ground, "Island I1 (±0)", -20f, -8f, -100f, -90f, PitFloor - 0.5f, 0f, coping: Surface.Accent);
            Platform(ground, "Island I2 (±0)", 10f, 22f, -100f, -90f, PitFloor - 0.5f, 0f, coping: Surface.Accent);
            Platform(ground, "Island I3 (±0)", 38f, 50f, -112f, -100f, PitFloor - 0.5f, 0f, coping: Surface.Accent);
            // The crossing: a shuttle out to I1 (stopping 4 m short of it), a turntable between I1 and I2, a shuttle east
            // to I3, a shuttle south from I3 (3 m short of the far side); and a lift up from the pit floor.
            MovingPlatform(platforms, "Shuttle North-South", new Vector3(-14f, 0f, -67f), new Vector3(8f, 0.5f, 8f), new Vector3(0f, 0f, -15f), 6f);
            MovingPlatform(platforms, "Turntable", new Vector3(2f, 0f, -95f), new Vector3(7f, 0.5f, 7f), Vector3.zero, 6f, spin: new Vector3(0f, 30f, 0f));
            MovingPlatform(platforms, "Shuttle East", new Vector3(26f, 0f, -106f), new Vector3(6f, 0.5f, 6f), new Vector3(9f, 0f, 0f), 5f);
            MovingPlatform(platforms, "Shuttle South", new Vector3(44f, 0f, -117f), new Vector3(6f, 0.5f, 6f), new Vector3(0f, 0f, -7f), 5f, phase: 0.5f);
            MovingPlatform(platforms, "Lift", new Vector3(2f, PitFloor + 0.01f, -125f), new Vector3(6f, 0.5f, 6f), new Vector3(0f, -PitFloor, 0f), 6f, pause: 1.5f);
            // Stairs back up from the pit floor at both ends.
            float flight = FlightLength(PitFloor, 0f);
            Flight(stairs, "Stair (Pit to North)", new Vector2(47f, p.yMax - flight), Vector2.up, 4f, PitFloor, 0f, PitFloor - 0.5f);
            Flight(stairs, "Stair (Pit to South)", new Vector2(-27f, p.yMin + flight), Vector2.down, 4f, PitFloor, 0f, PitFloor - 0.5f);
            FloorLabel(ground, "RIDE: SHUTTLE, TURNTABLE, SHUTTLE, SHUTTLE", new Vector2(-14f, -57f), 0f, Vector2.down);
            AreaSign(ground, new Vector2(-24f, -52f), 1.2f, Vector2.right, "P6", "Moving Ground", freeStanding: true);
        }

        // ---- The paths between zones, painted on the ground, and a park inside the circuit ----

        private static void BuildPaths(Transform root, Material material)
        {
            Transform paths = Zone(root, material, "Paths and Park");
            // P4's stair foot south to P5's north door; P5's east door to P6; P6's far side back to P0.
            GuideLine(paths, "Path (P4 to P5)", new[] { new Vector2(6f, 106f), new Vector2(-20f, 80f), new Vector2(-41f, 60f), new Vector2(-41f, -4f), new Vector2(-80f, -4f), new Vector2(-80f, -19f) }, 0f, Surface.Accent);
            GuideLine(paths, "Path (P5 to P6)", new[] { new Vector2(-59f, -45f), new Vector2(-24f, -45f), new Vector2(-14f, -55f) }, 0f, Surface.Accent);
            GuideLine(paths, "Path (P6 to P0)", new[] { new Vector2(44f, -134f), new Vector2(62f, -120f), new Vector2(62f, -30f), new Vector2(13f, -13f) }, 0f, Surface.Accent);
            // A park inside the circuit, away from every path.
            UnityEngine.Rect park = UnityEngine.Rect.MinMaxRect(30f, 16f, 116f, 128f);
            Lawns(paths, park, new[] { 0f }, 30, spacing: 2f, density: 0.6f);
            ScatterTrees(paths, park, 0f, 31, spread: 1.3f);
        }
    }
}
