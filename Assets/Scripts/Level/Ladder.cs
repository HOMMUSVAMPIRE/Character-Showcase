using System.Collections.Generic;
using UnityEngine;

namespace Level
{
    /// <summary>
    /// A ladder a character can climb (see BasicMovementMode's Wall Climbing settings): straight up and down it, only from
    /// its front (the side its forward, +Z, points to), its hands and feet on its rungs (see LadderIK). Climbed as a
    /// climbable surface is otherwise: taken hold of by heading into it, stepped onto and off at the bottom, jumped off,
    /// let go of, and climbed off the top onto a floor there.
    ///
    /// Its origin is the bottom of the ladder, in the middle of its width and depth, its up the way it climbs. Its rungs
    /// are a rung spacing apart, the first a spacing up, the last at least half a spacing below the top. With Build Mesh
    /// on, it makes its own rails, rungs and box collider (round the whole ladder) from its settings, whenever they change:
    /// set its height in the inspector. A level's ladders (see Graybox.Ladder) are built by the level and leave it off.
    /// Size it with its settings, not its scale.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class Ladder : MonoBehaviour
    {
        [Tooltip("How tall the ladder is (m): its rails' length")]
        [Min(0.2f)] [SerializeField] private float height = 4f;
        [Tooltip("How wide the ladder is (m), from the outside of one rail to the outside of the other")]
        [Min(0.2f)] [SerializeField] private float width = 0.6f;
        [Tooltip("How far apart (m) the rungs are")]
        [Min(0.1f)] [SerializeField] private float rungSpacing = 0.3f;
        [Tooltip("How thick (m) the rails are, each way: the ladder's depth")]
        [Min(0.01f)] [SerializeField] private float railSize = 0.06f;
        [Tooltip("How thick (m) the rungs are")]
        [Min(0.005f)] [SerializeField] private float rungSize = 0.035f;
        [Tooltip("Whether the ladder makes its own mesh and box collider from these settings. Off for a ladder whose " +
                 "geometry is made otherwise (a level's)")]
        [SerializeField] private bool buildMesh = true;
        [Tooltip("The material the ladder's own mesh is drawn with")]
        [SerializeField] private Material material;

        private Mesh mesh;
        private bool dirty = true;

        /// <summary>How tall the ladder is (m).</summary>
        public float Height { get => height; set { height = Mathf.Max(0.2f, value); dirty = true; } }
        /// <summary>How wide the ladder is (m), rail to rail.</summary>
        public float Width { get => width; set { width = Mathf.Max(0.2f, value); dirty = true; } }
        /// <summary>How far apart (m) the rungs are.</summary>
        public float RungSpacing { get => rungSpacing; set { rungSpacing = Mathf.Max(0.1f, value); dirty = true; } }
        /// <summary>How deep (m) the ladder is: its rails' thickness.</summary>
        public float Depth { get => railSize; set { railSize = Mathf.Max(0.01f, value); dirty = true; } }
        /// <summary>How thick (m) the rungs are.</summary>
        public float RungSize { get => rungSize; set { rungSize = Mathf.Max(0.005f, value); dirty = true; } }
        /// <summary>Whether the ladder makes its own mesh and collider.</summary>
        public bool BuildsMesh { get => buildMesh; set { buildMesh = value; dirty = true; } }
        /// <summary>The material its own mesh is drawn with.</summary>
        public Material Material { get => material; set { material = value; dirty = true; } }

        /// <summary>How many rungs it has: one every rung spacing from a spacing up, to at least half a spacing below the top.</summary>
        public int RungCount => RungCountFor(height, rungSpacing);

        /// <summary>The rung count of a ladder of the given height and rung spacing (as RungCount).</summary>
        public static int RungCountFor(float height, float spacing) =>
            spacing > 0f ? Mathf.Max(0, Mathf.FloorToInt((height - 0.5f * spacing) / spacing + 1e-4f)) : 0;

        /// <summary>How high (m) above the bottom rung k is (0 is the lowest).</summary>
        public float RungHeight(int k) => (k + 1) * rungSpacing;

        /// <summary>The way it's climbed, in world space (unit length).</summary>
        public Vector3 Up => transform.up;
        /// <summary>The way its front faces, toward whoever climbs it (unit length).</summary>
        public Vector3 Forward => transform.forward;
        /// <summary>Its own +X, across it (unit length): to the left of someone climbing it, who faces it from the front.</summary>
        public Vector3 Right => transform.right;
        /// <summary>The middle of the bottom of its front face, in world space.</summary>
        public Vector3 FrontBottom => transform.position + Forward * (0.5f * railSize);

        /// <summary>How high a point is up the ladder from its bottom (m).</summary>
        public float HeightOf(Vector3 point) => Vector3.Dot(point - transform.position, Up);
        /// <summary>How far a point is to the ladder's side of its middle (m, along Right).</summary>
        public float SideOf(Vector3 point) => Vector3.Dot(point - transform.position, Right);
        /// <summary>How far in front of its front face a point is (m; negative behind it).</summary>
        public float InFrontOf(Vector3 point) => Vector3.Dot(point - transform.position, Forward) - 0.5f * railSize;

        /// <summary>The ladder a collider belongs to (on it or a parent, and enabled), or null if it isn't one's.</summary>
        public static Ladder Of(Collider collider)
        {
            if (collider == null) { return null; }
            Ladder ladder = collider.GetComponentInParent<Ladder>();
            return ladder != null && ladder.isActiveAndEnabled ? ladder : null;
        }

        // Rebuilt on the next update, not here: OnEnable runs as the component is added, before whoever added it has set
        // it up, and a level's ladder (Build Mesh off) would have had its mesh and collider replaced by the defaults'.
        private void OnEnable() => dirty = true;
        private void OnValidate() => dirty = true;

        // Rebuilt when the settings change (in the editor too), not in OnValidate, where components can't be added.
        private void Update()
        {
            if (dirty) { Rebuild(); }
        }

        /// <summary>Makes the ladder's own mesh and collider again, now (if it builds them).</summary>
        public void Rebuild()
        {
            dirty = false;
            if (!buildMesh) { return; }
            if (!TryGetComponent(out MeshFilter filter)) { filter = gameObject.AddComponent<MeshFilter>(); }
            if (!TryGetComponent(out MeshRenderer renderer)) { renderer = gameObject.AddComponent<MeshRenderer>(); }
            if (!TryGetComponent(out BoxCollider box)) { box = gameObject.AddComponent<BoxCollider>(); }
            if (mesh == null)
            {
                // Made from the settings whenever the ladder loads, so not saved with the scene.
                mesh = new Mesh { name = "Ladder", hideFlags = HideFlags.DontSave };
            }
            BuildMesh(mesh, height, width, rungSpacing, railSize, rungSize);
            filter.sharedMesh = mesh;
            if (material != null) { renderer.sharedMaterial = material; }
            box.center = new Vector3(0f, height / 2f, 0f);
            box.size = new Vector3(width, height, railSize);
        }

        /// <summary>
        /// Fills a mesh with a ladder of the given size (in its own space, as the class's summary sets out): two rails and
        /// its rungs, square bars, flat shaded.
        /// </summary>
        public static void BuildMesh(Mesh mesh, float height, float width, float spacing, float railSize, float rungSize)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            void Bar(Vector3 min, Vector3 max)
            {
                Vector3[] c =
                {
                    new(min.x, min.y, min.z), new(max.x, min.y, min.z), new(max.x, max.y, min.z), new(min.x, max.y, min.z),
                    new(min.x, min.y, max.z), new(max.x, min.y, max.z), new(max.x, max.y, max.z), new(min.x, max.y, max.z),
                };
                int[][] faces = { new[] { 0, 3, 2, 1 }, new[] { 4, 5, 6, 7 }, new[] { 0, 4, 7, 3 }, new[] { 1, 2, 6, 5 }, new[] { 3, 7, 6, 2 }, new[] { 0, 1, 5, 4 } };
                foreach (int[] f in faces)
                {
                    int first = vertices.Count;
                    foreach (int i in f) { vertices.Add(c[i]); }
                    triangles.AddRange(new[] { first, first + 1, first + 2, first, first + 2, first + 3 });
                }
            }
            float halfWidth = width / 2f, r = railSize / 2f, s = rungSize / 2f;
            Bar(new Vector3(-halfWidth, 0f, -r), new Vector3(-halfWidth + railSize, height, r));
            Bar(new Vector3(halfWidth - railSize, 0f, -r), new Vector3(halfWidth, height, r));
            int rungs = RungCountFor(height, spacing);
            for (int k = 0; k < rungs; k++)
            {
                float y = (k + 1) * spacing;
                Bar(new Vector3(-halfWidth + railSize, y - s, -s), new Vector3(halfWidth - railSize, y + s, s));
            }
            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }

        private void OnDestroy()
        {
            if (mesh == null) { return; }
            if (Application.isPlaying) { Destroy(mesh); } else { DestroyImmediate(mesh); }
        }
    }
}
