using System;
using Level;
using UnityEngine;

namespace Character
{
    /// <summary>
    /// Bounce pads (see Level.BouncePad): standing on one launches the character into the air, to the pad's height. First
    /// in BasicMovementMode's components, since a pad launches whatever the character is doing on it (rolling, sliding,
    /// dashing).
    /// </summary>
    [Serializable]
    public class BouncePads : MovementComponent
    {
        [Tooltip("What every bounce pad's launch height is multiplied by for this character. 0 turns bounce pads off")]
        [Min(0f)] [SerializeField] private float bounceHeightMultiplier = 1f;
        [Tooltip("How much higher a bounce goes with Jump held as the character comes down on the pad (1.5 = half as high again)")]
        [Min(1f)] [SerializeField] private float bounceJumpBoost = 1.5f;

        // Seconds after a launch before a pad can launch the character again, so a launch can't happen twice before the
        // character has left the pad.
        private const float BounceCooldown = 0.2f;
        // The least share of a launch that goes up (a pad tilted further over than this launches as steeply as this).
        private const float MinLaunchUpShare = 0.2f;

        /// <summary>What every bounce pad's launch height is multiplied by for this character. 0 turns bounce pads off.</summary>
        public float BounceHeightMultiplier { get => bounceHeightMultiplier; set => bounceHeightMultiplier = value; }

        public override void AdvanceTimers(ref MovementData movementData, Inputs inputs, MovementController controller)
        {
            controller.bounce.cooldownRemaining = Mathf.Max(0f, controller.bounce.cooldownRemaining - movementData.deltaTime);
        }

        /// <summary>
        /// Launches the character off a bounce pad it's standing on: along the pad's up, fast enough that its feet rise
        /// the pad's launch height above where they are (times the bounce height multiplier, and the jump boost with Jump
        /// held), keeping the pad's share of its horizontal speed. The launch is the character's jump from the ground: its
        /// air jumps and dashes are left. It ends whatever else had the character (a dash, roll or slide). Returns whether
        /// the pad had the character this step (only on the step it launches: after that the character is simply in the
        /// air).
        /// </summary>
        public override bool TakeOver(ref MovementData movementData, Inputs inputs, MovementController controller)
        {
            BounceState bounce = controller.bounce;
            if (bounceHeightMultiplier <= 0f || !movementData.initialGrounded || bounce.cooldownRemaining > 0f) { return false; }
            BouncePad pad = BouncePad.Of(movementData.initialGroundTransform);
            if (pad == null || pad.LaunchHeight <= 0f) { return false; }
            // Nothing that holds the character on the pad (holding a crate on it, climbing up off it) is launched.
            if (controller.ledge.IsActive || controller.vault.IsActive || controller.grab.IsGrabbing || controller.wallClimb.IsClimbing)
            {
                return false;
            }
            Mode.InterruptOthers(ref movementData, controller, this);

            bool boosted = inputs.jump != null && inputs.jump.value;
            float height = pad.LaunchHeight * bounceHeightMultiplier * (boosted ? bounceJumpBoost : 1f);
            Vector3 up = pad.LaunchDirection;
            float upShare = Mathf.Max(up.y, MinLaunchUpShare);
            float riseSpeed = MovementHelpers.LaunchSpeedFor(height, Mode.GravityAcceleration(controller), movementData.deltaTime);

            Vector3 velocity = movementData.initialVelocity;
            Vector3 kept = new Vector3(velocity.x, 0f, velocity.z) * pad.KeepMomentum;
            movementData.velocityOverride = kept + up * (riseSpeed / upShare);
            movementData.directMovementVector = Vector3.zero;

            // The launch is the jump from the ground: the character's air jumps are left, and gravity is at full strength
            // from here (as after a jump, not the softer coyote gravity after walking off an edge).
            MovementHelpers.GiveBackJumps(controller);
            MovementHelpers.TryUseJump(controller);
            if (boosted) { controller.jump.pressConsumed = true; }
            bounce.cooldownRemaining = BounceCooldown;
            pad.NotifyLaunched();
            movementData.events |= MovementEvents.Bounced;
            movementData.state &= ~CharacterState.Grounded;
            return true;
        }
    }

    /// <summary>The delay before a bounce pad can launch the character again, so it doesn't launch twice off one landing.</summary>
    public class BounceState
    {
        public float cooldownRemaining;
    }
}
