using UnityEngine;
using UnityEngine.InputSystem;

namespace Character
{
    /// <summary>
    /// A singleton class that manages player inputs
    /// </summary>
    public class ACInputManager : MonoBehaviour
    {
        /// <summary>The singleton instance of the input manager (set in Awake; a second one destroys itself).</summary>
        public static ACInputManager instance;

        /// <summary>The PlayerInput component that allows access to the unity input system.</summary>
        [Tooltip("The PlayerInput the character's actions are read from.")]
        public PlayerInput inputSource = null;

        /// <summary>
        /// The current movement input data for the player, read anew each frame (Update). The same object (and buttons)
        /// throughout, updated in place, so reading it allocates nothing: read what you need from it each step.
        /// </summary>
        public Inputs inputs { get; private set; }

        // The actions read, looked up by name once (FindAction searches by string) and again only if the action map
        // changes; null for one the map doesn't have, which reads as never pressed.
        private InputActionMap actionMap;
        private InputAction move, jump, dash, sprint, crouch, roll, interact;

        /// <summary>
        /// Set up the singleton instance
        /// </summary>
        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(this.gameObject);
                return;
            }
            instance = this;
            inputs = new()
            {
                movementVector = Vector3.zero,
                jump = new(),
                dash = new(),
                sprint = new(),
                crouch = new(),
                roll = new(),
                grab = new(),
            };
        }

        /// <summary>
        /// Collect inputs from Unity's input system and compile them into a format useable by the movement system
        /// </summary>
        private void Update()
        {
            if (inputSource.currentActionMap != actionMap) { FindActions(inputSource.currentActionMap); }

            Vector2 moveVector2D = move != null ? move.ReadValue<Vector2>() : Vector2.zero;
            inputs.movementVector = new Vector3(moveVector2D.x, 0, moveVector2D.y);
            ReadButton(jump, inputs.jump);
            ReadButton(dash, inputs.dash);
            ReadButton(sprint, inputs.sprint);
            ReadButton(crouch, inputs.crouch);
            ReadButton(roll, inputs.roll);
            // Grabbing objects to push or pull is the Interact button, held.
            ReadButton(interact, inputs.grab);
        }

        private void FindActions(InputActionMap map)
        {
            actionMap = map;
            move = map?.FindAction("Move");
            jump = map?.FindAction("Jump");
            dash = map?.FindAction("Dash");
            sprint = map?.FindAction("Sprint");
            crouch = map?.FindAction("Crouch");
            roll = map?.FindAction("Roll");
            interact = map?.FindAction("Interact");
        }

        /// <summary>
        /// Reads a button action into its InputButton, in place, tracking presses, releases and hold duration from its
        /// state last frame. A missing action reads as never pressed.
        /// </summary>
        private static void ReadButton(InputAction action, InputButton button)
        {
            bool wasDown = button.value;
            float heldFor = button.holdDuration;
            button.value = action != null && action.ReadValue<float>() > 0.1f;
            button.justPressed = button.value && !wasDown;
            button.justReleased = !button.value && wasDown;
            button.holdDuration = button.value ? (button.justPressed ? 0 : heldFor + Time.deltaTime) : 0;
            if (button.justReleased) { button.lastHoldDuration = heldFor; }
        }
    }
}
