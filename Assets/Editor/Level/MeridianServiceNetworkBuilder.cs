using System.Collections.Generic;
using UnityEngine;
using static CharacterShowcase.EditorTools.Level.Graybox;

namespace CharacterShowcase.EditorTools.Level
{
    /// <summary>
    /// Builds the graybox of the Meridian Complex's Service Network (06), the underground maintenance level, into the
    /// complex's scene (see MeridianComplexBuilder), from the blockout sheet MeridianComplex-RemainingAreas.png: level
    /// geometry only, in the material it's given. Laid out in world coordinates.
    ///
    /// Its floor is at -12. Corridors (6 m wide, 6 m high) and rooms (8 m high) run under the Kinetic Gardens and on
    /// east past the Ascension Spire, with a service tunnel (4 m wide, 4 m high) on to the Fabrication Hall's
    /// basement. It comes up by stairwells at the gardens' two service-network markers and beside the spire, and two
    /// ventilation shafts rise from it to +16. Where it comes up, the ground (and the gardens' floor) has openings
    /// (GroundOpenings). How the sheet was read is in ServiceNetwork_Blockout_Notes.md beside the scene.
    /// </summary>
    public static class MeridianServiceNetworkBuilder
    {
        // ---- The sheet's dimensions (tunable: see the notes) ----

        public const float Level = -12f;           // The network's floor.
        private const float CorridorHeight = 6f;   // Main corridors: 6 m wide, 6 m high.
        private const float RoomHeight = 8f;
        private const float TunnelHeight = 4f;     // Service tunnels: 4 m wide, 4 m high.
        private const float ShaftTop = 16f;        // Ventilation shafts rise to +16.
        private const float GroundUnderside = -1f; // Open cells' walls rise to the ground's underside.

        /// <summary>Where the Fabrication Hall's basement door is (world x, z): the service tunnel's stair comes up to it at -8.</summary>
        public static readonly UnityEngine.Rect HallDoor = UnityEngine.Rect.MinMaxRect(66f, -43f, 67f, -37f);
        public const float HallDoorFloor = -8f, HallDoorTop = Level + RoomHeight; // The tunnel's ceiling there.

        private static float Corridor(float floor = Level) => floor + CorridorHeight;

        /// <summary>The network's rooms and passages (world x, z), tiling without overlaps.</summary>
        private static List<Cell> Layout() => new()
        {
            // Under the Kinetic Gardens: up to its two service-network markers.
            // (The south stairwell comes up in the gardens' entrance plaza, between its ramp and B1 and B2, its top
            // to the east; the corridor runs back east beside it from its foot.)
            new Cell("Stairwell (to the Kinetic Gardens, South)", -100f, 112f, -83.6f, 118f, Level, 0f, open: true),
            new Cell("Stair Foot (South)", -106f, 105f, -100f, 118f, Level, Corridor()),
            new Cell("Main Corridor (West)", -100f, 105f, -46f, 111f, Level, Corridor()),
            new Cell("Pump Room", -46f, 100f, -30f, 116f, Level, Level + RoomHeight),
            new Cell("Main Corridor (Pump Room to Junction)", -30f, 105f, -26f, 111f, Level, Corridor()),
            new Cell("Junction Room", -26f, 100f, -14f, 116f, Level, Level + RoomHeight),
            new Cell("North Corridor", -23f, 116f, -17f, 171f, Level, Corridor()),
            new Cell("Stairwell (to the Kinetic Gardens, North)", -39.4f, 165f, -23f, 171f, Level, 0f, open: true),
            // East, past the Ascension Spire.
            new Cell("Main Corridor (East)", -14f, 105f, 10f, 111f, Level, Corridor()),
            new Cell("Ventilation Shaft (West)", 10f, 104f, 18f, 112f, Level, 0f, open: true),
            new Cell("Spire Access Room", 18f, 100f, 30f, 116f, Level, Level + RoomHeight),
            new Cell("Stairwell (to the Ascension Spire)", 20f, 116f, 26f, 132f, Level, 0f, open: true),
            // The service tunnel to the Fabrication Hall.
            new Cell("Service Tunnel (East)", 30f, 106f, 58f, 110f, Level, Level + TunnelHeight),
            new Cell("Service Tunnel (South, North Part)", 58f, 64f, 62f, 110f, Level, Level + TunnelHeight),
            new Cell("Ventilation Shaft (East)", 56f, 56f, 64f, 64f, Level, 0f, open: true),
            new Cell("Service Tunnel (South)", 58f, -37f, 62f, 56f, Level, Level + TunnelHeight),
            new Cell("Hall Stair", 58f, -43f, 66f, -37f, Level, Level + RoomHeight),
        };

        /// <summary>Where the network comes up through the ground (world x, z): its stairwells and shafts.</summary>
        public static IEnumerable<UnityEngine.Rect> GroundOpenings
        {
            get
            {
                foreach (Cell cell in Layout()) { if (cell.Open) { yield return cell.Area; } }
            }
        }

        /// <summary>Builds the Service Network under the complex's root, all in the given material.</summary>
        public static void BuildInto(Transform complex, Material material)
        {
            Begin(material);
            Transform network = Group(complex, "06 Service Network");
            Cells(Group(network, "Rooms and Passages"), Layout(), new[] { HallDoor }, GroundUnderside);
            CellLighting(Group(network, "Lighting"), Layout());
            Transform utilities = Group(network, "Utilities");
            CellUtilities(utilities, Layout());
            // Utility stations on the rooms' walls without openings.
            UtilityStation(utilities, new Vector2(-38f, 116f), Vector2.down, Level);   // Pump Room, north wall.
            UtilityStation(utilities, new Vector2(-20f, 100f), Vector2.up, Level);     // Junction Room, south wall.
            UtilityStation(utilities, new Vector2(24f, 100f), Vector2.up, Level);      // Spire Access Room, south wall.

            Transform stairs = Group(network, "Stairs"), rails = Group(network, "Guardrails"), shafts = Group(network, "Ventilation Shafts");
            Transform connections = Group(network, "Connections"), future = Group(network, "Future Mechanics");
            float gardens = MeridianKineticGardensBuilder.FloorTop;

            // Stairwells: each from its foot in the network up to the surface, guarded round the opening but for the top.
            Flight(stairs, "Stair (to the Kinetic Gardens, South)", new Vector2(-100f, 115f), Vector2.right, 6f, Level, gardens, Level);
            Guard(rails, -100f, 112f, -83.6f, 118f, gardens, openSide: Vector2.right);
            Marker(connections, "To the Kinetic Gardens (South Access, ±0)", new Vector2(-83.6f, 115f), gardens);
            // Facing those coming down the south stair, on the stair foot's west wall.
            AreaSign(network, new Vector2(-106f, 111.5f), Level + 2f, Vector2.right, "06", "Service Network");

            Flight(stairs, "Stair (to the Kinetic Gardens, North)", new Vector2(-23f, 168f), Vector2.left, 6f, Level, gardens, Level);
            Guard(rails, -39.4f, 165f, -23f, 171f, gardens, openSide: Vector2.left);
            Marker(connections, "To the Kinetic Gardens (North Access, ±0)", new Vector2(-39.4f, 168f), gardens);

            Flight(stairs, "Stair (to the Ascension Spire)", new Vector2(23f, 116f), Vector2.up, 6f, Level, 0f, Level);
            Guard(rails, 20f, 116f, 26f, 132f, 0f, openSide: Vector2.up);
            Marker(connections, "To the Ascension Spire (±0)", new Vector2(23f, 132f), 0f);

            // The service tunnel's last stretch climbs to the Fabrication Hall's basement door at -8.
            Flight(stairs, "Stair (to the Fabrication Hall, -12 to -8)", new Vector2(66f - FlightLength(Level, HallDoorFloor), -40f), Vector2.right, 6f,
                Level, HallDoorFloor, Level);
            Marker(connections, "To the Fabrication Hall (Basement, -8)", new Vector2(66f, -40f), HallDoorFloor);

            // Ventilation shafts: open to the network below, walled up to +16 above the ground.
            ShaftWalls(shafts, "Ventilation Shaft (West)", 10f, 104f, 18f, 112f, GroundUnderside, ShaftTop);
            ShaftWalls(shafts, "Ventilation Shaft (East)", 56f, 56f, 64f, 64f, GroundUnderside, ShaftTop);
            Marker(future, "Ladder (future): West Shaft, -12 to +16", new Vector2(14f, 108f), Level);
            Marker(future, "Ladder (future): East Shaft, -12 to +16", new Vector2(60f, 60f), Level);
            Marker(future, "Ceiling Climb (future): Pump Room", new Vector2(-38f, 108f), Level + RoomHeight);
            Marker(future, "Crawl Space (future): from the Junction Room", new Vector2(-14f, 113.5f), Level);
            Marker(future, "Hidden Route (future): Spire Access Room", new Vector2(24f, 100f), Level);
        }

        /// <summary>Guardrails round a stairwell's opening at the surface, but for the side the stair comes up on.</summary>
        private static void Guard(Transform rails, float x0, float z0, float x1, float z1, float height, Vector2 openSide)
        {
            Vector2 a = new(x0, z0), b = new(x1, z0), c = new(x1, z1), d = new(x0, z1);
            if (openSide != Vector2.down) { Rail(rails, a, b, height); }
            if (openSide != Vector2.right) { Rail(rails, b, c, height); }
            if (openSide != Vector2.up) { Rail(rails, c, d, height); }
            if (openSide != Vector2.left) { Rail(rails, d, a, height); }
        }
    }
}
