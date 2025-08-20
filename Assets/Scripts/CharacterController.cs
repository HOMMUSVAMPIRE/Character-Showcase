using UnityEngine;

namespace Character
{
    public class CharacterController : MonoBehaviour
    {
        MovementController movementController;
        AnimationController animationController;

        private void OnEnable()
        {
            
        }

        private void OnDisable()
        {
            
        }

        private void Update()
        {
            UpdateCaches();
        }

        private void FixedUpdate()
        {
            ProcessInputs();
        }

        private void LateUpdate()
        {
            
        }

        public void Setup()
        {

        }

        public void UpdateCaches()
        {

        }

        public void ProcessInputs()
        {
            if (movementController != null)
            {
                movementController.ProcessMovementInputs(InputManager.instance.inputs);
            }
        }
    }
}
