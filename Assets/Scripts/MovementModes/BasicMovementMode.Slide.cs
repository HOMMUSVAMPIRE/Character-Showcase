using UnityEngine;

namespace Character
{
    // Sliding: pressing Crouch while sprinting slides along the ground on the sprint's momentum, low (at the crouch
    // height), slowing down until the slide ends in a crouch.
    public partial class BasicMovementMode
    {
        [Header("Sliding")]
        [Tooltip("Pressing Crouch while sprinting slides, if the character is moving at least this fast (m/s, at its " +
                 "authored size); slower, it just crouches")]
        [SerializeField] private float slideMinStartSpeed = 4.5f;
        [Tooltip("How fast (m/s) a slide starts, at least, at the character's authored size. A slide from a faster sprint " +
                 "keeps the sprint's speed.")]
        [SerializeField] private float slideStartSpeed = 10f;
        [Tooltip("How quickly (m/s²) a slide slows down")]
        [SerializeField] private float slideDeceleration = 8f;
        [Tooltip("A slide ends, in a crouch, once it slows to this speed (m/s)")]
        [SerializeField] private float slideEndSpeed = 3f;
        [Tooltip("The longest a slide lasts, in seconds, before it ends in a crouch. 0 turns sliding off")]
        [SerializeField] private float slideMaxDuration = 1.2f;
        [Tooltip("How quickly (degrees per second) movement input turns a slide")]
        [SerializeField] private float slideSteering = 90f;

        float SlideMode.slideStartSpeed { get => slideStartSpeed; set => slideStartSpeed = value; }
        float SlideMode.slideMaxDuration { get => slideMaxDuration; set => slideMaxDuration = value; }

        /// <summary>
        /// Starts a slide when Crouch is pressed while sprinting fast enough on the ground, and carries it on: the
        /// character keeps its momentum, slowing down and steered a little by movement input, until it's slow, has slid
        /// for the slide's longest time, or hits something, and then it's crouched. Pressing Crouch again (or, in hold
        /// mode, letting go of it) stands up instead. Jumping cancels the slide into a jump that keeps its speed, and
        /// sliding off an edge carries its momentum into the air.
        /// </summary>
        public bool ProcessMovementDataForSlide(ref MovementData movementData, Inputs inputs, MovementController controller)
        {
            SlideState slide = controller.slide;
            if (!slide.IsSliding && !TryStartSlide(ref movementData, inputs, controller)) { return false; }

            // Off the ground, the slide's momentum carries on as the character's own (it's already its inertia).
            if (!movementData.initialGrounded)
            {
                EndSlide(controller, crouched: false);
                return false;
            }

            bool standUp = holdToCrouch
                ? inputs.crouch == null || !inputs.crouch.value
                : controller.crouch.press.Consume(inputs.crouch, crouchInputBuffer);
            if (standUp)
            {
                EndSlide(controller, crouched: false);
                return false;
            }

            // A jump stands up (where there's room) and keeps the slide's speed: its momentum is already the
            // character's inertia, which the jump carries into the air.
            if (IsNewJumpPress(inputs, controller) && controller.CanFitAt(movementData.initialPosition, 1f))
            {
                EndSlide(controller, crouched: false);
                movementData.directMovementVector = Vector3.zero;
                (this as JumpMode).ProcessMovementDataForJump(ref movementData, inputs, controller);
                return true;
            }

            // The slide's speed is its momentum as the last step left it, so hitting a wall slows or stops it.
            Vector3 inertia = movementData.initialInertia;
            Vector3 velocity = new Vector3(inertia.x, 0f, inertia.z);
            float speed = velocity.magnitude;
            if (speed > 1e-3f) { slide.direction = velocity / speed; }
            Vector3 steer = InputDirection(movementData, inputs);
            if (steer.sqrMagnitude > 0.01f)
            {
                slide.direction = Vector3.RotateTowards(slide.direction, steer.normalized, slideSteering * Mathf.Deg2Rad * movementData.deltaTime, 0f);
            }
            speed = Mathf.Max(0f, speed - slideDeceleration * controller.AccelerationScale * movementData.deltaTime);
            slide.elapsed += movementData.deltaTime;

            movementData.initialInertia = new Vector3(slide.direction.x * speed, inertia.y, slide.direction.z * speed);
            if (speed <= slideEndSpeed * controller.SpeedScale || slide.elapsed >= slideMaxDuration * controller.DurationScale)
            {
                // Over: crouched, with what's left of the slide's speed, which grounded friction then eases out.
                EndSlide(controller, crouched: true);
                return false;
            }

            movementData.directMovementVector = Vector3.zero;
            movementData.targetRotation = Quaternion.LookRotation(slide.direction, Vector3.up);
            movementData.state |= CharacterState.Sliding | CharacterState.Moving;
            return true;
        }

        /// <summary>Starts a slide if Crouch was just pressed while sprinting fast enough on the ground.</summary>
        private bool TryStartSlide(ref MovementData movementData, Inputs inputs, MovementController controller)
        {
            if (slideMaxDuration <= 0f || !movementData.initialGrounded || controller.crouch.IsCrouched || AbilityInProgress(controller)) { return false; }
            if (inputs.sprint == null || !inputs.sprint.value) { return false; }
            Vector3 velocity = movementData.initialVelocity;
            Vector3 horizontal = new Vector3(velocity.x, 0f, velocity.z);
            float speed = horizontal.magnitude;
            if (speed < slideMinStartSpeed * controller.SpeedScale || speed < 1e-3f) { return false; }
            if (!controller.crouch.press.Consume(inputs.crouch, crouchInputBuffer)) { return false; }

            SlideState slide = controller.slide;
            slide.IsSliding = true;
            slide.elapsed = 0f;
            slide.direction = horizontal / speed;
            // Walking moves the character directly rather than through its momentum (see ProcessMovementDataForWalk), so
            // the sprint's speed becomes the slide's momentum here.
            speed = Mathf.Max(speed, slideStartSpeed * controller.SpeedScale);
            movementData.initialInertia = new Vector3(slide.direction.x * speed, movementData.initialInertia.y, slide.direction.z * speed);
            return true;
        }

        private static void EndSlide(MovementController controller, bool crouched)
        {
            controller.slide.IsSliding = false;
            controller.crouch.IsCrouched = crouched;
            controller.crouch.wantsToStand = false;
        }
    }
}
