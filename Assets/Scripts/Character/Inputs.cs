using UnityEngine;

namespace Character
{
    /// <summary>
    /// A collection of inputs that are required for movement and character actions
    /// </summary>
    [System.Serializable]
    public class Inputs
    {
        /// <summary>
        /// The move input on the ground plane (x right, z forward, each -1 to 1), relative to the input camera:
        /// MovementController turns it to face the way the camera looks.
        /// </summary>
        public Vector3 movementVector;
        /// <summary>Jumps (and does what Jump does in the abilities that use it, such as wall jumps).</summary>
        public InputButton jump;
        /// <summary>Starts a dash.</summary>
        public InputButton dash;
        /// <summary>Held to move at the movement mode's run speed instead of its walk speed. May be null (never sprinting).</summary>
        public InputButton sprint;
        /// <summary>Crouches (or, pressed while sprinting, slides). May be null (never pressed), as may the buttons below.</summary>
        public InputButton crouch;
        /// <summary>Starts a roll.</summary>
        public InputButton roll;
        /// <summary>Held to grab an object in front of the character, to push or pull it.</summary>
        public InputButton grab;
    }

    /// <summary>
    /// Stores data on a button to be used by the input manager
    /// </summary>
    public class InputButton
    {
        /// <summary>Whether the button is down.</summary>
        public bool value;
        /// <summary>Whether the button went down this frame.</summary>
        public bool justPressed;
        /// <summary>Whether the button came up this frame.</summary>
        public bool justReleased;
        /// <summary>How long the button has been down, in seconds (0 on the frame it's pressed, and while it's up).</summary>
        public float holdDuration;
        /// <summary>
        /// How long, in seconds, the button was held the last time it was released. For how long a press has lasted, as a
        /// charge attack needs: holdDuration while the button is down, this once it's released.
        /// </summary>
        public float lastHoldDuration;
    }
}
