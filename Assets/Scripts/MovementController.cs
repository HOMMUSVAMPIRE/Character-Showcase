using UnityEngine;
using static Character.CharacterUtilities;

namespace Character
{
    /// <summary>
    /// A class which controlls the way a character moves
    /// </summary>
    public class MovementController : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("A reference to the parent character controller")]
        private ACCharacterController characterController;

        [SerializeField]
        [Tooltip("The rigidbody that drives this character's physics interactions and enables proper movement behaviour")]
        private Rigidbody rb;

        // TODO: This should ideally be set up without relying on the main camera to support NPCs to use the same character controls
        [Tooltip("The transform of the camera that the player's inputs should be relative to")]
        public Transform cameraTransform => Camera.main.transform; // point this to a reference on character controller later

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
        [Tooltip("A multiplier on the character's jump speed")]
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

        private void RememberBaseCapsule()
        {
            if (hasBaseCapsule || capsule == null) { return; }
            baseRadius = capsule.radius;
            baseHeight = capsule.height;
            baseCenter = capsule.center;
            hasBaseCapsule = true;
        }

        /// <summary>
        /// Resizes the character's collision to the given size: the capsule's height (and center) scale with y, and its
        /// radius with the larger of x and z, since a capsule is round however the character is stretched. Collision
        /// sweeps, ground checks and depenetration all use the capsule, so they follow. (The radius is also capped at half
        /// the height; see ScaledRadius.) Doesn't check for room: see
        /// CanFit. Called by ACCharacterController.SetScale.
        /// </summary>
        public void ApplyScale(Vector3 scale)
        {
            Scale = scale;
            RememberBaseCapsule();
            if (capsule == null) { return; }
            capsule.radius = ScaledRadius(scale);
            capsule.height = baseHeight * scale.y;
            capsule.center = Vector3.Scale(baseCenter, scale);
        }

        /// <summary>
        /// The capsule's radius at a size: the larger of the width and depth scales, since a capsule is round however the
        /// character is stretched, but no more than half its height, so a wide, flat character's collider doesn't
        /// become a sphere taller than the character.
        /// </summary>
        private float ScaledRadius(Vector3 scale) => ScaledRadius(scale, baseRadius, baseHeight);

        private static float ScaledRadius(Vector3 scale, float radius, float height) =>
            Mathf.Min(radius * Mathf.Max(scale.x, scale.z), height * scale.y * 0.5f);

        /// <summary>The capsule's radius and height at the character's authored size (1, 1, 1), which its scale multiplies.</summary>
        public float BaseRadius { get { RememberBaseCapsule(); return baseRadius; } }
        public float BaseHeight { get { RememberBaseCapsule(); return baseHeight; } }

        /// <summary>
        /// Changes the capsule's size at the character's authored size (its scale then multiplies it), standing on the
        /// character's feet: the bottom stays where it is, and its center rises to half the height. Used to fit the
        /// collider to the character's model (see CharacterModelHost). Like growing, it waits for room: if the bigger
        /// capsule wouldn't fit where the character stands, nothing changes and this returns false.
        /// </summary>
        public bool SetBaseCapsule(float radius, float height)
        {
            RememberBaseCapsule();
            if (capsule == null || radius <= 0f || height <= 0f) { return false; }
            radius = Mathf.Min(radius, height * 0.5f);
            var center = new Vector3(baseCenter.x, height * 0.5f, baseCenter.z);
            bool grows = height > baseHeight + 1e-5f || radius > baseRadius + 1e-5f;
            if (grows && !CanFit(Scale, radius, height, center)) { return false; }
            baseRadius = radius;
            baseHeight = height;
            baseCenter = center;
            ApplyScale(Scale);
            return true;
        }

        /// <summary>
        /// Whether the character would fit where it stands at the given size, without overlapping the world (more than
        /// the skin width, so the floor it stands on doesn't count).
        /// </summary>
        public bool CanFit(Vector3 scale)
        {
            RememberBaseCapsule();
            return CanFit(scale, baseRadius, baseHeight, baseCenter);
        }

        /// <summary>The same, for a capsule of another authored size.</summary>
        private bool CanFit(Vector3 scale, float authoredRadius, float authoredHeight, Vector3 authoredCenter)
        {
            if (capsule == null) { return true; }
            Vector3 feet = rb != null ? rb.position : transform.position;
            float radiusAtScale = ScaledRadius(scale, authoredRadius, authoredHeight);
            float skinAtScale = skinWidth * (authoredRadius > 0f ? Mathf.Min(scale.y, radiusAtScale / authoredRadius) : scale.y);
            GetSweepCapsule(feet, radiusAtScale, authoredHeight * scale.y, Vector3.Scale(authoredCenter, scale), skinAtScale,
                out Vector3 top, out Vector3 bottom, out float radius);
            int count = Physics.OverlapCapsuleNonAlloc(top, bottom, radius, overlaps, movementLayerMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                if (!overlaps[i].transform.IsChildOf(transform)) { return false; }
            }
            return true;
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

        // How many more times the character can begin a jump (gets reset when grounded)
        [HideInInspector] public int jumpsRemaining = 0;

        // The character's dash timers and counters (used by movement modes that support dashing)
        public DashState dash { get; } = new DashState();
        // The maximum number of jumps the character can perform in succession
        public int maxJumps => movementHandler is JumpMode ? (movementHandler as JumpMode).maxJumps : 0;

        // The time at which the character was last on the ground (negative infinity until they first touch it)
        // Runtime state, so not serialized: a saved value (the prefab used to store 0) would override "never grounded".
        [System.NonSerialized] public float lastGroundedTime = float.NegativeInfinity;

        // The time at which the character last started a jump (negative infinity if they never have)
        [System.NonSerialized] public float lastJumpTime = float.NegativeInfinity;

        // Whether the current press of the jump button has already started a jump, so one press never starts two
        // (for example when a jump ends early against a ceiling while the press is still within the jump buffer)
        [System.NonSerialized] public bool jumpPressConsumed;

        /// <summary>Whether the character has started a jump since they were last on the ground.</summary>
        public bool HasJumpedSinceGrounded => lastJumpTime >= lastGroundedTime;

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
                cameraLook = cameraTransform.rotation,
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
                lastGroundedTime = Time.fixedTime;
                data.state = data.state | CharacterState.Grounded;
            }
            // Always pass these on, including while airborne: coyote time and the coyote gravity and air steering
            // curves all measure time since leaving the ground. (Previously they were only set while grounded, so in
            // the air they measured time since the game started and those features never took effect.)
            data.lastGroundedTime = lastGroundedTime;
            data.timeSinceGrounded = Mathf.Min(Time.fixedTime - lastGroundedTime, MaxTimeSinceGrounded);

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
        }

        private void RaiseEvents(MovementData data)
        {
            if (data.events == MovementEvents.None || MovementEventRaised == null) { return; }
            for (int bit = 1; bit <= (int)data.events; bit <<= 1)
            {
                var single = (MovementEvents)bit;
                if ((data.events & single) != 0) { MovementEventRaised(single, data); }
            }
        }

        /// <summary>
        /// Carries the character with the ground they stood on last step (position, and yaw if enabled), then checks
        /// whether they are grounded. If the carried position isn't grounded (e.g. they just jumped), the carry is
        /// skipped and the ground's velocity is added to their inertia instead, so they keep its momentum.
        /// Returns whether the character is grounded, and sets groundTransform.
        /// </summary>
        private bool ApplyGroundCarry(ref MovementData data)
        {
            GroundVelocity = Vector3.zero;
            Vector3 carry = Vector3.zero;
            Quaternion turn = Quaternion.identity;

            if (moveWithGround && carrierGround != null)
            {
                GetGroundPose(carrierGround, out Vector3 groundPosition, out Quaternion groundRotation);
                carry = groundPosition + groundRotation * carrierLocalPosition - data.initialPosition;
                if (rotateWithGround) { turn = YawOnly(groundRotation * Quaternion.Inverse(carrierRotation)); }
                if (carry.sqrMagnitude > maxGroundCarryPerStep * maxGroundCarryPerStep)
                {
                    carry = Vector3.zero; // Teleported: don't drag the character across the level.
                    turn = Quaternion.identity;
                }
            }

            Vector3 carriedPosition = data.initialPosition + carry;
            // Static ground still produces a tiny non-zero carry from floating point error; don't apply that.
            bool groundMoved = carry.sqrMagnitude > 1e-10f || Quaternion.Angle(Quaternion.identity, turn) > 1e-3f;
            if (!groundMoved)
            {
                return CheckGrounded(data.initialPosition, out groundTransform);
            }

            if (CheckGrounded(carriedPosition, out groundTransform))
            {
                data.initialPosition = carriedPosition;
                data.initialRotation = turn * data.initialRotation;
                pendingGroundRotation = turn * pendingGroundRotation;
                GroundVelocity = carry / data.deltaTime;
                return true;
            }

            // Left the moving ground: keep moving at its velocity rather than stopping dead.
            if (inheritGroundVelocity)
            {
                data.initialInertia += carry / data.deltaTime;
            }
            return CheckGrounded(data.initialPosition, out groundTransform);
        }

        /// <summary>Records the character's pose relative to their ground at the end of the step, for the next carry.</summary>
        private void RememberGround(MovementData data)
        {
            carrierGround = data.initialGrounded ? groundTransform : null;
            if (carrierGround == null) { return; }

            GetGroundPose(carrierGround, out Vector3 groundPosition, out Quaternion groundRotation);
            Vector3 finalPosition = data.initialPosition + data.finalMovementVector;
            carrierLocalPosition = Quaternion.Inverse(groundRotation) * (finalPosition - groundPosition);
            carrierRotation = groundRotation;
        }

        /// <summary>
        /// The ground's physics pose. For ground with a Rigidbody, use the body's pose rather than its transform, which
        /// may be an interpolated render pose that lags the simulation.
        /// </summary>
        private static void GetGroundPose(Transform ground, out Vector3 position, out Quaternion rotation)
        {
            if (ground.TryGetComponent(out Rigidbody body))
            {
                position = body.position;
                rotation = body.rotation;
            }
            else
            {
                position = ground.position;
                rotation = ground.rotation;
            }
        }

        /// <summary>The part of a rotation that turns about the world up axis.</summary>
        private static Quaternion YawOnly(Quaternion rotation)
        {
            Vector3 forward = Vector3.ProjectOnPlane(rotation * Vector3.forward, Vector3.up);
            if (forward.sqrMagnitude < 1e-6f) { return Quaternion.identity; }
            return Quaternion.Euler(0f, Vector3.SignedAngle(Vector3.forward, forward, Vector3.up), 0f);
        }

        /// <summary>
        /// Returns the ground yaw applied to the character since the last call, and resets it. Used by visuals that
        /// smooth their own rotation, so they turn with spinning ground instead of lagging behind it.
        /// </summary>
        public Quaternion ConsumeGroundRotation()
        {
            Quaternion turn = pendingGroundRotation;
            pendingGroundRotation = Quaternion.identity;
            return turn;
        }

        /// <summary>
        /// Checks whether the character is on the ground via spherecast downwards
        /// TODO: Implement variable gravity directions
        /// </summary>
        /// <param name="position"></param>
        /// <param name="groundTransform"></param>
        /// <returns></returns>
        public bool CheckGrounded(Vector3 position, out Transform groundTransform)
        {
            groundTransform = null;

            float r = GroundCheckRadius;
            // Start with the bottom of the sphere just above the feet so we can detect ground slightly below them,
            // and reject things above them. (2 cm at the authored size, scaled with it.)
            float startLift = 0.02f * ClearanceScale;
            Vector3 start = position + Vector3.up * (r + startLift);
            // Sweep the sphere's bottom from startLift above the feet to GroundedTolerance below them. Distance is
            // measured from the sphere's bottom, so the radius must NOT be added here — doing so made the character
            // count as grounded up to (r + tolerance) above the floor, cancelling their fall while still in the air.
            float castDist = startLift + GroundedTolerance;

            int count = Physics.SphereCastNonAlloc(start, r, Vector3.down, castHits, castDist, movementLayerMask, QueryTriggerInteraction.Ignore);
            float best = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = castHits[i];
                if (hit.transform.IsChildOf(transform)) { continue; }
                if (hit.distance <= 0f) { continue; }
                if (Vector3.Angle(hit.normal, Vector3.up) > maxSlopeAngle) { continue; }
                if (hit.distance < best)
                {
                    best = hit.distance;
                    // The object actually hit (its Rigidbody's transform, if it has one) - not the hierarchy root,
                    // which for a platform parented under a static "Environment" object would never move.
                    groundTransform = hit.transform;
                }
            }
            return float.IsFinite(best);
        }

        /// <summary>
        /// Resolve the character's intended motion against the world: push out of anything that moved into the
        /// character, sweep the capsule, slide along walls and walkable slopes, step up onto low ledges (stairs), slide
        /// off surfaces too steep to stand on, and stick to descending slopes. Momentum that a collision blocks is
        /// removed, and data.collisions records which sides were blocked.
        /// </summary>
        public bool CheckMovementValid(ref MovementData data)
        {
            data.collisions = CollisionFlags.None;
            Vector3 pos = data.initialPosition;
            Vector3 inertia = data.finalInertia;

            // Sweeps can't see anything they start inside of, so first push out of anything that moved into the
            // character since the last step (a moving wall, a platform's side, a lowering ceiling).
            Vector3 depenetration = Depenetrate(pos, data.initialRotation);
            if (depenetration != Vector3.zero)
            {
                pos += depenetration;
                Vector3 pushDirection = depenetration.normalized;
                data.collisions |= pushDirection.y > 0.7f ? CollisionFlags.Below
                    : pushDirection.y < -0.7f ? CollisionFlags.Above : CollisionFlags.Sides;
                RemoveInertiaInto(ref inertia, pushDirection);
            }

            Vector3 move = data.finalMovementVector;
            if (move.sqrMagnitude < 1e-12f && depenetration == Vector3.zero) { return true; }

            // Split into horizontal (slide/step-up) and vertical (gravity/jump) components so they resolve independently.
            Vector3 horiz = new Vector3(move.x, 0f, move.z);
            float vert = move.y;

            Vector3 horizResolved = ResolveHorizontal(pos, horiz, data.initialGrounded, ref inertia, ref data.collisions);
            pos += horizResolved;

            Vector3 vertResolved = ResolveVertical(pos, vert, ref inertia, ref data.collisions);
            pos += vertResolved;

            Vector3 totalResolved = depenetration + horizResolved + vertResolved;

            // Snap to ground while grounded so the character sits on the floor and follows descending slopes.
            // Allowed drop is the larger of:
            //  - GroundedTolerance: the gap CheckGrounded already counts as "on the ground". Grounded gravity is tiny
            //    (inertia is reset each grounded frame), so without closing this gap the character hovers above the
            //    floor for a while after landing.
            //  - The most a walkable slope could drop over the horizontal distance moved this frame.
            //  - While walking, the step height, so the character walks down stairs instead of floating off each step.
            // Anything further is a ledge, and gravity handles it (so walking off a ledge isn't a teleport).
            if (data.initialGrounded && vert <= 0f)
            {
                float horizDist = new Vector2(horizResolved.x, horizResolved.z).magnitude;
                float maxSlopeDrop = Mathf.Min(groundSnapDistance * HeightScale, horizDist * Mathf.Tan(maxSlopeAngle * Mathf.Deg2Rad));
                float maxDrop = Mathf.Max(GroundedTolerance, maxSlopeDrop) + SkinWidth;
                if (horizDist > 1e-4f * HeightScale) { maxDrop = Mathf.Max(maxDrop, StepHeight + SkinWidth); }
                if (CapsuleSweep(pos, Vector3.down, maxDrop + SkinWidth, out var snapHit)
                    && Vector3.Angle(snapHit.normal, Vector3.up) <= maxSlopeAngle)
                {
                    // Negative when the ground is closer than the skin width (rolling over a step's edge can close the
                    // gap): then this lifts the character back to it, as nothing else would. Depenetration ignores
                    // overlaps shallower than the skin, so without this the character stays sunk into the ground.
                    float drop = snapHit.distance - SkinWidth;
                    if (drop <= maxDrop)
                    {
                        totalResolved += Vector3.down * drop;
                        data.collisions |= CollisionFlags.Below;
                    }
                }
            }

            data.ForceMovementVector(totalResolved, inertia);
            return true;
        }

        /// <summary>Removes the part of the character's momentum heading into a surface with the given normal.</summary>
        private static void RemoveInertiaInto(ref Vector3 inertia, Vector3 surfaceNormal)
        {
            float into = Vector3.Dot(inertia, surfaceNormal);
            if (into < 0f) { inertia -= surfaceNormal * into; }
        }

        /// <summary>
        /// Resolve horizontal motion: sweep forward, slide along walls/walkable slopes, attempt step-up for low ledges.
        /// </summary>
        private Vector3 ResolveHorizontal(Vector3 feetPos, Vector3 horizMove, bool grounded, ref Vector3 inertia, ref CollisionFlags collisions)
        {
            float mag = horizMove.magnitude;
            if (mag < 1e-6f) { return Vector3.zero; }
            Vector3 dir = horizMove / mag;

            if (!CapsuleSweep(feetPos, dir, mag + SkinWidth, out var hit))
            {
                return horizMove;
            }

            float travel = Mathf.Max(0f, hit.distance - SkinWidth);
            Vector3 traveled = dir * travel;
            Vector3 remaining = horizMove - traveled;

            float slopeAngle = Vector3.Angle(hit.normal, Vector3.up);

            // Walkable slope (ramp): project remaining motion onto the slope and continue up/down it.
            if (slopeAngle <= maxSlopeAngle && hit.normal.y > 0.01f)
            {
                Vector3 slid = Vector3.ProjectOnPlane(remaining, hit.normal);
                return traveled + slid;
            }

            // Stair / low ledge: try to step up and over.
            if (grounded)
            {
                Vector3 stepped = TryStepUp(feetPos, dir, mag);
                if (stepped.sqrMagnitude > traveled.sqrMagnitude) { return stepped; }
            }

            // Wall: slide along it in the horizontal plane, losing the momentum heading into it.
            Vector3 wallNormal = new Vector3(hit.normal.x, 0f, hit.normal.z);
            if (wallNormal.sqrMagnitude < 1e-6f) { return traveled; }
            wallNormal.Normalize();
            collisions |= CollisionFlags.Sides;
            RemoveInertiaInto(ref inertia, wallNormal);
            Vector3 slide = Vector3.ProjectOnPlane(remaining, wallNormal);
            slide.y = 0f;
            float slideMag = slide.magnitude;
            if (slideMag < 1e-6f) { return traveled; }

            // Make sure the slide direction isn't itself blocked (a corner).
            if (CapsuleSweep(feetPos + traveled, slide / slideMag, slideMag + SkinWidth, out var slideHit))
            {
                Vector3 cornerNormal = new Vector3(slideHit.normal.x, 0f, slideHit.normal.z);
                if (cornerNormal.sqrMagnitude > 1e-6f) { RemoveInertiaInto(ref inertia, cornerNormal.normalized); }
                float slideTravel = Mathf.Max(0f, slideHit.distance - SkinWidth);
                return traveled + (slide / slideMag) * slideTravel;
            }
            return traveled + slide;
        }

        /// <summary>
        /// Attempt the classic step-up: lift the capsule by maxStepHeight, move forward, then drop back down.
        /// Returns Vector3.zero if the step cannot be made (headroom blocked, no floor, or floor too steep).
        /// </summary>
        private Vector3 TryStepUp(Vector3 feetPos, Vector3 dir, float distance)
        {
            // 1) Make sure there's headroom to lift up.
            float stepHeight = StepHeight;
            if (CapsuleSweep(feetPos, Vector3.up, stepHeight + SkinWidth, out _)) { return Vector3.zero; }
            Vector3 raised = feetPos + Vector3.up * stepHeight;

            // 2) Sweep forward from the raised position.
            if (CapsuleSweep(raised, dir, distance + SkinWidth, out var fwdHit))
            {
                float travel = Mathf.Max(0f, fwdHit.distance - SkinWidth);
                if (travel < 1e-4f * HeightScale) { return Vector3.zero; }
                raised += dir * travel;
            }
            else
            {
                raised += dir * distance;
            }

            // 3) Drop back down and make sure we land on walkable ground.
            if (!CapsuleSweep(raised, Vector3.down, stepHeight + SkinWidth, out var downHit)) { return Vector3.zero; }

            // 4) Make sure we land on the step, not perched on its edge. Moving only this step's distance forward, the
            // capsule's rounded bottom often comes down on the step's edge, well ahead of its axis. The ground check
            // (a slightly smaller sphere) then sees that edge as too steep to stand on, and the character slides back
            // off it, so steps a large fraction of the capsule's radius tall were never climbed. Instead, go on far
            // enough that the edge is within StepLandingAngle of straight below, for both spheres. Measured from the
            // contact point, not the hit normal, which for an edge can be either face's.
            float landingReach = Mathf.Min(SweepRadius, GroundCheckRadius) * Mathf.Sin(StepLandingAngle * Mathf.Deg2Rad);
            float ahead = Vector3.Dot(downHit.point - raised, dir);
            if (ahead > landingReach)
            {
                float extra = ahead - landingReach;
                if (CapsuleSweep(raised, dir, extra + SkinWidth, out _)) { return Vector3.zero; }
                raised += dir * extra;
                if (!CapsuleSweep(raised, Vector3.down, stepHeight + SkinWidth, out downHit)) { return Vector3.zero; }
            }
            if (Vector3.Angle(downHit.normal, Vector3.up) > maxSlopeAngle) { return Vector3.zero; }

            float drop = Mathf.Max(0f, downHit.distance - SkinWidth);
            Vector3 landed = raised - Vector3.up * drop;
            // Only accept if we actually ended up above the starting foot height (otherwise this isn't a step up).
            if (landed.y <= feetPos.y + 1e-4f * HeightScale) { return Vector3.zero; }
            // Nor if the step is taller than the step height. Judged by where the capsule touched down (the step's top,
            // or its edge, which is as high), not where its feet end up: perched part way up a taller step's edge, the
            // feet can be under the step height, and from there the character would ride up over the edge.
            if (downHit.point.y - feetPos.y > stepHeight + 1e-3f * HeightScale) { return Vector3.zero; }
            return landed - feetPos;
        }

        /// <summary>
        /// Resolve vertical motion: sweep up/down and clamp at the nearest contact, losing the momentum heading into
        /// it (so a jump doesn't keep pushing into a ceiling). Landing on a surface too steep to stand on slides the
        /// character down it rather than letting them rest there.
        /// </summary>
        private Vector3 ResolveVertical(Vector3 feetPos, float verticalDelta, ref Vector3 inertia, ref CollisionFlags collisions)
        {
            if (Mathf.Abs(verticalDelta) < 1e-6f) { return Vector3.zero; }
            Vector3 dir = verticalDelta > 0f ? Vector3.up : Vector3.down;
            float mag = Mathf.Abs(verticalDelta);
            if (!CapsuleSweep(feetPos, dir, mag + SkinWidth, out var hit)) { return dir * mag; }
            float travel = Mathf.Max(0f, hit.distance - SkinWidth);
            Vector3 traveled = dir * travel;

            collisions |= verticalDelta > 0f ? CollisionFlags.Above : CollisionFlags.Below;
            bool steep = Vector3.Angle(hit.normal, Vector3.up) > maxSlopeAngle;
            if (verticalDelta < 0f && !steep)
            {
                // Walkable ground just stops the fall. (Removing only the part heading into its surface would turn
                // some of the fall into sliding down a ramp.)
                inertia.y = Mathf.Max(inertia.y, 0f);
                return traveled;
            }
            RemoveInertiaInto(ref inertia, hit.normal);

            // Too steep to stand on (including the edge of a ledge caught by the capsule's rounded bottom): slide the
            // rest of the fall along the surface. The momentum kept above runs along the surface too, so the slide
            // speeds up like a fall would.
            if (verticalDelta < 0f)
            {
                Vector3 slide = Vector3.ProjectOnPlane(dir * (mag - travel), hit.normal);
                float slideMag = slide.magnitude;
                if (slideMag > 1e-6f)
                {
                    Vector3 slideDir = slide / slideMag;
                    if (CapsuleSweep(feetPos + traveled, slideDir, slideMag + SkinWidth, out var slideHit))
                    {
                        slide = slideDir * Mathf.Max(0f, slideHit.distance - SkinWidth);
                    }
                    traveled += slide;
                }
            }
            return traveled;
        }

        /// <summary>
        /// Finds how far the character must move to stop overlapping the world. Only overlaps deeper than the skin
        /// width count, so resting against the floor or a wall isn't treated as being inside it.
        /// </summary>
        private Vector3 Depenetrate(Vector3 feetPos, Quaternion rotation)
        {
            if (capsule == null) { return Vector3.zero; }

            Vector3 offset = Vector3.zero;
            for (int pass = 0; pass < MaxDepenetrationPasses; pass++)
            {
                GetSweepCapsule(feetPos + offset, out Vector3 top, out Vector3 bottom, out float radius);
                int count = Physics.OverlapCapsuleNonAlloc(top, bottom, radius, overlaps, movementLayerMask, QueryTriggerInteraction.Ignore);
                bool pushed = false;
                for (int i = 0; i < count; i++)
                {
                    Collider other = overlaps[i];
                    if (other.transform.IsChildOf(transform)) { continue; }
                    GetColliderPose(other, out Vector3 otherPosition, out Quaternion otherRotation);
                    if (Physics.ComputePenetration(capsule, feetPos + offset, rotation, other, otherPosition, otherRotation,
                            out Vector3 direction, out float distance))
                    {
                        offset += direction * distance;
                        pushed = true;
                    }
                }
                if (!pushed) { break; }
            }
            return offset;
        }

        /// <summary>
        /// A collider's physics pose. Colliders on an interpolated Rigidbody follow its physics pose, not their
        /// transform, which may hold a smoothed render pose that lags the simulation.
        /// </summary>
        private static void GetColliderPose(Collider collider, out Vector3 position, out Quaternion rotation)
        {
            Transform colliderTransform = collider.transform;
            Rigidbody body = collider.attachedRigidbody;
            if (body == null)
            {
                position = colliderTransform.position;
                rotation = colliderTransform.rotation;
                return;
            }

            Transform bodyTransform = body.transform;
            Quaternion toBodySpace = Quaternion.Inverse(bodyTransform.rotation);
            position = body.position + body.rotation * (toBodySpace * (colliderTransform.position - bodyTransform.position));
            rotation = body.rotation * (toBodySpace * colliderTransform.rotation);
        }

        /// <summary>
        /// The capsule used for collision queries, placed at the given feet position: the character's capsule shrunk by
        /// the skin width, with its bottom raised by the skin width.
        /// </summary>
        private void GetSweepCapsule(Vector3 feetPos, out Vector3 top, out Vector3 bottom, out float radius)
        {
            if (capsule != null) { GetSweepCapsule(feetPos, capsule.radius, capsule.height, capsule.center, SkinWidth, out top, out bottom, out radius); }
            else { GetSweepCapsule(feetPos, 0.5f, 2f, new Vector3(0f, 1f, 0f), SkinWidth, out top, out bottom, out radius); }
        }

        /// <summary>The same, for a capsule of the given size and skin width (one the character might be resized to).</summary>
        private static void GetSweepCapsule(Vector3 feetPos, float capsuleRadius, float capsuleHeight, Vector3 localCenter,
            float skin, out Vector3 top, out Vector3 bottom, out float radius)
        {
            radius = capsuleRadius - skin;
            if (radius <= 0f) { radius = capsuleRadius * 0.5f; }
            float height = Mathf.Max(capsuleHeight, capsuleRadius * 2f);
            float halfSegment = Mathf.Max(0f, (height * 0.5f) - radius - skin);
            Vector3 center = feetPos + localCenter;
            bottom = center - Vector3.up * halfSegment;
            top = center + Vector3.up * halfSegment;
        }

        /// <summary>
        /// Capsule-sweep from a feet-anchored position in the given direction, returning the closest non-self hit.
        /// </summary>
        private bool CapsuleSweep(Vector3 feetPos, Vector3 direction, float distance, out RaycastHit closest)
        {
            closest = default;
            if (direction.sqrMagnitude < 1e-8f || distance <= 0f) { return false; }

            GetSweepCapsule(feetPos, out Vector3 top, out Vector3 bot, out float radius);
            int count = Physics.CapsuleCastNonAlloc(top, bot, radius, direction.normalized, castHits, distance, movementLayerMask, QueryTriggerInteraction.Ignore);
            float min = float.PositiveInfinity;
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                RaycastHit h = castHits[i];
                if (h.transform.IsChildOf(transform)) { continue; }
                // hit.distance of 0 means we started already overlapping — skip so we can slide out instead of locking up.
                if (h.distance <= 0f) { continue; }
                if (h.distance < min) { min = h.distance; closest = h; found = true; }
            }
            return found;
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

        /// <summary>
        /// Attempt to use one of the character's jumps and decrement the remaining jumps, returning whether the jump can continue.
        /// </summary>
        /// <returns></returns>
        public bool TryUseJump()
        {
            if (jumpsRemaining > 0)
            {
                jumpsRemaining--;
                lastJumpTime = Time.fixedTime;
                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Reset the number of jumps available to the maximum jumps defined by the movement handling scriptable object
        /// </summary>
        public void ResetJumps()
        {
            jumpsRemaining = maxJumps;
        }
    }
}
