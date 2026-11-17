using System;

namespace Character
{
    /// <summary>The ways a character's body can be held (see BodyPosture).</summary>
    public enum BodyPostureKind
    {
        /// <summary>Upright, as authored.</summary>
        Standing,
        /// <summary>Upright but lowered from the top, the feet staying put: crouching, sliding, rolling.</summary>
        Low,
        /// <summary>Laid flat along the way the character faces, resting on the feet: swimming underwater.</summary>
        Lying,
    }

    /// <summary>
    /// How the character's body is held: what shape collision gives it (see CharacterBody). Components ask for one each
    /// step (see MovementComponent.Posture); the mode keeps the character in it where there's room (see
    /// MovementController.SetPosture). Its feet stay where they are: the character's origin is the bottom of its body
    /// however it's held, what it would stand (or lie) on. A low posture is a share of the character's standing height, so
    /// it holds through changes of size.
    /// </summary>
    public readonly struct BodyPosture : IEquatable<BodyPosture>
    {
        // Postures this close are the same (a share of the standing height).
        private const float Tolerance = 1e-5f;

        /// <summary>How the body is held.</summary>
        public BodyPostureKind Kind { get; }

        /// <summary>Low, the share of its standing height the body keeps (from the feet up); otherwise 1.</summary>
        public float Share { get; }

        private BodyPosture(BodyPostureKind kind, float share)
        {
            Kind = kind;
            Share = share;
        }

        /// <summary>Upright, as authored.</summary>
        public static BodyPosture Standing => new BodyPosture(BodyPostureKind.Standing, 1f);

        /// <summary>Lowered to the given share of the standing height (at least a tenth of it), the feet staying put.</summary>
        public static BodyPosture Low(float heightShare) =>
            new BodyPosture(BodyPostureKind.Low, Math.Clamp(heightShare, 0.1f, 1f));

        /// <summary>Laid flat along the facing, resting on the feet.</summary>
        public static BodyPosture Lying => new BodyPosture(BodyPostureKind.Lying, 1f);

        /// <summary>Whether the body is upright, as authored.</summary>
        public bool IsStanding => Kind == BodyPostureKind.Standing;

        /// <summary>
        /// Whether this is lower than other while staying upright (standing to low, or lower still), which always has room:
        /// the body only loses its top.
        /// </summary>
        public bool IsLowerThan(BodyPosture other) =>
            Kind == BodyPostureKind.Low && (other.Kind == BodyPostureKind.Standing || (other.Kind == BodyPostureKind.Low && Share < other.Share));

        public bool Equals(BodyPosture other) => Kind == other.Kind && Math.Abs(Share - other.Share) < Tolerance;
        public override bool Equals(object obj) => obj is BodyPosture other && Equals(other);
        public override int GetHashCode() => (int)Kind;
        public override string ToString() => Kind == BodyPostureKind.Low ? $"Low {Share:0.###}" : Kind.ToString();
    }
}
