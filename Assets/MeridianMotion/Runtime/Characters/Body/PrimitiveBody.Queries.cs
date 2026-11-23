using System.Collections.Generic;
using System.Globalization;
using MeridianMotion.PhysicsQueries;
using UnityEngine;

namespace MeridianMotion.Characters
{
    // The body's queries: whether it fits, sweeping it, and pushing it out of what it overlaps.
    public sealed partial class PrimitiveBody
    {
        public override bool Fits(Vector3 feet, Quaternion rotation, Vector3 scale, BodyPosture posture)
        {
            RememberAuthoredSize();
            return Fits(feet, rotation, scale, posture, authored);
        }

        /// <summary>
        /// Whether a body of the given authored shape (of any kind) would fit with its feet at feet, turned rotation, at the
        /// given scale and posture.
        /// </summary>
        public bool FitsAs(BodyShape authoredShape, Vector3 feet, Quaternion rotation, Vector3 scale, BodyPosture posture) =>
            Fits(feet, rotation, scale, posture, authoredShape);

        /// <summary>The same, for a body of another authored shape.</summary>
        private bool Fits(Vector3 feet, Quaternion rotation, Vector3 scale, BodyPosture posture, BodyShape authoredShape)
        {
            float lift = Owner.BodyLiftAt(scale, posture);
            int extraCount = PartsAt(authoredShape, scale, posture, scratchExtras, out BodyShape main);
            BodyShape s = Lifted(main, lift);
            float authoredFootprint = Footprint(authoredShape);
            float skinAtScale = Owner.AuthoredSkinWidth * (authoredFootprint > 0f ? Mathf.Min(scale.y, Footprint(s) / authoredFootprint) : scale.y);
            if (!PartFits(s, feet, rotation, skinAtScale)) { return false; }
            for (int i = 0; i < extraCount; i++)
            {
                if (!PartFits(Lifted(scratchExtras[i], lift), feet, rotation, skinAtScale)) { return false; }
            }
            return true;
        }

        /// <summary>Whether a part (shrunk by skin), its feet at feet and turned rotation, overlaps nothing but the character's own.</summary>
        private bool PartFits(BodyShape s, Vector3 feet, Quaternion rotation, float skin)
        {
            ColliderBuffer overlaps = Owner.OverlapBuffer;
            int count = Overlap(s, feet, rotation, skin, overlaps);
            for (int i = 0; i < count; i++)
            {
                if (!Owner.IsOwnCollider(overlaps[i].transform)) { return false; }
            }
            return true;
        }

        /// <summary>The body's part (0 its main shape, then its extra parts) now, lifted (see Lifted); count says how many it has.</summary>
        private BodyShape LiftedPart(int part, float lift) => Lifted(part == 0 ? Current : currentExtras[part - 1], lift);

        /// <summary>How many parts the body has now: its main shape and its extra parts.</summary>
        private int PartCount { get { _ = Current; return 1 + currentExtraCount; } }

        /// <summary>Shape-sweep from a feet-anchored position in the given direction, returning the closest non-self hit.</summary>
        public override bool Sweep(Vector3 feet, Quaternion rotation, Vector3 direction, float distance, out RaycastHit closest)
        {
            closest = default;
            if (direction.sqrMagnitude < 1e-8f || distance <= 0f) { return false; }

            Vector3 dir = direction.normalized;
            float lift = Owner.BodyLift;
            float skin = Owner.SkinWidth;
            RaycastHitBuffer castHits = Owner.CastBuffer;
            float min = float.PositiveInfinity;
            bool found = false;
            for (int part = 0, parts = PartCount; part < parts; part++)
            {
                int count = Cast(LiftedPart(part, lift), feet, rotation, skin, dir, distance, castHits);
                found |= Closest(castHits, count, ref min, ref closest);
            }
            return found;
        }

        /// <summary>The nearest of count hits that isn't the character's own (or started overlapping), if nearer than min.</summary>
        private bool Closest(RaycastHitBuffer hits, int count, ref float min, ref RaycastHit closest)
        {
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                RaycastHit h = hits[i];
                if (Owner.IsOwnCollider(h.collider.transform)) { continue; }
                // hit.distance of 0 means we started already overlapping — skip so we can slide out instead of locking up.
                if (h.distance <= 0f) { continue; }
                // The nearest; of two as near, the first in the stable order (see StableOrder).
                if (StableOrder.Better(h.distance, min, h, closest)) { min = h.distance; closest = h; found = true; }
            }
            return found;
        }

        public override Vector3 Depenetrate(Vector3 feet, Quaternion rotation)
        {
            float lift = Owner.BodyLift;
            float skin = Owner.SkinWidth;
            ColliderBuffer overlaps = Owner.OverlapBuffer;
            Vector3 offset = Vector3.zero;
            int parts = PartCount;
            for (int pass = 0; pass < MaxDepenetrationPasses; pass++)
            {
                bool pushed = false;
                for (int part = 0; part < parts; part++) { pushed |= PushOut(LiftedPart(part, lift), part, feet, rotation, skin, overlaps, ref offset); }
                if (!pushed) { break; }
            }
            return offset;
        }

        /// <summary>
        /// Pushes a part (the given one of the body's) out of what it overlaps, its feet at feet + offset, adding to offset.
        /// ComputePenetration needs a collider, and one that's turned on: a private one of the part's shape, turned on only
        /// while it's used (only in a step the part overlaps something), so nothing else ever meets it.
        /// </summary>
        private bool PushOut(BodyShape s, int part, Vector3 feet, Quaternion rotation, float skin, ColliderBuffer overlaps, ref Vector3 offset)
        {
            int count = Overlap(s, feet + offset, rotation, skin, overlaps);
            // Pushed out of each in turn, so the order changes where it ends up: the stable order, not the order they came in.
            if (count > 1) { StableOrder.Sort(overlaps, count); }
            bool pushed = false;
            Collider query = null;
            try
            {
                for (int i = 0; i < count; i++)
                {
                    Collider other = overlaps[i];
                    if (Owner.IsOwnCollider(other.transform)) { continue; }
                    // A loose physics object (a non-kinematic Rigidbody: a ball, say) meets the character's hitboxes, and
                    // never pushes the character. Resting on a fox's back, a ball sits inside its body (its hitboxes are
                    // lower than the head-high body), and being pushed out of it every step sank the fox through the floor.
                    Rigidbody otherBody = other.attachedRigidbody;
                    if (otherBody != null && !otherBody.isKinematic) { continue; }
                    if (query == null)
                    {
                        query = Owner.QueryCollider(s, part);
                        query.enabled = true;
                    }
                    MMotionMotor.GetColliderPose(other, out Vector3 otherPosition, out Quaternion otherRotation);
                    if (Physics.ComputePenetration(query, feet + offset, rotation, other, otherPosition, otherRotation,
                            out Vector3 direction, out float distance))
                    {
                        offset += direction * distance;
                        pushed = true;
                    }
                }
            }
            finally
            {
                if (query != null) { query.enabled = false; }
            }
            return pushed;
        }

        /// <summary>The colliders the shape (shrunk by skin), its feet at feet and turned rotation, overlaps, into overlaps.</summary>
        private int Overlap(BodyShape s, Vector3 feet, Quaternion rotation, float skin, ColliderBuffer overlaps)
        {
            int mask = Owner.MovementLayerMask;
            switch (s.kind)
            {
                case BodyShapeKind.Box:
                    GetQueryBox(s, feet, rotation, skin, out Vector3 boxCenter, out Vector3 halfExtents);
                    return overlaps.OverlapBox(boxCenter, halfExtents, rotation, mask, QueryTriggerInteraction.Ignore);
                case BodyShapeKind.Sphere:
                    GetQuerySphere(s, feet, rotation, skin, out Vector3 sphereCenter, out float sphereRadius);
                    return overlaps.OverlapSphere(sphereCenter, sphereRadius, mask, QueryTriggerInteraction.Ignore);
                default:
                    GetQueryCapsule(s, feet, rotation, skin, out Vector3 top, out Vector3 bottom, out float radius);
                    return overlaps.OverlapCapsule(top, bottom, radius, mask, QueryTriggerInteraction.Ignore);
            }
        }

        /// <summary>The shape (shrunk by skin), its feet at feet and turned rotation, cast along direction, into hits.</summary>
        private int Cast(BodyShape s, Vector3 feet, Quaternion rotation, float skin, Vector3 direction, float distance, RaycastHitBuffer hits)
        {
            int mask = Owner.MovementLayerMask;
            switch (s.kind)
            {
                case BodyShapeKind.Box:
                    GetQueryBox(s, feet, rotation, skin, out Vector3 boxCenter, out Vector3 halfExtents);
                    return hits.BoxCast(boxCenter, halfExtents, direction, rotation, distance, mask, QueryTriggerInteraction.Ignore);
                case BodyShapeKind.Sphere:
                    GetQuerySphere(s, feet, rotation, skin, out Vector3 sphereCenter, out float sphereRadius);
                    return hits.SphereCast(sphereCenter, sphereRadius, direction, distance, mask, QueryTriggerInteraction.Ignore);
                default:
                    GetQueryCapsule(s, feet, rotation, skin, out Vector3 top, out Vector3 bottom, out float radius);
                    return hits.CapsuleCast(top, bottom, radius, direction, distance, mask, QueryTriggerInteraction.Ignore);
            }
        }

        /// <summary>
        /// Where the shape's centre is, its feet at feet and turned rotation (a centred shape turned only about the vertical
        /// needn't turn).
        /// </summary>
        private static Vector3 WorldCenter(BodyShape s, Vector3 feet, Quaternion rotation) =>
            s.center.x == 0f && s.center.z == 0f && IsLevel(rotation) ? feet + s.center : feet + rotation * s.center;

        // Below this, a rotation's x and z parts are rounding: it's a turn about the vertical alone (a facing, unpitched).
        private const float LevelTolerance = 1e-6f;

        /// <summary>Whether a rotation only turns about the vertical (a facing, not pitched or tilted).</summary>
        private static bool IsLevel(Quaternion rotation) => Mathf.Abs(rotation.x) < LevelTolerance && Mathf.Abs(rotation.z) < LevelTolerance;

        /// <summary>
        /// The capsule used for collision queries, placed at the given feet position and turned rotation: the shape shrunk
        /// by the skin width (its radius, so its ends too).
        /// </summary>
        private static void GetQueryCapsule(BodyShape s, Vector3 feetPos, Quaternion rotation, float skin, out Vector3 top, out Vector3 bottom, out float radius)
        {
            radius = s.radius - skin;
            if (radius <= 0f) { radius = s.radius * 0.5f; }
            float height = Mathf.Max(s.height, s.radius * 2f);
            float halfSegment = Mathf.Max(0f, (height * 0.5f) - radius - skin);
            Vector3 center = WorldCenter(s, feetPos, rotation);
            // Upright and level, straight up: an upright capsule turned about its axis is the same capsule.
            Vector3 axis = s.direction == 1 && IsLevel(rotation) ? Vector3.up
                : rotation * (s.direction == 0 ? Vector3.right : s.direction == 1 ? Vector3.up : Vector3.forward);
            bottom = center - axis * halfSegment;
            top = center + axis * halfSegment;
        }

        private static void GetQueryBox(BodyShape s, Vector3 feetPos, Quaternion rotation, float skin, out Vector3 center, out Vector3 halfExtents)
        {
            center = feetPos + rotation * s.center;
            Vector3 half = 0.5f * s.size;
            halfExtents = new Vector3(Shrunk(half.x, skin), Shrunk(half.y, skin), Shrunk(half.z, skin));
        }

        private static void GetQuerySphere(BodyShape s, Vector3 feetPos, Quaternion rotation, float skin, out Vector3 center, out float radius)
        {
            center = WorldCenter(s, feetPos, rotation);
            radius = Shrunk(s.radius, skin);
        }

        private static float Shrunk(float half, float skin) => half - skin > 0f ? half - skin : half * 0.5f;
    }
}
