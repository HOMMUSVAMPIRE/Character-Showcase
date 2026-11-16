using System.Globalization;
using UnityEngine;

namespace Character
{
    /// <summary>
    /// An upright capsule body (see CharacterBody): a CapsuleCollider standing on the character's feet, its height scaling
    /// with the character's height and its posture (lowered from the top, the feet staying put), its radius with the
    /// larger of its width and depth. Its queries use the capsule shrunk by the skin width, its bottom raised by it.
    /// </summary>
    public sealed class CapsuleBody : CharacterBody
    {
        // Passes made to push the body out of geometry that moved into it, for when several things overlap.
        private const int MaxDepenetrationPasses = 4;

        private CapsuleCollider capsule;
        // The capsule as authored, which the character's size scales.
        private float baseRadius, baseHeight;
        private Vector3 baseCenter;
        private bool hasBaseCapsule;

        /// <summary>A capsule body for the owner, of the given collider (or, if null, the owner's CapsuleCollider, once it has one).</summary>
        public CapsuleBody(MovementController owner, CapsuleCollider collider) : base(owner) => capsule = collider;

        public override Collider Collider => capsule;
        public override bool HasAuthoredSize => hasBaseCapsule;
        public override float AuthoredHeight { get { RememberAuthoredSize(); return baseHeight; } }
        public override float AuthoredRadius { get { RememberAuthoredSize(); return baseRadius; } }
        public override float Radius => capsule.radius;
        public override float Height => capsule.height;

        // A capsule is round, so it reaches as far every way: its radius (or, before it has a collider, half a metre at
        // the character's height).
        public override float FrontExtent => capsule != null ? capsule.radius : 0.5f * Owner.HeightScale;
        public override float BackExtent => FrontExtent;
        public override float SideExtent => FrontExtent;

        public override void RememberAuthoredSize()
        {
            // Also before Awake (a prefab asset asked about, say), so the capsule is fetched here too.
            if (capsule == null) { capsule = Owner.GetComponent<CapsuleCollider>(); }
            if (hasBaseCapsule || capsule == null) { return; }
            baseRadius = capsule.radius;
            baseHeight = capsule.height;
            baseCenter = capsule.center;
            hasBaseCapsule = true;
        }

        /// <summary>
        /// Resizes the capsule to the given size: its height (and center) scale with y, and its radius with the larger of
        /// x and z, since a capsule is round however the character is stretched. (The radius is also capped at half the
        /// height; see CapsuleAt. A crouched character's capsule is lower too: the share of its standing height it has.)
        /// Doesn't check for room: see Fits.
        /// </summary>
        public override void Resize(Vector3 scale, float heightFraction)
        {
            RememberAuthoredSize();
            if (capsule == null) { return; }
            CapsuleAt(scale, heightFraction, baseRadius, baseHeight, baseCenter, out float radius, out float height, out Vector3 center);
            capsule.radius = radius;
            capsule.height = height;
            capsule.center = center;
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

        /// <summary>
        /// Changes the capsule's size as authored (the character's scale then multiplies it), standing on the feet: the
        /// bottom stays where it is, and its center rises to half the height. Used to fit the collider to the character's
        /// model (see CharacterModelHost). Like growing, it waits for room: if the bigger capsule wouldn't fit with its
        /// feet at feet (at the given scale and share of its standing height), nothing changes and this returns false.
        /// Doesn't resize the collider: the owner does (see MovementController.SetBaseCapsule).
        /// </summary>
        public bool SetAuthoredSize(float radius, float height, Vector3 feet, Vector3 scale, float heightFraction)
        {
            RememberAuthoredSize();
            if (capsule == null || radius <= 0f || height <= 0f) { return false; }
            radius = Mathf.Min(radius, height * 0.5f);
            var center = new Vector3(baseCenter.x, height * 0.5f, baseCenter.z);
            bool grows = height > baseHeight + 1e-5f || radius > baseRadius + 1e-5f;
            if (grows && !Fits(feet, scale, heightFraction, radius, height, center)) { return false; }
            baseRadius = radius;
            baseHeight = height;
            baseCenter = center;
            return true;
        }

        public override CharacterBody CopyFor(MovementController copy, float? authoredRadius = null, float? authoredHeight = null)
        {
            var body = new CapsuleBody(copy, copy.gameObject.AddComponent<CapsuleCollider>());
            body.CopyAuthoredSize(this, authoredRadius, authoredHeight);
            return body;
        }

        /// <summary>
        /// Takes source's size as authored (or, given an authored radius and height, that capsule standing on its feet), for
        /// a copy of a character.
        /// </summary>
        private void CopyAuthoredSize(CapsuleBody source, float? authoredRadius, float? authoredHeight)
        {
            source.RememberAuthoredSize();
            baseRadius = source.baseRadius;
            baseHeight = source.baseHeight;
            baseCenter = source.baseCenter;
            if (authoredRadius.HasValue && authoredHeight.HasValue && authoredHeight.Value > 0f)
            {
                baseHeight = authoredHeight.Value;
                baseRadius = Mathf.Min(authoredRadius.Value, baseHeight * 0.5f);
                baseCenter = new Vector3(baseCenter.x, baseHeight * 0.5f, baseCenter.z);
            }
            hasBaseCapsule = true;
        }

        public override bool Fits(Vector3 feet, Vector3 scale, float heightFraction)
        {
            RememberAuthoredSize();
            return Fits(feet, scale, heightFraction, baseRadius, baseHeight, baseCenter);
        }

        /// <summary>The same, for a capsule of another authored size.</summary>
        private bool Fits(Vector3 feet, Vector3 scale, float fraction, float authoredRadius, float authoredHeight, Vector3 authoredCenter)
        {
            if (capsule == null) { return true; }
            CapsuleAt(scale, fraction, authoredRadius, authoredHeight, authoredCenter, out float radiusAtScale, out float heightAtScale, out Vector3 centerAtScale);
            float skinAtScale = Owner.AuthoredSkinWidth * (authoredRadius > 0f ? Mathf.Min(scale.y, radiusAtScale / authoredRadius) : scale.y);
            GetSweepCapsule(feet, radiusAtScale, heightAtScale, centerAtScale, skinAtScale,
                out Vector3 top, out Vector3 bottom, out float radius);
            Collider[] overlaps = Owner.OverlapBuffer;
            int count = Physics.OverlapCapsuleNonAlloc(top, bottom, radius, overlaps, Owner.MovementLayerMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                if (!Owner.IsOwnCollider(overlaps[i].transform)) { return false; }
            }
            return true;
        }

        /// <summary>
        /// Capsule-sweep from a feet-anchored position in the given direction, returning the closest non-self hit.
        /// </summary>
        public override bool Sweep(Vector3 feet, Vector3 direction, float distance, out RaycastHit closest)
        {
            closest = default;
            if (direction.sqrMagnitude < 1e-8f || distance <= 0f) { return false; }

            GetSweepCapsule(feet, out Vector3 top, out Vector3 bot, out float radius);
            RaycastHit[] castHits = Owner.CastBuffer;
            int count = Physics.CapsuleCastNonAlloc(top, bot, radius, direction.normalized, castHits, distance, Owner.MovementLayerMask, QueryTriggerInteraction.Ignore);
            float min = float.PositiveInfinity;
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                RaycastHit h = castHits[i];
                if (Owner.IsOwnCollider(h.collider.transform)) { continue; }
                // hit.distance of 0 means we started already overlapping — skip so we can slide out instead of locking up.
                if (h.distance <= 0f) { continue; }
                if (h.distance < min) { min = h.distance; closest = h; found = true; }
            }
            return found;
        }

        public override Vector3 Depenetrate(Vector3 feet, Quaternion rotation)
        {
            if (capsule == null) { return Vector3.zero; }

            Collider[] overlaps = Owner.OverlapBuffer;
            Vector3 offset = Vector3.zero;
            for (int pass = 0; pass < MaxDepenetrationPasses; pass++)
            {
                GetSweepCapsule(feet + offset, out Vector3 top, out Vector3 bottom, out float radius);
                int count = Physics.OverlapCapsuleNonAlloc(top, bottom, radius, overlaps, Owner.MovementLayerMask, QueryTriggerInteraction.Ignore);
                bool pushed = false;
                for (int i = 0; i < count; i++)
                {
                    Collider other = overlaps[i];
                    if (Owner.IsOwnCollider(other.transform)) { continue; }
                    MovementController.GetColliderPose(other, out Vector3 otherPosition, out Quaternion otherRotation);
                    if (Physics.ComputePenetration(capsule, feet + offset, rotation, other, otherPosition, otherRotation,
                            out Vector3 direction, out float distance))
                    {
                        offset += direction * distance;
                        pushed = true;
                    }
                }
                if (!pushed) { break; }
            }
            return offset;
        }

        /// <summary>
        /// The capsule used for collision queries, placed at the given feet position: the character's capsule shrunk by
        /// the skin width, with its bottom raised by the skin width.
        /// </summary>
        private void GetSweepCapsule(Vector3 feetPos, out Vector3 top, out Vector3 bottom, out float radius)
        {
            if (capsule != null) { GetSweepCapsule(feetPos, capsule.radius, capsule.height, capsule.center, Owner.SkinWidth, out top, out bottom, out radius); }
            else { GetSweepCapsule(feetPos, 0.5f, 2f, new Vector3(0f, 1f, 0f), Owner.SkinWidth, out top, out bottom, out radius); }
        }

        /// <summary>The same, for a capsule of the given size and skin width (one the character might be resized to).</summary>
        private static void GetSweepCapsule(Vector3 feetPos, float capsuleRadius, float capsuleHeight, Vector3 localCenter,
            float skin, out Vector3 top, out Vector3 bottom, out float radius)
        {
            radius = capsuleRadius - skin;
            if (radius <= 0f) { radius = capsuleRadius * 0.5f; }
            float height = Mathf.Max(capsuleHeight, capsuleRadius * 2f);
            float halfSegment = Mathf.Max(0f, (height * 0.5f) - radius - skin);
            Vector3 center = feetPos + localCenter;
            bottom = center - Vector3.up * halfSegment;
            top = center + Vector3.up * halfSegment;
        }

        public override string CapabilitiesKey()
        {
            RememberAuthoredSize();
            CultureInfo invariant = CultureInfo.InvariantCulture;
            string F(float value) => value.ToString("R", invariant);
            return string.Join("|", F(baseRadius), F(baseHeight), $"{F(baseCenter.x)},{F(baseCenter.y)},{F(baseCenter.z)}");
        }
    }
}
