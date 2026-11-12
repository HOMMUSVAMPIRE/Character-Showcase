using UnityEngine;

namespace Character
{
    // Crouching: a lower, slower stance with a shorter collider. Its collider height is also what sliding and rolling
    // use (see UpdatePosture).
    public partial class BasicMovementMode
    {
        [Header("Crouching")]
        [Tooltip("How fast (m/s) the character moves while crouched, at its authored size (it scales with size, as the walk speed does)")]
        [SerializeField] private float crouchSpeed = 2f;
        [Tooltip("The character's collider height while crouched, sliding or rolling, as a share of its standing height. " +
                 "It stands back up only where there's room.")]
        [Range(0.2f, 1f)] [SerializeField] private float crouchHeight = 0.55f;
        [Tooltip("Hold Crouch to stay crouched and let go to stand, rather than pressing it once to crouch and again to stand")]
        [SerializeField] private bool holdToCrouch;
        [Tooltip("A press of Crouch counts if it was made at most this many seconds ago, so a press just before landing (or " +
                 "just before a slide is possible) still counts. Holding the button doesn't crouch again.")]
        [SerializeField] private float crouchInputBuffer = 0.1f;

        float CrouchMode.crouchSpeed { get => crouchSpeed; set => crouchSpeed = value; }
        float CrouchMode.crouchHeight { get => crouchHeight; set => crouchHeight = value; }

        /// <summary>
        /// Crouches or stands up as the Crouch button asks: in hold mode, crouched on the ground while it's held; otherwise
        /// each press on the ground crouches, and the next stands. Standing waits until there's room above the
        /// character, then happens by itself. A jump stands the character up first, where there's room (the jump is
        /// skipped where there isn't).
        /// </summary>
        public void ProcessMovementDataForCrouch(ref MovementData movementData, Inputs inputs, MovementController controller)
        {
            CrouchState crouch = controller.crouch;
            if (holdToCrouch)
            {
                bool held = inputs.crouch != null && inputs.crouch.value;
                if (held && movementData.initialGrounded)
                {
                    crouch.IsCrouched = true;
                    crouch.wantsToStand = false;
                }
                else if (!held && crouch.IsCrouched) { crouch.wantsToStand = true; }
            }
            // Crouching only starts on the ground (a press in the air counts on landing, within the buffer), but standing
            // can be asked for anywhere.
            else if ((crouch.IsCrouched || movementData.initialGrounded) && crouch.press.Consume(inputs.crouch, crouchInputBuffer))
            {
                if (crouch.IsCrouched) { crouch.wantsToStand = !crouch.wantsToStand; } // A second press while waiting for room cancels standing.
                else
                {
                    crouch.IsCrouched = true;
                    crouch.wantsToStand = false;
                }
            }

            bool standForJump = IsNewJumpPress(inputs, controller);
            if (crouch.IsCrouched && (crouch.wantsToStand || standForJump) && controller.CanFitAt(movementData.initialPosition, 1f))
            {
                crouch.IsCrouched = false;
                crouch.wantsToStand = false;
            }
        }

        /// <summary>
        /// Lowers the character's collider to the crouch height while it's crouched, sliding or rolling, and raises it
        /// again otherwise. Where there isn't room to stand (a slide or roll ending under something low, say), the
        /// character stays crouched, and stands up by itself once there is. Also marks the step as crouched.
        /// </summary>
        private void UpdatePosture(ref MovementData movementData, MovementController controller)
        {
            CrouchState crouch = controller.crouch;
            bool low = crouch.IsCrouched || controller.slide.IsSliding || controller.roll.IsRolling;
            if (low) { controller.SetHeightFraction(crouchHeight, movementData.initialPosition); }
            else if (!controller.SetHeightFraction(1f, movementData.initialPosition))
            {
                crouch.IsCrouched = true;
                crouch.wantsToStand = true;
            }

            if (crouch.IsCrouched && !controller.slide.IsSliding && !controller.roll.IsRolling)
            {
                movementData.state |= CharacterState.Crouching;
            }
        }
    }
}
