using System.Collections.Generic;
using UnityEngine;

namespace CameraSystem
{
    /// <summary>
    /// A volume that overrides camera behaviour while the camera's target is inside it — e.g. force first person
    /// in a cramped vent, or cap the zoom distance in a narrow corridor. Uses a trigger collider for its shape,
    /// but checks containment itself, so it doesn't need the target to have a Rigidbody.
    /// Supports primitive colliders and convex mesh colliders.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class CameraZone : MonoBehaviour
    {
        /// <summary>The enabled zones, which Find searches.</summary>
        internal static readonly List<CameraZone> Active = new();

        [Tooltip("When zones overlap, the highest priority zone wins.")]
        public int priority = 0;

        /// <summary>Force the view mode below while the target is inside this zone.</summary>
        [Header("View mode")]
        [Tooltip("Force the view mode below while the target is inside this zone.")]
        public bool forceMode = false;
        /// <summary>The view mode forced while inside, if Force Mode is on.</summary>
        [Tooltip("The view mode forced while inside, if Force Mode is on.")]
        public CameraMode mode = CameraMode.FirstPerson;

        [Header("Third person distance")]
        [Tooltip("Cap the third person distance while inside this zone.")]
        public bool limitDistance = false;
        /// <summary>The furthest the third person camera may be (m) while inside, if Limit Distance is on.</summary>
        [Tooltip("The furthest the third person camera may be (m) while inside, if Limit Distance is on.")]
        [Min(0.1f)] public float maxDistance = 2f;

        [Header("Field of view")]
        [Tooltip("Degrees added to (or, if negative, removed from) the field of view while inside this zone.")]
        [Range(-40f, 40f)] public float fovOffset = 0f;

        private Collider zoneCollider;

        private void Awake()
        {
            zoneCollider = GetComponent<Collider>();
            zoneCollider.isTrigger = true;
        }

        private void OnEnable() => Active.Add(this);
        private void OnDisable() => Active.Remove(this);

        /// <summary>Whether the point is inside this zone's enabled collider.</summary>
        public bool Contains(Vector3 point)
        {
            if (zoneCollider == null || !zoneCollider.enabled) { return false; }
            if (!zoneCollider.bounds.Contains(point)) { return false; }
            return (zoneCollider.ClosestPoint(point) - point).sqrMagnitude < 1e-6f;
        }

        /// <summary>Returns the highest priority enabled zone containing the point, or null.</summary>
        internal static CameraZone Find(Vector3 point)
        {
            CameraZone best = null;
            foreach (var zone in Active)
            {
                if ((best == null || zone.priority > best.priority) && zone.Contains(point)) { best = zone; }
            }
            return best;
        }

        private void OnDrawGizmos()
        {
            var c = GetComponent<Collider>();
            if (c == null) { return; }
            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.15f);
            Gizmos.DrawCube(c.bounds.center, c.bounds.size);
            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.8f);
            Gizmos.DrawWireCube(c.bounds.center, c.bounds.size);
        }
    }
}
