using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using static CharacterShowcase.EditorTools.Level.Graybox;

namespace CharacterShowcase.EditorTools.Level
{
    /// <summary>
    /// Builds the Level Playground: a small graybox scene with one of each piece the Meridian Complex is built from,
    /// made by the same Graybox pieces in the same material and environment (see GrayboxScene), so a change to a piece
    /// can be tried here in a few seconds before rebuilding the whole complex. Level geometry, lighting and markers
    /// only, like the complex's builders; its spawn marker is for Set Up Scene.
    ///
    /// Laid out on a 110 × 110 m ground (+Z north), round a spawn point at the middle facing north:
    /// - the start: an octagonal dais (a Prism) with bollards round it, and a chamfered column turned 45°, with accent
    ///   strips;
    /// - west, a terrace at +4 cut at 45° (a Prism over several box colliders), up a 1:4 ramp, guardrails round it;
    /// - north, a stair from the terrace to a walkway on columns at +8, two octagonal towers joined by a suspended
    ///   walkway (on trusses), a 6 m jump gap to a small platform, and a diagonal bridge out to a connection marker
    ///   (on I-beam columns, cyan accent posts at its ends, a sign gantry over its start); the towers stand on chamfered
    ///   columns with accent strips, a floodlight stands on the east tower, and hazard stripes mark the gap's edges;
    /// - south-east, a walled building with a doorway and a window (walls with openings, pilasters and frames), a
    ///   crate, a shelf, a utility station, door lights over the openings, an area sign by the door, and a catwalk on
    ///   a braced frame and brackets, up a stair;
    /// - east, underground at -6: a stairwell down from the ground, a passage, a lit room and a ventilation shaft
    ///   (Cells, their ceiling panels, strip lights and lights, pipe runs, a utility station, shaft walls and the
    ///   ground's openings);
    /// - south-west, one of each of the Calibration Annex's tests: steps either side of the step limit, a ledge, a
    ///   slope past the slope limit, a tunnel to crouch through, and a narrow beam beside a wall-run wall (the
    ///   platforms with copings round their edges, the lanes marked out with corner brackets and their measures, the
    ///   edges to jump from with hazard stripes);
    /// - south, one of each mechanic: a lift (a moving platform), a slide gate, and a timed 20 m run between two
    ///   timing gates, and in the building a crate to push (fixtures Set Up Scene makes work);
    /// - placeholders and markers for mechanics to come, as the complex has them.
    /// </summary>
    public static class LevelPlaygroundBuilder
    {
        public const string ScenePath = "Assets/Scenes/LevelPlayground/LevelPlayground.unity";

        // ---- The layout (tunable) ----

        private const float GroundHalf = 55f;
        private const float Terrace = 4f, Towers = 8f; // The terrace's and the towers' heights.
        private const float Basement = -6f;            // The underground floor.
        private const float GroundUnderside = -1f;     // The ground slab's underside: open cells' walls rise to it.
        private const float StairWidth = 6f;
        private const float TowerApothem = 8f;         // Wide enough across each flat for a stair-wide bridge.

        internal static readonly GrayboxScene Scene = new("Level Playground", ScenePath,
            ("the start", BuildStart),
            ("the terrace and ramp", BuildTerrace),
            ("the towers and bridges", BuildTowers),
            ("the building", BuildBuilding),
            ("the underground", BuildUnderground),
            ("the movement tests", BuildMovementTests),
            ("the mechanics", BuildMechanics),
            ("the ground", (root, material) => { Begin(material); Ground(root, GroundHalf, GroundOpenings); }),
            ("the trees", BuildTrees));

        /// <summary>
        /// Lawns and trees along the playground's east and north edges, where there's room for them (see Lawns and
        /// ScatterTrees), and a lawn on the terrace's top.
        /// </summary>
        private static void BuildTrees(Transform root, Material material)
        {
            Transform trees = Section(root, material, "Lawns and Trees");
            UnityEngine.Rect east = UnityEngine.Rect.MinMaxRect(38f, -52f, 54f, 52f), north = UnityEngine.Rect.MinMaxRect(-52f, 40f, 38f, 54f);
            Lawns(trees, east, new[] { 0f }, 0);
            Lawns(trees, north, new[] { 0f }, 1);
            Lawns(trees, UnityEngine.Rect.MinMaxRect(-40f, -12f, -22f, 12f), new[] { Terrace }, 2);
            ScatterTrees(trees, east, 0f, 0);
            ScatterTrees(trees, north, 0f, 1);
        }

        /// <summary>Builds the playground in the background (see LevelBuildWindow), with a window showing its progress. Opens the scene when it's done.</summary>
        [MenuItem("Tools/Character Showcase/Level/Build Level Playground")]
        public static void BuildFromMenu() => LevelBuildWindow.BuildFromMenu(Scene);

        /// <summary>For -executeMethod: the whole build at once, waiting for the lighting bake.</summary>
        public static void BuildFromCommandLine() => Scene.Build();

        /// <summary>A section of the playground: the pieces reset to their default dimensions, under a group of its own.</summary>
        private static Transform Section(Transform root, Material material, string name)
        {
            Begin(material);
            return Group(root, name);
        }

        /// <summary>Where the spawn point is, a dais and a turned column beside it.</summary>
        private static void BuildStart(Transform root, Material material)
        {
            Transform start = Section(root, material, "Start");
            // Just above the ground, so a character settles onto it rather than starting in contact with it.
            SpawnMarker(Group(start, "Spawn Points"), "Start", new Vector2(0f, -10f), 0.05f, 0f);
            Prism(start, "Dais (+0.6)", Offset(Octagon(3f), new Vector2(8f, -4f)), 0f, 0.6f);
            Bollards(start, Offset(Octagon(5f), new Vector2(8f, -4f)), 0f);
            GuideLine(start, "Guide Ring (Orange)", Offset(Octagon(4.2f), new Vector2(8f, -4f)), 0f, Surface.Accent, closed: true);
            Planter(start, "Planter", -5f, -2.6f, -7f, -4.6f, 0f, tree: true, new System.Random(Settings.foliage.seed));
            Column(start, "Column (Turned 45°)", new Vector2(4f, 8f), new Vector2(2f, 2f), 0f, 4f, 45f, accent: true);
        }

        // ---- West: the terrace ----

        // The terrace at +4, its north-east corner cut at 45°; the ramp comes up to its east edge, the stair to the
        // towers leaves its north edge.
        private static readonly Vector2[] TerraceShape = Clip(Rect(-38f, -10f, -24f, 10f), 1f, 1f, -18f);
        private static readonly Vector2 RampTop = new(-24f, -4f), TowerStairFoot = new(-33f, 10f);

        private static void BuildTerrace(Transform root, Material material)
        {
            Transform terrace = Section(root, material, "Terrace (+4)");
            Prism(terrace, "Terrace (+4)", TerraceShape, 0f, Terrace);
            // 1:4, the steepest the level's ramps go.
            Ramp(terrace, "Ramp (1:4, ±0 to +4)", RampTop + Vector2.right * (4f * Terrace), Vector2.left, 8f, 4f * Terrace, 0f, Terrace);
            RailAround(Group(terrace, "Guardrails"), TerraceShape, Terrace, (RampTop, 8f), (TowerStairFoot, StairWidth));
        }

        // ---- North: the towers and bridges ----

        /// <summary>An octagonal platform on a column, and where its guardrail is open.</summary>
        private sealed class Tower
        {
            public string Name;
            public Vector2 Centre;
            public float Apothem;
            public readonly List<(Vector2 centre, float width)> Openings = new();
            public Vector2 Flat(Vector2 direction) => Centre + direction.normalized * Apothem;
            public float Side => 2f * Apothem * Mathf.Tan(22.5f * Mathf.Deg2Rad);
        }

        private static void BuildTowers(Transform root, Material material)
        {
            Transform towers = Section(root, material, "Towers and Bridges (+8)");
            Transform rails = Group(towers, "Guardrails"), connections = Group(towers, "Connections"), future = Group(towers, "Future Mechanics");
            BridgeLength = 12f;

            var west = new Tower { Name = "West Tower (+8)", Centre = new Vector2(-33f, 30f), Apothem = TowerApothem };
            var east = new Tower { Name = "East Tower (+8)", Centre = new Vector2(-3f, 30f), Apothem = TowerApothem };
            var jump = new Tower { Name = "Jump Platform (+8)", Centre = east.Flat(Vector2.up) + Vector2.up * (6f + 3f), Apothem = 3f };

            // Up from the terrace by a stair, on along a walkway on columns to the west tower.
            float stairTop = TowerStairFoot.y + FlightLength(Terrace, Towers);
            Flight(towers, "Stair (+4 to +8)", TowerStairFoot, Vector2.up, StairWidth, Terrace, Towers);
            Vector2 westSouth = west.Flat(Vector2.down);
            Walkway(Group(towers, "Walkway (+8)"), rails, new Vector2(TowerStairFoot.x, stairTop), Vector2.up, westSouth.y - stairTop, StairWidth, Towers);
            west.Openings.Add((westSouth, StairWidth));

            // Between the towers: a suspended walkway (no columns).
            Vector2 from = west.Flat(Vector2.right), to = east.Flat(Vector2.left);
            Walkway(Group(towers, "Suspended Walkway (+8)"), rails, from, Vector2.right, Vector2.Distance(from, to), StairWidth, Towers, suspended: true);
            west.Openings.Add((from, StairWidth));
            east.Openings.Add((to, StairWidth));

            // A measured gap, open in both guardrails.
            Vector2 gapFrom = east.Flat(Vector2.up), gapTo = jump.Flat(Vector2.down);
            east.Openings.Add((gapFrom, east.Side));
            jump.Openings.Add((gapTo, jump.Side));
            Marker(future, "Gap (6 m, +8): East Tower to Jump Platform", (gapFrom + gapTo) / 2f, Towers);
            foreach ((Tower tower, Vector2 flat, Vector2 centre) in new[] { (east, Vector2.up, gapFrom), (jump, Vector2.down, gapTo) })
            {
                Vector2 half = Vector2.right * (tower.Side / 2f);
                HazardStripes(future, centre - half, centre + half, -flat, Towers);
            }

            // A bridge out from a diagonal flat, to where another area would join.
            Vector2 northEast = new Vector2(1f, 1f).normalized;
            Bridge(towers, rails, connections, "To Next Area (NE, +8)", east.Flat(northEast), northEast, StairWidth, Towers);
            east.Openings.Add((east.Flat(northEast), StairWidth));

            foreach (Tower tower in new[] { west, east, jump })
            {
                Transform t = Group(towers, tower.Name);
                Vector2[] deck = Offset(Octagon(tower.Apothem), tower.Centre);
                Prism(t, "Deck", deck, Towers - Deck, Towers);
                float column = tower.Apothem / 2f;
                Column(t, "Column", tower.Centre, new Vector2(column, column), 0f, Towers - Deck, accent: true, capped: false);
                RailAround(rails, deck, Towers, tower.Openings.ToArray());
            }
            Floodlight(Group(towers, "Floodlight"), east.Centre, Towers, (to - east.Centre).normalized);
            Banner(towers, east.Centre + Vector2.down * (east.Apothem / 4f), Vector2.down, Towers - Deck - 0.5f, 4f);

            BoxXZ(future, "Rope Anchor (+8)", west.Centre + Vector2.left * (west.Apothem - 2f), new Vector2(1f, 1f), Towers, Towers + 4f, 0f, Surface.Graphite);
            Marker(future, "Rope Swing (future): from the West Tower's anchor", west.Centre, Towers);
        }

        // ---- South-east: the building ----

        private static void BuildBuilding(Transform root, Material material)
        {
            Transform building = Section(root, material, "Building");
            Transform walls = Group(building, "Walls"), objects = Group(building, "Objects");
            const float x0 = 14f, x1 = 34f, z0 = -36f, z1 = -16f, top = 6f, t = 1f, h = t / 2f;
            // The west and east walls run the full length, the north and south between them, so none overlap.
            Wall(walls, "Wall (West)", new Vector2(x0, z0 - h), new Vector2(x0, z1 + h), 0f, top, t,
                (new Vector2(x0, (z0 + z1) / 2f), 4f, 0f, 3f));     // A doorway, on the ground.
            Wall(walls, "Wall (East)", new Vector2(x1, z0 - h), new Vector2(x1, z1 + h), 0f, top, t);
            Wall(walls, "Wall (North)", new Vector2(x0 + h, z1), new Vector2(x1 - h, z1), 0f, top, t,
                (new Vector2((x0 + x1) / 2f, z1), 4f, 1.5f, 3.5f)); // A window, filled in below and above.
            Wall(walls, "Wall (South)", new Vector2(x0 + h, z0), new Vector2(x1 - h, z0), 0f, top, t);

            // A catwalk at +4 along the south wall, held by brackets at the wall and a braced frame on the floor, up a
            // stair along the east wall.
            const float walk = 4f, walkZ0 = z0 + h, walkZ1 = walkZ0 + 3f, stairX = x1 - h - 1.25f;
            Transform catwalk = Group(building, "Catwalk (+4)");
            Block(catwalk, "Catwalk Deck (+4)", x0 + h, x1 - h, walkZ0, walkZ1, walk - Deck, walk, Surface.Steel);
            Flight(catwalk, "Stair (to the Catwalk)", new Vector2(stairX, walkZ1 + FlightLength(0f, walk)), Vector2.down, 2.5f, 0f, walk);
            Rail(Group(catwalk, "Guardrails"), new Vector2(x0 + h, walkZ1), new Vector2(x1 - h, walkZ1), walk, (new Vector2(stairX, walkZ1), 2.5f));
            BracedFrame(catwalk, "Braced Frame", new Vector2(28f, walkZ0), new Vector2(28f, walkZ1), 0f, walk - Deck);
            foreach (float x in new[] { 20f, 31f }) { Bracket(catwalk, new Vector2(x, walkZ0), Vector2.up, walk - Deck); }

            PushableCrate(objects, new Vector2(28f, -28f), 0f, 2f);
            UtilityStation(objects, new Vector2(x1 - h, -22f), Vector2.left, 0f); // On the east wall, between its pilasters.
            Block(objects, "Shelf", 17f, 23f, -34.5f, -33f, 0f, 2.5f, Surface.Graphite);
            AreaSign(building, new Vector2(x0 - h, -30.5f), 1.2f, Vector2.left, "00", "Level Playground"); // Beside the door.
            Banner(building, new Vector2(19.5f, z1 + h), Vector2.up, top - 0.4f, 3.6f, "00"); // On the north wall, beside the window.
        }

        // ---- East: the underground ----

        /// <summary>The underground's rooms and passages, tiling without overlaps.</summary>
        private static List<Cell> Layout() => new()
        {
            new Cell("Stairwell", 20f, 14f, 26f, 22f, Basement, 0f, open: true),
            new Cell("Passage", 20f, 6f, 26f, 14f, Basement, Basement + 4f),
            // Its ceiling slab's top meets the ground's underside.
            new Cell("Room", 16f, -6f, 30f, 6f, Basement, GroundUnderside - 0.5f),
            new Cell("Ventilation Shaft", 30f, -3f, 36f, 3f, Basement, 0f, open: true),
        };

        /// <summary>Where the underground comes up through the ground: its open cells.</summary>
        private static IEnumerable<UnityEngine.Rect> GroundOpenings
        {
            get
            {
                foreach (Cell cell in Layout()) { if (cell.Open) { yield return cell.Area; } }
            }
        }

        private static void BuildUnderground(Transform root, Material material)
        {
            Transform underground = Section(root, material, "Underground (-6)");
            List<Cell> cells = Layout();
            Cells(Group(underground, "Rooms and Passages"), cells, new UnityEngine.Rect[0], GroundUnderside);
            CellLighting(Group(underground, "Lighting"), cells);
            Transform utilities = Group(underground, "Utilities");
            CellUtilities(utilities, cells);
            UtilityStation(utilities, new Vector2(18f, 6f), Vector2.down, Basement); // The room's north wall, beside the passage.

            UnityEngine.Rect stairwell = cells[0].Area, shaft = cells[3].Area;
            Flight(underground, "Stair (-6 to ±0)", new Vector2(stairwell.center.x, stairwell.yMin), Vector2.up, StairWidth, Basement, 0f, Basement);
            Vector2 stairTop = new(stairwell.center.x, stairwell.yMax);
            RailAround(Group(underground, "Guardrails"), Rect(stairwell.xMin, stairwell.yMin, stairwell.xMax, stairwell.yMax), 0f, (stairTop, StairWidth));
            ShaftWalls(underground, "Ventilation Shaft (to +6)", shaft.xMin, shaft.yMin, shaft.xMax, shaft.yMax, GroundUnderside, 6f);
            Marker(Group(underground, "Future Mechanics"), "Ladder (future): up the ventilation shaft", shaft.center, Basement);
        }

        // ---- South-west: the movement tests ----

        private static void BuildMovementTests(Transform root, Material material)
        {
            Transform tests = Section(root, material, "Movement Tests");

            // Steps either side of the character's 0.35 m step height, and a ledge to catch and climb.
            Block(tests, "Step 0.30 m", -40f, -36f, -24f, -21f, 0f, 0.3f);
            Block(tests, "Step 0.40 m", -34f, -30f, -24f, -21f, 0f, 0.4f);
            Platform(tests, "Ledge 2.0 m", -28f, -24f, -24f, -20f, 0f, 2f);
            // Each marked out on the floor, with what it measures in front of it.
            foreach ((float x, float z1, string value) in new[] { (-40f, -21f, "0.30 m"), (-34f, -21f, "0.40 m"), (-28f, -20f, "2.0 m") })
            {
                CornerBrackets(tests, x, x + 4f, -24f, z1, 0f);
                FloorLabel(tests, value, new Vector2(x + 2f, -25.6f), 0f, Vector2.up);
            }

            // A 40° slope, past the slope limit, up to a 3 m deck.
            const float slopeRise = 3f;
            float slope = slopeRise / Mathf.Tan(40f * Mathf.Deg2Rad);
            Platform(tests, "Slope Test Deck (+3)", -20f, -16f, -32f, -26f, 0f, slopeRise);
            Ramp(tests, "Slope 40°", new Vector2(-18f, -26f + slope), Vector2.down, 4f, slope, 0f, slopeRise);
            FloorLabel(tests, "40°", new Vector2(-18f, -26f + slope + 1.4f), 0f, Vector2.down);

            // A tunnel 1.5 m clear: too low to stand in. Its walls stop under the ceiling, so none overlap.
            const float clear = 1.5f;
            Block(tests, "Tunnel Wall (South)", -14f, -8f, -24.5f, -24f, 0f, clear);
            Block(tests, "Tunnel Wall (North)", -14f, -8f, -21f, -20.5f, 0f, clear);
            Block(tests, "Tunnel Ceiling (1.5 m clear)", -14f, -8f, -24.5f, -20.5f, clear, clear + 0.5f);
            FloorLabel(tests, "1.5 m", new Vector2(-15.4f, -22.5f), 0f, Vector2.right);

            // A narrow beam between two blocks at +4 (up to the first by a stair), and a wall to run along beside it.
            Platform(tests, "Beam Start (+4)", -38f, -34f, -40f, -36f, 0f, 4f);
            Platform(tests, "Beam End (+4)", -24f, -20f, -40f, -36f, 0f, 4f);
            Block(tests, "Narrow Beam (1 m, +4)", -34f, -24f, -38.5f, -37.5f, 3f, 4f, Surface.Steel);
            HazardStripes(tests, new Vector2(-34f, -40f), new Vector2(-34f, -36f), Vector2.left, 4f);
            HazardStripes(tests, new Vector2(-24f, -40f), new Vector2(-24f, -36f), Vector2.right, 4f);
            Flight(tests, "Stair (Beam Start)", new Vector2(-36f, -36f + FlightLength(0f, 4f)), Vector2.down, 4f, 0f, 4f);
            Ruler(tests, "Ruler (Beam)", new Vector2(-33.5f, -35.2f), Vector2.right, 9f, 0f);
            Block(tests, "Wall-Run Wall (+8)", -33f, -23f, -33f, -32f, 0f, 8f, Surface.Panel);
        }

        // ---- South: one of each mechanic (fixtures the scene's game setup makes work: see Graybox.Mechanics) ----

        private static void BuildMechanics(Transform root, Material material)
        {
            Transform mechanics = Section(root, material, "Mechanics");
            // A lift up to a landing at +4.
            MovingPlatform(mechanics, "Lift", new Vector3(-34f, 0.05f, -48f), new Vector3(4f, 0.5f, 4f), new Vector3(0f, 4f, 0f), 6f, pause: 1.5f);
            Platform(mechanics, "Lift Landing (+4)", -32f, -28f, -50f, -46f, 0f, 4f, coping: Surface.Accent);
            // A low beam to slide or crouch under.
            LowBeam(mechanics, "Slide Gate", new Vector2(-20f, -51f), new Vector2(-20f, -45f), 0f, 1.2f);
            FloorLabel(mechanics, "1.2 m", new Vector2(-22.5f, -48f), 0f, Vector2.right);
            // A 20 m timed run east, its times on the finish gate.
            TimingGate(mechanics, "Playground", "start", new Vector2(-12f, -51f), new Vector2(-12f, -45f), 0f, Vector2.right, distance: 20f);
            TimingGate(mechanics, "Playground", "finish", new Vector2(8f, -51f), new Vector2(8f, -45f), 0f, Vector2.right, distance: 20f, display: true);
        }
    }
}
