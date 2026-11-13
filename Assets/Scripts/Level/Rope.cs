using System.Collections.Generic;
using UnityEngine;

namespace Level
{
    /// <summary>
    /// A rope a character can swing on (see BasicMovementMode's Rope Swinging settings): hanging from an anchor (this
    /// object's position), length metres long. A character reaching it in mid-air catches hold of it and swings on it as a
    /// pendulum; let go of, the rope swings on by itself, slowing until it hangs straight down again, and can be caught
    /// on its way back.
    ///
    /// The rope is a straight line from its anchor (it doesn't bend), and nothing collides with it: characters find it
    /// through All. Its swing is simulated in FixedUpdate, as physics is, and drawn in between physics steps as an
    /// interpolated Rigidbody is, so a character's hands drawn on it stay on it. With Build Mesh on, it draws itself (a
    /// mesh on this object, made again whenever it swings). Size it with its settings, not its scale.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class Rope : MonoBehaviour
    {
        [Tooltip("How long the rope is (m), from its anchor (this object's position) down")]
        [Min(0.5f)] [SerializeField] private float length = 8f;
        [Tooltip("How thick (m) the rope is drawn")]
        [Min(0.005f)] [SerializeField] private float thickness = 0.07f;
        [Tooltip("How quickly the rope's own swing dies away once it's let go of: the share of its speed it loses each second")]
        [Range(0f, 5f)] [SerializeField] private float swingDamping = 0.35f;
        [Tooltip("Whether the rope draws itself: a mesh on this object, made from these settings, that swings with it")]
        [SerializeField] private bool buildMesh = true;
        [Tooltip("The material the rope is drawn with (left empty, the MeshRenderer's own)")]
        [SerializeField] private Material material;

        private static readonly List<Rope> active = new();

        /// <summary>Every enabled rope, for characters to look for one to catch.</summary>
        public static IReadOnlyList<Rope> All => active;

        // The way down the rope from its anchor (unit length) at the last physics step and the one before it (for
        // drawing between them), and, swinging by itself, its bottom end's velocity.
        private Vector3 direction = Vector3.down, previousDirection = Vector3.down;
        private Vector3 tipVelocity;
        // The physics step (Time.fixedTime) direction was last moved on from, so it's moved on once per step, whether the
        // rope's own FixedUpdate or a holder comes first.
        private float snapshotTime = float.NegativeInfinity;
        private Object holder;

        private Mesh mesh;
        // The way down it was last drawn, and its object's rotation then (the mesh is in its space, hanging toward world
        // down, so turning the object needs it drawn again).
        private Vector3 drawnDirection;
        private Quaternion drawnRotation;
        private bool dirty = true;

        /// <summary>How long (m) the rope is.</summary>
        public float Length { get => length; set { length = Mathf.Max(0.5f, value); dirty = true; } }
        /// <summary>How thick (m) it's drawn.</summary>
        public float Thickness { get => thickness; set { thickness = Mathf.Max(0.005f, value); dirty = true; } }
        /// <summary>The share of its own swing's speed it loses each second.</summary>
        public float SwingDamping { get => swingDamping; set => swingDamping = Mathf.Max(0f, value); }
        /// <summary>Whether it draws itself.</summary>
        public bool BuildsMesh { get => buildMesh; set { buildMesh = value; dirty = true; } }
        /// <summary>The material it's drawn with (null: the MeshRenderer's own).</summary>
        public Material Material { get => material; set { material = value; dirty = true; } }

        /// <summary>Where the rope hangs from, in world space.</summary>
        public Vector3 Anchor => transform.position;
        /// <summary>The way down the rope from its anchor (unit length), at the last physics step.</summary>
        public Vector3 Direction => direction;
        /// <summary>Its bottom end, at the last physics step.</summary>
        public Vector3 Tip => Anchor + direction * length;
        /// <summary>Whether a character is holding it (and so swinging it).</summary>
        public bool IsHeld => holder != null;
        /// <summary>Who's holding it, if anyone.</summary>
        public Object Holder => holder;

        /// <summary>
        /// The way down the rope as it's drawn: between its last two physics steps, as far through as the frame is, as an
        /// interpolated Rigidbody is drawn (so a character on it, drawn so too, keeps its hands on it). Straight down in
        /// the editor.
        /// </summary>
        public Vector3 RenderDirection
        {
            get
            {
                if (!Application.isPlaying) { return Vector3.down; }
                float t = Time.inFixedTimeStep || Time.fixedDeltaTime <= 0f ? 1f
                    : Mathf.Clamp01((Time.time - Time.fixedTime) / Time.fixedDeltaTime);
                Vector3 drawn = Vector3.Slerp(previousDirection, direction, t);
                return drawn.sqrMagnitude > 1e-8f ? drawn.normalized : direction;
            }
        }

        /// <summary>The point on the rope nearest point (at the last physics step), and how far down the rope (m) from its anchor that is.</summary>
        public Vector3 ClosestPoint(Vector3 point, out float along)
        {
            along = Mathf.Clamp(Vector3.Dot(point - Anchor, direction), 0f, length);
            return Anchor + direction * along;
        }

        /// <summary>
        /// Holds the rope this physics step, swung so that it runs down from its anchor along down (a holder swinging on it
        /// calls this every step). The rope doesn't swing by itself while it's held.
        /// </summary>
        public void Hold(Object by, Vector3 down)
        {
            Snapshot();
            holder = by;
            if (down.sqrMagnitude > 1e-8f) { direction = down.normalized; }
            tipVelocity = Vector3.zero;
        }

        /// <summary>
        /// Lets go of the rope (if by is holding it), which then swings on by itself, its bottom end starting at the given
        /// velocity (m/s).
        /// </summary>
        public void Release(Object by, Vector3 endVelocity)
        {
            if (holder != by) { return; }
            holder = null;
            tipVelocity = endVelocity - direction * Vector3.Dot(endVelocity, direction);
        }

        /// <summary>The rope's way down as the last physics step left it becomes the one before, once each step.</summary>
        private void Snapshot()
        {
            if (snapshotTime == Time.fixedTime) { return; }
            snapshotTime = Time.fixedTime;
            previousDirection = direction;
        }

        private void OnEnable()
        {
            if (!active.Contains(this)) { active.Add(this); }
            direction = previousDirection = Vector3.down;
            tipVelocity = Vector3.zero;
            dirty = true;
        }

        private void OnDisable()
        {
            active.Remove(this);
            holder = null;
        }

        private void OnValidate() => dirty = true;

        /// <summary>
        /// Swings the rope by itself while no one holds it: a pendulum, its bottom end falling under gravity, held at the
        /// rope's length from the anchor, losing a share of its speed each second.
        /// </summary>
        private void FixedUpdate()
        {
            if (!Application.isPlaying) { return; }
            Snapshot();
            if (holder != null) { return; }
            // A holder destroyed without letting go (Unity's null check: the reference itself isn't null).
            if (!ReferenceEquals(holder, null)) { holder = null; }

            float dt = Time.fixedDeltaTime;
            if (tipVelocity.sqrMagnitude < 1e-6f && Vector3.Angle(direction, Vector3.down) < 0.05f) { return; } // At rest.
            tipVelocity += Physics.gravity * dt;
            tipVelocity -= direction * Vector3.Dot(tipVelocity, direction);
            tipVelocity *= Mathf.Max(0f, 1f - swingDamping * dt);
            Vector3 tip = direction * length + tipVelocity * dt;
            if (tip.sqrMagnitude < 1e-8f) { return; }
            Vector3 swung = tip.normalized;
            // The velocity the step actually moved it at, so it stays across the rope.
            tipVelocity = (swung - direction) * (length / dt);
            direction = swung;
        }

        private void Update()
        {
            if (!buildMesh) { return; }
            Vector3 drawn = RenderDirection;
            if (dirty || drawnDirection != drawn || drawnRotation != transform.rotation) { Rebuild(drawn); }
        }

        /// <summary>Draws the rope again, now, as it hangs (if it draws itself).</summary>
        public void Rebuild() => Rebuild(RenderDirection);

        private void Rebuild(Vector3 down)
        {
            dirty = false;
            drawnDirection = down;
            drawnRotation = transform.rotation;
            if (!buildMesh) { return; }
            if (!TryGetComponent(out MeshFilter filter)) { filter = gameObject.AddComponent<MeshFilter>(); }
            if (!TryGetComponent(out MeshRenderer renderer)) { renderer = gameObject.AddComponent<MeshRenderer>(); }
            if (mesh == null)
            {
                // Made from the settings, and again as it swings, so not saved with the scene.
                mesh = new Mesh { name = "Rope", hideFlags = HideFlags.DontSave };
                mesh.MarkDynamic();
            }
            LineMesh.Build(mesh, transform, Anchor, Anchor + down * length, thickness);
            filter.sharedMesh = mesh;
            if (material != null) { renderer.sharedMaterial = material; }
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        }

        private void OnDestroy()
        {
            if (mesh == null) { return; }
            if (Application.isPlaying) { Destroy(mesh); } else { DestroyImmediate(mesh); }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.55f, 0.1f);
            Gizmos.DrawLine(Anchor, Anchor + RenderDirection * length);
            Gizmos.DrawWireSphere(Anchor, 0.1f);
        }
    }

    /// <summary>A thin square bar from one point to another, as a mesh in a transform's space: a rope's or a cable's.</summary>
    internal static class LineMesh
    {
        private static readonly Vector3[] vertices = new Vector3[24];
        private static readonly Vector3[] normals = new Vector3[24];
        private static readonly Vector2[] uvs = new Vector2[24];
        private static readonly int[] triangles = new int[36];
        private static readonly int[] Forward = { 0, 1, 2, 0, 2, 3 }, Reversed = { 0, 2, 1, 0, 3, 2 };

        public static void Build(Mesh mesh, Transform space, Vector3 from, Vector3 to, float thickness)
        {
            Vector3 along = to - from;
            float length = along.magnitude;
            Vector3 axis = length > 1e-6f ? along / length : Vector3.down;
            Vector3 side = Vector3.Cross(axis, Mathf.Abs(axis.y) < 0.95f ? Vector3.up : Vector3.forward).normalized;
            Vector3 other = Vector3.Cross(axis, side);
            float h = thickness * 0.5f;
            // The four long faces, then the two ends; each face its own four corners, so it's shaded flat.
            Vector3[] faceNormals = { side, other, -side, -other };
            int v = 0, t = 0;
            for (int f = 0; f < 4; f++)
            {
                Vector3 n = faceNormals[f], across = faceNormals[(f + 1) % 4];
                Vector3 a = (n - across) * h, b = (n + across) * h;
                AddQuad(space, ref v, ref t, from + a, from + b, to + b, to + a, n, length);
            }
            Vector3 c0 = (side + other) * h, c1 = (-side + other) * h, c2 = (-side - other) * h, c3 = (side - other) * h;
            AddQuad(space, ref v, ref t, from + c0, from + c3, from + c2, from + c1, -axis, thickness);
            AddQuad(space, ref v, ref t, to + c0, to + c1, to + c2, to + c3, axis, thickness);

            mesh.Clear();
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
        }

        private static void AddQuad(Transform space, ref int v, ref int t, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal, float length)
        {
            Vector3 n = space.InverseTransformDirection(normal).normalized;
            vertices[v] = space.InverseTransformPoint(a); vertices[v + 1] = space.InverseTransformPoint(b);
            vertices[v + 2] = space.InverseTransformPoint(c); vertices[v + 3] = space.InverseTransformPoint(d);
            for (int i = 0; i < 4; i++) { normals[v + i] = n; }
            uvs[v] = new Vector2(0f, 0f); uvs[v + 1] = new Vector2(1f, 0f); uvs[v + 2] = new Vector2(1f, length); uvs[v + 3] = new Vector2(0f, length);
            // Wound so the face is seen from the normal's side: Unity's front faces are clockwise as seen, which makes
            // Cross(b - a, c - a) point toward the viewer.
            bool flip = Vector3.Dot(Vector3.Cross(b - a, c - a), normal) < 0f;
            int[] order = flip ? Reversed : Forward;
            for (int i = 0; i < 6; i++) { triangles[t + i] = v + order[i]; }
            v += 4;
            t += 6;
        }
    }
}
