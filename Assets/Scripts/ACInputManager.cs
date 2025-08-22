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
            if (instance != null)
            {
                Destroy(this.gameObject);
            }
            else
            {
                instance = this;
            }
            inputs = new()
            {
                movementVector = Vector3.zero,
                jump = new(),
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
                jump = jump
            };

            inputs = newInputs;
        }
    }
}
