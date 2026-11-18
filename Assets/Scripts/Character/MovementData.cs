using UnityEngine;

namespace Character
{
    /// <summary>
    /// A container for information on a character's movement in a frame
    /// </summary>
    [System.Serializable]
    public class MovementData
    {
        // The look this step's inputs are based on
        private Quaternion _look;
        /// <summary>The look's yaw alone (turned flat onto the ground plane): what move inputs are turned by.</summary>
        public Quaternion lookFlat { get; private set; }
        /// <summary>The way the look faces, pitch included.</summary>
        public Vector3 lookForward { get; private set; }

        /// <summary>
        /// The look this step's inputs are based on (its input frame's: CharacterInputFrame.Look), which the move input is
        /// relative to. Setting it sets lookFlat and lookForward.
        /// </summary>
        public Quaternion look
        {
            get { return _look; } set { _look = value; lookFlat = Quaternion.Euler(0, value.eulerAngles.y, 0); lookForward = value * Vector3.forward; }
        }

        /// <summary>
        /// The time step, in seconds, to use in movement calculations (usually Time.fixedDeltaTime, but could
        /// theoretically be run in an update loop).
        /// </summary>
        public float deltaTime { get; set; }


        /// <summary>
        /// Seconds since the character was last on the ground (0 while grounded). Drives coyote time, the coyote
        /// gravity curve and the air steering curve.
        /// </summary>
        public float timeSinceGrounded = 0f;

        /// <summary>The state of the character at the end of the previous physics step (Idle on the first).</summary>
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
        /// <summary>The character's position (the Rigidbody's) at the start of the step, after it was carried by its ground.</summary>
        public Vector3 initialPosition { get { return _initialPosition; } set { _initialPosition = value; dirty = true; } }
        /// <summary>The character's rotation (the Rigidbody's) at the start of the step.</summary>
        public Quaternion initialRotation { get { return _initialRotation; } set { _initialRotation = value; dirty = true; } }
        /// <summary>
        /// The momentum, in m/s, the character carries into the step from the last one (its final inertia): what keeps
        /// it moving through the air, as opposed to walking, which is applied directly each step.
        /// </summary>
        public Vector3 initialInertia { get { return _initialInertia; } set { _initialInertia = value; dirty = true; } }
        /// <summary>The character's velocity, in m/s, at the end of the last step (its final velocity).</summary>
        public Vector3 initialVelocity { get { return _initialVelocity; } set { _initialVelocity = value; dirty = true; } }
        /// <summary>The transform the character is standing on at the start of the step, or null in the air.</summary>
        public Transform initialGroundTransform { get { return _initialGroundTransform; } set { _initialGroundTransform = value; dirty = true; } }
        /// <summary>Whether the character is on the ground at the start of the step.</summary>
        public bool initialGrounded { get { return _initialGrounded; } set { _initialGrounded = value; dirty = true; } }
        /// <summary>
        /// A movement, in m, applied this step only and not carried into the next as momentum (walking, for one).
        /// </summary>
        public Vector3 directMovementVector { get { return _directMovementVector; } set { _directMovementVector = value; dirty = true; } }
        /// <summary>A change, in m/s, to the character's momentum this step (gravity, a jump, a force added between steps).</summary>
        public Vector3 velocityChange { get { return _velocityChange; } set { _velocityChange = value; dirty = true; } }
        /// <summary>
        /// The character's momentum, in m/s, this step, replacing its inertia and velocity change entirely (an ability
        /// moving the character along its own path), or null for none.
        /// </summary>
        public Vector3? velocityOverride { get { return _velocityOverride; } set { _velocityOverride = value; dirty = true; } }
        /// <summary>The rotation the character ends the step at.</summary>
        public Quaternion targetRotation { get { return _targetRotation; } set { _targetRotation = value; dirty = true; } }

        /// <summary>The state of the character during this step, built up by the movement mode.</summary>
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

        // Accessors to the above final variables. The get accessor re-computes all results if the movement data object is dirty
        /// <summary>Where the character ends the step: its initial position plus the final movement vector.</summary>
        public Vector3 finalPosition { get { ComputeResults();  return _finalPosition; } private set { _finalPosition = value; } }
        /// <summary>How far, in m, the character moves this step: its final inertia over the step, plus the direct movement.</summary>
        public Vector3 finalMovementVector { get { ComputeResults(); return _finalMovementVector; } private set { _finalMovementVector = value; } }
        /// <summary>The rotation the character ends the step at (the target rotation).</summary>
        public Quaternion finalRotation { get { ComputeResults(); return _finalRotation; } private set { _finalRotation = value; } }
        /// <summary>
        /// The momentum, in m/s, the character carries into the next step: the velocity override if there is one, else
        /// its initial inertia plus the velocity change.
        /// </summary>
        public Vector3 finalInertia { get { ComputeResults(); return _finalInertia; } private set { _finalInertia = value; } }
        /// <summary>The character's velocity, in m/s, this step: its final inertia plus the direct movement over the step.</summary>
        public Vector3 finalVelocity { get { ComputeResults(); return _finalVelocity; } private set { _finalVelocity = value; } }

        /// <summary>The transform on which the character will be standing at the end of the step.</summary>
        public Transform finalGroundTransform;
        /// <summary>Whether the character will be on the ground at the end of the step.</summary>
        public bool finalGrounded;

        /// <summary>
        /// Which sides of the character collision blocked this step's movement (Above = ceiling, Below = floor or a
        /// steep slope, Sides = walls).
        /// </summary>
        public CollisionFlags collisions;
        /// <summary>Which sides of the character collision blocked the last step's movement (its collisions).</summary>
        public CollisionFlags previousCollisions;

        /// <summary>
        /// What happened to the character this step. Raised by the MovementController once the step's movement is done.
        /// Not saved (Unity can't save an enum this wide, and it's only this step's).
        /// </summary>
        [System.NonSerialized] public MovementEvents events;

        /// <summary>
        /// How far (m) collision stepped the character up onto a step (positive) or dropped it down onto one (negative)
        /// this step, at once: walking up or down stairs, a step's whole height in a physics step. For the model to be
        /// eased over it (see AnimationController), rather than jumping with the character. 0 otherwise.
        /// </summary>
        public float stepChange;

        /// <summary>What collision did to the character this step (see StepResolution): for seeing why it moved as it did.</summary>
        public StepResolution resolution;

        /// <summary>How far (degrees, positive nose up) the character's body was pitched at the start of the step (see MovementController.BodyPitch).</summary>
        public float initialPitch;

        /// <summary>
        /// How far (degrees, positive nose up) the character's body is to be pitched about its feet by the end of the step:
        /// level (0) unless a component holds it pitched each step (swimming underwater, along the way it swims; see
        /// FreeSteering). Like a turn, it pitches only as far as the body has room to (see
        /// MovementController.CheckMovementValid).
        /// </summary>
        public float targetPitch;

        /// <summary>
        /// How far (m) below where its animations draw it the character's body is this step, for the model to be drawn
        /// lowered to it (see AnimationController): underwater, the body lies on its feet, but the swimming animations
        /// float it the swim depth above them, as they do treading water at the surface. 0 otherwise.
        /// </summary>
        public float modelDrop;

        /// <summary>
        /// Puts every field back as a new MovementData has it, so one object can be reused step after step without
        /// allocating (see MovementController.StartStep). A field added to this class must be reset here too:
        /// MovementDataTests fails if any isn't.
        /// </summary>
        public void Reset()
        {
            _look = default;
            lookFlat = default;
            lookForward = default;
            deltaTime = 0f;
            timeSinceGrounded = 0f;
            previousState = default;
            _initialPosition = default;
            _initialRotation = default;
            _initialInertia = default;
            _initialVelocity = default;
            _initialGroundTransform = null;
            _initialGrounded = false;
            _directMovementVector = default;
            _velocityChange = default;
            _velocityOverride = null;
            _targetRotation = default;
            state = default;
            dirty = true;
            computing = false;
            _finalPosition = default;
            _finalMovementVector = default;
            _finalRotation = default;
            _finalInertia = default;
            _finalVelocity = default;
            finalGroundTransform = null;
            finalGrounded = false;
            collisions = default;
            previousCollisions = default;
            events = default;
            stepChange = 0f;
            resolution = default;
            initialPitch = 0f;
            targetPitch = 0f;
            modelDrop = 0f;
        }

        /// <summary>
        /// Calculates the final state of the character based on the starting data and the intended motion data (only if
        /// something changed since it last did: the final values' accessors call it).
        /// </summary>
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

        /// <summary>Forces the final movement vector to be a specific value - used to account for collisions.</summary>
        public void ForceMovementVector(Vector3 moveVector)
        {
            directMovementVector += moveVector - finalMovementVector;
        }

        /// <summary>
        /// Forces the final movement vector and the momentum the character carries into the next step. Collisions use
        /// this to remove the momentum they blocked, so it doesn't build up against a ceiling or wall and fling the
        /// character once they move clear of it.
        /// </summary>
        public void ForceMovementVector(Vector3 moveVector, Vector3 inertia)
        {
            if (velocityOverride.HasValue) { velocityOverride = inertia; }
            else { velocityChange += inertia - finalInertia; }
            ForceMovementVector(moveVector);
        }
    }
}
