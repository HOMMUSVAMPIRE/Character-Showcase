using System;
using UnityEngine;
using UnityEngine.Serialization;
using static MeridianMotion.Characters.MovementRules;

namespace MeridianMotion.Characters
{
    /// <summary>
    /// Crouching and sliding: Crouch lowers the character into a slower stance with a shorter collider, which stands back
    /// up only where there's room; pressed while sprinting, it slides along the ground on the sprint's momentum instead,
    /// low, slowing down until the slide ends in a crouch (unless sliding is turned off, when it just crouches). The
    /// crouch height is also what rolls and other low moves use (see the mode's posture), and the crouch input buffer
    /// how long any Crouch press counts (see AbilityMovementMode.ConsumeCrouchPress).
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
        [SerializeField] [FormerlySerializedAs("slidingEnabled")] private bool canSlide = true;
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
        [Tooltip("The longest a slide lasts, in seconds, before it ends in a crouch. 0 turns sliding off, as Can Slide does")]
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
        public bool SlidingEnabled { get => canSlide; set => canSlide = value; }
        /// <summary>How fast (m/s) a slide starts, at least, at the character's authored size.</summary>
        public float SlideStartSpeed { get => slideStartSpeed; set => slideStartSpeed = value; }
        /// <summary>The longest a slide lasts, in seconds. 0 turns sliding off.</summary>
        public float SlideMaxDuration { get => slideMaxDuration; set => slideMaxDuration = value; }

        /// <summary>A slide has the character while it lasts (crouching is a stance, not an ability: others start from it, or refuse to).</summary>
        public override bool IsActive(MMotionMotor motor) => motor.slide.IsSliding;
        /// <summary>A slide is on the ground: the character is neither held up nor holding on.</summary>
        public override AbilityTraits Traits => AbilityTraits.None;
        /// <summary>It slides only with Can Slide on (and a Slide Max Duration).</summary>
        public override bool AllowsState(CharacterState state) => state != CharacterState.Sliding || (canSlide && slideMaxDuration > 0f);
        /// <summary>The events of Crouching and Sliding starting and ending (see MovementComponent.StateEvents).</summary>
        public override StateEventPair[] StateEvents => stateEvents;
        private static readonly StateEventPair[] stateEvents =
        {
            new(CharacterState.Crouching, MovementEvents.CrouchStarted, MovementEvents.CrouchEnded),
            new(CharacterState.Sliding, MovementEvents.SlideStarted, MovementEvents.SlideEnded),
        };

        /// <summary>Crouched or sliding, low at the crouch height.</summary>
        public override BodyPosture? Posture(MMotionMotor motor) =>
            motor.crouch.IsCrouched || motor.slide.IsSliding ? BodyPosture.Low(crouchHeight) : null;

        public override void Interrupt(MovementStep step, MMotionMotor motor)
        {
            if (motor.slide.IsSliding) { EndSlide(motor, crouched: motor.crouch.IsCrouched); }
        }

        /// <summary>
        /// Crouches or stands up as the Crouch button asks: in hold mode, crouched on the ground while it's held; otherwise
        /// each press on the ground crouches, and the next stands. Standing waits until there's room above the
        /// character, then happens by itself. A jump stands the character up first, where there's room (the jump is
        /// skipped where there isn't).
        /// </summary>
        public override void BeforeLocomotion(MovementStep step, StepInputs inputs, MMotionMotor motor)
        {
            CrouchState crouch = motor.crouch;
            if (holdToCrouch)
            {
                bool held = inputs.crouch != null && inputs.crouch.held;
                if (held && step.initialGrounded)
                {
                    crouch.IsCrouched = true;
                    crouch.wantsToStand = false;
                }
                else if (!held && crouch.IsCrouched) { crouch.wantsToStand = true; }
            }
            // Crouching only starts on the ground (a press in the air counts on landing, within the buffer), but standing
            // can be asked for anywhere.
            else if ((crouch.IsCrouched || step.initialGrounded) && crouch.press.Consume(inputs.crouch, crouchInputBuffer))
            {
                if (crouch.IsCrouched) { crouch.wantsToStand = !crouch.wantsToStand; } // A second press while waiting for room cancels standing.
                else
                {
                    crouch.IsCrouched = true;
                    crouch.wantsToStand = false;
                }
            }

            bool standForJump = Mode.HasFreshJumpPress(inputs, motor);
            if (crouch.IsCrouched && (crouch.wantsToStand || standForJump) && motor.CanFitAt(step.initialPosition))
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
        public override bool TakeOver(MovementStep step, StepInputs inputs, MMotionMotor motor)
        {
            SlideState slide = motor.slide;
            if (!slide.IsSliding && !TryStartSlide(step, inputs, motor)) { return false; }
            if (!canSlide)
            {
                EndSlide(motor, crouched: true); // Sliding turned off part way through a slide.
                return false;
            }

            // Off the ground, the slide's momentum carries on as the character's own (it's already its inertia).
            if (!step.initialGrounded)
            {
                EndSlide(motor, crouched: false);
                return false;
            }

            bool standUp = holdToCrouch
                ? inputs.crouch == null || !inputs.crouch.held
                : Mode.ConsumeCrouchPress(inputs, motor);
            if (standUp)
            {
                EndSlide(motor, crouched: false);
                return false;
            }

            // A jump stands up (where there's room) and keeps the slide's speed: its momentum is already the
            // character's inertia, which the jump carries into the air.
            if (Mode.HasFreshJumpPress(inputs, motor) && motor.CanFitAt(step.initialPosition))
            {
                EndSlide(motor, crouched: false);
                step.directMovementVector = Vector3.zero;
                Mode.Get<Jumping>()?.Jump(step, inputs, motor);
                return true;
            }

            // The slide's speed is its momentum as the last step left it, so hitting a wall slows or stops it.
            Vector3 inertia = step.initialInertia;
            Vector3 velocity = new Vector3(inertia.x, 0f, inertia.z);
            float speed = velocity.magnitude;
            if (speed > 1e-3f) { slide.direction = velocity / speed; }
            Vector3 steer = InputDirection(step, inputs);
            if (steer.sqrMagnitude > 0.01f)
            {
                slide.direction = Vector3.RotateTowards(slide.direction, steer.normalized, slideSteering * Mathf.Deg2Rad * step.deltaTime, 0f);
            }
            speed = Mathf.Max(0f, speed - slideDeceleration * motor.AccelerationScale * step.deltaTime);
            slide.elapsed += step.deltaTime;

            step.initialInertia = new Vector3(slide.direction.x * speed, inertia.y, slide.direction.z * speed);
            if (speed <= slideEndSpeed * motor.SpeedScale || MovementRules.HasElapsed(slide.elapsed, slideMaxDuration * motor.DurationScale, step.deltaTime))
            {
                // Over: crouched, with what's left of the slide's speed, which grounded friction then eases out.
                EndSlide(motor, crouched: true);
                return false;
            }

            step.directMovementVector = Vector3.zero;
            step.targetRotation = Quaternion.LookRotation(slide.direction, Vector3.up);
            step.state |= CharacterState.Sliding | CharacterState.Moving;
            return true;
        }

        /// <summary>Starts a slide if Crouch was just pressed while sprinting fast enough on the ground.</summary>
        private bool TryStartSlide(MovementStep step, StepInputs inputs, MMotionMotor motor)
        {
            if (!canSlide || slideMaxDuration <= 0f || !step.initialGrounded || motor.crouch.IsCrouched || Mode.AbilityInProgress(motor)) { return false; }
            if (inputs.sprint == null || !inputs.sprint.held) { return false; }
            Vector3 velocity = step.initialVelocity;
            Vector3 horizontal = new Vector3(velocity.x, 0f, velocity.z);
            float speed = horizontal.magnitude;
            if (speed < slideMinStartSpeed * motor.SpeedScale || speed < 1e-3f) { return false; }
            if (!Mode.ConsumeCrouchPress(inputs, motor)) { return false; }

            SlideState slide = motor.slide;
            slide.IsSliding = true;
            slide.elapsed = 0f;
            slide.direction = horizontal / speed;
            // Walking moves the character directly rather than through its momentum (see the mode's walking), so
            // the sprint's speed becomes the slide's momentum here.
            speed = Mathf.Max(speed, slideStartSpeed * motor.SpeedScale);
            step.initialInertia = new Vector3(slide.direction.x * speed, step.initialInertia.y, slide.direction.z * speed);
            return true;
        }

        private static void EndSlide(MMotionMotor motor, bool crouched)
        {
            motor.slide.IsSliding = false;
            motor.crouch.IsCrouched = crouched;
            motor.crouch.wantsToStand = false;
        }

        /// <summary>
        /// How low the character gets crouched (the crouch height's share of its standing height), and how far a slide
        /// from a sprint carries it, if it slides.
        /// </summary>
        public override void MeasureCapabilities(MovementSimulation simulation, MovementCapabilities capabilities)
        {
            capabilities.CrouchHeight = crouchHeight * capabilities.StandHeight;
            if (canSlide && slideMaxDuration > 0f) { capabilities.SlideLength = SlideLength(simulation); }
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
                StepInputs inputs = MovementSimulation.Move(Vector3.forward, true);
                inputs.crouch = MovementSimulation.Held(i);
                simulation.Step(inputs);
                bool sliding = simulation.State.HasAny(CharacterState.Sliding);
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
        public readonly PressBuffer press = new();
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

    // This state's place on the character (see MMotionMotor): declared here, beside its ability, so a new
    // ability adds its state in its own file rather than in MMotionMotor.cs.
    public partial class MMotionMotor
    {
        /// <summary>The character's crouching state (Crouching).</summary>
        public CrouchState crouch { get; } = new CrouchState();

        /// <summary>The character's sliding state (Crouching, which slides when Crouch is pressed while sprinting).</summary>
        public SlideState slide { get; } = new SlideState();
    }
}
