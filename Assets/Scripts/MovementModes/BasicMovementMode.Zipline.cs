using Level;
using UnityEngine;

namespace Character
{
    // Ziplines: catching hold of a zipline's cable (a Zipline) in mid-air, and riding along it, downhill, to its end.
    public partial class BasicMovementMode
    {
        [Header("Ziplines")]
        [Tooltip("How far (m) from the character's hands a zipline's cable can pass, at its authored size, for the " +
                 "character to catch hold of it in mid-air. 0 turns ziplines off")]
        [Min(0f)] [SerializeField] private float ziplineGrabReach = 0.6f;
        [Tooltip("How far below the cable the character's feet hang, as a share of its standing height (its hands are on the cable)")]
        [Min(0.1f)] [SerializeField] private float ziplineHangHeight = 1.15f;
        [Tooltip("The slowest (m/s) a ride goes, downhill, at the character's authored size, so a ride along a gentle cable still " +
                 "gets to its end. Uphill (caught heading up the cable) it slows to a stop and comes back down")]
        [Min(0f)] [SerializeField] private float ziplineMinSpeed = 3f;
        [Tooltip("The share of its speed a ride loses each second, to the cable's friction and the air (each zipline also has a top speed)")]
        [Range(0f, 2f)] [SerializeField] private float ziplineDrag = 0.1f;
        [Tooltip("How fast (m/s) the character moves into its hanging position under the cable once it catches hold, at its authored size")]
        [Min(0.1f)] [SerializeField] private float ziplineSnapSpeed = 6f;
        [Tooltip("How fast (m/s) jumping off a zipline sends the character up, at its authored size, on top of the ride's speed")]
        [Min(0f)] [SerializeField] private float ziplineJumpUpSpeed = 8f;
        [Tooltip("Seconds after letting go of a zipline before the same one can be caught again (another can be caught at once)")]
        [Min(0f)] [SerializeField] private float ziplineRegrabDelay = 0.5f;

        // How far (m, at the authored size) from a cable's ends the character lets go of it, so its hands stop short of
        // what holds the cable up.
        private const float ZiplineEndMargin = 0.4f;

        float ZiplineMode.ziplineGrabReach { get => ziplineGrabReach; set => ziplineGrabReach = value; }

        /// <summary>
        /// Catches hold of a zipline's cable the character's hands reach in mid-air, and rides it: hanging below the cable,
        /// facing the way it rides, speeding up downhill (gravity along the cable, less drag, at least the ride's
        /// slowest speed downhill, at most the zipline's top speed), until it reaches the end it's heading for and lets go,
        /// carrying on at the ride's speed. Jump lets go with a jump; Crouch lets go and drops; so does touching the
        /// ground, or being stopped by a wall or ceiling. Catching hold gives back the character's jumps and air dashes.
        /// </summary>
        public bool ProcessMovementDataForZipline(ref MovementData movementData, Inputs inputs, MovementController controller)
        {
            ZiplineState ride = controller.zipline;
            bool caught = false;
            if (!ride.IsRiding)
            {
                if (!TryCatchZipline(ref movementData, inputs, controller)) { return false; }
                caught = true;
            }
            Zipline line = ride.line;
            if (line == null || !line.isActiveAndEnabled)
            {
                LetGoOfZipline(controller); // What it was riding is gone: it falls on at the speed it was going.
                return false;
            }

            float dt = movementData.deltaTime;
            Vector3 direction = line.Direction;
            if (!caught)
            {
                ride.elapsed += dt;
                bool stopped = (movementData.previousCollisions & (CollisionFlags.Sides | CollisionFlags.Above)) != 0;
                if (movementData.initialGrounded || stopped || controller.crouch.press.Consume(inputs.crouch, crouchInputBuffer))
                {
                    LetGoOfZipline(controller);
                    return false;
                }
                if (IsNewJumpPress(inputs, controller))
                {
                    controller.jumpPressConsumed = true;
                    LetGoOfZipline(controller);
                    controller.ResetJumps();
                    controller.TryUseJump();
                    movementData.velocityOverride = direction * ride.speed + Vector3.up * (ziplineJumpUpSpeed * controller.SpeedScale);
                    movementData.directMovementVector = Vector3.zero;
                    movementData.events |= MovementEvents.Jumped;
                    return true;
                }
            }

            // Gravity along the cable speeds the ride up downhill and slows it uphill; drag takes a share of its speed.
            float g = gravityStrength * controller.AccelerationScale;
            ride.speed += -g * direction.y * dt;
            ride.speed -= ride.speed * ziplineDrag * dt;
            // The way the cable runs downhill (along a level one, the way the ride is going): at least the slowest speed
            // that way, so a ride never stops part way. Heading uphill, it's left to slow, stop and come back.
            float downhill = direction.y < -1e-3f ? 1f : direction.y > 1e-3f ? -1f : (ride.speed >= 0f ? 1f : -1f);
            float slowest = ziplineMinSpeed * controller.SpeedScale;
            if (ride.speed * downhill >= 0f && Mathf.Abs(ride.speed) < slowest) { ride.speed = downhill * slowest; }
            ride.speed = Mathf.Clamp(ride.speed, -line.MaxSpeed, line.MaxSpeed);

            float length = line.Length;
            float margin = Mathf.Min(ZiplineEndMargin * controller.HeightScale, 0.5f * length);
            ride.along += ride.speed * dt;
            bool reachedEnd = (ride.speed > 0f && ride.along >= length - margin) || (ride.speed < 0f && ride.along <= margin);
            ride.along = Mathf.Clamp(ride.along, margin, length - margin);

            // Along the cable at the ride's speed, closing in on hanging straight below it (quickly, but not in one jump,
            // from wherever the character caught hold).
            Vector3 feet = movementData.initialPosition;
            Vector3 travel = direction * (ride.speed * dt);
            Vector3 hang = line.PointAt(ride.along) - Vector3.up * (ziplineHangHeight * controller.StandingHeight);
            Vector3 correction = Vector3.ClampMagnitude(hang - (feet + travel), ziplineSnapSpeed * controller.SpeedScale * dt);
            movementData.velocityOverride = (travel + correction) / dt;
            movementData.directMovementVector = Vector3.zero;

            Vector3 facing = direction * (ride.speed < 0f ? -1f : 1f);
            facing.y = 0f;
            if (facing.sqrMagnitude > 1e-4f) { movementData.targetRotation = Quaternion.LookRotation(facing.normalized, Vector3.up); }

            if (reachedEnd)
            {
                // Off the end, carrying on at the ride's speed.
                LetGoOfZipline(controller);
                movementData.velocityOverride = direction * ride.speed;
                return true;
            }
            movementData.state |= CharacterState.Ziplining;
            return true;
        }

        /// <summary>
        /// Catches hold of the nearest zipline cable the character's hands reach in mid-air this step (between where they
        /// are and where they're going: a fast fall passes a cable in less than a step), with room to hang below it. Not
        /// the one let go of last, until the regrab delay is over.
        /// </summary>
        private bool TryCatchZipline(ref MovementData movementData, Inputs inputs, MovementController controller)
        {
            ZiplineState ride = controller.zipline;
            if (ziplineGrabReach <= 0f || movementData.initialGrounded
                || AbilityInProgress(controller, exceptWallSlide: true, exceptDash: true)) { return false; }

            float dt = movementData.deltaTime;
            float hangHeight = ziplineHangHeight * controller.StandingHeight;
            Vector3 grip = movementData.initialPosition + Vector3.up * hangHeight;
            Vector3 gripNext = grip + movementData.initialVelocity * dt;
            float reach = ziplineGrabReach * controller.HeightScale;
            float margin = ZiplineEndMargin * controller.HeightScale;

            Zipline best = null;
            float bestDistance = reach * reach, bestAlong = 0f;
            foreach (Zipline line in Zipline.All)
            {
                if (line == null || (line == ride.lastLine && ride.regrabCooldown > 0f)) { continue; }
                float length = line.Length;
                if (length <= 2f * margin) { continue; }
                Vector3 direction = line.Direction;
                Vector3 from = line.Start + direction * margin, to = line.End - direction * margin;
                float distance = ClosestBetweenSegments(from, to, grip, gripNext, out float onCable, out _);
                if (distance > bestDistance) { continue; }
                best = line;
                bestDistance = distance;
                bestAlong = margin + onCable * (length - 2f * margin);
            }
            if (best == null) { return false; }
            Vector3 hang = best.PointAt(bestAlong) - Vector3.up * hangHeight;
            if (!controller.CanFitAt(hang, 1f)) { return false; }

            ride.line = best;
            ride.along = bestAlong;
            // The ride starts at the character's speed along the cable, either way.
            ride.speed = Vector3.Dot(movementData.initialVelocity, best.Direction);
            ride.elapsed = 0f;
            TakeHoldOfALine(ref movementData, controller);
            return true;
        }

        /// <summary>Lets go of the zipline ridden; the same one can't be caught again for the regrab delay.</summary>
        private void LetGoOfZipline(MovementController controller)
        {
            ZiplineState ride = controller.zipline;
            ride.lastLine = ride.line;
            ride.line = null;
            ride.regrabCooldown = ziplineRegrabDelay;
        }

        /// <summary>
        /// What catching hold of a rope or a zipline does besides: it counts as touching the ground (the character's jumps
        /// and air dashes come back), it's done standing (a character crouched in the air stands), and it ends a dash or a
        /// slide down a wall the character was in.
        /// </summary>
        private void TakeHoldOfALine(ref MovementData movementData, MovementController controller)
        {
            GiveBackAirMoves(controller);
            controller.crouch.IsCrouched = false;
            controller.crouch.wantsToStand = false;
            if (controller.wallSlide.IsWallSliding) { EndWallSlide(controller); }
            if (controller.dash.IsDashing)
            {
                controller.dash.timeRemaining = 0f;
                movementData.events |= MovementEvents.DashEnded;
            }
        }

        /// <summary>
        /// The closest points between two line segments (p1 to q1, and p2 to q2): how far along each (0 to 1) they are,
        /// and the square of the distance between them. (Ericson, Real-Time Collision Detection, 5.1.9.)
        /// </summary>
        private static float ClosestBetweenSegments(Vector3 p1, Vector3 q1, Vector3 p2, Vector3 q2, out float s, out float t)
        {
            Vector3 d1 = q1 - p1, d2 = q2 - p2, r = p1 - p2;
            float a = Vector3.Dot(d1, d1), e = Vector3.Dot(d2, d2), f = Vector3.Dot(d2, r);
            const float Epsilon = 1e-8f;
            if (a <= Epsilon && e <= Epsilon)
            {
                s = t = 0f;
                return r.sqrMagnitude;
            }
            if (a <= Epsilon)
            {
                s = 0f;
                t = Mathf.Clamp01(f / e);
            }
            else
            {
                float c = Vector3.Dot(d1, r);
                if (e <= Epsilon)
                {
                    t = 0f;
                    s = Mathf.Clamp01(-c / a);
                }
                else
                {
                    float b = Vector3.Dot(d1, d2), denominator = a * e - b * b;
                    s = denominator > Epsilon ? Mathf.Clamp01((b * f - c * e) / denominator) : 0f;
                    t = (b * s + f) / e;
                    if (t < 0f) { t = 0f; s = Mathf.Clamp01(-c / a); }
                    else if (t > 1f) { t = 1f; s = Mathf.Clamp01((b - c) / a); }
                }
            }
            return (p1 + d1 * s - (p2 + d2 * t)).sqrMagnitude;
        }
    }
}
