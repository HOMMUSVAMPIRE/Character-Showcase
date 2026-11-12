using UnityEngine;
using UnityEngine.InputSystem;

namespace Character
{
    /// <summary>
    /// A singleton class that manages player inputs
    /// </summary>
    public class ACInputManager : MonoBehaviour
    {
        // The singleton instance of the input manager
        public static ACInputManager instance;

        // The PlayerInput component that allows access to the unity input system
        public PlayerInput inputSource = null;

        // the current movement input data for the player
        public Inputs inputs { get; private set; }

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
            };
        }

        /// <summary>
        /// Collect inputs from Unity's input system and compile them into a format useable by the movement system
        /// </summary>
        private void Update()
        {
            var moveVector2D = inputSource.currentActionMap.FindAction("Move").ReadValue<Vector2>();
            Vector3 moveVector = new Vector3(moveVector2D.x, 0, moveVector2D.y);

            var jumpSource = inputSource.currentActionMap.FindAction("Jump");
            var jump = new InputButton()
            {
                value = jumpSource.ReadValue<float>() > 0.1f,
            };
            jump.justPressed = jump.value && !inputs.jump.value;
            jump.justReleased = !jump.value && inputs.jump.value;
            jump.holdDuration = jump.value ? (jump.justPressed ? 0 : inputs.jump.holdDuration + Time.deltaTime) : 0;
            jump.lastHoldDuration = jump.justReleased ? inputs.jump.holdDuration : inputs.jump.lastHoldDuration;

            var newInputs = new Inputs()
            {
                movementVector = moveVector,
                jump = jump,
                dash = ReadButton("Dash", inputs.dash),
                sprint = ReadButton("Sprint", inputs.sprint),
            };

            inputs = newInputs;
        }

        /// <summary>
        /// Reads a button action into an InputButton, tracking presses, releases and hold duration from its previous state.
        /// A missing action reads as never pressed.
        /// </summary>
        private InputButton ReadButton(string actionName, InputButton previous)
        {
            previous ??= new InputButton();
            var action = inputSource.currentActionMap.FindAction(actionName);
            var button = new InputButton()
            {
                value = action != null && action.ReadValue<float>() > 0.1f,
            };
            button.justPressed = button.value && !previous.value;
            button.justReleased = !button.value && previous.value;
            button.holdDuration = button.value ? (button.justPressed ? 0 : previous.holdDuration + Time.deltaTime) : 0;
            button.lastHoldDuration = button.justReleased ? previous.holdDuration : previous.lastHoldDuration;
            return button;
        }
    }
}
