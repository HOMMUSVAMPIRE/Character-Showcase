using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.InputSystem;

namespace MeridianMotion.Characters
{
    /// <summary>
    /// Reads the player's device (through a PlayerInput) each render frame, and gives the character one input frame per
    /// physics step (FrameFor: see InputLatch), so a tap between two physics steps still counts and a frame that runs two
    /// steps doesn't press a button twice. The look in each frame is the main camera's (the player's view), so the move
    /// input is relative to it; the movement itself never reads a camera. One per scene (a second destroys
    /// itself): instance.
    /// </summary>
    [AddComponentMenu("Meridian Motion/Characters/Player Input Reader")]
    public class PlayerInputReader : MonoBehaviour
    {
        /// <summary>The singleton instance of the input manager (set in Awake; a second one destroys itself).</summary>
        public static PlayerInputReader instance;

        /// <summary>The PlayerInput component that allows access to the unity input system.</summary>
        [Tooltip("The PlayerInput the character's actions are read from.")]
        [FormerlySerializedAs("inputSource")] public PlayerInput playerInput = null;

        private readonly InputLatch latch = new();
        private readonly InputSuppression suppression = new();

        /// <summary>
        /// Whether the player's input stops reaching the character (a window open over the game: the UI's On Game Input
        /// Blocked Changed sets it): nothing held, nothing pressed. A button held through it counts again only once it's let
        /// go and pressed again (see InputSuppression). The look still follows the camera.
        /// </summary>
        public bool Suppressed { get => suppression.Suppressed; set => suppression.Suppressed = value; }

        /// <summary>Sets Suppressed (for a UnityEvent&lt;bool&gt; to call).</summary>
        public void SetSuppressed(bool suppressed) => Suppressed = suppressed;

        // The actions read, looked up by name once (FindAction searches by string) and again only if the action map
        // changes; null for one the map doesn't have, which reads as never pressed.
        private InputActionMap actionMap;
        private InputAction move, jump, dash, sprint, crouch, roll, interact, attack;

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
        }

        /// <summary>
        /// The input frame for the character's physics step tick (its MMotionMotor.Tick): see InputLatch. The same
        /// frame for every character asking within one physics step.
        /// </summary>
        public CharacterInputFrame FrameFor(uint tick) => latch.Take(tick, Time.fixedTime);

        /// <summary>Reads the device for this render frame into the latch.</summary>
        private void Update()
        {
            if (playerInput.currentActionMap != actionMap) { FindActions(playerInput.currentActionMap); }

            Vector2 moveInput = move != null ? move.ReadValue<Vector2>() : Vector2.zero;
            InputButtons down = InputButtons.None, pressed = InputButtons.None;
            ReadButton(jump, InputButtons.Jump, ref down, ref pressed);
            ReadButton(dash, InputButtons.Dash, ref down, ref pressed);
            ReadButton(sprint, InputButtons.Sprint, ref down, ref pressed);
            ReadButton(crouch, InputButtons.Crouch, ref down, ref pressed);
            ReadButton(roll, InputButtons.Roll, ref down, ref pressed);
            // Grabbing objects to push or pull is the Interact button, held.
            ReadButton(interact, InputButtons.Interact, ref down, ref pressed);
            ReadButton(attack, InputButtons.Attack, ref down, ref pressed);
            suppression.Apply(ref moveInput, ref down, ref pressed);

            // The player looks the way their camera does: the move input is relative to it. Without one, along +Z.
            Camera view = Camera.main;
            CharacterInputFrame look = view != null ? default(CharacterInputFrame).LookingAlong(view.transform.rotation) : default;
            latch.Sample(moveInput, look.lookYaw, look.lookPitch, down, pressed);
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
            attack = map?.FindAction("Attack");
        }

        /// <summary>
        /// Reads a button action: into down if it's down now, into pressed if it went down this frame (a tap that was
        /// pressed and let go within one frame included). A missing action reads as never pressed.
        /// </summary>
        private static void ReadButton(InputAction action, InputButtons button, ref InputButtons down, ref InputButtons pressed)
        {
            if (action == null) { return; }
            if (action.ReadValue<float>() > 0.1f) { down |= button; }
            if (action.WasPressedThisFrame()) { pressed |= button; }
        }
    }
}
