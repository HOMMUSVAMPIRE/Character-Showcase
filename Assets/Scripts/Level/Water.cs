using System.Collections.Generic;
using UnityEngine;

namespace Level
{
    /// <summary>
    /// A body of water a character can swim in (see the movement mode's Swimming component): a box whose top is the
    /// water's surface, at this object's position, reaching down its depth and across its width and length (this
    /// object's x and z: it may be turned about the vertical, not tilted). A character deep enough in it swims at the
    /// surface.
    ///
    /// Nothing collides with the water: characters find it through All, and stand on whatever is under it (a pool's
    /// floor). With Build Mesh on, it draws its own surface (a flat grid on this object, made from its settings whenever
    /// they change); a level's water (see Graybox.Water) is drawn by the level and leaves it off. It doesn't move: moving
    /// water isn't supported.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class Water : MonoBehaviour
    {
        [Tooltip("The water's size (m): its width (x), its depth below the surface (y) and its length (z), in this " +
                 "object's space. Its surface is at this object's position")]
        [SerializeField] private Vector3 size = new Vector3(10f, 3f, 10f);
        [Tooltip("Whether the water draws its own surface: a flat grid on this object, made from these settings. Off for " +
                 "water drawn otherwise (a level's)")]
        [SerializeField] private bool buildMesh = true;
        [Tooltip("The material the surface is drawn with (left empty, the MeshRenderer's own)")]
        [SerializeField] private Material material;

        // The surface grid's cells' size (m): fine enough for the water shader's waves to bend it.
        private const float GridCell = 1f;
        // The most cells along either side of the grid, so a lake's surface stays a reasonable mesh.
        private const int MaxGridCells = 128;

        private static readonly List<Water> active = new();

        /// <summary>Every enabled body of water, for characters to find the one they're in.</summary>
        public static IReadOnlyList<Water> All => active;

        private Mesh mesh;
        private bool dirty = true;

        /// <summary>The water's size (m): width (x), depth below the surface (y) and length (z), in its own space.</summary>
        public Vector3 Size { get => size; set { size = Vector3.Max(value, Vector3.zero); dirty = true; } }
        /// <summary>Whether it draws its own surface.</summary>
        public bool BuildsMesh { get => buildMesh; set { buildMesh = value; dirty = true; } }
        /// <summary>The material its surface is drawn with (null: the MeshRenderer's own).</summary>
        public Material Material { get => material; set { material = value; dirty = true; } }
        /// <summary>The height (m, world space) of the water's surface.</summary>
        public float SurfaceHeight => transform.position.y;
        /// <summary>The height (m, world space) of the water's bottom.</summary>
        public float BottomHeight => transform.position.y - size.y;

        /// <summary>Whether point is over or under the water's surface: within its width and length, at any height.</summary>
        public bool Covers(Vector3 point)
        {
            Vector3 local = Quaternion.Inverse(transform.rotation) * (point - transform.position);
            return Mathf.Abs(local.x) <= size.x * 0.5f && Mathf.Abs(local.z) <= size.z * 0.5f;
        }

        /// <summary>Whether point is in the water: within its width and length, between its bottom and its surface.</summary>
        public bool Contains(Vector3 point) => point.y <= SurfaceHeight && point.y >= BottomHeight && Covers(point);

        /// <summary>
        /// The body of water point is in (see Contains), the one with the highest surface where several overlap, or null
        /// if it's in none.
        /// </summary>
        public static Water At(Vector3 point)
        {
            Water found = null;
            for (int i = 0; i < active.Count; i++)
            {
                Water water = active[i];
                if (water == null || !water.Contains(point)) { continue; }
                if (found == null || water.SurfaceHeight > found.SurfaceHeight) { found = water; }
            }
            return found;
        }

        /// <summary>
        /// The body of water whose footprint point is over (or under) and whose surface is at most above below it (or
        /// above it): what point is in, or over by no more than above (a character just leaving it, for a splash where it
        /// left). The one with the highest surface where several are; null if none.
        /// </summary>
        public static Water Under(Vector3 point, float above)
        {
            Water found = null;
            for (int i = 0; i < active.Count; i++)
            {
                Water water = active[i];
                if (water == null || point.y < water.BottomHeight || point.y > water.SurfaceHeight + above || !water.Covers(point)) { continue; }
                if (found == null || water.SurfaceHeight > found.SurfaceHeight) { found = water; }
            }
            return found;
        }

        private void OnEnable()
        {
            if (!active.Contains(this)) { active.Add(this); }
            dirty = true;
        }

        private void OnDisable() => active.Remove(this);
        private void OnValidate() => dirty = true;

        // Rebuilt when the settings change (in the editor too), not in OnValidate, where components can't be added. The
        // mesh is in the water's own space, so moving or turning it needs no rebuild.
        private void Update()
        {
            if (dirty) { Rebuild(); }
        }

        /// <summary>Makes the surface's mesh again, now (if it draws its own).</summary>
        public void Rebuild()
        {
            dirty = false;
            if (!buildMesh) { return; }
            if (!TryGetComponent(out MeshFilter filter)) { filter = gameObject.AddComponent<MeshFilter>(); }
            if (!TryGetComponent(out MeshRenderer renderer)) { renderer = gameObject.AddComponent<MeshRenderer>(); }
            // Made from the settings whenever the water loads, so not saved with the scene.
            if (mesh == null) { mesh = new Mesh { name = "Water Surface", hideFlags = HideFlags.DontSave }; }
            BuildSurface(mesh, size.x, size.z);
            filter.sharedMesh = mesh;
            if (material != null) { renderer.sharedMaterial = material; }
        }

        /// <summary>
        /// Fills mesh with a flat grid, width by length (m), centred on the origin and facing up, in cells of about
        /// GridCell (so the water shader's waves can bend it), with a UV over it in metres.
        /// </summary>
        public static void BuildSurface(Mesh mesh, float width, float length)
        {
            int columns = Mathf.Clamp(Mathf.CeilToInt(width / GridCell), 1, MaxGridCells);
            int rows = Mathf.Clamp(Mathf.CeilToInt(length / GridCell), 1, MaxGridCells);
            var vertices = new Vector3[(columns + 1) * (rows + 1)];
            var uvs = new Vector2[vertices.Length];
            var normals = new Vector3[vertices.Length];
            for (int r = 0, v = 0; r <= rows; r++)
            {
                for (int c = 0; c <= columns; c++, v++)
                {
                    float x = (c / (float)columns - 0.5f) * width, z = (r / (float)rows - 0.5f) * length;
                    vertices[v] = new Vector3(x, 0f, z);
                    uvs[v] = new Vector2(x, z);
                    normals[v] = Vector3.up;
                }
            }
            var triangles = new int[columns * rows * 6];
            for (int r = 0, t = 0; r < rows; r++)
            {
                for (int c = 0; c < columns; c++, t += 6)
                {
                    int a = r * (columns + 1) + c, b = a + 1, d = a + columns + 1, e = d + 1;
                    triangles[t] = a; triangles[t + 1] = d; triangles[t + 2] = b;
                    triangles[t + 3] = b; triangles[t + 4] = d; triangles[t + 5] = e;
                }
            }
            mesh.Clear();
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
        }

        private void OnDestroy()
        {
            if (mesh == null) { return; }
            if (Application.isPlaying) { Destroy(mesh); } else { DestroyImmediate(mesh); }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.55f, 1f);
            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
            Gizmos.DrawWireCube(new Vector3(0f, -size.y * 0.5f, 0f), size);
        }
    }
}
