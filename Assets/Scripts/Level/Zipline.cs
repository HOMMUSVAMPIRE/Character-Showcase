using System.Collections.Generic;
using UnityEngine;

namespace Level
{
    /// <summary>
    /// A zipline a character can ride (see BasicMovementMode's Ziplines settings): a straight cable from this object's
    /// position to its end point (in this object's space). A character reaching the cable in mid-air catches hold of it
    /// and rides along it, speeding up downhill, to whichever end it's heading for, and lets go there.
    ///
    /// Nothing collides with the cable: characters find it through All. With Build Mesh on, it draws its own cable (a mesh
    /// on this object, made from its settings whenever they change); a level's ziplines (see Graybox.Zipline) are drawn
    /// by the level and leave it off. It doesn't move: a moving zipline isn't supported.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class Zipline : MonoBehaviour
    {
        [Tooltip("Where the cable ends, in this object's space (it starts at this object's position)")]
        [SerializeField] private Vector3 endPoint = new Vector3(0f, -4f, 20f);
        [Tooltip("The fastest (m/s) a character rides along the cable")]
        [Min(0.5f)] [SerializeField] private float maxSpeed = 16f;
        [Tooltip("How thick (m) the cable is drawn")]
        [Min(0.005f)] [SerializeField] private float thickness = 0.04f;
        [Tooltip("Whether the zipline draws its own cable: a mesh on this object, made from these settings. Off for a cable drawn otherwise (a level's)")]
        [SerializeField] private bool buildMesh = true;
        [Tooltip("The material the cable is drawn with (left empty, the MeshRenderer's own)")]
        [SerializeField] private Material material;

        private static readonly List<Zipline> active = new();

        /// <summary>Every enabled zipline, for characters to look for one to catch.</summary>
        public static IReadOnlyList<Zipline> All => active;

        private Mesh mesh;
        private bool dirty = true;

        /// <summary>Where the cable starts, in world space: this object's position.</summary>
        public Vector3 Start => transform.position;
        /// <summary>Where the cable ends, in world space.</summary>
        public Vector3 End { get => transform.TransformPoint(endPoint); set { endPoint = transform.InverseTransformPoint(value); dirty = true; } }
        /// <summary>Where the cable ends, in this object's space.</summary>
        public Vector3 LocalEnd { get => endPoint; set { endPoint = value; dirty = true; } }
        /// <summary>How long (m) the cable is.</summary>
        public float Length => Vector3.Distance(Start, End);
        /// <summary>The way along the cable from its start to its end (unit length).</summary>
        public Vector3 Direction
        {
            get
            {
                Vector3 along = End - Start;
                return along.sqrMagnitude > 1e-8f ? along.normalized : Vector3.forward;
            }
        }
        /// <summary>The fastest (m/s) a character rides along it.</summary>
        public float MaxSpeed { get => maxSpeed; set => maxSpeed = Mathf.Max(0.5f, value); }
        /// <summary>How thick (m) the cable is drawn.</summary>
        public float Thickness { get => thickness; set { thickness = Mathf.Max(0.005f, value); dirty = true; } }
        /// <summary>Whether it draws its own cable.</summary>
        public bool BuildsMesh { get => buildMesh; set { buildMesh = value; dirty = true; } }
        /// <summary>The material its cable is drawn with (null: the MeshRenderer's own).</summary>
        public Material Material { get => material; set { material = value; dirty = true; } }

        /// <summary>The point on the cable the given distance (m) along it from its start (clamped to the cable).</summary>
        public Vector3 PointAt(float along) => Start + Direction * Mathf.Clamp(along, 0f, Length);

        /// <summary>The point on the cable nearest point, and how far (m) along the cable from its start that is.</summary>
        public Vector3 ClosestPoint(Vector3 point, out float along)
        {
            along = Mathf.Clamp(Vector3.Dot(point - Start, Direction), 0f, Length);
            return Start + Direction * along;
        }

        /// <summary>
        /// The point on the cable straight above (or below) point, in the cable's span, for something hanging from it; the
        /// nearest point, for a cable too steep for that.
        /// </summary>
        public Vector3 PointAbove(Vector3 point)
        {
            Vector3 direction = Direction;
            Vector2 flat = new Vector2(direction.x, direction.z);
            if (flat.sqrMagnitude < 1e-4f) { return ClosestPoint(point, out _); }
            Vector3 offset = point - Start;
            float along = Vector2.Dot(new Vector2(offset.x, offset.z), flat) / flat.sqrMagnitude;
            return PointAt(along);
        }

        private void OnEnable()
        {
            if (!active.Contains(this)) { active.Add(this); }
            dirty = true;
        }

        private void OnDisable() => active.Remove(this);
        private void OnValidate() => dirty = true;

        // Rebuilt when the settings change (in the editor too), not in OnValidate, where components can't be added. The
        // mesh is in the zipline's own space, from its origin to its end point, so moving or turning it needs no rebuild.
        private void Update()
        {
            if (dirty) { Rebuild(); }
        }

        /// <summary>Makes the cable's mesh again, now (if it draws its own).</summary>
        public void Rebuild()
        {
            dirty = false;
            if (!buildMesh) { return; }
            if (!TryGetComponent(out MeshFilter filter)) { filter = gameObject.AddComponent<MeshFilter>(); }
            if (!TryGetComponent(out MeshRenderer renderer)) { renderer = gameObject.AddComponent<MeshRenderer>(); }
            // Made from the settings whenever the zipline loads, so not saved with the scene.
            if (mesh == null) { mesh = new Mesh { name = "Zipline Cable", hideFlags = HideFlags.DontSave }; }
            LineMesh.Build(mesh, transform, Start, End, thickness);
            filter.sharedMesh = mesh;
            if (material != null) { renderer.sharedMaterial = material; }
        }

        private void OnDestroy()
        {
            if (mesh == null) { return; }
            if (Application.isPlaying) { Destroy(mesh); } else { DestroyImmediate(mesh); }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.55f, 0.1f);
            Gizmos.DrawLine(Start, End);
            Gizmos.DrawWireSphere(Start, 0.15f);
            Gizmos.DrawWireCube(End, Vector3.one * 0.3f);
        }
    }
}
