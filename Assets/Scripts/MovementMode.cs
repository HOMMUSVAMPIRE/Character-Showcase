using UnityEngine;
using static Character.CharacterUtilities;

namespace Character
{
    public abstract class MovementMode : ScriptableObject
    {
        public virtual void ProcessMovementData(ref MovementData movementData, Inputs inputs)
        {
            return;
        }
    }

    [CreateAssetMenu(fileName = "BasicMovementMode", menuName = "Scriptable Objects/MovementModes/Basic")]
    public abstract class BasicMovementMode : MovementMode, WalkRunMode, JumpMode
    {
        [SerializeField] private float walkSpeed;
        [SerializeField] private float runSpeed;
        [SerializeField] private float jumpStrength;

        float WalkRunMode.walkSpeed { get => walkSpeed; set => walkSpeed = value; }
        float WalkRunMode.runSpeed { get => runSpeed; set => runSpeed = value; }
        float JumpMode.jumpStrength { get => jumpStrength; set => jumpStrength = value; }

        public bool CanJump(MovementData data)
        {
            return data.initialGrounded;
        }

        public override void ProcessMovementData(ref MovementData movementData, Inputs inputs)
        {
            (this as WalkRunMode).ProcessMovementDataForWalk(ref movementData, inputs);
            (this as JumpMode).ProcessMovementDataForJump(ref movementData, inputs);
        }
    }

    public interface WalkRunMode
    {
        public abstract float walkSpeed { get; set; }
        public abstract float runSpeed { get; set; }

        public virtual void ProcessMovementDataForWalk(ref MovementData movementData, Inputs inputs)
        {
            movementData.directMovementVector = movementData.cameraLookFlat * inputs.movementVector * walkSpeed * Time.fixedDeltaTime;
            movementData.targetRotation = Quaternion.LookRotation(movementData.directMovementVector, Vector3.up);
        }
    }
    
    public interface JumpMode
    {
        public abstract float jumpStrength {get; set;}

        public abstract bool CanJump(MovementData data);

        public virtual void ProcessMovementDataForJump(ref MovementData movementData, Inputs inputs)
        {
            movementData.velocityChange += Vector3.up * jumpStrength;
        }
    }
}
