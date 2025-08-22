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
                initialPosition = float.IsNormal(rb.position.x) ? rb.position : transform.position,
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

            Vector3 gravityNorm = Vector3.down;
            var hits = Physics.SphereCastAll(position - (gravityNorm * (groundCheckRadius * 2)), groundCheckRadius, gravityNorm, groundCheckRadius * 4, movementLayerMask, QueryTriggerInteraction.Ignore);
            foreach (var hit in hits)
            {
                if (hit.transform.IsChildOf(transform)) { continue; }
                if (hit.distance == 0 || hit.distance > (groundCheckRadius * 2) + 0.05f) { continue; }
                groundTransform = hit.transform.root;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Determine whether the character can move in the way called for by it's movement data, and fix any inconsistencies
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public bool CheckMovementValid(ref MovementData data)
        {
            // pre-store some useful variables that will make this more readable later
            var capsuleHeight = 2f;
            var capsuleRadius = 0.5f;
            Vector3 relativeCapsuleTop = Vector3.up * (capsuleHeight - capsuleRadius);
            Vector3 relativeCapsuleBot = Vector3.up * capsuleRadius;

            Vector3 cast = data.finalMovementVector;
            float castMag = cast.magnitude;
            Vector3 castNorm = cast / castMag;

            Vector3 capsuleTop = data.initialPosition + relativeCapsuleTop - cast;
            Vector3 capsuleBot = data.initialPosition + relativeCapsuleBot - cast;

            float castRadius = capsuleRadius - 0.1f;

            float minDist = float.PositiveInfinity;
            Vector3 hitNorm = Vector3.zero;

            // Capsule-cast along the movement vector and for each hit...
            var hits = Physics.CapsuleCastAll(capsuleTop, capsuleBot, castRadius, castNorm, castMag * 3f, movementLayerMask, QueryTriggerInteraction.Ignore);
            foreach (var hit in hits)
            {
                // If the character has already moved past the obstacle or if the character IS the object we hit, ignore it
                if (hit.distance < castMag || hit.transform.IsChildOf(transform)) { continue; }
                // If the hit is farther than we intend to move this frame, ignore it
                if (hit.distance > castMag * 2) { continue; }

                // Add to our hit normal (see note below
                hitNorm += hit.normal;

                // Figure out which obstacle in our path we will hit first
                if (hit.distance < minDist)
                { 
                    minDist = hit.distance;
                }
            }
            hitNorm = hitNorm.normalized;

            // Adjust the character's movement to avoid moving through the obstacle
            // TODO: this requires a lot of refinement, particularly on inclined surfaces to avoid slipping
            if (float.IsFinite(minDist) && minDist < castMag * 2f)
            {
                float cutDist = ((castMag * 2) - minDist);
                float parallel = -Vector3.Dot(hitNorm, castNorm);
                float perpendicular = 1 - parallel;
                float xzCutDist = cutDist * perpendicular;
                float yCutDist = cutDist * parallel;
                data.ForceMovementVector(cast + hitNorm * cutDist);
            }

            return true;
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
            AddVelocityChange(force * Time.deltaTime / rb.mass);
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
