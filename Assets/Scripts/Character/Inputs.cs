using UnityEngine;

namespace Character
{
    /// <summary>
    /// A collection of inputs that are required for movement and character actions
    /// </summary>
    [System.Serializable]
    public class Inputs
    {
        public Vector3 movementVector;
        public InputButton jump;
        public InputButton dash;
        // Held to move at the movement mode's run speed instead of its walk speed. May be null (never sprinting).
        public InputButton sprint;
        // Crouches (or, pressed while sprinting, slides). May be null (never pressed), as may the buttons below.
        public InputButton crouch;
        // Starts a roll
        public InputButton roll;
        // Held to grab an object in front of the character, to push or pull it
        public InputButton grab;
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
}
