using UnityEngine;
using static Character.CharacterUtilities;

namespace Character
{
    /// <summary>
    /// A Scriptable Object type which defines a very standard character movement scheme
    /// </summary>
    [CreateAssetMenu(fileName = "BasicMovementMode", menuName = "Scriptable Objects/MovementModes/Basic")] [System.Serializable]
    public class BasicMovementMode : MovementMode, WalkRunMode, JumpMode
    {
        [Header("Speed")]
        [Tooltip("How fast the character moves by default")]
        [SerializeField] private float walkSpeed;
        [Tooltip("How fast the character moves when sprinting (not yet implemented)")]
        [SerializeField] private float runSpeed;

        [Header("Jumping")]
        // TODO: Replace these with a jump height and calculations to determine needed velocity from height
        [Tooltip("A multiplier for how fast the player moves in a jump")]
        [SerializeField] private float jumpStrength;
        [Tooltip("A curve which determines the impact of holding down the jump button on continued upwards momentum")]
        [SerializeField] private AnimationCurve jumpStrengthCurve = new();
        [SerializeField] private int maxJumps;

        [Header("Gravity & Falling")]
        [Tooltip("The base acceleration applied to the character when falling")]
        [SerializeField] private float gravityStrength;
        [Tooltip("The maximum falling speed the player can reach. (if 0, do not clamp fall speed)")]
        [SerializeField] private float maxFallSpeed;
        [Tooltip("How long after being grounded the player can still jump as if grounded")]
        [SerializeField] private float coyoteTime;
        [Tooltip("A curve that determines the impact of gravity during coyote time")]
        [SerializeField] private AnimationCurve coyoteTimeGravityCurve = new();

        [Header("Air Steering")]
        [Tooltip("The maximum strength with which the player can control their character's movement while in the air")]
        [SerializeField] private float airSteeringStrength;
        [Tooltip("A curve that determines how quickly after leaving the ground that the player can steer their character in the air")]
        [SerializeField] private AnimationCurve airSteeringStrengthCurve = new();

        [Header("QualityOfLife")]
        [Tooltip("Dampens the impact of horizontal velocity on the character's movement while grounded")]
        [SerializeField] private float groundedDampenHorizontalInertia = 10f;

        // Mandatory Interaface Variables
        float WalkRunMode.walkSpeed { get => walkSpeed; set => walkSpeed = value; }
        float WalkRunMode.runSpeed { get => runSpeed; set => runSpeed = value; }
        float JumpMode.jumpStrength { get => jumpStrength; set => jumpStrength = value; }
        int JumpMode.maxJumps { get => maxJumps; set => maxJumps = value; }

        /// <summary>
        /// Determines whether the character can perform a jump
        /// </summary>
        /// <param name="data"></param>
        /// <param name="controller"></param>
        /// <returns></returns>
        public bool CanJump(MovementData data, MovementController controller)
        {
            return data.previousState.HasFlag(CharacterState.Jumping) || controller.TryUseJump();
        }

        /// <summary>
        /// Uses inputs and the starting data for the character at the beginning of the frame to fill out a movement data object
        /// with data on how the character should move this frame.
        /// </summary>
        /// <param name="movementData"></param>
        /// <param name="inputs"></param>
        /// <param name="controller"></param>
        public override void ProcessMovementData(ref MovementData movementData, Inputs inputs, MovementController controller)
        {
            // How much gravity do we apply
            // TODO: this could be it's own function, but for now, this suffices
            float g = gravityStrength * (movementData.initialGrounded ? 0.5f : coyoteTimeGravityCurve.Evaluate(Time.fixedTime - movementData.lastGroundedTime));
            // Apply gravity to the character
            movementData.velocityChange += Vector3.down * g * movementData.deltaTime;

            // If we are on the ground, limit the downward velocity - we do want some, so that we stick to the ground, but not too much
            if (movementData.initialGrounded)
            {
                movementData.initialVelocity = new Vector3(movementData.initialVelocity.x, -g, movementData.initialVelocity.z);
            }

            // Apply movement input to the character
            (this as WalkRunMode).ProcessMovementDataForWalk(ref movementData, inputs, controller);
            // Apply jump input to the character
            (this as JumpMode).ProcessMovementDataForJump(ref movementData, inputs, controller);

            // If we are falling too fast, limit our fall speed.
            if (movementData.finalVelocity.y < -maxFallSpeed)
            {
                movementData.ForceMovementVector(new Vector3(movementData.finalMovementVector.x, maxFallSpeed * movementData.deltaTime * -1f, movementData.finalMovementVector.z));
            }
        }

        /// <summary>
        /// Handles applying horizontal movement to the player based on wasd or joystick input
        /// </summary>
        /// <param name="movementData"></param>
        /// <param name="inputs"></param>
        /// <param name="controller"></param>
        public void ProcessMovementDataForWalk(ref MovementData movementData, Inputs inputs, MovementController controller)
        {
            float steeringEffectiveness = controller.movementSpeedMultiplier;

            if (movementData.initialGrounded)
            {
                // We are grounded, so to limit sliding, kill horizontal momentum over time to simulate friction
                movementData.initialInertia = Vector3.Lerp(movementData.initialInertia, Vector3.up * movementData.initialInertia.y, groundedDampenHorizontalInertia * movementData.deltaTime);
            }
            else
            {
                // We are not grounded, so determine the extent to which we are able to move
                steeringEffectiveness *= airSteeringStrengthCurve.Evaluate(Time.fixedTime - movementData.lastGroundedTime) * airSteeringStrength;
            }

            // if there is any input and we can move, Move the character at their walking speed in the direction of their input, relative to the camera. Face the direction they are moving.
            if (inputs.movementVector.sqrMagnitude > 0.001f && steeringEffectiveness > 0)
            {
                movementData.directMovementVector = movementData.cameraLookFlat * inputs.movementVector * steeringEffectiveness * walkSpeed * Time.fixedDeltaTime;
                movementData.targetRotation = Quaternion.Lerp(movementData.initialRotation, Quaternion.LookRotation(movementData.directMovementVector.sqrMagnitude > 0 ? movementData.directMovementVector : movementData.initialRotation * Vector3.forward, Vector3.up), steeringEffectiveness);
            }

            // Affect the character's state
            movementData.state = movementData.state | CharacterState.Moving;
        }

        /// <summary>
        /// Reads jump button input and translates that into satisfying jumps
        /// </summary>
        /// <param name="movementData"></param>
        /// <param name="inputs"></param>
        /// <param name="controller"></param>
        public void ProcessMovementDataForJump(ref MovementData movementData, Inputs inputs, MovementController controller)
        {
            // If we're on the ground and not already in a jump, reset the number of jumps available to us
            if (!movementData.previousState.HasFlag(CharacterState.Jumping) && ((Time.fixedTime - movementData.lastGroundedTime) <= coyoteTime))
            {
                controller.ResetJumps();
            }

            // When the jump button is down, and we can perform a jump, do a jump
            if (inputs.jump.value && CanJump(movementData, controller))
            {
                // Figure out what our velocity will be this frame of the jump
                // TODO: As noted before, this should ideally be calculated based on a jump height, but for now, this is based on a velocity curve
                float jumpVelocity = controller.jumpStrengthModifier * jumpStrengthCurve.Evaluate(inputs.jump.holdDuration) * jumpStrength;
                if (jumpVelocity <= 0) { return; }

                // Stop downwards momentum that previously applied to the character
                movementData.initialInertia = new Vector3(movementData.initialInertia.x, Mathf.Max(movementData.initialInertia.y, 0), movementData.initialInertia.z);
                // Add vertical velocity using the jump velocity calculated above
                movementData.velocityChange = new Vector3(movementData.velocityChange.x, jumpVelocity - movementData.initialVelocity.y, movementData.velocityChange.z);
                // Add horizontal velocity if the character was moving before the jump so that the air steering curve does not kill preexisting horizontal momentum
                movementData.velocityChange += (movementData.cameraLookFlat * inputs.movementVector * walkSpeed * movementData.deltaTime);

                // Affect the character's state for animation implementation at a later date
                movementData.state = movementData.state | CharacterState.Jumping;
                movementData.state = movementData.state & (~CharacterState.Grounded);
            }

            // If a jump just ended, severely dampen vertical momentum to make the end of a jump feel responsive
            if (movementData.previousState.HasFlag(CharacterState.Jumping) && !movementData.state.HasFlag(CharacterState.Jumping))
            {
                movementData.initialInertia = new Vector3(movementData.initialInertia.x, Mathf.Lerp(movementData.initialVelocity.y, Mathf.Min(movementData.initialVelocity.y, 0f), 0.9f), movementData.initialInertia.z);
            }
        }
    }
}
