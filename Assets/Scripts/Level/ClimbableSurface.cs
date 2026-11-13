using UnityEngine;

namespace Level
{
    /// <summary>
    /// Marks colliders a character can climb (see BasicMovementMode's Wall Climbing settings): this object's colliders and
    /// those of its children, so one on a group makes the whole group climbable. Every face of a climbable collider that's
    /// steep enough is climbable, and a character climbing it moves on round corners onto the next climbable face, and
    /// across from one climbable collider to another touching it, but not onto anything without this. Disable the
    /// component to make the surface unclimbable for a while. To make only parts of an object climbable (one face, or
    /// part of one), paint them with a PaintedClimbableSurface instead.
    /// </summary>
    [DisallowMultipleComponent]
    public class ClimbableSurface : MonoBehaviour
    {
        [Tooltip("How fast a character climbs on it, as a share of its climbing speed: lower for a hard climb, higher for " +
                 "a ladder.")]
        [Range(0.1f, 2f)] [SerializeField] private float speedMultiplier = 1f;

        /// <summary>How fast a character climbs on it, as a share of its climbing speed.</summary>
        public float SpeedMultiplier { get => speedMultiplier; set => speedMultiplier = Mathf.Clamp(value, 0.1f, 2f); }

        /// <summary>The climbable surface a collider belongs to (on it or a parent, and enabled), or null if it isn't climbable.</summary>
        public static ClimbableSurface Of(Collider collider)
        {
            if (collider == null) { return null; }
            ClimbableSurface surface = collider.GetComponentInParent<ClimbableSurface>();
            return surface != null && surface.isActiveAndEnabled ? surface : null;
        }

        /// <summary>
        /// Whether a point on a collider (facing normal there, at an edge or not: see PaintedClimbableSurface.IsPaintedAt)
        /// can be climbed: the collider is climbable as a whole (a ClimbableSurface), or the point is painted climbable (a
        /// PaintedClimbableSurface, on the collider or a parent), paint within tolerance (m) counting. Gives the climbing
        /// speed multiplier there.
        /// </summary>
        public static bool IsClimbableAt(Collider collider, Vector3 point, Vector3 normal, bool atEdge, float tolerance, out float speedMultiplier)
        {
            speedMultiplier = 1f;
            ClimbableSurface whole = Of(collider);
            if (whole != null)
            {
                speedMultiplier = whole.SpeedMultiplier;
                return true;
            }
            if (collider == null) { return false; }
            PaintedClimbableSurface painted = collider.GetComponentInParent<PaintedClimbableSurface>();
            if (painted == null || !painted.isActiveAndEnabled || !painted.IsPaintedAt(point, normal, atEdge, tolerance)) { return false; }
            speedMultiplier = painted.SpeedMultiplier;
            return true;
        }
    }
}
