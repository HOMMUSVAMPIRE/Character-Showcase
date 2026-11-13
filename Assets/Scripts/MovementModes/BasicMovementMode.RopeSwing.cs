using Level;
using UnityEngine;

namespace Character
{
    // Rope swinging: catching hold of a rope (a Rope) in mid-air, and swinging on it as a pendulum.
    public partial class BasicMovementMode
    {
        [Header("Rope Swinging")]
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

        float RopeSwingMode.ropeGrabReach { get => ropeGrabReach; set => ropeGrabReach = value; }

        /// <summary>
        /// Catches hold of a rope the character's hands reach in mid-air, and swings on it: a pendulum about the rope's
        /// anchor, the hands where they caught hold, carrying on with the character's speed across the rope. Gravity swings
        /// it, movement input pumps it (pushing the way the input points), and it loses a little speed each second; it goes
        /// no further out than the largest swing angle. Jump lets go with the swing's speed and a jump up; Crouch, or
        /// touching the ground, lets go. The character faces the way it steers. Catching hold gives back its jumps and air
        /// dashes. Something in the way stops the swing (the character stays on the rope).
        /// </summary>
        public bool ProcessMovementDataForRopeSwing(ref MovementData movementData, Inputs inputs, MovementController controller)
        {
            RopeSwingState swing = controller.ropeSwing;
            bool caught = false;
            if (!swing.IsSwinging)
            {
                if (!TryCatchRope(ref movementData, controller)) { return false; }
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
                if (movementData.initialGrounded || controller.crouch.press.Consume(inputs.crouch, crouchInputBuffer))
                {
                    LetGoOfRope(controller);
                    return false;
                }
                if (IsNewJumpPress(inputs, controller))
                {
                    controller.jumpPressConsumed = true;
                    Vector3 launch = swing.velocity + Vector3.up * (ropeJumpUpSpeed * controller.SpeedScale);
                    LetGoOfRope(controller);
                    controller.ResetJumps();
                    controller.TryUseJump();
                    movementData.velocityOverride = launch;
                    movementData.directMovementVector = Vector3.zero;
                    movementData.events |= MovementEvents.Jumped;
                    return true;
                }
            }

            // The swing: the grip's velocity under gravity and the input's push, kept across the rope (its part along the
            // rope is what the rope holds back), losing a share of its speed, then moved on round the anchor.
            float radius = swing.gripDistance;
            Vector3 offset = swing.offset;
            Vector3 velocity = swing.velocity;
            velocity += Vector3.down * (gravityStrength * controller.AccelerationScale * dt);
            velocity += InputDirection(movementData, inputs) * (ropeSwingAcceleration * controller.AccelerationScale * dt);
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

            Vector3 feet = rope.Anchor + next - Vector3.up * (ropeGripHeight * controller.StandingHeight);
            movementData.velocityOverride = (feet - movementData.initialPosition) / dt;
            movementData.directMovementVector = Vector3.zero;
            Vector3 steer = InputDirection(movementData, inputs);
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
        private void AfterRopeSwingResolved(MovementData movementData, MovementController controller)
        {
            RopeSwingState swing = controller.ropeSwing;
            if (!swing.IsSwinging || swing.rope == null || !movementData.state.HasFlag(CharacterState.RopeSwinging)) { return; }
            Vector3 feet = movementData.initialPosition + movementData.finalMovementVector;
            Vector3 reached = feet + Vector3.up * (ropeGripHeight * controller.StandingHeight) - swing.rope.Anchor;
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
        private bool TryCatchRope(ref MovementData movementData, MovementController controller)
        {
            RopeSwingState swing = controller.ropeSwing;
            if (ropeGrabReach <= 0f || movementData.initialGrounded
                || AbilityInProgress(controller, exceptWallSlide: true, exceptDash: true)) { return false; }

            float dt = movementData.deltaTime;
            Vector3 grip = movementData.initialPosition + Vector3.up * (ropeGripHeight * controller.StandingHeight);
            Vector3 gripNext = grip + movementData.initialVelocity * dt;
            float reach = ropeGrabReach * controller.HeightScale;

            Rope best = null;
            float bestDistance = reach * reach;
            foreach (Rope rope in Rope.All)
            {
                if (rope == null || rope.IsHeld || (rope == swing.lastRope && swing.regrabCooldown > 0f)) { continue; }
                float top = Mathf.Min(ropeMinGripDistance, rope.Length);
                Vector3 from = rope.Anchor + rope.Direction * top;
                float distance = ClosestBetweenSegments(from, rope.Tip, grip, gripNext, out _, out _);
                if (distance > bestDistance) { continue; }
                best = rope;
                bestDistance = distance;
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
            TakeHoldOfALine(ref movementData, controller);
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
}
