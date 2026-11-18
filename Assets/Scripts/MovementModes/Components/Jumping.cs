using System;
using UnityEngine;

namespace Character
{
    /// <summary>
    /// Jumping: a fresh press of Jump on the ground (or just after walking off it), or with a jump left in mid-air,
    /// launches the character up to a height between the min jump height (a tap) and the max (held). It runs once the
    /// character has walked and dashed (see MovementComponent.AfterWalking), and not while it's dashing or crouched with
    /// no room to stand. Other abilities jump through it (a jump out of a slide: see Crouching).
    /// </summary>
    [Serializable]
    public class Jumping : MovementComponent
    {
        [Tooltip("How high (m) the character's feet rise on the shortest jump, a tap of Jump, at its authored size (it scales " +
                 "with size). Letting go of Jump partway up cuts the rise short, but never below this")]
        [Min(0f)] [SerializeField] private float minJumpHeight = 0.63f;
        [Tooltip("How high (m) the character's feet rise with Jump held to the top of the jump, at its authored size (it scales " +
                 "with size). 0 turns jumping off")]
        [Min(0f)] [SerializeField] private float maxJumpHeight = 1.81f;
        [Tooltip("How many jumps the character can make before landing again: 1 jumps from the ground only, 2 adds a jump in mid-air")]
        [SerializeField] private int maxJumps;
        [Tooltip("A jump starts if the button was pressed at most this many seconds ago, so a press just before landing still jumps. Holding the button doesn't start new jumps.")]
        [SerializeField] private float jumpInputBuffer = 0.1f;
        [Tooltip("How long after being grounded the player can still jump as if grounded")]
        [SerializeField] private float coyoteTime;

        /// <summary>How many jumps the character can make before landing again (1 = from the ground only).</summary>
        public int MaxJumps { get => maxJumps; set => maxJumps = value; }
        /// <summary>How high (m) the shortest jump (a tap) rises, at the character's authored size.</summary>
        public float MinJumpHeight { get => minJumpHeight; set => minJumpHeight = value; }
        /// <summary>How high (m) a jump held to its top rises, at the character's authored size. 0 turns jumping off.</summary>
        public float MaxJumpHeight { get => maxJumpHeight; set => maxJumpHeight = value; }
        /// <summary>How long (s) a press of Jump counts for, after it's made.</summary>
        public float JumpInputBuffer => jumpInputBuffer;

        /// <summary>The events of a moment it raises (see MovementComponent.OneOffEvents): a jump from the ground (or in its coyote time), and one in the air.</summary>
        public override MovementEvents OneOffEvents => MovementEvents.Jumped | MovementEvents.AirJumped;

        /// <summary>
        /// Jumps once the character has walked and dashed: not while dashing (including the dash's last step, where its
        /// velocity override would make the jump use up a jump without any effect), nor while crouched with no room to
        /// stand up for it (see Crouching).
        /// </summary>
        public override void AfterWalking(MovementData movementData, Inputs inputs, MovementController controller)
        {
            if ((movementData.state & CharacterState.Dashing) != 0 || controller.crouch.IsCrouched) { return; }
            Jump(movementData, inputs, controller);
        }

        /// <summary>
        /// Jumps: a fresh press of Jump, on the ground (or just after walking off it: coyote time) or with a jump left in
        /// mid-air, launches the character straight up fast enough that its feet would rise the max jump height, under
        /// its gravity (worked out for the physics steps, so the height is exact; see LaunchSpeedFor). Held, the jump
        /// rises all the way; let go of while rising, it's cut short, to what's left of the min jump height above where it
        /// started (or straight to falling, if it's already past that), so a tap rises the min jump height and a press
        /// held longer anything up to the max. Bumping a ceiling ends the rise (the collision takes its momentum).
        /// Heights scale with the character's size, and with the square of the MovementController's jump strength
        /// modifier (a multiplier on the launch speed).
        /// </summary>
        public void Jump(MovementData movementData, Inputs inputs, MovementController controller)
        {
            // If we're on the ground, or just walked off it (coyote time), and not already in a jump, reset the number of
            // jumps available to us. Not if we left the ground by jumping, though: a quick tap jump would otherwise land
            // back inside the coyote window and get its jump refunded.
            if ((movementData.previousState & CharacterState.Jumping) == 0 && (movementData.timeSinceGrounded <= coyoteTime)
                && (movementData.initialGrounded || !controller.HasJumpedSinceGrounded))
            {
                MovementHelpers.GiveBackJumps(controller);
            }

            float g = Mode.GravityAcceleration(controller);
            float heightScale = controller.HeightScale * controller.jumpStrengthModifier * controller.jumpStrengthModifier;
            // A jump from the ground or within coyote time, rather than in mid-air (checked before the jump is used)
            bool fromGround = movementData.initialGrounded || (movementData.timeSinceGrounded <= coyoteTime && !controller.HasJumpedSinceGrounded);
            // Check the jump has height first, since starting one uses up a jump.
            bool startingJump = maxJumpHeight > 0f && Mode.IsNewJumpPress(inputs, controller) && MovementHelpers.TryUseJump(controller);
            Vector3 inertia = movementData.initialInertia;
            if (startingJump)
            {
                controller.jump.press.Use();
                controller.jump.startHeight = movementData.initialPosition.y;
                movementData.events |= fromGround ? MovementEvents.Jumped : MovementEvents.AirJumped;
                // The launch speed outright, replacing whatever the character was rising or falling at (horizontal
                // momentum is carried into the air by the movement controller as it leaves the ground).
                float launch = JumpSpeedFor(maxJumpHeight * heightScale, g, movementData.deltaTime);
                movementData.velocityChange = new Vector3(movementData.velocityChange.x, launch - inertia.y, movementData.velocityChange.z);
                movementData.state = (movementData.state | CharacterState.Jumping) & ~CharacterState.Grounded;
                return;
            }

            // Still rising from a jump (not stopped by a ceiling): it carries on, under gravity (applied already).
            bool rising = ((movementData.previousState & CharacterState.Jumping) != 0) && inertia.y > 0f
                          && !movementData.previousCollisions.HasFlag(CollisionFlags.Above);
            if (!rising) { return; }
            movementData.state |= CharacterState.Jumping;
            if (inputs.jump != null && inputs.jump.value) { return; }

            // Let go of: no faster than it takes to rise the rest of the min jump height. Once cut, the rise under
            // gravity keeps to that from step to step on its own (see LaunchSpeedFor), so this changes it only once.
            float risen = movementData.initialPosition.y - controller.jump.startHeight;
            float remaining = minJumpHeight * heightScale - risen;
            float cut = remaining > 0f ? JumpSpeedFor(remaining, g, movementData.deltaTime) : 0f;
            float rise = inertia.y + movementData.velocityChange.y;
            if (rise > cut)
            {
                movementData.velocityChange = new Vector3(movementData.velocityChange.x, cut - inertia.y, movementData.velocityChange.z);
                if (cut <= 0f) { movementData.state &= ~CharacterState.Jumping; }
            }
        }

        /// <summary>
        /// The launch speed that rises height metres under gravity g in physics steps (see LaunchSpeedFor). Without
        /// gravity no speed reaches a height, so a jump then launches as it would under Earth's, and drifts on.
        /// </summary>
        private static float JumpSpeedFor(float height, float g, float deltaTime) =>
            g > 0f ? MovementHelpers.LaunchSpeedFor(height, g, deltaTime) : Mathf.Sqrt(2f * EarthGravity * Mathf.Max(0f, height));

        // Earth's gravity (m/s²), which a jump's launch speed is worked out under when the character has none.
        private const float EarthGravity = 9.81f;
    }

    /// <summary>
    /// The character's jumps: how many it has left before landing, when it last started one (and from what height, for
    /// cutting it short), and whether the current press of Jump has started something already. Jumps off walls, ropes and
    /// ziplines, and bounce pads, use and give back the same jumps (see MovementHelpers.TryUseJump and GiveBackJumps).
    /// </summary>
    public class JumpState
    {
        /// <summary>How many more times the character can start a jump before landing (given back on the ground).</summary>
        public int remaining;
        /// <summary>The step (MovementController.StepTick) the character last started a jump in (NeverTick if it never has).</summary>
        public long lastJumpTick = MovementController.NeverTick;
        /// <summary>How high the character's feet were when it last started a jump (for cutting a jump short: see Jumping).</summary>
        public float startHeight;
        /// <summary>
        /// The press of Jump: one press starts one thing, never two (for example when a jump ends early against a ceiling
        /// while the press is still within the jump buffer), and only within Jumping's Jump Input Buffer of being made.
        /// </summary>
        public readonly ButtonPress press = new();
    }

    // This state's place on the character (see MovementController): declared here, beside its ability, so a new
    // ability adds its state in its own file rather than in MovementController.cs.
    public partial class MovementController
    {
        /// <summary>The character's jumping state (Jumping).</summary>
        public JumpState jump { get; } = new JumpState();
    }
}
