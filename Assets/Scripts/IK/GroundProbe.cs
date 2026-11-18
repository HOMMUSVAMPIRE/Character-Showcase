using PhysicsQueries;
using UnityEngine;

namespace Character
{
    /// <summary>
    /// The ray foot and paw placement (FootPlacementIK, PawPlacementIK) find the ground with: straight down, the closest
    /// hit that's on the given layers, not the character's own (under the ignored root) and no steeper than the slope
    /// limit, moved to where the ground is drawn (see CharacterUtilities.ToDrawnPose). Each component keeps its own
    /// settings and gives them here; the probe holds only the hit buffer, so it allocates nothing per frame (unless a query fills it: see QueryBuffer).
    /// </summary>
    public sealed class GroundProbe
    {
        private readonly RaycastHitBuffer hits = new(8, "GroundProbe.hits");

        /// <summary>
        /// The colliders a placement ignores when it isn't told which: the character's own, under the nearest parent with
        /// a Rigidbody (the character root), or under the model's root if there's none.
        /// </summary>
        public static Transform DefaultIgnoredRoot(Component placement)
        {
            Rigidbody body = placement.GetComponentInParent<Rigidbody>();
            return body != null ? body.transform : placement.transform.root;
        }

        /// <summary>
        /// Casts down from origin, up to distance, and gives the closest ground hit (see the class summary). Returns false
        /// if there's none.
        /// </summary>
        public bool Find(Vector3 origin, float distance, LayerMask layers, Transform ignoreCollidersUnder, float maxSlopeAngle,
            out RaycastHit closest)
        {
            closest = default;
            int count = hits.Raycast(origin, Vector3.down, distance, layers, QueryTriggerInteraction.Ignore);
            float best = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                RaycastHit h = hits[i];
                if (ignoreCollidersUnder != null && h.transform.IsChildOf(ignoreCollidersUnder)) { continue; }
                if (Vector3.Angle(h.normal, Vector3.up) > maxSlopeAngle) { continue; }
                if (h.distance < best) { best = h.distance; closest = h; }
            }
            if (best == float.PositiveInfinity) { return false; }
            // Where the ground is drawn: placed where physics has a moving platform, feet and paws jumped up and down on it.
            CharacterUtilities.ToDrawnPose(ref closest);
            return true;
        }
    }
}
