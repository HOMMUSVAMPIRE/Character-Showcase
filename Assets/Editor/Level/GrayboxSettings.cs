using System;
using UnityEditor;
using UnityEngine;

namespace CharacterShowcase.EditorTools.Level
{
    /// <summary>
    /// The numbers the level builders' shared pieces are made with (see Graybox): stair profiles, guardrails, wall
    /// detail, columns, structure, light fixtures, utilities, markings, signs, and the environment's sun and haze. One asset
    /// (AssetPath), made with these defaults the first time it's needed, then kept: tune it in the inspector (Tools >
    /// Character Showcase > Level > Generation Settings) and rebuild a level to see the change. Try changes in the Level
    /// Playground first; it builds in a couple of seconds.
    ///
    /// It holds what every piece shares, not where the areas put things: an area's layout (its platforms' heights, a
    /// stair's width, where the crates go) is its builder's. The materials' looks are in the material assets themselves
    /// (see GrayboxScene.MaterialFolder). A builder can still change a stair profile or a bridge's length for its area
    /// after Begin (as the Kinetic Gardens' steeper stairs do).
    /// </summary>
    public sealed class GrayboxSettings : ScriptableObject
    {
        public const string AssetPath = "Assets/Editor/Level/GrayboxSettings.asset";

        [Serializable]
        public sealed class StairSettings
        {
            [Tooltip("Each step at most this high (m). Keep it under the character's step height (0.35 m).")]
            public float stepRise = 0.3f;
            [Tooltip("Each step this deep (m).")]
            public float stepRun = 0.4f;
            [Tooltip("How deep the orange nosing strip along each step's front edge is (m).")]
            public float nosingDepth = 0.04f;
        }

        [Serializable]
        public sealed class DeckSettings
        {
            [Tooltip("How thick decks, walkways and catwalks are (m).")]
            public float deckThickness = 1f;
            [Tooltip("How far bridges run out to where the next area joins (m).")]
            public float bridgeLength = 20f;
            [Tooltip("The I-beam columns under walkways and bridges: their footprint, across and along (m).")]
            public Vector2 walkwayColumnSize = new(1f, 1f);
            [Tooltip("Walkway columns this far apart along the walkway (m), the first half this far from its start.")]
            public float walkwayColumnSpacing = 10f;
            [Tooltip("How far out from the walkway's middle its columns stand, as a fraction of its half-width.")]
            [Range(0f, 1f)] public float walkwayColumnSpread = 0.8f;
            [Tooltip("Ramp supports this far apart along the ramp (m).")]
            public float rampSupportSpacing = 8f;
            [Tooltip("How far in from each side of a suspended walkway's deck its trusses are (m, to their middles). Keep the deck's edge a ledge with something under it to hang against.")]
            public float trussInset = 0.25f;
            [Tooltip("The deepest a suspended walkway's trusses are (m); short walkways get shallower ones (a sixth of their length).")]
            public float trussMaxDepth = 1.5f;
        }

        [Serializable]
        public sealed class RailSettings
        {
            [Tooltip("How high guardrails are (m), to the top of the top rail.")]
            public float railHeight = 1.1f;
            [Tooltip("How thick a guardrail's collider is (m): one solid box the rail's full height and length.")]
            public float railThickness = 0.15f;
            [Tooltip("Posts at most this far apart (m).")]
            public float postSpacing = 2f;
            [Tooltip("How wide the posts are, both ways (m).")]
            public float postSize = 0.06f;
            [Tooltip("The orange top rail's width and depth (m). Keep it wider than the posts and the mid rail, so their sides never share a plane.")]
            public Vector2 topRailSize = new(0.08f, 0.08f);
            [Tooltip("Where the mid rail is, as a fraction of the rail's height.")]
            [Range(0.2f, 0.8f)] public float midRailHeight = 0.45f;
            [Tooltip("The mid rail's width and depth (m). Keep it narrower than the posts.")]
            public Vector2 midRailSize = new(0.05f, 0.05f);
            [Tooltip("The orange fascia band along the edge a rail stands on: how far below the surface it starts and ends (m).")]
            public Vector2 fasciaDrop = new(0.05f, 0.2f);
            [Tooltip("How far the fascia stands proud of the rail's collider on each side (m).")]
            public float fasciaProud = 0.01f;
        }

        [Serializable]
        public sealed class WallSettings
        {
            [Tooltip("Walls at least this long get pilasters (m).")]
            public float pilasterMinWallLength = 12f;
            [Tooltip("Pilasters at most this far apart (m), centred on the wall.")]
            public float pilasterSpacing = 8f;
            [Tooltip("How wide pilasters are (m).")]
            public float pilasterWidth = 0.8f;
            [Tooltip("How far pilasters stand proud of the wall (m). They have no collider, so keep this small.")]
            public float pilasterDepth = 0.08f;
            [Tooltip("How wide the frame round an opening is (m), its orange lip included.")]
            public float frameWidth = 0.2f;
            [Tooltip("How wide the orange lip along the frame's inside edge is (m).")]
            public float frameLip = 0.05f;
            [Tooltip("How far the frame stands proud of the wall (m).")]
            public float frameDepth = 0.06f;
            [Tooltip("How high and thick the graphite skirting along underground rooms' walls is (m).")]
            public Vector2 skirtingSize = new(0.12f, 0.02f);
        }

        [Serializable]
        public sealed class PlatformSettings
        {
            [Tooltip("How deep the graphite coping round a platform's top edge is (m); low platforms get a quarter of their height.")]
            public float copingHeight = 0.3f;
            [Tooltip("How far the coping stands proud of the platform's sides (m).")]
            public float copingDepth = 0.03f;
            [Tooltip("How far below the platform's top the coping stops (m), so it's never in the plane of a stair or ramp arriving there.")]
            public float copingGap = 0.01f;
        }

        [Serializable]
        public sealed class ColumnSettings
        {
            [Tooltip("How much of a column's narrower side each chamfer cuts off.")]
            [Range(0f, 0.4f)] public float chamfer = 0.15f;
            [Tooltip("The widest an accent strip up a column's face is (m); narrow faces get a quarter of their width.")]
            public float accentStripWidth = 0.15f;
            [Tooltip("How far accent strips stand proud of the column (m).")]
            public float accentStripProud = 0.02f;
            [Tooltip("Where accent strips start and end, as fractions of the column's height.")]
            public Vector2 accentStripSpan = new(0.15f, 0.85f);
        }

        [Serializable]
        public sealed class StructureSettings
        {
            [Tooltip("An I-beam column's flange and web thickness, as fractions of its footprint.")]
            public Vector2 iBeamFlangeAndWeb = new(0.08f, 0.06f);
            [Tooltip("How thick trusses are (m), across.")]
            public float trussThickness = 0.3f;
            [Tooltip("How deep a truss's chords and posts are (m).")]
            public float trussChord = 0.2f;
            [Tooltip("How wide a braced frame's posts and beam are (m).")]
            public float framePost = 0.3f;
            [Tooltip("How deep a braced frame's cross bracing is (m).")]
            public float frameBrace = 0.12f;
            [Tooltip("How far a knee bracket reaches down the wall and out under what it holds (m).")]
            public float bracketSize = 1f;
        }

        [Serializable]
        public sealed class FixtureSettings
        {
            [Tooltip("Ceiling light panels in underground rooms: their size (m), across and along the room.")]
            public Vector2 ceilingPanelSize = new(1.2f, 0.6f);
            [Tooltip("How far a ceiling panel (its graphite housing and glowing face) hangs below the ceiling (m).")]
            public float ceilingPanelDrop = 0.08f;
            [Tooltip("Strip lights along underground corridors: how wide they are (m), and how far below the ceiling they hang.")]
            public Vector2 stripLightSize = new(0.15f, 0.06f);
            [Tooltip("Rooms narrower than this both ways (m) are corridors, lit by a strip along the ceiling instead of panels.")]
            public float corridorWidth = 7f;
            [Tooltip("The light over each doorway: its width and height (m).")]
            public Vector2 doorLightSize = new(0.6f, 0.12f);
            [Tooltip("How far above the doorway's frame the light is (m).")]
            public float doorLightGap = 0.15f;
            [Tooltip("Bollards: how high (m), and how wide (m).")]
            public Vector2 bollardSize = new(1f, 0.25f);
            [Tooltip("Bollards at most this far apart along their line (m).")]
            public float bollardSpacing = 6f;
            [Tooltip("Floodlight masts on tower decks: how high (m).")]
            public float floodlightMastHeight = 4f;
            [Tooltip("A floodlight's head: its width and height (m).")]
            public Vector2 floodlightHeadSize = new(0.7f, 0.45f);
            [Tooltip("The cyan accent posts at each end of a bridge: how wide (m), and how far above the guardrail they rise (m).")]
            public Vector2 accentPost = new(0.1f, 0.5f);
        }

        [Serializable]
        public sealed class LightSettings
        {
            [Tooltip("Lights underground at most this far apart along a room or corridor (m).")]
            public float spacing = 12f;
            [Tooltip("The underground lights' intensity.")]
            public float intensity = 3f;
            [Tooltip("The underground lights' colour (and the glow of the level's light fixtures' material, when it's first made).")]
            public Color colour = new(1f, 0.93f, 0.82f);
            [Tooltip("An underground light's range, as a multiple of the larger of its spacing and the room's width.")]
            public float rangeFactor = 1.25f;
            [Tooltip("Whether the fixtures in daylight (door lights, bollards, floodlights) cast light as well as glowing. Off by default: in the midday sun they'd add little but real-time lights.")]
            public bool fixturesCastLight;
            [Tooltip("When fixtures cast light: their intensity and range (m).")]
            public Vector2 fixtureLight = new(2f, 8f);
            [Tooltip("When fixtures cast light: the floodlights' spot angle (degrees).")]
            public float floodlightAngle = 70f;
        }

        [Serializable]
        public sealed class UtilitySettings
        {
            [Tooltip("The pipes run along underground ceilings: the main (steel) and second (orange) pipe's diameters (m).")]
            public Vector2 pipeDiameters = new(0.3f, 0.18f);
            [Tooltip("How far below the ceiling the pipes run (m, to their middles).")]
            public float pipeDrop = 0.45f;
            [Tooltip("How far from the wall the main pipe runs (m, to its middle); the second runs beside it.")]
            public float pipeWallOffset = 0.5f;
            [Tooltip("The hangers holding the pipes up: at most this far apart (m).")]
            public float hangerSpacing = 3f;
            [Tooltip("Pipes are made with this many sides.")]
            [Range(6, 16)] public int pipeSides = 8;
            [Tooltip("A utility station's cabinet: width, height and depth (m). It has a collider: it stands out from the wall far enough to walk into.")]
            public Vector3 utilityPanelSize = new(0.8f, 1.4f, 0.25f);
            [Tooltip("How far above the floor the cabinet's bottom is (m).")]
            public float utilityPanelLift = 0.4f;
            [Tooltip("The conduit up from the cabinet: its diameter (m), and how high above the floor it reaches (m), to the vent.")]
            public Vector2 conduit = new(0.08f, 4f);
            [Tooltip("The vent at the top of the conduit: width and height (m).")]
            public Vector2 ventSize = new(0.7f, 0.45f);
            [Tooltip("The vent's slats this far apart (m).")]
            public float ventSlatSpacing = 0.09f;
        }

        [Serializable]
        public sealed class MarkingSettings
        {
            [Tooltip("How far markings (hazard stripes, corner brackets) stand proud of the surface they're on (m): raised, like thick paint, so they never flicker against it. They have no collider.")]
            public float thickness = 0.01f;
            [Tooltip("How far in from the edge hazard stripes reach (m).")]
            public float hazardWidth = 0.4f;
            [Tooltip("How wide each of the hazard stripes' diagonal bands is (m), along the edge.")]
            public float stripeWidth = 0.25f;
            [Tooltip("Corner brackets round a test lane: each arm's length and width (m).")]
            public Vector2 cornerBracket = new(0.6f, 0.08f);
            [Tooltip("How far out from the lane's footprint the corner brackets are (m).")]
            public float cornerGap = 0.3f;
            [Tooltip("How tall the measured values painted on the floor in front of test lanes are (m).")]
            public float labelHeight = 0.35f;
            [Tooltip("The colour the measured values are painted in: dark, to read on the sunlit concrete.")]
            public Color labelColour = new(0.18f, 0.19f, 0.22f);
        }

        [Serializable]
        public sealed class SignSettings
        {
            [Tooltip("The colour of signs' lettering.")]
            public Color textColour = new(0.95f, 0.96f, 0.97f);
            [Tooltip("The sign gantry over the start of each bridge: how high its beam's top is above the deck (m). Keep its panel's bottom out of jumping reach.")]
            public float gantryHeight = 4.6f;
            [Tooltip("How wide the gantry's posts and beam are (m).")]
            public float gantryPost = 0.15f;
            [Tooltip("The gantry's sign panel: its height (m), and the widest it is (m; narrower bridges get one a metre narrower than the bridge).")]
            public Vector2 gantryPanel = new(0.9f, 6f);
            [Tooltip("How tall the gantry sign's lettering is (m).")]
            public float gantryTextHeight = 0.42f;
            [Tooltip("The sign at each area's entrance: its width and height (m).")]
            public Vector2 areaSignSize = new(4.4f, 1.6f);
            [Tooltip("How tall an area sign's number and name are (m).")]
            public Vector2 areaSignTextHeights = new(0.95f, 0.36f);
            [Tooltip("Free-standing area signs stand this high (m), to their bottom.")]
            public float areaSignLift = 1.2f;
        }

        [Serializable]
        public sealed class DressingSettings
        {
            [Tooltip("Banners: how wide (m), and how deep their swallowtail notch is (m).")]
            public Vector2 banner = new(3f, 0.8f);
            [Tooltip("How far banners hang out from the wall or column they're on (m).")]
            public float bannerStandOff = 0.15f;
            [Tooltip("How wide the orange and white guide lines on floors are (m).")]
            public float guideLineWidth = 0.15f;
            [Tooltip("Spawn pads: how wide the bracketed square round each spawn point is (m).")]
            public float spawnPadSize = 1.6f;
            [Tooltip("Where a mechanic to come will be, a cyan bracketed square this wide (m) on the floor, with its name.")]
            public float futureMarkSize = 1.4f;
            [Tooltip("Floor rulers: a tick every this far (m), a longer one every metre, and a number every this many metres.")]
            public Vector2 rulerTicks = new(0.5f, 2f);
            [Tooltip("The vertical orange bands up tall towers: how wide (m), and how far proud of the face (m).")]
            public Vector2 towerBand = new(1.4f, 0.03f);
        }

        [Serializable]
        public sealed class FoliageSettings
        {
            [Tooltip("Trees: the trunk's height range (m), each tree picking one at random.")]
            public Vector2 trunkHeight = new(1.6f, 2.6f);
            [Tooltip("Trees: the canopy's widest radius range (m).")]
            public Vector2 canopyRadius = new(1.4f, 2.2f);
            [Tooltip("Shrubs: their radius range (m).")]
            public Vector2 shrubRadius = new(0.45f, 0.8f);
            [Tooltip("Planters: how high their concrete rim is (m). They have a collider: they're too high to step onto.")]
            public float planterHeight = 0.6f;
            [Tooltip("Scattered trees: about this far apart (m).")]
            public float scatterSpacing = 10f;
            [Tooltip("Scattered trees are only put where nothing else is within this far (m), so they never crowd a route, stair or platform.")]
            public float scatterClearance = 2.6f;
            [Tooltip("The seed the scattering and the trees' shapes start from: the same seed gives the same trees every build.")]
            public int seed = 1234;
            [Tooltip("Lawns are laid in square cells this wide (m): smaller cells fit them closer round what's on the ground.")]
            public float lawnCell = 1f;
            [Tooltip("Lawns keep this far (m) from a platform's edge, and this far from anything standing on the floor (a wall, stair, rail or column), leaving paving round it.")]
            public Vector2 lawnMargins = new(0.6f, 0.5f);
            [Tooltip("How far lawns stand proud of the floor (m). They have no collider.")]
            public float lawnThickness = 0.015f;
            [Tooltip("Grass tufts on lawns: about this many to a square metre.")]
            public float tuftDensity = 0.6f;
            [Tooltip("Grass tufts' height range (m).")]
            public Vector2 tuftHeight = new(0.18f, 0.42f);
            [Tooltip("How many blades each grass tuft has.")]
            [Range(2, 9)] public int tuftBlades = 5;
            [Tooltip("The chance a lawn cell at the lawn's edge gets a shrub, for a planted border.")]
            [Range(0f, 1f)] public float borderShrubChance = 0.12f;
            [Tooltip("Grass tufts aren't drawn beyond about this far from the camera (m), with a 60° field of view; the lawns under them still are. They're most of the level's triangles, and a few pixels each beyond this.")]
            public float tuftDrawDistance = 70f;
            [Tooltip("Whether grass tufts cast shadows. Off: thousands of thin blades in the shadow maps (which reach the full shadow distance, in view or not) for shadows too fine to see.")]
            public bool tuftsCastShadows;
        }

        [Serializable]
        public sealed class MechanicsSettings
        {
            [Tooltip("How deep the orange band round a moving platform's or crate's edge is (m).")]
            public float edgeBand = 0.15f;
            [Tooltip("Timing gates: how wide their posts are (m), and how high their beam is (m).")]
            public float gatePost = 0.3f;
            [Tooltip("Timing gates: how high the beam's underside is above the floor (m).")]
            public float gateHeight = 4.2f;
            [Tooltip("Timing gates: the sign on the beam showing the course's times, width and height (m).")]
            public Vector2 gatePanel = new(3.6f, 1.1f);
            [Tooltip("Timing gates: how tall the sign's lettering is at most (m); long lines are shrunk to fit.")]
            public float gateTextHeight = 0.32f;
            [Tooltip("Low beams to slide under: how deep the beam is along the way under it (m).")]
            public float lowBeamDepth = 0.5f;
            [Tooltip("Ladders: how far apart their rungs are (m).")]
            public float ladderRungSpacing = 0.3f;
            [Tooltip("Ladders: how thick their rails are, each way (m): the ladder's depth.")]
            public float ladderRail = 0.06f;
            [Tooltip("Ladders: how thick their rungs are (m).")]
            public float ladderRung = 0.035f;
        }

        [Serializable]
        public sealed class CullingSettings
        {
            [Tooltip("Whether building a level bakes its occlusion culling data (after the lighting), so cameras skip what's hidden behind walls, terraces and the ground, not only what's out of view.")]
            public bool bakeOcclusion = true;
            [Tooltip("The smallest thing that hides what's behind it (m): smaller gives finer culling, but a longer bake and more data.")]
            public float smallestOccluder = 5f;
            [Tooltip("The smallest gap the camera can see through (m): keep it under the narrowest doorway or slot, or what's beyond it may be culled when it shouldn't.")]
            public float smallestHole = 0.25f;
            [Tooltip("How much of what the bake sees must be front faces for a cell to count (percent): 100 keeps cameras that end up inside geometry from culling everything.")]
            [Range(5f, 100f)] public float backfaceThreshold = 100f;
        }

        [Serializable]
        public sealed class EnvironmentSettings
        {
            [Tooltip("The sun's rotation (degrees): midday, high (65° up) in the south-south-east, so terraces and stairs cast short, readable shadows.")]
            public Vector3 sunRotation = new(65f, -30f, 0f);
            [Tooltip("The sun's colour.")]
            public Color sunColour = new(1f, 0.96f, 0.9f);
            [Tooltip("The sun's intensity.")]
            public float sunIntensity = 2f;
            [Tooltip("The haze's colour.")]
            public Color hazeColour = new(0.74f, 0.82f, 0.9f);
            [Tooltip("The haze's density (exponential fog): 0.0015 is about 14% haze at 100 m, 45% at 400 m.")]
            public float hazeDensity = 0.0015f;
        }

        [Header("Stairs")] public StairSettings stairs = new();
        [Header("Decks, Bridges and Walkways")] public DeckSettings decks = new();
        [Header("Guardrails")] public RailSettings rails = new();
        [Header("Walls")] public WallSettings walls = new();
        [Header("Platforms")] public PlatformSettings platforms = new();
        [Header("Columns")] public ColumnSettings columns = new();
        [Header("Structure")] public StructureSettings structure = new();
        [Header("Light Fixtures")] public FixtureSettings fixtures = new();
        [Header("Lights")] public LightSettings lights = new();
        [Header("Utilities")] public UtilitySettings utilities = new();
        [Header("Markings")] public MarkingSettings markings = new();
        [Header("Signs and Wayfinding")] public SignSettings signs = new();
        [Header("Dressing")] public DressingSettings dressing = new();
        [Header("Foliage")] public FoliageSettings foliage = new();
        [Header("Culling")] public CullingSettings culling = new();
        [Header("Mechanics")] public MechanicsSettings mechanics = new();
        [Header("Environment")] public EnvironmentSettings environment = new();

        /// <summary>The settings asset, made with the defaults if there isn't one yet.</summary>
        public static GrayboxSettings Load()
        {
            var settings = AssetDatabase.LoadAssetAtPath<GrayboxSettings>(AssetPath);
            if (settings != null) { return settings; }
            settings = CreateInstance<GrayboxSettings>();
            AssetDatabase.CreateAsset(settings, AssetPath);
            AssetDatabase.SaveAssets();
            return settings;
        }

        [MenuItem("Tools/Character Showcase/Level/Generation Settings")]
        private static void SelectInProject()
        {
            Selection.activeObject = Load();
            EditorGUIUtility.PingObject(Selection.activeObject);
        }
    }
}
