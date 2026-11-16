using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CharacterShowcase.EditorTools.Level
{
    /// <summary>
    /// Graybox building blocks shared by the level builders: boxes and solids, stairs, ramps, bridges, guardrails and
    /// connection markers, laid out on the ground plane (a Vector2's x is x east, its y is z north) under rotated or
    /// offset groups, each part of them in one of the palette's surfaces (see Surface). A builder calls Begin, sets the
    /// stair profile and other dimensions it wants, then builds.
    /// </summary>
    public static partial class Graybox
    {
        /// <summary>
        /// Each stair step at most this high (keep it under the character's step height) and this deep: the settings'
        /// profile after Begin, which a builder can change for its area.
        /// </summary>
        public static float StepRise = 0.3f, StepRun = 0.4f;
        /// <summary>How far bridges run out, how thick decks are, and guardrails' height and thickness: the settings' after Begin.</summary>
        public static float BridgeLength = 20f, Deck = 1f, RailHeight = 1.1f, RailThickness = 0.15f;
        /// <summary>The numbers the pieces are made with (see GrayboxSettings), loaded by Begin.</summary>
        public static GrayboxSettings Settings { get; private set; }
        /// <summary>The material everything's given that has no surface of its own in the palette: the one passed to Begin, or the render pipeline's default.</summary>
        public static Material Material;

        /// <summary>
        /// What a piece, or part of one, is made of: each a material of the palette (see UsePalette). The pieces choose
        /// their own (walls are panelled concrete, guardrails metal with an orange top rail, stairs concrete with orange
        /// nosings, the ground paving); boxes and solids are concrete unless a builder says otherwise.
        /// </summary>
        public enum Surface
        {
            /// <summary>Smooth cast concrete: decks, floors, terraces, steps.</summary>
            Concrete,
            /// <summary>Concrete in panels with recessed seams: walls and large masses.</summary>
            Panel,
            /// <summary>The ground: darker concrete in large paving slabs.</summary>
            Ground,
            /// <summary>Dark painted metal: posts, rails, trims, machinery.</summary>
            Graphite,
            /// <summary>Bare steel: columns, supports, catwalks, crates.</summary>
            Steel,
            /// <summary>Orange paint, for edges worth seeing: top rails, stair nosings, platform fascias.</summary>
            Accent,
            /// <summary>A light fixture's glowing face, warm white: ceiling panels, strip lights, door lights, floodlights.</summary>
            Light,
            /// <summary>Glowing cyan, the style sheet's accent light: posts marking the bridges between areas.</summary>
            Cyan,
            /// <summary>Off-white paint: guide lines, banners' emblems, spawn pads, rulers.</summary>
            White,
            /// <summary>Leaves: trees' canopies and shrubs.</summary>
            Foliage,
            /// <summary>Earth and bark: planters' soil, tree trunks.</summary>
            Soil,
            /// <summary>Lawn: grass over open ground and platform tops, and its tufts.</summary>
            Grass,
            /// <summary>Water's surface: see-through, rippling, darker where it's deeper (its own shader: see GrayboxScene).</summary>
            Water,
        }

        private static Material[] palette;

        /// <summary>
        /// The materials the surfaces are given (indexed by Surface) for the builds to come: GrayboxScene sets it before
        /// building. A surface it has no material for is given Material.
        /// </summary>
        public static void UsePalette(Material[] materials) => palette = materials;

        private static readonly List<(object owner, System.Action place)> deferred = new();

        /// <summary>
        /// Whose deferred placements are being put off (see Defer): the level editor's preview sets it to the piece being
        /// built, so it knows what each piece's finishing touches made, and can make them again with the piece.
        /// </summary>
        public static object DeferOwner;

        /// <summary>
        /// Puts off placing something until the whole level's built (RunDeferred, which GrayboxScene calls after every
        /// part): for what has to find the floor under it, or room round it, among everything else.
        /// </summary>
        public static void Defer(System.Action place) => deferred.Add((DeferOwner, place));

        /// <summary>
        /// Places everything put off (see Defer), now that the level's colliders are all where they'll be; each through
        /// around (if given), with the DeferOwner it was put off under.
        /// </summary>
        public static void RunDeferred(System.Action<object, System.Action> around = null)
        {
            Physics.SyncTransforms();
            (object owner, System.Action place)[] places = deferred.ToArray();
            deferred.Clear();
            foreach ((object owner, System.Action place) in places)
            {
                if (around != null) { around(owner, place); } else { place(); }
            }
        }

        /// <summary>Forgets anything put off by a build that didn't finish.</summary>
        public static void ClearDeferred() => deferred.Clear();

        /// <summary>The material for a surface: the palette's, or Material.</summary>
        public static Material For(Surface surface)
        {
            Material material = palette != null && (int)surface < palette.Length ? palette[(int)surface] : null;
            return material != null ? material : Material;
        }

        /// <summary>
        /// Gets ready to build: the material for anything the palette has no surface for (or, if that's null, the render
        /// pipeline's default), the settings loaded, and the dimensions reset to the settings'.
        /// </summary>
        public static void Begin(Material material = null)
        {
            Material = material;
            if (Material == null)
            {
                GameObject probe = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Material = probe.GetComponent<MeshRenderer>().sharedMaterial;
                Object.DestroyImmediate(probe);
            }
            Settings = GrayboxSettings.Load();
            StepRise = Settings.stairs.stepRise;
            StepRun = Settings.stairs.stepRun;
            BridgeLength = Settings.decks.bridgeLength;
            Deck = Settings.decks.deckThickness;
            RailHeight = Settings.rails.railHeight;
            RailThickness = Settings.rails.railThickness;
        }

        /// <summary>
        /// An empty marker where a route leaves for another area (where that area's geometry joins this one), or where a
        /// mechanic to come will be: one named "[Mechanic] (future): …" is marked on the floor under it (see FutureMark).
        /// </summary>
        [Piece]
        public static void Marker(Transform parent, string name, Vector2 at, float height)
        {
            using LevelSpecRecorder.Scope recording = LevelSpecRecorder.Call(nameof(Marker), parent, name, at, height);
            var marker = new GameObject(name);
            marker.transform.SetParent(parent, false);
            marker.transform.localPosition = new Vector3(at.x, height, at.y);
            FutureMark(parent, name, at, height);
        }

        /// <summary>
        /// The name every spawn marker's starts with. Game setup tools look for markers by this name (the level knows
        /// nothing about what spawns there), so keep it in step with SceneGameSetup.SpawnMarkerPrefix.
        /// </summary>
        public const string SpawnMarkerPrefix = "Spawn Point";

        /// <summary>
        /// An empty marker where a character can start: its feet at the given height, facing yaw degrees clockwise from
        /// north, with a spawn pad on the floor under it (see SpawnPad). Only a place: the game setup puts what spawns there.
        /// </summary>
        [Piece]
        public static void SpawnMarker(Transform parent, string name, Vector2 at, float height, float yaw)
        {
            using LevelSpecRecorder.Scope recording = LevelSpecRecorder.Call(nameof(SpawnMarker), parent, name, at, height, yaw);
            var marker = new GameObject($"{SpawnMarkerPrefix} ({name})");
            marker.transform.SetParent(parent, false);
            marker.transform.localPosition = new Vector3(at.x, height, at.y);
            marker.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            SpawnPad(parent, at, height, yaw);
        }

        // ---- Shapes (on the ground plane: x east, y here meaning z north) ----

        /// <summary>A regular octagon with flats facing the compass points, the given distance from its centre to each flat.</summary>
        public static Vector2[] Octagon(float apothem)
        {
            var points = new Vector2[8];
            float radius = apothem / Mathf.Cos(22.5f * Mathf.Deg2Rad);
            for (int i = 0; i < 8; i++)
            {
                float angle = (22.5f + 45f * i) * Mathf.Deg2Rad;
                points[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            }
            return points;
        }

        /// <summary>An axis-aligned rectangle from its centre and size.</summary>
        public static Vector2[] Rect(Vector2 centre, Vector2 size)
        {
            Vector2 h = size / 2f;
            return new[] { centre + new Vector2(-h.x, -h.y), centre + new Vector2(h.x, -h.y), centre + new Vector2(h.x, h.y), centre + new Vector2(-h.x, h.y) };
        }

        /// <summary>An axis-aligned rectangle from its corners' coordinates.</summary>
        public static Vector2[] Rect(float x0, float z0, float x1, float z1) => Rect(new Vector2((x0 + x1) / 2f, (z0 + z1) / 2f), new Vector2(x1 - x0, z1 - z0));

        /// <summary>A shape moved by offset.</summary>
        public static Vector2[] Offset(Vector2[] shape, Vector2 offset)
        {
            var moved = new Vector2[shape.Length];
            for (int i = 0; i < shape.Length; i++) { moved[i] = shape[i] + offset; }
            return moved;
        }

        /// <summary>A convex shape cut to where nx·x + nz·z ≤ c.</summary>
        public static Vector2[] Clip(Vector2[] shape, float nx, float nz, float c)
        {
            var result = new List<Vector2>();
            for (int i = 0; i < shape.Length; i++)
            {
                Vector2 p = shape[i], q = shape[(i + 1) % shape.Length];
                float dp = nx * p.x + nz * p.y - c, dq = nx * q.x + nz * q.y - c;
                if (dp <= 0f) { result.Add(p); }
                if (dp * dq < 0f) { result.Add(Vector2.Lerp(p, q, dp / (dp - dq))); }
            }
            return result.ToArray();
        }

        // ---- Objects ----

        /// <summary>An empty group under parent, for the pieces made in it.</summary>
        [Piece]
        public static Transform Group(Transform parent, string name)
        {
            using LevelSpecRecorder.Scope recording = LevelSpecRecorder.Call(nameof(Group), parent, name);
            var group = new GameObject(name).transform;
            group.SetParent(parent, false);
            return group;
        }

        /// <summary>A group turned about the vertical axis through the origin, so its contents can be laid out facing north.</summary>
        [Piece]
        public static Transform Turned(Transform parent, string name, float yaw)
        {
            using LevelSpecRecorder.Scope recording = LevelSpecRecorder.Call(nameof(Turned), parent, name, yaw);
            Transform group = Group(parent, name);
            group.localRotation = Quaternion.Euler(0f, yaw, 0f);
            return group;
        }

        /// <summary>
        /// A group placed at position (and turned yaw about the vertical), so its contents can be laid out in its own
        /// coordinates, from its origin. Builders place groups with this, not by moving them, so a level spec records it.
        /// </summary>
        [Piece]
        public static Transform Placed(Transform parent, string name, Vector3 position, float yaw = 0f)
        {
            using LevelSpecRecorder.Scope recording = LevelSpecRecorder.Call(nameof(Placed), parent, name, position, yaw);
            Transform group = Group(parent, name);
            group.SetLocalPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            return group;
        }

        /// <summary>
        /// A box (Unity's cube with a UV2, and its box collider) centred at localCentre in the parent, turned yaw about
        /// the vertical, in the surface's material. Static.
        /// </summary>
        [Piece]
        public static GameObject Box(Transform parent, string name, Vector3 localCentre, Vector3 size, float yaw = 0f, Surface surface = Surface.Concrete)
        {
            using LevelSpecRecorder.Scope recording = LevelSpecRecorder.Call(nameof(Box), parent, name, localCentre, size, yaw, surface);
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.SetLocalPositionAndRotation(localCentre, Quaternion.Euler(0f, yaw, 0f));
            box.transform.localScale = size;
            // Unity's cube with a UV2, so things can be painted onto one face of it (see GrayboxUV2).
            box.GetComponent<MeshFilter>().sharedMesh = GrayboxUV2.Cube;
            box.GetComponent<MeshRenderer>().sharedMaterial = For(surface);
            MakeStatic(box);
            return box;
        }

        /// <summary>A box over a footprint on the ground plane (centre and size in the parent's x and z), from y0 to y1.</summary>
        [Piece]
        public static GameObject BoxXZ(Transform parent, string name, Vector2 centre, Vector2 size, float y0, float y1, float yaw = 0f,
            Surface surface = Surface.Concrete)
        {
            using LevelSpecRecorder.Scope recording = LevelSpecRecorder.Call(nameof(BoxXZ), parent, name, centre, size, y0, y1, yaw, surface);
            return Box(parent, name, new Vector3(centre.x, (y0 + y1) / 2f, centre.y), new Vector3(size.x, y1 - y0, size.y), yaw, surface);
        }

        /// <summary>A box as BoxXZ makes it, but only to see: no collider, for trims too small to matter to movement.</summary>
        [Piece]
        public static GameObject Trim(Transform parent, string name, Vector2 centre, Vector2 size, float y0, float y1, float yaw, Surface surface)
        {
            using LevelSpecRecorder.Scope recording = LevelSpecRecorder.Call(nameof(Trim), parent, name, centre, size, y0, y1, yaw, surface);
            GameObject trim = BoxXZ(parent, name, centre, size, y0, y1, yaw, surface);
            Object.DestroyImmediate(trim.GetComponent<BoxCollider>());
            MakeStatic(trim, occluder: false);
            return trim;
        }

        /// <summary>An axis-aligned box over x from x0 to x1 and z from z0 to z1 (in the parent's x and z), from y0 to y1.</summary>
        [Piece]
        public static GameObject Block(Transform parent, string name, float x0, float x1, float z0, float z1, float y0, float y1,
            Surface surface = Surface.Concrete)
        {
            using LevelSpecRecorder.Scope recording = LevelSpecRecorder.Call(nameof(Block), parent, name, x0, x1, z0, z1, y0, y1, surface);
            return BoxXZ(parent, name, new Vector2((x0 + x1) / 2f, (z0 + z1) / 2f), new Vector2(x1 - x0, z1 - z0), y0, y1, 0f, surface);
        }

        /// <summary>
        /// Level geometry doesn't move: static for batching, occlusion and reflection probes. Not Contribute GI: the
        /// level has no baked or realtime GI (it's lit by the sun and the sky's ambient), so nothing would use it, and
        /// with it the lighting bake packed lightmaps for every piece anyway, which took about seven minutes. Only solid
        /// pieces (boxes, solids, stairs, columns) are occluders, hiding what's behind them in the occlusion bake; what
        /// can be seen through or round (railings, trusses, frames, foliage, lawns, markings, fixtures, pipes, signs) is
        /// only hidden, never hides: as an occluder it would only cost the bake and risk culling what shows through it.
        /// </summary>
        public static void MakeStatic(GameObject obj, bool occluder = true) =>
            GameObjectUtility.SetStaticEditorFlags(obj, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic
                | StaticEditorFlags.ReflectionProbeStatic | (occluder ? StaticEditorFlags.OccluderStatic : 0));
    }
}
