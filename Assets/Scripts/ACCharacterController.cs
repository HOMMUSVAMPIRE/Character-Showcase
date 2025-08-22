using UnityEngine;

namespace Character
{
    /// <summary>
    /// The component that drives a character by delegating different categories of functionality to different components which this manages.
    /// </summary>
    public class ACCharacterController : MonoBehaviour
    {
        [Header("Subcomponent references")]
        [Tooltip("The component responsible for character movement")]
        [SerializeField] private MovementController movementController;
        [Tooltip("The component responsible for updating character animations and visuals")]
        [SerializeField] private AnimationController animationController;

        private void Update()
        {
            // TODO: collect inputs if not driven by the player
        }

        private void FixedUpdate()
        {
            // Calculate and apply movement to the character using input data
            if (movementController != null)
            {
                movementController.ProcessMovementInputs(ACInputManager.instance.inputs);

                movementController.MovementCleanup();
            }
            // Update the animation controller's movement related variables
            if (animationController != null)
            {
                // Rotate the character's visual representation smoothly to avoid unnatural "snapping"
                animationController.SmoothRotateVisuals(Time.fixedDeltaTime);
            }
        }

        private void LateUpdate()
        {
            // TODO: do any end-of frame cleanup and store data to be referenced in future frames
        }

        public void Setup()
        {
            // TODO: setup subcomponents as needed
        }

        /// <summary>
        /// This should be called by a future game manager script at the start of the frame on all active character objects to cache data about the character such as the character's bounds
        /// </summary>
        public void UpdateCaches()
        {
            
        }

    }
}
