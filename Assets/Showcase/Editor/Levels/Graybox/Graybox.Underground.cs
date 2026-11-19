using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MeridianMotion.Showcase.Levels
{
    // The ground and slabs with openings cut through them, underground cells (rooms and passages), and shaft walls.
    public static partial class Graybox
    {
        /// <summary>
        /// A level slab over an axis-aligned area (a Rect's x is x, its y is z), from y0 to y1, with axis-aligned holes
        /// cut through it: built as the fewest boxes the holes leave, band by band.
        /// </summary>
        [Piece]
        public static void SlabWithHoles(Transform parent, string name, UnityEngine.Rect area, IEnumerable<UnityEngine.Rect> holes,
            float y0, float y1, GrayboxMaterial surface = GrayboxMaterial.Concrete)
        {
            using LevelSpecRecorder.Scope recording = LevelSpecRecorder.Call(nameof(SlabWithHoles), parent, name, area, holes, y0, y1, surface);
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
        [Piece]
        public static void Ground(Transform parent, float half, IEnumerable<UnityEngine.Rect> openings)
        {
            using LevelSpecRecorder.Scope recording = LevelSpecRecorder.Call(nameof(Ground), parent, half, openings);
            SlabWithHoles(Group(parent, "Ground (placeholder)"), "Ground", UnityEngine.Rect.MinMaxRect(-half, -half, half, half), openings, -1f, 0f, GrayboxMaterial.Ground);
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

            /// <summary>How high the cell's walls rise: to its ceiling, or to openTop if it's open above.</summary>
            public float Top(float openTop) => Open ? openTop : Ceiling;
        }

        /// <summary>
        /// An enclosed network of rooms and passages from cells that tile without overlapping: a floor under each, a
        /// ceiling over each closed one, and walls round the outside of the whole, open wherever two cells meet or a
        /// cell meets a portal (a doorway into something built separately). Open cells' walls rise to openTop. Where a
        /// taller cell meets a lower one, a header closes the wall above the opening.
        /// </summary>
        [Piece]
        public static void Cells(Transform parent, IList<Cell> cells, IList<UnityEngine.Rect> portals, float openTop, float thickness = 0.5f)
        {
            using LevelSpecRecorder.Scope recording = LevelSpecRecorder.Call(nameof(Cells), parent, cells, portals, openTop, thickness);
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
                                cell.Floor, cell.Floor + skirting.x, yaw, GrayboxMaterial.Graphite);
                        }
                        // Close the outside corners: extend past a corner of the cell unless that's inside the network.
                        if (extendStart && !Inside(p + dir * (s - thickness / 2f) + n * (thickness / 2f))) { s -= thickness; }
                        if (extendEnd && !Inside(p + dir * (e + thickness / 2f) + n * (thickness / 2f))) { e += thickness; }
                        if (e - s < eps || pieceTop - bottom < eps) { return; }
                        BoxXZ(group, name, p + dir * ((s + e) / 2f) + offset, new Vector2(thickness, e - s), bottom, pieceTop, yaw, GrayboxMaterial.Panel);
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
        [Piece]
        public static void ShaftWalls(Transform parent, string name, float x0, float z0, float x1, float z1, float y0, float y1, float thickness = 0.5f)
        {
            using LevelSpecRecorder.Scope recording = LevelSpecRecorder.Call(nameof(ShaftWalls), parent, name, x0, z0, x1, z1, y0, y1, thickness);
            Transform shaft = Group(parent, name);
            float t = thickness;
            Block(shaft, "Wall (North)", x0 - t, x1 + t, z1, z1 + t, y0, y1, GrayboxMaterial.Panel);
            Block(shaft, "Wall (South)", x0 - t, x1 + t, z0 - t, z0, y0, y1, GrayboxMaterial.Panel);
            Block(shaft, "Wall (East)", x1, x1 + t, z0, z1, y0, y1, GrayboxMaterial.Panel);
            Block(shaft, "Wall (West)", x0 - t, x0, z0, z1, y0, y1, GrayboxMaterial.Panel);
        }
    }
}
