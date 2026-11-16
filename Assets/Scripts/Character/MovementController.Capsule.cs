using UnityEngine;

namespace Character
{
    // The character's collider: its size (ApplyScale), its height while crouched (SetHeightFraction), and whether it fits somewhere.
    public partial class MovementController
    {
        private void RememberBaseCapsule()
        {
            // Also before Awake (a prefab asset asked about, say), so the capsule is fetched here too.
            if (capsule == null) { capsule = GetComponent<CapsuleCollider>(); }
            if (hasBaseCapsule || capsule == null) { return; }
            baseRadius = capsule.radius;
            baseHeight = capsule.height;
            baseCenter = capsule.center;
            hasBaseCapsule = true;
        }

        /// <summary>
        /// Resizes the character's collision to the given size: the capsule's height (and center) scale with y, and its
        /// radius with the larger of x and z, since a capsule is round however the character is stretched. Collision
        /// sweeps, ground checks and depenetration all use the capsule, so they follow. (The radius is also capped at half
        /// the height; see CapsuleAt. A crouched character's capsule is lower too: see SetHeightFraction.) Doesn't check for room: see
        /// CanFit. Called by ACCharacterController.SetScale.
        /// </summary>
        public void ApplyScale(Vector3 scale)
        {
            Scale = scale;
            RememberBaseCapsule();
            if (capsule == null) { return; }
            CapsuleAt(scale, heightFraction, baseRadius, baseHeight, baseCenter, out float radius, out float height, out Vector3 center);
            capsule.radius = radius;
            capsule.height = height;
            capsule.center = center;
        }

        // The share of its standing height the character's collider has: below 1 while crouched (see SetHeightFraction).
        private float heightFraction = 1f;

        /// <summary>The share of its standing height the character's collider has now (1 standing; less crouched).</summary>
        public float HeightFraction => heightFraction;

        /// <summary>
        /// Lowers or raises the top of the character's collider, keeping its feet where they are: to the given share of
        /// its standing height (crouching, say). Lowering always works; raising only where there's room above the
        /// character standing at feet (its physics position this step), and otherwise nothing changes and this returns
        /// false. The share stays through changes of size.
        /// </summary>
        public bool SetHeightFraction(float fraction, Vector3 feet)
        {
            fraction = Mathf.Clamp(fraction, 0.1f, 1f);
            if (Mathf.Abs(fraction - heightFraction) < 1e-5f) { return true; }
            if (fraction > heightFraction && !CanFitAt(feet, fraction)) { return false; }
            heightFraction = fraction;
            ApplyScale(Scale);
            return true;
        }

        /// <summary>
        /// The capsule at a size and share of its standing height. Its height scales with both. Its radius scales with the
        /// larger of the width and depth scales, since a capsule is round however the character is stretched, but is no
        /// more than half its height, so a wide, flat (or crouched) character's collider doesn't become a sphere taller
        /// than the character. Its bottom stays at the feet, so a lower capsule is lowered from the top.
        /// </summary>
        private static void CapsuleAt(Vector3 scale, float fraction, float authoredRadius, float authoredHeight, Vector3 authoredCenter,
            out float radius, out float height, out Vector3 center)
        {
            height = authoredHeight * scale.y * fraction;
            radius = Mathf.Min(authoredRadius * Mathf.Max(scale.x, scale.z), height * 0.5f);
            center = Vector3.Scale(authoredCenter, scale);
            float bottom = (authoredCenter.y - authoredHeight * 0.5f) * scale.y;
            center.y = bottom + height * 0.5f;
        }

        /// <summary>The capsule's radius and height at the character's authored size (1, 1, 1), which its scale multiplies.</summary>
        /// <summary>The tallest step or ledge the character steps up by itself at its authored size (see StepHeight for its current size).</summary>
        public float MaxStepHeight => maxStepHeight;
        /// <summary>The steepest slope, in degrees, the character treats as walkable ground.</summary>
        public float MaxSlopeAngle => maxSlopeAngle;
        public float BaseRadius { get { RememberBaseCapsule(); return baseRadius; } }
        public float BaseHeight { get { RememberBaseCapsule(); return baseHeight; } }

        /// <summary>
        /// Changes the capsule's size at the character's authored size (its scale then multiplies it), standing on the
        /// character's feet: the bottom stays where it is, and its center rises to half the height. Used to fit the
        /// collider to the character's model (see CharacterModelHost). Like growing, it waits for room: if the bigger
        /// capsule wouldn't fit where the character stands, nothing changes and this returns false.
        /// </summary>
        public bool SetBaseCapsule(float radius, float height)
        {
            RememberBaseCapsule();
            if (capsule == null || radius <= 0f || height <= 0f) { return false; }
            radius = Mathf.Min(radius, height * 0.5f);
            var center = new Vector3(baseCenter.x, height * 0.5f, baseCenter.z);
            bool grows = height > baseHeight + 1e-5f || radius > baseRadius + 1e-5f;
            if (grows && !CanFit(rb != null ? rb.position : transform.position, Scale, heightFraction, radius, height, center)) { return false; }
            baseRadius = radius;
            baseHeight = height;
            baseCenter = center;
            ApplyScale(Scale);
            return true;
        }

        /// <summary>
        /// Whether the character would fit where it stands at the given size, without overlapping the world (more than
        /// the skin width, so the floor it stands on doesn't count).
        /// </summary>
        public bool CanFit(Vector3 scale)
        {
            RememberBaseCapsule();
            return CanFit(rb != null ? rb.position : transform.position, scale, heightFraction, baseRadius, baseHeight, baseCenter);
        }

        /// <summary>
        /// Whether the character would fit with its feet at the given position, at its size and the given share of its
        /// standing height (1 standing; see SetHeightFraction), without overlapping the world.
        /// </summary>
        public bool CanFitAt(Vector3 feet, float fraction = 1f)
        {
            RememberBaseCapsule();
            return CanFit(feet, Scale, Mathf.Clamp(fraction, 0.1f, 1f), baseRadius, baseHeight, baseCenter);
        }

        /// <summary>The same, for a capsule of another authored size.</summary>
        private bool CanFit(Vector3 feet, Vector3 scale, float fraction, float authoredRadius, float authoredHeight, Vector3 authoredCenter)
        {
            if (capsule == null) { return true; }
            CapsuleAt(scale, fraction, authoredRadius, authoredHeight, authoredCenter, out float radiusAtScale, out float heightAtScale, out Vector3 centerAtScale);
            float skinAtScale = skinWidth * (authoredRadius > 0f ? Mathf.Min(scale.y, radiusAtScale / authoredRadius) : scale.y);
            GetSweepCapsule(feet, radiusAtScale, heightAtScale, centerAtScale, skinAtScale,
                out Vector3 top, out Vector3 bottom, out float radius);
            int count = Physics.OverlapCapsuleNonAlloc(top, bottom, radius, overlaps, movementLayerMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                if (!IsOwnCollider(overlaps[i].transform)) { return false; }
            }
            return true;
        }
    }
}
