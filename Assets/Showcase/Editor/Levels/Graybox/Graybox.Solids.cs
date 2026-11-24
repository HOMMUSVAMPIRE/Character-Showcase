using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MeridianMotion.Showcase.Levels
{
    // Prisms: convex solids, filled with box colliders that cover their footprints exactly.
    public static partial class Graybox
    {
        /// <summary>
        /// A solid with the given convex footprint (in the parent's x and z), from y0 to y1: a mesh to see, and box
        /// colliders to stand on (see FootprintBoxes), on child objects named "Collider".
        /// </summary>
        [Piece]
        public static GameObject Prism(Transform parent, string name, Vector2[] shape, float y0, float y1, GrayboxMaterial surface = GrayboxMaterial.Concrete)
        {
            using LevelSpecRecorder.Scope recording = LevelSpecRecorder.Call(nameof(Prism), parent, name, shape, y0, y1, surface);
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
            // A UV2 chart for each triangle (each has its own vertices).
            var faces = new List<List<int>>(vertices.Count / 3);
            for (int i = 0; i + 2 < vertices.Count; i += 3) { faces.Add(new List<int> { i, i + 1, i + 2 }); }
            mesh.SetUVs(1, GrayboxUV2.Pack(vertices, faces));
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var solid = new GameObject(name);
            solid.transform.SetParent(parent, false);
            solid.AddComponent<MeshFilter>().sharedMesh = mesh;
            Surface(solid, solid.AddComponent<MeshRenderer>(), surface);
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
    }
}
