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

        // How far below the feet the floor can be while still counting as grounded. The ground snap always closes
        // this gap, so the character never hovers inside it.
        private const float GroundedTolerance = 0.1f;

        private void Awake()
        {
            if (capsule == null) { capsule = GetComponent<CapsuleCollider>(); }
        }

        [Tooltip("The transform on which the character is currently standing")]
        public Transform groundTransform;
        // TODO: A series of matrices which will be used to move the character with whatever they are standing on prior to movement calculations
        private Matrix4x4 lastFrameGroundTransformToWorldMatrix;
        private Matrix4x4 groundTransformToWorldMatrix;
        private Matrix4x4 worldToGroundTransformMatrix;

        // How many more times the character can begin a jump (gets reset when grounded)
        [HideInInspector] public int jumpsRemaining = 0;
        // The maximum number of jumps the character can perform in succession
        public int maxJumps => movementHandler is JumpMode ? (movementHandler as JumpMode).maxJumps : 0;

        // The time at which the character was last on the ground
        [HideInInspector] public float lastGroundedTime = 0f;

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
                initialRotation = transform.rotation,
                cameraLook = cameraTransform.rotation,
            };

            data.previousState = hasLastFrameMovement ? lastFrameMovementData.state : CharacterState.Idle;
            data.state = CharacterState.Idle;

            data.initialInertia = hasLastFrameMovement ? lastFrameMovementData.finalInertia : Vector3.zero;
            data.initialVelocity = hasLastFrameMovement ? lastFrameMovementData.finalVelocity : Vector3.zero;

            // Check whether the character is starting on the ground this frame via spherecast downwards
            // TODO: avoid hard coding gravity direction
            data.initialGrounded = CheckGrounded(data.initialPosition, out groundTransform);
            groundTransformToWorldMatrix = groundTransform?.localToWorldMatrix ?? Matrix4x4.identity;
            worldToGroundTransformMatrix = groundTransform?.worldToLocalMatrix ?? Matrix4x4.identity;
            data.initialGroundTransform = groundTransform;

            // If we are on the ground, update related data on the movement data object
            if (data.initialGrounded)
            {
                lastGroundedTime = Time.fixedTime;
                data.lastGroundedTime = lastGroundedTime;

                data.state = data.state | CharacterState.Grounded;
            }

            // Setup initial movement data before calculation to avoid weird behaviour
            data.directMovementVector = Vector3.zero;
            data.targetRotation = rb.rotation;

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

            float r = groundCheckRadius > 0f ? groundCheckRadius : (capsule != null ? Mathf.Max(0.05f, capsule.radius - 0.05f) : 0.45f);
            // Start with the bottom of the sphere just above the feet so we can detect ground slightly below them,
            // and reject things above them.
            const float startLift = 0.02f;
            Vector3 start = position + Vector3.up * (r + startLift);
            // Sweep the sphere's bottom from startLift above the feet to GroundedTolerance below them. Distance is
            // measured from the sphere's bottom, so the radius must NOT be added here — doing so made the character
            // count as grounded up to (r + tolerance) above the floor, cancelling their fall while still in the air.
            float castDist = startLift + GroundedTolerance;

            var hits = Physics.SphereCastAll(start, r, Vector3.down, castDist, movementLayerMask, QueryTriggerInteraction.Ignore);
            float best = float.PositiveInfinity;
            foreach (var hit in hits)
            {
                if (hit.transform.IsChildOf(transform)) { continue; }
                if (hit.distance <= 0f) { continue; }
                if (Vector3.Angle(hit.normal, Vector3.up) > maxSlopeAngle) { continue; }
                if (hit.distance < best)
                {
                    best = hit.distance;
                    groundTransform = hit.transform.root;
                }
            }
            return float.IsFinite(best);
        }

        /// <summary>
        /// Resolve the character's intended motion against the world: sweep the capsule, slide along
        /// walkable slopes, step up onto low ledges (stairs), and stick to descending slopes.
        /// </summary>
        public bool CheckMovementValid(ref MovementData data)
        {
            Vector3 move = data.finalMovementVector;
            if (move.sqrMagnitude < 1e-12f) { return true; }

            Vector3 pos = data.initialPosition;

            // Split into horizontal (slide/step-up) and vertical (gravity/jump) components so they resolve independently.
            Vector3 horiz = new Vector3(move.x, 0f, move.z);
            float vert = move.y;

            Vector3 horizResolved = ResolveHorizontal(pos, horiz, data.initialGrounded);
            pos += horizResolved;

            Vector3 vertResolved = ResolveVertical(pos, vert);
            pos += vertResolved;

            Vector3 totalResolved = horizResolved + vertResolved;

            // Snap to ground while grounded so the character sits on the floor and follows descending slopes.
            // Allowed drop is the larger of:
            //  - GroundedTolerance: the gap CheckGrounded already counts as "on the ground". Grounded gravity is tiny
            //    (inertia is reset each grounded frame), so without closing this gap the character hovers above the
            //    floor for a while after landing.
            //  - The most a walkable slope could drop over the horizontal distance moved this frame.
            // Anything further is a ledge, and gravity handles it (so walking off a ledge isn't a teleport).
            if (data.initialGrounded && vert <= 0f)
            {
                float horizDist = new Vector2(horizResolved.x, horizResolved.z).magnitude;
                float maxSlopeDrop = Mathf.Min(groundSnapDistance, horizDist * Mathf.Tan(maxSlopeAngle * Mathf.Deg2Rad));
                float maxDrop = Mathf.Max(GroundedTolerance, maxSlopeDrop) + skinWidth;
                if (CapsuleSweep(pos, Vector3.down, maxDrop + skinWidth, out var snapHit)
                    && Vector3.Angle(snapHit.normal, Vector3.up) <= maxSlopeAngle)
                {
                    float drop = Mathf.Max(0f, snapHit.distance - skinWidth);
                    if (drop <= maxDrop) { totalResolved += Vector3.down * drop; }
                }
            }

            data.ForceMovementVector(totalResolved);
            return true;
        }

        /// <summary>
        /// Resolve horizontal motion: sweep forward, slide along walls/walkable slopes, attempt step-up for low ledges.
        /// </summary>
        private Vector3 ResolveHorizontal(Vector3 feetPos, Vector3 horizMove, bool grounded)
        {
            float mag = horizMove.magnitude;
            if (mag < 1e-6f) { return Vector3.zero; }
            Vector3 dir = horizMove / mag;

            if (!CapsuleSweep(feetPos, dir, mag + skinWidth, out var hit))
            {
                return horizMove;
            }

            float travel = Mathf.Max(0f, hit.distance - skinWidth);
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

            // Wall: slide along it in the horizontal plane.
            Vector3 wallNormal = new Vector3(hit.normal.x, 0f, hit.normal.z);
            if (wallNormal.sqrMagnitude < 1e-6f) { return traveled; }
            wallNormal.Normalize();
            Vector3 slide = Vector3.ProjectOnPlane(remaining, wallNormal);
            slide.y = 0f;
            float slideMag = slide.magnitude;
            if (slideMag < 1e-6f) { return traveled; }

            // Make sure the slide direction isn't itself blocked.
            if (CapsuleSweep(feetPos + traveled, slide / slideMag, slideMag + skinWidth, out var slideHit))
            {
                float slideTravel = Mathf.Max(0f, slideHit.distance - skinWidth);
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
            if (CapsuleSweep(feetPos, Vector3.up, maxStepHeight + skinWidth, out _)) { return Vector3.zero; }
            Vector3 raised = feetPos + Vector3.up * maxStepHeight;

            // 2) Sweep forward from the raised position.
            if (CapsuleSweep(raised, dir, distance + skinWidth, out var fwdHit))
            {
                float travel = Mathf.Max(0f, fwdHit.distance - skinWidth);
                if (travel < 1e-4f) { return Vector3.zero; }
                raised += dir * travel;
            }
            else
            {
                raised += dir * distance;
            }

            // 3) Drop back down and make sure we land on walkable ground.
            if (!CapsuleSweep(raised, Vector3.down, maxStepHeight + skinWidth, out var downHit)) { return Vector3.zero; }
            if (Vector3.Angle(downHit.normal, Vector3.up) > maxSlopeAngle) { return Vector3.zero; }

            float drop = Mathf.Max(0f, downHit.distance - skinWidth);
            Vector3 landed = raised - Vector3.up * drop;
            // Only accept if we actually ended up above the starting foot height (otherwise this isn't a step up).
            if (landed.y <= feetPos.y + 1e-4f) { return Vector3.zero; }
            return landed - feetPos;
        }

        /// <summary>
        /// Resolve vertical motion: sweep up/down and clamp at the nearest contact.
        /// </summary>
        private Vector3 ResolveVertical(Vector3 feetPos, float verticalDelta)
        {
            if (Mathf.Abs(verticalDelta) < 1e-6f) { return Vector3.zero; }
            Vector3 dir = verticalDelta > 0f ? Vector3.up : Vector3.down;
            float mag = Mathf.Abs(verticalDelta);
            if (!CapsuleSweep(feetPos, dir, mag + skinWidth, out var hit)) { return dir * mag; }
            float travel = Mathf.Max(0f, hit.distance - skinWidth);
            return dir * travel;
        }

        /// <summary>
        /// Capsule-sweep from a feet-anchored position in the given direction, returning the closest non-self hit.
        /// </summary>
        private bool CapsuleSweep(Vector3 feetPos, Vector3 direction, float distance, out RaycastHit closest)
        {
            closest = default;
            if (direction.sqrMagnitude < 1e-8f || distance <= 0f) { return false; }

            float radius = (capsule != null ? capsule.radius : 0.5f) - skinWidth;
            if (radius <= 0f) { radius = 0.01f; }
            float height = capsule != null ? Mathf.Max(capsule.height, capsule.radius * 2f) : 2f;
            Vector3 localCenter = capsule != null ? capsule.center : new Vector3(0f, 1f, 0f);
            float halfSegment = Mathf.Max(0f, (height * 0.5f) - radius - skinWidth);
            Vector3 center = feetPos + localCenter;
            Vector3 bot = center - Vector3.up * halfSegment;
            Vector3 top = center + Vector3.up * halfSegment;

            var hits = Physics.CapsuleCastAll(top, bot, radius, direction.normalized, distance, movementLayerMask, QueryTriggerInteraction.Ignore);
            float min = float.PositiveInfinity;
            bool found = false;
            foreach (var h in hits)
            {
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
                rb.Move(pos, rot);
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }

        /// <summary>
        /// At the end of the frame, store information in the last frame data for use next frame
        /// </summary>
        public void MovementCleanup()
        {
            lastFrameMovementData = currentMovementData;
            currentMovementData = null;

            lastFrameGroundTransformToWorldMatrix = groundTransformToWorldMatrix;
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
