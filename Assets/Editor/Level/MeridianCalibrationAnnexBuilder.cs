using UnityEngine;
using static CharacterShowcase.EditorTools.Level.Graybox;

namespace CharacterShowcase.EditorTools.Level
{
    /// <summary>
    /// Builds the graybox of the Meridian Complex's Calibration Annex (07), the measured test ground south-west of the
    /// Concourse, into the complex's scene (see MeridianComplexBuilder), from the blockout sheet
    /// MeridianComplex-RemainingAreas.png: level geometry only, in the material it's given. Laid out in world
    /// coordinates.
    ///
    /// A walled yard of repeatable tests for a character controller, each a row of measured variations: step heights
    /// either side of the character's step limit, slopes from 10° to 60°, ledge heights, jump gaps from 2 to 20 m,
    /// tunnels with falling ceilings, a jump lane from ±0 to +16, a wall-run test to +20 and a ledge-climb face to +12.
    /// The Concourse's south-west bridge (+12) joins its arrival platform. How the sheet was read is in
    /// CalibrationAnnex_Blockout_Notes.md beside the scene.
    /// </summary>
    public static class MeridianCalibrationAnnexBuilder
    {
        // ---- The sheet's dimensions, and the tests' measures (tunable: see the notes) ----

        private const float X0 = -140f, X1 = -55f, Z0 = -155f, Z1 = -55f;  // The yard.
        private const float FloorTop = 0.05f;
        private const float WallHeight = 4f, WallThickness = 2f;
        private const float Arrival = 12f, ArrivalLanding = 6f, StairWidth = 8f;
        private const float LaneWidth = 4f;

        private static readonly float[] StepHeights = { 0.1f, 0.2f, 0.3f, 0.35f, 0.4f, 0.5f };
        private static readonly float[] SlopeAngles = { 10f, 20f, 30f, 40f, 50f, 60f };
        private const float SlopeRise = 3f;
        private static readonly float[] LedgeHeights = { 1f, 1.5f, 2f, 2.5f, 3f, 4f };
        private static readonly float[] JumpGaps = { 2f, 4f, 6f, 8f, 10f, 12f, 16f, 20f };
        private const float Runway = 4f;
        private static readonly float[] TunnelHeights = { 3f, 2.5f, 2f, 1.5f, 1f };
        private const float WallRunPlatform = 8f, WallRunGap = 16f, WallRunWall = 20f;

        /// <summary>Builds the Calibration Annex under the complex's root, all in the given material.</summary>
        public static void BuildInto(Transform complex, Material material)
        {
            Begin(material);
            Transform annex = Group(complex, "07 Calibration Annex");
            Transform yard = Group(annex, "Yard"), arrival = Group(annex, "Arrival"), rails = Group(annex, "Guardrails");
            Transform connections = Group(annex, "Connections"), future = Group(annex, "Future Mechanics");

            // The yard: its floor and a low wall round it, open on the north and east, with the arrival platform in its north-east corner.
            Block(yard, "Floor (±0)", X0, X1, Z0, Z1, -0.5f, FloorTop);
            const float t = WallThickness, open0 = -100f, open1 = -90f, corner = -74f;
            Block(yard, "Wall (West)", X0, X0 + t, Z0, Z1, 0f, WallHeight, Surface.Panel);
            Block(yard, "Wall (South)", X0 + t, X1, Z0, Z0 + t, 0f, WallHeight, Surface.Panel);
            Block(yard, "Wall (North, West)", X0 + t, open0, Z1 - t, Z1, 0f, WallHeight, Surface.Panel);
            Block(yard, "Wall (North, East)", open1, corner, Z1 - t, Z1, 0f, WallHeight, Surface.Panel);
            Block(yard, "Wall (East, South)", X1 - t, X1, Z0 + t, open0, 0f, WallHeight, Surface.Panel);
            Block(yard, "Wall (East, North)", X1 - t, X1, open1, corner, 0f, WallHeight, Surface.Panel);
            Marker(connections, "Entrance (N, ±0)", new Vector2((open0 + open1) / 2f, Z1), FloorTop);
            Marker(connections, "Entrance (E, ±0)", new Vector2(X1, (open0 + open1) / 2f), FloorTop);
            AreaSign(yard, new Vector2(-106f, Z1), 1.2f, Vector2.up, "07", "Calibration Annex"); // Beside the north entrance, on the wall's face.

            BuildArrival(arrival, rails, connections, future, corner);
            BuildStepAndLedgeTests(Group(annex, "Step and Ledge Tests"));
            BuildSlopeTest(Group(annex, "Slope Test"));
            BuildTunnelTest(Group(annex, "Tunnel Test"));
            BuildJumpTest(Group(annex, "Jump Distance Test"));
            BuildJumpLane(Group(annex, "Jump Lane (±0 to +16)"));
            BuildWallRunTest(Group(annex, "Wall-Run Test"), future);
        }

        /// <summary>
        /// The arrival platform (+12), at the end of the Concourse's south-west bridge (extended to it), cut square to
        /// the bridge; down from it by a stair, with a landing at +6. Its south face is the ledge-climb face.
        /// </summary>
        private static void BuildArrival(Transform arrival, Transform rails, Transform connections, Transform future, float corner)
        {
            Vector2 sw = new Vector2(-1f, -1f).normalized;
            Vector2 bridgeEnd = sw * 57.5f, platformEdge = new(X1, Z1);
            Walkway(Group(arrival, "Bridge (from the Concourse)"), rails, bridgeEnd, sw, Vector2.Distance(bridgeEnd, platformEdge), 8f, Arrival);
            Vector2[] platform = Clip(Rect(corner, corner, -50f, -50f), 1f, 1f, platformEdge.x + platformEdge.y);
            Prism(arrival, "Arrival Platform (+12)", platform, 0f, Arrival);
            const float stairZ = -62f;
            float upper = FlightLength(ArrivalLanding, Arrival), lower = FlightLength(FloorTop, ArrivalLanding), landing = 3f;
            Flight(arrival, "Stair (+12 to +6)", new Vector2(corner - upper, stairZ), Vector2.right, StairWidth, ArrivalLanding, Arrival);
            Platform(arrival, "Landing (+6)", corner - upper - landing, corner - upper, stairZ - StairWidth / 2f, stairZ + StairWidth / 2f, 0f, ArrivalLanding);
            Flight(arrival, "Stair (+6 to ±0)", new Vector2(corner - upper - landing - lower, stairZ), Vector2.right, StairWidth, FloorTop, ArrivalLanding);
            RailAround(rails, platform, Arrival, (platformEdge, 8f), (new Vector2(corner, stairZ), StairWidth));
            Marker(connections, "To Concourse (NE, +12)", platformEdge, Arrival);
            Marker(future, "Ledge Climb (future): the arrival platform's south face, ±0 to +12", new Vector2(-62f, corner), FloorTop);
        }

        /// <summary>Single steps of rising height, either side of the character's step limit; and ledges of rising height.</summary>
        private static void BuildStepAndLedgeTests(Transform tests)
        {
            for (int i = 0; i < StepHeights.Length; i++)
            {
                float x = -132f + 6f * i;
                Block(tests, $"Step {StepHeights[i]:0.00} m", x, x + LaneWidth, -66f, -63f, 0f, FloorTop + StepHeights[i]);
                CornerBrackets(tests, x, x + LaneWidth, -66f, -63f, FloorTop);
                FloorLabel(tests, $"{StepHeights[i]:0.00} m", new Vector2(x + LaneWidth / 2f, -67.6f), FloorTop, Vector2.up);
            }
            for (int i = 0; i < LedgeHeights.Length; i++)
            {
                float x = -132f + 6f * i;
                Platform(tests, $"Ledge {LedgeHeights[i]:0.0} m", x, x + LaneWidth, -78f, -74f, 0f, FloorTop + LedgeHeights[i]);
                CornerBrackets(tests, x, x + LaneWidth, -78f, -74f, FloorTop);
                FloorLabel(tests, $"{LedgeHeights[i]:0.0} m", new Vector2(x + LaneWidth / 2f, -72.4f), FloorTop, Vector2.down);
            }
        }

        /// <summary>Ramps of rising steepness, each 3 m high, up to a shared deck; the steepest are past the character's slope limit.</summary>
        private static void BuildSlopeTest(Transform test)
        {
            const float deckZ1 = -104f, deckZ0 = -110f;
            for (int i = 0; i < SlopeAngles.Length; i++)
            {
                float length = SlopeRise / Mathf.Tan(SlopeAngles[i] * Mathf.Deg2Rad);
                float x = -130f + 6f * i;
                Ramp(test, $"Slope {SlopeAngles[i]:0}°", new Vector2(x, deckZ1 + length), Vector2.down, LaneWidth, length, FloorTop, FloorTop + SlopeRise);
                FloorLabel(test, $"{SlopeAngles[i]:0}°", new Vector2(x, deckZ1 + length + 1.4f), FloorTop, Vector2.down);
            }
            float top = FloorTop + SlopeRise;
            Platform(test, "Slope Test Deck (+3)", -132f, -98f, deckZ0, deckZ1, 0f, top);
            Flight(test, "Stair (Slope Test Deck)", new Vector2(-132f - FlightLength(FloorTop, top), (deckZ0 + deckZ1) / 2f), Vector2.right, 6f, FloorTop, top);
        }

        /// <summary>A straight tunnel, 3 m wide, whose ceiling drops every 6 m: from 3 m clear down to 1 m.</summary>
        private static void BuildTunnelTest(Transform test)
        {
            const float x0 = -95f, z0 = -87f, z1 = -84f, section = 6f, wall = 0.5f;
            float top = FloorTop + TunnelHeights[0] + wall;
            float x1 = x0 + section * TunnelHeights.Length;
            Block(test, "Tunnel Wall (South)", x0, x1, z0 - wall, z0, 0f, top);
            Block(test, "Tunnel Wall (North)", x0, x1, z1, z1 + wall, 0f, top);
            for (int i = 0; i < TunnelHeights.Length; i++)
            {
                float x = x0 + section * i;
                Block(test, $"Tunnel Ceiling ({TunnelHeights[i]:0.0} m clear)", x, x + section, z0 - wall, z1 + wall, FloorTop + TunnelHeights[i], top);
            }
        }

        /// <summary>
        /// A runway at +4 with landings across gaps from 2 to 20 m, one lane each: fall short and land on the floor
        /// below. Up to the runway by a stair at its west end.
        /// </summary>
        private static void BuildJumpTest(Transform test)
        {
            const float x0 = -132f, x1 = -92f, z0 = -122f, z1 = -114f, landing = 6f;
            float top = Runway;
            Platform(test, "Runway (+4)", x0, x1, z0, z1, 0f, top);
            Flight(test, "Stair (Runway)", new Vector2(x0 - FlightLength(FloorTop, top), (z0 + z1) / 2f), Vector2.right, 6f, FloorTop, top);
            HazardStripes(test, new Vector2(x0, z0), new Vector2(x1, z0), Vector2.up, top);
            // A ruler on the floor beside the lanes, out from the runway's edge, to read the gaps by.
            Ruler(test, "Ruler (Jump Distance)", new Vector2(x0 - 1.5f, z0), Vector2.down, 26f, FloorTop);
            for (int i = 0; i < JumpGaps.Length; i++)
            {
                float x = -132f + 5f * i;
                Platform(test, $"Landing (Gap {JumpGaps[i]:0} m)", x, x + LaneWidth, z0 - JumpGaps[i] - landing, z0 - JumpGaps[i], 0f, top);
                HazardStripes(test, new Vector2(x, z0 - JumpGaps[i]), new Vector2(x + LaneWidth, z0 - JumpGaps[i]), Vector2.down, top);
                FloorLabel(test, $"{JumpGaps[i]:0} m", new Vector2(x + LaneWidth / 2f, z0 - JumpGaps[i] - landing / 2f), top, Vector2.down);
            }
        }

        /// <summary>Blocks 4 m across, 2 m apart, each 2 m higher than the last: a jump lane from the floor up to +16.</summary>
        private static void BuildJumpLane(Transform lane)
        {
            for (int k = 1; k <= 8; k++)
            {
                float z = -94f - 6f * (k - 1);
                Platform(lane, $"Jump Lane Block (+{2 * k})", -66f, -62f, z - 2f, z + 2f, 0f, 2f * k);
            }
        }

        /// <summary>Two platforms at +8 across a 16 m gap, beside a wall rising to +20 for running along; up to the first by a stair.</summary>
        private static void BuildWallRunTest(Transform test, Transform future)
        {
            const float x0 = -88f, x1 = -80f, near0 = -100f, near1 = -92f;
            float far1 = near0 - WallRunGap, far0 = far1 - 8f;
            Platform(test, "Wall-Run Start (+8)", x0, x1, near0, near1, 0f, WallRunPlatform);
            Platform(test, "Wall-Run Finish (+8)", x0, x1, far0, far1, 0f, WallRunPlatform);
            HazardStripes(test, new Vector2(x0, near0), new Vector2(x1, near0), Vector2.up, WallRunPlatform);
            HazardStripes(test, new Vector2(x0, far1), new Vector2(x1, far1), Vector2.down, WallRunPlatform);
            Block(test, "Wall-Run Wall (+20)", x1 + 1f, x1 + 2f, far1 - 2f, near0 + 2f, 0f, WallRunWall, Surface.Panel);
            Flight(test, "Stair (Wall-Run Start)", new Vector2(x0 - FlightLength(FloorTop, WallRunPlatform), (near0 + near1) / 2f), Vector2.right, 8f, FloorTop, WallRunPlatform);
            Marker(future, "Wall Run (future): along the wall, across the 16 m gap", new Vector2(x1 + 1f, (near0 + far1) / 2f), WallRunPlatform);
        }
    }
}
