using UnityEngine;

namespace Character
{
    // Ledges: catching hold of a ledge in mid-air, hanging from it, and climbing up onto it.
    public partial class BasicMovementMode
    {
        [Header("Ledges")]
        [Tooltip("Seconds a climb from hanging up onto the ledge takes, at the character's authored size. 0 turns ledge " +
                 "grabbing off")]
        [SerializeField] private float ledgeClimbDuration = 0.7f;
        [Tooltip("The lowest a ledge can be to grab, above the character's feet, as a share of its standing height")]
        [SerializeField] private float ledgeGrabMinHeight = 0.7f;
        [Tooltip("The highest a ledge can be to grab, above the character's feet, as a share of its standing height " +
                 "(about as high as its hands reach)")]
        [SerializeField] private float ledgeGrabMaxHeight = 1.3f;
        [Tooltip("How far below a ledge the character's feet hang from it, as a share of its standing height")]
        [SerializeField] private float ledgeHangHeight = 1.1f;
        [Tooltip("How far (m) in front of the character a ledge's wall can be to grab, at its authored size")]
        [SerializeField] private float ledgeGrabReach = 0.3f;
        [Tooltip("A ledge is only grabbed while the character is rising slower than this (m/s), so a jump passes ledges on " +
                 "the way up and catches them near the top of the jump or falling")]
        [SerializeField] private float ledgeMaxGrabRiseSpeed = 2f;
        [Tooltip("How far (degrees) from facing a ledge's wall head-on the character can be heading and still grab it")]
        [Range(0f, 90f)] [SerializeField] private float ledgeMaxGrabAngle = 50f;
        [Tooltip("How fast (m/s) the character moves into its hanging position once it grabs a ledge, straight below the " +
                 "point of the edge it grabbed, against the wall")]
        [SerializeField] private float ledgeSnapSpeed = 12f;
        [Tooltip("How far (m) past the ledge's edge the character's collider ends up after climbing onto it, at its " +
                 "authored size")]
        [SerializeField] private float ledgeClimbInset = 0.1f;
        [Tooltip("Seconds after letting go of a ledge before one can be grabbed again, so dropping doesn't catch the same ledge")]
        [SerializeField] private float ledgeRegrabDelay = 0.4f;

        // The share of a climb spent rising to the ledge's height, before moving forward onto it.
        private const float ClimbRiseShare = 0.6f;

        float LedgeMode.ledgeClimbDuration { get => ledgeClimbDuration; set => ledgeClimbDuration = value; }

        /// <summary>
        /// Grabs a ledge the character reaches for in mid-air (heading toward it, its top within reach of the
        /// character's hands, and room to hang below it), and carries on hanging or climbing: hanging, the character
        /// holds still below the ledge (moving with it, if it moves); Jump climbs up onto it (crouched, if there's only
        /// room to crouch on top), and Crouch or steering away from the wall lets go, as does the ledge's object going.
        /// </summary>
        public bool ProcessMovementDataForLedge(ref MovementData movementData, Inputs inputs, MovementController controller)
        {
            LedgeState ledge = controller.ledge;
            if (!ledge.IsActive && !TryGrabLedge(ref movementData, inputs, controller)) { return false; }
            if (ledge.ledge == null || !ledge.ledge.gameObject.activeInHierarchy)
            {
                LetGoOfLedge(controller); // What it belonged to is gone.
                return false;
            }

            MovementController.GetPhysicsPose(ledge.ledge, out Vector3 ledgePosition, out Quaternion ledgeRotation);
            Vector3 wallNormal = ledgeRotation * ledge.localWallNormal;
            wallNormal.y = 0f;
            wallNormal = wallNormal.sqrMagnitude > 1e-6f ? wallNormal.normalized : -Facing(movementData);
            movementData.directMovementVector = Vector3.zero;
            movementData.targetRotation = Quaternion.LookRotation(-wallNormal, Vector3.up);

            if (ledge.IsHanging)
            {
                // Standing on something while hanging (a floor just below the hanging position) lets go too.
                bool letGo = movementData.initialGrounded
                             || controller.crouch.press.Consume(inputs.crouch, crouchInputBuffer)
                             || Vector3.Dot(InputDirection(movementData, inputs), wallNormal) > 0.5f;
                if (letGo)
                {
                    LetGoOfLedge(controller);
                    movementData.initialInertia = Vector3.zero;
                    return false;
                }
                if (IsNewJumpPress(inputs, controller))
                {
                    controller.jumpPressConsumed = true;
                    TryStartClimb(movementData, controller, ledgePosition, ledgeRotation);
                }
            }

            if (ledge.IsClimbing) { return Climb(ref movementData, controller, ledgePosition, ledgeRotation); }

            // Hanging: move into the hanging position (quickly, but not in one jump), then hold it, without gravity.
            Vector3 hang = ledgePosition + ledgeRotation * ledge.localHangPosition;
            Vector3 move = Vector3.ClampMagnitude(hang - movementData.initialPosition, ledgeSnapSpeed * controller.SpeedScale * movementData.deltaTime);
            movementData.velocityOverride = move / movementData.deltaTime;
            movementData.state |= CharacterState.LedgeHanging;
            return true;
        }

        /// <summary>
        /// Grabs a ledge if the character is in mid-air, not rising fast, and heading toward a wall whose top is within
        /// reach of its hands, with room to hang below it.
        /// </summary>
        private bool TryGrabLedge(ref MovementData movementData, Inputs inputs, MovementController controller)
        {
            LedgeState ledge = controller.ledge;
            // A wall being slid down doesn't stop its top being grabbed, once the slide has brought it within reach.
            if (ledgeClimbDuration <= 0f || movementData.initialGrounded || ledge.regrabCooldown > 0f
                || AbilityInProgress(controller, exceptWallSlide: true)) { return false; }
            if (movementData.initialVelocity.y > ledgeMaxGrabRiseSpeed * controller.SpeedScale) { return false; }

            Vector3 heading = InputDirection(movementData, inputs);
            heading = heading.sqrMagnitude > 0.01f ? heading.normalized : Facing(movementData);
            Vector3 feet = movementData.initialPosition;
            float height = controller.StandingHeight;
            if (!FindLedge(controller, feet, heading, ledgeGrabMinHeight * height, ledgeGrabMaxHeight * height,
                    controller.Radius + ledgeGrabReach * controller.HeightScale, out RaycastHit top, out Vector3 wallNormal, out Vector3 edge))
            {
                return false;
            }

            Vector3 hang = edge + wallNormal * (controller.Radius + 2f * controller.Skin);
            hang.y = edge.y - ledgeHangHeight * height;
            if (!controller.CanFitAt(hang, 1f)) { return false; }
            Vector3 climbTarget = edge - wallNormal * (controller.Radius + ledgeClimbInset * controller.HeightScale);
            climbTarget.y = edge.y + controller.Skin;

            // Kept in the ledge's space, so the character hangs on and climbs ledges that move.
            ledge.ledge = top.transform;
            MovementController.GetPhysicsPose(ledge.ledge, out Vector3 ledgePosition, out Quaternion ledgeRotation);
            Quaternion toLedgeSpace = Quaternion.Inverse(ledgeRotation);
            ledge.localHangPosition = toLedgeSpace * (hang - ledgePosition);
            ledge.localClimbTarget = toLedgeSpace * (climbTarget - ledgePosition);
            ledge.localEdge = toLedgeSpace * (edge - ledgePosition);
            ledge.localWallNormal = toLedgeSpace * wallNormal;
            ledge.phase = LedgeState.Phase.Hanging;
            if (controller.wallSlide.IsWallSliding) { EndWallSlide(controller); }
            // Hanging is done standing up: a character crouched in the air (walked off an edge crouched) stands to grab.
            controller.crouch.IsCrouched = false;
            controller.crouch.wantsToStand = false;
            return true;
        }

        /// <summary>
        /// Looks for a ledge in front of the character: a wall within reach (of its axis) at the bottom of a window of
        /// heights above its feet (low to high: for grabbing, the lowest and highest ledge heights), open space above it
        /// at the top of the window, and a walkable top between the two. Gives the top's hit, the wall's normal
        /// (flattened, pointing toward the character), and the point on the ledge's edge in front of the character.
        /// </summary>
        private bool FindLedge(MovementController controller, Vector3 feet, Vector3 heading, float low, float high, float reach,
            out RaycastHit top, out Vector3 wallNormal, out Vector3 edge)
        {
            top = default;
            wallNormal = Vector3.zero;
            edge = Vector3.zero;
            if (high <= low) { return false; }

            // A wall in front, low in the window, facing the character.
            if (!controller.Raycast(feet + Vector3.up * low, heading, reach, out RaycastHit wall)) { return false; }
            wallNormal = new Vector3(wall.normal.x, 0f, wall.normal.z);
            if (Mathf.Abs(wall.normal.y) > 0.3f || wallNormal.sqrMagnitude < 1e-6f) { return false; }
            wallNormal.Normalize();
            if (Vector3.Angle(heading, -wallNormal) > ledgeMaxGrabAngle) { return false; }

            // Open space above it at the top of the window, out to a little past the wall's face, so the ledge's top is
            // somewhere in between.
            float inset = Mathf.Min(0.1f * controller.HeightScale, controller.Radius);
            float depth = wall.distance + inset;
            Vector3 above = feet + Vector3.up * high;
            if (controller.Raycast(above, heading, depth, out _)) { return false; }

            // The ledge's top, straight down from there: walkable, within the window.
            if (!controller.Raycast(above + heading * depth, Vector3.down, high - low, out top)) { return false; }
            if (Vector3.Angle(top.normal, Vector3.up) > controller.MaxSlopeAngle) { return false; }

            edge = new Vector3(wall.point.x, top.point.y, wall.point.z);
            return true;
        }

        /// <summary>
        /// Starts climbing up onto the ledge, if there's room on top to stand, or else to crouch (the climb then ends
        /// crouched). Without room for either, the character keeps hanging.
        /// </summary>
        private void TryStartClimb(MovementData movementData, MovementController controller, Vector3 ledgePosition, Quaternion ledgeRotation)
        {
            LedgeState ledge = controller.ledge;
            Vector3 target = ledgePosition + ledgeRotation * ledge.localClimbTarget;
            // Where the character stands once up: dropped onto whatever is there, so a ledge that's the edge of rising
            // stairs (or any rise within a step) climbs onto them, resting on their edges as walking does. Their next
            // step leaves no room at the edge's own height, so without this the character could hang there but never
            // climb up.
            if (TryDropOntoLedge(controller, target, out Vector3 standing)) { target = standing; ledge.climbToCrouch = false; }
            else if (controller.CanFitAt(target, 1f)) { ledge.climbToCrouch = false; }
            else if (controller.CanFitAt(target, crouchHeight)) { ledge.climbToCrouch = true; }
            else { return; }
            ledge.localClimbTarget = Quaternion.Inverse(ledgeRotation) * (target - ledgePosition);

            ledge.phase = LedgeState.Phase.Climbing;
            ledge.climbElapsed = 0f;
            ledge.climbDuration = Mathf.Max(ledgeClimbDuration * controller.DurationScale, 1e-4f);
            // The climb starts from wherever the character is (it may not have settled into its hanging position yet).
            ledge.localHangPosition = Quaternion.Inverse(ledgeRotation) * (movementData.initialPosition - ledgePosition);
            if (ledge.climbToCrouch)
            {
                // Crouched from the start, so the collider fits under whatever is low over the ledge.
                controller.crouch.IsCrouched = true;
                controller.crouch.wantsToStand = true;
            }
        }

        /// <summary>
        /// Finds where the character would stand, standing up, on top of the ledge near target: its collider lowered from
        /// up to two steps' height above (clear of stairs' next step, as well as their first) onto the first thing under it, walkable (or the edge of a step, as stairs' are),
        /// with room to stand there.
        /// </summary>
        private static bool TryDropOntoLedge(MovementController controller, Vector3 target, out Vector3 standing)
        {
            standing = target;
            if (controller.HeightFraction < 1f) { return false; } // The sweep would be of a crouched collider.
            float stepHeight = controller.MaxStepHeight * controller.HeightScale;
            Vector3 from = target + Vector3.up * (2f * stepHeight);
            if (!controller.CanFitAt(from, 1f)) { return false; }
            if (!controller.SweepCapsule(from, Vector3.down, 2f * stepHeight + 2f * controller.Skin, out RaycastHit hit)) { return false; }
            standing = from + Vector3.down * Mathf.Max(0f, hit.distance - controller.Skin);
            return controller.CanFitAt(standing, 1f);
        }

        /// <summary>
        /// Climbs up onto the ledge: rising up the wall to the ledge's height, then moving forward onto it, over the
        /// climb's duration, without gravity. The character's collision still applies, so nothing is passed through.
        /// </summary>
        private bool Climb(ref MovementData movementData, MovementController controller, Vector3 ledgePosition, Quaternion ledgeRotation)
        {
            LedgeState ledge = controller.ledge;
            ledge.climbElapsed += movementData.deltaTime;
            float t = ledge.ClimbProgress;

            Vector3 start = ledgePosition + ledgeRotation * ledge.localHangPosition;
            Vector3 end = ledgePosition + ledgeRotation * ledge.localClimbTarget;
            Vector3 overEdge = new Vector3(start.x, end.y, start.z);
            Vector3 point = t < ClimbRiseShare
                ? Vector3.Lerp(start, overEdge, Mathf.SmoothStep(0f, 1f, t / ClimbRiseShare))
                : Vector3.Lerp(overEdge, end, Mathf.SmoothStep(0f, 1f, (t - ClimbRiseShare) / (1f - ClimbRiseShare)));

            movementData.velocityOverride = (point - movementData.initialPosition) / movementData.deltaTime;
            movementData.state |= CharacterState.LedgeClimbing;
            if (t >= 1f)
            {
                // On top: the character stands (or crouches) there from the next step, with no momentum left over.
                ledge.phase = LedgeState.Phase.None;
                ledge.ledge = null;
                movementData.velocityOverride = Vector3.zero;
                movementData.directMovementVector = point - movementData.initialPosition;
            }
            return true;
        }

        private void LetGoOfLedge(MovementController controller)
        {
            LedgeState ledge = controller.ledge;
            ledge.phase = LedgeState.Phase.None;
            ledge.ledge = null;
            ledge.regrabCooldown = ledgeRegrabDelay;
        }
    }
}
