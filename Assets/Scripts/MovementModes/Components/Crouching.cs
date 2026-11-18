using System;
using UnityEngine;
using static Character.MovementHelpers;

namespace Character
{
    /// <summary>
    /// Crouching and sliding: Crouch lowers the character into a slower stance with a shorter collider, which stands back
    /// up only where there's room; pressed while sprinting, it slides along the ground on the sprint's momentum instead,
    /// low, slowing down until the slide ends in a crouch (unless sliding is turned off, when it just crouches). The
    /// crouch height is also what rolls and other low moves use (see the mode's posture), and the crouch input buffer
    /// how long any Crouch press counts (see ComposedMovementMode.ConsumeCrouchPress).
    /// </summary>
    [Serializable]
    public class Crouching : MovementComponent
    {
        [Tooltip("How fast (m/s) the character moves while crouched, at its authored size (it scales with size, as the walk speed does)")]
        [SerializeField] private float crouchSpeed = 2f;
        [Tooltip("The character's body height while crouched, sliding or rolling (and climbing onto a ledge with only room " +
                 "to crouch), as a share of its standing height. It stands back up only where there's room. Used for rolls " +
                 "even with Crouching turned off")]
        [Range(0.2f, 1f)] [SerializeField] private float crouchHeight = 0.55f;
        [Tooltip("Hold Crouch to stay crouched and let go to stand, rather than pressing it once to crouch and again to stand")]
        [SerializeField] private bool holdToCrouch;
        [Tooltip("A press of Crouch counts if it was made at most this many seconds ago, so a press just before landing (or " +
                 "just before a slide is possible) still counts. Holding the button doesn't crouch again.")]
        [SerializeField] private float crouchInputBuffer = 0.1f;

        [Header("Sliding")]
        [Tooltip("Whether pressing Crouch while sprinting slides. Turned off, it crouches, as at any other speed")]
        [SerializeField] private bool slidingEnabled = true;
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
        [Tooltip("The longest a slide lasts, in seconds, before it ends in a crouch. 0 turns sliding off, as Sliding Enabled does")]
        [SerializeField] private float slideMaxDuration = 1.2f;
        [Tooltip("How quickly (degrees per second) movement input turns a slide")]
        [SerializeField] private float slideSteering = 90f;

        /// <summary>How fast (m/s) the character moves while crouched, at its authored size.</summary>
        public float CrouchSpeed { get => crouchSpeed; set => crouchSpeed = value; }
        /// <summary>The character's collider height while crouched, sliding or rolling, as a share of its standing height.</summary>
        public float CrouchHeight { get => crouchHeight; set => crouchHeight = value; }
        /// <summary>How long (s) a press of Crouch counts for, after it's made.</summary>
        public float CrouchInputBuffer => crouchInputBuffer;
        /// <summary>Whether pressing Crouch while sprinting slides.</summary>
        public bool SlidingEnabled { get => slidingEnabled; set => slidingEnabled = value; }
        /// <summary>How fast (m/s) a slide starts, at least, at the character's authored size.</summary>
        public float SlideStartSpeed { get => slideStartSpeed; set => slideStartSpeed = value; }
        /// <summary>The longest a slide lasts, in seconds. 0 turns sliding off.</summary>
        public float SlideMaxDuration { get => slideMaxDuration; set => slideMaxDuration = value; }

        /// <summary>A slide has the character while it lasts (crouching is a stance, not an ability: others start from it, or refuse to).</summary>
        public override bool IsActive(MovementController controller) => controller.slide.IsSliding;
        /// <summary>The events of Crouching and Sliding starting and ending (see MovementComponent.StateEvents).</summary>
        public override StateEvent[] StateEvents => stateEvents;
        private static readonly StateEvent[] stateEvents =
        {
            new(CharacterState.Crouching, MovementEvents.CrouchStarted, MovementEvents.CrouchEnded),
            new(CharacterState.Sliding, MovementEvents.SlideStarted, MovementEvents.SlideEnded),
        };

        /// <summary>Crouched or sliding, low at the crouch height.</summary>
        public override BodyPosture? Posture(MovementController controller) =>
            controller.crouch.IsCrouched || controller.slide.IsSliding ? BodyPosture.Low(crouchHeight) : null;

        public override void Interrupt(MovementData movementData, MovementController controller)
        {
            if (controller.slide.IsSliding) { EndSlide(controller, crouched: controller.crouch.IsCrouched); }
        }

        /// <summary>
        /// Crouches or stands up as the Crouch button asks: in hold mode, crouched on the ground while it's held; otherwise
        /// each press on the ground crouches, and the next stands. Standing waits until there's room above the
        /// character, then happens by itself. A jump stands the character up first, where there's room (the jump is
        /// skipped where there isn't).
        /// </summary>
        public override void BeforeLocomotion(MovementData movementData, Inputs inputs, MovementController controller)
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

            bool standForJump = Mode.IsNewJumpPress(inputs, controller);
            if (crouch.IsCrouched && (crouch.wantsToStand || standForJump) && controller.CanFitAt(movementData.initialPosition))
            {
                crouch.IsCrouched = false;
                crouch.wantsToStand = false;
            }
        }

        /// <summary>
        /// Starts a slide when Crouch is pressed while sprinting fast enough on the ground, and carries it on: the
        /// character keeps its momentum, slowing down and steered a little by movement input, until it's slow, has slid
        /// for the slide's longest time, or hits something, and then it's crouched. Pressing Crouch again (or, in hold
        /// mode, letting go of it) stands up instead. Jumping cancels the slide into a jump that keeps its speed, and
        /// sliding off an edge carries its momentum into the air.
        /// </summary>
        public override bool TakeOver(MovementData movementData, Inputs inputs, MovementController controller)
        {
            SlideState slide = controller.slide;
            if (!slide.IsSliding && !TryStartSlide(movementData, inputs, controller)) { return false; }
            if (!slidingEnabled)
            {
                EndSlide(controller, crouched: true); // Sliding turned off part way through a slide.
                return false;
            }

            // Off the ground, the slide's momentum carries on as the character's own (it's already its inertia).
            if (!movementData.initialGrounded)
            {
                EndSlide(controller, crouched: false);
                return false;
            }

            bool standUp = holdToCrouch
                ? inputs.crouch == null || !inputs.crouch.value
                : Mode.ConsumeCrouchPress(inputs, controller);
            if (standUp)
            {
                EndSlide(controller, crouched: false);
                return false;
            }

            // A jump stands up (where there's room) and keeps the slide's speed: its momentum is already the
            // character's inertia, which the jump carries into the air.
            if (Mode.IsNewJumpPress(inputs, controller) && controller.CanFitAt(movementData.initialPosition))
            {
                EndSlide(controller, crouched: false);
                movementData.directMovementVector = Vector3.zero;
                Mode.Get<Jumping>()?.Jump(movementData, inputs, controller);
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
            if (speed <= slideEndSpeed * controller.SpeedScale || MovementHelpers.HasElapsed(slide.elapsed, slideMaxDuration * controller.DurationScale, movementData.deltaTime))
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
        private bool TryStartSlide(MovementData movementData, Inputs inputs, MovementController controller)
        {
            if (!slidingEnabled || slideMaxDuration <= 0f || !movementData.initialGrounded || controller.crouch.IsCrouched || Mode.AbilityInProgress(controller)) { return false; }
            if (inputs.sprint == null || !inputs.sprint.value) { return false; }
            Vector3 velocity = movementData.initialVelocity;
            Vector3 horizontal = new Vector3(velocity.x, 0f, velocity.z);
            float speed = horizontal.magnitude;
            if (speed < slideMinStartSpeed * controller.SpeedScale || speed < 1e-3f) { return false; }
            if (!Mode.ConsumeCrouchPress(inputs, controller)) { return false; }

            SlideState slide = controller.slide;
            slide.IsSliding = true;
            slide.elapsed = 0f;
            slide.direction = horizontal / speed;
            // Walking moves the character directly rather than through its momentum (see the mode's walking), so
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

        /// <summary>
        /// How low the character gets crouched (the crouch height's share of its standing height), and how far a slide
        /// from a sprint carries it, if it slides.
        /// </summary>
        public override void MeasureCapabilities(MovementSimulation simulation, MovementCapabilities capabilities)
        {
            capabilities.CrouchHeight = crouchHeight * capabilities.StandHeight;
            if (slidingEnabled && slideMaxDuration > 0f) { capabilities.SlideLength = SlideLength(simulation); }
        }

        /// <summary>How far (m) a slide from a sprint carries the character, from where it starts to where it ends.</summary>
        private static float SlideLength(MovementSimulation simulation)
        {
            simulation.Reset(Vector3.zero);
            simulation.Run(MovementSimulation.StepsFor(MovementSimulation.MeasureRunUp), _ => MovementSimulation.Move(Vector3.forward, true));
            float start = float.NaN;
            for (int i = 0; i < MovementSimulation.StepsFor(MovementSimulation.MeasureTimeout); i++)
            {
                float before = simulation.Position.z;
                Inputs inputs = MovementSimulation.Move(Vector3.forward, true);
                inputs.crouch = MovementSimulation.Held(i);
                simulation.Step(inputs);
                bool sliding = (simulation.State & CharacterState.Sliding) != 0;
                if (sliding && float.IsNaN(start)) { start = before; }
                if (!sliding && !float.IsNaN(start)) { return simulation.Position.z - start; }
            }
            return float.NaN;
        }
    }

    /// <summary>Whether the character is crouched, and whether it's waiting for room to stand up.</summary>
    public class CrouchState
    {
        /// <summary>Whether the character is crouched (its collider lowered to the crouch height).</summary>
        public bool IsCrouched;
        /// <summary>Asked to stand, but there wasn't room yet: stands as soon as there is.</summary>
        public bool wantsToStand;
        /// <summary>The Crouch button's presses (when crouching toggles), so each press counts once.</summary>
        public readonly ButtonPress press = new();
    }

    /// <summary>A slide in progress.</summary>
    public class SlideState
    {
        /// <summary>Whether the character is sliding.</summary>
        public bool IsSliding;
        /// <summary>Seconds since the slide started.</summary>
        public float elapsed;
        /// <summary>The direction the slide is heading (horizontal, unit length).</summary>
        public Vector3 direction;
    }

    // This state's place on the character (see MovementController): declared here, beside its ability, so a new
    // ability adds its state in its own file rather than in MovementController.cs.
    public partial class MovementController
    {
        /// <summary>The character's crouching state (Crouching).</summary>
        public CrouchState crouch { get; } = new CrouchState();

        /// <summary>The character's sliding state (Crouching, which slides when Crouch is pressed while sprinting).</summary>
        public SlideState slide { get; } = new SlideState();
    }
}
