using System.Globalization;
using UnityEngine;

namespace Character
{
    /// <summary>
    /// A body (see CharacterBody) that is one of Unity's primitive colliders on the character's object: a capsule (upright
    /// or lying along an axis), a box or a sphere, centred wherever it's authored. Its size follows the character's (a
    /// capsule's length along its axis with that axis's scale, its radius with the larger of the others, but no more than
    /// half its height; a box's sides each with theirs; a sphere with the largest). Its postures are its authored shape
    /// lowered from the top (low) or laid down along the facing, resting on the feet (lying), no thicker than its lying
    /// thickness (see BodyShape). Its queries use the shape shrunk by the skin width on every side. Its shape as authored
    /// is its collider's, or the one it's given (see MovementController.SetBody).
    /// </summary>
    public sealed class PrimitiveBody : CharacterBody
    {
        // Passes made to push the body out of geometry that moved into it, for when several things overlap.
        private const int MaxDepenetrationPasses = 4;

        private Collider collider;
        // The shape as authored, which the character's size and posture change.
        private BodyShape authored;
        private bool hasAuthored;

        /// <summary>
        /// A body for the owner, of the given primitive collider (or, if null, the owner's first solid capsule, box or
        /// sphere collider, once it has one).
        /// </summary>
        public PrimitiveBody(MovementController owner, Collider primitive) : base(owner) => collider = IsPrimitive(primitive) ? primitive : null;

        private static bool IsPrimitive(Collider c) => c is CapsuleCollider || c is BoxCollider || c is SphereCollider;

        public override Collider Collider => collider;
        public override bool HasAuthoredSize => hasAuthored;

        /// <summary>The body's shape as authored (at the character's authored size, standing): its collider's, or the one it was given.</summary>
        public BodyShape AuthoredShape { get { RememberAuthoredSize(); return authored; } }
        public override float AuthoredHeight { get { RememberAuthoredSize(); return UprightHeight(authored); } }
        public override float AuthoredRadius { get { RememberAuthoredSize(); return Footprint(authored); } }
        public override float FootprintRadius => Footprint(Current);
        public override float Height
        {
            get
            {
                BodyShape s = Current;
                return s.kind switch
                {
                    BodyShapeKind.Box => s.size.y,
                    BodyShapeKind.Capsule when s.direction == 1 => s.height,
                    _ => 2f * s.radius,
                };
            }
        }

        public override bool TurnsWithFacing
        {
            get
            {
                if (collider == null) { return false; }
                BodyShape s = Current;
                bool offCentre = s.center.x != 0f || s.center.z != 0f;
                return s.kind switch
                {
                    BodyShapeKind.Capsule => s.direction != 1 || offCentre,
                    BodyShapeKind.Sphere => offCentre,
                    _ => true,
                };
            }
        }

        // How far the shape reaches ahead of, behind and beside the feet, in the character's frame. A round shape on the
        // feet reaches its radius every way (or, before it has a collider, half a metre at the character's height).
        public override float FrontExtent => collider != null ? Current.center.z + HalfExtents(Current).z : 0.5f * Owner.HeightScale;
        public override float BackExtent => collider != null ? HalfExtents(Current).z - Current.center.z : 0.5f * Owner.HeightScale;
        public override float SideExtent => collider != null ? Mathf.Abs(Current.center.x) + HalfExtents(Current).x : 0.5f * Owner.HeightScale;

        /// <summary>The collider's shape as it is now.</summary>
        private BodyShape Current => Read(collider);

        public override void RememberAuthoredSize()
        {
            // Also before Awake (a prefab asset asked about, say), so the collider is fetched here too.
            if (collider == null) { collider = FindPrimitive(Owner); }
            if (hasAuthored || collider == null) { return; }
            authored = Read(collider);
            hasAuthored = true;
        }

        /// <summary>The owner's first solid primitive collider on its own object (a capsule, then a box, then a sphere).</summary>
        private static Collider FindPrimitive(MovementController owner)
        {
            foreach (Collider c in owner.GetComponents<Collider>())
            {
                if (IsPrimitive(c) && !c.isTrigger) { return c; }
            }
            return null;
        }

        public override void Resize(Vector3 scale, BodyPosture posture)
        {
            RememberAuthoredSize();
            if (collider == null) { return; }
            Write(ShapeAt(authored, scale, posture));
        }

        // ---- Shapes ----

        // No collider yet: the default upright capsule, 2 m tall, standing on the feet.
        private static BodyShape Read(Collider c) => BodyShape.TryRead(c, out BodyShape shape) ? shape : BodyShape.Capsule(0.5f, 2f);

        private void Write(BodyShape s)
        {
            switch (collider)
            {
                case CapsuleCollider capsule:
                    if (capsule.direction != s.direction) { capsule.direction = s.direction; }
                    capsule.radius = s.radius;
                    capsule.height = s.height;
                    capsule.center = s.center;
                    break;
                case BoxCollider box:
                    box.size = s.size;
                    box.center = s.center;
                    break;
                case SphereCollider sphere:
                    sphere.radius = s.radius;
                    sphere.center = s.center;
                    break;
            }
        }

        /// <summary>How tall the shape stands, top to bottom.</summary>
        private static float UprightHeight(BodyShape s) => s.kind switch
        {
            BodyShapeKind.Box => s.size.y,
            BodyShapeKind.Capsule when s.direction == 1 => s.height,
            _ => 2f * s.radius,
        };

        /// <summary>The radius of the shape's footprint: a capsule's or sphere's radius, half a box's narrower side.</summary>
        private static float Footprint(BodyShape s) => s.kind == BodyShapeKind.Box ? 0.5f * Mathf.Min(s.size.x, s.size.z) : s.radius;

        /// <summary>Half the shape's size along each of the character's axes, about its centre.</summary>
        private static Vector3 HalfExtents(BodyShape s)
        {
            switch (s.kind)
            {
                case BodyShapeKind.Box: return 0.5f * s.size;
                case BodyShapeKind.Sphere: return new Vector3(s.radius, s.radius, s.radius);
                default:
                    float half = 0.5f * Mathf.Max(s.height, 2f * s.radius);
                    return new Vector3(s.direction == 0 ? half : s.radius, s.direction == 1 ? half : s.radius, s.direction == 2 ? half : s.radius);
            }
        }

        /// <summary>The shape at a size and posture: upright (lowered from the top while low), or laid down along the facing.</summary>
        private static BodyShape ShapeAt(BodyShape authored, Vector3 scale, BodyPosture posture)
        {
            BodyShape s = Upright(authored, scale, posture.Kind == BodyPostureKind.Low ? posture.Share : 1f);
            // Its lying thickness scales as its height does, which it is, lying.
            return posture.Kind == BodyPostureKind.Lying ? LaidDown(s, authored.lyingThickness * scale.y) : s;
        }

        /// <summary>
        /// The authored shape at a size and share of its standing height, its bottom staying at the feet's height (so a
        /// lower shape is lowered from the top).
        /// </summary>
        private static BodyShape Upright(BodyShape a, Vector3 scale, float fraction)
        {
            BodyShape s = a;
            switch (a.kind)
            {
                case BodyShapeKind.Capsule when a.direction == 1:
                    CapsuleAt(scale, fraction, a.radius, a.height, a.center, out s.radius, out s.height, out s.center);
                    break;
                case BodyShapeKind.Capsule:
                {
                    // Lying along X or Z as authored: its length scales with that axis, its radius with the larger of the
                    // others (no more than half its lowered height).
                    float across = a.direction == 0 ? scale.z : scale.x;
                    float radius = a.radius * Mathf.Max(scale.y, across);
                    s.radius = Mathf.Min(radius, radius * fraction);
                    s.height = Mathf.Max(a.height * (a.direction == 0 ? scale.x : scale.z), 2f * s.radius);
                    s.center = Vector3.Scale(a.center, scale);
                    s.center.y = (a.center.y - a.radius) * scale.y + s.radius;
                    break;
                }
                case BodyShapeKind.Box:
                    s.size = Vector3.Scale(a.size, scale);
                    s.size.y *= fraction;
                    s.center = Vector3.Scale(a.center, scale);
                    s.center.y = (a.center.y - 0.5f * a.size.y) * scale.y + 0.5f * s.size.y;
                    break;
                case BodyShapeKind.Sphere:
                    s.radius = a.radius * Mathf.Max(scale.x, Mathf.Max(scale.y, scale.z)) * fraction;
                    s.center = Vector3.Scale(a.center, scale);
                    s.center.y = (a.center.y - a.radius) * scale.y + s.radius;
                    break;
            }
            return s;
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
        /// The upright shape laid down along the facing, its bottom where it was (at the feet): an upright capsule lies
        /// along +Z, a box's height and depth swap, and a sphere (or a capsule already lying) stays as it is. Given a
        /// thickness (more than 0), it's no thicker than that: a capsule's radius is no more than half of it (its length
        /// staying as it was), a box no taller. A sphere stays round.
        /// </summary>
        private static BodyShape LaidDown(BodyShape s, float thickness)
        {
            float bottom = s.center.y - HalfExtents(s).y;
            if (s.kind == BodyShapeKind.Capsule && s.direction == 1) { s.direction = 2; }
            if (s.kind == BodyShapeKind.Box) { s.size = new Vector3(s.size.x, s.size.z, s.size.y); }
            if (thickness > 0f)
            {
                if (s.kind == BodyShapeKind.Capsule && s.direction != 1)
                {
                    float length = Mathf.Max(s.height, 2f * s.radius);
                    s.radius = Mathf.Min(s.radius, 0.5f * thickness);
                    s.height = length;
                }
                else if (s.kind == BodyShapeKind.Box) { s.size.y = Mathf.Min(s.size.y, thickness); }
            }
            s.center.y = bottom + HalfExtents(s).y;
            return s;
        }

        /// <summary>
        /// Gives the body another shape as authored (see BodyShape), of its collider's kind. Like growing, it waits for room:
        /// unless told not to (checkRoom false), if the new body would reach anywhere the current one doesn't and wouldn't
        /// fit with its feet at feet (turned rotation, at the given scale and posture), nothing changes and this returns
        /// false; also false for a shape of another kind, or without a size. Doesn't resize the collider: the owner does
        /// (see MovementController.SetBody).
        /// </summary>
        public bool SetAuthoredShape(BodyShape shape, Vector3 feet, Quaternion rotation, Vector3 scale, BodyPosture posture, bool checkRoom = true)
        {
            RememberAuthoredSize();
            if (collider == null || !shape.HasSize || !shape.IsKindOf(collider)) { return false; }
            if (checkRoom && Reaches(ShapeAt(shape, scale, posture), ShapeAt(authored, scale, posture))
                && !Fits(feet, rotation, scale, posture, shape)) { return false; }
            authored = shape;
            hasAuthored = true;
            return true;
        }

        /// <summary>Whether shape reaches anywhere than doesn't: above, ahead, behind or to either side of the feet (or below them).</summary>
        private static bool Reaches(BodyShape shape, BodyShape than)
        {
            const float tolerance = 1e-5f;
            Vector3 a = HalfExtents(shape), b = HalfExtents(than);
            return shape.center.y + a.y > than.center.y + b.y + tolerance || shape.center.y - a.y < than.center.y - b.y - tolerance
                || shape.center.z + a.z > than.center.z + b.z + tolerance || shape.center.z - a.z < than.center.z - b.z - tolerance
                || Mathf.Abs(shape.center.x) + a.x > Mathf.Abs(than.center.x) + b.x + tolerance;
        }

        public override CharacterBody CopyFor(MovementController copy, BodyShape? authoredShape = null)
        {
            RememberAuthoredSize();
            BodyShape shape = authoredShape is { HasSize: true } given ? given : authored;
            var added = (Collider)copy.gameObject.AddComponent(shape.ColliderType);
            if (added is CapsuleCollider capsule) { capsule.direction = shape.direction; }
            return new PrimitiveBody(copy, added) { authored = shape, hasAuthored = true };
        }

        // ---- Queries ----

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
            if (collider == null) { return true; }
            BodyShape s = ShapeAt(authoredShape, scale, posture);
            float authoredFootprint = Footprint(authoredShape);
            float skinAtScale = Owner.AuthoredSkinWidth * (authoredFootprint > 0f ? Mathf.Min(scale.y, Footprint(s) / authoredFootprint) : scale.y);
            Collider[] overlaps = Owner.OverlapBuffer;
            int count = Overlap(s, feet, rotation, skinAtScale, overlaps);
            for (int i = 0; i < count; i++)
            {
                if (!Owner.IsOwnCollider(overlaps[i].transform)) { return false; }
            }
            return true;
        }

        /// <summary>Shape-sweep from a feet-anchored position in the given direction, returning the closest non-self hit.</summary>
        public override bool Sweep(Vector3 feet, Quaternion rotation, Vector3 direction, float distance, out RaycastHit closest)
        {
            closest = default;
            if (direction.sqrMagnitude < 1e-8f || distance <= 0f) { return false; }

            Vector3 dir = direction.normalized;
            BodyShape s = Current;
            float skin = Owner.SkinWidth;
            RaycastHit[] castHits = Owner.CastBuffer;
            int count = Cast(s, feet, rotation, skin, dir, distance, castHits);
            float min = float.PositiveInfinity;
            return Closest(castHits, count, ref min, ref closest);
        }

        /// <summary>The nearest of count hits that isn't the character's own (or started overlapping), if nearer than min.</summary>
        private bool Closest(RaycastHit[] hits, int count, ref float min, ref RaycastHit closest)
        {
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                RaycastHit h = hits[i];
                if (Owner.IsOwnCollider(h.collider.transform)) { continue; }
                // hit.distance of 0 means we started already overlapping — skip so we can slide out instead of locking up.
                if (h.distance <= 0f) { continue; }
                if (h.distance < min) { min = h.distance; closest = h; found = true; }
            }
            return found;
        }

        public override Vector3 Depenetrate(Vector3 feet, Quaternion rotation)
        {
            if (collider == null) { return Vector3.zero; }

            BodyShape s = Current;
            float skin = Owner.SkinWidth;
            Collider[] overlaps = Owner.OverlapBuffer;
            Vector3 offset = Vector3.zero;
            for (int pass = 0; pass < MaxDepenetrationPasses; pass++)
            {
                int count = Overlap(s, feet + offset, rotation, skin, overlaps);
                bool pushed = false;
                for (int i = 0; i < count; i++)
                {
                    Collider other = overlaps[i];
                    if (Owner.IsOwnCollider(other.transform)) { continue; }
                    MovementController.GetColliderPose(other, out Vector3 otherPosition, out Quaternion otherRotation);
                    if (Physics.ComputePenetration(collider, feet + offset, rotation, other, otherPosition, otherRotation,
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

        /// <summary>The colliders the shape (shrunk by skin), its feet at feet and turned rotation, overlaps, into overlaps.</summary>
        private int Overlap(BodyShape s, Vector3 feet, Quaternion rotation, float skin, Collider[] overlaps)
        {
            int mask = Owner.MovementLayerMask;
            switch (s.kind)
            {
                case BodyShapeKind.Box:
                    GetQueryBox(s, feet, rotation, skin, out Vector3 boxCenter, out Vector3 halfExtents);
                    return Physics.OverlapBoxNonAlloc(boxCenter, halfExtents, overlaps, rotation, mask, QueryTriggerInteraction.Ignore);
                case BodyShapeKind.Sphere:
                    GetQuerySphere(s, feet, rotation, skin, out Vector3 sphereCenter, out float sphereRadius);
                    return Physics.OverlapSphereNonAlloc(sphereCenter, sphereRadius, overlaps, mask, QueryTriggerInteraction.Ignore);
                default:
                    GetQueryCapsule(s, feet, rotation, skin, out Vector3 top, out Vector3 bottom, out float radius);
                    return Physics.OverlapCapsuleNonAlloc(top, bottom, radius, overlaps, mask, QueryTriggerInteraction.Ignore);
            }
        }

        /// <summary>The shape (shrunk by skin), its feet at feet and turned rotation, cast along direction, into hits.</summary>
        private int Cast(BodyShape s, Vector3 feet, Quaternion rotation, float skin, Vector3 direction, float distance, RaycastHit[] hits)
        {
            int mask = Owner.MovementLayerMask;
            switch (s.kind)
            {
                case BodyShapeKind.Box:
                    GetQueryBox(s, feet, rotation, skin, out Vector3 boxCenter, out Vector3 halfExtents);
                    return Physics.BoxCastNonAlloc(boxCenter, halfExtents, direction, hits, rotation, distance, mask, QueryTriggerInteraction.Ignore);
                case BodyShapeKind.Sphere:
                    GetQuerySphere(s, feet, rotation, skin, out Vector3 sphereCenter, out float sphereRadius);
                    return Physics.SphereCastNonAlloc(sphereCenter, sphereRadius, direction, hits, distance, mask, QueryTriggerInteraction.Ignore);
                default:
                    GetQueryCapsule(s, feet, rotation, skin, out Vector3 top, out Vector3 bottom, out float radius);
                    return Physics.CapsuleCastNonAlloc(top, bottom, radius, direction, hits, distance, mask, QueryTriggerInteraction.Ignore);
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

        public override string CapabilitiesKey()
        {
            RememberAuthoredSize();
            CultureInfo invariant = CultureInfo.InvariantCulture;
            string F(float value) => value.ToString("R", invariant);
            string V(Vector3 v) => $"{F(v.x)},{F(v.y)},{F(v.z)}";
            BodyShape a = authored;
            return string.Join("|", a.kind.ToString(), a.direction.ToString(invariant), F(a.radius), F(a.height), V(a.center), V(a.size), F(a.lyingThickness));
        }
    }
}
