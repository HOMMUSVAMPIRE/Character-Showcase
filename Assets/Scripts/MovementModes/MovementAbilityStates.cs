using UnityEngine;

namespace Character
{
    // Per-character state for the movement abilities (crouching, sliding, rolling, wall-running, ledges, pushing and
    // pulling), which persists between physics steps. Like DashState, it lives on the MovementController (one per
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

        public bool IsRolling => timeRemaining > 0f;
        /// <summary>How far through the roll the character is: 0 at its start, 1 at its end.</summary>
        public float Progress => duration > 0f ? Mathf.Clamp01(1f - timeRemaining / duration) : 0f;
    }

    /// <summary>A wall run in progress, and the cooldown before the next.</summary>
    public class WallRunState
    {
        public bool IsWallRunning;
        // Seconds since the wall run started
        public float elapsed;
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

    /// <summary>A slide down a wall in progress, the cooldown before the next, and the jumps off walls made since landing.</summary>
    public class WallSlideState
    {
        public bool IsWallSliding;
        // Seconds since the slide started
        public float elapsed;
        // Seconds until the character can slide down a wall again
        public float cooldownRemaining;
        // The wall's normal (horizontal, unit length, pointing away from the wall toward the character), and the point
        // of the wall in front of the character
        public Vector3 wallNormal;
        public Vector3 wallPoint;
        // Jumps off walls made while sliding down them since the character last touched the ground
        public int jumpsUsed;
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
