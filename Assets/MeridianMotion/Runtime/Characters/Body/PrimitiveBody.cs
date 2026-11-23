using System.Collections.Generic;
using System.Globalization;
using MeridianMotion.PhysicsQueries;
using UnityEngine;

namespace MeridianMotion.Characters
{
    /// <summary>
    /// A body (see CharacterBody) shaped as one of Unity's primitive colliders: a capsule (upright or lying along an axis),
    /// a box or a sphere, centred wherever it's authored. It's a shape, not a collider: the movement needs no collider on
    /// the character (its queries are made with the shape itself, and pushing it out of the world with a private collider
    /// that nothing else meets: see MMotionMotor.QueryCollider). If the character has a collider of its kind (its
    /// Body Collider), the body keeps it shaped as it is, for the physics that meets the character; it's the body's
    /// shape as authored, too, unless it's given one (see MMotionMotor.SetBody). Its size follows the character's (a
    /// capsule's length along its axis with that axis's scale, its radius with the larger of the others, but no more than
    /// half its height; a box's sides each with theirs; a sphere with the largest). Its postures are its authored shape
    /// lowered from the top (low) or laid down along the facing, resting on the feet (lying), no thicker than its lying
    /// thickness (see BodyShape). Its queries use the shape shrunk by the skin width on every side. Without a collider or
    /// a shape given it, it's the default body (BodyShape.Default).
    ///
    /// It can have extra parts (see BodyDefinition's Extra Parts): more primitives beside its main shape, together one
    /// body (a long generic-rig character's back and head), the way a model's hitboxes are many (see SetExtraParts). Its queries cover
    /// every part; it's as tall as its highest part's top, and reaches as far as its furthest part does. Held low, every
    /// part is kept under the lowered top (one already under it staying where it is, the others pushed down under it,
    /// made thinner only where they must be); lying, only its main shape lies down, the extra parts left out. The main
    /// shape is the one its collider (if any) keeps, its footprint the main shape's.
    /// </summary>
    public sealed partial class PrimitiveBody : CharacterBody
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
        public PrimitiveBody(MMotionMotor owner, Collider primitive) : base(owner)
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
        /// Parts without a size are left out. Doesn't resize it: the owner does (see MMotionMotor.SetBody).
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
        private static Collider FindPrimitive(MMotionMotor owner)
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
