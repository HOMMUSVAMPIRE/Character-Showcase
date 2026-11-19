using System;
using UnityEngine;

namespace MeridianMotion.Characters
{
    /// <summary>The kinds of primitive a character's body can be (see BodyShape).</summary>
    public enum BodyShapeKind
    {
        /// <summary>A capsule, upright or lying along an axis.</summary>
        Capsule,
        /// <summary>A box.</summary>
        Box,
        /// <summary>A sphere.</summary>
        Sphere,
    }

    /// <summary>
    /// A character's body as authored (at its authored size, 1, 1, 1): one of Unity's primitive collider shapes, relative
    /// to its feet (the character's origin) and its facing (+Z ahead), standing; and how thick it is lying down (see
    /// BodyPosture.Lying), if thinner than laying its standing shape down makes it. A PrimitiveBody is shaped from one (see
    /// MMotionMotor.SetBody); a BodyDefinition asset holds one, for a character or a model (see
    /// CharacterModelSource.BodyDefinition).
    /// </summary>
    [Serializable]
    public struct BodyShape : IEquatable<BodyShape>
    {
        [Tooltip("The kind of primitive the body is: a capsule (upright, or lying along an axis), a box or a sphere")]
        public BodyShapeKind kind;
        [Tooltip("The middle of the body, relative to the character's feet (its origin) and facing (+Z ahead), in metres: " +
                 "half its height up, for a body standing on its feet")]
        public Vector3 center;
        [Tooltip("A capsule's or sphere's radius (m)")]
        [Min(0f)] public float radius;
        [Tooltip("A capsule's length (m), end to end, along its axis")]
        [Min(0f)] public float height;
        [Tooltip("A capsule's axis: 0 across (X), 1 up (Y), 2 ahead (Z), as a CapsuleCollider's Direction")]
        [Range(0, 2)] public int direction;
        [Tooltip("A box's size (m): across, up, and front to back")]
        public Vector3 size;
        [Tooltip("How thick (m) the body is lying down (swimming underwater), at most as thick as laying it down makes it: a " +
                 "swimmer is thinner front to back than it's wide. 0 lies it down as it stands (a capsule as thick as it's " +
                 "wide; a box as thick as it's deep)")]
        [Min(0f)] public float lyingThickness;

        /// <summary>
        /// The body a character has with no Body Definition and no collider to be shaped from: an upright capsule standing on
        /// the feet, the size of an adult.
        /// </summary>
        public static BodyShape Default => Capsule(0.5f, 2f);

        /// <summary>An upright capsule standing on the feet, radius (m) round (no more than half its height) and height (m) tall.</summary>
        public static BodyShape Capsule(float radius, float height, float lyingThickness = 0f) => new BodyShape
        {
            kind = BodyShapeKind.Capsule, radius = Mathf.Min(radius, 0.5f * height), height = height, direction = 1,
            center = new Vector3(0f, 0.5f * height, 0f), lyingThickness = lyingThickness,
        };

        /// <summary>A box of the given size (m) standing on the feet, centred over them.</summary>
        public static BodyShape Box(Vector3 size, float lyingThickness = 0f) => new BodyShape
        {
            kind = BodyShapeKind.Box, size = size, center = new Vector3(0f, 0.5f * size.y, 0f), lyingThickness = lyingThickness,
        };

        /// <summary>A sphere of the given radius (m) resting on the feet.</summary>
        public static BodyShape Sphere(float radius) => new BodyShape
        {
            kind = BodyShapeKind.Sphere, radius = radius, center = new Vector3(0f, radius, 0f),
        };

        /// <summary>The shape of a primitive collider as it is (its lying thickness 0); false for any other collider.</summary>
        public static bool TryRead(Collider collider, out BodyShape shape)
        {
            switch (collider)
            {
                case CapsuleCollider capsule:
                    shape = new BodyShape { kind = BodyShapeKind.Capsule, center = capsule.center, radius = capsule.radius, height = capsule.height, direction = capsule.direction };
                    return true;
                case BoxCollider box:
                    shape = new BodyShape { kind = BodyShapeKind.Box, center = box.center, size = box.size };
                    return true;
                case SphereCollider sphere:
                    shape = new BodyShape { kind = BodyShapeKind.Sphere, center = sphere.center, radius = sphere.radius };
                    return true;
                default:
                    shape = default;
                    return false;
            }
        }

        /// <summary>The collider type a body of this kind is.</summary>
        public Type ColliderType => kind switch
        {
            BodyShapeKind.Box => typeof(BoxCollider),
            BodyShapeKind.Sphere => typeof(SphereCollider),
            _ => typeof(CapsuleCollider),
        };

        /// <summary>Whether the collider is of the kind this shape is.</summary>
        public bool IsKindOf(Collider collider) => collider != null && collider.GetType() == ColliderType;

        /// <summary>Whether the shape has a size: a box with sides, a capsule or sphere with a radius.</summary>
        public bool HasSize => kind == BodyShapeKind.Box ? size.x > 0f && size.y > 0f && size.z > 0f : radius > 0f;

        /// <summary>The same shape, every length times factor (a model's own body at the size its source gives it, say).</summary>
        public BodyShape Scaled(float factor)
        {
            BodyShape s = this;
            s.center *= factor;
            s.radius *= factor;
            s.height *= factor;
            s.size *= factor;
            s.lyingThickness *= factor;
            return s;
        }

        public bool Equals(BodyShape other) => kind == other.kind && center == other.center && radius == other.radius && height == other.height
                                               && direction == other.direction && size == other.size && lyingThickness == other.lyingThickness;
        public override bool Equals(object obj) => obj is BodyShape other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(kind, center, radius, height, direction, size, lyingThickness);

        public override string ToString() => kind switch
        {
            BodyShapeKind.Box => $"Box {size} at {center}",
            BodyShapeKind.Sphere => $"Sphere r {radius:0.###} at {center}",
            _ => $"Capsule r {radius:0.###} h {height:0.###} axis {direction} at {center}",
        } + (lyingThickness > 0f ? $", lying {lyingThickness:0.###} thick" : "");
    }
}
