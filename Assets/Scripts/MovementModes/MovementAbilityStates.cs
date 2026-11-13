using UnityEngine;

namespace Character
{
    // Per-character state for the movement abilities (crouching, sliding, rolling, wall-running, ledges, vaulting,
    // climbing, pushing and pulling, ropes, ziplines and bounce pads), which persists between physics steps. Like DashState, it lives on the MovementController (one per
    // character), since movement modes are shared assets.

    /// <summary>
    /// Turns a button into one-off presses: a press counts once, and only within a buffer of when it was made, so a
    /// press made just before an ability is ready still starts it, but holding the button doesn't start it again.
    /// </summary>
    public class ButtonPress
    {
        private bool consumed;

        /// <summary>Call every step: a released button frees its next press.</summary>
        public void Track(InputButton button)
        {
            if (button == null || !button.value) { consumed = false; }
        }

        /// <summary>Whether the button is down on a press that hasn't been used yet, made at most buffer seconds ago.</summary>
        public bool IsFresh(InputButton button, float buffer) =>
            button != null && button.value && !consumed && button.holdDuration <= buffer;

        /// <summary>Uses up the press if it's fresh (see IsFresh), returning whether it was.</summary>
        public bool Consume(InputButton button, float buffer)
        {
            if (!IsFresh(button, buffer)) { return false; }
            consumed = true;
            return true;
        }
    }

    /// <summary>Whether the character is crouched, and whether it's waiting for room to stand up.</summary>
    public class CrouchState
    {
        public bool IsCrouched;
        // Asked to stand, but there wasn't room yet: stands as soon as there is.
        public bool wantsToStand;
        public readonly ButtonPress press = new();
    }

    /// <summary>A slide in progress.</summary>
    public class SlideState
    {
        public bool IsSliding;
        // Seconds since the slide started
        public float elapsed;
        // The direction the slide is heading (horizontal, unit length)
        public Vector3 direction;
    }

    /// <summary>A roll in progress, and the cooldown between rolls.</summary>
    public class RollState
    {
        // Seconds left in the current roll (0 when not rolling)
        public float timeRemaining;
        // Seconds until another roll may start
        public float cooldownRemaining;
        // The direction of the current roll (horizontal, unit length)
        public Vector3 direction;
        // How long the current roll lasts, in seconds
        public float duration;
        public readonly ButtonPress press = new();
        // Where the character's current fall started (its feet's y, m: where it last wasn't falling, or was on the ground
        // or held by a wall or a ledge), and, on the step it lands, how far it fell from there (0 on every other step):
        // for rolling on landing
        public float fallTop;
        public bool hasFallTop;
        public float landingFall;

        public bool IsRolling => timeRemaining > 0f;
        /// <summary>How far through the roll the character is: 0 at its start, 1 at its end.</summary>
        public float Progress => duration > 0f ? Mathf.Clamp01(1f - timeRemaining / duration) : 0f;
    }

    /// <summary>A wall run in progress, and the cooldown before the next.</summary>
    public class WallRunState
    {
        public bool IsWallRunning;
        // Seconds since the wall run started, and how long the character has been steering off it (see the release delay)
        public float elapsed;
        public float releaseTime;
        // Seconds until another wall run may start
        public float cooldownRemaining;
        // The wall's normal (horizontal, unit length, pointing away from the wall toward the character)
        public Vector3 wallNormal;
        // Which side of the character the wall is on: 1 = right, -1 = left (0 when not wall-running)
        public float side;
        // The wall last run along (its normal), which can't be run along again until the character touches the ground
        public Vector3 lastWallNormal;
        public bool hasLastWall;
    }

    /// <summary>
    /// Air steering coming back after a jump off a wall (a wall run or a wall slide), and the last wall jumped off since
    /// landing (a kick off a wall slid down gives back air jumps only off a different one).
    /// </summary>
    public class WallJumpState
    {
        public bool recovering;
        // Seconds since the jump
        public float elapsed;
        public Vector3 lastWallNormal;
        public bool hasLastWall;
    }

    /// <summary>A slide down a wall in progress, the cooldown before the next, and the jumps off walls made since landing.</summary>
    public class WallSlideState
    {
        public bool IsWallSliding;
        // Seconds since the slide started, and how long the character has been steering away from the wall
        public float elapsed;
        public float releaseTime;
        // Seconds until the character can slide down a wall again
        public float cooldownRemaining;
        // The wall's normal (horizontal, unit length, pointing away from the wall toward the character), and the point
        // of the wall in front of the character
        public Vector3 wallNormal;
        public Vector3 wallPoint;
        // Jumps in a row off the same wall while sliding down it since the character last touched the ground
        public int jumpsUsed;
        // How the character was last moving (horizontally) in mid-air, and when (Time.fixedTime): what it hit a wall
        // with, after the wall stopped it
        public Vector3 approachVelocity;
        public float approachTime = float.NegativeInfinity;
        // A wall the character slid off the end of, or dropped from: not slid down again until it touches the ground
        public Vector3 lastWallNormal;
        public bool hasLastWall;
    }

    /// <summary>
    /// A ledge being hung from or climbed. Positions are kept in the ledge's own space (the object it belongs to), so
    /// the character hangs on and climbs ledges that move.
    /// </summary>
    public class LedgeState
    {
        public enum Phase { None, Hanging, Climbing }

        public Phase phase;
        // The object the ledge belongs to (its Rigidbody's, if it has one)
        public Transform ledge;
        // Where the character's feet hang, in the ledge's space
        public Vector3 localHangPosition;
        // The wall's normal below the ledge, in the ledge's space (pointing out, toward the character)
        public Vector3 localWallNormal;
        // The point on the ledge's edge the character grabbed (on its top, straight in front of the character), in the
        // ledge's space: where its hands hold on
        public Vector3 localEdge;
        // Where the character's feet end up on top of the ledge after climbing, in the ledge's space
        public Vector3 localClimbTarget;
        // Seconds since the climb started, and how long it takes
        public float climbElapsed;
        public float climbDuration;
        // Whether the climb ends crouched (there's room on top to crouch, but not to stand)
        public bool climbToCrouch;
        // Seconds until a ledge can be grabbed again after letting go of one
        public float regrabCooldown;

        public bool IsActive => phase != Phase.None;

        /// <summary>
        /// Where the grabbed edge is now, in world space, and the wall's normal below it (horizontal, toward the
        /// character), from the ledge's transform: its rendered pose, for visuals such as placing the hands on it.
        /// False when not on a ledge.
        /// </summary>
        public bool TryGetEdge(out Vector3 edge, out Vector3 wallNormal)
        {
            edge = wallNormal = Vector3.zero;
            if (!IsActive || ledge == null) { return false; }
            edge = ledge.position + ledge.rotation * localEdge;
            wallNormal = ledge.rotation * localWallNormal;
            wallNormal.y = 0f;
            if (wallNormal.sqrMagnitude < 1e-6f) { return false; }
            wallNormal.Normalize();
            return true;
        }
        public bool IsHanging => phase == Phase.Hanging;
        public bool IsClimbing => phase == Phase.Climbing;
        /// <summary>How far through the climb the character is: 0 at its start, 1 on top.</summary>
        public float ClimbProgress => climbDuration > 0f ? Mathf.Clamp01(climbElapsed / climbDuration) : 0f;
    }

    /// <summary>
    /// A vault over something low, or a step up onto it, in progress. Positions are kept in the space of the object
    /// vaulted over, so a vault over something moving still goes over it.
    /// </summary>
    public class VaultState
    {
        public enum Phase { None, Vaulting, SteppingUp }

        // The share of a vault's Progress spent rising: about when the Quaternius library's safety vault, played over the
        // vault, carries its body over the hand it has planted.
        public const float RiseShare = 0.35f;

        public Phase phase;
        // The object vaulted over (its Rigidbody's, if it has one)
        public Transform obstacle;
        // The point on the edge vaulted over (on its top, in front of the character), and the wall's normal below it
        // (pointing toward the character where the vault started), in the obstacle's space
        public Vector3 localEdge;
        public Vector3 localWallNormal;
        // Where the character's feet start a vault and end it (over the edge, the whole body past the wall's face), in
        // the obstacle's space
        public Vector3 localStart;
        public Vector3 localEnd;
        // How far (m) the rise carries the character toward the wall, closing the gap to it
        public float gapDistance;
        // Seconds the rise takes, and the swing over after it
        public float riseDuration;
        public float crossDuration;
        // How fast (m/s) the swing over starts, and the speed it speeds up to, which the character leaves with
        public float crossStartSpeed;
        public float exitSpeed;
        // The velocity the character leaves with (and, stepping up, moves at), in world space
        public Vector3 exitVelocity;
        // Seconds since the vault or step up started
        public float elapsed;

        public bool IsActive => phase != Phase.None;
        public bool IsVaulting => phase == Phase.Vaulting;
        public bool IsSteppingUp => phase == Phase.SteppingUp;

        /// <summary>
        /// How far through the vault the character is, for its animation: from 0 to RiseShare over the rise, then on to
        /// 1 over the swing over (and still 1 on the step it ends, once the phase has been cleared).
        /// </summary>
        public float Progress
        {
            get
            {
                if (elapsed < riseDuration) { return RiseShare * elapsed / Mathf.Max(riseDuration, 1e-4f); }
                return Mathf.Lerp(RiseShare, 1f, crossDuration > 0f ? Mathf.Clamp01((elapsed - riseDuration) / crossDuration) : 1f);
            }
        }

        /// <summary>
        /// Where the edge being vaulted over (or stepped up onto) is now, in world space, and the wall's normal below it
        /// (horizontal, toward where the character started), from the obstacle's transform: its rendered pose, for
        /// placing a hand or the feet on it. False when not vaulting or stepping up.
        /// </summary>
        public bool TryGetEdge(out Vector3 edge, out Vector3 wallNormal)
        {
            edge = wallNormal = Vector3.zero;
            if (!IsActive || obstacle == null) { return false; }
            edge = obstacle.position + obstacle.rotation * localEdge;
            wallNormal = obstacle.rotation * localWallNormal;
            wallNormal.y = 0f;
            if (wallNormal.sqrMagnitude < 1e-6f) { return false; }
            wallNormal.Normalize();
            return true;
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

        public bool IsClimbing;
        public Phase phase;
        // Seconds since stepping on or off started, and how long it takes
        public float stepElapsed;
        public float stepDuration;
        // The surface held: its normal at the point nearest the character's grip (unit length, pointing toward the
        // character; it turns smoothly round an outside corner), and that point
        public Vector3 normal;
        public Vector3 point;
        // The object climbed (its Rigidbody's, if it has one), and the character's feet in its space at the end of the
        // last step, to move along with it
        public Transform surface;
        public Vector3 localFeet;
        public bool hasLocalFeet;
        // The surface's speed multiplier (see Level.ClimbableSurface)
        public float speedMultiplier = 1f;
        // The ladder climbed, if it's one (see Level.Ladder): straight up and down it, on its middle; null otherwise
        public Level.Ladder ladder;
        // Seconds since the climb started
        public float elapsed;
        // Seconds until a climbable surface can be taken hold of again, after letting go of one or jumping off it
        public float regrabCooldown;

        public bool IsEntering => IsClimbing && phase == Phase.Entering;
        public bool IsExiting => IsClimbing && phase == Phase.Exiting;
        /// <summary>How far through stepping onto the surface or off it the character is: 0 at its start, 1 at its end (0 otherwise).</summary>
        public float StepProgress => (IsEntering || IsExiting) && stepDuration > 0f ? Mathf.Clamp01(stepElapsed / stepDuration) : 0f;
    }

    /// <summary>
    /// A rope being swung on, and the delay before the last rope let go of can be caught again. The swing is a pendulum
    /// about the rope's anchor: the character's grip (where its hands hold the rope) stays the grip distance from it.
    /// </summary>
    public class RopeSwingState
    {
        // The rope held (null when not swinging)
        public Level.Rope rope;
        // How far (m) down the rope from its anchor the hands hold it
        public float gripDistance;
        // Where the grip is, from the anchor (gripDistance long), and how fast it's moving (m/s, across the rope)
        public Vector3 offset;
        public Vector3 velocity;
        // Seconds since catching hold
        public float elapsed;
        // The rope last let go of, and seconds until it can be caught again (others can be caught at once, so the
        // character can swing from rope to rope)
        public Level.Rope lastRope;
        public float regrabCooldown;

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

    /// <summary>
    /// A zipline being ridden, and the delay before the last one let go of can be caught again. The character hangs from
    /// a point on the cable, the given distance along it from its start, moving along it at speed (m/s, positive toward
    /// its end).
    /// </summary>
    public class ZiplineState
    {
        // The zipline ridden (null when not riding one)
        public Level.Zipline line;
        public float along;
        public float speed;
        // Seconds since catching hold
        public float elapsed;
        // The zipline last let go of, and seconds until it can be caught again
        public Level.Zipline lastLine;
        public float regrabCooldown;

        public bool IsRiding => line != null;

        /// <summary>
        /// Where the hands hold the cable for a character whose feet are at feet (its rendered pose: the cable doesn't
        /// move, but the character's rendered position lags its physics one): the point straight above them. And the way
        /// it's riding along the cable (unit length). False when not riding.
        /// </summary>
        public bool TryGetGrip(Vector3 feet, out Vector3 point, out Vector3 travel)
        {
            point = travel = Vector3.zero;
            if (line == null) { return false; }
            point = line.PointAbove(feet);
            travel = line.Direction * (speed < 0f ? -1f : 1f);
            return true;
        }
    }

    /// <summary>The delay before a bounce pad can launch the character again, so it doesn't launch twice off one landing.</summary>
    public class BounceState
    {
        public float cooldownRemaining;
    }

    /// <summary>An object held to push or pull.</summary>
    public class GrabState
    {
        // The object held (null when not grabbing)
        public Level.PushableObject target;
        // The normal of the object's face the character holds (horizontal, unit length, pointing toward the character)
        public Vector3 faceNormal;
        // Where the character's feet are relative to the object, in the object's space: kept while holding it
        public Vector3 localHoldOffset;
        // This step's move back to where the character holds the object, which the object doesn't share
        public Vector3 holdCorrection;

        public bool IsGrabbing => target != null;
    }
}
