using Level;
using UnityEngine;

namespace Character
{
    // Wall climbing: taking hold of a climbable surface by running or jumping into it, and climbing over it in any
    // direction, round corners onto the climbable surfaces it joins, but never off the climbable area.
    public partial class BasicMovementMode
    {
        [Header("Wall Climbing")]
        [Tooltip("How fast (m/s) the character climbs at full movement input, at its authored size (a surface can climb " +
                 "faster or slower: see ClimbableSurface). 0 turns climbing off")]
        [SerializeField] private float wallClimbSpeed = 2.5f;
        [Tooltip("How far (degrees) from heading straight into a climbable surface the character can be heading (steering, " +
                 "or moving) and still take hold of it on touching it: near 90, anything but sliding along it takes hold")]
        [Range(0f, 89f)] [SerializeField] private float wallClimbStartMaxAngle = 80f;
        [Tooltip("How far (m) from a climbable surface the character can be to take hold of it, at its authored size")]
        [SerializeField] private float wallClimbStartReach = 0.1f;
        [Tooltip("Heading toward a climbable surface at least this fast (m/s, at the character's authored size) takes hold " +
                 "of it whatever the steering (jumping or falling into it, say); slower, steering into it does")]
        [SerializeField] private float wallClimbMinApproachSpeed = 1f;
        [Tooltip("Where the character holds on, above its feet, as a share of its standing height: the climbable area is " +
                 "where this point can be against a climbable surface, so it's how far the body goes past the area's edges")]
        [Range(0.2f, 1f)] [SerializeField] private float wallClimbGripHeight = 0.6f;
        [Tooltip("How far (degrees) a surface can lean back from upright and still be climbed: one leaning back more is a " +
                 "top, climbed over onto (where it's walkable) rather than climbed on")]
        [Range(0f, 60f)] [SerializeField] private float wallClimbMaxLeanBack = 30f;
        [Tooltip("How far (degrees) a surface can lean out over the character (an overhang) and still be climbed")]
        [Range(0f, 60f)] [SerializeField] private float wallClimbMaxOverhang = 30f;
        [Tooltip("How fast (m/s, at the character's authored size) the character is drawn in against the surface it's " +
                 "holding, so it stays against it as it curves and goes round corners")]
        [SerializeField] private float wallClimbSnapSpeed = 3f;
        [Tooltip("How fast (m/s) jumping off a surface being climbed pushes the character away from it")]
        [SerializeField] private float wallClimbJumpAwaySpeed = 7f;
        [Tooltip("How fast (m/s) jumping off a surface being climbed launches the character upward")]
        [SerializeField] private float wallClimbJumpUpSpeed = 8f;
        [Tooltip("Seconds after letting go of a climbable surface (or jumping off it) before one can be taken hold of again")]
        [Min(0f)] [SerializeField] private float wallClimbRegrabDelay = 0.4f;
        [Tooltip("Seconds taking hold of a climbable surface from standing on the ground takes (stepping up onto it: the " +
                 "character holds still meanwhile), at its authored size. 0 = at once")]
        [Min(0f)] [SerializeField] private float wallClimbEnterDuration = 0.7f;
        [Tooltip("Seconds stepping off the bottom of a climbable surface onto the ground takes (the character holds still " +
                 "meanwhile), at its authored size. 0 = at once")]
        [Min(0f)] [SerializeField] private float wallClimbExitDuration = 0.7f;

        // How much further (m, at the authored size) than its holding distance the surface may be from the character's
        // grip for the character to climb there. Going round an outside corner, the grip swings round the corner's edge at
        // the holding distance; past the edge of a climbable area (a climbable panel on a wall), the nearest climbable
        // point stays at the area's edge, and moving away from it is stopped once it's this much further than holding.
        private const float WallClimbEdgeTolerance = 0.08f;

        // Something unclimbable nearer the grip than the climbable surface by more than this many skin widths ends the
        // climbable area there. Less than this is the character only touching it, as against the side wall of an inside
        // corner.
        private const float WallClimbUnclimbableMargin = 3f;

        // Going round an edge (the grip nearest an edge or corner of the climbable surface rather than a face of it), the
        // character only goes on round if nothing unclimbable is within this much (m, at the authored size) further than
        // the edge: round the corner of a climbable pillar, but not round the thin edge of a climbable panel set on a
        // plain wall, where the wall's face comes just after it. Without this, the character swung part way round the
        // panel's edge, turning toward the wall, before the wall stopped it.
        private const float WallClimbCornerClearance = 0.25f;

        // How far (m, at the authored size) from the character's grip paint still counts for holding on where it is (but
        // not for moving on: see ProbeClimbableSurface's callers).
        private const float WallClimbPaintTolerance = 0.05f;

        // A climbed surface moving further than this (m) in one physics step is treated as teleporting, and doesn't carry
        // the character.
        private const float WallClimbMaxCarryPerStep = 2f;

        // Reused for every climbing probe (several per physics step), so they don't allocate. Movement modes are shared
        // assets and run on the main thread only, one character at a time.
        private static readonly Collider[] climbOverlaps = new Collider[16];

        float WallClimbMode.wallClimbSpeed { get => wallClimbSpeed; set => wallClimbSpeed = value; }

        /// <summary>
        /// The surface nearest the character's grip, if it's climbable (all of it, or painted climbable there): the collider, the point on it nearest the grip, its
        /// normal there (toward the grip; round an edge or corner, it points from the edge), and how far it is. Also how
        /// far the nearest unclimbable surface is (infinity if there's none in reach).
        /// </summary>
        private struct ClimbContact
        {
            public Collider collider;
            // The climbing speed multiplier there (see ClimbableSurface and PaintedClimbableSurface)
            public float speedMultiplier;
            public Vector3 point;
            public Vector3 normal;
            public float distance;
            public float unclimbableDistance;
            // Whether the point is on an edge or corner of the surface, not a face (the normal points from the edge)
            public bool atEdge;
            // The ladder it's on, if it's one's (its front face, on its middle at the grip's height); null otherwise
            public Ladder ladder;
        }

        /// <summary>
        /// Takes hold of a climbable surface when the character touches one heading toward it (steering into it, or moving
        /// into it fast enough, within the start angle of head-on), on the ground or in mid-air, and carries the climb
        /// on: movement input moves the character over the surface (up and down with the stick's up and down, sideways
        /// with its left and right, as seen from the camera), facing it and without gravity, round outside corners
        /// and into inside ones onto whatever climbable surface it meets, and along with the surface if it moves. The
        /// climbable area holds it: it stops where its grip would leave a climbable surface (its edge, or something
        /// unclimbable round it), and at a top too shallow to climb it climbs over onto it, if it's walkable and there's
        /// room (as from a ledge). Jump kicks off the surface, up and away from it; Crouch lets go, as does pressing down
        /// at the bottom with the feet on the ground, or the surface going.
        /// </summary>
        public bool ProcessMovementDataForWallClimb(ref MovementData movementData, Inputs inputs, MovementController controller)
        {
            WallClimbState climb = controller.wallClimb;
            if (!climb.IsClimbing && !TryStartWallClimb(ref movementData, inputs, controller)) { return false; }

            // Along with the surface, if it moves: every probe is made from where that takes the character.
            Vector3 carry = WallClimbCarry(movementData, controller);
            Vector3 feet = movementData.initialPosition + carry;
            float reach = WallClimbReach(controller);
            // Where the character is now, paint just beside its grip counts too: collision can nudge it a hair off the
            // painted cells it climbed onto, and that mustn't drop it.
            if (!ProbeClimbableSurface(controller, feet, climb.normal, reach, out ClimbContact here, WallClimbPaintTolerance * controller.HeightScale))
            {
                EndWallClimb(controller); // Nothing climbable within reach any more: it's gone, or moved away.
                return false;
            }
            HoldSurface(climb, here);
            climb.elapsed += movementData.deltaTime;
            Vector3 normal = climb.normal;

            // Stepping off the bottom onto the ground: held still until it's done, then on the ground.
            if (climb.IsExiting)
            {
                climb.stepElapsed += movementData.deltaTime;
                if (climb.stepElapsed >= climb.stepDuration)
                {
                    LetGoOfWall(controller, normal);
                    return false;
                }
                HoldStill(ref movementData, controller, here, carry);
                return true;
            }

            if (controller.crouch.press.Consume(inputs.crouch, crouchInputBuffer))
            {
                LetGoOfWall(controller, normal);
                return false;
            }
            if (IsNewJumpPress(inputs, controller))
            {
                WallClimbJump(ref movementData, controller, normal);
                return true;
            }

            // Stepping onto the surface from the ground: held still until it's done, then climbing.
            if (climb.IsEntering)
            {
                climb.stepElapsed += movementData.deltaTime;
                if (climb.stepElapsed < climb.stepDuration)
                {
                    HoldStill(ref movementData, controller, here, carry);
                    return true;
                }
                climb.phase = WallClimbState.Phase.Climbing;
            }

            ClimbAxes(normal, out Vector3 right, out Vector3 up);
            Vector2 input = ClimbInput(movementData, inputs, right, up);
            // A ladder is climbed only up and down.
            if (here.ladder != null) { input.x = 0f; }
            // At the bottom, standing on the ground, pressing down steps off the surface onto it.
            if (movementData.initialGrounded && input.y < -0.5f)
            {
                float exit = wallClimbExitDuration * controller.DurationScale;
                if (exit <= 0f)
                {
                    LetGoOfWall(controller, normal);
                    return false;
                }
                climb.phase = WallClimbState.Phase.Exiting;
                climb.stepElapsed = 0f;
                climb.stepDuration = exit;
                HoldStill(ref movementData, controller, here, carry);
                return true;
            }

            // Pushing up with a walkable top within reach of the hands, the character climbs up onto it, as from a ledge.
            // Off a ladder's top only onto a floor there: a ladder standing on its own isn't climbed over.
            if (input.y > 0.5f && TryClimbOverTop(ref movementData, controller, -FlatNormal(normal, movementData), withinReach: true, requireFloor: here.ladder != null))
            {
                EndWallClimb(controller);
                return true;
            }

            float step = wallClimbSpeed * controller.SpeedScale * climb.speedMultiplier * movementData.deltaTime;
            Vector3 vertical = up * (input.y * step), sideways = right * (input.x * step);
            bool climbsVertically = Mathf.Abs(input.y) > 1e-4f;
            ClimbContact upOrDown = here;
            bool canClimbVertically = climbsVertically && CanClimbTo(controller, feet + vertical, normal, reach, here, out upOrDown);
            // Pushing up at a top too shallow to climb (or the area's top edge) climbs over it onto it, if it's walkable.
            if (climbsVertically && !canClimbVertically && input.y > 0.5f
                && TryClimbOverTop(ref movementData, controller, -FlatNormal(normal, movementData), withinReach: false, requireFloor: here.ladder != null))
            {
                EndWallClimb(controller);
                return true;
            }

            // The whole move if the grip stays on the climbable area, or else as much of it, up or down or sideways, as does.
            Vector3 move = Vector3.zero;
            ClimbContact there = here;
            bool climbsSideways = Mathf.Abs(input.x) > 1e-4f;
            if (climbsVertically && climbsSideways && CanClimbTo(controller, feet + vertical + sideways, normal, reach, here, out ClimbContact both))
            {
                move = vertical + sideways;
                there = both;
            }
            else if (canClimbVertically)
            {
                move = vertical;
                there = upOrDown;
            }
            else if (climbsSideways && CanClimbTo(controller, feet + sideways, normal, reach, here, out ClimbContact across))
            {
                move = sideways;
                there = across;
            }

            // Drawn in (or eased out) to the holding distance from the surface where the move ends, along its normal
            // there: round an outside corner, this swings the character round the edge.
            float snap = wallClimbSnapSpeed * controller.SpeedScale * movementData.deltaTime;
            Vector3 hold = -there.normal * Mathf.Clamp(there.distance - WallClimbHoldDistance(controller), -snap, snap)
                           + LadderCentring(there.ladder, feet + move, snap);

            if (CanHoldAt(controller, there, here.distance)) { HoldSurface(climb, there); }
            movementData.velocityOverride = Vector3.zero;
            movementData.directMovementVector = carry + move + hold;
            movementData.targetRotation = Quaternion.LookRotation(-FlatNormal(climb.normal, movementData), Vector3.up);
            movementData.state |= CharacterState.WallClimbing;
            if (move.sqrMagnitude > 1e-12f) { movementData.state |= CharacterState.Moving; }
            return true;
        }

        /// <summary>
        /// Takes hold of a climbable surface if the character is touching one (its grip within the start reach of it),
        /// steep enough to climb, and heading into it: steering into it, or moving into it fast enough, within the start
        /// angle of head-on. Not while crouched, nor during another ability, other than a wall run or a slide down a wall,
        /// which a climbable surface is climbed instead.
        /// </summary>
        private bool TryStartWallClimb(ref MovementData movementData, Inputs inputs, MovementController controller)
        {
            WallClimbState climb = controller.wallClimb;
            if (wallClimbSpeed <= 0f || climb.regrabCooldown > 0f || controller.crouch.IsCrouched) { return false; }
            if (controller.dash.IsDashing || controller.ledge.IsActive || controller.vault.IsActive || controller.grab.IsGrabbing
                || controller.roll.IsRolling || controller.slide.IsSliding)
            {
                return false;
            }

            // Which way the character is heading: as steered, or else as it's moving (or was, as it hit the surface).
            Vector3 steer = InputDirection(movementData, inputs);
            Vector3 approach = movementData.initialGrounded ? Vector3.zero : Approach(movementData, controller.wallSlide, controller);
            Vector3 heading = steer.sqrMagnitude > 0.01f ? steer.normalized
                : approach.sqrMagnitude > 0.01f ? approach.normalized : Facing(movementData);

            Vector3 feet = movementData.initialPosition;
            float reach = controller.Radius + wallClimbStartReach * controller.HeightScale + controller.Skin;
            if (!ProbeClimbableSurface(controller, feet, -heading, reach, out ClimbContact contact)) { return false; }
            if (!SteepEnoughToClimb(contact.normal) || IsUnclimbableNearer(controller, contact)) { return false; }

            Vector3 into = -FlatNormal(contact.normal, movementData);
            bool steeringIn = steer.magnitude >= 0.3f && Vector3.Angle(steer, into) <= wallClimbStartMaxAngle;
            bool movingIn = approach.magnitude >= wallClimbMinApproachSpeed * controller.SpeedScale
                            && Vector3.Angle(approach, into) <= wallClimbStartMaxAngle;
            if (!steeringIn && !movingIn) { return false; }

            if (controller.wallRun.IsWallRunning) { EndWallRun(controller); }
            if (controller.wallSlide.IsWallSliding) { EndWallSlide(controller); }
            climb.IsClimbing = true;
            climb.elapsed = 0f;
            // Taking hold from standing on the ground, the character steps up onto the surface first.
            float enter = wallClimbEnterDuration * controller.DurationScale;
            climb.phase = movementData.initialGrounded && enter > 0f ? WallClimbState.Phase.Entering : WallClimbState.Phase.Climbing;
            climb.stepElapsed = 0f;
            climb.stepDuration = enter;
            // Holding onto a climbable surface counts as touching the ground: the character's jumps and air dashes come back.
            GiveBackAirMoves(controller);
            climb.hasLocalFeet = false;
            HoldSurface(climb, contact);
            return true;
        }

        /// <summary>Holds the character where it is on the surface this step (moving with it, and kept at the holding distance).</summary>
        private void HoldStill(ref MovementData movementData, MovementController controller, ClimbContact here, Vector3 carry)
        {
            float snap = wallClimbSnapSpeed * controller.SpeedScale * movementData.deltaTime;
            Vector3 hold = -here.normal * Mathf.Clamp(here.distance - WallClimbHoldDistance(controller), -snap, snap)
                           + LadderCentring(here.ladder, movementData.initialPosition + carry, snap);
            movementData.velocityOverride = Vector3.zero;
            movementData.directMovementVector = carry + hold;
            movementData.targetRotation = Quaternion.LookRotation(-FlatNormal(controller.wallClimb.normal, movementData), Vector3.up);
            movementData.state |= CharacterState.WallClimbing;
        }

        /// <summary>
        /// Finds the surface nearest the character's grip (with its feet at feet), within reach, among the climbable
        /// ones, and how far the nearest unclimbable one is. hintNormal is the way the surface is expected to face, for
        /// colliders whose nearest point can't be found directly (see MovementController.ClosestPointOn). False if
        /// nothing climbable is within reach.
        /// </summary>
        private bool ProbeClimbableSurface(MovementController controller, Vector3 feet, Vector3 hintNormal, float reach,
            out ClimbContact contact, float paintTolerance = 0f)
        {
            contact = new ClimbContact { distance = float.PositiveInfinity, unclimbableDistance = float.PositiveInfinity };
            Vector3 grip = feet + Vector3.up * (wallClimbGripHeight * controller.StandingHeight);
            int count = controller.OverlapSphere(grip, reach, climbOverlaps);
            float bestScore = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                Collider candidate = climbOverlaps[i];
                Ladder ladder = Ladder.Of(candidate);
                Vector3 point, normal;
                float distance, speedMultiplier = 1f;
                bool atEdge = false;
                if (ladder != null && OnLadder(controller, ladder, grip, out point, out distance))
                {
                    // A ladder is climbed on its front face, on its middle: always square on to it.
                    normal = ladder.Forward;
                }
                else
                {
                    ladder = null;
                    if (!MovementController.ClosestPointOn(candidate, grip, -hintNormal, reach, out point)) { continue; }
                    distance = Vector3.Distance(grip, point);
                    normal = (grip - point) / Mathf.Max(distance, 1e-6f);
                    atEdge = IsAtEdge(candidate, grip, normal, distance, reach);
                    // A collider's nearest point decides it: where that isn't climbable (an unpainted part of a painted
                    // one, a ladder from behind or past its ends), the collider is as unclimbable there as a plain wall.
                    if (Ladder.Of(candidate) != null
                        || !ClimbableSurface.IsClimbableAt(candidate, point, normal, atEdge, paintTolerance, out speedMultiplier))
                    {
                        contact.unclimbableDistance = Mathf.Min(contact.unclimbableDistance, distance);
                        continue;
                    }
                }
                // The surface held (facing as hintNormal) wins a near tie: in an inside corner, both walls are at the
                // holding distance, and without this the character kept turning back to the first wall it held, never
                // moving on along the other.
                float score = Vector3.Dot(normal, hintNormal) > 0.95f ? distance - controller.Skin : distance;
                if (score >= bestScore) { continue; }
                bestScore = score;
                contact.collider = candidate;
                contact.speedMultiplier = speedMultiplier;
                contact.point = point;
                contact.distance = distance;
                contact.normal = normal;
                contact.atEdge = atEdge;
                contact.ladder = ladder;
            }
            return contact.collider != null;
        }

        /// <summary>
        /// Whether a grip is on a ladder: in front of its front face (it's only climbed from the front), between its
        /// bottom and top, and no further to its side than the character's radius past its edge. Gives the point on its
        /// front face, on its middle at the grip's height, and how far in front of the face the grip is.
        /// </summary>
        private static bool OnLadder(MovementController controller, Ladder ladder, Vector3 grip, out Vector3 point, out float distance)
        {
            float up = ladder.HeightOf(grip);
            distance = ladder.InFrontOf(grip);
            point = ladder.FrontBottom + ladder.Up * up;
            return distance > -controller.Skin && up >= 0f && up <= ladder.Height
                   && Mathf.Abs(ladder.SideOf(grip)) <= 0.5f * ladder.Width + controller.Radius;
        }

        /// <summary>On a ladder, the move this step back toward its middle (at most step long); zero off one.</summary>
        private static Vector3 LadderCentring(Ladder ladder, Vector3 feet, float step)
        {
            if (ladder == null) { return Vector3.zero; }
            return -ladder.Right * Mathf.Clamp(ladder.SideOf(feet), -step, step);
        }

        /// <summary>
        /// Whether the point on a collider nearest the grip is on an edge or corner of it rather than a face: on a face, a
        /// ray from the grip back along the normal meets the surface square on; round an edge it meets one of the faces
        /// either side, at an angle, or misses.
        /// </summary>
        private static bool IsAtEdge(Collider collider, Vector3 grip, Vector3 normal, float distance, float reach) =>
            !collider.Raycast(new Ray(grip, -normal), out RaycastHit square, distance + reach) || Vector3.Dot(square.normal, normal) < 0.996f;

        /// <summary>
        /// Whether the character, with its feet at feet, would still be on the climbable area (see CanHoldAt), its grip
        /// no further from the surface than from (where it is now) or the edge tolerance allows.
        /// </summary>
        private bool CanClimbTo(MovementController controller, Vector3 feet, Vector3 hintNormal, float reach, ClimbContact from, out ClimbContact contact) =>
            ProbeClimbableSurface(controller, feet, hintNormal, reach, out contact) && CanHoldAt(controller, contact, from.distance);

        /// <summary>
        /// Whether a contact is one the character can climb on: steep enough, with its grip near enough the surface
        /// (within the edge tolerance of its holding distance, or no further than allowedDistance: having just taken
        /// hold from a little way off, it's drawn in as it climbs), and nothing unclimbable nearer.
        /// </summary>
        private bool CanHoldAt(MovementController controller, ClimbContact contact, float allowedDistance = 0f) =>
            contact.collider != null && SteepEnoughToClimb(contact.normal)
            && contact.distance <= Mathf.Max(WallClimbHoldDistance(controller) + WallClimbEdgeTolerance * controller.HeightScale, allowedDistance + 1e-4f)
            && !IsUnclimbableNearer(controller, contact);

        /// <summary>
        /// Whether something unclimbable ends the climbable area at a contact: nearer than the climbable surface, or round
        /// an edge, close beyond it (see WallClimbCornerClearance).
        /// </summary>
        private static bool IsUnclimbableNearer(MovementController controller, ClimbContact contact) =>
            contact.unclimbableDistance < (contact.atEdge
                ? contact.distance + WallClimbCornerClearance * controller.HeightScale
                : contact.distance - WallClimbUnclimbableMargin * controller.Skin);

        /// <summary>Whether a surface with this normal is steep enough to climb: within the lean back and overhang of upright.</summary>
        private bool SteepEnoughToClimb(Vector3 normal) =>
            normal.y <= Mathf.Sin(wallClimbMaxLeanBack * Mathf.Deg2Rad) && normal.y >= -Mathf.Sin(wallClimbMaxOverhang * Mathf.Deg2Rad);

        /// <summary>How far the character's grip is held from the surface: its collider just clear of it.</summary>
        private static float WallClimbHoldDistance(MovementController controller) => controller.Radius + controller.Skin;

        /// <summary>How far from the grip a surface being climbed is looked for.</summary>
        private float WallClimbReach(MovementController controller) =>
            controller.Radius + Mathf.Max(wallClimbStartReach, 2f * WallClimbEdgeTolerance) * controller.HeightScale + 2f * controller.Skin;

        /// <summary>Remembers the surface held at a contact.</summary>
        private static void HoldSurface(WallClimbState climb, ClimbContact contact)
        {
            climb.normal = contact.normal;
            climb.point = contact.point;
            climb.speedMultiplier = contact.speedMultiplier;
            climb.ladder = contact.ladder;
            Transform surface = contact.collider.attachedRigidbody != null ? contact.collider.attachedRigidbody.transform : contact.collider.transform;
            if (surface != climb.surface) { climb.hasLocalFeet = false; }
            climb.surface = surface;
        }

        /// <summary>
        /// The directions to climb in on a surface with this normal: right (horizontal, as the character faces it) and up
        /// it (straight up a wall; up and in, or up and out, on one leaning back or over).
        /// </summary>
        private static void ClimbAxes(Vector3 normal, out Vector3 right, out Vector3 up)
        {
            right = Vector3.Cross(Vector3.up, -normal);
            right = right.sqrMagnitude > 1e-6f ? right.normalized : Vector3.right;
            up = Vector3.Cross(-normal, right).normalized;
        }

        /// <summary>
        /// The movement input as a direction over the surface (x along right, y along up; no longer than 1): the stick as
        /// seen on the screen, so up on the stick climbs up and right goes right, whichever way the camera looks at the
        /// surface. With the camera looking along the surface, where the screen's directions don't lie on it, the
        /// stick's own directions are used.
        /// </summary>
        private static Vector2 ClimbInput(MovementData movementData, Inputs inputs, Vector3 right, Vector3 up)
        {
            Vector2 stick = new Vector2(inputs.movementVector.x, inputs.movementVector.z);
            float amount = Mathf.Clamp01(stick.magnitude);
            if (amount < 0.05f) { return Vector2.zero; }
            Quaternion look = movementData.cameraLook;
            Vector3 onScreen = look * Vector3.right * stick.x + look * Vector3.up * stick.y;
            Vector2 onSurface = new Vector2(Vector3.Dot(onScreen, right), Vector3.Dot(onScreen, up));
            if (onSurface.magnitude < 0.25f * amount) { onSurface = stick; }
            return onSurface.normalized * amount;
        }

        /// <summary>A surface's normal, flattened onto the ground (unit length; the character's facing reversed if it's flat).</summary>
        private static Vector3 FlatNormal(Vector3 normal, MovementData movementData)
        {
            Vector3 flat = new Vector3(normal.x, 0f, normal.z);
            return flat.sqrMagnitude > 1e-6f ? flat.normalized : -Facing(movementData);
        }

        /// <summary>How far the surface being climbed has moved the character since the last step (zero if it's still).</summary>
        private static Vector3 WallClimbCarry(MovementData movementData, MovementController controller)
        {
            WallClimbState climb = controller.wallClimb;
            if (!climb.hasLocalFeet || climb.surface == null) { return Vector3.zero; }
            MovementController.GetPhysicsPose(climb.surface, out Vector3 position, out Quaternion rotation);
            Vector3 carry = position + rotation * climb.localFeet - movementData.initialPosition;
            // Static surfaces still give a tiny carry from floating point error, and a teleport shouldn't drag the character.
            if (carry.sqrMagnitude < 1e-10f || carry.sqrMagnitude > WallClimbMaxCarryPerStep * WallClimbMaxCarryPerStep) { return Vector3.zero; }
            return carry;
        }

        /// <summary>
        /// Remembers where the character's feet are in the space of the surface it's climbing, once the step's movement is
        /// resolved, to move along with it next step.
        /// </summary>
        private static void AfterWallClimbResolved(MovementData movementData, MovementController controller)
        {
            WallClimbState climb = controller.wallClimb;
            if (!climb.IsClimbing || climb.surface == null || !movementData.state.HasFlag(CharacterState.WallClimbing))
            {
                climb.hasLocalFeet = false;
                return;
            }
            MovementController.GetPhysicsPose(climb.surface, out Vector3 position, out Quaternion rotation);
            Vector3 feet = movementData.initialPosition + movementData.finalMovementVector;
            climb.localFeet = Quaternion.Inverse(rotation) * (feet - position);
            climb.hasLocalFeet = true;
        }

        /// <summary>
        /// Kicks off the surface: upward, and away from it. Gives back the character's jumps, less the one this uses, and
        /// takes away air steering for a moment, as jumping off a wall run does.
        /// </summary>
        private void WallClimbJump(ref MovementData movementData, MovementController controller, Vector3 normal)
        {
            EndWallClimb(controller);
            Vector3 away = FlatNormal(normal, movementData);
            controller.jumpPressConsumed = true;
            controller.ResetJumps();
            controller.TryUseJump();
            RememberWallJumpedOff(controller, away);
            StartWallJumpSteeringRecovery(controller);
            movementData.velocityOverride = away * (wallClimbJumpAwaySpeed * controller.SpeedScale) + Vector3.up * (wallClimbJumpUpSpeed * controller.SpeedScale);
            movementData.directMovementVector = Vector3.zero;
            movementData.targetRotation = Quaternion.LookRotation(away, Vector3.up);
            movementData.events |= MovementEvents.WallJumped;
            movementData.state |= CharacterState.Moving;
        }

        /// <summary>
        /// Lets go of the surface (with Crouch, or stepping off it at the bottom): the character drops, and doesn't slide
        /// down the same wall before it lands.
        /// </summary>
        private void LetGoOfWall(MovementController controller, Vector3 normal)
        {
            EndWallClimb(controller);
            Vector3 flat = new Vector3(normal.x, 0f, normal.z);
            if (flat.sqrMagnitude < 1e-6f) { return; }
            controller.wallSlide.lastWallNormal = flat.normalized;
            controller.wallSlide.hasLastWall = true;
        }

        private void EndWallClimb(MovementController controller)
        {
            WallClimbState climb = controller.wallClimb;
            climb.IsClimbing = false;
            climb.phase = WallClimbState.Phase.Climbing;
            climb.regrabCooldown = wallClimbRegrabDelay;
            climb.surface = null;
            climb.ladder = null;
            climb.hasLocalFeet = false;
        }
    }
}
