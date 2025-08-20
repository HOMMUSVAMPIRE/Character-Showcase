using UnityEngine;

namespace Character
{
    public class AnimationController : MonoBehaviour
    {
        [SerializeField]
        private CharacterController characterController;

        [SerializeField]
        private GameObject characterModelRoot;
        [SerializeField]
        private Animator animator;

        [SerializeField]
        private float rotationFixSpeed = 10f;

        public void UpdateAnimator()
        {
            //animator.SetVector("movement", characterController.movementController.velocity);
        }

        public void SmoothRotateVisuals(float deltaTime)
        {
            characterModelRoot.transform.localRotation = Quaternion.Slerp(characterModelRoot.transform.localRotation, Quaternion.identity, deltaTime * rotationFixSpeed);
        }
    }
}
