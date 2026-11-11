using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CharacterShowcase.EditorTools.Level
{
    /// <summary>
    /// Graybox building blocks shared by the level builders: boxes and solids in one material (see Begin), stairs, ramps,
    /// bridges, guardrails and connection markers, laid out on the ground plane (a Vector2's x is x east, its y is z
    /// north) under rotated or offset groups. A builder calls Begin, sets the stair profile and other dimensions it
    /// wants, then builds.
    /// </summary>
    public static class Graybox
    {
        /// <summary>Each stair step at most this high (keep it under the character's step height) and this deep.</summary>
        public static float StepRise = 0.3f, StepRun = 0.4f;
        /// <summary>How far bridges run out, how thick decks are, and guardrails' height and thickness.</summary>
        public static float BridgeLength = 20f, Deck = 1f, RailHeight = 1.1f, RailThickness = 0.15f;
        /// <summary>The material everything's given: the one passed to Begin, or the render pipeline's default.</summary>
        public static Material Material;

        /// <summary>
        /// Gets ready to build: everything's given material (or, if that's null, the render pipeline's default), and
        /// the dimensions are reset to their defaults.
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
            StepRise = 0.3f; StepRun = 0.4f; BridgeLength = 20f; Deck = 1f; RailHeight = 1.1f; RailThickness = 0.15f;
        }

        // ---- Pieces ----

        /// <summary>
        /// A straight flight of solid steps from base (its bottom edge's middle) along direction, rising from y0 to y1,
        /// each at most StepRise high and StepRun deep, and solid down to floor (the ground, unless it's below ground).
        /// Returns its length along the direction.
        /// </summary>
        public static float Flight(Transform parent, string name, Vector2 basePoint, Vector2 direction, float width, float y0, float y1,
            float floor = 0f)
        {
            Transform flight = Group(parent, name);
            int steps = Mathf.CeilToInt((y1 - y0) / StepRise - 1e-4f);
            float rise = (y1 - y0) / steps;
            float yaw = Mathf.Atan2(direction.x, direction.y) * Mathf.Rad2Deg;
            for (int i = 0; i < steps; i++)
            {
                Vector2 centre = basePoint + direction * ((i + 0.5f) * StepRun);
                // Solid to the ground, so a flight over open ground stands on something.
                BoxXZ(flight, $"Step {i + 1}", centre, new Vector2(width, StepRun), Mathf.Min(floor, y0), y0 + rise * (i + 1), yaw);
            }
            return steps * StepRun;
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
            for (float d = 6f; d < length - 2f; d += 8f)
            {
                float y = Mathf.Lerp(y0, y1, d / length) - Deck;
                foreach (float side in new[] { -1f, 1f })
                {
                    Vector2 at = start + direction * d + new Vector2(direction.y, -direction.x) * (side * (width / 2f - 1f));
                    if (y > 0.5f) { BoxXZ(ramp, "Support", at, new Vector2(1f, 1f), 0f, y, yaw); }
                }
            }
        }

        /// <summary>
        /// A bridge out from start along direction at the given height, with guardrails along both sides, columns
        /// down to the ground, and a marker at its far end where the next area joins.
        /// </summary>
        public static void Bridge(Transform bridges, Transform rails, Transform connections, string name, Vector2 start, Vector2 direction,
            float width, float height)
        {
            Transform bridge = Group(bridges, name.Replace("To ", "Bridge to ").Replace("Future Connection", "Bridge"));
            Vector2 end = Walkway(bridge, rails, start, direction, BridgeLength, width, height);
            Marker(connections, name, end, height);
        }

        /// <summary>
        /// A deck from start along direction for length at the given height, with guardrails along both sides, and
        /// columns down to the ground unless it's suspended (hung between the things at its ends). Returns its far end.
        /// </summary>
        public static Vector2 Walkway(Transform parent, Transform rails, Vector2 start, Vector2 direction, float length, float width,
            float height, bool suspended = false)
        {
            float yaw = Mathf.Atan2(direction.x, direction.y) * Mathf.Rad2Deg;
            Vector2 end = start + direction * length, side = new Vector2(direction.y, -direction.x) * (width / 2f);
            BoxXZ(parent, "Deck", start + direction * (length / 2f), new Vector2(width, length), height - Deck, height, yaw);
            if (!suspended)
            {
                for (float d = 5f; d < length; d += 10f)
                {
                    foreach (float s in new[] { -1f, 1f })
                    {
                        BoxXZ(parent, "Column", start + direction * d + side * (s * 0.8f), new Vector2(1f, 1f), 0f, height - Deck, yaw);
                    }
                }
            }
            Rail(rails, start + side, end + side, height);
            Rail(rails, start - side, end - side, height);
            return end;
        }

        /// <summary>
        /// A wall along the line from a to b (centred on it), from y0 to y1, with openings (centre on the line, width,
        /// and the opening's bottom and top: the wall's filled in below and above each).
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
                BoxXZ(parent, piece, a + dir * ((s + e) / 2f), new Vector2(thickness, e - s), bottom, top, yaw);
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
        }

        /// <summary>Guardrails along every edge of a shape at the given height, leaving gaps (centre, width) where routes go through.</summary>
        public static void RailAround(Transform rails, Vector2[] shape, float height, params (Vector2 centre, float width)[] gaps)
        {
            for (int i = 0; i < shape.Length; i++) { Rail(rails, shape[i], shape[(i + 1) % shape.Length], height, gaps); }
        }

        /// <summary>A guardrail from a to b on a surface at the given height, leaving gaps (centre, width) that lie along it.</summary>
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
                BoxXZ(rails, "Guardrail", a + dir * ((s + e) / 2f), new Vector2(RailThickness, e - s), height, height + RailHeight, yaw);
            }
            foreach ((float cutFrom, float cutTo) in cuts)
            {
                Piece(from, Mathf.Clamp(cutFrom, 0f, length));
                from = Mathf.Max(from, Mathf.Clamp(cutTo, 0f, length));
            }
            Piece(from, length);
        }

        /// <summary>An empty marker where a route leaves for another area: where that area's geometry joins this one.</summary>
        public static void Marker(Transform parent, string name, Vector2 at, float height)
        {
            var marker = new GameObject(name);
            marker.transform.SetParent(parent, false);
            marker.transform.localPosition = new Vector3(at.x, height, at.y);
        }

        /// <summary>
        /// A level slab over an axis-aligned area (a Rect's x is x, its y is z), from y0 to y1, with axis-aligned holes
        /// cut through it: built as the fewest boxes the holes leave, band by band.
        /// </summary>
        public static void SlabWithHoles(Transform parent, string name, UnityEngine.Rect area, IEnumerable<UnityEngine.Rect> holes,
            float y0, float y1)
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
                    BoxXZ(parent, cuts.Count == 0 ? name : $"{name} {++n}", new Vector2((x0 + x1) / 2f, (z0 + z1) / 2f), new Vector2(x1 - x0, z1 - z0), y0, y1);
                }
            }
        }

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
                        // Close the outside corners: extend past a corner of the cell unless that's inside the network.
                        if (extendStart && !Inside(p + dir * (s - thickness / 2f) + n * (thickness / 2f))) { s -= thickness; }
                        if (extendEnd && !Inside(p + dir * (e + thickness / 2f) + n * (thickness / 2f))) { e += thickness; }
                        if (e - s < eps || pieceTop - bottom < eps) { return; }
                        BoxXZ(group, name, p + dir * ((s + e) / 2f) + offset, new Vector2(thickness, e - s), bottom, pieceTop, yaw);
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

        public static GameObject Box(Transform parent, string name, Vector3 localCentre, Vector3 size, float yaw = 0f)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.SetLocalPositionAndRotation(localCentre, Quaternion.Euler(0f, yaw, 0f));
            box.transform.localScale = size;
            box.GetComponent<MeshRenderer>().sharedMaterial = Material;
            MakeStatic(box);
            return box;
        }

        /// <summary>A box over a footprint on the ground plane (centre and size in the parent's x and z), from y0 to y1.</summary>
        public static GameObject BoxXZ(Transform parent, string name, Vector2 centre, Vector2 size, float y0, float y1, float yaw = 0f) =>
            Box(parent, name, new Vector3(centre.x, (y0 + y1) / 2f, centre.y), new Vector3(size.x, y1 - y0, size.y), yaw);

        /// <summary>
        /// A solid with the given convex footprint (in the parent's x and z), from y0 to y1: a mesh to see, and box
        /// colliders to stand on (see FootprintBoxes), on child objects named "Collider".
        /// </summary>
        public static GameObject Prism(Transform parent, string name, Vector2[] shape, float y0, float y1)
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
            solid.AddComponent<MeshRenderer>().sharedMaterial = Material;
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
        /// exactly: on each edge, the deepest rectangle standing on it that stays inside the footprint. Each lies
        /// inside the footprint, so none sticks out; for the level's shapes (rectangles, octagons, and rectangles with
        /// 45° cuts) together they cover it. Rectangles that repeat or lie within another are left out.
        /// </summary>
        public static List<(Vector2 centre, Vector2 size, float yaw)> FootprintBoxes(Vector2[] shape)
        {
            const float eps = 1e-3f;
            int n = shape.Length;
            Vector2 centroid = Vector2.zero;
            foreach (Vector2 p in shape) { centroid += p; }
            centroid /= n;
            // Each edge's outward normal and offset: inside is where Dot(normal, p) <= offset.
            var normals = new Vector2[n];
            var offsets = new float[n];
            for (int i = 0; i < n; i++)
            {
                Vector2 a = shape[i], b = shape[(i + 1) % n], d = b - a;
                Vector2 normal = new Vector2(d.y, -d.x).normalized;
                if (Vector2.Dot(normal, centroid - a) > 0f) { normal = -normal; }
                normals[i] = normal;
                offsets[i] = Vector2.Dot(normal, a);
            }
            // How far from p (on the boundary) along an inward direction until leaving the footprint.
            float Exit(Vector2 p, Vector2 direction)
            {
                float best = float.MaxValue;
                for (int j = 0; j < n; j++)
                {
                    float rate = Vector2.Dot(normals[j], direction);
                    if (rate <= 1e-5f) { continue; }
                    best = Mathf.Min(best, Mathf.Max(0f, (offsets[j] - Vector2.Dot(normals[j], p)) / rate));
                }
                return best;
            }

            var rects = new List<(Vector2[] corners, Vector2 centre, Vector2 size, float yaw)>();
            for (int i = 0; i < n; i++)
            {
                Vector2 a = shape[i], b = shape[(i + 1) % n];
                float length = Vector2.Distance(a, b);
                if (length < 0.05f) { continue; }
                Vector2 inward = -normals[i];
                float depth = Mathf.Min(Exit(a, inward), Exit(b, inward));
                if (depth < 0.05f || depth == float.MaxValue) { continue; }
                Vector2[] corners = { a, b, b + inward * depth, a + inward * depth };
                rects.Add((corners, (a + b) / 2f + inward * (depth / 2f), new Vector2(length, depth), Mathf.Atan2(inward.x, inward.y) * Mathf.Rad2Deg));
            }

            bool Within(Vector2[] inner, Vector2[] outer)
            {
                foreach (Vector2 p in inner)
                {
                    for (int k = 0; k < 4; k++)
                    {
                        Vector2 u = outer[k], v = outer[(k + 1) % 4], m = (outer[0] + outer[2]) / 2f;
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

        /// <summary>Level geometry doesn't move: static for batching, lighting and occlusion.</summary>
        public static void MakeStatic(GameObject obj) =>
            GameObjectUtility.SetStaticEditorFlags(obj, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI
                | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ReflectionProbeStatic);
    }
}
