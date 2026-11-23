using System.Collections.Generic;
using System.Globalization;
using MeridianMotion.PhysicsQueries;
using UnityEngine;

namespace MeridianMotion.Characters
{
    // The body's shapes: at a size and posture, held low, lying, lifted over steps, and its collider's.
    public sealed partial class PrimitiveBody
    {
        // No collider: the default body (an upright capsule standing on the feet).
        private static BodyShape Read(Collider c) => BodyShape.TryRead(c, out BodyShape shape) ? shape : BodyShape.Default;

        /// <summary>Shapes a primitive collider of the shape's kind as the shape (see MMotionMotor.QueryCollider, too).</summary>
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
        /// The shape with its bottom held lift (m) above the feet, its top where it was (see MMotionMotor's Hover Over
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
        /// size. Doesn't resize it: the owner does (see MMotionMotor.SetBody).
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

        public override CharacterBody CopyFor(MMotionMotor copy, BodyShape? authoredShape = null)
        {
            RememberAuthoredSize();
            BodyShape shape = authoredShape is { HasSize: true } given ? given : authored;
            // No collider: the copy's movement doesn't need one.
            var body = new PrimitiveBody(copy, null) { authored = shape, hasAuthored = true };
            body.SetExtraParts(authoredExtras);
            return body;
        }
    }
}
