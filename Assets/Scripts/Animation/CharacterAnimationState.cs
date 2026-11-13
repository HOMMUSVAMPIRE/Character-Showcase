using UnityEngine;

namespace Character
{
    /// <summary>
    /// What a character's animations need to know about how they're moving, worked out from the movement each frame.
    /// Animation code reads this rather than the movement system's own data, so models, animators and IK don't depend
    /// on how movement is implemented, and movement can change without breaking them.
    /// </summary>
    public struct CharacterAnimationState
    {
        // World space velocity, in m/s
        public Vector3 velocity;
        // Velocity relative to the way the character faces: x = right, y = up, z = forward
        public Vector3 localVelocity;
        public float horizontalSpeed;
        // Vertical speed in m/s, positive rising: in the air, that is. 0 while touching the ground, where vertical
        // movement is only following the ground (up stairs, down slopes). See velocity for the movement itself.
        public float verticalSpeed;
        // Yaw turn rate in degrees per second (positive = turning right)
        public float turnSpeed;
        // The way the character faces (yaw only)
        public Quaternion facing;

        // Whether the character should be animated as on the ground. Stays true for a moment after leaving the ground
        // without jumping, so stepping off small ledges and bumps doesn't flash the falling animation.
        public bool grounded;
        // Whether the character is actually touching the ground this frame
        public bool touchingGround;
        // Seconds since the character last touched the ground (0 while touching it)
        public float timeInAir;
        public bool moving;
        public bool jumping;
        public bool dashing;
        // Crouched (not counting the low stance of a slide or roll). While rolling, whether the roll ends crouched (it
        // started crouched), so its animation blends into crouching over its end, not into standing and back down.
        public bool crouching;
        public bool sliding;
        public bool rolling;
        // How far through the roll (0 to 1) while rolling, and through the climb onto a ledge while climbing
        public float rollProgress;
        public float climbProgress;
        public bool wallRunning;
        // Which side the wall is on during a wall run: 1 = right, -1 = left, 0 when not wall-running
        public float wallSide;
        // Hanging from a ledge, and climbing up from one onto it
        public bool hanging;
        public bool climbing;
        // Vaulting over something low, and about how far through the vault (0 to 1)
        public bool vaulting;
        public float vaultProgress;
        // Stepping up onto something low and deep (its edge in ledgeEdge and ledgeNormal), and how far (m, 0 or
        // negative) the model is being held below the character as it eases up after the step's quick rise: 0 once
        // it has caught up (see AnimationController).
        public bool steppingUp;
        public float stepUpLag;
        // While hanging or climbing: the point on the ledge's edge the character holds (on its top, in front of the
        // character), and the wall's normal below it (horizontal, pointing toward the character). While vaulting, the
        // same for the edge vaulted over (the normal toward where the vault started). Zero otherwise.
        public Vector3 ledgeEdge;
        public Vector3 ledgeNormal;
        // Sliding down a wall, or climbing a climbable surface (localVelocity then gives the climb: x to the right, y up):
        // a point on the wall's face in front of the character, and its normal (horizontal, pointing toward the
        // character). Zero when doing neither.
        public bool wallSliding;
        public bool wallClimbing;
        // Climbing, stepping onto the surface from the ground or off its bottom onto the ground (the character holds
        // still meanwhile), and how far through it (0 to 1; 0 otherwise)
        public bool climbEntering;
        public bool climbExiting;
        public float climbStepProgress;
        // Climbing a ladder (wallClimbing is set too), and the ladder: where its rungs are, for placing hands and feet on them
        public bool onLadder;
        public LadderPose ladder;
        public Vector3 wallPoint;
        public Vector3 wallNormal;
        // Holding an object to push or pull (localVelocity.z is then positive pushing, negative pulling)
        public bool grabbing;
        // Swinging on a rope, and riding a zipline. While doing either: where the hands hold the line (the rope or the
        // cable; as it's drawn this frame), and the line's direction there (unit length): up the rope, or along the cable
        // the way the character rides. Zero otherwise.
        public bool swinging;
        public bool ziplining;
        public Vector3 gripPoint;
        public Vector3 gripDirection;
        /// <summary>The character's scale (x width, y height, z depth; 1, 1, 1 as authored). See ACCharacterController.SetScale.</summary>
        public Vector3 scale;
        /// <summary>
        /// What the character's speeds are multiplied by at its size (see MovementController.SpeedScale; 1 as authored).
        /// Speeds divided by it are as the character would move at its authored size.
        /// </summary>
        public float speedScale;

        /// <summary>
        /// How much longer than as authored the model's strides are in the direction it's moving: its scale along that
        /// direction (relative to its facing). A model stretched to twice the depth takes strides twice as long forward.
        /// </summary>
        public float StrideScale
        {
            get
            {
                Vector3 s = scale == Vector3.zero ? Vector3.one : scale;
                Vector2 direction = new Vector2(localVelocity.x, localVelocity.z);
                if (direction.sqrMagnitude < 1e-6f) { return s.z; } // Standing still: strides would be forward.
                direction.Normalize();
                return new Vector2(s.x * direction.x, s.z * direction.y).magnitude;
            }
        }
    }

    /// <summary>
    /// A ladder being climbed, in world space, as animation needs it: where its rungs are. Its rung k is at
    /// frontBottom + up * (k + 1) * rungSpacing, a rung's thickness behind the front face, spanning its width.
    /// </summary>
    public struct LadderPose
    {
        // The middle of the bottom of its front face, and its directions: up it, across it (its own right), and out of
        // its front (toward the climber)
        public Vector3 frontBottom;
        public Vector3 up;
        public Vector3 right;
        public Vector3 forward;
        public float height;
        public float width;
        public float rungSpacing;
        public float rungSize;
        public float depth;
        public int rungCount;

        /// <summary>Where rung k's middle is (its centre, between the rails), offset across it by side metres.</summary>
        public Vector3 Rung(int k, float side) =>
            frontBottom + up * ((k + 1) * rungSpacing) + right * side - forward * (0.5f * depth);

        /// <summary>How high a point is up the ladder from its bottom.</summary>
        public float HeightOf(Vector3 point) => Vector3.Dot(point - frontBottom, up);
    }

    /// <summary>
    /// Something that provides a character's animation state, such as the AnimationController. Components on a model
    /// (like FootPlacementIK) find one in their parents, so they work on any character without knowing its setup.
    /// </summary>
    public interface ICharacterAnimationStateSource
    {
        CharacterAnimationState AnimationState { get; }
    }
}
