using UnityEngine;

namespace Character
{
    /// <summary>
    /// A character controller component that acts as an interface to the animator component
    /// Very much still a WIP
    /// </summary>
    public class AnimationController : MonoBehaviour
    {
        [SerializeField] [Tooltip("A reference to the parent character controller")]
        private ACCharacterController characterController;

        [SerializeField] [Tooltip("A reference to the character model's root transform that is a child of this transform")]
        private GameObject characterModelRoot;
        [SerializeField] [Tooltip("A reference to the animator component on the character model")]
        private Animator animator;
        [SerializeField] [Tooltip("A reference to the IK component on the character model, which layers IK targets for the hands, feet, elbows and knees over the animations")]
        private CharacterIK ik;

        /// <summary>IK for the character's hands, feet, elbows and knees, blended over its animations. May be null.</summary>
        public CharacterIK IK => ik;

        // Animator parameter names, shared with the editor tooling that builds the Animator Controller.
        public const string SpeedParameter = "Speed";
        public const string VerticalSpeedParameter = "VerticalSpeed";
        public const string GroundedParameter = "Grounded";
        public const string DashingParameter = "Dashing";
        private static readonly int SpeedId = Animator.StringToHash(SpeedParameter);
        private static readonly int VerticalSpeedId = Animator.StringToHash(VerticalSpeedParameter);
        private static readonly int GroundedId = Animator.StringToHash(GroundedParameter);
        private static readonly int DashingId = Animator.StringToHash(DashingParameter);

        [Header("Animation Variables")]
        [SerializeField] [Tooltip("Seconds over which the speed driving the locomotion blend is smoothed, so starting and stopping ease between clips")]
        private float speedDampTime = 0.1f;
        [SerializeField] [Tooltip("Seconds the character must be off the ground before the airborne animation plays (unless they jumped), so stepping off small ledges or bumps doesn't flash the falling pose")]
        private float airborneDelay = 0.12f;
        // How long the character has been off the ground
        private float airTime = 0f;

        [Header("Smooth Rotation Variables")]
        [SerializeField] [Tooltip("The speed at which the character model smoothly rotates to match the parent object's rotation")]
        private float rotationFixSpeed = 10f;
        // The last known world space rotation of the character model
        private Quaternion lastRotation = Quaternion.identity;

        /// <summary>
        /// Feeds the character's latest movement into the animator: horizontal speed drives the locomotion blend
        /// (idle / walk / run), and grounded state plus vertical speed drive the airborne blend (rise / fall).
        /// Call this every rendered frame with the most recently completed movement data.
        /// </summary>
        /// <param name="movementData">The latest completed movement data, or null if none yet</param>
        /// <param name="deltaTime"></param>
        public void UpdateAnimator(MovementData movementData, float deltaTime)
        {
            if (animator == null || movementData == null) { return; }

            Vector3 velocity = movementData.finalVelocity;
            float horizontalSpeed = new Vector2(velocity.x, velocity.z).magnitude;

            bool grounded = movementData.state.HasFlag(CharacterState.Grounded);
            bool jumping = movementData.state.HasFlag(CharacterState.Jumping);
            airTime = grounded ? 0f : airTime + deltaTime;
            bool showGrounded = grounded || (!jumping && airTime < airborneDelay);

            animator.SetFloat(SpeedId, horizontalSpeed, speedDampTime, deltaTime);
            animator.SetFloat(VerticalSpeedId, velocity.y);
            animator.SetBool(GroundedId, showGrounded);
            animator.SetBool(DashingId, movementData.state.HasFlag(CharacterState.Dashing));
        }

        /// <summary>
        /// Rotates the visual representation of the character smoothly to match the current facing direction
        /// this allows the character's movement to feel snappy without the character's visuals looking unnatural when rapidly changing direction.
        /// Call this every rendered frame (not from FixedUpdate), so the rotation updates smoothly rather than in physics-rate steps.
        /// </summary>
        /// <param name="deltaTime"></param>
        /// <param name="groundRotation">Rotation applied to the whole character by the ground they stand on since the
        /// last call (e.g. a spinning platform). The visuals take it on immediately instead of smoothing toward it, so
        /// they turn with the platform rather than lagging behind.</param>
        public void SmoothRotateVisuals(float deltaTime, Quaternion groundRotation)
        {
            lastRotation = groundRotation * lastRotation;
            // Exponential smoothing keeps the turn speed the same regardless of frame rate.
            float t = 1f - Mathf.Exp(-rotationFixSpeed * deltaTime);
            characterModelRoot.transform.rotation = Quaternion.Slerp(lastRotation, transform.rotation, t);
            lastRotation = characterModelRoot.transform.rotation;
        }
    }
}
