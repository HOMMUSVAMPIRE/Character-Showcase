using System.Collections.Generic;
using System.Linq;

namespace Character
{
    /// <summary>
    /// What a character can do, as it's tuned and sized: the limits of its movement, in metres (and degrees) at its size.
    /// Worked out by MovementController.Capabilities, from the settings where they're exact by construction (the step
    /// height, the jump heights) and otherwise by running a copy of the character's own movement and measuring what it
    /// does (see MovementSimulation), so they can't drift from the movement. A value is NaN where the character can't do
    /// that at all (wall-running turned off, say).
    ///
    /// Levels are held to these (the levelspec tool's movement checks read them from Tools/LevelSpec/envelope.json, which
    /// is written from them: see MovementEnvelopeWriter).
    /// </summary>
    public sealed class MovementCapabilities
    {
        /// <summary>A move and how far it reaches: a gap it crosses, or a height it climbs.</summary>
        public readonly struct Move
        {
            public Move(string name, float reach)
            {
                Name = name;
                Reach = reach;
            }

            /// <summary>What the move is ("sprint jump", "jump to the ledge").</summary>
            public string Name { get; }
            /// <summary>How far (m) it reaches: across a gap at the same height, or up.</summary>
            public float Reach { get; }

            public override string ToString() => $"{Name}: {Reach:0.00} m";
        }

        /// <summary>The tallest step (m) the character steps up by itself (MovementController's Max Step Height, at its size).</summary>
        public float StepHeight { get; internal set; } = float.NaN;
        /// <summary>The steepest slope (degrees) it treats as walkable ground (MovementController's Max Slope Angle).</summary>
        public float WalkableSlopeAngle { get; internal set; } = float.NaN;
        /// <summary>How tall (m) the character stands: what it needs to stand under something.</summary>
        public float StandHeight { get; internal set; } = float.NaN;
        /// <summary>How tall (m) its body is crouched: what it needs to crouch under something (NaN without crouching).</summary>
        public float CrouchHeight { get; internal set; } = float.NaN;
        /// <summary>How far (m) its body reaches to either side: half the width it needs to pass between things.</summary>
        public float HalfWidth { get; internal set; } = float.NaN;
        /// <summary>How high (m) its feet rise on the shortest jump (a tap).</summary>
        public float MinJumpHeight { get; internal set; } = float.NaN;
        /// <summary>How high (m) its feet rise on the highest jump (Jump held).</summary>
        public float MaxJumpHeight { get; internal set; } = float.NaN;
        /// <summary>How far (m) a slide carries it, from a sprint (NaN without sliding).</summary>
        public float SlideLength { get; internal set; } = float.NaN;
        /// <summary>How far (m) a roll carries it (NaN without rolling).</summary>
        public float RollLength { get; internal set; } = float.NaN;
        /// <summary>How far (m) along a wall a full wall run carries it (NaN without wall-running).</summary>
        public float WallRunLength { get; internal set; } = float.NaN;
        /// <summary>How far (m) it falls during a full wall run (NaN without wall-running).</summary>
        public float WallRunDrop { get; internal set; } = float.NaN;

        /// <summary>The lists behind Jumps and Climbs, filled by MovementController.Capabilities.</summary>
        internal readonly List<Move> jumps = new(), climbs = new();

        /// <summary>The gaps (at the same height) its moves cross, shortest first: "jump", "sprint jump", "sprint jump and air dash", ...</summary>
        public IReadOnlyList<Move> Jumps => jumps;
        /// <summary>The heights its moves climb, lowest first: "step up", "jump up", "jump to the ledge", ...</summary>
        public IReadOnlyList<Move> Climbs => climbs;

        /// <summary>The widest gap it can cross, any way (m); 0 if it can't jump.</summary>
        public float FarthestJump => jumps.Count > 0 ? jumps.Max(j => j.Reach) : 0f;
        /// <summary>The highest it can climb, any way (m).</summary>
        public float HighestClimb => climbs.Count > 0 ? climbs.Max(c => c.Reach) : 0f;

        /// <summary>The first (least) of its moves that crosses a gap this wide, or null if none does.</summary>
        public Move? MoveAcross(float gap) => FirstReaching(jumps, gap);
        /// <summary>The first (least) of its moves that climbs this high, or null if none does.</summary>
        public Move? MoveUp(float height) => FirstReaching(climbs, height);

        private static Move? FirstReaching(List<Move> moves, float distance)
        {
            foreach (Move move in moves.OrderBy(m => m.Reach))
            {
                if (distance <= move.Reach + 1e-4f) { return move; }
            }
            return null;
        }

        /// <summary>Adds a move, kept in order of reach; a move that couldn't be measured (NaN, or no reach) isn't added.</summary>
        internal static void Add(List<Move> moves, string name, float reach)
        {
            if (float.IsNaN(reach) || reach <= 0f) { return; }
            moves.Add(new Move(name, reach));
            moves.Sort((a, b) => a.Reach.CompareTo(b.Reach));
        }

        public override string ToString() =>
            $"step {StepHeight:0.00} m, slopes to {WalkableSlopeAngle:0}°, stands {StandHeight:0.00} m, crouches {CrouchHeight:0.00} m, " +
            $"jumps {MinJumpHeight:0.00}-{MaxJumpHeight:0.00} m high; jumps: {string.Join(", ", jumps)}; climbs: {string.Join(", ", climbs)}; " +
            $"slide {SlideLength:0.00} m, roll {RollLength:0.00} m, wall run {WallRunLength:0.00} m falling {WallRunDrop:0.00} m";
    }
}
