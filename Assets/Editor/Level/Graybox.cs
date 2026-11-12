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
        }

        private static Material[] palette;

        /// <summary>
        /// The materials the surfaces are given (indexed by Surface) for the builds to come: GrayboxScene sets it before
        /// building. A surface it has no material for is given Material.
        /// </summary>
        public static void UsePalette(Material[] materials) => palette = materials;

        private static readonly List<System.Action> deferred = new();

        /// <summary>
        /// Puts off placing something until the whole level's built (RunDeferred, which GrayboxScene calls after every
        /// part): for what has to find the floor under it, or room round it, among everything else.
        /// </summary>
        public static void Defer(System.Action place) => deferred.Add(place);

        /// <summary>Places everything put off (see Defer), now that the level's colliders are all where they'll be.</summary>
        public static void RunDeferred()
        {
            Physics.SyncTransforms();
            System.Action[] places = deferred.ToArray();
            deferred.Clear();
            foreach (System.Action place in places) { place(); }
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

        // ---- Pieces ----

        /// <summary>
        /// A straight flight of solid steps from base (its bottom edge's middle) along direction, rising from y0 to y1,
        /// each at most StepRise high and StepRun deep, and solid down to floor (the ground, unless it's below ground).
        /// It's one object: one mesh of the whole flight (see FlightMesh) and a box collider per step. Returns its
        /// length along the direction.
        /// </summary>
        public static float Flight(Transform parent, string name, Vector2 basePoint, Vector2 direction, float width, float y0, float y1,
            float floor = 0f)
        {
            int steps = Mathf.CeilToInt((y1 - y0) / StepRise - 1e-4f);
            float rise = (y1 - y0) / steps;
            float yaw = Mathf.Atan2(direction.x, direction.y) * Mathf.Rad2Deg;
            // Solid to the ground, so a flight over open ground stands on something.
            float bottom = Mathf.Min(floor, y0);

            // Laid out facing north (+z up the flight) from its base, and turned to face the direction.
            var flight = new GameObject(name);
            flight.transform.SetParent(parent, false);
            flight.transform.SetLocalPositionAndRotation(new Vector3(basePoint.x, 0f, basePoint.y), Quaternion.Euler(0f, yaw, 0f));
            if (steps < 1) { return 0f; }
            FlightMesh(steps, width, StepRun, y0, rise, bottom).AddTo(flight, name);
            for (int i = 0; i < steps; i++)
            {
                float top = y0 + rise * (i + 1);
                var step = flight.AddComponent<BoxCollider>();
                step.center = new Vector3(0f, (bottom + top) / 2f, (i + 0.5f) * StepRun);
                step.size = new Vector3(width, top - bottom, StepRun);
            }
            MakeStatic(flight);
            return steps * StepRun;
        }

        /// <summary>
        /// The visible solid of a flight of steps, up +z from z = 0 and centred on x = 0: its steps' treads (each with an
        /// orange nosing strip along its front edge) and risers (the first running down to the bottom), its back, and its
        /// two sides, each side one stair-shaped face. It has no underside, since a flight always stands on its floor.
        /// That's 10 triangles a step and 2 more, with no faces hidden inside it and none overlapping (where a box per
        /// step's sides met in one plane and flickered).
        /// </summary>
        private static GrayboxMesh FlightMesh(int steps, float width, float run, float y0, float rise, float bottom)
        {
            float half = width / 2f, length = steps * run;
            var mesh = new GrayboxMesh();
            const Surface concrete = Surface.Concrete;

            for (int i = 0; i < steps; i++)
            {
                float z0 = i * run, zn = z0 + Settings.stairs.nosingDepth, z1 = z0 + run, top = y0 + rise * (i + 1), below = i == 0 ? bottom : top - rise;
                mesh.Quad(Surface.Accent, Vector3.up, new Vector3(-half, top, z0), new Vector3(half, top, z0), new Vector3(half, top, zn), new Vector3(-half, top, zn));
                mesh.Quad(concrete, Vector3.up, new Vector3(-half, top, zn), new Vector3(half, top, zn), new Vector3(half, top, z1), new Vector3(-half, top, z1));
                mesh.Quad(concrete, Vector3.back, new Vector3(-half, below, z0), new Vector3(half, below, z0), new Vector3(half, top, z0), new Vector3(-half, top, z0));
            }
            float summit = y0 + rise * steps;
            mesh.Quad(concrete, Vector3.forward, new Vector3(-half, bottom, length), new Vector3(half, bottom, length), new Vector3(half, summit, length),
                new Vector3(-half, summit, length));

            // Each side's outline: the bottom front corner, then each step's front top corner (P) and back top corner
            // (Q), then the bottom back corner. It's triangulated with the fewest triangles (as many as its corners, less
            // two) and no extra vertices: each step after the first gives the triangle under its tread and beside its
            // riser (Q before, P, Q after), and what's left, under the line of back corners along the stair's slope, is
            // fanned from the bottom back corner, which sees all of it.
            foreach (float x in new[] { -half, half })
            {
                Vector3 normal = new Vector3(Mathf.Sign(x), 0f, 0f);
                var outline = new List<Vector3> { new(x, bottom, 0f) };
                for (int i = 0; i < steps; i++)
                {
                    float top = y0 + rise * (i + 1);
                    outline.Add(new Vector3(x, top, i * run));
                    outline.Add(new Vector3(x, top, (i + 1) * run));
                }
                outline.Add(new Vector3(x, bottom, length));
                int first = mesh.Face(normal, outline);
                int front = first, back = first + outline.Count - 1;
                // P(i): step i's front top corner, steps counted from 0; Q(i): step i's back top corner, counted from 1.
                int P(int i) => first + 1 + 2 * i;
                int Q(int i) => first + 2 * i;
                for (int i = 1; i < steps; i++) { mesh.Triangle(concrete, Q(i), P(i), Q(i + 1), normal); }
                mesh.Triangle(concrete, back, front, P(0), normal);
                mesh.Triangle(concrete, back, P(0), Q(1), normal);
                for (int i = 1; i < steps; i++) { mesh.Triangle(concrete, back, Q(i), Q(i + 1), normal); }
            }
            return mesh;
        }

        /// <summary>How long a flight from y0 to y1 is, in the current stair profile.</summary>
        public static float FlightLength(float y0, float y1) => Mathf.CeilToInt(Mathf.Abs(y1 - y0) / StepRise - 1e-4f) * StepRun;

        /// <summary>A ramp from start along direction, falling (or rising) from y0 to y1 over its length: a tilted slab, with a support under its high end.</summary>
        public static void Ramp(Transform parent, string name, Vector2 start, Vector2 direction, float width, float length, float y0, float y1)
        {
            Transform ramp = Group(parent, name);
            float yaw = Mathf.Atan2(direction.x, direction.y) * Mathf.Rad2Deg;
            float slope = Mathf.Atan2(y0 - y1, length) * Mathf.Rad2Deg;
            float slab = Mathf.Sqrt(length * length + (y1 - y0) * (y1 - y0));
            Vector2 middle = start + direction * (length / 2f);
            // Its top surface meets y0 at the start and y1 at the end: the slab's centre is half its thickness below.
            Quaternion rotation = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(slope, 0f, 0f);
            Vector3 centre = new Vector3(middle.x, (y0 + y1) / 2f, middle.y) - rotation * Vector3.up * (Deck / 2f);
            GameObject deck = Box(ramp, "Ramp", centre, new Vector3(width, Deck, slab));
            deck.transform.localRotation = rotation;
            float spacing = Settings.decks.rampSupportSpacing;
            Vector2 support = Settings.decks.walkwayColumnSize;
            for (float d = 0.75f * spacing; d < length - 2f; d += spacing)
            {
                float y = Mathf.Lerp(y0, y1, d / length) - Deck;
                foreach (float side in new[] { -1f, 1f })
                {
                    Vector2 at = start + direction * d + new Vector2(direction.y, -direction.x) * (side * (width / 2f - support.x));
                    if (y > 0.5f) { IBeamColumn(ramp, "Support", at, support, 0f, y, yaw); }
                }
            }
        }

        /// <summary>
        /// A bridge out from start along direction at the given height, with guardrails along both sides, columns
        /// down to the ground, a marker at its far end where the next area joins, a cyan accent post at each corner of
        /// its deck, so the way from one area to another reads from a distance, and a sign gantry over its start naming
        /// where it goes (when its name says: "To [place] (...)").
        /// </summary>
        public static void Bridge(Transform bridges, Transform rails, Transform connections, string name, Vector2 start, Vector2 direction,
            float width, float height)
        {
            Transform bridge = Group(bridges, name.Replace("To ", "Bridge to ").Replace("Future Connection", "Bridge"));
            Vector2 end = Walkway(bridge, rails, start, direction, BridgeLength, width, height);
            Marker(connections, name, end, height);
            string destination = Destination(name);
            if (destination != null) { BridgeGantry(bridge, destination, start, direction, width, height); }
            Vector2 side = new Vector2(direction.y, -direction.x) * (width / 2f);
            // Set in from the bridge's ends a little, so the posts' faces aren't in the plane of the top rails' ends.
            float inset = Settings.fixtures.accentPost.x / 2f + 0.02f;
            foreach (Vector2 at in new[] { start + direction * inset, end - direction * inset })
            {
                AccentPost(bridge, at + side, height);
                AccentPost(bridge, at - side, height);
            }
        }

        /// <summary>
        /// A deck from start along direction for length at the given height, with guardrails along both sides, and
        /// I-beam columns down to the ground; or, if it's suspended (hung between the things at its ends), a truss
        /// under each side instead. Returns its far end.
        /// </summary>
        public static Vector2 Walkway(Transform parent, Transform rails, Vector2 start, Vector2 direction, float length, float width,
            float height, bool suspended = false)
        {
            float yaw = Mathf.Atan2(direction.x, direction.y) * Mathf.Rad2Deg;
            Vector2 end = start + direction * length, side = new Vector2(direction.y, -direction.x) * (width / 2f);
            BoxXZ(parent, "Deck", start + direction * (length / 2f), new Vector2(width, length), height - Deck, height, yaw);
            GrayboxSettings.DeckSettings decks = Settings.decks;
            if (!suspended)
            {
                for (float d = decks.walkwayColumnSpacing / 2f; d < length; d += decks.walkwayColumnSpacing)
                {
                    foreach (float s in new[] { -1f, 1f })
                    {
                        IBeamColumn(parent, "Column", start + direction * d + side * (s * decks.walkwayColumnSpread), decks.walkwayColumnSize, 0f,
                            height - Deck, yaw);
                    }
                }
            }
            else
            {
                // Just in from the deck's edges, so the edge is still a ledge with something to hang against.
                Vector2 inset = side.normalized * (width / 2f - decks.trussInset);
                float depth = Mathf.Min(decks.trussMaxDepth, length / 6f);
                foreach (float s in new[] { -1f, 1f })
                {
                    Truss(parent, "Truss", start + inset * s, end + inset * s, height - Deck - depth, height - Deck);
                }
            }
            Rail(rails, start + side, end + side, height);
            Rail(rails, start - side, end - side, height);
            return end;
        }

        /// <summary>
        /// A wall of panelled concrete along the line from a to b (centred on it), from y0 to y1, with openings (centre
        /// on the line, width, and the opening's bottom and top: the wall's filled in below and above each), pilasters
        /// along it and frames round its openings (see WallDetail).
        /// </summary>
        public static void Wall(Transform parent, string name, Vector2 a, Vector2 b, float y0, float y1, float thickness,
            params (Vector2 centre, float width, float bottom, float top)[] openings)
        {
            float length = Vector2.Distance(a, b);
            if (length < 0.05f) { return; }
            Vector2 dir = (b - a) / length;
            float yaw = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;
            void Piece(string piece, float s, float e, float bottom, float top)
            {
                if (e - s < 0.05f || top - bottom < 0.05f) { return; }
                BoxXZ(parent, piece, a + dir * ((s + e) / 2f), new Vector2(thickness, e - s), bottom, top, yaw, Surface.Panel);
            }
            var cuts = new List<(float from, float to, float bottom, float top)>();
            foreach ((Vector2 centre, float width, float bottom, float top) in openings)
            {
                float t = Vector2.Dot(centre - a, dir);
                cuts.Add((Mathf.Clamp(t - width / 2f, 0f, length), Mathf.Clamp(t + width / 2f, 0f, length), bottom, top));
            }
            cuts.Sort((x, y) => x.from.CompareTo(y.from));
            float from = 0f;
            foreach ((float cutFrom, float cutTo, float bottom, float top) in cuts)
            {
                Piece(name, from, cutFrom, y0, y1);
                Piece(name + " (Below Opening)", cutFrom, cutTo, y0, bottom);
                Piece(name + " (Above Opening)", cutFrom, cutTo, top, y1);
                from = Mathf.Max(from, cutTo);
            }
            Piece(name, from, length, y0, y1);
            WallDetail(parent, name, a, dir, length, y0, y1, thickness, cuts);
        }

        /// <summary>Guardrails along every edge of a shape at the given height, leaving gaps (centre, width) where routes go through.</summary>
        public static void RailAround(Transform rails, Vector2[] shape, float height, params (Vector2 centre, float width)[] gaps)
        {
            for (int i = 0; i < shape.Length; i++) { Rail(rails, shape[i], shape[(i + 1) % shape.Length], height, gaps); }
        }

        /// <summary>
        /// A guardrail from a to b on a surface at the given height, leaving gaps (centre, width) that lie along it: one
        /// object for each run between gaps (see GuardrailRun).
        /// </summary>
        public static void Rail(Transform rails, Vector2 a, Vector2 b, float height, params (Vector2 centre, float width)[] gaps)
        {
            float length = Vector2.Distance(a, b);
            if (length < 0.05f) { return; }
            Vector2 dir = (b - a) / length;
            var cuts = new List<(float from, float to)>();
            foreach ((Vector2 centre, float width) in gaps)
            {
                float t = Vector2.Dot(centre - a, dir);
                Vector2 off = centre - (a + dir * t);
                if (off.magnitude > 0.1f || t < -width / 2f || t > length + width / 2f) { continue; }
                cuts.Add((t - width / 2f, t + width / 2f));
            }
            cuts.Sort((x, y) => x.from.CompareTo(y.from));
            float from = 0f;
            float yaw = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;
            void Piece(float s, float e)
            {
                if (e - s < 0.2f) { return; }
                GuardrailRun(rails, a + dir * ((s + e) / 2f), e - s, height, yaw);
            }
            foreach ((float cutFrom, float cutTo) in cuts)
            {
                Piece(from, Mathf.Clamp(cutFrom, 0f, length));
                from = Mathf.Max(from, Mathf.Clamp(cutTo, 0f, length));
            }
            Piece(from, length);
        }

        /// <summary>
        /// One straight run of guardrail, centred on centre along yaw, on a surface at the given height: graphite posts at
        /// most the settings' post spacing apart under an orange top rail, a graphite mid rail, and an orange fascia band along the edge
        /// it stands on, just below the surface and standing a little proud of the face under it. One mesh, and one box
        /// collider the full height and length (RailThickness thick), so nothing can slip between the rails: the
        /// collision is that of the solid guardrails the level had before.
        /// </summary>
        private static void GuardrailRun(Transform parent, Vector2 centre, float length, float height, float yaw)
        {
            var rail = new GameObject("Guardrail");
            rail.transform.SetParent(parent, false);
            rail.transform.SetLocalPositionAndRotation(new Vector3(centre.x, height, centre.y), Quaternion.Euler(0f, yaw, 0f));

            // Laid out along +z, centred; the top rail sits on the posts. The mid rail stops inside the end posts and is
            // narrower than them (as they are than the top rail), so no two faces share a plane but where they meet face
            // to face.
            GrayboxSettings.RailSettings r = Settings.rails;
            float post = r.postSize / 2f, topRail = r.topRailSize.x / 2f, cap = r.topRailSize.y, midRail = r.midRailSize.x / 2f;
            float half = length / 2f, top = RailHeight, mid = top * r.midRailHeight, fascia = RailThickness / 2f + r.fasciaProud;
            var mesh = new GrayboxMesh();
            mesh.Box(Surface.Accent, new Vector3(-topRail, top - cap, -half), new Vector3(topRail, top, half));
            mesh.Box(Surface.Graphite, new Vector3(-midRail, mid, -half + post), new Vector3(midRail, mid + r.midRailSize.y, half - post));
            int bays = Mathf.Max(1, Mathf.CeilToInt(length / r.postSpacing - 1e-3f));
            for (int i = 0; i <= bays; i++)
            {
                float z = Mathf.Lerp(-half + post, half - post, (float)i / bays);
                // No top (under the top rail) or bottom (on the surface).
                mesh.Box(Surface.Graphite, new Vector3(-post, 0f, z - post), new Vector3(post, top - cap, z + post), top: false, bottom: false);
            }
            mesh.Box(Surface.Accent, new Vector3(-fascia, -r.fasciaDrop.y, -half), new Vector3(fascia, -r.fasciaDrop.x, half));
            mesh.AddTo(rail, "Guardrail");

            var collider = rail.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, top / 2f, 0f);
            collider.size = new Vector3(RailThickness, top, length);
            MakeStatic(rail, occluder: false);
        }

        /// <summary>
        /// An empty marker where a route leaves for another area (where that area's geometry joins this one), or where a
        /// mechanic to come will be: one named "[Mechanic] (future): …" is marked on the floor under it (see FutureMark).
        /// </summary>
        public static void Marker(Transform parent, string name, Vector2 at, float height)
        {
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
        public static void SpawnMarker(Transform parent, string name, Vector2 at, float height, float yaw)
        {
            var marker = new GameObject($"{SpawnMarkerPrefix} ({name})");
            marker.transform.SetParent(parent, false);
            marker.transform.localPosition = new Vector3(at.x, height, at.y);
            marker.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            SpawnPad(parent, at, height, yaw);
        }

        /// <summary>
        /// A level slab over an axis-aligned area (a Rect's x is x, its y is z), from y0 to y1, with axis-aligned holes
        /// cut through it: built as the fewest boxes the holes leave, band by band.
        /// </summary>
        public static void SlabWithHoles(Transform parent, string name, UnityEngine.Rect area, IEnumerable<UnityEngine.Rect> holes,
            float y0, float y1, Surface surface = Surface.Concrete)
        {
            var cuts = new List<UnityEngine.Rect>();
            foreach (UnityEngine.Rect hole in holes) { if (hole.Overlaps(area)) { cuts.Add(hole); } }
            var zs = new SortedSet<float> { area.yMin, area.yMax };
            foreach (UnityEngine.Rect hole in cuts)
            {
                zs.Add(Mathf.Clamp(hole.yMin, area.yMin, area.yMax));
                zs.Add(Mathf.Clamp(hole.yMax, area.yMin, area.yMax));
            }
            var bands = new List<(float z0, float z1, List<(float x0, float x1)> spans)>();
            float? previous = null;
            foreach (float z in zs)
            {
                if (previous is float z0 && z - z0 > 0.001f)
                {
                    float mid = (z0 + z) / 2f;
                    var blocked = new List<(float, float)>();
                    foreach (UnityEngine.Rect hole in cuts) { if (hole.yMin < mid && hole.yMax > mid) { blocked.Add((hole.xMin, hole.xMax)); } }
                    blocked.Sort();
                    var spans = new List<(float, float)>();
                    float x = area.xMin;
                    foreach ((float b0, float b1) in blocked)
                    {
                        if (b0 > x) { spans.Add((x, Mathf.Min(b0, area.xMax))); }
                        x = Mathf.Max(x, b1);
                    }
                    if (x < area.xMax) { spans.Add((x, area.xMax)); }
                    // Merge with the band below when the spans are the same.
                    if (bands.Count > 0 && SameSpans(bands[^1].spans, spans)) { bands[^1] = (bands[^1].z0, z, spans); }
                    else { bands.Add((z0, z, spans)); }
                }
                previous = z;
            }
            int n = 0;
            foreach ((float z0, float z1, List<(float x0, float x1)> spans) in bands)
            {
                foreach ((float x0, float x1) in spans)
                {
                    if (x1 - x0 < 0.001f) { continue; }
                    BoxXZ(parent, cuts.Count == 0 ? name : $"{name} {++n}", new Vector2((x0 + x1) / 2f, (z0 + z1) / 2f), new Vector2(x1 - x0, z1 - z0), y0, y1, 0f, surface);
                }
            }
        }

        /// <summary>
        /// A placeholder ground at ±0 (a 1 m slab under it, in large paving slabs) over a square half across from the parent's origin, open
        /// where anything goes below it (stairwells, shafts, basements).
        /// </summary>
        public static void Ground(Transform parent, float half, IEnumerable<UnityEngine.Rect> openings) =>
            SlabWithHoles(Group(parent, "Ground (placeholder)"), "Ground", UnityEngine.Rect.MinMaxRect(-half, -half, half, half), openings, -1f, 0f, Surface.Ground);

        private static bool SameSpans(List<(float x0, float x1)> a, List<(float x0, float x1)> b)
        {
            if (a.Count != b.Count) { return false; }
            for (int i = 0; i < a.Count; i++)
            {
                if (Mathf.Abs(a[i].x0 - b[i].x0) > 0.001f || Mathf.Abs(a[i].x1 - b[i].x1) > 0.001f) { return false; }
            }
            return true;
        }

        /// <summary>
        /// One axis-aligned room or passage of an enclosed network (see Cells): its area (a Rect's y is z), its floor
        /// and ceiling heights, and whether it's open above (a stairwell or shaft) instead of having a ceiling.
        /// </summary>
        public struct Cell
        {
            public string Name;
            public UnityEngine.Rect Area;
            public float Floor, Ceiling;
            public bool Open;

            public Cell(string name, float x0, float z0, float x1, float z1, float floor, float ceiling, bool open = false)
            {
                Name = name;
                Area = UnityEngine.Rect.MinMaxRect(x0, z0, x1, z1);
                Floor = floor;
                Ceiling = ceiling;
                Open = open;
            }

            public float Top(float openTop) => Open ? openTop : Ceiling;
        }

        /// <summary>
        /// An enclosed network of rooms and passages from cells that tile without overlapping: a floor under each, a
        /// ceiling over each closed one, and walls round the outside of the whole, open wherever two cells meet or a
        /// cell meets a portal (a doorway into something built separately). Open cells' walls rise to openTop. Where a
        /// taller cell meets a lower one, a header closes the wall above the opening.
        /// </summary>
        public static void Cells(Transform parent, IList<Cell> cells, IList<UnityEngine.Rect> portals, float openTop, float thickness = 0.5f)
        {
            const float slab = 0.5f, eps = 0.01f;
            bool Inside(Vector2 p)
            {
                foreach (Cell c in cells) { if (c.Area.Contains(p)) { return true; } }
                foreach (UnityEngine.Rect r in portals) { if (r.Contains(p)) { return true; } }
                return false;
            }
            foreach (Cell cell in cells)
            {
                Transform group = Group(parent, cell.Name);
                UnityEngine.Rect a = cell.Area;
                BoxXZ(group, "Floor", a.center, a.size, cell.Floor - slab, cell.Floor);
                if (!cell.Open) { BoxXZ(group, "Ceiling", a.center, a.size, cell.Ceiling, cell.Ceiling + slab); }
                float top = cell.Open ? openTop : cell.Ceiling + slab;

                // Each edge: from p to q, with the outward normal.
                var edges = new[]
                {
                    (p: new Vector2(a.xMin, a.yMax), q: new Vector2(a.xMax, a.yMax), n: Vector2.up),
                    (p: new Vector2(a.xMin, a.yMin), q: new Vector2(a.xMax, a.yMin), n: Vector2.down),
                    (p: new Vector2(a.xMax, a.yMin), q: new Vector2(a.xMax, a.yMax), n: Vector2.right),
                    (p: new Vector2(a.xMin, a.yMin), q: new Vector2(a.xMin, a.yMax), n: Vector2.left),
                };
                foreach ((Vector2 p, Vector2 q, Vector2 n) in edges)
                {
                    float length = Vector2.Distance(p, q);
                    Vector2 dir = (q - p) / length;
                    // What's just outside this edge, along it: openings (with the neighbour's top, if a cell).
                    var openings = new List<(float from, float to, float neighbourTop)>();
                    void Consider(UnityEngine.Rect other, float neighbourTop)
                    {
                        Vector2 probe = p + n * eps;
                        bool across = n.x != 0f ? other.xMin <= probe.x && other.xMax >= probe.x : other.yMin <= probe.y && other.yMax >= probe.y;
                        if (!across) { return; }
                        float o0 = n.x != 0f ? other.yMin : other.xMin, o1 = n.x != 0f ? other.yMax : other.xMax;
                        float e0 = n.x != 0f ? p.y : p.x;
                        float from = Mathf.Max(0f, o0 - e0), to = Mathf.Min(length, o1 - e0);
                        if (to - from > eps) { openings.Add((from, to, neighbourTop)); }
                    }
                    foreach (Cell other in cells)
                    {
                        if (!other.Area.Equals(cell.Area)) { Consider(other.Area, other.Open ? openTop : other.Ceiling + slab); }
                    }
                    foreach (UnityEngine.Rect portal in portals) { Consider(portal, top); }
                    openings.Sort((x, y) => x.from.CompareTo(y.from));

                    float yaw = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;
                    Vector2 offset = n * (thickness / 2f);
                    void Piece(string name, float s, float e, float bottom, float pieceTop, bool extendStart, bool extendEnd)
                    {
                        // A graphite skirting along the foot of the wall inside the cell. The east and west walls' stop
                        // short of the corners, where the north and south walls' run on to them, so none overlap.
                        Vector2 skirting = Settings.walls.skirtingSize;
                        float inset = n.x != 0f ? skirting.y : 0f;
                        if (name == "Wall" && e - s - 2f * inset > eps)
                        {
                            Trim(group, "Skirting", p + dir * ((s + e) / 2f) - n * (skirting.y / 2f), new Vector2(skirting.y, e - s - 2f * inset),
                                cell.Floor, cell.Floor + skirting.x, yaw, Surface.Graphite);
                        }
                        // Close the outside corners: extend past a corner of the cell unless that's inside the network.
                        if (extendStart && !Inside(p + dir * (s - thickness / 2f) + n * (thickness / 2f))) { s -= thickness; }
                        if (extendEnd && !Inside(p + dir * (e + thickness / 2f) + n * (thickness / 2f))) { e += thickness; }
                        if (e - s < eps || pieceTop - bottom < eps) { return; }
                        BoxXZ(group, name, p + dir * ((s + e) / 2f) + offset, new Vector2(thickness, e - s), bottom, pieceTop, yaw, Surface.Panel);
                    }
                    float at = 0f;
                    foreach ((float from, float to, float neighbourTop) in openings)
                    {
                        Piece("Wall", at, from, cell.Floor - slab, top, at < eps, false);
                        if (neighbourTop < top - eps) { Piece("Header", from, to, neighbourTop, top, false, false); }
                        at = Mathf.Max(at, to);
                    }
                    Piece("Wall", at, length, cell.Floor - slab, top, at < eps, true);
                }
            }
        }

        /// <summary>
        /// Walls round an axis-aligned opening (x from x0 to x1, z from z0 to z1), from y0 to y1, outside it: a shaft's
        /// chimney above the ground. The north and south walls run across the corners.
        /// </summary>
        public static void ShaftWalls(Transform parent, string name, float x0, float z0, float x1, float z1, float y0, float y1, float thickness = 0.5f)
        {
            Transform shaft = Group(parent, name);
            float t = thickness;
            Block(shaft, "Wall (North)", x0 - t, x1 + t, z1, z1 + t, y0, y1, Surface.Panel);
            Block(shaft, "Wall (South)", x0 - t, x1 + t, z0 - t, z0, y0, y1, Surface.Panel);
            Block(shaft, "Wall (East)", x1, x1 + t, z0, z1, y0, y1, Surface.Panel);
            Block(shaft, "Wall (West)", x0 - t, x0, z0, z1, y0, y1, Surface.Panel);
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

        public static Transform Group(Transform parent, string name)
        {
            var group = new GameObject(name).transform;
            group.SetParent(parent, false);
            return group;
        }

        /// <summary>A group turned about the vertical axis through the origin, so its contents can be laid out facing north.</summary>
        public static Transform Turned(Transform parent, string name, float yaw)
        {
            Transform group = Group(parent, name);
            group.localRotation = Quaternion.Euler(0f, yaw, 0f);
            return group;
        }

        public static GameObject Box(Transform parent, string name, Vector3 localCentre, Vector3 size, float yaw = 0f, Surface surface = Surface.Concrete)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.SetLocalPositionAndRotation(localCentre, Quaternion.Euler(0f, yaw, 0f));
            box.transform.localScale = size;
            box.GetComponent<MeshRenderer>().sharedMaterial = For(surface);
            MakeStatic(box);
            return box;
        }

        /// <summary>A box over a footprint on the ground plane (centre and size in the parent's x and z), from y0 to y1.</summary>
        public static GameObject BoxXZ(Transform parent, string name, Vector2 centre, Vector2 size, float y0, float y1, float yaw = 0f,
            Surface surface = Surface.Concrete) =>
            Box(parent, name, new Vector3(centre.x, (y0 + y1) / 2f, centre.y), new Vector3(size.x, y1 - y0, size.y), yaw, surface);

        /// <summary>A box as BoxXZ makes it, but only to see: no collider, for trims too small to matter to movement.</summary>
        public static GameObject Trim(Transform parent, string name, Vector2 centre, Vector2 size, float y0, float y1, float yaw, Surface surface)
        {
            GameObject trim = BoxXZ(parent, name, centre, size, y0, y1, yaw, surface);
            Object.DestroyImmediate(trim.GetComponent<BoxCollider>());
            MakeStatic(trim, occluder: false);
            return trim;
        }

        /// <summary>An axis-aligned box over x from x0 to x1 and z from z0 to z1 (in the parent's x and z), from y0 to y1.</summary>
        public static GameObject Block(Transform parent, string name, float x0, float x1, float z0, float z1, float y0, float y1,
            Surface surface = Surface.Concrete) =>
            BoxXZ(parent, name, new Vector2((x0 + x1) / 2f, (z0 + z1) / 2f), new Vector2(x1 - x0, z1 - z0), y0, y1, 0f, surface);

        /// <summary>
        /// A solid with the given convex footprint (in the parent's x and z), from y0 to y1: a mesh to see, and box
        /// colliders to stand on (see FootprintBoxes), on child objects named "Collider".
        /// </summary>
        public static GameObject Prism(Transform parent, string name, Vector2[] shape, float y0, float y1, Surface surface = Surface.Concrete)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            Vector3 centroid = Vector3.zero;
            foreach (Vector2 p in shape) { centroid += new Vector3(p.x, (y0 + y1) / 2f, p.y); }
            centroid /= shape.Length;
            // Each face has its own vertices, for flat shading; each triangle faces away from the solid's middle (Unity's
            // front face is the side Cross(b - a, c - a) points to).
            void Triangle(Vector3 a, Vector3 b, Vector3 c)
            {
                Vector3 normal = Vector3.Cross(b - a, c - a);
                if (Vector3.Dot(normal, (a + b + c) / 3f - centroid) < 0f) { (b, c) = (c, b); }
                int i = vertices.Count;
                vertices.Add(a); vertices.Add(b); vertices.Add(c);
                triangles.Add(i); triangles.Add(i + 1); triangles.Add(i + 2);
            }
            for (int i = 1; i + 1 < shape.Length; i++)
            {
                Triangle(new Vector3(shape[0].x, y1, shape[0].y), new Vector3(shape[i].x, y1, shape[i].y), new Vector3(shape[i + 1].x, y1, shape[i + 1].y));
                Triangle(new Vector3(shape[0].x, y0, shape[0].y), new Vector3(shape[i].x, y0, shape[i].y), new Vector3(shape[i + 1].x, y0, shape[i + 1].y));
            }
            for (int i = 0; i < shape.Length; i++)
            {
                Vector2 p = shape[i], q = shape[(i + 1) % shape.Length];
                Vector3 p0 = new(p.x, y0, p.y), q0 = new(q.x, y0, q.y), p1 = new(p.x, y1, p.y), q1 = new(q.x, y1, q.y);
                Triangle(p0, q0, q1);
                Triangle(p0, q1, p1);
            }

            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var solid = new GameObject(name);
            solid.transform.SetParent(parent, false);
            solid.AddComponent<MeshFilter>().sharedMesh = mesh;
            solid.AddComponent<MeshRenderer>().sharedMaterial = For(surface);
            MakeStatic(solid);
            foreach ((Vector2 centre, Vector2 size, float yaw) in FootprintBoxes(shape))
            {
                var collider = new GameObject("Collider");
                collider.transform.SetParent(solid.transform, false);
                collider.transform.SetLocalPositionAndRotation(new Vector3(centre.x, (y0 + y1) / 2f, centre.y), Quaternion.Euler(0f, yaw, 0f));
                collider.AddComponent<BoxCollider>().size = new Vector3(size.x, y1 - y0, size.y);
                MakeStatic(collider);
            }
            return solid;
        }

        /// <summary>
        /// Boxes (centre, size across and along, and yaw, as BoxXZ takes them) that together fill a convex footprint
        /// whose edges all run at multiples of 45° (the level's rectangles, octagons, and rectangles with 45° cuts),
        /// none sticking out. The footprint's cut into bands square to the axes, each giving the widest rectangle that
        /// fits it and, where a 45° edge bounds it, a right-angled triangle beside it. Each triangle's covered by a 45°
        /// rectangle standing on its long side if that fits in the footprint; if not, by a square in its corner and
        /// the two half-size triangles left along the edge, in turn (to within a centimetre or so, at worst). Bands
        /// of one width are merged, and rectangles that repeat or lie within another are left out.
        /// </summary>
        public static List<(Vector2 centre, Vector2 size, float yaw)> FootprintBoxes(Vector2[] shape)
        {
            const float eps = 1e-3f;
            int n = shape.Length;
            Vector2 centroid = Vector2.zero;
            foreach (Vector2 p in shape) { centroid += p; }
            centroid /= n;
            bool InShape(Vector2 q)
            {
                for (int i = 0; i < n; i++)
                {
                    Vector2 a = shape[i], b = shape[(i + 1) % n];
                    Vector2 normal = new Vector2((b - a).y, -(b - a).x).normalized;
                    if (Vector2.Dot(normal, centroid - a) > 0f) { normal = -normal; }
                    if (Vector2.Dot(normal, q - a) > eps) { return false; }
                }
                return true;
            }
            // The footprint's x range along a line of constant z (allowing for rounding in its corners' heights).
            (float min, float max) Span(float z)
            {
                float min = float.MaxValue, max = float.MinValue;
                void Include(float x) { min = Mathf.Min(min, x); max = Mathf.Max(max, x); }
                for (int i = 0; i < n; i++)
                {
                    Vector2 a = shape[i], b = shape[(i + 1) % n];
                    if (Mathf.Abs(a.y - b.y) < eps)
                    {
                        if (Mathf.Abs(z - a.y) < eps) { Include(a.x); Include(b.x); }
                        continue;
                    }
                    if (z < Mathf.Min(a.y, b.y) - eps || z > Mathf.Max(a.y, b.y) + eps) { continue; }
                    Include(Mathf.Lerp(a.x, b.x, Mathf.Clamp01((z - a.y) / (b.y - a.y))));
                }
                return (min, max);
            }

            var rects = new List<(Vector2[] corners, Vector2 centre, Vector2 size, float yaw)>();
            void Square(Vector2 p, Vector2 q)
            {
                Vector2 lo = Vector2.Min(p, q), hi = Vector2.Max(p, q);
                if (hi.x - lo.x < 0.005f || hi.y - lo.y < 0.005f) { return; }
                rects.Add((new[] { lo, new Vector2(hi.x, lo.y), hi, new Vector2(lo.x, hi.y) }, (lo + hi) / 2f, hi - lo, 0f));
            }
            // A right-angled triangle with its right angle at c, its long side from a to b along a 45° edge.
            void Triangle(Vector2 a, Vector2 b, Vector2 c, int depth)
            {
                float length = Vector2.Distance(a, b);
                if (length < 0.01f) { return; }
                Vector2 inward = ((c - (a + b) / 2f)).normalized;
                float d = length / 2f;
                Vector2 b2 = b + inward * d, a2 = a + inward * d;
                if (InShape(a2) && InShape(b2))
                {
                    rects.Add((new[] { a, b, b2, a2 }, (a + b) / 2f + inward * (d / 2f), new Vector2(length, d), Mathf.Atan2(inward.x, inward.y) * Mathf.Rad2Deg));
                    return;
                }
                if (depth >= 8) { return; }
                Vector2 m = (a + b) / 2f;
                Square(c, m);
                Triangle(a, m, (a + c) / 2f, depth + 1);
                Triangle(m, b, (b + c) / 2f, depth + 1);
            }

            var levels = new List<float>();
            foreach (Vector2 p in shape) { if (!levels.Exists(z => Mathf.Abs(z - p.y) < eps)) { levels.Add(p.y); } }
            levels.Sort();
            // Where both sides of a band lean the same way, it's a slanted parallelogram: split it into bands no
            // taller than it's wide, so each still has an upright rectangle inside it.
            var cuts = new List<float> { levels[0] };
            for (int i = 0; i + 1 < levels.Count; i++)
            {
                float z0 = levels[i], z1 = levels[i + 1];
                for (int k = 1; k <= 32; k++)
                {
                    bool fits = true;
                    for (int j = 0; j < k && fits; j++)
                    {
                        float za = Mathf.Lerp(z0, z1, (float)j / k), zb = Mathf.Lerp(z0, z1, (float)(j + 1) / k);
                        (float la, float ha) = Span(za);
                        (float lb, float hb) = Span(zb);
                        fits = Mathf.Min(ha, hb) - Mathf.Max(la, lb) >= -eps;
                    }
                    if (!fits) { continue; }
                    for (int j = 1; j <= k; j++) { cuts.Add(Mathf.Lerp(z0, z1, (float)j / k)); }
                    break;
                }
            }
            var bands = new List<(float z0, float z1, float x0, float x1)>();
            for (int i = 0; i + 1 < cuts.Count; i++)
            {
                float z0 = cuts[i], z1 = cuts[i + 1];
                if (z1 - z0 < 0.005f) { continue; }
                (float lo0, float hi0) = Span(z0);
                (float lo1, float hi1) = Span(z1);
                // The footprint's convex, so its narrowest along the band is at one of the band's edges.
                float x0 = Mathf.Max(lo0, lo1), x1 = Mathf.Min(hi0, hi1);
                // (Where the band comes to a point, it has no rectangle, only its triangles.)
                if (x1 - x0 < 0.005f) { }
                else if (bands.Count > 0 && Mathf.Abs(bands[^1].z1 - z0) < eps && Mathf.Abs(bands[^1].x0 - x0) < eps && Mathf.Abs(bands[^1].x1 - x1) < eps)
                {
                    bands[^1] = (bands[^1].z0, z1, x0, x1);
                }
                else { bands.Add((z0, z1, x0, x1)); }
                // The triangles either side, where a 45° edge bounds the band.
                if (Mathf.Abs(lo0 - lo1) > 0.005f)
                {
                    if (lo0 < lo1) { Triangle(new Vector2(lo0, z0), new Vector2(lo1, z1), new Vector2(lo1, z0), 0); }
                    else { Triangle(new Vector2(lo0, z0), new Vector2(lo1, z1), new Vector2(lo0, z1), 0); }
                }
                if (Mathf.Abs(hi0 - hi1) > 0.005f)
                {
                    if (hi0 > hi1) { Triangle(new Vector2(hi0, z0), new Vector2(hi1, z1), new Vector2(hi1, z0), 0); }
                    else { Triangle(new Vector2(hi0, z0), new Vector2(hi1, z1), new Vector2(hi0, z1), 0); }
                }
            }
            foreach ((float z0, float z1, float x0, float x1) in bands) { Square(new Vector2(x0, z0), new Vector2(x1, z1)); }

            bool Within(Vector2[] inner, Vector2[] outer)
            {
                Vector2 m = (outer[0] + outer[2]) / 2f;
                foreach (Vector2 p in inner)
                {
                    for (int k = 0; k < 4; k++)
                    {
                        Vector2 u = outer[k], v = outer[(k + 1) % 4];
                        Vector2 normal = new Vector2((v - u).y, -(v - u).x).normalized;
                        if (Vector2.Dot(normal, m - u) > 0f) { normal = -normal; }
                        if (Vector2.Dot(normal, p - u) > eps) { return false; }
                    }
                }
                return true;
            }
            var kept = new List<(Vector2 centre, Vector2 size, float yaw)>();
            for (int i = 0; i < rects.Count; i++)
            {
                bool redundant = false;
                for (int j = 0; j < rects.Count && !redundant; j++)
                {
                    if (i == j || !Within(rects[i].corners, rects[j].corners)) { continue; }
                    // Within another: drop it, unless they're the same rectangle and this is the first of them.
                    redundant = !Within(rects[j].corners, rects[i].corners) || j < i;
                }
                if (!redundant) { kept.Add((rects[i].centre, rects[i].size, rects[i].yaw)); }
            }
            return kept;
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
