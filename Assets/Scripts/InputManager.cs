using UnityEngine;

namespace Character
{
    public class InputManager : MonoBehaviour
    {
        public static InputManager instance;

        public Inputs inputs { get; private set; }

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
        }

        private void Update()
        {
            /*            var moveVector =
                            (Input.GetButton(KeyCode.W) ? Vector3.forward : Vector3.zero) +
                            (Input.GetButton(KeyCode.A) ? Vector3.left : Vector3.zero) +
                            (Input.GetButton(KeyCode.S) ? Vector3.back : Vector3.zero) +
                            (Input.GetButton(KeyCode.D) ? Vector3.right : Vector3.zero) +*/

            inputs = new Inputs()
            {
                //movementVector = moveVector;
            };
        }
    }
}
