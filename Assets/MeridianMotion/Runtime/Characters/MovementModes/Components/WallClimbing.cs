using System;
using MeridianMotion.Levels;
using MeridianMotion.PhysicsQueries;
using UnityEngine;
using static MeridianMotion.Characters.MovementRules;

namespace MeridianMotion.Characters
{
    /// <summary>
    /// Wall climbing: taking hold of a climbable surface (see MeridianMotion.Levels.ClimbableSurface and MeridianMotion.Levels.ClimbableLadder) by running or
    /// jumping into it, and climbing over it in any direction, round corners onto the climbable surfaces it joins, but
    /// never off the climbable area.
    /// </summary>
    [Serializable]
    public partial class WallClimbing : MovementComponent
    {
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

        /// <summary>How fast (m/s) the character climbs at full input, at its authored size. 0 turns climbing off.</summary>
        public float WallClimbSpeed { get => wallClimbSpeed; set => wallClimbSpeed = value; }

        /// <summary>The events of a moment it raises (see MovementComponent.OneOffEvents): a jump off the wall (MovementRules.JumpOffWall).</summary>
        public override MovementEvents OneOffEvents => MovementEvents.WallJumped;
        public override bool IsActive(MMotionMotor motor) => motor.wallClimb.IsClimbing;
        /// <summary>Climbing, the character holds on to the surface, which holds it up.</summary>
        public override AbilityTraits Traits => AbilityTraits.Supported | AbilityTraits.Attached;
        /// <summary>The events of WallClimbing starting and ending (see MovementComponent.StateEvents).</summary>
        public override StateEventPair[] StateEvents => stateEvents;
        private static readonly StateEventPair[] stateEvents =
        {
            new(CharacterState.WallClimbing, MovementEvents.WallClimbStarted, MovementEvents.WallClimbEnded),
        };

        public override void AdvanceTimers(MovementStep step, StepInputs inputs, MMotionMotor motor)
        {
            motor.wallClimb.regrabCooldown = MovementRules.CountDown(motor.wallClimb.regrabCooldown, step.deltaTime);
        }

        public override void Interrupt(MovementStep step, MMotionMotor motor)
        {
            if (motor.wallClimb.IsClimbing) { EndWallClimb(motor); }
        }

        /// <summary>
        /// Takes hold of a climbable surface when the character touches one heading toward it (steering into it, or moving
        /// into it fast enough, within the start angle of head-on), on the ground or in mid-air, and carries the climb
        /// on: movement input moves the character over the surface (up and down with the stick's up and down, sideways
        /// with its left and right, as seen along the input's look: the player's view), facing it and without gravity, round outside corners
        /// and into inside ones onto whatever climbable surface it meets, and along with the surface if it moves. The
        /// climbable area holds it: it stops where its grip would leave a climbable surface (its edge, or something
        /// unclimbable round it), and at a top too shallow to climb it climbs over onto it, if it's walkable and there's
        /// room (as from a ledge). Jump kicks off the surface, up and away from it; Crouch lets go, as does pressing down
        /// at the bottom with the feet on the ground, or the surface going.
        /// </summary>
        public override bool TakeOver(MovementStep step, StepInputs inputs, MMotionMotor motor)
        {
            WallClimbState climb = motor.wallClimb;
            if (!climb.IsClimbing && !TryStartWallClimb(step, inputs, motor)) { return false; }

            // Along with the surface, if it moves: every probe is made from where that takes the character.
            Vector3 carry = WallClimbCarry(step, motor);
            Vector3 feet = step.initialPosition + carry;
            // Still closing in from where it took hold, it reaches as far as the surface was last step: taking hold lying
            // down underwater, its body reached far ahead of its feet, and climbing stands it up, its front then well
            // short of the surface. It let go at once, before it could close in (see closingIn).
            float reach = Mathf.Max(WallClimbReach(motor), climb.closingIn + motor.Skin);
            // Where the character is now, paint just beside its grip counts too: collision can nudge it a hair off the
            // painted cells it climbed onto, and that mustn't drop it.
            if (!ProbeClimbableSurface(motor, feet, climb.normal, reach, out ClimbContact here, WallClimbPaintTolerance * motor.HeightScale))
            {
                EndWallClimb(motor); // Nothing climbable within reach any more: it's gone, or moved away.
                return false;
            }
            // No further than it's been: it only ever closes in, so a surface moving away is let go of as before.
            climb.closingIn = Mathf.Min(climb.closingIn, here.distance);
            HoldSurface(climb, here);
            climb.elapsed += step.deltaTime;
            Vector3 normal = climb.normal;

            // Stepping off the bottom onto the ground: held still until it's done, then on the ground.
            // The time is checked before this step is added to it, so the character holds still for exactly the exit
            // (and below, the enter) time, counting the step it began on.
            if (climb.IsExiting)
            {
                if (MovementRules.HasElapsed(climb.stepElapsed, climb.stepDuration, step.deltaTime))
                {
                    LetGoOfWall(motor, normal);
                    return false;
                }
                climb.stepElapsed += step.deltaTime;
                HoldStill(step, motor, here, carry);
                return true;
            }

            if (Mode.ConsumeCrouchPress(inputs, motor))
            {
                LetGoOfWall(motor, normal);
                return false;
            }
            if (Mode.HasFreshJumpPress(inputs, motor))
            {
                WallClimbJump(step, motor, normal);
                return true;
            }

            // Stepping onto the surface from the ground: held still until it's done, then climbing.
            if (climb.IsEntering)
            {
                if (!MovementRules.HasElapsed(climb.stepElapsed, climb.stepDuration, step.deltaTime))
                {
                    climb.stepElapsed += step.deltaTime;
                    HoldStill(step, motor, here, carry);
                    return true;
                }
                climb.phase = WallClimbState.Phase.Climbing;
            }

            ClimbAxes(normal, out Vector3 right, out Vector3 up);
            Vector2 input = ClimbInput(step, inputs, right, up);
            // A ladder is climbed only up and down.
            if (here.ladder != null) { input.x = 0f; }
            // At the bottom, standing on the ground, pressing down steps off the surface onto it.
            if (step.initialGrounded && input.y < -0.5f)
            {
                float exit = wallClimbExitDuration * motor.DurationScale;
                if (exit <= 0f)
                {
                    LetGoOfWall(motor, normal);
                    return false;
                }
                climb.phase = WallClimbState.Phase.Exiting;
                climb.stepElapsed = 0f;
                climb.stepDuration = exit;
                HoldStill(step, motor, here, carry);
                return true;
            }

            // Pushing up with a walkable top within reach of the hands, the character climbs up onto it, as from a ledge
            // (handed to the ledge climb: without ledges, there's no climbing over the top). Off a ladder's top only onto
            // a floor there: a ladder standing on its own isn't climbed over.
            Ledges ledges = Mode.Get<Ledges>();
            if (input.y > 0.5f && ledges != null && ledges.TryClimbOverTop(step, motor, -FlatNormal(normal, step), withinReach: true, requireFloor: here.ladder != null))
            {
                EndWallClimb(motor);
                return true;
            }

            float distance = wallClimbSpeed * motor.SpeedScale * climb.speedMultiplier * step.deltaTime;
            Vector3 vertical = up * (input.y * distance), sideways = right * (input.x * distance);
            bool climbsVertically = Mathf.Abs(input.y) > 1e-4f;
            ClimbContact upOrDown = here;
            bool canClimbVertically = climbsVertically && CanClimbTo(motor, feet + vertical, normal, reach, here, out upOrDown);
            // Pushing up at a top too shallow to climb (or the area's top edge) climbs over it onto it, if it's walkable.
            if (climbsVertically && !canClimbVertically && input.y > 0.5f && ledges != null
                && ledges.TryClimbOverTop(step, motor, -FlatNormal(normal, step), withinReach: false, requireFloor: here.ladder != null))
            {
                EndWallClimb(motor);
                return true;
            }

            // The whole move if the grip stays on the climbable area, or else as much of it, up or down or sideways, as does.
            Vector3 move = Vector3.zero;
            ClimbContact there = here;
            bool climbsSideways = Mathf.Abs(input.x) > 1e-4f;
            if (climbsVertically && climbsSideways && CanClimbTo(motor, feet + vertical + sideways, normal, reach, here, out ClimbContact both))
            {
                move = vertical + sideways;
                there = both;
            }
            else if (canClimbVertically)
            {
                move = vertical;
                there = upOrDown;
            }
            else if (climbsSideways && CanClimbTo(motor, feet + sideways, normal, reach, here, out ClimbContact across))
            {
                move = sideways;
                there = across;
            }

            // Drawn in (or eased out) to the holding distance from the surface where the move ends, along its normal
            // there: round an outside corner, this swings the character round the edge.
            float snap = wallClimbSnapSpeed * motor.SpeedScale * step.deltaTime;
            Vector3 hold = -there.normal * Mathf.Clamp(there.distance - WallClimbHoldDistance(motor), -snap, snap)
                           + LadderCentring(there.ladder, feet + move, snap);

            if (CanHoldAt(motor, there, here.distance)) { HoldSurface(climb, there); }
            step.velocityOverride = Vector3.zero;
            step.directMovementVector = carry + move + hold;
            step.targetRotation = Quaternion.LookRotation(-FlatNormal(climb.normal, step), Vector3.up);
            step.state |= CharacterState.WallClimbing;
            if (move.sqrMagnitude > 1e-12f) { step.state |= CharacterState.Moving; }
            return true;
        }

        /// <summary>
        /// Takes hold of a climbable surface if the character is touching one (its grip within the start reach of it),
        /// steep enough to climb, and heading into it: steering into it, or moving into it fast enough, within the start
        /// angle of head-on. Not while crouched, nor during another ability, other than a wall run or a slide down a wall,
        /// which a climbable surface is climbed instead.
        /// </summary>
        private bool TryStartWallClimb(MovementStep step, StepInputs inputs, MMotionMotor motor)
        {
            WallClimbState climb = motor.wallClimb;
            if (wallClimbSpeed <= 0f || climb.regrabCooldown > 0f || motor.crouch.IsCrouched) { return false; }
            // A swimmer takes hold too: climbing out of the water (Swimming comes later in the list, so it's under way here).
            if (Mode.AbilityInProgressExcept<WallRunning, WallSliding, Swimming>(motor)) { return false; }

            // Which way the character is heading: as steered, or else as it's moving (or was, as it hit the surface).
            Vector3 steer = InputDirection(step, inputs);
            Vector3 approach = step.initialGrounded ? Vector3.zero : WallSliding.Approach(step, motor.wallSlide, motor);
            Vector3 heading = steer.sqrMagnitude > 0.01f ? steer.normalized
                : NormalizedOr(approach, Facing(step), 0.01f);

            Vector3 feet = step.initialPosition;
            float reach = motor.Body.FrontExtent + wallClimbStartReach * motor.HeightScale + motor.Skin;
            if (!ProbeClimbableSurface(motor, feet, -heading, reach, out ClimbContact contact)) { return false; }
            if (!SteepEnoughToClimb(contact.normal) || IsUnclimbableNearer(motor, contact)) { return false; }

            Vector3 into = -FlatNormal(contact.normal, step);
            bool steeringIn = steer.magnitude >= 0.3f && Vector3.Angle(steer, into) <= wallClimbStartMaxAngle;
            bool movingIn = approach.magnitude >= wallClimbMinApproachSpeed * motor.SpeedScale
                            && Vector3.Angle(approach, into) <= wallClimbStartMaxAngle;
            if (!steeringIn && !movingIn) { return false; }

            Mode.InterruptOthers(step, motor, this); // A wall run or a slide down a wall, climbed instead.
            climb.IsClimbing = true;
            climb.elapsed = 0f;
            // Taking hold from standing on the ground, the character steps up onto the surface first.
            float enter = wallClimbEnterDuration * motor.DurationScale;
            climb.phase = step.initialGrounded && enter > 0f ? WallClimbState.Phase.Entering : WallClimbState.Phase.Climbing;
            climb.stepElapsed = 0f;
            climb.stepDuration = enter;
            // Holding onto a climbable surface counts as touching the ground: the character's jumps and air dashes come back.
            GiveBackAirMoves(motor);
            climb.hasLocalFeet = false;
            climb.closingIn = contact.distance;
            HoldSurface(climb, contact);
            return true;
        }

        /// <summary>Holds the character where it is on the surface this step (moving with it, and kept at the holding distance).</summary>
        private void HoldStill(MovementStep step, MMotionMotor motor, ClimbContact here, Vector3 carry)
        {
            float snap = wallClimbSnapSpeed * motor.SpeedScale * step.deltaTime;
            Vector3 hold = -here.normal * Mathf.Clamp(here.distance - WallClimbHoldDistance(motor), -snap, snap)
                           + LadderCentring(here.ladder, step.initialPosition + carry, snap);
            step.velocityOverride = Vector3.zero;
            step.directMovementVector = carry + hold;
            step.targetRotation = Quaternion.LookRotation(-FlatNormal(motor.wallClimb.normal, step), Vector3.up);
            step.state |= CharacterState.WallClimbing;
        }

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
            right = NormalizedOr(right, Vector3.right);
            up = Vector3.Cross(-normal, right).normalized;
        }

        /// <summary>
        /// The movement input as a direction over the surface (x along right, y along up; no longer than 1): the stick as
        /// seen along the input's look (the player's screen), so up on the stick climbs up and right goes right, whichever
        /// way it looks at the surface. With the look along the surface, where the look's directions don't lie on it, the
        /// stick's own directions are used.
        /// </summary>
        private static Vector2 ClimbInput(MovementStep step, StepInputs inputs, Vector3 right, Vector3 up)
        {
            Vector2 stick = new Vector2(inputs.movementVector.x, inputs.movementVector.z);
            float amount = Mathf.Clamp01(stick.magnitude);
            if (amount < 0.05f) { return Vector2.zero; }
            Quaternion look = step.look;
            Vector3 onScreen = look * Vector3.right * stick.x + look * Vector3.up * stick.y;
            Vector2 onSurface = new Vector2(Vector3.Dot(onScreen, right), Vector3.Dot(onScreen, up));
            if (onSurface.magnitude < 0.25f * amount) { onSurface = stick; }
            return onSurface.normalized * amount;
        }

        /// <summary>A surface's normal, flattened onto the ground (unit length; the character's facing reversed if it's flat).</summary>
        private static Vector3 FlatNormal(Vector3 normal, MovementStep step)
        {
            Vector3 flat = Flat(normal);
            return NormalizedOr(flat, -Facing(step));
        }

        /// <summary>How far the surface being climbed has moved the character since the last step (zero if it's still).</summary>
        private static Vector3 WallClimbCarry(MovementStep step, MMotionMotor motor)
        {
            WallClimbState climb = motor.wallClimb;
            if (!climb.hasLocalFeet || climb.surface == null) { return Vector3.zero; }
            MMotionMotor.GetPhysicsPose(climb.surface, out Vector3 position, out Quaternion rotation);
            Vector3 carry = position + rotation * climb.localFeet - step.initialPosition;
            // Static surfaces still give a tiny carry from floating point error, and a teleport shouldn't drag the character.
            if (carry.sqrMagnitude < 1e-10f || carry.sqrMagnitude > WallClimbMaxCarryPerStep * WallClimbMaxCarryPerStep) { return Vector3.zero; }
            return carry;
        }

        /// <summary>
        /// Remembers where the character's feet are in the space of the surface it's climbing, once the step's movement is
        /// resolved, to move along with it next step.
        /// </summary>
        public override void AfterMovementResolved(MovementStep step, MMotionMotor motor)
        {
            WallClimbState climb = motor.wallClimb;
            if (!climb.IsClimbing || climb.surface == null || !step.state.HasAny(CharacterState.WallClimbing))
            {
                climb.hasLocalFeet = false;
                return;
            }
            MMotionMotor.GetPhysicsPose(climb.surface, out Vector3 position, out Quaternion rotation);
            Vector3 feet = step.initialPosition + step.finalMovementVector;
            climb.localFeet = Quaternion.Inverse(rotation) * (feet - position);
            climb.hasLocalFeet = true;
        }

        /// <summary>
        /// Kicks off the surface: upward, and away from it. Gives back the character's jumps, less the one this uses, and
        /// takes away air steering for a moment, as jumping off a wall run does.
        /// </summary>
        private void WallClimbJump(MovementStep step, MMotionMotor motor, Vector3 normal)
        {
            EndWallClimb(motor);
            Vector3 away = FlatNormal(normal, step);
            MovementRules.JumpOffWall(step, motor,
                away * (wallClimbJumpAwaySpeed * motor.SpeedScale) + Vector3.up * (wallClimbJumpUpSpeed * motor.SpeedScale), away, away);
        }

        /// <summary>
        /// Lets go of the surface (with Crouch, or stepping off it at the bottom): the character drops, and doesn't slide
        /// down the same wall before it lands.
        /// </summary>
        private void LetGoOfWall(MMotionMotor motor, Vector3 normal)
        {
            EndWallClimb(motor);
            Vector3 flat = Flat(normal);
            if (flat.sqrMagnitude < 1e-6f) { return; }
            motor.wallSlide.lastWallNormal = flat.normalized;
            motor.wallSlide.hasLastWall = true;
        }

        private void EndWallClimb(MMotionMotor motor)
        {
            WallClimbState climb = motor.wallClimb;
            climb.IsClimbing = false;
            climb.phase = WallClimbState.Phase.Climbing;
            climb.regrabCooldown = wallClimbRegrabDelay;
            climb.surface = null;
            climb.ladder = null;
            climb.hasLocalFeet = false;
            climb.closingIn = 0f;
        }
    }

    /// <summary>
    /// A climb over a climbable surface in progress, and the delay before taking hold of one again. The character's
    /// position is kept in the space of the object climbed, so it climbs on surfaces that move.
    /// </summary>
    public class WallClimbState
    {
        /// <summary>
        /// Holding on and climbing; or, at the bottom, stepping onto the surface from the ground or back off it onto the
        /// ground, held still while it's done.
        /// </summary>
        public enum Phase { Climbing, Entering, Exiting }

        /// <summary>Whether the character is holding on to a climbable surface (in any phase).</summary>
        public bool IsClimbing;
        /// <summary>What the character is doing on the surface, while IsClimbing.</summary>
        public Phase phase;
        /// <summary>Seconds since stepping on or off started.</summary>
        public float stepElapsed;
        /// <summary>How long stepping on or off takes, in seconds.</summary>
        public float stepDuration;
        /// <summary>
        /// The surface held: its normal at the point nearest the character's grip (unit length, pointing toward the
        /// character; it turns smoothly round an outside corner).
        /// </summary>
        public Vector3 normal;
        /// <summary>The point on the surface held nearest the character's grip.</summary>
        public Vector3 point;
        /// <summary>The object climbed (its Rigidbody's, if it has one).</summary>
        public Transform surface;
        /// <summary>The character's feet in the surface's space at the end of the last step, to move along with it.</summary>
        public Vector3 localFeet;
        /// <summary>Whether localFeet holds the feet from the last step.</summary>
        public bool hasLocalFeet;
        /// <summary>
        /// How far (m) the surface was from the grip when the character took hold of it, or since, the nearest: how far it
        /// reaches for it while it closes in to its holding distance (taking hold lying down, its body reached further).
        /// </summary>
        public float closingIn;
        /// <summary>The surface's speed multiplier (see MeridianMotion.Levels.ClimbableSurface).</summary>
        public float speedMultiplier = 1f;
        /// <summary>The ladder climbed, if it's one (see MeridianMotion.Levels.ClimbableLadder): straight up and down it, on its middle; null otherwise.</summary>
        public MeridianMotion.Levels.ClimbableLadder ladder;
        /// <summary>Seconds since the climb started.</summary>
        public float elapsed;
        /// <summary>Seconds until a climbable surface can be taken hold of again, after letting go of one or jumping off it.</summary>
        public float regrabCooldown;

        /// <summary>Whether the character is stepping onto the surface from the ground.</summary>
        public bool IsEntering => IsClimbing && phase == Phase.Entering;
        /// <summary>Whether the character is stepping off the surface onto the ground.</summary>
        public bool IsExiting => IsClimbing && phase == Phase.Exiting;
        /// <summary>
        /// How far through stepping onto the surface or off it the character is, or was on its last step: 0 at its start,
        /// 1 at its end, and held at 1 once it's done, until the next begins (0 if it took hold in mid-air, without a
        /// step). Held, because the step's animation is drawn at this for a moment after the step, blending out into the
        /// climb or onto the ground: dropping to 0 as the step ended drew it back at its start, so it seemed to play twice.
        /// </summary>
        public float StepProgress => stepDuration > 0f ? Mathf.Clamp01(stepElapsed / stepDuration) : 0f;
    }

    // This state's place on the character (see MMotionMotor): declared here, beside its ability, so a new
    // ability adds its state in its own file rather than in MMotionMotor.cs.
    public partial class MMotionMotor
    {
        /// <summary>The character's wall and ladder climbing state (WallClimbing).</summary>
        public WallClimbState wallClimb { get; } = new WallClimbState();
    }
}
