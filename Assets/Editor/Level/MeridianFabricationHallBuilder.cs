using System.Collections.Generic;
using UnityEngine;
using static CharacterShowcase.EditorTools.Level.Graybox;

namespace CharacterShowcase.EditorTools.Level
{
    /// <summary>
    /// Builds the graybox of the Meridian Complex's Fabrication Hall (04), the industrial district east of the
    /// Concourse, into the complex's scene (see MeridianComplexBuilder), from the blockout sheets
    /// MeridianComplex-ZoneLayouts.png and MeridianComplex-RemainingAreas.png: level geometry only, in the material
    /// it's given.
    ///
    /// The hall is 120 × 100 m, laid out in its own coordinates (centred on its middle, +Z north) under a group moved
    /// to HallCentre, with walls to +16 and no roof: a 3 × 3 grid of rooms on floors from -8 to +8, as the sheet's key
    /// locations, an upper catwalk at +12, and crates, shelves and machinery standing in for its objects. The
    /// Concourse's east bridge (+8) comes in at its main entrance, its north-east bridge (+12) onto the catwalk, and
    /// the Service Network's tunnel at its basement (-8). How the sheets were read is in
    /// FabricationHall_Blockout_Notes.md beside the scene.
    /// </summary>
    public static class MeridianFabricationHallBuilder
    {
        /// <summary>Where the hall's middle is in the world.</summary>
        public static readonly Vector3 HallCentre = new(126f, 0f, 0f);

        // ---- The sheets' dimensions (tunable: see the notes) ----

        private const float WallTop = 16f;      // Main hall height.
        private const float WallBase = -8.5f;   // Walls go down to the basements' floor slabs.
        private const float DoorHeight = 6f, DoorWidth = 4f;
        private const float StairWidth = 6f;
        private const float Catwalk = 12f;      // Upper catwalks.
        private const float Basement = -8f;
        private const float CrateSize = 4f;

        // The grid of rooms (hall coordinates): three columns and, in each, three rows, with 1 m walls between.
        private const float WestX0 = -59f, WestX1 = -29f, CentreX0 = -28f, CentreX1 = 20f, EastX0 = 21f, EastX1 = 59f;
        private const float NorthZ0 = 29f, NorthZ1 = 49f, SouthZ0 = -49f;
        private const float WestSouthZ1 = -16f, CentreSouthZ1 = -21f, EastSouthZ1 = -13f; // Each column's middle row starts 1 m north.

        /// <summary>Where the hall opens the ground (world x, z): over its two basements.</summary>
        public static IEnumerable<UnityEngine.Rect> GroundOpenings
        {
            get
            {
                yield return World(UnityEngine.Rect.MinMaxRect(WestX0, SouthZ0, WestX1, WestSouthZ1));
                yield return World(UnityEngine.Rect.MinMaxRect(EastX0, SouthZ0, EastX1, EastSouthZ1));
            }
        }

        private static UnityEngine.Rect World(UnityEngine.Rect r) => new(r.x + HallCentre.x, r.y + HallCentre.z, r.width, r.height);

        /// <summary>Builds the Fabrication Hall under the complex's root, all in the given material.</summary>
        public static void BuildInto(Transform complex, Material material)
        {
            Begin(material);
            Transform hall = Group(complex, "04 Fabrication Hall");
            BuildApproaches(Group(hall, "Approaches"));

            Transform building = Group(hall, "Hall");
            building.localPosition = HallCentre;
            Transform rooms = Group(building, "Rooms");
            Transform stairs = Group(building, "Stairs and Ramps");
            Transform catwalks = Group(building, "Catwalks (+12)");
            Transform rails = Group(building, "Guardrails");
            Transform objects = Group(building, "Objects");
            Transform connections = Group(building, "Connections");
            Transform future = Group(building, "Future Mechanics");
            BuildRooms(rooms);
            BuildWalls(Group(building, "Walls"));
            BuildStairs(stairs, rails);
            BuildCatwalks(catwalks, stairs, rails);
            BuildObjects(objects, future);
            BuildUtilities(Group(building, "Utilities"));
            Marker(connections, "To Concourse (W, +8)", new Vector2(-60f, 0f), 8f);
            // Beside the main entrance, on the west wall's face, between its pilasters.
            AreaSign(building, new Vector2(-60f, -12f), 8.6f, Vector2.left, "04", "Fabrication Hall");
            Banner(building, new Vector2(-60f, 10f), Vector2.left, WallTop - 0.6f, 6f, "04"); // And a banner the other side of it.
            Marker(connections, "To Concourse (NW, +12)", new Vector2(-60f, 38f), Catwalk);
            Marker(connections, "To Service Network (W, -8)", new Vector2(-60f, -40f), Basement);
            Marker(connections, "Service Door (S, ±0)", new Vector2(-4f, -50f), 0f);
            Marker(connections, "Loading Dock (W, ±0 to +4)", new Vector2(-84f, 18f), 0f);
            Marker(connections, "Future Connection (N, +12): toward the Ascension Spire", new Vector2(40f, 50f), Catwalk);
        }

        private static GameObject Room(Transform parent, string name, float x0, float x1, float z0, float z1, float floor) =>
            Block(parent, name, x0, x1, z0, z1, floor > 0f ? 0f : floor - 0.5f, floor);

        /// <summary>The rooms' floors: solid up to +4 and +8, the ground at ±0, and slabs at -8 in the basements.</summary>
        private static void BuildRooms(Transform rooms)
        {
            Room(rooms, "Crate Store (+4)", WestX0, WestX1, NorthZ0, NorthZ1, 4f);
            Room(rooms, "3 Loading Bay (+4)", WestX0, WestX1, WestSouthZ1 + 1f, NorthZ0 - 1f, 4f);
            Room(rooms, "6 Maintenance (-8)", WestX0, WestX1, SouthZ0, WestSouthZ1, Basement);
            Room(rooms, "2 Storage (+8)", CentreX0, CentreX1, NorthZ0, NorthZ1, 8f);
            Room(rooms, "1 Main Workshop (+8)", CentreX0, CentreX1, CentreSouthZ1 + 1f, NorthZ0 - 1f, 8f);
            // 5 Service Corridor (±0): the ground, from the south wall to the workshop.
            Room(rooms, "Parts Store (+8)", EastX0, EastX1, NorthZ0, NorthZ1, 8f);
            Room(rooms, "4 Assembly Wing (+4)", EastX0, EastX1, EastSouthZ1 + 1f, NorthZ0 - 1f, 4f);
            Room(rooms, "7 Utility Room (-8)", EastX0, EastX1, SouthZ0, EastSouthZ1, Basement);
            // The entrance gallery: across the loading bay at +8, from the main entrance to the workshop.
            Room(rooms, "Entrance Gallery (+8)", WestX0, WestX1, -5f, 5f, 8f);
        }

        /// <summary>Doorways: 4 m wide and 6 m high between rooms on one floor; stairways full width, up to 6 m above their top.</summary>
        private static (Vector2, float, float, float) Door(float x, float z, float floor) => (new Vector2(x, z), DoorWidth, floor, floor + DoorHeight);
        private static (Vector2, float, float, float) Stairway(float x, float z, float bottom, float top) => (new Vector2(x, z), StairWidth, bottom, top + DoorHeight);

        private static void BuildWalls(Transform walls)
        {
            const float t = 1f;
            // Outer walls (centred half a metre inside the footprint).
            Wall(walls, "Wall (West)", new Vector2(-59.5f, -50f), new Vector2(-59.5f, 50f), WallBase, WallTop, t,
                (new Vector2(-59.5f, 0f), 10f, 8f, 8f + DoorHeight),                          // Main entrance, from the Concourse's east bridge.
                (new Vector2(-59.5f, 18f), 8f, 4f, 4f + DoorHeight),                          // The loading dock, up its ramp.
                (new Vector2(-59.5f, 38f), 8f, Catwalk, WallTop),                             // The catwalk, from the Concourse's north-east bridge.
                (new Vector2(-59.5f, -40f), StairWidth, MeridianServiceNetworkBuilder.HallDoorFloor, MeridianServiceNetworkBuilder.HallDoorTop)); // The Service Network.
            Wall(walls, "Wall (East)", new Vector2(59.5f, -50f), new Vector2(59.5f, 50f), WallBase, WallTop, t);
            Wall(walls, "Wall (South)", new Vector2(-60f, -49.5f), new Vector2(60f, -49.5f), WallBase, WallTop, t,
                (new Vector2(-4f, -49.5f), StairWidth, 0f, DoorHeight));                       // The service door, on the ground.
            Wall(walls, "Wall (North)", new Vector2(-60f, 49.5f), new Vector2(60f, 49.5f), WallBase, WallTop, t);

            // Between the columns.
            Wall(walls, "Wall (West Column)", new Vector2(-28.5f, -49f), new Vector2(-28.5f, 49f), WallBase, WallTop, t,
                (new Vector2(-28.5f, 0f), 10f, 8f, 8f + DoorHeight),                          // Entrance gallery to the workshop.
                (new Vector2(-28.5f, 38f), 8f, Catwalk, WallTop),                             // The catwalk.
                Stairway(-28.5f, -40f, WallBase, 0f));                                       // Service corridor down to maintenance.
            Wall(walls, "Wall (East Column)", new Vector2(20.5f, -49f), new Vector2(20.5f, 49f), WallBase, WallTop, t,
                Stairway(20.5f, 0f, 4f, 8f),                                                  // Assembly up to the workshop.
                (new Vector2(20.5f, 38f), 8f, Catwalk, WallTop),                              // The catwalk.
                Stairway(20.5f, -35f, WallBase, 0f));                                        // Service corridor down to the utility room.

            // Between the rows.
            Wall(walls, "Wall (Crate Store)", new Vector2(WestX0, 28.5f), new Vector2(WestX1, 28.5f), WallBase, WallTop, t, Door(-44f, 28.5f, 4f));
            Wall(walls, "Wall (Loading Bay, South)", new Vector2(WestX0, -15.5f), new Vector2(WestX1, -15.5f), WallBase, WallTop, t, Stairway(-50f, -15.5f, WallBase, 4f));
            Wall(walls, "Wall (Storage)", new Vector2(CentreX0, 28.5f), new Vector2(CentreX1, 28.5f), WallBase, WallTop, t,
                Door(4f, 28.5f, 8f), (new Vector2(-22f, 28.5f), 4f, Catwalk, WallTop));      // A door, and the catwalk spur above.
            Wall(walls, "Wall (Workshop, South)", new Vector2(CentreX0, -20.5f), new Vector2(CentreX1, -20.5f), WallBase, WallTop, t, Stairway(0f, -20.5f, 0f, 8f));
            Wall(walls, "Wall (Parts Store)", new Vector2(EastX0, 28.5f), new Vector2(EastX1, 28.5f), WallBase, WallTop, t, Stairway(40f, 28.5f, 4f, 8f));
            Wall(walls, "Wall (Assembly Wing, South)", new Vector2(EastX0, -12.5f), new Vector2(EastX1, -12.5f), WallBase, WallTop, t);
        }

        /// <summary>Stairs between the floors, each ending at the far face of the wall it goes through.</summary>
        private static void BuildStairs(Transform stairs, Transform rails)
        {
            // The entrance gallery (+8) down to the loading bay (+4), either side.
            Flight(stairs, "Stair (Gallery to Loading Bay, North)", new Vector2(-44f, 5f + FlightLength(4f, 8f)), Vector2.down, StairWidth, 4f, 8f);
            Flight(stairs, "Stair (Gallery to Loading Bay, South)", new Vector2(-44f, -5f - FlightLength(4f, 8f)), Vector2.up, StairWidth, 4f, 8f);
            Rail(rails, new Vector2(WestX0, 5f), new Vector2(WestX1, 5f), 8f, (new Vector2(-44f, 5f), StairWidth));
            Rail(rails, new Vector2(WestX0, -5f), new Vector2(WestX1, -5f), 8f, (new Vector2(-44f, -5f), StairWidth));

            // Loading bay (+4) down to maintenance (-8).
            Flight(stairs, "Stair (Loading Bay to Maintenance)", new Vector2(-50f, -15f - FlightLength(Basement, 4f)), Vector2.up, StairWidth, Basement, 4f, Basement);
            // Workshop (+8) down to the service corridor (±0).
            Flight(stairs, "Stair (Workshop to Service Corridor)", new Vector2(0f, -20f - FlightLength(0f, 8f)), Vector2.up, StairWidth, 0f, 8f);
            // Assembly wing (+4) up to the workshop and to the parts store (+8).
            Flight(stairs, "Stair (Assembly Wing to Workshop)", new Vector2(20f + FlightLength(4f, 8f), 0f), Vector2.left, StairWidth, 4f, 8f);
            Flight(stairs, "Stair (Assembly Wing to Parts Store)", new Vector2(40f, 29f - FlightLength(4f, 8f)), Vector2.up, StairWidth, 4f, 8f);
            // Service corridor (±0) down to maintenance and the utility room (-8).
            Flight(stairs, "Stair (Service Corridor to Maintenance)", new Vector2(-28f - FlightLength(Basement, 0f), -40f), Vector2.right, StairWidth, Basement, 0f, Basement);
            Flight(stairs, "Stair (Service Corridor to Utility Room)", new Vector2(20f + FlightLength(Basement, 0f), -35f), Vector2.left, StairWidth, Basement, 0f, Basement);
        }

        /// <summary>
        /// The upper catwalk at +12: right across the north rooms from the north-east bridge's entrance, with stairs
        /// down into storage and the parts store, and a spur into a ring round the main workshop, which has a stair up
        /// from its floor.
        /// </summary>
        private static void BuildCatwalks(Transform catwalks, Transform stairs, Transform rails)
        {
            const float z0 = 34f, z1 = 42f, ring = 3f;
            CatwalkDeck(catwalks, "Catwalk (North)", -60f, 60f, z0, z1);
            Rail(rails, new Vector2(WestX0, z0), new Vector2(EastX1, z0), Catwalk, (new Vector2(-22f, z0), 4f));
            Rail(rails, new Vector2(WestX0, z1), new Vector2(EastX1, z1), Catwalk, (new Vector2(-4f, z1), StairWidth), (new Vector2(40f, z1), StairWidth));
            Flight(stairs, "Stair (Catwalk to Storage)", new Vector2(-4f, z1 + FlightLength(8f, Catwalk)), Vector2.down, StairWidth, 8f, Catwalk);
            Flight(stairs, "Stair (Catwalk to Parts Store)", new Vector2(40f, z1 + FlightLength(8f, Catwalk)), Vector2.down, StairWidth, 8f, Catwalk);

            // Braced frames under it, on the floors below (clear of the stairs, shelves and crates there), and brackets
            // under it where it goes through the walls.
            float post = Settings.structure.framePost;
            foreach ((float x, float floor) in new[] { (-44f, 4f), (-14f, 8f), (8f, 8f), (30f, 8f), (50f, 8f) })
            {
                BracedFrame(catwalks, "Braced Frame", new Vector2(x, z0 + post / 2f), new Vector2(x, z1 - post / 2f), floor, Catwalk - Deck);
            }
            foreach ((float face, float outward) in new[] { (WestX0, 1f), (WestX1, -1f), (CentreX0, 1f), (CentreX1, -1f), (EastX0, 1f), (EastX1, -1f) })
            {
                foreach (float z in new[] { z0 + 0.5f, z1 - 0.5f })
                {
                    Bracket(catwalks, new Vector2(face, z), new Vector2(outward, 0f), Catwalk - Deck);
                }
            }

            // The spur, through the storage wall into the workshop ring.
            CatwalkDeck(catwalks, "Catwalk (Spur)", -24f, -20f, NorthZ0 - 1f, z0);
            Rail(rails, new Vector2(-24f, NorthZ0), new Vector2(-24f, z0), Catwalk);
            Rail(rails, new Vector2(-20f, NorthZ0), new Vector2(-20f, z0), Catwalk);

            float wx0 = CentreX0, wx1 = CentreX1, wz0 = CentreSouthZ1 + 1f, wz1 = NorthZ0 - 1f;
            CatwalkDeck(catwalks, "Workshop Ring (North)", wx0, wx1, wz1 - ring, wz1);
            CatwalkDeck(catwalks, "Workshop Ring (South)", wx0, wx1, wz0, wz0 + ring);
            CatwalkDeck(catwalks, "Workshop Ring (West)", wx0, wx0 + ring, wz0 + ring, wz1 - ring);
            CatwalkDeck(catwalks, "Workshop Ring (East)", wx1 - ring, wx1, wz0 + ring, wz1 - ring);
            // An overhead crane over the workshop, its runway rails on the column walls' tops.
            const float railTop = WallTop + 0.3f;
            Block(catwalks, "Crane Runway (West)", CentreX0 - 0.7f, CentreX0 - 0.3f, wz0, wz1, WallTop, railTop, Surface.Steel);
            Block(catwalks, "Crane Runway (East)", CentreX1 + 0.3f, CentreX1 + 0.7f, wz0, wz1, WallTop, railTop, Surface.Steel);
            OverheadCrane(catwalks, new Vector2(CentreX0 - 0.5f, 4f), new Vector2(CentreX1 + 0.5f, 4f), railTop, Catwalk + 1f);

            Flight(stairs, "Stair (Workshop to Ring)", new Vector2(-4f, wz1 - ring - FlightLength(8f, Catwalk)), Vector2.up, StairWidth, 8f, Catwalk);
            Vector2 a = new(wx0 + ring, wz0 + ring), b = new(wx1 - ring, wz0 + ring), c = new(wx1 - ring, wz1 - ring), d = new(wx0 + ring, wz1 - ring);
            Rail(rails, a, b, Catwalk);
            Rail(rails, b, c, Catwalk);
            Rail(rails, c, d, Catwalk, (new Vector2(-4f, wz1 - ring), StairWidth));
            Rail(rails, d, a, Catwalk);
        }

        /// <summary>A catwalk deck at +12, 1 m thick, hung from the walls.</summary>
        private static void CatwalkDeck(Transform parent, string name, float x0, float x1, float z0, float z1) =>
            Block(parent, name, x0, x1, z0, z1, Catwalk - Deck, Catwalk, Surface.Steel);

        /// <summary>Crates, shelves and machinery: static stand-ins for the sheet's interactive objects, with markers for what they'll become.</summary>
        private static void BuildObjects(Transform objects, Transform future)
        {
            void Crate(string where, float x, float z, float floor) =>
                BoxXZ(objects, $"Crate ({where})", new Vector2(x, z), new Vector2(CrateSize, CrateSize), floor, floor + CrateSize, 0f, Surface.Steel);
            Crate("Loading Bay", -50f, 25f, 4f);
            Crate("Loading Bay", -52f, -10f, 4f);
            Crate("Loading Bay", -35f, -10f, 4f);
            Crate("Loading Bay", -35f, 15f, 4f);
            Crate("Crate Store", -52f, 46f, 4f);
            Crate("Crate Store", -46f, 46f, 4f);
            Crate("Crate Store", -52f, 46f, 8f);
            Crate("Crate Store", -36f, 46f, 4f);
            Crate("Crate Store", -52f, 31.5f, 4f);
            Crate("Workshop", -15f, 10f, 8f);
            Crate("Workshop", 8f, -10f, 8f);
            Crate("Workshop", 10f, 14f, 8f);
            Crate("Parts Store", 50f, 32f, 8f);
            Crate("Parts Store", 26f, 46f, 8f);
            Marker(future, "Pushable Crates (future): the crates", new Vector2(-50f, 25f), 8f);

            // Shelves in storage, 2 m aisles between them (narrow passages), low enough to stay under the catwalk.
            foreach ((float x0, float x1, float z) in new[] { (-26f, -8f, 31f), (8f, 18f, 31f), (-26f, -9f, 46.5f), (3f, 18f, 46.5f) })
            {
                BoxXZ(objects, "Storage Shelf", new Vector2((x0 + x1) / 2f, z), new Vector2(x1 - x0, 1.5f), 8f, 10.5f, 0f, Surface.Graphite);
            }
            BoxXZ(objects, "Assembly Line", new Vector2(42f, 12f), new Vector2(24f, 3f), 4f, 5f, 0f, Surface.Steel);
            BoxXZ(objects, "Assembly Line", new Vector2(42f, -4f), new Vector2(24f, 3f), 4f, 5f, 0f, Surface.Steel);
            foreach (Vector2 at in new[] { new Vector2(30f, -40f), new Vector2(42f, -40f), new Vector2(52f, -24f) })
            {
                BoxXZ(objects, "Machinery", at, new Vector2(CrateSize, CrateSize), Basement, Basement + CrateSize, 0f, Surface.Graphite);
            }
            foreach (Vector2 at in new[] { new Vector2(-52f, -42f), new Vector2(-52f, -26f) })
            {
                BoxXZ(objects, "Tank", at, new Vector2(5f, 5f), Basement, Basement + 6f, 0f, Surface.Steel);
            }

            Marker(future, "Sliding Door (future): Storage", new Vector2(4f, 28.5f), 8f);
            Marker(future, "Sliding Door (future): Crate Store", new Vector2(-44f, 28.5f), 4f);
            Marker(future, "Shutter (future): Service Door", new Vector2(-4f, -49.5f), 0f);
            Marker(future, "Lift (future): Assembly Wing to Catwalk", new Vector2(54f, 20f), 4f);
            Marker(future, "Ladder (future): Workshop to Ring", new Vector2(18f, -14f), 8f);
            Marker(future, "Crouch Passage (future): between the shelves", new Vector2(-17f, 33.5f), 8f);
            Marker(future, "Maintenance Access (future): catwalk to the roofline", new Vector2(56f, 38f), Catwalk);
        }

        /// <summary>
        /// Utility stations on the rooms' outer and column walls, one in each of the maintenance and utility rooms,
        /// the assembly wing, the loading bay and the workshop, between the walls' pilasters and clear of their doors.
        /// </summary>
        private static void BuildUtilities(Transform utilities)
        {
            UtilityStation(utilities, new Vector2(-59f, -34f), Vector2.right, Basement); // 6 Maintenance, west wall.
            UtilityStation(utilities, new Vector2(59f, -36f), Vector2.left, Basement);   // 7 Utility Room, east wall.
            UtilityStation(utilities, new Vector2(59f, 4f), Vector2.left, 4f);           // 4 Assembly Wing, east wall.
            UtilityStation(utilities, new Vector2(-59f, -12f), Vector2.right, 4f);       // 3 Loading Bay, west wall.
            UtilityStation(utilities, new Vector2(-28f, 12f), Vector2.right, 8f);        // 1 Main Workshop, west column wall.
        }

        /// <summary>
        /// Outside, in world coordinates: the walkway from the Concourse's east bridge (+8) to the main entrance, the
        /// landing at the end of its north-east bridge (+12) and the walkway on to the catwalk, and the loading dock's
        /// 1:6 ramp from the ground up to the loading bay (+4).
        /// </summary>
        private static void BuildApproaches(Transform approaches)
        {
            Transform rails = Group(approaches, "Guardrails");
            float wallX = HallCentre.x - 60f;
            Walkway(Group(approaches, "Walkway (from the Concourse's East Bridge, +8)"), rails, new Vector2(57.5f, 0f), Vector2.right, wallX - 57.5f, 10f, 8f);

            // The north-east bridge ends at (40.66, 40.66), 8 m wide across the diagonal: a landing cut square to it.
            Vector2 bridgeEnd = new Vector2(1f, 1f).normalized * 57.5f;
            float cut = bridgeEnd.x + bridgeEnd.y;
            Vector2[] landing = Clip(Rect(37f, 30f, 58f, 46f), -1f, -1f, -cut);
            Prism(Group(approaches, "Landing (from the Concourse's North-East Bridge, +12)"), "Landing (+12)", landing, 0f, Catwalk);
            RailAround(rails, landing, Catwalk, (bridgeEnd, 8f), (new Vector2(58f, 38f), 8f));
            Walkway(Group(approaches, "Walkway (to the Catwalk, +12)"), rails, new Vector2(58f, 38f), Vector2.right, wallX - 58f, 8f, Catwalk);

            const float dock = 4f;
            Ramp(approaches, "Loading Dock Ramp (1:6, ±0 to +4)", new Vector2(wallX - dock * 6f, 18f), Vector2.right, 8f, dock * 6f, 0f, dock);
        }
    }
}
