using UnityEngine;

namespace MeridianMotion.Characters
{
    /// <summary>
    /// A character's input as its movement reads it: the move input, and each button's state. The character works it out
    /// each step from its input frames (InputState.View: see CharacterInputFrame), so presses and hold durations are
    /// counted in steps. Scripts that drive a character (the tests, the capabilities measurement) also write one a step,
    /// of which only the move input and which buttons are down count (CharacterInputFrame.Holding): the timings are the
    /// character's own.
    /// </summary>
    [System.Serializable]
    public class StepInputs
    {
        /// <summary>
        /// The move input on the ground plane (x right, z forward, each -1 to 1), relative to the input's look (its frame's:
        /// the player's camera, for the player): the movement turns it by the look's yaw (MovementStep.lookFlat).
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
        public InputButton interact;
    }

    /// <summary>
    /// A button's state as the movement reads it (see Inputs), worked out from the steps it's been down in.
    /// </summary>
    public class InputButton
    {
        /// <summary>Whether the button is down.</summary>
        public bool held;
        /// <summary>Whether the button went down this step.</summary>
        public bool justPressed;
        /// <summary>Whether the button came up this step.</summary>
        public bool justReleased;
        /// <summary>How long the button has been down, in seconds: steps times the step's length (0 on the step it's pressed, and while it's up).</summary>
        public float holdDuration;
        /// <summary>
        /// How long, in seconds, the button was held the last time it was released. For how long a press has lasted, as a
        /// charge attack needs: holdDuration while the button is down, this once it's released.
        /// </summary>
        public float lastHoldDuration;
    }
}
