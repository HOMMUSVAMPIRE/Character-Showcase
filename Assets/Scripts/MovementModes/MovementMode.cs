using UnityEngine;
using static Character.CharacterUtilities;

namespace Character
{
    // The base class for scriptable objects which control the ways characters move.
    public abstract class MovementMode : ScriptableObject
    {
        public virtual void ProcessMovementData(ref MovementData movementData, Inputs inputs, MovementController controller)
        {
            return;
        }
    }

    /// <summary>
    /// An interface defining the functions necessary for a deriving class to handle movement related functionality
    /// </summary>
    public interface WalkRunMode
    {
        public abstract float walkSpeed { get; set; }
        public abstract float runSpeed { get; set; }

        public virtual void ProcessMovementDataForWalk(ref MovementData movementData, Inputs inputs, MovementController controller)
        {
            if (inputs.movementVector.sqrMagnitude > 0.001f)
            {
                movementData.directMovementVector = movementData.cameraLookFlat * inputs.movementVector * walkSpeed * Time.fixedDeltaTime;
                movementData.targetRotation = Quaternion.LookRotation(movementData.directMovementVector.sqrMagnitude > 0 ? movementData.directMovementVector : movementData.initialRotation * Vector3.forward, Vector3.up);
            }
            movementData.state = movementData.state | CharacterState.Moving;
        }
    }

    /// <summary>
    /// An interface defining the functions necessary for a deriving class to handle jumping related functionality
    /// </summary>
    public interface JumpMode
    {
        public abstract int maxJumps { get; set; }
        public abstract float jumpStrength {get; set;}

        public abstract bool CanJump(MovementData data, MovementController controller);

        public virtual void ProcessMovementDataForJump(ref MovementData movementData, Inputs inputs, MovementController controller)
        {
            if (CanJump(movementData, controller) && inputs.jump.value)
            {
                movementData.velocityChange += Vector3.up * jumpStrength;
                movementData.state = movementData.state | CharacterState.Jumping;
            }
        }
    }

    // TODO: Add an interface defining dash-related functions
}
