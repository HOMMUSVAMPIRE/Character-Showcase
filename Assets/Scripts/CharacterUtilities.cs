using UnityEngine;

namespace Character
{
    /// <summary>
    /// A home for any static utility functions related to character operation here
    /// </summary>
    public static class CharacterUtilities
    { 
    }
    
    /// <summary>
    /// An enum which stores animation-relevant stateful information about a character
    /// </summary>
    [System.Flags]
    public enum CharacterState
    {
        Idle = 0,
        Moving = 1,
        Grounded = 2,
        Jumping = 4,
        Dashing = 8,
        Running = 16,
        Dead = 32,
    }

    /// <summary>
    /// A collection of inputs that are required for movement and character actions
    /// </summary>
    [System.Serializable]
    public class Inputs
    {
        public Vector3 movementVector;
        public InputButton jump;
        public InputButton dash;
    }

    /// <summary>
    /// Stores data on a button to be used by the input manager
    /// </summary>
    public class InputButton
    {
        public bool value;
        public bool justPressed;
        public bool justReleased;
        public float holdDuration;
        public float held;
        public float lastHoldDuration;
    }

    /// <summary>
    /// A container for information on a character's movement in a frame
    /// </summary>
    [System.Serializable]
    public class MovementData
    {
        // The view that inputs this frame are based on
        private Quaternion _cameraLook;
        public Quaternion cameraLookFlat { get; private set; }
        public Vector3 cameraForward { get; private set; }

        public Quaternion cameraLook
        {
            get { return _cameraLook; } set { _cameraLook = value; cameraLookFlat = Quaternion.Euler(0, value.eulerAngles.y, 0); cameraForward = value * Vector3.forward; }
        }

        // The delta time variable to use in movement calculations (usually Time.fixedDeltaTime, but could theoretically be run in an update loop)
        public float deltaTime { get; set; }

        // A timestamp for when the character was last on the ground
        public float lastGroundedTime = 0f;

        // The state of the character on the previous frame
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
        public Vector3 initialPosition { get { return _initialPosition; } set { _initialPosition = value; dirty = true; } }
        public Quaternion initialRotation { get { return _initialRotation; } set { _initialRotation = value; dirty = true; } }
        public Vector3 initialInertia { get { return _initialInertia; } set { _initialInertia = value; dirty = true; } }
        public Vector3 initialVelocity { get { return _initialVelocity; } set { _initialVelocity = value; dirty = true; } }
        public Transform initialGroundTransform { get { return _initialGroundTransform; } set { _initialGroundTransform = value; dirty = true; } }
        public bool initialGrounded { get { return _initialGrounded; } set { _initialGrounded = value; dirty = true; } }
        public Vector3 directMovementVector { get { return _directMovementVector; } set { _directMovementVector = value; dirty = true; } }
        public Vector3 velocityChange { get { return _velocityChange; } set { _velocityChange = value; dirty = true; } }
        public Vector3? velocityOverride { get { return _velocityOverride; } set { _velocityOverride = value; dirty = true; } }
        public Quaternion targetRotation { get { return _targetRotation; } set { _targetRotation = value; dirty = true; } }

        // The state of the character during this frame
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

        // Accessors to the above final variables. The set accessor re-computes all results if the movement data object is dirty
        public Vector3 finalPosition { get { ComputeResults();  return _finalPosition; } private set { _finalPosition = value; } }
        public Vector3 finalMovementVector { get { ComputeResults(); return _finalMovementVector; } private set { _finalMovementVector = value; } }
        public Quaternion finalRotation { get { ComputeResults(); return _finalRotation; } private set { _finalRotation = value; } }
        public Vector3 finalInertia { get { ComputeResults(); return _finalInertia; } private set { _finalInertia = value; } }
        public Vector3 finalVelocity { get { ComputeResults(); return _finalVelocity; } private set { _finalVelocity = value; } }

        // The transform on which the character will be standing at the end of the frame
        public Transform finalGroundTransform;
        // Whether the character will be on the ground at the end of the frame
        public bool finalGrounded;

        // Calculates the final state of the character based on the starting data and the intended motion data
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
            }

            finalInertia = velocityOverride ?? initialInertia + velocityChange;
            finalVelocity = finalInertia  + (directMovementVector / deltaTime);
            finalMovementVector = (finalInertia * deltaTime) + directMovementVector;
            finalPosition = initialPosition + finalMovementVector;

            finalRotation = targetRotation;

            computing = false;
            dirty = false;
        }

        // Forces the final movement vector to be a specific value - used to account for collisions
        public void ForceMovementVector(Vector3 moveVector)
        {
            directMovementVector += moveVector - (finalMovementVector);
            _finalInertia = _finalVelocity;
        }
    }
}
