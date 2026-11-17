using System.Globalization;
using UnityEngine;

namespace Character
{
    /// <summary>
    /// A body (see CharacterBody) that is one of Unity's primitive colliders on the character's object: a capsule (upright
    /// or lying along an axis), a box or a sphere, centred wherever it's authored. Its size follows the character's (a
    /// capsule's length along its axis with that axis's scale, its radius with the larger of the others, but no more than
    /// half its height; a box's sides each with theirs; a sphere with the largest). Its postures are its authored shape
    /// lowered from the top (low) or laid down along the facing, resting on the feet (lying). Its queries use the shape
    /// shrunk by the skin width on every side.
    /// </summary>
    public sealed class PrimitiveBody : CharacterBody
    {
        // Passes made to push the body out of geometry that moved into it, for when several things overlap.
        private const int MaxDepenetrationPasses = 4;

        private enum Kind { Capsule, Box, Sphere }

        /// <summary>A primitive shape, relative to the character's feet and facing (+Z ahead).</summary>
        private struct Shape
        {
            public Kind kind;
            public Vector3 center;
            /// <summary>A capsule's or sphere's radius.</summary>
            public float radius;
            /// <summary>A capsule's length, end to end, along its axis.</summary>
            public float height;
            /// <summary>A capsule's axis: 0 across (X), 1 up (Y), 2 ahead (Z), as CapsuleCollider.direction.</summary>
            public int axis;
            /// <summary>A box's size.</summary>
            public Vector3 size;
        }

        private Collider collider;
        // The shape as authored, which the character's size and posture change.
        private Shape authored;
        private bool hasAuthored;

        /// <summary>
        /// A body for the owner, of the given primitive collider (or, if null, the owner's first solid capsule, box or
        /// sphere collider, once it has one).
        /// </summary>
        public PrimitiveBody(MovementController owner, Collider primitive) : base(owner) => collider = IsPrimitive(primitive) ? primitive : null;

        private static bool IsPrimitive(Collider c) => c is CapsuleCollider || c is BoxCollider || c is SphereCollider;

        public override Collider Collider => collider;
        public override bool HasAuthoredSize => hasAuthored;
        public override float AuthoredHeight { get { RememberAuthoredSize(); return UprightHeight(authored); } }
        public override float AuthoredRadius { get { RememberAuthoredSize(); return Footprint(authored); } }
        public override float FootprintRadius => Footprint(Current);
        public override float Height
        {
            get
            {
                Shape s = Current;
                return s.kind switch
                {
                    Kind.Box => s.size.y,
                    Kind.Capsule when s.axis == 1 => s.height,
                    _ => 2f * s.radius,
                };
            }
        }

        public override bool TurnsWithFacing
        {
            get
            {
                if (collider == null) { return false; }
                Shape s = Current;
                bool offCentre = s.center.x != 0f || s.center.z != 0f;
                return s.kind switch
                {
                    Kind.Capsule => s.axis != 1 || offCentre,
                    Kind.Sphere => offCentre,
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
        private Shape Current => Read(collider);

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

        private static Shape Read(Collider c)
        {
            switch (c)
            {
                case CapsuleCollider capsule:
                    return new Shape { kind = Kind.Capsule, center = capsule.center, radius = capsule.radius, height = capsule.height, axis = capsule.direction };
                case BoxCollider box:
                    return new Shape { kind = Kind.Box, center = box.center, size = box.size };
                case SphereCollider sphere:
                    return new Shape { kind = Kind.Sphere, center = sphere.center, radius = sphere.radius };
                default:
                    // No collider yet: the default upright capsule, 2 m tall, standing on the feet.
                    return new Shape { kind = Kind.Capsule, center = new Vector3(0f, 1f, 0f), radius = 0.5f, height = 2f, axis = 1 };
            }
        }

        private void Write(Shape s)
        {
            switch (collider)
            {
                case CapsuleCollider capsule:
                    if (capsule.direction != s.axis) { capsule.direction = s.axis; }
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
        private static float UprightHeight(Shape s) => s.kind switch
        {
            Kind.Box => s.size.y,
            Kind.Capsule when s.axis == 1 => s.height,
            _ => 2f * s.radius,
        };

        /// <summary>The radius of the shape's footprint: a capsule's or sphere's radius, half a box's narrower side.</summary>
        private static float Footprint(Shape s) => s.kind == Kind.Box ? 0.5f * Mathf.Min(s.size.x, s.size.z) : s.radius;

        /// <summary>Half the shape's size along each of the character's axes, about its centre.</summary>
        private static Vector3 HalfExtents(Shape s)
        {
            switch (s.kind)
            {
                case Kind.Box: return 0.5f * s.size;
                case Kind.Sphere: return new Vector3(s.radius, s.radius, s.radius);
                default:
                    float half = 0.5f * Mathf.Max(s.height, 2f * s.radius);
                    return new Vector3(s.axis == 0 ? half : s.radius, s.axis == 1 ? half : s.radius, s.axis == 2 ? half : s.radius);
            }
        }

        /// <summary>The shape at a size and posture: upright (lowered from the top while low), or laid down along the facing.</summary>
        private static Shape ShapeAt(Shape authored, Vector3 scale, BodyPosture posture)
        {
            Shape s = Upright(authored, scale, posture.Kind == BodyPostureKind.Low ? posture.Share : 1f);
            return posture.Kind == BodyPostureKind.Lying ? LaidDown(s) : s;
        }

        /// <summary>
        /// The authored shape at a size and share of its standing height, its bottom staying at the feet's height (so a
        /// lower shape is lowered from the top).
        /// </summary>
        private static Shape Upright(Shape a, Vector3 scale, float fraction)
        {
            Shape s = a;
            switch (a.kind)
            {
                case Kind.Capsule when a.axis == 1:
                    CapsuleAt(scale, fraction, a.radius, a.height, a.center, out s.radius, out s.height, out s.center);
                    break;
                case Kind.Capsule:
                {
                    // Lying along X or Z as authored: its length scales with that axis, its radius with the larger of the
                    // others (no more than half its lowered height).
                    float across = a.axis == 0 ? scale.z : scale.x;
                    float radius = a.radius * Mathf.Max(scale.y, across);
                    s.radius = Mathf.Min(radius, radius * fraction);
                    s.height = Mathf.Max(a.height * (a.axis == 0 ? scale.x : scale.z), 2f * s.radius);
                    s.center = Vector3.Scale(a.center, scale);
                    s.center.y = (a.center.y - a.radius) * scale.y + s.radius;
                    break;
                }
                case Kind.Box:
                    s.size = Vector3.Scale(a.size, scale);
                    s.size.y *= fraction;
                    s.center = Vector3.Scale(a.center, scale);
                    s.center.y = (a.center.y - 0.5f * a.size.y) * scale.y + 0.5f * s.size.y;
                    break;
                case Kind.Sphere:
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
        /// along +Z, a box's height and depth swap, and a sphere (or a capsule already lying) stays as it is.
        /// </summary>
        private static Shape LaidDown(Shape s)
        {
            float bottom = s.center.y - HalfExtents(s).y;
            if (s.kind == Kind.Capsule && s.axis == 1) { s.axis = 2; }
            if (s.kind == Kind.Box) { s.size = new Vector3(s.size.x, s.size.z, s.size.y); }
            s.center.y = bottom + HalfExtents(s).y;
            return s;
        }

        /// <summary>
        /// The authored shape refitted to a footprint radius and standing height, standing on the feet: an upright capsule
        /// of that radius (no more than half the height) and height; a box that tall, its sides in proportion and its wider
        /// side twice the radius; a sphere of that radius (no more than half the height). False for a lying capsule.
        /// </summary>
        private static bool Refit(Shape a, float radius, float height, out Shape s)
        {
            s = a;
            switch (a.kind)
            {
                case Kind.Capsule when a.axis == 1:
                    s.radius = Mathf.Min(radius, height * 0.5f);
                    s.height = height;
                    s.center = new Vector3(a.center.x, height * 0.5f, a.center.z);
                    return true;
                case Kind.Box:
                {
                    float wider = Mathf.Max(a.size.x, a.size.z, 1e-4f);
                    float k = 2f * radius / wider;
                    s.size = new Vector3(a.size.x * k, height, a.size.z * k);
                    s.center = new Vector3(a.center.x, height * 0.5f, a.center.z);
                    return true;
                }
                case Kind.Sphere:
                    s.radius = Mathf.Min(radius, height * 0.5f);
                    s.center = new Vector3(a.center.x, s.radius, a.center.z);
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Changes the body's size as authored (the character's scale then multiplies it), standing on the feet (see
        /// Refit). Used to fit the collider to the character's model (see CharacterModelHost). Like growing, it waits for
        /// room: if the bigger body wouldn't fit with its feet at feet (turned rotation, at the given scale and posture),
        /// nothing changes and this returns false. Doesn't resize the collider: the owner does (see
        /// MovementController.SetBaseSize).
        /// </summary>
        public bool SetAuthoredSize(float radius, float height, Vector3 feet, Quaternion rotation, Vector3 scale, BodyPosture posture)
        {
            RememberAuthoredSize();
            if (collider == null || radius <= 0f || height <= 0f || !Refit(authored, radius, height, out Shape refitted)) { return false; }
            bool grows = UprightHeight(refitted) > UprightHeight(authored) + 1e-5f || Footprint(refitted) > Footprint(authored) + 1e-5f;
            if (grows && !Fits(feet, rotation, scale, posture, refitted)) { return false; }
            authored = refitted;
            return true;
        }

        public override CharacterBody CopyFor(MovementController copy, float? authoredRadius = null, float? authoredHeight = null)
        {
            RememberAuthoredSize();
            Collider added = authored.kind switch
            {
                Kind.Box => copy.gameObject.AddComponent<BoxCollider>(),
                Kind.Sphere => copy.gameObject.AddComponent<SphereCollider>(),
                _ => copy.gameObject.AddComponent<CapsuleCollider>(),
            };
            var body = new PrimitiveBody(copy, added) { authored = authored, hasAuthored = true };
            if (authoredRadius.HasValue && authoredHeight.HasValue && authoredHeight.Value > 0f
                && Refit(authored, authoredRadius.Value, authoredHeight.Value, out Shape refitted))
            {
                body.authored = refitted;
            }
            if (added is CapsuleCollider capsule) { capsule.direction = body.authored.axis; }
            return body;
        }

        // ---- Queries ----

        public override bool Fits(Vector3 feet, Quaternion rotation, Vector3 scale, BodyPosture posture)
        {
            RememberAuthoredSize();
            return Fits(feet, rotation, scale, posture, authored);
        }

        /// <summary>The same, for a body of another authored shape.</summary>
        private bool Fits(Vector3 feet, Quaternion rotation, Vector3 scale, BodyPosture posture, Shape authoredShape)
        {
            if (collider == null) { return true; }
            Shape s = ShapeAt(authoredShape, scale, posture);
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
            Shape s = Current;
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

            Shape s = Current;
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
        private int Overlap(Shape s, Vector3 feet, Quaternion rotation, float skin, Collider[] overlaps)
        {
            int mask = Owner.MovementLayerMask;
            switch (s.kind)
            {
                case Kind.Box:
                    GetQueryBox(s, feet, rotation, skin, out Vector3 boxCenter, out Vector3 halfExtents);
                    return Physics.OverlapBoxNonAlloc(boxCenter, halfExtents, overlaps, rotation, mask, QueryTriggerInteraction.Ignore);
                case Kind.Sphere:
                    GetQuerySphere(s, feet, rotation, skin, out Vector3 sphereCenter, out float sphereRadius);
                    return Physics.OverlapSphereNonAlloc(sphereCenter, sphereRadius, overlaps, mask, QueryTriggerInteraction.Ignore);
                default:
                    GetQueryCapsule(s, feet, rotation, skin, out Vector3 top, out Vector3 bottom, out float radius);
                    return Physics.OverlapCapsuleNonAlloc(top, bottom, radius, overlaps, mask, QueryTriggerInteraction.Ignore);
            }
        }

        /// <summary>The shape (shrunk by skin), its feet at feet and turned rotation, cast along direction, into hits.</summary>
        private int Cast(Shape s, Vector3 feet, Quaternion rotation, float skin, Vector3 direction, float distance, RaycastHit[] hits)
        {
            int mask = Owner.MovementLayerMask;
            switch (s.kind)
            {
                case Kind.Box:
                    GetQueryBox(s, feet, rotation, skin, out Vector3 boxCenter, out Vector3 halfExtents);
                    return Physics.BoxCastNonAlloc(boxCenter, halfExtents, direction, hits, rotation, distance, mask, QueryTriggerInteraction.Ignore);
                case Kind.Sphere:
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
        private static Vector3 WorldCenter(Shape s, Vector3 feet, Quaternion rotation) =>
            s.center.x == 0f && s.center.z == 0f && IsLevel(rotation) ? feet + s.center : feet + rotation * s.center;

        // Below this, a rotation's x and z parts are rounding: it's a turn about the vertical alone (a facing, unpitched).
        private const float LevelTolerance = 1e-6f;

        /// <summary>Whether a rotation only turns about the vertical (a facing, not pitched or tilted).</summary>
        private static bool IsLevel(Quaternion rotation) => Mathf.Abs(rotation.x) < LevelTolerance && Mathf.Abs(rotation.z) < LevelTolerance;

        /// <summary>
        /// The capsule used for collision queries, placed at the given feet position and turned rotation: the shape shrunk
        /// by the skin width (its radius, so its ends too).
        /// </summary>
        private static void GetQueryCapsule(Shape s, Vector3 feetPos, Quaternion rotation, float skin, out Vector3 top, out Vector3 bottom, out float radius)
        {
            radius = s.radius - skin;
            if (radius <= 0f) { radius = s.radius * 0.5f; }
            float height = Mathf.Max(s.height, s.radius * 2f);
            float halfSegment = Mathf.Max(0f, (height * 0.5f) - radius - skin);
            Vector3 center = WorldCenter(s, feetPos, rotation);
            // Upright and level, straight up: an upright capsule turned about its axis is the same capsule.
            Vector3 axis = s.axis == 1 && IsLevel(rotation) ? Vector3.up
                : rotation * (s.axis == 0 ? Vector3.right : s.axis == 1 ? Vector3.up : Vector3.forward);
            bottom = center - axis * halfSegment;
            top = center + axis * halfSegment;
        }

        private static void GetQueryBox(Shape s, Vector3 feetPos, Quaternion rotation, float skin, out Vector3 center, out Vector3 halfExtents)
        {
            center = feetPos + rotation * s.center;
            Vector3 half = 0.5f * s.size;
            halfExtents = new Vector3(Shrunk(half.x, skin), Shrunk(half.y, skin), Shrunk(half.z, skin));
        }

        private static void GetQuerySphere(Shape s, Vector3 feetPos, Quaternion rotation, float skin, out Vector3 center, out float radius)
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
            Shape a = authored;
            return string.Join("|", a.kind.ToString(), a.axis.ToString(invariant), F(a.radius), F(a.height), V(a.center), V(a.size));
        }
    }
}
