using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CharacterShowcase.EditorTools.Level
{
    /// <summary>
    /// A second UV set (UV2, Unity's UV channel 1) for the level's meshes, in which every face has its own space: what
    /// textures painted onto a particular face are drawn in (the climbable areas painted onto a surface, say). The first
    /// UV set doesn't do: it's in metres, projected along the axis each face faces, so faces facing the same way share it,
    /// and Unity's cube puts all six faces on the same square. Each face is laid flat as a chart of its own, the charts at
    /// one scale (so detail is as fine on every face, for the mesh's size), packed in rows into the unit square with a
    /// gap round each. Fast enough for the whole level, and the same every build; it adds no vertices.
    /// </summary>
    internal static class GrayboxUV2
    {
        /// <summary>Where the level's boxes' mesh is kept: Unity's cube, with a UV2.</summary>
        public const string CubeMeshPath = "Assets/Models/Level/Graybox Cube.asset";

        // The gap round each chart, as a share of the packed square's side: about 4 texels of a 512 texture.
        private const float Padding = 0.008f;

        private static Mesh cube;

        /// <summary>
        /// The mesh the level's boxes use: Unity's cube (the same vertices, triangles and UVs), with a UV2. Made the first
        /// time it's needed, and kept as an asset.
        /// </summary>
        public static Mesh Cube
        {
            get
            {
                if (cube != null) { return cube; }
                cube = AssetDatabase.LoadAssetAtPath<Mesh>(CubeMeshPath);
                if (cube != null) { return cube; }
                GameObject probe = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Mesh builtIn = probe.GetComponent<MeshFilter>().sharedMesh;
                Object.DestroyImmediate(probe);
                cube = Object.Instantiate(builtIn);
                cube.name = "Graybox Cube";
                SetFaceCharts(cube);
                string folder = System.IO.Path.GetDirectoryName(CubeMeshPath)?.Replace('\\', '/');
                if (!string.IsNullOrEmpty(folder) && !AssetDatabase.IsValidFolder(folder))
                {
                    AssetDatabase.CreateFolder(System.IO.Path.GetDirectoryName(folder)?.Replace('\\', '/'), System.IO.Path.GetFileName(folder));
                }
                AssetDatabase.CreateAsset(cube, CubeMeshPath);
                return cube;
            }
        }

        /// <summary>
        /// Gives a mesh a UV2 with a chart for each face: each set of triangles sharing their vertices with no others (the
        /// level's meshes give each face its own vertices, for flat shading).
        /// </summary>
        public static void SetFaceCharts(Mesh mesh)
        {
            var vertices = new List<Vector3>();
            mesh.GetVertices(vertices);
            var faces = new List<List<int>>();
            // Triangles that share a vertex are one face (union-find over the vertices each triangle joins).
            var parent = new int[vertices.Count];
            for (int i = 0; i < parent.Length; i++) { parent[i] = i; }
            int Find(int x) { while (parent[x] != x) { parent[x] = parent[parent[x]]; x = parent[x]; } return x; }
            var triangles = new List<int>();
            for (int sub = 0; sub < mesh.subMeshCount; sub++)
            {
                mesh.GetTriangles(triangles, sub);
                for (int t = 0; t + 2 < triangles.Count; t += 3)
                {
                    int a = Find(triangles[t]), b = Find(triangles[t + 1]), c = Find(triangles[t + 2]);
                    parent[b] = a;
                    parent[Find(c)] = a;
                }
            }
            var faceIndex = new Dictionary<int, int>();
            for (int i = 0; i < vertices.Count; i++)
            {
                int root = Find(i);
                if (!faceIndex.TryGetValue(root, out int face)) { faceIndex[root] = face = faces.Count; faces.Add(new List<int>()); }
                faces[face].Add(i);
            }
            mesh.SetUVs(1, Pack(vertices, faces));
        }

        /// <summary>
        /// UV2 for vertices grouped into faces (each a list of vertex indices, all on one plane): each face projected onto
        /// its plane and packed.
        /// </summary>
        public static List<Vector2> Pack(IList<Vector3> vertices, IList<List<int>> faces)
        {
            var uv2 = new List<Vector2>(new Vector2[vertices.Count]);
            int count = faces.Count;
            if (count == 0) { return uv2; }

            // Each face flat on its plane, its corner at the origin: the chart's points and size.
            var sizes = new Vector2[count];
            for (int f = 0; f < count; f++)
            {
                List<int> face = faces[f];
                Vector3 normal = FaceNormal(vertices, face);
                Vector3 u = Vector3.Cross(normal, Mathf.Abs(normal.y) < 0.9f ? Vector3.up : Vector3.forward).normalized;
                Vector3 v = Vector3.Cross(normal, u);
                Vector2 min = new(float.PositiveInfinity, float.PositiveInfinity), max = new(float.NegativeInfinity, float.NegativeInfinity);
                foreach (int i in face)
                {
                    var p = new Vector2(Vector3.Dot(vertices[i], u), Vector3.Dot(vertices[i], v));
                    uv2[i] = p;
                    min = Vector2.Min(min, p);
                    max = Vector2.Max(max, p);
                }
                foreach (int i in face) { uv2[i] -= min; }
                sizes[f] = Vector2.Max(max - min, Vector2.one * 1e-4f);
            }

            // Rows of charts, tallest first, in a square a little bigger than their total area, grown until they fit.
            var order = new int[count];
            for (int i = 0; i < count; i++) { order[i] = i; }
            System.Array.Sort(order, (a, b) => sizes[b].y != sizes[a].y ? sizes[b].y.CompareTo(sizes[a].y) : a.CompareTo(b));
            float area = 0f, widest = 0f;
            foreach (Vector2 s in sizes) { area += s.x * s.y; widest = Mathf.Max(widest, Mathf.Max(s.x, s.y)); }
            float side = Mathf.Max(Mathf.Sqrt(area) * 1.15f, widest);
            var offsets = new Vector2[count];
            for (int attempt = 0; attempt < 200; attempt++, side *= 1.05f)
            {
                float gap = Padding * side;
                float x = gap, y = gap, rowHeight = 0f;
                bool fits = true;
                foreach (int f in order)
                {
                    Vector2 s = sizes[f];
                    if (x + s.x + gap > side && x > gap) { x = gap; y += rowHeight + gap; rowHeight = 0f; }
                    if (x + s.x + gap > side || y + s.y + gap > side) { fits = false; break; }
                    offsets[f] = new Vector2(x, y);
                    x += s.x + gap;
                    rowHeight = Mathf.Max(rowHeight, s.y);
                }
                if (fits) { break; }
            }
            for (int f = 0; f < count; f++)
            {
                foreach (int i in faces[f]) { uv2[i] = (uv2[i] + offsets[f]) / side; }
            }
            return uv2;
        }

        // A face's plane: the normal of the first three of its corners that aren't in a line.
        private static Vector3 FaceNormal(IList<Vector3> vertices, List<int> face)
        {
            for (int i = 2; i < face.Count; i++)
            {
                Vector3 n = Vector3.Cross(vertices[face[1]] - vertices[face[0]], vertices[face[i]] - vertices[face[0]]);
                if (n.sqrMagnitude > 1e-12f) { return n.normalized; }
            }
            return Vector3.up;
        }
    }
}
