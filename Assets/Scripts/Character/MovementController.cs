using UnityEngine;

namespace Character
{
    /// <summary>
    /// Moves a character: each physics step it runs its movement mode on the inputs (ProcessMovementInputs), resolves
    /// the result against the world (CheckMovementValid) and moves the Rigidbody. It holds the character's movement
    /// settings and its abilities' state. Split by area: its collider (MovementController.Capsule.cs), the ground
    /// (.Ground.cs), collision (.Collision.cs), the queries abilities use (.Queries.cs) and what it can do
    /// (.Capabilities.cs). Serialized fields all stay in this file, so the inspector keeps their order.
    /// </summary>
    public partial class MovementController : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("A reference to the parent character controller")]
        private ACCharacterController characterController;

        [SerializeField]
        [Tooltip("The rigidbody that drives this character's physics interactions and enables proper movement behaviour")]
        private Rigidbody rb;

        /// <summary>
        /// The camera the character's inputs are relative to, if not the main camera (an NPC's, say, or none: a movement
        /// simulation's own, which looks along +Z). Not saved.
        /// </summary>
        [System.NonSerialized] public Transform InputCamera;
        /// <summary>The camera the character's inputs are relative to: InputCamera, else the main camera; null for none (inputs in world space).</summary>
        public Transform cameraTransform => InputCamera != null ? InputCamera : (Camera.main != null ? Camera.main.transform : null);

        [Tooltip("The scriptable object that drives this character's movement")]
        public MovementMode movementHandler;

        /// <summary>
        /// Raised after each physics step, once for each event that happened during it (one flag at a time), with that
        /// step's movement data. Animation, effects and sound hook in here rather than watching the movement state.
        /// </summary>
        public event System.Action<MovementEvents, MovementData> MovementEventRaised;

        // The previous and current movement data information for this character
        [HideInInspector] public MovementData lastFrameMovementData = null;
        [HideInInspector] public MovementData currentMovementData = null;

        // A change in velocity to be applied to the character on the next update loop (like if the character gets thrown by an explosion)
        [HideInInspector] public Vector3 pendingVelocityChange;
        // A complete override on the character's velocity to be applied next frame
        public Vector3? velocityOverride = null;

        [Tooltip("A multiplier on the character's movement speed")]
        public float movementSpeedMultiplier;
        [Tooltip("A multiplier on the character's jump launch speed (jump heights scale with its square)")]
        public float jumpStrengthModifier;

        [Tooltip("The radius of the sphere cast used to determine whether the character is standing on the ground")]
        public float groundCheckRadius;

        [Tooltip("A mask which defines the physics layers which affect where the character can move and how")]
        public LayerMask movementLayerMask = 0;

        [Header("Collision")]
        [SerializeField]
        [Tooltip("The character's capsule collider — collision sweeps use its size. Auto-fetched in Awake if left empty.")]
        private CapsuleCollider capsule;
        [SerializeField]
        [Tooltip("Maximum height of a step or ledge the character can automatically step up (stairs).")]
        private float maxStepHeight = 0.35f;
        [SerializeField]
        [Tooltip("Maximum slope angle (degrees) the character treats as walkable ground.")]
        private float maxSlopeAngle = 55f;
        [SerializeField]
        [Tooltip("Small buffer kept between the character and surfaces to avoid jitter.")]
        private float skinWidth = 0.02f;
        [SerializeField]
        [Tooltip("Distance below the feet the character will probe to stick to descending slopes. Keep small — anything larger than the gap between stair treads feels like teleporting instead of falling when walking off a ledge.")]
        private float groundSnapDistance = 0.1f;

        [Header("Size")]
        [SerializeField]
        [Tooltip("How the character's movement changes with its size (see ACCharacterController.SetScale). Proportional: " +
                 "speeds, accelerations and gravity scale with height and timings stay the same, so a character of any " +
                 "size moves exactly like a scaled copy of itself: a jump lasts as long and goes as high for its size, " +
                 "and it crosses its own height in the same time. Natural: speeds scale with the square root of height " +
                 "and timings with it, while gravity stays the same, as for real animals: smaller characters look " +
                 "quicker for their size, and jumps take longer the bigger they are. Unchanged: speeds stay as tuned.")]
        private MovementSizeScaling sizeScaling = MovementSizeScaling.Proportional;

        /// <summary>The character's size, as set by ACCharacterController.SetScale (1, 1, 1 is as authored).</summary>
        public Vector3 Scale { get; private set; } = Vector3.one;
        /// <summary>The character's height relative to as authored: distances like step height scale with it.</summary>
        public float HeightScale => Scale.y;
        /// <summary>How the character's movement changes with its size.</summary>
        public MovementSizeScaling SizeScaling { get => sizeScaling; set => sizeScaling = value; }
        /// <summary>What speeds (and jump launch speed) are multiplied by at the character's size. See sizeScaling.</summary>
        public float SpeedScale => sizeScaling switch
        {
            MovementSizeScaling.Proportional => HeightScale,
            MovementSizeScaling.Natural => Mathf.Sqrt(HeightScale),
            _ => 1f,
        };
        /// <summary>What durations of size-dependent motion (such as a dash) are multiplied by at the character's size.</summary>
        public float DurationScale => sizeScaling == MovementSizeScaling.Natural ? Mathf.Sqrt(HeightScale) : 1f;
        /// <summary>
        /// What accelerations (gravity, air steering) are multiplied by at the character's size: speed per duration, so
        /// speeds build up and fall away over the same share of a jump or a stride at any size.
        /// </summary>
        public float AccelerationScale => SpeedScale / DurationScale;

        /// <summary>
        /// The character's smallest dimension relative to as authored: the lesser of its height and its collider's
        /// radius. Small clearances (the skin width) scale with it, so they stay as thin, for the character's size, on
        /// every side.
        /// </summary>
        private float ClearanceScale => hasBaseCapsule && baseRadius > 0f && capsule != null
            ? Mathf.Min(HeightScale, capsule.radius / baseRadius) : HeightScale;

        /// <summary>The gap kept between the character and surfaces, at its size.</summary>
        private float SkinWidth => skinWidth * ClearanceScale;

        // The capsule as authored, which the character's size scales.
        private float baseRadius, baseHeight;
        private Vector3 baseCenter;
        private bool hasBaseCapsule;

        // How far below the feet the floor can be while still counting as grounded. The ground snap always closes
        // this gap, so the character never hovers inside it.
        private const float BaseGroundedTolerance = 0.1f;
        // Scaled with height: at a tenth of the size, a fixed 0.1 m would be the character's whole height.
        private float GroundedTolerance => BaseGroundedTolerance * HeightScale;

        // How far from straight below the character's feet a step's edge may be when it lands on the step (see TryStepUp).
        private const float StepLandingAngle = 40f;

        /// <summary>The radius of the capsule used for collision sweeps (the capsule's, less the skin width).</summary>
        private float SweepRadius => Mathf.Max(0.01f * ClearanceScale, (capsule != null ? capsule.radius : 0.5f) - SkinWidth);

        /// <summary>The radius of the sphere CheckGrounded casts under the feet.</summary>
        private float GroundCheckRadius => groundCheckRadius > 0f ? groundCheckRadius * Mathf.Max(Scale.x, Scale.z)
            : (capsule != null ? capsule.radius * 0.9f : 0.45f); // 90%: 0.45 m on the 0.5 m capsule.

        /// <summary>The tallest step the character climbs at its size.</summary>
        private float StepHeight => maxStepHeight * HeightScale;

        // Passes made to push the character out of geometry that moved into them, for when several things overlap.
        private const int MaxDepenetrationPasses = 4;
        private readonly Collider[] overlaps = new Collider[16];
        // Reused for every ground check and collision sweep (several per physics step), so they don't allocate
        private readonly RaycastHit[] castHits = new RaycastHit[32];

        private void Awake()
        {
            if (capsule == null) { capsule = GetComponent<CapsuleCollider>(); }
            RememberBaseCapsule();
        }

        [Tooltip("The transform on which the character is currently standing")]
        public Transform groundTransform;

        [Header("Moving Ground")]
        [SerializeField]
        [Tooltip("Carry the character along with whatever they are standing on (moving platforms, elevators).")]
        private bool moveWithGround = true;
        [SerializeField]
        [Tooltip("Also turn the character with ground that rotates about the vertical axis (spinning platforms).")]
        private bool rotateWithGround = true;
        [SerializeField]
        [Tooltip("When leaving moving ground (jumping or walking off), keep its velocity so the character doesn't stop dead in mid-air.")]
        private bool inheritGroundVelocity = true;
        [SerializeField]
        [Tooltip("Ground that moves further than this in one physics step is treated as teleporting, and doesn't carry the character.")]
        private float maxGroundCarryPerStep = 2f;

        // The ground the character stood on at the end of the last step, and the character's pose relative to it.
        private Transform carrierGround;
        private Vector3 carrierLocalPosition;
        private Quaternion carrierRotation = Quaternion.identity;
        // Ground yaw applied since the visuals last consumed it (see ConsumeGroundRotation).
        private Quaternion pendingGroundRotation = Quaternion.identity;

        /// <summary>Velocity of the ground carrying the character during the last step (zero on static ground or in the air).</summary>
        public Vector3 GroundVelocity { get; private set; }

        // The state of the character's abilities, which persists between physics steps (each kept with its component, in
        // Components/: movement modes are shared assets, so it lives here, one per character).
        public JumpState jump { get; } = new JumpState();
        public DashState dash { get; } = new DashState();
        public CrouchState crouch { get; } = new CrouchState();
        public SlideState slide { get; } = new SlideState();
        public RollState roll { get; } = new RollState();
        public WallRunState wallRun { get; } = new WallRunState();
        public WallSlideState wallSlide { get; } = new WallSlideState();
        public WallJumpState wallJump { get; } = new WallJumpState();
        public LedgeState ledge { get; } = new LedgeState();
        public VaultState vault { get; } = new VaultState();
        public GrabState grab { get; } = new GrabState();
        public WallClimbState wallClimb { get; } = new WallClimbState();
        public RopeSwingState ropeSwing { get; } = new RopeSwingState();
        public ZiplineState zipline { get; } = new ZiplineState();
        public BounceState bounce { get; } = new BounceState();

        /// <summary>
        /// The character's own clock: the time (s) of its current physics step, counted from its first. Movement times
        /// things by it rather than by Time.fixedTime, so a copy of the character stepped faster than real time (see
        /// MovementSimulation) times them the same.
        /// </summary>
        public float StepTime { get; private set; }

        // The time (StepTime) at which the character was last on the ground (negative infinity until they first touch it)
        // Runtime state, so not serialized: a saved value (the prefab used to store 0) would override "never grounded".
        [System.NonSerialized] public float lastGroundedTime = float.NegativeInfinity;

        /// <summary>Whether the character has started a jump since they were last on the ground.</summary>
        public bool HasJumpedSinceGrounded => jump.lastJumpTime >= lastGroundedTime;

        // Caps time since grounded so "never grounded" stays a finite number when evaluating curves with it.
        private const float MaxTimeSinceGrounded = 1000f;

        /// <summary>
        /// Transforms inputs into character movement
        /// Called by the parent character controller to ensure consistent execution order
        /// TODO: Break this up into smaller & cleaner functions
        /// </summary>
        /// <param name="inputs"></param>
        public void ProcessMovementInputs(Inputs inputs)
        {
            StepTime += Time.fixedDeltaTime;
            // Whether the last frame's movement data is valid to be used (will be false on the first frame)
            bool hasLastFrameMovement = lastFrameMovementData != null;

            // Create a data object representing the character's movement this frame, and populate it with some initial data
            MovementData data = new MovementData()
            {
                deltaTime = Time.fixedDeltaTime,
                initialPosition = (float.IsNaN(rb.position.x) || float.IsNaN(rb.position.y) || float.IsNaN(rb.position.z)) ? transform.position : rb.position,
                // Read the physics pose, not the transform: with Rigidbody interpolation the transform holds a
                // smoothed render pose that lags behind the simulation.
                initialRotation = rb.rotation,
                cameraLook = cameraTransform != null ? cameraTransform.rotation : Quaternion.identity,
            };

            data.previousState = hasLastFrameMovement ? lastFrameMovementData.state : CharacterState.Idle;
            data.state = CharacterState.Idle;

            data.initialInertia = hasLastFrameMovement ? lastFrameMovementData.finalInertia : Vector3.zero;
            data.initialVelocity = hasLastFrameMovement ? lastFrameMovementData.finalVelocity : Vector3.zero;
            data.previousCollisions = hasLastFrameMovement ? lastFrameMovementData.collisions : CollisionFlags.None;

            // Move with whatever the character was standing on, then check whether the character is starting on the
            // ground this frame via spherecast downwards
            // TODO: avoid hard coding gravity direction
            data.initialGrounded = ApplyGroundCarry(ref data);
            data.initialGroundTransform = groundTransform;

            // On leaving the ground (walking off an edge or jumping), keep moving at the speed the character was going.
            // On the ground, walking is applied directly each frame rather than stored as inertia, so without this the
            // character would stop dead in the air until air steering kicks in.
            if (!data.initialGrounded && hasLastFrameMovement && lastFrameMovementData.initialGrounded)
            {
                Vector3 walking = lastFrameMovementData.finalVelocity - lastFrameMovementData.finalInertia;
                data.initialInertia += new Vector3(walking.x, 0f, walking.z);
            }

            // If we are on the ground, update related data on the movement data object
            if (data.initialGrounded)
            {
                lastGroundedTime = StepTime;
                data.state = data.state | CharacterState.Grounded;
            }
            // Always pass these on, including while airborne: coyote time and the coyote gravity and air steering
            // curves all measure time since leaving the ground. (Previously they were only set while grounded, so in
            // the air they measured time since the game started and those features never took effect.)
            data.lastGroundedTime = lastGroundedTime;
            data.timeSinceGrounded = Mathf.Min(StepTime - lastGroundedTime, MaxTimeSinceGrounded);

            // Setup initial movement data before calculation to avoid weird behaviour
            data.directMovementVector = Vector3.zero;
            data.targetRotation = data.initialRotation; // Includes any turn from rotating ground.

            data.velocityChange = pendingVelocityChange;
            pendingVelocityChange = Vector3.zero;

            data.velocityOverride = velocityOverride;
            velocityOverride = null;

            // Use a scriptable object to process inputs and apply them to the character's movement data
            if (movementHandler != null)
            {
                movementHandler.ProcessMovementData(ref data, inputs, this);
            }

            // Check whether the character can currently move in the way that is called for by their movement data, and fix it if not
            CheckMovementValid(ref data);
            if (movementHandler != null) { movementHandler.AfterMovementResolved(data, this); }

            // Save this frame's complete movement data
            currentMovementData = data;

            // Use the character's rigidbody to execute the movement for this frame
            CompleteMovement(data);

            RememberGround(data);

            if (hasLastFrameMovement) { DetectEvents(data); }
            RaiseEvents(data);
        }

        /// <summary>
        /// Adds the events every movement mode shares, found by comparing this step to the last: landing, leaving the
        /// ground, and newly touching a ceiling or wall. (Movement modes report jumps and dashes themselves.)
        /// </summary>
        private static void DetectEvents(MovementData data)
        {
            bool wasGrounded = data.previousState.HasFlag(CharacterState.Grounded);
            bool isGrounded = data.state.HasFlag(CharacterState.Grounded);
            if (!wasGrounded && isGrounded) { data.events |= MovementEvents.Landed; }
            if (wasGrounded && !isGrounded) { data.events |= MovementEvents.LeftGround; }

            CollisionFlags newContacts = data.collisions & ~data.previousCollisions;
            if ((newContacts & CollisionFlags.Above) != 0) { data.events |= MovementEvents.HitCeiling; }
            if ((newContacts & CollisionFlags.Sides) != 0) { data.events |= MovementEvents.HitWall; }

            foreach (var (state, started, ended) in StateEvents)
            {
                bool was = (data.previousState & state) != 0, now = (data.state & state) != 0;
                if (!was && now) { data.events |= started; }
                if (was && !now) { data.events |= ended; }
            }
        }

        /// <summary>
        /// The events raised when the character starts and stops being in each state, so movement modes only have to
        /// set the state (a new ability adds its state and events here).
        /// </summary>
        private static readonly (CharacterState state, MovementEvents started, MovementEvents ended)[] StateEvents =
        {
            (CharacterState.Crouching, MovementEvents.CrouchStarted, MovementEvents.CrouchEnded),
            (CharacterState.Sliding, MovementEvents.SlideStarted, MovementEvents.SlideEnded),
            (CharacterState.Rolling, MovementEvents.RollStarted, MovementEvents.RollEnded),
            (CharacterState.WallRunning, MovementEvents.WallRunStarted, MovementEvents.WallRunEnded),
            (CharacterState.LedgeHanging, MovementEvents.LedgeGrabbed, MovementEvents.LedgeReleased),
            (CharacterState.LedgeClimbing, MovementEvents.LedgeClimbStarted, MovementEvents.LedgeClimbEnded),
            (CharacterState.Grabbing, MovementEvents.GrabStarted, MovementEvents.GrabEnded),
            (CharacterState.WallSliding, MovementEvents.WallSlideStarted, MovementEvents.WallSlideEnded),
            (CharacterState.Vaulting, MovementEvents.VaultStarted, MovementEvents.VaultEnded),
            (CharacterState.SteppingUp, MovementEvents.StepUpStarted, MovementEvents.StepUpEnded),
            (CharacterState.WallClimbing, MovementEvents.WallClimbStarted, MovementEvents.WallClimbEnded),
            (CharacterState.RopeSwinging, MovementEvents.RopeGrabbed, MovementEvents.RopeReleased),
            (CharacterState.Ziplining, MovementEvents.ZiplineStarted, MovementEvents.ZiplineEnded),
        };

        private void RaiseEvents(MovementData data)
        {
            if (data.events == MovementEvents.None || MovementEventRaised == null) { return; }
            // Every bit of the enum, by count rather than while the bit is no more than the events: once the top bit is
            // in use, shifting on past it goes negative and then to 0, and a loop on bit <= events never ends.
            for (int shift = 0; shift < 63; shift++)
            {
                var single = (MovementEvents)(1L << shift);
                if ((data.events & single) != 0) { MovementEventRaised(single, data); }
            }
        }

        /// <summary>
        /// Uses the rigidbody to move the character in accordance with the finished movement data for the frame
        /// </summary>
        /// <param name="movementData"></param>
        public void CompleteMovement(MovementData movementData = null)
        {
            if (movementData == null) {movementData = currentMovementData;}
            if (rb != null)
            {
                Vector3 pos = movementData.initialPosition + movementData.finalMovementVector;
                Quaternion rot = movementData.finalRotation;
                // The body must be kinematic: Move only interpolates for kinematic bodies (on a dynamic body it is
                // a teleport, which makes the character visibly step at the physics rate), and a dynamic body would
                // also be pushed around by the physics solver on top of this controller's own collision handling.
                rb.Move(pos, rot);
                if (!rb.isKinematic)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
            }
        }

        /// <summary>
        /// At the end of the frame, store information in the last frame data for use next frame
        /// </summary>
        public void MovementCleanup()
        {
            lastFrameMovementData = currentMovementData;
            currentMovementData = null;
        }

        /// <summary>
        /// Add a force to the character to be applied in the next fixed update
        /// </summary>
        /// <param name="force"></param>
        public void AddForce(Vector3 force)
        {
            AddVelocityChange(force * Time.fixedDeltaTime / rb.mass);
        }

        /// <summary>
        /// Add a change in velocity to the character to be applied in the next fixed update
        /// </summary>
        /// <param name="velocityChange"></param>
        public void AddVelocityChange(Vector3 velocityChange)
        {
            pendingVelocityChange += velocityChange;
        }

        /// <summary>
        /// Override the character's velocity in the next fixed update
        /// </summary>
        /// <param name="newVelocity"></param>
        public void OverrideVelocity(Vector3 newVelocity)
        {
            velocityOverride = newVelocity;
        }

    }
}
