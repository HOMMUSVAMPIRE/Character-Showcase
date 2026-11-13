using UnityEngine;
using static CharacterShowcase.EditorTools.Level.Graybox;

namespace CharacterShowcase.EditorTools.Level
{
    /// <summary>
    /// Builds the graybox of the Meridian Complex's Ascension Spire (03), the vertical-traversal district north of the
    /// Concourse, into the complex's scene (see MeridianComplexBuilder), from the blockout sheets
    /// MeridianComplex-ZoneLayouts.png and MeridianComplex-RemainingAreas.png: level geometry only, in the material
    /// it's given.
    ///
    /// Laid out in its own coordinates, centred on the central core, under a group moved to SpireCentre: a cross of
    /// a central core (+20), west and east platforms (+12), a south wing up from the entry (±0), and the summit tower
    /// (+56) to the north, climbed by a switchback stair. The Concourse's north bridge (+16) joins it by a skyway to the
    /// west platform, where the Kinetic Gardens' east bridge (+12) also ends. How the sheets were read is in
    /// AscensionSpire_Blockout_Notes.md beside the scene.
    /// </summary>
    public static class MeridianAscensionSpireBuilder
    {
        /// <summary>Where the central core's middle is in the world: the west platform's end meets the gardens' east bridge.</summary>
        public static readonly Vector3 SpireCentre = new(48f, 0f, 148f);

        // ---- The sheets' dimensions (tunable: see the notes) ----

        private const float Core = 20f, CoreApothem = 18f;     // The central core, at +20.
        private const float Platform = 12f;                     // West and east platforms.
        private const float PlatformWidth = 12f, PlatformEnd = 50f;
        private const float StairWidth = 6f;
        private const float Lower = 8f;                         // The south wing's terrace.
        private const float Summit = 56f, Crown = 60f;          // The summit floor, and its crown.
        private const float TowerHalf = 14f, TowerSouth = CoreApothem, TowerNorth = CoreApothem + 2f * TowerHalf;
        private const float Landing = 38f;                      // The switchback's landing.
        private const float Balcony = 40f;
        private const float SkywayWidth = 8f, SkywayHeight = 16f;

        private static float CoreSide => CoreApothem * Mathf.Tan(22.5f * Mathf.Deg2Rad);

        /// <summary>Builds the Ascension Spire under the complex's root, all in the given material.</summary>
        public static void BuildInto(Transform complex, Material material)
        {
            Begin(material);
            Transform spire = Group(complex, "03 Ascension Spire");
            BuildSkyway(Group(spire, "Skyway (from the Concourse, +16)"));

            Transform s = Placed(spire, "Spire", SpireCentre);
            Transform masses = Group(s, "Core and Platforms"), stairs = Group(s, "Stairs"), rails = Group(s, "Guardrails");
            Transform connections = Group(s, "Connections"), future = Group(s, "Future Mechanics"), landmarks = Group(s, "Landmarks");

            // The central core, and the west and east platforms, each up a stair to it.
            Vector2[] core = Octagon(CoreApothem);
            Prism(masses, "2 Central Core (+20)", core, 0f, Core, Surface.Panel);
            float stairFoot = CoreApothem + FlightLength(Platform, Core);
            BoxXZ(masses, "3 West Platform (+12)", new Vector2(-(PlatformEnd + stairFoot) / 2f, 0f), new Vector2(PlatformEnd - stairFoot, PlatformWidth), 0f, Platform);
            BoxXZ(masses, "4 East Platform (+12)", new Vector2((PlatformEnd + stairFoot) / 2f, 0f), new Vector2(PlatformEnd - stairFoot, PlatformWidth), 0f, Platform);
            Flight(stairs, "Stair (West Platform to Core)", new Vector2(-stairFoot, 0f), Vector2.right, StairWidth, Platform, Core);
            Flight(stairs, "Stair (East Platform to Core)", new Vector2(stairFoot, 0f), Vector2.left, StairWidth, Platform, Core);
            Vector2 skywayFoot = new(SkywayX - SpireCentre.x, -PlatformWidth / 2f);
            RailAround(rails, Rect(-PlatformEnd, -PlatformWidth / 2f, -stairFoot, PlatformWidth / 2f), Platform,
                (new Vector2(-PlatformEnd, 0f), 8f), (new Vector2(-stairFoot, 0f), StairWidth), (skywayFoot, SkywayWidth));
            RailAround(rails, Rect(stairFoot, -PlatformWidth / 2f, PlatformEnd, PlatformWidth / 2f), Platform,
                (new Vector2(PlatformEnd, 0f), 8f), (new Vector2(stairFoot, 0f), StairWidth));
            Marker(connections, "To Kinetic Gardens (W, +12)", new Vector2(-PlatformEnd, 0f), Platform);
            Marker(connections, "Future Connection (E, +12): toward the Fabrication Hall", new Vector2(PlatformEnd, 0f), Platform);

            // The south wing: up from the entry (±0) to a terrace at +8, and on up to the core.
            const float wingHalf = 14f, wingSouth = -46f;
            BoxXZ(masses, "South Terrace (+8)", new Vector2(0f, (wingSouth - CoreApothem) / 2f), new Vector2(2f * wingHalf, -wingSouth - CoreApothem), 0f, Lower);
            float entryFoot = wingSouth - FlightLength(0f, Lower);
            Flight(stairs, "Stair (Entry to South Terrace)", new Vector2(0f, entryFoot), Vector2.up, StairWidth, 0f, Lower);
            Flight(stairs, "Stair (South Terrace to Core)", new Vector2(0f, -CoreApothem - FlightLength(Lower, Core)), Vector2.up, StairWidth, Lower, Core);
            RailAround(rails, Rect(-wingHalf, wingSouth, wingHalf, -CoreApothem), Lower, (new Vector2(0f, wingSouth), StairWidth), (new Vector2(0f, -CoreApothem), StairWidth));
            BoxXZ(masses, "5 Entry Bridge (±0)", new Vector2(0f, entryFoot - 5.5f), new Vector2(20f, 11f), -0.5f, 0.05f);
            Column(landmarks, "Entry Pylon (West)", new Vector2(-12f, entryFoot - 5.5f), new Vector2(4f, 4f), 0f, 12f, 0f, Surface.Graphite, accent: true);
            Column(landmarks, "Entry Pylon (East)", new Vector2(12f, entryFoot - 5.5f), new Vector2(4f, 4f), 0f, 12f, 0f, Surface.Graphite, accent: true);
            Marker(connections, "Entry (S, ±0)", new Vector2(0f, entryFoot - 11f), 0f);
            AreaSign(landmarks, new Vector2(StairWidth / 2f + 4f, wingSouth), 2.5f, Vector2.down, "03", "Ascension Spire"); // On the south terrace's face.

            // The summit tower, north of the core, and the gallery from the core round to the foot of its stair.
            Prism(landmarks, "1 Spire Summit Tower (+56)", Rect(-TowerHalf, TowerSouth, TowerHalf, TowerNorth), 0f, Summit, Surface.Panel);
            // The concept art's orange bands up the tower (on the west and north faces: the switchback stair hides the
            // east), and a banner down its south face.
            TowerBands(landmarks, -TowerHalf, TowerHalf, TowerSouth, TowerNorth, Core + 2f, Summit - 2f, Vector2.left, Vector2.up);
            Banner(landmarks, new Vector2(0f, TowerSouth), Vector2.down, Summit - 3f, 16f, "03");
            const float laneWidth = 6f, galleryEast = TowerHalf + 2f * laneWidth;
            float laneStart = TowerSouth + 4f; // Where the switchback's first flight starts (the summit landing's north edge).
            Vector2[] gallery = Clip(Rect(CoreSide, 4f, galleryEast, laneStart), -1f, -1f, -CoreApothem * Mathf.Sqrt(2f));
            Prism(masses, "Tower Gallery (+20)", gallery, 0f, Core);

            // The switchback: north up the outer lane to a landing at +38, then south up the inner lane, beside the
            // tower, to the summit landing and the summit.
            float laneA = TowerHalf + 1.5f * laneWidth, laneB = TowerHalf + 0.5f * laneWidth;
            float laneEnd = laneStart + FlightLength(Core, Landing);
            Flight(stairs, "Stair (Gallery to Landing, +20 to +38)", new Vector2(laneA, laneStart), Vector2.up, laneWidth, Core, Landing);
            BoxXZ(masses, "Switchback Landing (+38)", new Vector2((TowerHalf + galleryEast) / 2f, laneEnd + 3f), new Vector2(galleryEast - TowerHalf, 6f), 0f, Landing);
            Flight(stairs, "Stair (Landing to Summit, +38 to +56)", new Vector2(laneB, laneEnd), Vector2.down, laneWidth, Landing, Summit);
            BoxXZ(masses, "Summit Landing (+56)", new Vector2(laneB, (TowerSouth + laneStart) / 2f), new Vector2(laneWidth, laneStart - TowerSouth), Core, Summit);

            // Guardrails: the core's top, the gallery, the landing, the summit landing and the summit.
            float coreSide = 2f * CoreSide;
            Vector2 northEast = new Vector2(1f, 1f).normalized * CoreApothem;
            RailAround(rails, core, Core,
                (new Vector2(-CoreApothem, 0f), StairWidth), (new Vector2(CoreApothem, 0f), StairWidth), (new Vector2(0f, -CoreApothem), StairWidth),
                (new Vector2(0f, CoreApothem), coreSide), (northEast, coreSide));
            float galleryNorthWest = CoreSide;
            RailAround(rails, gallery, Core, (northEast, coreSide),
                (new Vector2((galleryNorthWest + galleryEast) / 2f, laneStart), galleryEast - galleryNorthWest));
            RailAround(rails, Rect(TowerHalf, laneEnd, galleryEast, laneEnd + 6f), Landing, (new Vector2((TowerHalf + galleryEast) / 2f, laneEnd), galleryEast - TowerHalf));
            RailAround(rails, Rect(TowerHalf, TowerSouth, galleryEast - laneWidth, laneStart), Summit,
                (new Vector2(TowerHalf, (TowerSouth + laneStart) / 2f), laneStart - TowerSouth), (new Vector2(laneB, laneStart), laneWidth));
            RailAround(rails, Rect(-TowerHalf, TowerSouth, TowerHalf, TowerNorth), Summit, (new Vector2(TowerHalf, (TowerSouth + laneStart) / 2f), laneStart - TowerSouth));

            // The summit's crown: four posts and a frame to +60.
            foreach (Vector2 corner in new[] { new Vector2(-1f, -1f), new Vector2(1f, -1f), new Vector2(1f, 1f), new Vector2(-1f, 1f) })
            {
                Vector2 at = new Vector2(0f, (TowerSouth + TowerNorth) / 2f) + new Vector2(corner.x * (TowerHalf - 1f), corner.y * (TowerHalf - 1f));
                BoxXZ(landmarks, "Crown Post", at, new Vector2(2f, 2f), Summit, Crown, 0f, Surface.Steel);
            }
            float mid = (TowerSouth + TowerNorth) / 2f, span = 2f * TowerHalf - 2f;
            BoxXZ(landmarks, "Crown Beam (South)", new Vector2(0f, TowerSouth + 1f), new Vector2(span, 1f), Crown - 1f, Crown, 0f, Surface.Steel);
            BoxXZ(landmarks, "Crown Beam (North)", new Vector2(0f, TowerNorth - 1f), new Vector2(span, 1f), Crown - 1f, Crown, 0f, Surface.Steel);
            BoxXZ(landmarks, "Crown Beam (West)", new Vector2(-TowerHalf + 1f, mid), new Vector2(1f, span), Crown - 1f, Crown, 0f, Surface.Steel);
            BoxXZ(landmarks, "Crown Beam (East)", new Vector2(TowerHalf - 1f, mid), new Vector2(1f, span), Crown - 1f, Crown, 0f, Surface.Steel);
            Marker(landmarks, "Spire Summit (+56)", new Vector2(0f, mid), Summit);

            // A narrow balcony high on the tower's west face, for the climbing routes the sheets plan.
            BoxXZ(future, "Narrow Balcony (+40)", new Vector2(-TowerHalf - 3f, mid), new Vector2(6f, 16f), Balcony - Deck, Balcony);
            RailAround(rails, Rect(-TowerHalf - 6f, mid - 8f, -TowerHalf, mid + 8f), Balcony, (new Vector2(-TowerHalf, mid), 16f));
            Marker(future, "Ladder (future): Core to the Balcony, +20 to +40 (24 m)", new Vector2(-TowerHalf, TowerSouth + 2f), Core);
            Marker(future, "Climbable Wall (future): the tower's west face, Balcony to Summit", new Vector2(-TowerHalf, mid), Balcony);
            Marker(future, "Wall Run (future): along the south terrace's east side", new Vector2(wingHalf, (wingSouth - CoreApothem) / 2f), Lower);
            Marker(future, "Ladder (future): West Platform to Core", new Vector2(-CoreApothem - 1f, -4f), Platform);
            Marker(future, "Rope (future): Summit to the East Platform", new Vector2(TowerHalf, mid), Summit);
        }

        /// <summary>The skyway's centre line (world x): within the Concourse's 12 m north bridge, and over the west platform.</summary>
        private const float SkywayX = 2f;

        /// <summary>
        /// The skyway, in world coordinates: on from the end of the Concourse's north bridge (+16) to the west platform,
        /// down a short stair to its +12.
        /// </summary>
        private static void BuildSkyway(Transform skyway)
        {
            Transform rails = Group(skyway, "Guardrails");
            const float bridgeEnd = 75.6f;
            float platformSouth = SpireCentre.z - PlatformWidth / 2f, stairTop = platformSouth - FlightLength(Platform, SkywayHeight);
            Walkway(skyway, rails, new Vector2(SkywayX, bridgeEnd), Vector2.up, stairTop - bridgeEnd, SkywayWidth, SkywayHeight);
            Flight(skyway, "Stair (Skyway to West Platform, +16 to +12)", new Vector2(SkywayX, platformSouth), Vector2.down, SkywayWidth, Platform, SkywayHeight);
            // The north bridge is wider than the skyway: guard the rest of its end.
            Rail(rails, new Vector2(-6f, bridgeEnd), new Vector2(SkywayX - SkywayWidth / 2f, bridgeEnd), SkywayHeight);
        }
    }
}
