using UnityEngine;

namespace Character
{
    // Wall sliding: running into a wall head-on in mid-air slides slowly down it, and jumping kicks off it, upward.
    public partial class BasicMovementMode
    {
        [Header("Wall Sliding")]
        [Tooltip("The fastest (m/s) the character slides down a wall, at its authored size")]
        [SerializeField] private float wallSlideSpeed = 2.5f;
        [Tooltip("How quickly (m/s²) a fall is braked down to the slide speed on hitting a wall, at the character's authored size")]
        [SerializeField] private float wallSlideBraking = 30f;
        [Tooltip("The longest a slide down a wall lasts, in seconds, before the character falls off it. 0 turns wall sliding off")]
        [SerializeField] private float wallSlideMaxDuration = 3f;
        [Tooltip("How far (degrees) from heading straight into a wall the character can be and still slide down it (more " +
                 "glancing, it can wall-run along it instead)")]
        [Range(0f, 80f)] [SerializeField] private float wallSlideMaxAngle = 45f;
        [Tooltip("How far (m) in front of the character a wall can be to slide down it, at its authored size")]
        [SerializeField] private float wallSlideCheckDistance = 0.2f;
        [Tooltip("A slide down a wall only starts this high (m) or more above the ground, at the character's authored size")]
        [SerializeField] private float wallSlideMinHeight = 0.5f;
        [Tooltip("How fast (m/s) jumping off a wall the character is sliding down launches it upward")]
        [SerializeField] private float wallSlideJumpUpSpeed = 12f;
        [Tooltip("How fast (m/s) jumping off a wall the character is sliding down pushes it away from the wall")]
        [SerializeField] private float wallSlideJumpAwaySpeed = 5f;
        [Tooltip("How many times the character can jump off walls it's sliding down before it touches the ground again " +
                 "(steering back into the wall after a jump slides down it again, so this limits climbing a wall in hops). " +
                 "0 = no jumping off walls.")]
        [Min(0)] [SerializeField] private int wallSlideMaxJumps = 3;
        [Tooltip("Seconds after letting go of a wall (or jumping off it) before the character can slide down one again")]
        [SerializeField] private float wallSlideCooldown = 0.25f;

        float WallSlideMode.wallSlideSpeed { get => wallSlideSpeed; set => wallSlideSpeed = value; }
        float WallSlideMode.wallSlideMaxDuration { get => wallSlideMaxDuration; set => wallSlideMaxDuration = value; }

        /// <summary>
        /// Starts a slide down a wall when the character, falling in mid-air and high enough above the ground, heads into
        /// a wall in front of it near head-on (steering into it, or moving toward it), and carries it on: the character
        /// is pressed against the wall facing it, its fall braked down to the slide speed, until it lands, the wall ends,
        /// it steers away from the wall, presses Crouch, or has slid for the slide's longest time. Jumping kicks off
        /// the wall, upward and a little away from it, and gives back the character's air jumps, as a wall run's jump
        /// does, up to a number of times before landing. Having slid off the end of its time on a wall, or dropped from
        /// it with Crouch, the character doesn't slide down that wall again before landing.
        /// </summary>
        public bool ProcessMovementDataForWallSlide(ref MovementData movementData, Inputs inputs, MovementController controller)
        {
            WallSlideState slide = controller.wallSlide;
            if (!slide.IsWallSliding && !TryStartWallSlide(ref movementData, inputs, controller)) { return false; }

            Vector3 feet = movementData.initialPosition;
            if (movementData.initialGrounded || !FindWallAhead(controller, feet, -slide.wallNormal, out Vector3 normal, out Vector3 point))
            {
                EndWallSlide(controller);
                return false;
            }
            slide.wallNormal = normal;
            slide.wallPoint = point;
            slide.elapsed += movementData.deltaTime;

            Vector3 steer = InputDirection(movementData, inputs);
            // Sliding off the end of its time, or dropping off with Crouch, the character falls: it doesn't slide down
            // the same wall again before it touches the ground (or that time would mean nothing). Steering away is
            // only letting go, and steering back in slides on.
            bool dropped = slide.elapsed > wallSlideMaxDuration * controller.DurationScale
                           || controller.crouch.press.Consume(inputs.crouch, crouchInputBuffer);
            if (dropped || Vector3.Dot(steer, normal) > 0.5f)
            {
                EndWallSlide(controller);
                if (dropped)
                {
                    slide.lastWallNormal = normal;
                    slide.hasLastWall = true;
                }
                return false;
            }

            if (IsNewJumpPress(inputs, controller) && slide.jumpsUsed < wallSlideMaxJumps)
            {
                WallSlideJump(ref movementData, controller, normal);
                return true;
            }

            // Slide down: a faster fall is braked down to the slide speed, a slower one speeds up to it under gravity.
            float slideSpeed = wallSlideSpeed * controller.SpeedScale;
            float fall = movementData.initialInertia.y;
            fall = fall > -slideSpeed
                ? Mathf.Max(-slideSpeed, fall - gravityStrength * controller.AccelerationScale * movementData.deltaTime)
                : Mathf.MoveTowards(fall, -slideSpeed, wallSlideBraking * controller.AccelerationScale * movementData.deltaTime);
            movementData.velocityOverride = Vector3.up * fall - normal * (WallRunStickSpeed * controller.SpeedScale);
            movementData.directMovementVector = Vector3.zero;
            movementData.targetRotation = Quaternion.LookRotation(-normal, Vector3.up);
            movementData.state |= CharacterState.WallSliding;
            return true;
        }

        /// <summary>
        /// Starts a slide down a wall if the character is falling in mid-air, high enough above the ground, and heading
        /// into a wall just in front of it near head-on: steering into it, or moving toward it.
        /// </summary>
        private bool TryStartWallSlide(ref MovementData movementData, Inputs inputs, MovementController controller)
        {
            WallSlideState slide = controller.wallSlide;
            if (wallSlideMaxDuration <= 0f || movementData.initialGrounded || slide.cooldownRemaining > 0f
                || controller.crouch.IsCrouched || AbilityInProgress(controller))
            {
                return false;
            }
            Vector3 inertia = movementData.initialInertia;
            if (inertia.y > 0f) { return false; } // Rising into a wall carries on rising; the slide starts as it falls.

            // Which way the character is heading: as steered, or else as it's moving, or else as it faces.
            Vector3 steer = InputDirection(movementData, inputs);
            Vector3 moving = new Vector3(inertia.x, 0f, inertia.z);
            Vector3 heading = steer.sqrMagnitude > 0.01f ? steer.normalized
                : moving.sqrMagnitude > 0.01f ? moving.normalized : Facing(movementData);
            Vector3 feet = movementData.initialPosition;
            if (!FindWallAhead(controller, feet, heading, out Vector3 normal, out Vector3 point)) { return false; }
            if (Vector3.Angle(heading, -normal) > wallSlideMaxAngle) { return false; }
            if (slide.hasLastWall && Vector3.Angle(normal, slide.lastWallNormal) < SameWallAngle) { return false; }
            bool steeringIn = Vector3.Dot(steer, -normal) >= 0.5f;
            bool movingIn = Vector3.Dot(moving, -normal) >= 1f * controller.SpeedScale;
            if (!steeringIn && !movingIn) { return false; }

            float minHeight = wallSlideMinHeight * controller.HeightScale;
            if (controller.Raycast(feet + Vector3.up * controller.Skin, Vector3.down, minHeight + controller.Skin, out _)) { return false; }

            slide.IsWallSliding = true;
            slide.elapsed = 0f;
            slide.wallNormal = normal;
            slide.wallPoint = point;
            return true;
        }

        /// <summary>
        /// Whether there's a wall to slide down in front of the character in the given direction (within the check
        /// distance, and upright enough), its normal, flattened (pointing away from the wall), and where it was hit.
        /// </summary>
        private bool FindWallAhead(MovementController controller, Vector3 feet, Vector3 direction, out Vector3 normal, out Vector3 point)
        {
            normal = point = Vector3.zero;
            float reach = wallSlideCheckDistance * controller.HeightScale + controller.Skin;
            if (!controller.SweepCapsule(feet, direction, reach, out RaycastHit hit)) { return false; }
            if (Mathf.Abs(hit.normal.y) > Mathf.Sin(wallRunMaxWallTilt * Mathf.Deg2Rad)) { return false; }
            normal = new Vector3(hit.normal.x, 0f, hit.normal.z);
            if (normal.sqrMagnitude < 1e-6f) { return false; }
            normal.Normalize();
            point = hit.point;
            return true;
        }

        /// <summary>
        /// Kicks off the wall: upward, and a little away from it. Gives back the character's jumps, less the one this
        /// uses, and steers like a jump from the ground, as a wall run's jump does.
        /// </summary>
        private void WallSlideJump(ref MovementData movementData, MovementController controller, Vector3 normal)
        {
            EndWallSlide(controller);
            controller.wallSlide.jumpsUsed++;
            controller.jumpPressConsumed = true;
            controller.ResetJumps();
            controller.TryUseJump();
            // Counted as leaving the ground, so air steering ramps back in as after a jump (see WallJump).
            controller.lastGroundedTime = Time.fixedTime;
            movementData.velocityOverride = normal * (wallSlideJumpAwaySpeed * controller.SpeedScale) + Vector3.up * (wallSlideJumpUpSpeed * controller.SpeedScale);
            movementData.directMovementVector = Vector3.zero;
            movementData.targetRotation = Quaternion.LookRotation(normal, Vector3.up);
            movementData.events |= MovementEvents.WallJumped;
            movementData.state |= CharacterState.Moving;
        }

        private void EndWallSlide(MovementController controller)
        {
            controller.wallSlide.IsWallSliding = false;
            controller.wallSlide.cooldownRemaining = wallSlideCooldown;
        }
    }
}
