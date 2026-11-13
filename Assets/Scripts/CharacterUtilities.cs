using UnityEngine;

namespace Character
{
    /// <summary>
    /// A home for any static utility functions related to character operation here
    /// </summary>
    public static class CharacterUtilities
    {
        /// <summary>
        /// Where the animation puts an IK goal, in world space. Call only while the Animator's IK API is usable (from
        /// OnAnimatorIK). Unity's humanoid IK reports foot goals as if the model were scaled evenly by its width (x)
        /// scale, so for a model stretched along one axis (taller, say) a foot goal's height is off by the difference: a
        /// model stretched to twice its height has its foot goals reported at half their height above its origin. (Hand
        /// goals are reported where they are.) SetIKPosition takes true world positions, so a foot goal placed from a
        /// reported one (a foot planted relative to it, or a goal aimed at it to let a knee hint apply) would be lifted
        /// or sunk. This puts the height right.
        /// </summary>
        public static Vector3 AnimatedIKGoalPosition(Animator animator, AvatarIKGoal goal)
        {
            Vector3 position = animator.GetIKPosition(goal);
            if (goal != AvatarIKGoal.LeftFoot && goal != AvatarIKGoal.RightFoot) { return position; }
            Transform root = animator.transform;
            Vector3 scale = root.lossyScale;
            if (Mathf.Abs(scale.x) < 1e-6f || Mathf.Abs(scale.y - scale.x) <= 1e-5f * Mathf.Abs(scale.x)) { return position; }
            Vector3 local = Quaternion.Inverse(root.rotation) * (position - root.position);
            local.y *= scale.y / scale.x;
            return root.position + root.rotation * local;
        }

        /// <summary>
        /// The position to give SetIKPosition for a hand or foot to land at the given world position. Unity solves a
        /// limb's goal relative to where the limb starts (a foot's from its hip joint, a hand's from its collarbone), and
        /// on a model stretched along one axis it moves the hand or foot further from there, across and along the model,
        /// than it's asked to: by the model's width (x) and depth (z) scale over its height (y) scale. On a model
        /// stretched to twice its depth, a goal 5 cm in front of the hip puts the foot 10 cm in front; on one twice as
        /// wide, a hand reaching across misses by tens of centimetres. This scales those offsets back. The limb's start
        /// is taken where it was last posed, as it can't be known this frame until the IK is done; it moves little from
        /// frame to frame, and only that movement, times the stretch, matters.
        /// </summary>
        public static Vector3 IKGoalPositionToSet(Animator animator, AvatarIKGoal goal, Vector3 position)
        {
            Transform root = animator.transform;
            Vector3 scale = root.lossyScale;
            if (Mathf.Abs(scale.x) < 1e-6f || Mathf.Abs(scale.z) < 1e-6f
                || (Mathf.Abs(scale.x - scale.y) <= 1e-5f * Mathf.Abs(scale.y) && Mathf.Abs(scale.z - scale.y) <= 1e-5f * Mathf.Abs(scale.y)))
            {
                return position;
            }
            Transform start = LimbStart(animator, goal);
            if (start == null) { return position; }
            Vector3 fromStart = Quaternion.Inverse(root.rotation) * (position - start.position);
            fromStart.x *= scale.y / scale.x;
            fromStart.z *= scale.y / scale.z;
            return start.position + root.rotation * fromStart;
        }

        // Where Unity's IK takes a limb to start: the hip joint, or the collarbone (the upper arm, for a rig without one).
        private static Transform LimbStart(Animator animator, AvatarIKGoal goal)
        {
            switch (goal)
            {
                case AvatarIKGoal.LeftFoot: return animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
                case AvatarIKGoal.RightFoot: return animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
                case AvatarIKGoal.LeftHand:
                    Transform left = animator.GetBoneTransform(HumanBodyBones.LeftShoulder);
                    return left != null ? left : animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
                default:
                    Transform right = animator.GetBoneTransform(HumanBodyBones.RightShoulder);
                    return right != null ? right : animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            }
        }
    }
    
    /// <summary>
    /// An enum which stores animation-relevant stateful information about a character
    /// </summary>
    [System.Flags]
    public enum CharacterState
    {
        Idle = 0,
        Moving = 1,
        Grounded = 2,
        Jumping = 4,
        Dashing = 8,
        Running = 16,
        Dead = 32,
        // Crouched (low and slow), not counting the low stance of a slide or roll
        Crouching = 64,
        Sliding = 128,
        Rolling = 256,
        WallRunning = 512,
        // Hanging from a ledge
        LedgeHanging = 1024,
        // Climbing up from a ledge onto it
        LedgeClimbing = 2048,
        // Holding a large object to push or pull it
        Grabbing = 4096,
        // Sliding slowly down a wall it ran into head-on
        WallSliding = 8192,
        // Vaulting over something too low to grab and too high to step onto, caught in mid-air
        Vaulting = 16384,
        // Stepping up onto the top of something low and deep, reached from mid-air or sprinting (see Vaulting)
        SteppingUp = 32768,
        // Climbing a climbable surface (see Level.ClimbableSurface), moving over it in any direction
        WallClimbing = 65536,
    }

    /// <summary>
    /// Things that can happen to a character during a physics step, for animation, effects and sound to react to.
    /// Movement modes report the ones they cause (jumps and dashes); the MovementController detects the rest.
    /// </summary>
    [System.Flags]
    public enum MovementEvents
    {
        None = 0,
        // A jump from the ground, or just after walking off it (coyote time)
        Jumped = 1,
        // A jump in mid-air (a double jump)
        AirJumped = 2,
        // Touched down after being in the air
        Landed = 4,
        // Left the ground, by jumping or walking off an edge
        LeftGround = 8,
        DashStarted = 16,
        // A dash finished, or was cancelled into a jump
        DashEnded = 32,
        // Started touching a ceiling
        HitCeiling = 64,
        // Started touching a wall
        HitWall = 128,
        // The starts and ends of the abilities below are found by the MovementController, from the character's state
        // (see MovementController.StateEvents), so every movement mode raises them alike.
        CrouchStarted = 256,
        CrouchEnded = 512,
        SlideStarted = 1024,
        SlideEnded = 2048,
        RollStarted = 4096,
        RollEnded = 8192,
        WallRunStarted = 16384,
        WallRunEnded = 32768,
        // Jumped off a wall during a wall run, wall slide or wall climb (reported by the movement mode)
        WallJumped = 65536,
        // Caught hold of a ledge in mid-air
        LedgeGrabbed = 131072,
        // Stopped hanging from a ledge: let go, or started climbing up
        LedgeReleased = 262144,
        LedgeClimbStarted = 524288,
        // Finished climbing up onto a ledge
        LedgeClimbEnded = 1048576,
        // Took hold of an object to push or pull
        GrabStarted = 2097152,
        // Let go of an object
        GrabEnded = 4194304,
        // Started or stopped sliding down a wall (jumping off one is a WallJumped)
        WallSlideStarted = 8388608,
        WallSlideEnded = 16777216,
        // Started or finished vaulting over something
        VaultStarted = 33554432,
        VaultEnded = 67108864,
        // Started or finished stepping up onto something low and deep
        StepUpStarted = 134217728,
        StepUpEnded = 268435456,
        // Took hold of a climbable surface, and let go of it (jumping off one is a WallJumped; climbing up over its top
        // onto a ledge is a LedgeClimbStarted)
        WallClimbStarted = 536870912,
        WallClimbEnded = 1073741824,
    }

    /// <summary>How a character's speeds change with its size. See MovementController.</summary>
    public enum MovementSizeScaling
    {
        /// <summary>Speeds and jump launch speed scale with the square root of height, and timings with it, while gravity
        /// stays the same, as for real animals.</summary>
        Natural = 0,
        /// <summary>Speeds stay as tuned at any size.</summary>
        Unchanged = 1,
        /// <summary>Speeds, accelerations and gravity all scale with height, and timings stay the same, so the character
        /// moves exactly like a scaled copy of itself: the same in relation to its size at any size.</summary>
        Proportional = 2,
    }

    /// <summary>
    /// A collection of inputs that are required for movement and character actions
    /// </summary>
    [System.Serializable]
    public class Inputs
    {
        public Vector3 movementVector;
        public InputButton jump;
        public InputButton dash;
        // Held to move at the movement mode's run speed instead of its walk speed. May be null (never sprinting).
        public InputButton sprint;
        // Crouches (or, pressed while sprinting, slides). May be null (never pressed), as may the buttons below.
        public InputButton crouch;
        // Starts a roll
        public InputButton roll;
        // Held to grab an object in front of the character, to push or pull it
        public InputButton grab;
    }

    /// <summary>
    /// A character's dash state, which persists between frames. Lives on the MovementController (one per character),
    /// since movement modes are shared assets.
    /// </summary>
    public class DashState
    {
        // Seconds left in the current dash (0 when not dashing)
        public float timeRemaining;
        // Seconds until another dash may start
        public float cooldownRemaining;
        // Air dashes used since last touching the ground
        public int airDashesUsed;
        // The direction of the current dash
        public Vector3 direction;
        // Whether the current press of the dash button has already started a dash, so holding it doesn't repeat
        public bool pressConsumed;

        public bool IsDashing => timeRemaining > 0f;
    }

    /// <summary>
    /// Stores data on a button to be used by the input manager
    /// </summary>
    public class InputButton
    {
        public bool value;
        public bool justPressed;
        public bool justReleased;
        public float holdDuration;
        public float held;
        public float lastHoldDuration;
    }

    /// <summary>
    /// A container for information on a character's movement in a frame
    /// </summary>
    [System.Serializable]
    public class MovementData
    {
        // The view that inputs this frame are based on
        private Quaternion _cameraLook;
        public Quaternion cameraLookFlat { get; private set; }
        public Vector3 cameraForward { get; private set; }

        public Quaternion cameraLook
        {
            get { return _cameraLook; } set { _cameraLook = value; cameraLookFlat = Quaternion.Euler(0, value.eulerAngles.y, 0); cameraForward = value * Vector3.forward; }
        }

        // The delta time variable to use in movement calculations (usually Time.fixedDeltaTime, but could theoretically be run in an update loop)
        public float deltaTime { get; set; }

        // A timestamp for when the character was last on the ground
        public float lastGroundedTime = 0f;

        // Seconds since the character was last on the ground (0 while grounded). Drives coyote time, the coyote
        // gravity curve and the air steering curve.
        public float timeSinceGrounded = 0f;

        // The state of the character on the previous frame
        public CharacterState previousState;

        // Data on the character's transform, velocity, and state at the start of the frame
        private Vector3 _initialPosition;
        private Quaternion _initialRotation;
        private Vector3 _initialInertia;
        private Vector3 _initialVelocity;
        private Transform _initialGroundTransform;
        private bool _initialGrounded;

        // Data on the way the character is attempting to move, accelerate, and rotate this frame
        private Vector3 _directMovementVector;
        private Vector3 _velocityChange;
        private Vector3? _velocityOverride;
        private Quaternion _targetRotation;

        // Accessors to the above variables. Using the set accessor marks the final results of the movement as dirty
        public Vector3 initialPosition { get { return _initialPosition; } set { _initialPosition = value; dirty = true; } }
        public Quaternion initialRotation { get { return _initialRotation; } set { _initialRotation = value; dirty = true; } }
        public Vector3 initialInertia { get { return _initialInertia; } set { _initialInertia = value; dirty = true; } }
        public Vector3 initialVelocity { get { return _initialVelocity; } set { _initialVelocity = value; dirty = true; } }
        public Transform initialGroundTransform { get { return _initialGroundTransform; } set { _initialGroundTransform = value; dirty = true; } }
        public bool initialGrounded { get { return _initialGrounded; } set { _initialGrounded = value; dirty = true; } }
        public Vector3 directMovementVector { get { return _directMovementVector; } set { _directMovementVector = value; dirty = true; } }
        public Vector3 velocityChange { get { return _velocityChange; } set { _velocityChange = value; dirty = true; } }
        public Vector3? velocityOverride { get { return _velocityOverride; } set { _velocityOverride = value; dirty = true; } }
        public Quaternion targetRotation { get { return _targetRotation; } set { _targetRotation = value; dirty = true; } }

        // The state of the character during this frame
        public CharacterState state;

        // Whether the final state of the character's transform at the end of the frame needs re-computation
        private bool dirty = true;
        private bool computing = false;

        // Data on the character's trandform and velocity at the end of the frame. This is the "goal" that is used to actually move the character
        private Vector3 _finalPosition;
        private Vector3 _finalMovementVector;
        private Quaternion _finalRotation;
        private Vector3 _finalInertia;
        private Vector3 _finalVelocity;

        // Accessors to the above final variables. The set accessor re-computes all results if the movement data object is dirty
        public Vector3 finalPosition { get { ComputeResults();  return _finalPosition; } private set { _finalPosition = value; } }
        public Vector3 finalMovementVector { get { ComputeResults(); return _finalMovementVector; } private set { _finalMovementVector = value; } }
        public Quaternion finalRotation { get { ComputeResults(); return _finalRotation; } private set { _finalRotation = value; } }
        public Vector3 finalInertia { get { ComputeResults(); return _finalInertia; } private set { _finalInertia = value; } }
        public Vector3 finalVelocity { get { ComputeResults(); return _finalVelocity; } private set { _finalVelocity = value; } }

        // The transform on which the character will be standing at the end of the frame
        public Transform finalGroundTransform;
        // Whether the character will be on the ground at the end of the frame
        public bool finalGrounded;

        // Which sides of the character collision blocked this frame's movement (Above = ceiling, Below = floor or a
        // steep slope, Sides = walls), and last frame's
        public CollisionFlags collisions;
        public CollisionFlags previousCollisions;

        // What happened to the character this frame. Raised by the MovementController once the frame's movement is done
        public MovementEvents events;

        // Calculates the final state of the character based on the starting data and the intended motion data
        public void ComputeResults()
        {
            if (computing || !dirty) { return; }

            computing = true;

            if (deltaTime == 0)
            {
                finalInertia = initialInertia;
                finalVelocity = initialVelocity;
                finalMovementVector = Vector3.zero;
                finalPosition = initialPosition;
                finalRotation = initialRotation;
                computing = false;
                dirty = false;
                return;
            }

            finalInertia = velocityOverride ?? initialInertia + velocityChange;
            finalVelocity = finalInertia  + (directMovementVector / deltaTime);
            finalMovementVector = (finalInertia * deltaTime) + directMovementVector;
            finalPosition = initialPosition + finalMovementVector;

            finalRotation = targetRotation;

            computing = false;
            dirty = false;
        }

        // Forces the final movement vector to be a specific value - used to account for collisions
        public void ForceMovementVector(Vector3 moveVector)
        {
            directMovementVector += moveVector - finalMovementVector;
        }

        // Forces the final movement vector and the momentum the character carries into the next frame. Collisions use
        // this to remove the momentum they blocked, so it doesn't build up against a ceiling or wall and fling the
        // character once they move clear of it.
        public void ForceMovementVector(Vector3 moveVector, Vector3 inertia)
        {
            if (velocityOverride.HasValue) { velocityOverride = inertia; }
            else { velocityChange += inertia - finalInertia; }
            ForceMovementVector(moveVector);
        }
    }
}
