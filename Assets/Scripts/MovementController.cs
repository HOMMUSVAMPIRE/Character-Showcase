using UnityEngine;
using static Character.CharacterUtilities;

namespace Character
{
    public class MovementController : MonoBehaviour
    {
        [SerializeField]
        private CharacterController characterController;
        [SerializeField]
        private Rigidbody rb;

        public Transform cameraTransform => transform; // point this to a reference on character controller later

        public static MovementMode movementHandler;

        public MovementData lastFrameMovementData = null;
        public MovementData currentMovementData = null;

        public Vector3 pendingVelocityChange;
        public Vector3? velocityOverride = null;

        public float movementSpeedMultiplier;
        public float jumpStrengthModifier;

        public Vector3 gravity;

        public float groundCheckRadius;

        public LayerMask movementLayerMask = 0;

        public Transform groundTransform;
        public Matrix4x4 lastFrameGroundTransformToWorldMatrix;
        public Matrix4x4 groundTransformToWorldMatrix;
        public Matrix4x4 worldToGroundTransformMatrix;

        public void ProcessMovementInputs(Inputs inputs)
        {
            MovementData data = new MovementData()
            {
                initialPosition = lastFrameMovementData.finalPosition,
                initialRotation = lastFrameMovementData.finalRotation,
                initialGroundTransform = lastFrameMovementData.finalGroundTransform,
                initialInertia = lastFrameMovementData.finalInertia,
                initialVelocity = lastFrameMovementData.finalVelocity,
                cameraLook = cameraTransform.rotation,
            };

            data.initialGrounded = CheckGrounded(data.initialPosition, gravity, out groundTransform);
            groundTransformToWorldMatrix = groundTransform.localToWorldMatrix;
            worldToGroundTransformMatrix = groundTransform.worldToLocalMatrix;
            data.initialGroundTransform = groundTransform;

            data.velocityChange = pendingVelocityChange + gravity * Time.fixedDeltaTime;
            pendingVelocityChange = Vector3.zero;

            data.velocityOverride = velocityOverride;
            velocityOverride = null;

            if (movementHandler != null)
            {
                movementHandler.ProcessMovementData(ref data, inputs);
            }

            CheckMovementValid(ref data);

            CompleteMovement(data);
        }

        public bool CheckGrounded(Vector3 position, Vector3 gravity, out Transform groundTransform)
        {
            groundTransform = null;

            if (gravity.sqrMagnitude < 0.001f) { return false; }

            Vector3 gravityNorm = gravity.normalized;
            var hits = Physics.SphereCastAll(position - (gravityNorm * (groundCheckRadius * 2)), groundCheckRadius, gravityNorm, groundCheckRadius * 4, movementLayerMask, QueryTriggerInteraction.Ignore);
            foreach (var hit in hits)
            {
                if (hit.transform.IsChildOf(transform)) { continue; }
                if (hit.distance == 0) { continue; }
                groundTransform = hit.transform.root;
                return true;
            }
            return false;
        }

        public bool CheckMovementValid(ref MovementData data)
        {
            return true;
        }

        public void CompleteMovement(MovementData movementData = null)
        {
            if (movementData == null) {movementData = currentMovementData;}
            if (rb != null)
            {
                rb.Move(movementData.finalPosition, movementData.finalRotation);
            }
        }

        public void MovementCleanup()
        {
            lastFrameMovementData = currentMovementData;
            currentMovementData = null;

            lastFrameGroundTransformToWorldMatrix = groundTransformToWorldMatrix;
        }

        public void AddForce(Vector3 force)
        {
            pendingVelocityChange += force * Time.deltaTime / rb.mass;
        }

        public void AddVelocityChange(Vector3 velocityChange)
        {
            pendingVelocityChange += velocityChange;
        }

        public void OverrideVelocity(Vector3 newVelocity)
        {
            velocityOverride = newVelocity;
        }
    }
}
