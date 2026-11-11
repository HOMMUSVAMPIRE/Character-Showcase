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
        /// <summary>The character's scale (x width, y height, z depth; 1, 1, 1 as authored). See ACCharacterController.SetScale.</summary>
        public Vector3 scale;

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
    /// Something that provides a character's animation state, such as the AnimationController. Components on a model
    /// (like FootPlacementIK) find one in their parents, so they work on any character without knowing its setup.
    /// </summary>
    public interface ICharacterAnimationStateSource
    {
        CharacterAnimationState AnimationState { get; }
    }
}
