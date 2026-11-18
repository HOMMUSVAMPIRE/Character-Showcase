using System;
using System.Collections.Generic;
using Level;
using UnityEngine;

namespace Character
{
    /// <summary>
    /// Rope swinging (see Level.Rope): catching hold of a rope in mid-air, and swinging on it as a pendulum.
    /// </summary>
    [Serializable]
    public class RopeSwinging : MovementComponent
    {
        [Tooltip("How far (m) from the character's hands a rope can pass, at its authored size, for the character to catch " +
                 "hold of it in mid-air. 0 turns rope swinging off")]
        [Min(0f)] [SerializeField] private float ropeGrabReach = 0.5f;
        [Tooltip("Where the character's hands hold the rope, above its feet, as a share of its standing height (about as high as they reach)")]
        [Min(0.1f)] [SerializeField] private float ropeGripHeight = 1.05f;
        [Tooltip("The shortest (m) the rope above the hands can be: catching hold of a rope nearer its anchor holds it this far down")]
        [Min(0.1f)] [SerializeField] private float ropeMinGripDistance = 1f;
        [Tooltip("How quickly (m/s²) movement input pumps the swing, pushing it the way the input points, at the character's authored size")]
        [Min(0f)] [SerializeField] private float ropeSwingAcceleration = 6f;
        [Tooltip("The share of its speed the swing loses each second (with no input, a swing slowly dies away)")]
        [Range(0f, 2f)] [SerializeField] private float ropeSwingDamping = 0.15f;
        [Tooltip("How far (degrees) from hanging straight down the swing can go: it stops there and swings back")]
        [Range(10f, 89f)] [SerializeField] private float ropeMaxSwingAngle = 70f;
        [Tooltip("The fastest (m/s) the character swings, at its authored size")]
        [Min(0.1f)] [SerializeField] private float ropeMaxSwingSpeed = 14f;
        [Tooltip("How fast (m/s) jumping off a rope sends the character up, at its authored size, on top of the swing's own speed")]
        [Min(0f)] [SerializeField] private float ropeJumpUpSpeed = 8f;
        [Tooltip("Seconds after letting go of a rope before the same one can be caught again (another can be caught at once, " +
                 "so the character can swing from rope to rope)")]
        [Min(0f)] [SerializeField] private float ropeRegrabDelay = 0.6f;

        /// <summary>How far (m) from the character's hands a rope can be to catch hold of it, at its authored size. 0 turns rope swinging off.</summary>
        public float RopeGrabReach { get => ropeGrabReach; set => ropeGrabReach = value; }

        public override bool IsActive(MovementController controller) => controller.ropeSwing.IsSwinging;
        /// <summary>Swinging, the character holds on to the rope, which holds it up.</summary>
        public override AbilityTraits Traits => AbilityTraits.HoldsUp | AbilityTraits.HoldsOn;
        /// <summary>The events of RopeSwinging starting and ending (see MovementComponent.StateEvents).</summary>
        public override StateEvent[] StateEvents => stateEvents;
        private static readonly StateEvent[] stateEvents =
        {
            new(CharacterState.RopeSwinging, MovementEvents.RopeGrabbed, MovementEvents.RopeReleased),
        };

        public override void AdvanceTimers(MovementData movementData, Inputs inputs, MovementController controller)
        {
            controller.ropeSwing.regrabCooldown = MovementHelpers.CountDown(controller.ropeSwing.regrabCooldown, movementData.deltaTime);
        }

        public override void Interrupt(MovementData movementData, MovementController controller)
        {
            if (controller.ropeSwing.IsSwinging) { LetGoOfRope(controller); }
        }

        /// <summary>
        /// Catches hold of a rope the character's hands reach in mid-air, and swings on it: a pendulum about the rope's
        /// anchor, the hands where they caught hold, carrying on with the character's speed across the rope. Gravity swings
        /// it, movement input pumps it (pushing the way the input points), and it loses a little speed each second; it goes
        /// no further out than the largest swing angle. Jump lets go with the swing's speed and a jump up; Crouch, or
        /// touching the ground, lets go. The character faces the way it steers. Catching hold gives back its jumps and air
        /// dashes. Something in the way stops the swing (the character stays on the rope).
        /// </summary>
        public override bool TakeOver(MovementData movementData, Inputs inputs, MovementController controller)
        {
            RopeSwingState swing = controller.ropeSwing;
            bool caught = false;
            if (!swing.IsSwinging)
            {
                if (!TryCatchRope(movementData, controller)) { return false; }
                caught = true;
            }
            Rope rope = swing.rope;
            if (rope == null || !rope.isActiveAndEnabled || (rope.IsHeld && rope.Holder != controller))
            {
                LetGoOfRope(controller); // What it was swinging on is gone: it falls on at the speed it was going.
                return false;
            }

            float dt = movementData.deltaTime;
            if (!caught)
            {
                swing.elapsed += dt;
                if (movementData.initialGrounded || Mode.ConsumeCrouchPress(inputs, controller))
                {
                    LetGoOfRope(controller);
                    return false;
                }
                if (Mode.IsNewJumpPress(inputs, controller))
                {
                    Vector3 launch = swing.velocity + Vector3.up * (ropeJumpUpSpeed * controller.SpeedScale);
                    LetGoOfRope(controller);
                    MovementHelpers.JumpOff(movementData, controller, launch, MovementEvents.Jumped);
                    return true;
                }
            }

            // The swing: the grip's velocity under gravity and the input's push, kept across the rope (its part along the
            // rope is what the rope holds back), losing a share of its speed, then moved on round the anchor.
            float radius = swing.gripDistance;
            Vector3 offset = swing.offset;
            Vector3 velocity = swing.velocity;
            velocity += Vector3.down * (Mode.GravityAcceleration(controller) * dt);
            velocity += MovementHelpers.InputDirection(movementData, inputs) * (ropeSwingAcceleration * controller.AccelerationScale * dt);
            Vector3 outward = offset / radius;
            velocity -= outward * Vector3.Dot(velocity, outward);
            velocity *= Mathf.Max(0f, 1f - ropeSwingDamping * dt);
            velocity = Vector3.ClampMagnitude(velocity, ropeMaxSwingSpeed * controller.SpeedScale);
            Vector3 next = offset + velocity * dt;
            next = next.sqrMagnitude > 1e-8f ? next.normalized : outward;
            // No further out than the largest swing angle: held there, it swings back.
            if (Vector3.Angle(next, Vector3.down) > ropeMaxSwingAngle)
            {
                next = Vector3.RotateTowards(Vector3.down, next, ropeMaxSwingAngle * Mathf.Deg2Rad, 0f).normalized;
            }
            next *= radius;
            // The velocity the step actually moves the grip at (round the anchor, and stopped at the largest angle).
            swing.velocity = (next - offset) / dt;
            swing.offset = next;

            Vector3 feet = rope.Anchor + next - Vector3.up * (ropeGripHeight * controller.Body.StandingHeight);
            movementData.velocityOverride = (feet - movementData.initialPosition) / dt;
            movementData.directMovementVector = Vector3.zero;
            Vector3 steer = MovementHelpers.InputDirection(movementData, inputs);
            if (steer.sqrMagnitude > 0.01f) { movementData.targetRotation = Quaternion.LookRotation(steer.normalized, Vector3.up); }
            movementData.state |= CharacterState.RopeSwinging;
            rope.Hold(controller, next);
            return true;
        }

        /// <summary>
        /// Once the step's movement is resolved: if something stopped the character short of where the swing would have
        /// taken it, the swing goes from where it got to, losing the speed it was stopped of. The rope is swung to the
        /// character's hands.
        /// </summary>
        public override void AfterMovementResolved(MovementData movementData, MovementController controller)
        {
            RopeSwingState swing = controller.ropeSwing;
            if (!swing.IsSwinging || swing.rope == null || !movementData.state.HasFlag(CharacterState.RopeSwinging)) { return; }
            Vector3 feet = movementData.initialPosition + movementData.finalMovementVector;
            Vector3 reached = feet + Vector3.up * (ropeGripHeight * controller.Body.StandingHeight) - swing.rope.Anchor;
            if (reached.sqrMagnitude < 1e-8f) { return; }
            reached = reached.normalized * swing.gripDistance;
            if ((reached - swing.offset).sqrMagnitude > 1e-8f)
            {
                float dt = movementData.deltaTime;
                Vector3 previous = swing.offset - swing.velocity * dt;
                Vector3 velocity = (reached - previous) / dt;
                Vector3 outward = reached / swing.gripDistance;
                swing.velocity = velocity - outward * Vector3.Dot(velocity, outward);
                swing.offset = reached;
            }
            swing.rope.Hold(controller, swing.offset);
        }

        /// <summary>
        /// Catches hold of the nearest rope the character's hands reach in mid-air this step (between where they are and
        /// where they're going), one no one else holds. Not the one let go of last, until the regrab delay is over. The
        /// hands hold it where they are (at least the shortest grip distance down it, at most its length), and the
        /// character's speed across the rope carries on as the swing's.
        /// </summary>
        private bool TryCatchRope(MovementData movementData, MovementController controller)
        {
            RopeSwingState swing = controller.ropeSwing;
            if (ropeGrabReach <= 0f || movementData.initialGrounded
                || Mode.AbilityInProgressExcept<WallSliding, Dashing>(controller)) { return false; }

            float dt = movementData.deltaTime;
            Vector3 grip = movementData.initialPosition + Vector3.up * (ropeGripHeight * controller.Body.StandingHeight);
            Vector3 gripNext = grip + movementData.initialVelocity * dt;
            float reach = ropeGrabReach * controller.HeightScale;

            Rope best = null;
            float bestSqrDistance = reach * reach;
            // Indexed, not foreach: All is an IReadOnlyList, whose enumerator is boxed, an allocation every step in the air.
            IReadOnlyList<Rope> ropes = Rope.All;
            for (int i = 0; i < ropes.Count; i++)
            {
                Rope rope = ropes[i];
                if (rope == null || rope.IsHeld || (rope == swing.lastRope && swing.regrabCooldown > 0f)) { continue; }
                float top = Mathf.Min(ropeMinGripDistance, rope.Length);
                Vector3 from = rope.Anchor + rope.Direction * top;
                float sqrDistance = MovementHelpers.SqrDistanceBetweenSegments(from, rope.Tip, grip, gripNext, out _, out _);
                if (sqrDistance > bestSqrDistance) { continue; }
                best = rope;
                bestSqrDistance = sqrDistance;
            }
            if (best == null) { return false; }

            // Held where the hands are: the rope swings to them (they're within reach of it).
            Vector3 offset = grip - best.Anchor;
            float radius = Mathf.Clamp(offset.magnitude, Mathf.Min(ropeMinGripDistance, best.Length), best.Length);
            Vector3 outward = offset.sqrMagnitude > 1e-8f ? offset.normalized : Vector3.down;
            if (Vector3.Angle(outward, Vector3.down) > ropeMaxSwingAngle)
            {
                outward = Vector3.RotateTowards(Vector3.down, outward, ropeMaxSwingAngle * Mathf.Deg2Rad, 0f).normalized;
            }
            Vector3 velocity = movementData.initialVelocity;
            swing.rope = best;
            swing.gripDistance = radius;
            swing.offset = outward * radius;
            swing.velocity = velocity - outward * Vector3.Dot(velocity, outward);
            swing.elapsed = 0f;
            best.Hold(controller, outward);
            MovementHelpers.TakeHoldOfALine(movementData, controller, this);
            return true;
        }

        /// <summary>
        /// Lets go of the rope swung on, which swings on by itself as it was going; the same rope can't be caught again
        /// for the regrab delay.
        /// </summary>
        private void LetGoOfRope(MovementController controller)
        {
            RopeSwingState swing = controller.ropeSwing;
            if (swing.rope != null)
            {
                // The rope's end swings at the same rate as the hands do, further out.
                float radius = Mathf.Max(swing.gripDistance, 0.01f);
                swing.rope.Release(controller, swing.velocity * (swing.rope.Length / radius));
            }
            swing.lastRope = swing.rope;
            swing.rope = null;
            swing.regrabCooldown = ropeRegrabDelay;
        }
    }

    /// <summary>
    /// A rope being swung on, and the delay before the last rope let go of can be caught again. The swing is a pendulum
    /// about the rope's anchor: the character's grip (where its hands hold the rope) stays the grip distance from it.
    /// </summary>
    public class RopeSwingState
    {
        /// <summary>The rope held (null when not swinging).</summary>
        public Level.Rope rope;
        /// <summary>How far (m) down the rope from its anchor the hands hold it.</summary>
        public float gripDistance;
        /// <summary>Where the grip is, from the anchor (gripDistance long).</summary>
        public Vector3 offset;
        /// <summary>How fast the grip is moving (m/s, across the rope).</summary>
        public Vector3 velocity;
        /// <summary>Seconds since catching hold.</summary>
        public float elapsed;
        /// <summary>
        /// The rope last let go of, which can't be caught again until regrabCooldown runs out (others can be caught at
        /// once, so the character can swing from rope to rope).
        /// </summary>
        public Level.Rope lastRope;
        /// <summary>Seconds until lastRope can be caught again.</summary>
        public float regrabCooldown;

        /// <summary>Whether the character is holding a rope.</summary>
        public bool IsSwinging => rope != null;

        /// <summary>
        /// Where the hands hold the rope now, and the way up the rope from there (unit length), from the rope's rendered
        /// pose: for placing the hands on it. False when not swinging.
        /// </summary>
        public bool TryGetGrip(out Vector3 point, out Vector3 upTheRope)
        {
            point = upTheRope = Vector3.zero;
            if (rope == null) { return false; }
            Vector3 down = rope.RenderDirection;
            point = rope.Anchor + down * gripDistance;
            upTheRope = -down;
            return true;
        }
    }

    // This state's place on the character (see MovementController): declared here, beside its ability, so a new
    // ability adds its state in its own file rather than in MovementController.cs.
    public partial class MovementController
    {
        /// <summary>The character's rope swinging state (RopeSwinging).</summary>
        public RopeSwingState ropeSwing { get; } = new RopeSwingState();
    }
}
