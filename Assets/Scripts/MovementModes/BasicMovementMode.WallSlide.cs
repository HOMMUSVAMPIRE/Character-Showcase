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
        [SerializeField] private float wallSlideJumpAwaySpeed = 9f;
        [Tooltip("How many times in a row the character can jump off the same wall it's sliding down before it touches the " +
                 "ground again (steering back into the wall after a jump slides down it again, so this limits climbing a " +
                 "wall in hops). Kicking off another wall, as up a chimney, starts the count again. 0 = no jumping off walls.")]
        [Min(0)] [SerializeField] private int wallSlideMaxJumps = 3;
        [Tooltip("Seconds after letting go of a wall (or jumping off it) before the character can slide down one again")]
        [SerializeField] private float wallSlideCooldown = 0.25f;
        [Tooltip("Seconds the character has to keep steering away from the wall before it lets go of a slide, so turning " +
                 "toward where to jump doesn't drop it off. Jumping and Crouch are at once")]
        [Min(0f)] [SerializeField] private float wallSlideReleaseDelay = 0.25f;
        [Tooltip("A slide starts on hitting a wall moving into it at least this fast (m/s, at the character's authored " +
                 "size), within the slide's angle of head-on, whatever the steering; slower, steering into the wall starts it")]
        [SerializeField] private float wallSlideMinApproachSpeed = 1f;
        [Tooltip("A slide only starts rising slower than this (m/s, at the character's authored size): a jump into a wall " +
                 "carries on up it, and is caught near the top of its rise, carrying on rising under gravity into the slide")]
        [SerializeField] private float wallSlideMaxStartRiseSpeed = 3f;

        // How long (seconds, at the authored size) the speed the character hit a wall with is remembered for starting a
        // slide: the wall removes it at once, and a slide can't always start the moment it's hit (a kick off one wall
        // reaches the other side of a chimney before the slide's cooldown is over).
        private const float WallSlideApproachMemory = 0.3f;

        float WallSlideMode.wallSlideSpeed { get => wallSlideSpeed; set => wallSlideSpeed = value; }
        float WallSlideMode.wallSlideMaxDuration { get => wallSlideMaxDuration; set => wallSlideMaxDuration = value; }

        /// <summary>
        /// Starts a slide down a wall when the character, in mid-air (falling, or rising slowly) and high enough above the
        /// ground, hits a wall in front of it near head-on (moving into it, as it hit it, or steering into it if it isn't
        /// moving much), and carries it on: the character
        /// is pressed against the wall facing it, its fall braked down to the slide speed, until it lands, the wall ends,
        /// it has steered away from the wall for the release delay, presses Crouch, or has slid for the slide's longest
        /// time. Jumping kicks off the wall, upward and away from it, up to a number of times before landing, and gives
        /// back the character's air jumps only off a different wall from the last one jumped off (since landing): so
        /// kicking between facing walls climbs them, but hopping back up the same wall takes the character's own jumps
        /// (or dashes) each time. Air steering comes back only gradually after a kick. Having slid off the end of its time on a wall, or dropped from
        /// it with Crouch, the character doesn't slide down that wall again before landing. Only walls that reach up the
        /// character's body are slid down, and the wall's top can still be grabbed during a slide, if it's within reach.
        /// </summary>
        public bool ProcessMovementDataForWallSlide(ref MovementData movementData, Inputs inputs, MovementController controller)
        {
            WallSlideState slide = controller.wallSlide;
            RememberApproach(movementData, slide);
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
            // Steering away only lets go once it's lasted the release delay.
            bool steeringAway = Vector3.Dot(steer, normal) > 0.5f;
            slide.releaseTime = steeringAway ? slide.releaseTime + movementData.deltaTime : 0f;
            if (dropped || (steeringAway && slide.releaseTime >= wallSlideReleaseDelay))
            {
                EndWallSlide(controller);
                if (dropped)
                {
                    slide.lastWallNormal = normal;
                    slide.hasLastWall = true;
                }
                return false;
            }

            // Only so many kicks in a row off the same wall; off another wall (up a chimney), the count starts again.
            int jumpsInARow = JumpedOffThisWallLast(controller, normal) ? slide.jumpsUsed : 0;
            if (IsNewJumpPress(inputs, controller) && jumpsInARow < wallSlideMaxJumps)
            {
                slide.jumpsUsed = jumpsInARow;
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
            // Rising fast into a wall carries on rising; the slide starts near the top of the rise, or falling.
            if (inertia.y > wallSlideMaxStartRiseSpeed * controller.SpeedScale) { return false; }

            // Which way the character is heading: as it hit (or is moving toward) the wall, if that's fast enough, so a
            // slide comes of running or jumping into a wall whatever the steering; or else as steered, or as it faces.
            Vector3 steer = InputDirection(movementData, inputs);
            Vector3 approach = Approach(movementData, slide, controller);
            bool approaching = approach.magnitude >= wallSlideMinApproachSpeed * controller.SpeedScale;
            Vector3 heading = approaching ? approach.normalized
                : steer.sqrMagnitude > 0.01f ? steer.normalized : Facing(movementData);
            Vector3 feet = movementData.initialPosition;
            if (!FindWallAhead(controller, feet, heading, out Vector3 normal, out Vector3 point)) { return false; }
            if (Vector3.Angle(heading, -normal) > wallSlideMaxAngle) { return false; }
            if (slide.hasLastWall && Vector3.Angle(normal, slide.lastWallNormal) < SameWallAngle) { return false; }
            bool movingIn = approaching && Vector3.Dot(approach, -normal) >= wallSlideMinApproachSpeed * controller.SpeedScale;
            bool steeringIn = !approaching && Vector3.Dot(steer, -normal) >= 0.5f;
            if (!steeringIn && !movingIn) { return false; }
            // Only a wall up against the character's body: one reaching a little higher than the lowest ledge it could
            // grab. The capsule sweep above also finds the top edge of a low wall caught with the legs, and sliding down
            // that, the character hung beside it from its knees, and was still sliding once the top came within reach
            // of its hands. A wall that low is vaulted over (see ProcessMovementDataForVault), or fallen past until its
            // top can be grabbed. A little higher, so a top just coming within reach is grabbed (the ledge is tried
            // first) rather than slid past on the step it's at the very bottom of the grab window.
            float bodyHeight = Mathf.Lerp(ledgeGrabMinHeight, ledgeGrabMaxHeight, 0.25f) * controller.StandingHeight;
            float bodyReach = controller.Radius + wallSlideCheckDistance * controller.HeightScale + 2f * controller.Skin;
            if (!controller.Raycast(feet + Vector3.up * bodyHeight, -normal, bodyReach, out _)) { return false; }

            float minHeight = wallSlideMinHeight * controller.HeightScale;
            if (controller.Raycast(feet + Vector3.up * controller.Skin, Vector3.down, minHeight + controller.Skin, out _)) { return false; }

            slide.IsWallSliding = true;
            slide.elapsed = 0f;
            slide.releaseTime = 0f;
            slide.wallNormal = normal;
            slide.wallPoint = point;
            return true;
        }

        /// <summary>
        /// Remembers how the character is moving (horizontally) while it's moving: hitting a wall stops it, and the
        /// speed it hit with is what a slide starts from (see Approach).
        /// </summary>
        private static void RememberApproach(MovementData movementData, WallSlideState slide)
        {
            Vector3 inertia = movementData.initialInertia;
            Vector3 horizontal = new Vector3(inertia.x, 0f, inertia.z);
            if (movementData.initialGrounded) { slide.approachTime = float.NegativeInfinity; return; }
            if (horizontal.sqrMagnitude > 0.25f)
            {
                slide.approachVelocity = horizontal;
                slide.approachTime = Time.fixedTime;
            }
        }

        /// <summary>
        /// How the character is coming at a wall (horizontally): as it's moving now, or the faster of that and how it
        /// was moving within the approach memory (before a wall stopped it).
        /// </summary>
        private static Vector3 Approach(MovementData movementData, WallSlideState slide, MovementController controller)
        {
            Vector3 inertia = movementData.initialInertia;
            Vector3 moving = new Vector3(inertia.x, 0f, inertia.z);
            bool remembered = Time.fixedTime - slide.approachTime <= WallSlideApproachMemory * controller.DurationScale;
            return remembered && slide.approachVelocity.sqrMagnitude > moving.sqrMagnitude ? slide.approachVelocity : moving;
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
        /// Kicks off the wall: upward, and away from it. Gives back the character's jumps, less the one this uses, and its
        /// air dashes, as a wall run does, but only off a different wall from the last one jumped off since landing: kicking off a
        /// wall, jumping back onto it higher and kicking off again, the jumps given back each time climbed any wall.
        /// Air steering comes back gradually, as after a wall run's jump.
        /// </summary>
        private void WallSlideJump(ref MovementData movementData, MovementController controller, Vector3 normal)
        {
            EndWallSlide(controller);
            controller.wallSlide.jumpsUsed++;
            controller.jumpPressConsumed = true;
            // Air dashes come back with the jumps, under the same rule (off a different wall from the last): given back
            // for every slide, hopping up one wall would have a dash each time on top of the character's own jumps.
            if (!JumpedOffThisWallLast(controller, normal))
            {
                GiveBackAirMoves(controller);
                controller.TryUseJump();
            }
            RememberWallJumpedOff(controller, normal);
            StartWallJumpSteeringRecovery(controller);
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
