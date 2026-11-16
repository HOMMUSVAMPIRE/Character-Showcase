using UnityEngine;

namespace Character
{
    // For movement modes: the character's size, and world queries that skip the character's own colliders.
    public partial class MovementController
    {
        /// <summary>The character's collider height standing up, in metres at its size (its collider may be lower: see HeightFraction).</summary>
        public float StandingHeight => BaseHeight * Scale.y;
        /// <summary>The character's collider radius, in metres at its size.</summary>
        public float Radius => Body.Collider != null ? Body.Radius : 0.5f * HeightScale;
        /// <summary>The gap the character keeps from surfaces, in metres at its size.</summary>
        public float Skin => SkinWidth;
        /// <summary>The physics layers the character collides with.</summary>
        public LayerMask MovementLayerMask => movementLayerMask;

        /// <summary>
        /// Sweeps the character's collider (as it is now) from feet along direction, returning the nearest thing it
        /// would hit within distance, other than the character itself. A hit's distance includes the skin width, so a
        /// surface the character rests against is about Skin away.
        /// </summary>
        public bool SweepBody(Vector3 feet, Vector3 direction, float distance, out RaycastHit hit) =>
            Body.Sweep(feet, direction, distance, out hit);

        /// <summary>The nearest thing a ray hits within distance, other than the character itself (triggers aren't hit).</summary>
        public bool Raycast(Vector3 origin, Vector3 direction, float distance, out RaycastHit hit)
        {
            hit = default;
            if (direction.sqrMagnitude < 1e-8f || distance <= 0f) { return false; }
            int count = Physics.RaycastNonAlloc(origin, direction.normalized, castHits, distance, movementLayerMask, QueryTriggerInteraction.Ignore);
            float nearest = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                RaycastHit candidate = castHits[i];
                if (IsOwnCollider(candidate.collider.transform) || candidate.distance >= nearest) { continue; }
                nearest = candidate.distance;
                hit = candidate;
            }
            return float.IsFinite(nearest);
        }

        /// <summary>
        /// Finds the colliders within radius of center, other than the character's own (and what it ignores: see
        /// IgnoredCollisionRoot), triggers aside, into results. Returns how many there are.
        /// </summary>
        public int OverlapSphere(Vector3 center, float radius, Collider[] results)
        {
            if (results == null || results.Length == 0 || radius <= 0f) { return 0; }
            int count = Physics.OverlapSphereNonAlloc(center, radius, results, movementLayerMask, QueryTriggerInteraction.Ignore);
            int kept = 0;
            for (int i = 0; i < count; i++)
            {
                if (!IsOwnCollider(results[i].transform)) { results[kept++] = results[i]; }
            }
            return kept;
        }

        /// <summary>
        /// The point on a collider nearest to point, at the collider's physics pose. Unity can only find that for boxes,
        /// spheres, capsules and convex meshes; for other colliders (a concave mesh, terrain), the point a ray from point
        /// along hint hits within reach is used instead. False if there's none (or point is inside the collider).
        /// </summary>
        public static bool ClosestPointOn(Collider collider, Vector3 point, Vector3 hint, float reach, out Vector3 closest)
        {
            closest = point;
            if (collider is BoxCollider || collider is SphereCollider || collider is CapsuleCollider || collider is MeshCollider { convex: true })
            {
                GetColliderPose(collider, out Vector3 position, out Quaternion rotation);
                closest = Physics.ClosestPoint(point, collider, position, rotation);
                return (closest - point).sqrMagnitude > 1e-10f;
            }
            if (hint.sqrMagnitude < 1e-8f || !collider.Raycast(new Ray(point, hint.normalized), out RaycastHit hit, reach)) { return false; }
            closest = hit.point;
            return true;
        }

        /// <summary>
        /// An object's physics pose (its Rigidbody's, if it has one, rather than its possibly interpolated transform), for
        /// following things that move, such as a ledge on a moving platform.
        /// </summary>
        public static void GetPhysicsPose(Transform target, out Vector3 position, out Quaternion rotation) =>
            GetGroundPose(target, out position, out rotation);
    }
}
