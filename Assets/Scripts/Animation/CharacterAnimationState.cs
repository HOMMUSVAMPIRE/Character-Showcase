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
        /// <summary>World space velocity, in m/s.</summary>
        public Vector3 velocity;
        /// <summary>Velocity (m/s) relative to the way the character faces: x = right, y = up, z = forward.</summary>
        public Vector3 localVelocity;
        /// <summary>Speed across the ground (m/s): velocity without its vertical part.</summary>
        public float horizontalSpeed;
        /// <summary>
        /// Vertical speed in m/s, positive rising: in the air, that is. 0 while touching the ground, where vertical
        /// movement is only following the ground (up stairs, down slopes). See velocity for the movement itself.
        /// </summary>
        public float verticalSpeed;
        /// <summary>Yaw turn rate in degrees per second (positive = turning right).</summary>
        public float turnSpeed;
        /// <summary>The way the character faces (yaw only).</summary>
        public Quaternion facing;
        /// <summary>
        /// How far (degrees, positive nose up) the character's body is pitched about its feet, from level (see
        /// MovementController.BodyPitch): swimming underwater, along the way it swims. The model is tilted to match.
        /// </summary>
        public float bodyPitch;

        /// <summary>
        /// Whether the character should be animated as on the ground. Stays true for a moment after leaving the ground
        /// without jumping, so stepping off small ledges and bumps doesn't flash the falling animation.
        /// </summary>
        public bool grounded;
        /// <summary>Whether the character is actually touching the ground this frame.</summary>
        public bool touchingGround;
        /// <summary>Seconds since the character last touched the ground (0 while touching it).</summary>
        public float timeInAir;
        /// <summary>
        /// Whether the character is moving across the ground faster than the AnimationController's moving threshold
        /// (scaled with its size).
        /// </summary>
        public bool moving;
        /// <summary>Whether the character is in a jump.</summary>
        public bool jumping;
        /// <summary>Whether the character is dashing.</summary>
        public bool dashing;
        /// <summary>
        /// Crouched (not counting the low stance of a slide or roll). While rolling, whether the roll ends crouched (it
        /// started crouched), so its animation blends into crouching over its end, not into standing and back down.
        /// </summary>
        public bool crouching;
        /// <summary>Whether the character is sliding.</summary>
        public bool sliding;
        /// <summary>Whether the character is rolling.</summary>
        public bool rolling;
        /// <summary>How far through the roll (0 to 1) while rolling.</summary>
        public float rollProgress;
        /// <summary>How far through the climb onto a ledge (0 to 1) while climbing; 0 otherwise.</summary>
        public float climbProgress;
        /// <summary>Whether the character is running along a wall.</summary>
        public bool wallRunning;
        /// <summary>Which side the wall is on during a wall run: 1 = right, -1 = left, 0 when not wall-running.</summary>
        public float wallSide;
        /// <summary>Hanging from a ledge.</summary>
        public bool hanging;
        /// <summary>Climbing up from a ledge onto it.</summary>
        public bool climbing;
        /// <summary>
        /// Vaulting over something low: while the character vaults, and as its animation finishes after a fast one (see
        /// vaultProgress).
        /// </summary>
        public bool vaulting;
        /// <summary>
        /// How far through the vault (0 to 1) its animation is: the vault's progress, but no faster than plays it over
        /// the AnimationController's Vault Min Duration, so after a fast vault it finishes as the character runs on.
        /// </summary>
        public float vaultProgress;
        /// <summary>
        /// How far through the vault (0 to 1) the character itself is (1 once it's over), which vaultProgress can lag:
        /// where its body is, for placing the hand on the top (see LedgeHandIK).
        /// </summary>
        public float vaultBodyProgress;
        /// <summary>Stepping up onto something low and deep (its edge in ledgeEdge and ledgeNormal).</summary>
        public bool steppingUp;
        /// <summary>
        /// How far (m, 0 or negative) the model is being held below the character as it eases up after a step up's quick
        /// rise: 0 once it has caught up (see AnimationController).
        /// </summary>
        public float stepUpLag;
        /// <summary>
        /// While hanging or climbing: the point on the ledge's edge the character holds (on its top, in front of the
        /// character). While vaulting or stepping up, the same for the edge vaulted over or stepped onto. Zero otherwise.
        /// </summary>
        public Vector3 ledgeEdge;
        /// <summary>
        /// The wall's normal below ledgeEdge (horizontal): pointing toward the character while hanging or climbing,
        /// toward where it started while vaulting or stepping up. Zero otherwise.
        /// </summary>
        public Vector3 ledgeNormal;
        /// <summary>Sliding down a wall (see wallPoint and wallNormal).</summary>
        public bool wallSliding;
        /// <summary>
        /// Climbing a climbable surface or a ladder (see wallPoint and wallNormal); localVelocity then gives the climb:
        /// x to the right, y up.
        /// </summary>
        public bool wallClimbing;
        /// <summary>
        /// Climbing, and stepping onto the surface from the ground (the character holds still meanwhile; see
        /// climbStepProgress).
        /// </summary>
        public bool climbEntering;
        /// <summary>
        /// Climbing, and stepping off the surface's bottom onto the ground (the character holds still meanwhile; see
        /// climbStepProgress).
        /// </summary>
        public bool climbExiting;
        /// <summary>
        /// How far through stepping onto or off a climbed surface (0 to 1), held at 1 after a step, until the next begins
        /// (see WallClimbState.StepProgress).
        /// </summary>
        public float climbStepProgress;
        /// <summary>Climbing a ladder (wallClimbing is set too): see ladder.</summary>
        public bool onLadder;
        /// <summary>The ladder being climbed: where its rungs are, for placing hands and feet on them. Empty otherwise.</summary>
        public LadderPose ladder;
        /// <summary>
        /// While sliding down a wall, climbing or running along one: a point on the wall's face nearest the character.
        /// Zero otherwise.
        /// </summary>
        public Vector3 wallPoint;
        /// <summary>
        /// While sliding down a wall, climbing or running along one: the wall's normal (horizontal, pointing toward the
        /// character). Zero
        /// otherwise.
        /// </summary>
        public Vector3 wallNormal;
        /// <summary>Holding an object to push or pull (localVelocity.z is then positive pushing, negative pulling).</summary>
        public bool grabbing;
        /// <summary>Swinging on a rope (see gripPoint and gripDirection).</summary>
        public bool swinging;
        /// <summary>Riding a zipline (see gripPoint and gripDirection).</summary>
        public bool ziplining;
        /// <summary>Swimming at the surface of deep water (horizontalSpeed is how fast it swims).</summary>
        public bool swimming;
        /// <summary>Swimming under the surface (swimming is set too).</summary>
        public bool diving;
        /// <summary>
        /// While swinging or ziplining: where the hands hold the line (the rope or the cable; as it's drawn this frame).
        /// Zero otherwise.
        /// </summary>
        public Vector3 gripPoint;
        /// <summary>
        /// While swinging or ziplining: the line's direction at gripPoint (unit length): up the rope, or along the cable
        /// the way the character rides. Zero otherwise.
        /// </summary>
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
        /// <summary>The middle of the bottom of its front face.</summary>
        public Vector3 frontBottom;
        /// <summary>The way it's climbed (unit length).</summary>
        public Vector3 up;
        /// <summary>Across it, its own right (unit length): to the left of someone climbing it, who faces its front.</summary>
        public Vector3 right;
        /// <summary>Out of its front, toward the climber (unit length).</summary>
        public Vector3 forward;
        /// <summary>How tall it is (m): its rails' length.</summary>
        public float height;
        /// <summary>How wide it is (m), rail to rail.</summary>
        public float width;
        /// <summary>How far apart (m) the rungs are.</summary>
        public float rungSpacing;
        /// <summary>How thick (m) the rungs are.</summary>
        public float rungSize;
        /// <summary>How deep (m) it is: its rails' thickness.</summary>
        public float depth;
        /// <summary>How many rungs it has.</summary>
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
        /// <summary>The character's animation state as of its latest update.</summary>
        CharacterAnimationState AnimationState { get; }
    }
}
