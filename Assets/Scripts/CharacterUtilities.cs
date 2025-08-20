using UnityEngine;

namespace Character
{
    public static class CharacterUtilities
    {
        [System.Flags]
        public enum CharacterState
        {
            Idle = 0,
            Moving,
            Grounded,
            Jumping,
            Dashing,
            Running,
            Dead,
        }
    }

    [System.Serializable]
    public class Inputs
    {
        public Vector3 movementVector;
        public InputButton jump;
        public InputButton dash;
    }

    public class InputButton
    {
        public bool value;
        public bool justPressed;
        public bool justReleased;
        public float holdDuration;
        public float held;
        public float lastHoldDuration;
    }

    [System.Serializable]
    public class MovementData
    {
        private Quaternion _cameraLook;
        public Quaternion cameraLookFlat { get; private set; }
        public Vector3 cameraForward { get; private set; }

        public Quaternion cameraLook
        {
            get { return _cameraLook; } set { _cameraLook = value; cameraLookFlat = Quaternion.Euler(0, value.eulerAngles.y, 0); cameraForward = value * Vector3.forward; }
        }

        private float deltaTime;

        private Vector3 _initialPosition;
        private Quaternion _initialRotation;
        private Vector3 _initialInertia;
        private Vector3 _initialVelocity;
        private Transform _initialGroundTransform;
        private bool _initialGrounded;

        public Vector3 initialPosition { get { return _initialPosition; } set { _initialPosition = value; dirty = true; } }
        public Quaternion initialRotation { get { return _initialRotation; } set { _initialRotation = value; dirty = true; } }
        public Vector3 initialInertia { get { return _initialInertia; } set { _initialInertia = value; dirty = true; } }
        public Vector3 initialVelocity { get { return _initialVelocity; } set { _initialVelocity = value; dirty = true; } }
        public Transform initialGroundTransform { get { return _initialGroundTransform; } set { _initialGroundTransform = value; dirty = true; } }
        public bool initialGrounded { get { return _initialGrounded; } set { _initialGrounded = value; dirty = true; } }

        private Vector3 _directMovementVector;
        private Vector3 _velocityChange;
        private Vector3? _velocityOverride;
        private Quaternion _targetRotation;

        public Vector3 directMovementVector { get { return _directMovementVector; } set { _directMovementVector = value; dirty = true; } }
        public Vector3 velocityChange { get { return _velocityChange; } set { _velocityChange = value; dirty = true; } }
        public Vector3? velocityOverride { get { return _velocityOverride; } set { _velocityOverride = value; dirty = true; } }
        public Quaternion targetRotation { get { return _targetRotation; } set { _targetRotation = value; dirty = true; } }

        private CharacterUtilities.CharacterState state;

        private bool dirty = true;

        private Vector3 _finalPosition;
        private Vector3 _finalMovementVector;
        private Quaternion _finalRotation;
        private Vector3 _finalInertia;
        private Vector3 _finalVelocity;

        public Vector3 finalPosition { get { ComputeResults();  return _finalPosition; } private set { _finalPosition = value; } }
        public Vector3 finalMovementVector { get { ComputeResults(); return _finalMovementVector; } private set { _finalMovementVector = value; } }
        public Quaternion finalRotation { get { ComputeResults(); return _finalRotation; } private set { _finalRotation = value; } }
        public Vector3 finalInertia { get { ComputeResults(); return _finalInertia; } private set { _finalInertia = value; } }
        public Vector3 finalVelocity { get { ComputeResults(); return _finalVelocity; } private set { _finalVelocity = value; } }

        public Transform finalGroundTransform;
        public bool finalGrounded;

        public void ComputeResults()
        {
            if (!dirty) { return; }
            finalInertia = velocityOverride ?? initialInertia + velocityChange;
            finalVelocity = finalInertia  + (directMovementVector / deltaTime);
            finalMovementVector = (finalInertia * deltaTime) + directMovementVector;
            finalPosition = initialPosition + finalMovementVector;

            finalRotation = targetRotation;
            
            dirty = false;
        }
    }
}
