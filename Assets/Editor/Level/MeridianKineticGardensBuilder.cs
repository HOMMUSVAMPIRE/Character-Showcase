using System.Collections.Generic;
using UnityEngine;
using static CharacterShowcase.EditorTools.Level.Graybox;

namespace CharacterShowcase.EditorTools.Level
{
    /// <summary>
    /// Builds the graybox of the Meridian Complex's Kinetic Gardens, the open-air traversal district, into the complex's
    /// scene (see MeridianComplexBuilder), from the blockout plan MeridianComplex-KineticGardens.png: level geometry
    /// only, in the material it's given. It sits north-west of the Concourse, in the same world coordinates, and joins
    /// it where the Concourse's north-west bridge ends.
    ///
    /// The district is laid out in the plan's own coordinates (x and z from -200 to -80) under a group moved into
    /// place (see PlanToWorld), so its pieces can be checked against the plan by number. The approach from the
    /// Concourse is in world coordinates. How the plan was read, and what was chosen where it was ambiguous, is in
    /// KineticGardens_Blockout_Notes.md beside the scene.
    /// </summary>
    public static class MeridianKineticGardensBuilder
    {
        /// <summary>Where the plan's coordinates are in the world: the plan's (-140, -200), its south entrance, is at (-80, 88).</summary>
        public static readonly Vector3 PlanToWorld = new(60f, 0f, 288f);

        // ---- The plan's dimensions (tunable: see the notes) ----

        private const float HeightStep = 4f;     // The primary platform height step.
        private const float Rise = 4f / 12f;     // 12 steps per 4 m (the plan's 0.4 m rise is more than the character can step).
        private const float Run = 0.8f;          // The plan's stair run.
        private const float StairWidth = 8f;     // Recovery paths at least 8 m wide.
        private const float BoundaryHeight = 4f, BoundaryThickness = 2f;
        private const float BeamWidth = 1f;      // Narrow beams on the advanced route.
        /// <summary>The district floor's top: a few centimetres proud of the ground, so the two don't flicker.</summary>
        public const float FloorTop = 0.05f;

        // Where the Concourse's north-west bridge ends (see MeridianConcourseBuilder): 71.5 m out along the diagonal, at +20.
        private static readonly Vector2 ConcourseNorthWest = new Vector2(-1f, 1f).normalized * 71.5f;
        private const float ConcourseNorthWestHeight = 20f;

        // The gentle route from the Concourse's +8 ring: 1:6 ramps (the plan's beginner grade), with a landing between.
        private const float GentleGrade = 6f, GentleLanding = 4f;
        // The forecourt in front of the south entrance (world x, top, z, and its size): where both routes come down.
        private static readonly Vector3 Forecourt = new(-70f, 0.05f, 76f);
        private static readonly Vector2 ForecourtSize = new(44f, 24f);

        /// <summary>Builds the Kinetic Gardens, and the approach to them from the Concourse, under the complex's root, all in the given material.</summary>
        public static void BuildInto(Transform complex, Material material)
        {
            Begin(material);
            StepRise = Rise;
            StepRun = Run;

            Transform gardens = Group(complex, "02 Kinetic Gardens");
            Transform district = Group(gardens, "District");
            district.localPosition = PlanToWorld;
            BuildBoundary(Group(district, "District Boundary"));
            Transform platforms = Group(district, "Platforms");
            Transform stairs = Group(district, "Stairs and Ramps");
            Transform traversal = Group(district, "Traversal (Advanced Route)");
            Transform bridges = Group(district, "Bridges");
            Transform rails = Group(district, "Guardrails");
            Transform connections = Group(district, "Connections");
            Transform route = Group(district, "Route Markers");
            BuildBandA(platforms, stairs, connections);
            BuildBandsBAndC(platforms, stairs, traversal, bridges, rails, connections, route);
            BuildBandsDAndE(platforms, stairs, traversal, route);
            BuildApproach(Group(gardens, "Approach from the Concourse"));
            // Lawns over the district's open ground and its platforms' tops (paving left round what stands on them and
            // along their edges), then trees over the open ground, where they don't crowd a platform, stair or route.
            UnityEngine.Rect plan = UnityEngine.Rect.MinMaxRect(-198f, -198f, -82f, -82f);
            Lawns(district, plan, new[] { FloorTop, HeightStep, 2f * HeightStep, 3f * HeightStep, 4f * HeightStep, 5f * HeightStep }, 3);
            ScatterTrees(district, plan, FloorTop, 2);
        }

        /// <summary>
        /// A platform over a rectangle of the plan (x from x0 to x1, z from z0 to z1), solid from the ground to its top,
        /// with an orange coping round its edge (the concept art's gardens).
        /// </summary>
        private static GameObject Platform(Transform parent, string name, float x0, float x1, float z0, float z1, float top) =>
            Graybox.Platform(parent, name, x0, x1, z0, z1, 0f, top, coping: Surface.Accent);

        /// <summary>
        /// The district's floor (±0, a few centimetres proud of the ground so the two don't flicker where they meet) and
        /// a low boundary wall round its 120 × 120 m, open on the south for the main entrance.
        /// </summary>
        private static void BuildBoundary(Transform boundary)
        {
            // Open where the Service Network's stairwells come up through it (their openings are in world coordinates).
            var openings = new List<UnityEngine.Rect>();
            foreach (UnityEngine.Rect r in MeridianServiceNetworkBuilder.GroundOpenings)
            {
                openings.Add(new UnityEngine.Rect(r.x - PlanToWorld.x, r.y - PlanToWorld.z, r.width, r.height));
            }
            SlabWithHoles(boundary, "Floor (±0)", UnityEngine.Rect.MinMaxRect(-200f, -200f, -80f, -80f), openings, -0.5f, FloorTop, Surface.Ground);
            float t = BoundaryThickness, h = BoundaryHeight;
            Block(boundary, "Wall (North)", -200f - t, -80f + t, -80f, -80f + t, 0f, h, Surface.Panel);
            Block(boundary, "Wall (West)", -200f - t, -200f, -200f - t, -80f, 0f, h, Surface.Panel);
            Block(boundary, "Wall (East)", -80f, -80f + t, -200f - t, -80f, 0f, h, Surface.Panel);
            Block(boundary, "Wall (South-West)", -200f - t, -150f, -200f - t, -200f, 0f, h, Surface.Panel);
            Block(boundary, "Wall (South-East)", -130f, -80f + t, -200f - t, -200f, 0f, h, Surface.Panel);
            AreaSign(boundary, new Vector2(-120f, -200f - t), 1.2f, Vector2.down, "02", "Kinetic Gardens"); // Beside the south entrance.
        }

        /// <summary>
        /// Band A (±0 to +4, the ground and recovery level, z -200 to -176): the entrance plaza at ±0, a raised platform at
        /// +4 to the south-east up a 1:6 ramp, and a stair up to the west terrace.
        /// </summary>
        private static void BuildBandA(Transform platforms, Transform stairs, Transform connections)
        {
            Marker(connections, "To Concourse (S, ±0)", new Vector2(-140f, -200f), 0f);
            // (The plan puts it at (-160, -180), under the ramp up to A2: it's moved to where the stairwell fits.)
            Marker(connections, "To Service Network (Below) 1", new Vector2(-143.6f, -173f), 0f);
            Marker(connections, "To Service Network (Below) 2", new Vector2(-100f, -120f), 0f);

            Platform(platforms, "A2 South-East Platform (+4)", -126f, -82f, -190f, -166f, HeightStep);
            const float ramp = HeightStep * 6f; // 1:6, about 9.5 degrees.
            Ramp(stairs, "Ramp (1:6, ±0 to +4)", new Vector2(-126f - ramp, -182f), Vector2.right, StairWidth, ramp, 0f, HeightStep);
            Flight(stairs, "Stair (±0 to +4, West)", new Vector2(-192f, -176f - 12f * Run), Vector2.up, StairWidth, 0f, HeightStep);
        }

        /// <summary>
        /// Bands B and C (+4 to +12, z -176 to -130): the west terraces at +4 and +8 (route point 2), a stepping platform
        /// at +4 across a gap, the central walk at +8 (route point 3), and the east terrace at +12 with the upper bridge
        /// east to the Ascension Spire.
        /// </summary>
        private static void BuildBandsBAndC(Transform platforms, Transform stairs, Transform traversal, Transform bridges, Transform rails,
            Transform connections, Transform route)
        {
            float flight = 12f * Run; // One 4 m flight's length.

            // West: +4, and up to +8 behind it.
            Platform(platforms, "B1 West Terrace (+4)", -198f, -160f, -176f, -150f, HeightStep);
            Marker(route, "Route 2 (+4)", new Vector2(-176f, -159f), HeightStep);
            Flight(stairs, "Stair (+4 to +8, West)", new Vector2(-192f, -150f - flight), Vector2.up, StairWidth, HeightStep, 2f * HeightStep);
            Platform(platforms, "C1 West Terrace (+8)", -198f, -160f, -150f, -124f, 2f * HeightStep);

            // Beginner route 2 to 3: across a gap (8 m) to a stepping platform, then up to the central walk.
            Platform(platforms, "B2 Stepping Platform (+4)", -152f, -136f, -170f, -158f, HeightStep);
            Flight(stairs, "Stair (+4 to +8, Central)", new Vector2(-144f, -158f), Vector2.up, StairWidth, HeightStep, 2f * HeightStep);
            Platform(platforms, "B3 Central Walk (+8)", -150f, -116f, -158f + flight, -136f, 2f * HeightStep);
            Marker(route, "Route 3 (+8)", new Vector2(-123f, -142f), 2f * HeightStep);
            // The central walk's west end faces the west terrace across a 10 m gap at +8: a jump (or dash) on the advanced route.
            Marker(traversal, "Gap (10 m, +8): B3 to C1", new Vector2(-155f, -140f), 2f * HeightStep);
            HazardStripes(traversal, new Vector2(-150f, -158f + flight), new Vector2(-150f, -136f), Vector2.right, 2f * HeightStep);
            HazardStripes(traversal, new Vector2(-160f, -150f), new Vector2(-160f, -124f), Vector2.left, 2f * HeightStep);

            // East: up to +12 from the central walk, and from the south-east platform by a stair along the east wall.
            Flight(stairs, "Stair (+8 to +12, East)", new Vector2(-116f, -140f), Vector2.right, StairWidth, 2f * HeightStep, 3f * HeightStep);
            Platform(platforms, "C2 East Terrace (+12)", -116f + flight, -92f, -162f, -136f, 3f * HeightStep);
            float eastStairTop = -166f + 24f * Run;
            Flight(stairs, "Stair (+4 to +12, East Wall)", new Vector2(-87f, -166f), Vector2.up, StairWidth - 2f, HeightStep, 3f * HeightStep);
            Platform(platforms, "C2 East Terrace (+12, North)", -92f, -82f, eastStairTop, -136f, 3f * HeightStep);
            Bridge(bridges, rails, connections, "To Ascension Spire (E, +12)", new Vector2(-82f, -140f), Vector2.right, StairWidth, 3f * HeightStep);
        }

        /// <summary>
        /// Bands D and E (+12 to +20, z -130 to -80): the west terraces at +12 and +16 (route point 5), the central
        /// platforms at +12 up the broad ramp (route point 4), the advanced route's dash island, narrow beam and ledge
        /// chain, and the goal viewpoint at +20 in the north-east (route point 6).
        /// </summary>
        private static void BuildBandsDAndE(Transform platforms, Transform stairs, Transform traversal, Transform route)
        {
            float flight = 12f * Run;
            float h8 = 2f * HeightStep, h12 = 3f * HeightStep, h16 = 4f * HeightStep, h20 = 5f * HeightStep;

            // West: up to +12 and +16.
            Flight(stairs, "Stair (+8 to +12, West)", new Vector2(-192f, -124f - flight), Vector2.up, StairWidth, h8, h12);
            Platform(platforms, "D1 West Terrace (+12)", -198f, -170f, -124f, -108f, h12);
            Flight(stairs, "Stair (+12 to +16, West)", new Vector2(-176f, -108f - flight), Vector2.up, StairWidth, h12, h16);
            Platform(platforms, "E1 North-West Terrace (+16)", -192f, -160f, -108f, -84f, h16);
            Marker(route, "Route 5 (+16)", new Vector2(-168f, -95f), h16);

            // Beginner route 3 to 4: a broad 1:4 ramp up to +12, then a short jump (4 m) to the north platform.
            const float ramp = 4f * 4f;
            Ramp(stairs, "Broad Ramp (1:4, +8 to +12)", new Vector2(-130f, -136f), Vector2.up, 12f, ramp, h8, h12);
            Platform(platforms, "D2 Central Platform (+12)", -140f, -120f, -136f + ramp, -108f, h12);
            Platform(platforms, "E2 North Platform (+12)", -148f, -118f, -104f, -86f, h12);
            Marker(route, "Route 4 (+12)", new Vector2(-133f, -96f), h12);

            // Advanced: a dash island between the west terrace and the central platform at +12 (gaps of 10 and 12 m).
            Platform(traversal, "D3 Dash Island (+12)", -160f, -152f, -122f, -112f, h12);
            // Advanced 5 to 4: a narrow beam out from the north-west terrace at +16, dropping to the north platform, beside a
            // wall to run along (planned: at least 4 m wide and 8 m high).
            BoxXZ(traversal, "Narrow Beam (+16)", new Vector2(-154f, -95f), new Vector2(12f, BeamWidth), h16 - 1f, h16, 0f, Surface.Steel);
            BoxXZ(traversal, "Wall-Run Wall (planned)", new Vector2(-154f, -101.5f), new Vector2(12f, 1f), 0f, h20, 0f, Surface.Panel);

            // Beginner route 4 to 6: up from the north platform to the viewpoint, by a stair, a landing at +16 and a stair
            // into the viewpoint itself.
            Flight(stairs, "Stair (+12 to +16, North)", new Vector2(-118f, -96f), Vector2.right, StairWidth, h12, h16);
            float landing = -118f + flight;
            Platform(stairs, "Landing (+16)", landing, -104f, -100f, -92f, h16);
            Flight(stairs, "Stair (+16 to +20, Viewpoint)", new Vector2(-104f, -96f), Vector2.right, StairWidth, h16, h20);
            float stairTop = -104f + flight;
            // The viewpoint at +20, around the stair coming up into it.
            Platform(platforms, "E3 Goal Viewpoint (+20, South)", -104f, -82f, -108f, -100f, h20);
            Platform(platforms, "E3 Goal Viewpoint (+20, North)", -104f, -82f, -92f, -82f, h20);
            Platform(platforms, "E3 Goal Viewpoint (+20, East)", stairTop, -82f, -100f, -92f, h20);
            Marker(route, "Route 6: Goal Viewpoint (+20)", new Vector2(-90f, -90f), h20);

            // Advanced 4 to 6: a ledge chain of columns up from the north platform to the viewpoint, 2 m higher each.
            float[] chain = { 14f, 16f, 18f };
            for (int i = 0; i < chain.Length; i++)
            {
                BoxXZ(traversal, $"Ledge Chain Column (+{chain[i]:0})", new Vector2(-115f + 4f * i, -88f), new Vector2(2f, 2f), 0f, chain[i], 0f, Surface.Graphite);
            }
        }

        /// <summary>
        /// The routes from the Concourse to the gardens' south entrance, both ending on a forecourt in front of it:
        /// the gentle route, down two 1:6 ramps from the Concourse's +8 ring with a landing at +4, the main way in;
        /// and the high route, down stairs from the end of the Concourse's +20 north-west bridge, with landings at +14
        /// and +8 (in the Concourse's stair profile).
        /// </summary>
        private static void BuildApproach(Transform approach)
        {
            Vector2 outward = ConcourseNorthWest.normalized, back = -outward;
            float yaw = Mathf.Atan2(outward.x, outward.y) * Mathf.Rad2Deg;

            // The gentle route.
            Transform gentle = Group(approach, "Gentle Route (from +8)");
            Vector2 top = MeridianConcourseBuilder.GardensRouteStart;
            float width = MeridianConcourseBuilder.GardensRouteWidth, flight = HeightStep * GentleGrade;
            Ramp(gentle, "Ramp (1:6, +8 to +4)", top, outward, width, flight, 2f * HeightStep, HeightStep);
            BoxXZ(gentle, "Landing (+4)", top + outward * (flight + GentleLanding / 2f), new Vector2(width, GentleLanding), 0f, HeightStep, yaw);
            Ramp(gentle, "Ramp (1:6, +4 to ±0)", top + outward * (flight + GentleLanding), outward, width, flight, HeightStep, Forecourt.y);

            // The high route.
            Transform high = Group(approach, "High Route (from +20)");
            float rise = StepRise, run = StepRun;
            StepRise = 0.3f;
            StepRun = 0.4f;
            float u = ConcourseNorthWest.magnitude;
            Vector2 At(float distance) => outward * distance;
            float y = ConcourseNorthWestHeight;
            foreach ((float to, string name) in new[] { (14f, "+20 to +14"), (8f, "+14 to +8"), (0f, "+8 to ±0") })
            {
                int steps = Mathf.CeilToInt((y - to) / StepRise - 1e-4f);
                float length = steps * StepRun;
                // Built from its foot, rising back toward the Concourse.
                Flight(high, $"Stair ({name})", At(u + length), back, StairWidth, to, y);
                u += length;
                y = to;
                if (to > 0f)
                {
                    BoxXZ(high, $"Landing (+{to:0})", At(u + 1.5f), new Vector2(StairWidth, 3f), 0f, to, yaw);
                    u += 3f;
                }
            }
            StepRise = rise;
            StepRun = run;

            // The forecourt, from the south entrance (the plan's z = -200, world z = 88) south over both routes' feet.
            BoxXZ(approach, "Forecourt (±0)", new Vector2(Forecourt.x, Forecourt.z), new Vector2(ForecourtSize.x, ForecourtSize.y), -0.5f, Forecourt.y);
        }
    }
}
