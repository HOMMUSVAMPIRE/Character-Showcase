using System.Collections.Generic;
using UnityEngine;

namespace CharacterShowcase.EditorTools.Level
{
    /// <summary>
    /// A mesh being put together from flat faces for one object, each face in one of the palette's surfaces (a submesh
    /// per surface, so the object takes one material per surface it uses). Each face has its own vertices, for flat
    /// shading, and UVs in metres projected along the axis it faces. Used for the pieces that are more than a box: a
    /// flight of steps, a guardrail.
    /// </summary>
    internal sealed class GrayboxMesh
    {
        private readonly List<Vector3> vertices = new();
        private readonly List<Vector2> uvs = new();
        private readonly List<Graybox.Surface> surfaces = new();
        private readonly List<List<int>> triangles = new();

        /// <summary>Adds a face's corners as vertices and returns the first one's index (for Triangle).</summary>
        public int Face(Vector3 normal, IList<Vector3> corners)
        {
            int first = vertices.Count;
            foreach (Vector3 v in corners)
            {
                vertices.Add(v);
                uvs.Add(Mathf.Abs(normal.y) > 0.5f ? new Vector2(v.x, v.z) : Mathf.Abs(normal.z) > 0.5f ? new Vector2(v.x, v.y) : new Vector2(v.z, v.y));
            }
            return first;
        }

        /// <summary>
        /// A triangle of vertices a, b and c (from Face), in a surface, turned to face along normal (Unity's front face is
        /// the side Cross(b - a, c - a) points to).
        /// </summary>
        public void Triangle(Graybox.Surface surface, int a, int b, int c, Vector3 normal)
        {
            if (Vector3.Dot(Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]), normal) < 0f) { (b, c) = (c, b); }
            List<int> list = Submesh(surface);
            list.Add(a); list.Add(b); list.Add(c);
        }

        /// <summary>A flat four-cornered face, corners in order round it.</summary>
        public void Quad(Graybox.Surface surface, Vector3 normal, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            int i = Face(normal, new[] { a, b, c, d });
            Triangle(surface, i, i + 1, i + 2, normal);
            Triangle(surface, i, i + 2, i + 3, normal);
        }

        /// <summary>
        /// An axis-aligned box from min to max, leaving out the faces that can't be seen: its top or bottom (where it
        /// meets something above or stands on something), or its sides across z (where it runs into something at both
        /// ends).
        /// </summary>
        public void Box(Graybox.Surface surface, Vector3 min, Vector3 max, bool top = true, bool bottom = true, bool ends = true,
            bool sides = true)
        {
            Vector3 a = min, b = max;
            if (sides)
            {
                Quad(surface, Vector3.left, new(a.x, a.y, a.z), new(a.x, b.y, a.z), new(a.x, b.y, b.z), new(a.x, a.y, b.z));
                Quad(surface, Vector3.right, new(b.x, a.y, a.z), new(b.x, a.y, b.z), new(b.x, b.y, b.z), new(b.x, b.y, a.z));
            }
            if (top) { Quad(surface, Vector3.up, new(a.x, b.y, a.z), new(b.x, b.y, a.z), new(b.x, b.y, b.z), new(a.x, b.y, b.z)); }
            if (bottom) { Quad(surface, Vector3.down, new(a.x, a.y, a.z), new(a.x, a.y, b.z), new(b.x, a.y, b.z), new(b.x, a.y, a.z)); }
            if (ends)
            {
                Quad(surface, Vector3.back, new(a.x, a.y, a.z), new(b.x, a.y, a.z), new(b.x, b.y, a.z), new(a.x, b.y, a.z));
                Quad(surface, Vector3.forward, new(a.x, a.y, b.z), new(a.x, b.y, b.z), new(b.x, b.y, b.z), new(b.x, a.y, b.z));
            }
        }

        /// <summary>
        /// A box round the line from a to b (a beam at any angle): width across along side (made square to the line),
        /// and depth across the third way. Its ends are left out unless asked for (a beam usually ends in something).
        /// </summary>
        public void Beam(Graybox.Surface surface, Vector3 a, Vector3 b, Vector3 side, float width, float depth, bool ends = false)
        {
            Vector3 along = (b - a).normalized;
            Vector3 w = Vector3.ProjectOnPlane(side, along).normalized * (width / 2f);
            Vector3 d = Vector3.Cross(along, w).normalized * (depth / 2f);
            void Side(Vector3 normal, Vector3 u, Vector3 v) => Quad(surface, normal, a + u - v, b + u - v, b + u + v, a + u + v);
            Side(w.normalized, w, d);
            Side(-w.normalized, -w, d);
            Side(d.normalized, d, w);
            Side(-d.normalized, -d, w);
            if (ends)
            {
                Quad(surface, -along, a - w - d, a + w - d, a + w + d, a - w + d);
                Quad(surface, along, b - w - d, b + w - d, b + w + d, b - w + d);
            }
        }

        /// <summary>A round bar from a to b (a pipe, a conduit), radius thick, with sides faces round it; its ends closed if asked for.</summary>
        public void Tube(Graybox.Surface surface, Vector3 a, Vector3 b, float radius, int sides, bool ends = false)
        {
            Vector3 along = (b - a).normalized;
            Vector3 u = Vector3.Cross(along, Mathf.Abs(along.y) < 0.9f ? Vector3.up : Vector3.right).normalized, v = Vector3.Cross(along, u);
            Vector3 Ring(int i) => (u * Mathf.Cos(i * 2f * Mathf.PI / sides) + v * Mathf.Sin(i * 2f * Mathf.PI / sides)) * radius;
            for (int i = 0; i < sides; i++)
            {
                Vector3 p = Ring(i), q = Ring(i + 1);
                Quad(surface, (p + q).normalized, a + p, b + p, b + q, a + q);
            }
            if (!ends) { return; }
            foreach ((Vector3 centre, Vector3 normal) in new[] { (a, -along), (b, along) })
            {
                var corners = new Vector3[sides];
                for (int i = 0; i < sides; i++) { corners[i] = centre + Ring(i); }
                int first = Face(normal, corners);
                for (int i = 1; i + 1 < sides; i++) { Triangle(surface, first, first + i, first + i + 1, normal); }
            }
        }

        /// <summary>
        /// A tapering solid round a vertical axis at centre, with sides faces: radius r0 at y0 to r1 at y1, the rings
        /// turned turn degrees (for trees' faceted canopies); its top and bottom closed if asked for.
        /// </summary>
        public void Frustum(Graybox.Surface surface, Vector3 centre, float r0, float r1, float y0, float y1, int sides, float turn = 0f,
            bool top = true, bool bottom = false)
        {
            Vector3 Ring(int i, float r, float y)
            {
                float angle = (i * 360f / sides + turn) * Mathf.Deg2Rad;
                return centre + new Vector3(Mathf.Cos(angle) * r, y, Mathf.Sin(angle) * r);
            }
            for (int i = 0; i < sides; i++)
            {
                Vector3 a = Ring(i, r0, y0), b = Ring(i + 1, r0, y0), c = Ring(i + 1, r1, y1), d = Ring(i, r1, y1);
                Vector3 normal = Vector3.Cross(c - a, b - d);
                if (Vector3.Dot(normal, (a + b) / 2f - (centre + Vector3.up * y0)) < 0f) { normal = -normal; }
                Quad(surface, normal.normalized, a, b, c, d);
            }
            void Cap(float r, float y, Vector3 normal)
            {
                if (r < 0.001f) { return; }
                var corners = new Vector3[sides];
                for (int i = 0; i < sides; i++) { corners[i] = Ring(i, r, y); }
                int first = Face(normal, corners);
                for (int i = 1; i + 1 < sides; i++) { Triangle(surface, first, first + i, first + i + 1, normal); }
            }
            if (top) { Cap(r1, y1, Vector3.up); }
            if (bottom) { Cap(r0, y0, Vector3.down); }
        }

        /// <summary>
        /// A flat plate with a convex outline (x across, y up) from z0 to z1: its two faces and its edges. Plates of
        /// several convex pieces make other outlines (a banner's swallowtail).
        /// </summary>
        public void Plate(Graybox.Surface surface, IList<Vector2> outline, float z0, float z1)
        {
            Vector2 centroid = Vector2.zero;
            foreach (Vector2 p in outline) { centroid += p; }
            centroid /= outline.Count;
            foreach ((float z, Vector3 normal) in new[] { (z0, Vector3.back), (z1, Vector3.forward) })
            {
                var corners = new Vector3[outline.Count];
                for (int i = 0; i < outline.Count; i++) { corners[i] = new Vector3(outline[i].x, outline[i].y, z); }
                int first = Face(normal, corners);
                for (int i = 1; i + 1 < outline.Count; i++) { Triangle(surface, first, first + i, first + i + 1, normal); }
            }
            for (int i = 0; i < outline.Count; i++)
            {
                Vector2 p = outline[i], q = outline[(i + 1) % outline.Count];
                Vector2 outward = new Vector2(q.y - p.y, p.x - q.x).normalized;
                if (Vector2.Dot(outward, (p + q) / 2f - centroid) < 0f) { outward = -outward; }
                Quad(surface, new Vector3(outward.x, outward.y, 0f), new(p.x, p.y, z0), new(q.x, q.y, z0), new(q.x, q.y, z1), new(p.x, p.y, z1));
            }
        }

        /// <summary>
        /// A solid with a convex footprint (x and z) from y0 to y1: its sides, and its top and bottom if asked for
        /// (leave out the bottom of anything standing on the ground, the top of anything under a deck).
        /// </summary>
        public void Prism(Graybox.Surface surface, IList<Vector2> shape, float y0, float y1, bool top = true, bool bottom = false)
        {
            Vector2 centroid = Vector2.zero;
            foreach (Vector2 p in shape) { centroid += p; }
            centroid /= shape.Count;
            for (int i = 0; i < shape.Count; i++)
            {
                Vector2 p = shape[i], q = shape[(i + 1) % shape.Count];
                Vector2 outward = new Vector2(q.y - p.y, p.x - q.x).normalized;
                if (Vector2.Dot(outward, (p + q) / 2f - centroid) < 0f) { outward = -outward; }
                Quad(surface, new Vector3(outward.x, 0f, outward.y), new(p.x, y0, p.y), new(q.x, y0, q.y), new(q.x, y1, q.y), new(p.x, y1, p.y));
            }
            void Cap(float y, Vector3 normal)
            {
                var corners = new Vector3[shape.Count];
                for (int i = 0; i < shape.Count; i++) { corners[i] = new Vector3(shape[i].x, y, shape[i].y); }
                int first = Face(normal, corners);
                for (int i = 1; i + 1 < shape.Count; i++) { Triangle(surface, first, first + i, first + i + 1, normal); }
            }
            if (top) { Cap(y1, Vector3.up); }
            if (bottom) { Cap(y0, Vector3.down); }
        }

        /// <summary>Gives obj the mesh (named name), and a renderer with the material of each surface it uses.</summary>
        public void AddTo(GameObject obj, string name)
        {
            var mesh = new Mesh { name = name, subMeshCount = surfaces.Count };
            if (vertices.Count > 65535) { mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32; }
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            var materials = new Material[surfaces.Count];
            for (int i = 0; i < surfaces.Count; i++)
            {
                mesh.SetTriangles(triangles[i], i);
                materials[i] = Graybox.For(surfaces[i]);
            }
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            obj.AddComponent<MeshFilter>().sharedMesh = mesh;
            obj.AddComponent<MeshRenderer>().sharedMaterials = materials;
        }

        private List<int> Submesh(Graybox.Surface surface)
        {
            int i = surfaces.IndexOf(surface);
            if (i < 0)
            {
                surfaces.Add(surface);
                triangles.Add(new List<int>());
                i = surfaces.Count - 1;
            }
            return triangles[i];
        }
    }
}
