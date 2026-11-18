using Unity.Profiling;
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

        /// <summary>
        /// The movement data of the last physics step to finish (null before the first): what animation and the next
        /// step read the character's state, velocity and collisions from.
        /// </summary>
        [HideInInspector] public MovementData lastFrameMovementData = null;
        /// <summary>The movement data of the physics step in progress (null between steps: MovementCleanup moves it to lastFrameMovementData).</summary>
        [HideInInspector] public MovementData currentMovementData = null;
        // The two movement data objects the character's steps take turns with (see StartStep): a step's is kept as the
        // last step's (lastFrameMovementData) until the next has run, so the next takes the other. Each step's data lives
        // only that long: anything that needs it later copies what it needs.
        private readonly MovementData[] stepData = { new MovementData(), new MovementData() };

        /// <summary>
        /// A change in velocity, in m/s, to be applied to the character in the next physics step (like if the character
        /// gets thrown by an explosion); added to with AddVelocityChange, and cleared once applied.
        /// </summary>
        [HideInInspector] public Vector3 pendingVelocityChange;
        /// <summary>
        /// A complete override on the character's velocity, in m/s, to be applied in the next physics step (set with
        /// OverrideVelocity), or null for none. Cleared once applied.
        /// </summary>
        public Vector3? velocityOverride = null;

        [Tooltip("A multiplier on the character's movement speed")]
        public float movementSpeedMultiplier;
        [Tooltip("A multiplier on the character's jump launch speed (jump heights scale with its square)")]
        public float jumpStrengthModifier;

        [Tooltip("The radius (m, at the authored size) of the sphere cast used to determine whether the character is standing " +
                 "on the ground. 0: 90% of the body's footprint, following it as it's refitted (to a model, say). A fixed " +
                 "radius wider than the body starts inside a slope it stands on, which doesn't count as ground")]
        public float groundCheckRadius;

        [Tooltip("A mask which defines the physics layers which affect where the character can move and how")]
        public LayerMask movementLayerMask = 0;

        [Header("Collision")]
        [SerializeField]
        [Tooltip("The character's collider, for the physics that meets it: a capsule, box or sphere, kept in the body's shape (see Body), and the body's shape as authored if there's no Body Definition. Its first solid one of those if left empty. Optional: the movement doesn't need it")]
        [UnityEngine.Serialization.FormerlySerializedAs("capsule")]
        private Collider bodyCollider;
        [SerializeField]
        [Tooltip("The character's body as authored (see BodyDefinition): the Body Collider is shaped from it as the character " +
                 "wakes (made a collider of its kind if it isn't one). Empty: the Body Collider as it's authored")]
        private BodyDefinition bodyDefinition;
        [SerializeField]
        [Tooltip("Maximum height of a step or ledge the character can automatically step up (stairs).")]
        private float maxStepHeight = 0.35f;
        [SerializeField]
        [Tooltip("Hold the body's bottom above the feet by the Max Step Height, standing or crouched (not lying), and keep " +
                 "the feet on the ground with probes under them: steps, edges and bumps under that height pass under the " +
                 "body (the feet step up and down onto them), so it never rests on an edge or a corner; anything taller " +
                 "meets the body. Off: the body stands on the feet, stepping up onto what it meets")]
        private bool hoverOverSteps = true;
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
        /// The character's smallest dimension relative to as authored: the lesser of its height and its body's footprint's
        /// radius. Small clearances (the skin width) scale with it, so they stay as thin, for the character's size, on
        /// every side.
        /// </summary>
        private float ClearanceScale => Body.HasAuthoredSize && Body.AuthoredRadius > 0f
            ? Mathf.Min(HeightScale, Body.FootprintRadius / Body.AuthoredRadius) : HeightScale;

        /// <summary>The gap kept between the character and surfaces, at its size.</summary>
        internal float SkinWidth => skinWidth * ClearanceScale;
        /// <summary>The gap kept between the character and surfaces at its authored size (a body scales it for itself).</summary>
        internal float AuthoredSkinWidth => skinWidth;

        // How far below the feet the floor can be while still counting as grounded. The ground snap always closes
        // this gap, so the character never hovers inside it.
        private const float BaseGroundedTolerance = 0.1f;
        // Scaled with height: at a tenth of the size, a fixed 0.1 m would be the character's whole height.
        private float GroundedTolerance => BaseGroundedTolerance * HeightScale;

        // How far from straight below the character's feet a step's edge may be when it lands on the step (see TryStepUp).
        private const float StepLandingAngle = 40f;

        /// <summary>The radius of the body used for collision sweeps (the body's, less the skin width).</summary>
        private float SweepRadius => Mathf.Max(0.01f * ClearanceScale, Body.FootprintRadius - SkinWidth);

        /// <summary>The radius of the sphere CheckGrounded casts under the feet.</summary>
        private float GroundCheckRadius => groundCheckRadius > 0f ? groundCheckRadius * Mathf.Max(Scale.x, Scale.z)
            : Body.FootprintRadius * 0.9f; // 90%: 0.45 m on the 0.5 m capsule.

        /// <summary>The tallest step the character climbs at its size.</summary>
        private float StepHeight => maxStepHeight * HeightScale;

        private readonly Collider[] overlaps = new Collider[16];
        // Reused for every ground check and collision sweep (several per physics step), so they don't allocate
        private readonly RaycastHit[] castHits = new RaycastHit[32];
        /// <summary>The buffer the body's overlap queries fill (reused, so they don't allocate).</summary>
        internal Collider[] OverlapBuffer => overlaps;
        /// <summary>The buffer the body's sweeps fill (reused: the ground check and sweeps share it, one at a time).</summary>
        internal RaycastHit[] CastBuffer => castHits;

        private void Awake()
        {
            ShapeFromDefinition();
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

        // The state of the character's abilities, which persists between physics steps: movement modes are shared
        // assets, so it lives on the character, one per character. Each ability's state is declared beside its component
        // (a part of this class in the component's file: Components/Ledges.cs has `ledge`, say), so adding an ability
        // doesn't touch this file.

        /// <summary>
        /// The character's own clock: the time (s) of its current physics step, counted from its first. Movement times
        /// things by it rather than by Time.fixedTime, so a copy of the character stepped faster than real time (see
        /// MovementSimulation) times them the same.
        /// </summary>
        public float StepTime { get; private set; }

        // Runtime state, so not serialized: a saved value (the prefab used to store 0) would override "never grounded".
        /// <summary>The time (StepTime) at which the character was last on the ground (negative infinity until they first touch it).</summary>
        [System.NonSerialized] public float lastGroundedTime = float.NegativeInfinity;

        /// <summary>Whether the character has started a jump since they were last on the ground.</summary>
        public bool HasJumpedSinceGrounded => jump.lastJumpTime >= lastGroundedTime;

        // Caps time since grounded so "never grounded" stays a finite number when evaluating curves with it.
        private const float MaxTimeSinceGrounded = 1000f;

        // Profiler markers for the parts of a step (the Profiler's CPU Usage; each movement component has its own too,
        // "Movement.<Component>": see MovementComponent.Marker). See docs/testing.md, Performance.
        private static readonly ProfilerMarker StepMarker = new("Movement.Step");
        private static readonly ProfilerMarker ModeMarker = new("Movement.Mode");
        private static readonly ProfilerMarker CollisionMarker = new("Movement.Collision");

        /// <summary>
        /// One physics step of the character's movement, called by the ACCharacterController in FixedUpdate (so every
        /// character steps in the same order): the step's starting state (StartStep), what the movement mode makes of the
        /// inputs (RunMovementMode), then resolving that against the world and moving the Rigidbody (ResolveAndApply).
        /// </summary>
        public void ProcessMovementInputs(Inputs inputs)
        {
            using (StepMarker.Auto())
            {
                MovementData data = StartStep();
                RunMovementMode(data, inputs);
                ResolveAndApply(data);
            }
        }

        /// <summary>
        /// Advances the character's clock and starts the step's movement data from where the last step left the
        /// character: its pose, momentum and state, carried by the ground it stood on, whether it's on the ground now, and
        /// any forces added between steps.
        /// </summary>
        private MovementData StartStep()
        {
            StepTime += Time.fixedDeltaTime;
            // Whether the last frame's movement data is valid to be used (will be false on the first frame)
            bool hasLastFrameMovement = lastFrameMovementData != null;

            // The step's movement data: one of the two this character reuses (not the last step's, which this one starts
            // from), reset, so a step allocates none. Populated with some initial data.
            MovementData data = lastFrameMovementData == stepData[0] ? stepData[1] : stepData[0];
            data.Reset();
            data.deltaTime = Time.fixedDeltaTime;
            data.initialPosition = (float.IsNaN(rb.position.x) || float.IsNaN(rb.position.y) || float.IsNaN(rb.position.z)) ? transform.position : rb.position;
            // Read the physics pose, not the transform: with Rigidbody interpolation the transform holds a smoothed render
            // pose that lags behind the simulation.
            data.initialRotation = rb.rotation;
            data.cameraLook = cameraTransform != null ? cameraTransform.rotation : Quaternion.identity;

            data.previousState = hasLastFrameMovement ? lastFrameMovementData.state : CharacterState.Idle;
            data.state = CharacterState.Idle;

            data.initialInertia = hasLastFrameMovement ? lastFrameMovementData.finalInertia : Vector3.zero;
            data.initialVelocity = hasLastFrameMovement ? lastFrameMovementData.finalVelocity : Vector3.zero;
            data.previousCollisions = hasLastFrameMovement ? lastFrameMovementData.collisions : CollisionFlags.None;

            // Move with whatever the character was standing on, then check whether the character is starting on the
            // ground this frame via spherecast downwards
            data.initialGrounded = ApplyGroundCarry(data);
            data.initialGroundTransform = groundTransform;
            data.resolution.groundedBy = data.initialGrounded ? lastGroundedBy : GroundedBy.None;

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
            // Level unless a component holds the body pitched (see MovementData.targetPitch).
            data.initialPitch = bodyPitch;
            data.targetPitch = 0f;

            data.velocityChange = pendingVelocityChange;
            pendingVelocityChange = Vector3.zero;

            data.velocityOverride = velocityOverride;
            velocityOverride = null;
            return data;
        }

        /// <summary>Has the movement mode fill in how the character means to move this step, from the inputs.</summary>
        private void RunMovementMode(MovementData data, Inputs inputs)
        {
            if (movementHandler == null) { return; }
            using (ModeMarker.Auto())
            {
                movementHandler.ProcessMovementData(data, inputs, this);
            }
        }

        /// <summary>
        /// Resolves the step's movement against the world (fixing whatever can't move as called for), lets the movement
        /// mode move what goes with the character, moves the Rigidbody, and raises the step's events.
        /// </summary>
        private void ResolveAndApply(MovementData data)
        {
            bool hasLastFrameMovement = lastFrameMovementData != null;
            using (CollisionMarker.Auto())
            {
                CheckMovementValid(data);
            }
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
        private void DetectEvents(MovementData data)
        {
            bool wasGrounded = data.previousState.HasFlag(CharacterState.Grounded);
            bool isGrounded = data.state.HasFlag(CharacterState.Grounded);
            if (!wasGrounded && isGrounded) { data.events |= MovementEvents.Landed; }
            if (wasGrounded && !isGrounded) { data.events |= MovementEvents.LeftGround; }

            CollisionFlags newContacts = data.collisions & ~data.previousCollisions;
            if ((newContacts & CollisionFlags.Above) != 0) { data.events |= MovementEvents.HitCeiling; }
            if ((newContacts & CollisionFlags.Sides) != 0) { data.events |= MovementEvents.HitWall; }

            // The abilities' starts and ends, which the movement mode knows (each ability's component declares them).
            if (movementHandler != null) { movementHandler.DetectStateEvents(data); }
        }

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
