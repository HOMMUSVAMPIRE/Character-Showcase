using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Character
{
    /// <summary>
    /// A body (see CharacterBody) shaped as one of Unity's primitive colliders: a capsule (upright or lying along an axis),
    /// a box or a sphere, centred wherever it's authored. It's a shape, not a collider: the movement needs no collider on
    /// the character (its queries are made with the shape itself, and pushing it out of the world with a private collider
    /// that nothing else meets: see MovementController.QueryCollider). If the character has a collider of its kind (its
    /// Body Collider), the body keeps it shaped as it is, for the physics that meets the character; it's the body's
    /// shape as authored, too, unless it's given one (see MovementController.SetBody). Its size follows the character's (a
    /// capsule's length along its axis with that axis's scale, its radius with the larger of the others, but no more than
    /// half its height; a box's sides each with theirs; a sphere with the largest). Its postures are its authored shape
    /// lowered from the top (low) or laid down along the facing, resting on the feet (lying), no thicker than its lying
    /// thickness (see BodyShape). Its queries use the shape shrunk by the skin width on every side. Without a collider or
    /// a shape given it, it's an upright capsule 2 m tall standing on the feet.
    ///
    /// It can have extra parts (see BodyDefinition's Extra Parts): more primitives beside its main shape, together one
    /// body (a long creature's back and head), the way a model's hitboxes are many (see SetExtraParts). Its queries cover
    /// every part; it's as tall as its highest part's top, and reaches as far as its furthest part does. Held low, every
    /// part is kept under the lowered top (one already under it staying where it is, the others pushed down under it,
    /// made thinner only where they must be); lying, only its main shape lies down, the extra parts left out. The main
    /// shape is the one its collider (if any) keeps, its footprint the main shape's.
    /// </summary>
    public sealed class PrimitiveBody : CharacterBody
    {
        // Passes made to push the body out of geometry that moved into it, for when several things overlap.
        private const int MaxDepenetrationPasses = 4;

        // The collider the body keeps shaped as it is, if the character has one (see Collider).
        private Collider collider;
        // The shape as authored, which the character's size and posture change.
        private BodyShape authored;
        private bool hasAuthored;
        // The shape now, at the character's size and posture (see Resize).
        private BodyShape current;
        private bool hasCurrent;
        // Whether the owner has been looked through for a collider (see RememberAuthoredSize).
        private bool searched;
        // The extra parts as authored, and now (the first currentExtraCount of currentExtras), and room for them at another
        // size or posture (see Fits).
        private BodyShape[] authoredExtras = System.Array.Empty<BodyShape>();
        private BodyShape[] currentExtras = System.Array.Empty<BodyShape>();
        private BodyShape[] scratchExtras = System.Array.Empty<BodyShape>();
        private int currentExtraCount;

        /// <summary>
        /// A body for the owner, keeping the given primitive collider shaped as it is (or, if null, the owner's first
        /// solid capsule, box or sphere collider, if it has one).
        /// </summary>
        public PrimitiveBody(MovementController owner, Collider primitive) : base(owner)
        {
            collider = IsPrimitive(primitive) ? primitive : null;
            searched = collider != null;
        }

        private static bool IsPrimitive(Collider c) => c is CapsuleCollider || c is BoxCollider || c is SphereCollider;

        public override Collider Collider => collider;
        public override bool HasAuthoredSize => hasAuthored;

        /// <summary>The body's shape as authored (at the character's authored size, standing): its collider's, or the one it was given.</summary>
        public BodyShape AuthoredShape { get { RememberAuthoredSize(); return authored; } }
        public override float AuthoredHeight
        {
            get
            {
                RememberAuthoredSize();
                if (authoredExtras.Length == 0) { return UprightHeight(authored); }
                float top = TopOf(authored);
                foreach (BodyShape part in authoredExtras) { top = Mathf.Max(top, TopOf(part)); }
                return top;
            }
        }

        /// <summary>The body's extra parts as authored (see SetExtraParts): none for a body of one shape.</summary>
        public IReadOnlyList<BodyShape> AuthoredExtraParts => authoredExtras;

        /// <summary>
        /// Gives the body extra parts as authored (see BodyDefinition's Extra Parts; primitives at the character's authored
        /// size, relative to its feet and facing, standing), in place of any it had; none or null makes it one shape again.
        /// Parts without a size are left out. Doesn't resize it: the owner does (see MovementController.SetBody).
        /// </summary>
        public void SetExtraParts(IReadOnlyList<BodyShape> parts)
        {
            var kept = new List<BodyShape>();
            if (parts != null) { foreach (BodyShape part in parts) { if (part.HasSize) { kept.Add(part); } } }
            authoredExtras = kept.ToArray();
            currentExtras = new BodyShape[authoredExtras.Length];
            scratchExtras = new BodyShape[authoredExtras.Length];
            currentExtraCount = 0;
            hasCurrent = false;
        }

        /// <summary>How high above the feet the shape's top is.</summary>
        private static float TopOf(BodyShape s) => s.center.y + HalfExtents(s).y;
        public override float AuthoredRadius { get { RememberAuthoredSize(); return Footprint(authored); } }
        public override float FootprintRadius => Footprint(Current);
        public override float Height
        {
            get
            {
                BodyShape s = Current;
                if (currentExtraCount > 0)
                {
                    // Of several parts: as high as its highest part's top is above the feet.
                    float top = TopOf(s);
                    for (int i = 0; i < currentExtraCount; i++) { top = Mathf.Max(top, TopOf(currentExtras[i])); }
                    return top;
                }
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
                if (TurnsWith(Current)) { return true; }
                for (int i = 0; i < currentExtraCount; i++) { if (TurnsWith(currentExtras[i])) { return true; } }
                return false;
            }
        }

        /// <summary>Whether turning the character turns the shape into somewhere else (it isn't round about the feet).</summary>
        private static bool TurnsWith(BodyShape s)
        {
            bool offCentre = s.center.x != 0f || s.center.z != 0f;
            return s.kind switch
            {
                BodyShapeKind.Capsule => s.direction != 1 || offCentre,
                BodyShapeKind.Sphere => offCentre,
                _ => true,
            };
        }

        public override bool FlatBottomed => Current.kind == BodyShapeKind.Box && currentExtraCount == 0;

        // How far the body reaches ahead of, behind and beside the feet, in the character's frame: its furthest part. A
        // round shape on the feet reaches its radius every way.
        public override float FrontExtent
        {
            get
            {
                float reach = Front(Current);
                for (int i = 0; i < currentExtraCount; i++) { reach = Mathf.Max(reach, Front(currentExtras[i])); }
                return reach;
            }
        }

        public override float BackExtent
        {
            get
            {
                float reach = Back(Current);
                for (int i = 0; i < currentExtraCount; i++) { reach = Mathf.Max(reach, Back(currentExtras[i])); }
                return reach;
            }
        }

        public override float SideExtent
        {
            get
            {
                float reach = Side(Current);
                for (int i = 0; i < currentExtraCount; i++) { reach = Mathf.Max(reach, Side(currentExtras[i])); }
                return reach;
            }
        }

        private static float Front(BodyShape s) => s.center.z + HalfExtents(s).z;
        private static float Back(BodyShape s) => HalfExtents(s).z - s.center.z;
        private static float Side(BodyShape s) => Mathf.Abs(s.center.x) + HalfExtents(s).x;

        /// <summary>
        /// The body's shape now, at the character's size and posture: as the last Resize made it, or (before one) its
        /// shape as authored, at the character's size and posture now.
        /// </summary>
        public BodyShape Current
        {
            get
            {
                if (!hasCurrent)
                {
                    RememberAuthoredSize();
                    currentExtraCount = PartsAt(authored, Owner.Scale, Owner.Posture, currentExtras, out current);
                    hasCurrent = true;
                }
                return current;
            }
        }

        public override void RememberAuthoredSize()
        {
            // Also before Awake (a prefab asset asked about, say), so the collider is fetched here too: once (this is
            // asked every step, and a character without a collider would otherwise look for one every time).
            if (collider == null && !searched)
            {
                collider = FindPrimitive(Owner);
                searched = true;
            }
            if (hasAuthored) { return; }
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
            currentExtraCount = PartsAt(authored, scale, posture, currentExtras, out current);
            hasCurrent = true;
            // The collider, if there is one, follows the body: the movement itself doesn't need it.
            if (current.IsKindOf(collider)) { Write(collider, current); }
        }

        // ---- Shapes ----

        // No collider: the default upright capsule, 2 m tall, standing on the feet.
        private static BodyShape Read(Collider c) => BodyShape.TryRead(c, out BodyShape shape) ? shape : BodyShape.Capsule(0.5f, 2f);

        /// <summary>Shapes a primitive collider of the shape's kind as the shape (see MovementController.QueryCollider, too).</summary>
        internal static void Write(Collider collider, BodyShape s)
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
        /// The body's parts at a size and posture: its main shape (as ShapeAt shapes it, for a body of one shape) and its
        /// extra parts (into extras, returning how many). Of several parts, held low, each is kept under the lowered top
        /// (see Squashed); lying, the extra parts are left out.
        /// </summary>
        private int PartsAt(BodyShape authoredMain, Vector3 scale, BodyPosture posture, BodyShape[] extras, out BodyShape main)
        {
            if (authoredExtras.Length == 0 || posture.Kind == BodyPostureKind.Lying)
            {
                main = ShapeAt(authoredMain, scale, posture);
                return 0;
            }
            main = Upright(authoredMain, scale, 1f);
            float top = TopOf(main);
            for (int i = 0; i < authoredExtras.Length; i++)
            {
                extras[i] = Upright(authoredExtras[i], scale, 1f);
                top = Mathf.Max(top, TopOf(extras[i]));
            }
            if (posture.Kind == BodyPostureKind.Low)
            {
                float lowered = top * posture.Share;
                main = Squashed(main, lowered);
                for (int i = 0; i < authoredExtras.Length; i++) { extras[i] = Squashed(extras[i], lowered); }
            }
            return authoredExtras.Length;
        }

        /// <summary>
        /// The shape kept under top (m above the feet): as it is if it's under it already; otherwise pushed down under it, as
        /// far as the feet, and made shorter (a box or upright capsule) or thinner (a sphere or lying capsule) only as far
        /// as it must be to fit between them.
        /// </summary>
        private static BodyShape Squashed(BodyShape s, float top)
        {
            if (TopOf(s) <= top) { return s; }
            switch (s.kind)
            {
                case BodyShapeKind.Box:
                {
                    float bottom = Mathf.Max(0f, top - s.size.y);
                    s.size.y = top - bottom;
                    s.center.y = 0.5f * (top + bottom);
                    return s;
                }
                case BodyShapeKind.Capsule when s.direction == 1:
                {
                    float bottom = Mathf.Max(0f, top - s.height);
                    s.height = top - bottom;
                    s.radius = Mathf.Min(s.radius, 0.5f * s.height);
                    s.center.y = 0.5f * (top + bottom);
                    return s;
                }
                default:
                    s.radius = Mathf.Min(s.radius, 0.5f * top);
                    s.center.y = top - s.radius;
                    return s;
            }
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
        /// The shape with its bottom held lift (m) above the feet, its top where it was (see MovementController's Hover Over
        /// Steps): a box or upright capsule shortened from the bottom, as far as that lifts it, but never thinner across
        /// (a capsule no shorter than it's wide, a box no thinner than its skin's twice over), so it reaches as far ahead
        /// and to the sides. A shape already that far above the feet, or round across its height (a sphere, or a capsule
        /// lying), isn't changed. The movement's queries are made with this shape; the body itself (its Height, its
        /// collider) stays as it is.
        /// </summary>
        private BodyShape Lifted(BodyShape s, float lift)
        {
            if (lift <= 0f) { return s; }
            float half = HalfExtents(s).y;
            float bottom = s.center.y - half, top = s.center.y + half;
            if (bottom >= lift) { return s; }
            switch (s.kind)
            {
                case BodyShapeKind.Box:
                {
                    float lifted = Mathf.Max(bottom, Mathf.Min(lift, top - 4f * Owner.SkinWidth));
                    s.size.y = top - lifted;
                    s.center.y = 0.5f * (top + lifted);
                    return s;
                }
                case BodyShapeKind.Capsule when s.direction == 1:
                {
                    float lifted = Mathf.Max(bottom, Mathf.Min(lift, top - 2f * s.radius));
                    s.height = top - lifted;
                    s.center.y = 0.5f * (top + lifted);
                    return s;
                }
                default:
                    return s;
            }
        }

        /// <summary>
        /// Gives the body another shape as authored (see BodyShape): of its collider's kind, if it has a collider. Like
        /// growing, it waits for room: unless told not to (checkRoom false), if the new body would reach anywhere the
        /// current one doesn't and wouldn't fit with its feet at feet (turned rotation, at the given scale and posture),
        /// nothing changes and this returns false; also false for a shape of another kind than its collider, or without a
        /// size. Doesn't resize it: the owner does (see MovementController.SetBody).
        /// </summary>
        public bool SetAuthoredShape(BodyShape shape, Vector3 feet, Quaternion rotation, Vector3 scale, BodyPosture posture, bool checkRoom = true)
        {
            RememberAuthoredSize();
            if (!shape.HasSize || (collider != null && !shape.IsKindOf(collider))) { return false; }
            if (checkRoom && Reaches(ShapeAt(shape, scale, posture), ShapeAt(authored, scale, posture))
                && !Fits(feet, rotation, scale, posture, shape)) { return false; }
            authored = shape;
            hasAuthored = true;
            hasCurrent = false;
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
            // No collider: the copy's movement doesn't need one.
            var body = new PrimitiveBody(copy, null) { authored = shape, hasAuthored = true };
            body.SetExtraParts(authoredExtras);
            return body;
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
            Collider[] overlaps = Owner.OverlapBuffer;
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
            RaycastHit[] castHits = Owner.CastBuffer;
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
            float lift = Owner.BodyLift;
            float skin = Owner.SkinWidth;
            Collider[] overlaps = Owner.OverlapBuffer;
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
        private bool PushOut(BodyShape s, int part, Vector3 feet, Quaternion rotation, float skin, Collider[] overlaps, ref Vector3 offset)
        {
            int count = Overlap(s, feet + offset, rotation, skin, overlaps);
            bool pushed = false;
            Collider query = null;
            try
            {
                for (int i = 0; i < count; i++)
                {
                    Collider other = overlaps[i];
                    if (Owner.IsOwnCollider(other.transform)) { continue; }
                    if (query == null)
                    {
                        query = Owner.QueryCollider(s, part);
                        query.enabled = true;
                    }
                    MovementController.GetColliderPose(other, out Vector3 otherPosition, out Quaternion otherRotation);
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
            string Shape(BodyShape a) => string.Join("|", a.kind.ToString(), a.direction.ToString(invariant), F(a.radius), F(a.height), V(a.center), V(a.size), F(a.lyingThickness));
            string key = Shape(authored);
            foreach (BodyShape part in authoredExtras) { key += "+" + Shape(part); }
            return key;
        }
    }
}
