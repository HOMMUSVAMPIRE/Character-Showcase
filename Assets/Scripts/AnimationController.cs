using UnityEngine;

namespace Character
{
    /// <summary>
    /// A character controller component that acts as an interface to the animator component
    /// Very much still a WIP
    /// </summary>
    public class AnimationController : MonoBehaviour
    {
        [SerializeField] [Tooltip("A reference to the parent character controller")]
        private ACCharacterController characterController;

        [SerializeField] [Tooltip("A reference to the character model's root transform that is a child of this transform")]
        private GameObject characterModelRoot;
        [SerializeField] [Tooltip("A reference to the animator component on the character model")]
        private Animator animator;

        [Header("Smooth Rotation Variables")]
        [SerializeField] [Tooltip("The speed at which the character model smoothly rotates to match the parent object's rotation")]
        private float rotationFixSpeed = 10f;
        // The last known world space rotation of the character model
        private Quaternion lastRotation = Quaternion.identity;

        /// <summary>
        /// Will interface with an animator and apply movement related data by setting animator triggers and other variables
        /// </summary>
        public void UpdateAnimator()
        {
            //animator.SetVector("movement", characterController.movementController.velocity);
        }

        /// <summary>
        /// Rotates the visual representation of the character smoothly to match the current facing direction
        /// this allows the character's movement to feel snappy without the character's visuals looking unnatural when rapidly changing direction
        /// </summary>
        /// <param name="deltaTime"></param>
        public void SmoothRotateVisuals(float deltaTime)
        {
            characterModelRoot.transform.rotation = Quaternion.Slerp(lastRotation, transform.rotation, deltaTime * rotationFixSpeed);
            lastRotation = characterModelRoot.transform.rotation;
        }
    }
}
