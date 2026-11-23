using System;
using System.Collections.Generic;
using MeridianMotion.Levels;
using UnityEngine;
using static MeridianMotion.Characters.MovementRules;

namespace MeridianMotion.Characters
{
    /// <summary>
    /// Rope swinging (see MeridianMotion.Levels.SwingRope): catching hold of a rope in mid-air, and swinging on it as a pendulum.
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

        /// <summary>The events of a moment it raises (see MovementComponent.OneOffEvents): a jump off the rope.</summary>
        public override MovementEvents OneOffEvents => MovementEvents.Jumped;
        public override bool IsActive(MMotionMotor motor) => motor.ropeSwing.IsSwinging;
        /// <summary>Swinging, the character holds on to the rope, which holds it up.</summary>
        public override AbilityTraits Traits => AbilityTraits.Supported | AbilityTraits.Attached;
        /// <summary>The events of RopeSwinging starting and ending (see MovementComponent.StateEvents).</summary>
        public override StateEventPair[] StateEvents => stateEvents;
        private static readonly StateEventPair[] stateEvents =
        {
            new(CharacterState.RopeSwinging, MovementEvents.RopeGrabbed, MovementEvents.RopeReleased),
        };

        public override void AdvanceTimers(MovementStep step, StepInputs inputs, MMotionMotor motor)
        {
            motor.ropeSwing.regrabCooldown = MovementRules.CountDown(motor.ropeSwing.regrabCooldown, step.deltaTime);
        }

        public override void Interrupt(MovementStep step, MMotionMotor motor)
        {
            if (motor.ropeSwing.IsSwinging) { LetGoOfRope(motor); }
        }

        /// <summary>
        /// Catches hold of a rope the character's hands reach in mid-air, and swings on it: a pendulum about the rope's
        /// anchor, the hands where they caught hold, carrying on with the character's speed across the rope. Gravity swings
        /// it, movement input pumps it (pushing the way the input points), and it loses a little speed each second; it goes
        /// no further out than the largest swing angle. Jump lets go with the swing's speed and a jump up; Crouch, or
        /// touching the ground, lets go. The character faces the way it steers. Catching hold gives back its jumps and air
        /// dashes. Something in the way stops the swing (the character stays on the rope).
        /// </summary>
        public override bool TakeOver(MovementStep step, StepInputs inputs, MMotionMotor motor)
        {
            RopeSwingState swing = motor.ropeSwing;
            bool caught = false;
            if (!swing.IsSwinging)
            {
                if (!TryCatchRope(step, motor)) { return false; }
                caught = true;
            }
            SwingRope rope = swing.rope;
            if (rope == null || !rope.isActiveAndEnabled || (rope.IsHeld && rope.Holder != motor))
            {
                LetGoOfRope(motor); // What it was swinging on is gone: it falls on at the speed it was going.
                return false;
            }

            float dt = step.deltaTime;
            if (!caught)
            {
                swing.elapsed += dt;
                if (step.initialGrounded || Mode.ConsumeCrouchPress(inputs, motor))
                {
                    LetGoOfRope(motor);
                    return false;
                }
                if (Mode.HasFreshJumpPress(inputs, motor))
                {
                    Vector3 launch = swing.velocity + Vector3.up * (ropeJumpUpSpeed * motor.SpeedScale);
                    LetGoOfRope(motor);
                    MovementRules.JumpOff(step, motor, launch, MovementEvents.Jumped);
                    return true;
                }
            }

            // The swing: the grip's velocity under gravity and the input's push, kept across the rope (its part along the
            // rope is what the rope holds back), losing a share of its speed, then moved on round the anchor.
            float radius = swing.gripDistance;
            Vector3 offset = swing.offset;
            Vector3 velocity = swing.velocity;
            velocity += Vector3.down * (Mode.GravityAcceleration(motor) * dt);
            velocity += MovementRules.InputDirection(step, inputs) * (ropeSwingAcceleration * motor.AccelerationScale * dt);
            Vector3 outward = offset / radius;
            velocity -= outward * Vector3.Dot(velocity, outward);
            velocity *= Mathf.Max(0f, 1f - ropeSwingDamping * dt);
            velocity = Vector3.ClampMagnitude(velocity, ropeMaxSwingSpeed * motor.SpeedScale);
            Vector3 next = offset + velocity * dt;
            next = NormalizedOr(next, outward, 1e-8f);
            // No further out than the largest swing angle: held there, it swings back.
            if (Vector3.Angle(next, Vector3.down) > ropeMaxSwingAngle)
            {
                next = Vector3.RotateTowards(Vector3.down, next, ropeMaxSwingAngle * Mathf.Deg2Rad, 0f).normalized;
            }
            next *= radius;
            // The velocity the step actually moves the grip at (round the anchor, and stopped at the largest angle).
            swing.velocity = (next - offset) / dt;
            swing.offset = next;

            Vector3 feet = rope.Anchor + next - Vector3.up * (ropeGripHeight * motor.Body.StandingHeight);
            step.velocityOverride = (feet - step.initialPosition) / dt;
            step.directMovementVector = Vector3.zero;
            Vector3 steer = MovementRules.InputDirection(step, inputs);
            if (steer.sqrMagnitude > 0.01f) { step.targetRotation = Quaternion.LookRotation(steer.normalized, Vector3.up); }
            step.state |= CharacterState.RopeSwinging;
            rope.Hold(motor, next);
            return true;
        }

        /// <summary>
        /// Once the step's movement is resolved: if something stopped the character short of where the swing would have
        /// taken it, the swing goes from where it got to, losing the speed it was stopped of. The rope is swung to the
        /// character's hands.
        /// </summary>
        public override void AfterMovementResolved(MovementStep step, MMotionMotor motor)
        {
            RopeSwingState swing = motor.ropeSwing;
            if (!swing.IsSwinging || swing.rope == null || !step.state.HasAny(CharacterState.RopeSwinging)) { return; }
            Vector3 feet = step.initialPosition + step.finalMovementVector;
            Vector3 reached = feet + Vector3.up * (ropeGripHeight * motor.Body.StandingHeight) - swing.rope.Anchor;
            if (reached.sqrMagnitude < 1e-8f) { return; }
            reached = reached.normalized * swing.gripDistance;
            if ((reached - swing.offset).sqrMagnitude > 1e-8f)
            {
                float dt = step.deltaTime;
                Vector3 previous = swing.offset - swing.velocity * dt;
                Vector3 velocity = (reached - previous) / dt;
                Vector3 outward = reached / swing.gripDistance;
                swing.velocity = velocity - outward * Vector3.Dot(velocity, outward);
                swing.offset = reached;
            }
            swing.rope.Hold(motor, swing.offset);
        }

        /// <summary>
        /// Catches hold of the nearest rope the character's hands reach in mid-air this step (between where they are and
        /// where they're going), one no one else holds. Not the one let go of last, until the regrab delay is over. The
        /// hands hold it where they are (at least the shortest grip distance down it, at most its length), and the
        /// character's speed across the rope carries on as the swing's.
        /// </summary>
        private bool TryCatchRope(MovementStep step, MMotionMotor motor)
        {
            RopeSwingState swing = motor.ropeSwing;
            if (ropeGrabReach <= 0f || step.initialGrounded
                || Mode.AbilityInProgressExcept<WallSliding, Dashing>(motor)) { return false; }

            float dt = step.deltaTime;
            Vector3 grip = step.initialPosition + Vector3.up * (ropeGripHeight * motor.Body.StandingHeight);
            Vector3 gripNext = grip + step.initialVelocity * dt;
            float reach = ropeGrabReach * motor.HeightScale;

            SwingRope best = null;
            float bestSqrDistance = reach * reach;
            // Indexed, not foreach: All is an IReadOnlyList, whose enumerator is boxed, an allocation every step in the air.
            IReadOnlyList<SwingRope> ropes = SwingRope.All;
            for (int i = 0; i < ropes.Count; i++)
            {
                SwingRope rope = ropes[i];
                if (rope == null || rope.IsHeld || (rope == swing.lastRope && swing.regrabCooldown > 0f)) { continue; }
                float top = Mathf.Min(ropeMinGripDistance, rope.Length);
                Vector3 from = rope.Anchor + rope.Direction * top;
                float sqrDistance = MovementRules.SqrDistanceBetweenSegments(from, rope.Tip, grip, gripNext, out _, out _);
                // The nearest; of two as near, the first by where it is (StableOrder), not the order they were enabled in.
                if (sqrDistance > bestSqrDistance || (sqrDistance == bestSqrDistance && best != null && StableOrder.Compare(rope.Anchor, best.Anchor) >= 0)) { continue; }
                best = rope;
                bestSqrDistance = sqrDistance;
            }
            if (best == null) { return false; }

            // Held where the hands are: the rope swings to them (they're within reach of it).
            Vector3 offset = grip - best.Anchor;
            float radius = Mathf.Clamp(offset.magnitude, Mathf.Min(ropeMinGripDistance, best.Length), best.Length);
            Vector3 outward = NormalizedOr(offset, Vector3.down, 1e-8f);
            if (Vector3.Angle(outward, Vector3.down) > ropeMaxSwingAngle)
            {
                outward = Vector3.RotateTowards(Vector3.down, outward, ropeMaxSwingAngle * Mathf.Deg2Rad, 0f).normalized;
            }
            Vector3 velocity = step.initialVelocity;
            swing.rope = best;
            swing.gripDistance = radius;
            swing.offset = outward * radius;
            swing.velocity = velocity - outward * Vector3.Dot(velocity, outward);
            swing.elapsed = 0f;
            best.Hold(motor, outward);
            MovementRules.AttachToLine(step, motor, this);
            return true;
        }

        /// <summary>
        /// Lets go of the rope swung on, which swings on by itself as it was going; the same rope can't be caught again
        /// for the regrab delay.
        /// </summary>
        private void LetGoOfRope(MMotionMotor motor)
        {
            RopeSwingState swing = motor.ropeSwing;
            if (swing.rope != null)
            {
                // The rope's end swings at the same rate as the hands do, further out.
                float radius = Mathf.Max(swing.gripDistance, 0.01f);
                swing.rope.Release(motor, swing.velocity * (swing.rope.Length / radius));
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
        public MeridianMotion.Levels.SwingRope rope;
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
        public MeridianMotion.Levels.SwingRope lastRope;
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

    // This state's place on the character (see MMotionMotor): declared here, beside its ability, so a new
    // ability adds its state in its own file rather than in MMotionMotor.cs.
    public partial class MMotionMotor
    {
        /// <summary>The character's rope swinging state (RopeSwinging).</summary>
        public RopeSwingState ropeSwing { get; } = new RopeSwingState();
    }
}
