using UnityEngine;
using static Character.CharacterUtilities;

namespace Character
{
    /// <summary>
    /// A Scriptable Object type which defines a very standard character movement scheme
    /// </summary>
    [CreateAssetMenu(fileName = "BasicMovementMode", menuName = "Scriptable Objects/MovementModes/Basic")] [System.Serializable]
    public class BasicMovementMode : MovementMode, WalkRunMode, JumpMode, DashMode
    {
        [Header("Speed")]
        [Tooltip("How fast the character moves by default")]
        [SerializeField] private float walkSpeed;
        [Tooltip("How fast the character moves when sprinting (not yet implemented)")]
        [SerializeField] private float runSpeed;

        [Header("Jumping")]
        // TODO: Replace these with a jump height and calculations to determine needed velocity from height
        [Tooltip("A multiplier for how fast the player moves in a jump")]
        [SerializeField] private float jumpStrength;
        [Tooltip("A curve which determines the impact of holding down the jump button on continued upwards momentum")]
        [SerializeField] private AnimationCurve jumpStrengthCurve = new();
        [SerializeField] private int maxJumps;
        [Tooltip("A jump starts if the button was pressed at most this many seconds ago, so a press just before landing still jumps. Holding the button doesn't start new jumps.")]
        [SerializeField] private float jumpInputBuffer = 0.1f;

        [Header("Gravity & Falling")]
        [Tooltip("The base acceleration applied to the character when falling")]
        [SerializeField] private float gravityStrength;
        [Tooltip("The maximum falling speed the player can reach. (if 0, do not clamp fall speed)")]
        [SerializeField] private float maxFallSpeed;
        [Tooltip("How long after being grounded the player can still jump as if grounded")]
        [SerializeField] private float coyoteTime;
        [Tooltip("A curve that determines the impact of gravity during coyote time")]
        [SerializeField] private AnimationCurve coyoteTimeGravityCurve = new();

        [Header("Air Steering")]
        [Tooltip("The maximum strength with which the player can control their character's movement while in the air")]
        [SerializeField] private float airSteeringStrength;
        [Tooltip("A curve that determines how quickly after leaving the ground that the player can steer their character in the air")]
        [SerializeField] private AnimationCurve airSteeringStrengthCurve = new();
        [Tooltip("How quickly (m/s²) air steering changes the character's horizontal velocity, before the strength and curve above")]
        [SerializeField] private float airAcceleration = 80f;
        [Tooltip("How quickly (m/s²) the character slows down in the air with no steering input")]
        [SerializeField] private float airDrag = 4f;

        [Header("Dashing")]
        [Tooltip("How fast the character moves during a dash")]
        [SerializeField] private float dashSpeed = 30f;
        [Tooltip("How long a dash lasts, in seconds")]
        [SerializeField] private float dashDuration = 0.15f;
        [Tooltip("A curve over the dash (0 = start, 1 = end) scaling its speed, so a dash can burst out and ease off")]
        [SerializeField] private AnimationCurve dashSpeedCurve = AnimationCurve.Linear(0f, 1f, 1f, 0.75f);
        [Tooltip("The fraction of the dash's speed the character keeps as momentum once the dash ends")]
        [Range(0f, 1f)] [SerializeField] private float dashEndSpeedRetention = 0.3f;
        [Tooltip("Seconds after a dash starts before another can begin")]
        [SerializeField] private float dashCooldown = 0.4f;
        [Tooltip("How many dashes the character can perform before landing again")]
        [SerializeField] private int maxAirDashes = 1;
        [Tooltip("If the dash button is pressed this many seconds before a dash is available, dash as soon as it is")]
        [SerializeField] private float dashInputBuffer = 0.1f;
        [Tooltip("Jumping during a dash cancels it into the jump. This is the fraction of the dash's speed carried into the jump")]
        [Range(0f, 1f)] [SerializeField] private float dashJumpSpeedRetention = 0.5f;

        [Header("QualityOfLife")]
        [Tooltip("Dampens the impact of horizontal velocity on the character's movement while grounded")]
        [SerializeField] private float groundedDampenHorizontalInertia = 10f;

        // Mandatory Interaface Variables
        float WalkRunMode.walkSpeed { get => walkSpeed; set => walkSpeed = value; }
        float WalkRunMode.runSpeed { get => runSpeed; set => runSpeed = value; }
        float JumpMode.jumpStrength { get => jumpStrength; set => jumpStrength = value; }
        int JumpMode.maxJumps { get => maxJumps; set => maxJumps = value; }
        float DashMode.dashSpeed { get => dashSpeed; set => dashSpeed = value; }
        float DashMode.dashDuration { get => dashDuration; set => dashDuration = value; }
        int DashMode.maxAirDashes { get => maxAirDashes; set => maxAirDashes = value; }

        /// <summary>
        /// Determines whether the character can perform a jump
        /// </summary>
        /// <param name="data"></param>
        /// <param name="controller"></param>
        /// <returns></returns>
        public bool CanJump(MovementData data, MovementController controller)
        {
            return data.previousState.HasFlag(CharacterState.Jumping) || controller.TryUseJump();
        }

        /// <summary>
        /// Whether the jump button press is fresh enough to start a new jump, and hasn't started one already (holding
        /// the button only continues a jump)
        /// </summary>
        /// <param name="inputs"></param>
        /// <param name="controller"></param>
        /// <returns></returns>
        private bool IsNewJumpPress(Inputs inputs, MovementController controller)
        {
            return inputs.jump != null && inputs.jump.value && inputs.jump.holdDuration <= jumpInputBuffer && !controller.jumpPressConsumed;
        }

        /// <summary>
        /// Uses inputs and the starting data for the character at the beginning of the frame to fill out a movement data object
        /// with data on how the character should move this frame.
        /// </summary>
        /// <param name="movementData"></param>
        /// <param name="inputs"></param>
        /// <param name="controller"></param>
        public override void ProcessMovementData(ref MovementData movementData, Inputs inputs, MovementController controller)
        {
            // A released jump button frees the next press to start a jump (checked here, before the dash, which can
            // skip jump processing while it lasts)
            if (inputs.jump == null || !inputs.jump.value) { controller.jumpPressConsumed = false; }

            // How much gravity do we apply
            // TODO: this could be it's own function, but for now, this suffices
            // The coyote gravity curve only applies after walking off the ground. After a jump, gravity is at full
            // strength, so a jump ended early (a short tap, or bumping a ceiling) doesn't float at its peak.
            float airGravity = controller.HasJumpedSinceGrounded ? 1f : coyoteTimeGravityCurve.Evaluate(movementData.timeSinceGrounded);
            float g = gravityStrength * (movementData.initialGrounded ? 0.5f : airGravity);
            // Apply gravity to the character
            movementData.velocityChange += Vector3.down * g * movementData.deltaTime;

            // If we are on the ground, limit the downward velocity - we do want some, so that we stick to the ground, but not too much
            if (movementData.initialGrounded)
            {
                // Note: g is an acceleration used as a speed here, and a jump replaces this velocity rather than the
                // (clamped) inertia below, so every jump's first frame gets g as extra launch speed: part of the jump's
                // tuned feel (0.4 m of a 3 m jump). Being a single frame's worth, it can't scale with size the way the
                // rest of the jump does (speed and duration both), so it's scaled by height directly, which keeps jump
                // height proportional to the character's size. At the authored size it's unchanged.
                movementData.initialVelocity = new Vector3(movementData.initialVelocity.x, -g * controller.HeightScale, movementData.initialVelocity.z);
                // Inertia is what actually drives movement. Without this, gravity piles up in inertia.y every grounded
                // frame (the floor blocks the motion but never resets inertia), so walking off a ledge after standing
                // for a while launches the character downward far faster than a normal fall. This frame's gravity
                // (added via velocityChange above) still applies, which keeps a slight downward push to stay grounded.
                movementData.initialInertia = new Vector3(movementData.initialInertia.x, Mathf.Max(movementData.initialInertia.y, 0f), movementData.initialInertia.z);
            }

            // Apply movement input to the character
            (this as WalkRunMode).ProcessMovementDataForWalk(ref movementData, inputs, controller);
            // Apply dash input to the character. A dash takes over the character's movement while it lasts.
            ProcessMovementDataForDash(ref movementData, inputs, controller);
            // Apply jump input to the character (not while dashing, including the dash's last frame, where the dash's
            // velocity override would make the jump use up a jump without any effect)
            if (!movementData.state.HasFlag(CharacterState.Dashing))
            {
                (this as JumpMode).ProcessMovementDataForJump(ref movementData, inputs, controller);
            }

            // If we are falling too fast, limit our fall speed. A maxFallSpeed of 0 disables the clamp (per tooltip).
            // Like every speed, the limit scales with the character's size (see MovementController.SpeedScale).
            float fallLimit = maxFallSpeed * controller.SpeedScale;
            if (maxFallSpeed > 0f && movementData.finalVelocity.y < -fallLimit)
            {
                Vector3 inertia = movementData.finalInertia;
                movementData.ForceMovementVector(new Vector3(movementData.finalMovementVector.x, fallLimit * movementData.deltaTime * -1f, movementData.finalMovementVector.z),
                    new Vector3(inertia.x, Mathf.Max(inertia.y, -fallLimit), inertia.z));
            }
        }

        /// <summary>
        /// Determines whether the character can begin a dash this frame: the button has been newly pressed (or was
        /// pressed within the input buffer), the cooldown is over, and they have an air dash left if airborne
        /// </summary>
        /// <param name="data"></param>
        /// <param name="inputs"></param>
        /// <param name="controller"></param>
        /// <returns></returns>
        public bool CanDash(MovementData data, Inputs inputs, MovementController controller)
        {
            DashState dash = controller.dash;
            bool pressed = inputs.dash != null && inputs.dash.value && !dash.pressConsumed && inputs.dash.holdDuration <= dashInputBuffer;
            bool hasAirDash = data.initialGrounded || dash.airDashesUsed < maxAirDashes;
            return pressed && !dash.IsDashing && dash.cooldownRemaining <= 0f && hasAirDash;
        }

        /// <summary>
        /// Reads dash button input and turns it into a short, fast burst of movement. While dashing, the character moves
        /// at the dash's speed in a straight line, ignoring gravity and steering; when the dash ends they keep a fraction
        /// of its speed as momentum, which the grounded friction or the landing then eases out.
        /// </summary>
        /// <param name="movementData"></param>
        /// <param name="inputs"></param>
        /// <param name="controller"></param>
        public void ProcessMovementDataForDash(ref MovementData movementData, Inputs inputs, MovementController controller)
        {
            DashState dash = controller.dash;
            float deltaTime = movementData.deltaTime;

            // Advance timers, and give back air dashes on the ground
            dash.cooldownRemaining = Mathf.Max(0f, dash.cooldownRemaining - deltaTime);
            if (movementData.initialGrounded) { dash.airDashesUsed = 0; }
            if (inputs.dash == null || !inputs.dash.value) { dash.pressConsumed = false; }

            // Start a dash in the direction of movement input (relative to the camera), or straight ahead without input
            if (CanDash(movementData, inputs, controller))
            {
                Vector3 direction = movementData.cameraLookFlat * inputs.movementVector;
                if (direction.sqrMagnitude < 0.01f) { direction = movementData.initialRotation * Vector3.forward; }
                direction.y = 0f;
                dash.direction = direction.normalized;
                dash.timeRemaining = dashDuration * controller.DurationScale;
                dash.cooldownRemaining = dashCooldown;
                dash.pressConsumed = true;
                if (!movementData.initialGrounded) { dash.airDashesUsed++; }
                movementData.events |= MovementEvents.DashStarted;
            }

            if (!dash.IsDashing) { return; }

            // Jumping cancels the dash into the jump, keeping some of the dash's speed; the jump then runs as normal this
            // frame. Only a press made after the dash began counts, so a jump held from before it doesn't cancel it.
            // The dash's speed and length follow the character's size (see MovementController.SpeedScale).
            float duration = dashDuration * controller.DurationScale;
            float topSpeed = dashSpeed * controller.SpeedScale;
            float dashElapsed = duration - dash.timeRemaining;
            bool jumpPressedDuringDash = IsNewJumpPress(inputs, controller) && inputs.jump.holdDuration < dashElapsed;
            bool hasJump = movementData.initialGrounded || controller.jumpsRemaining > 0;
            if (jumpPressedDuringDash && hasJump)
            {
                dash.timeRemaining = 0f;
                movementData.events |= MovementEvents.DashEnded;
                Vector3 momentum = dash.direction * (topSpeed * dashJumpSpeedRetention);
                movementData.initialInertia = new Vector3(momentum.x, 0f, momentum.z);
                movementData.directMovementVector = Vector3.zero; // Momentum only, so walking doesn't stack on top of it.
                return;
            }

            // Dash: take over the character's velocity entirely (no gravity, no steering), and face the dash
            float progress = 1f - dash.timeRemaining / Mathf.Max(duration, 1e-4f);
            float speed = topSpeed * dashSpeedCurve.Evaluate(progress);
            dash.timeRemaining = Mathf.Max(0f, dash.timeRemaining - deltaTime);

            // On the final frame of the dash, hand over to momentum rather than stopping dead
            if (!dash.IsDashing)
            {
                speed = topSpeed * dashEndSpeedRetention;
                movementData.events |= MovementEvents.DashEnded;
            }

            movementData.velocityOverride = dash.direction * speed;
            movementData.directMovementVector = Vector3.zero;
            movementData.targetRotation = Quaternion.LookRotation(dash.direction, Vector3.up);
            movementData.state = movementData.state | CharacterState.Dashing;
        }

        /// <summary>
        /// Handles applying horizontal movement to the player based on wasd or joystick input
        /// </summary>
        /// <param name="movementData"></param>
        /// <param name="inputs"></param>
        /// <param name="controller"></param>
        public void ProcessMovementDataForWalk(ref MovementData movementData, Inputs inputs, MovementController controller)
        {
            float steeringEffectiveness = controller.movementSpeedMultiplier;

            if (movementData.initialGrounded)
            {
                // We are grounded, so to limit sliding, kill horizontal momentum over time to simulate friction
                movementData.initialInertia = Vector3.Lerp(movementData.initialInertia, Vector3.up * movementData.initialInertia.y, groundedDampenHorizontalInertia * movementData.deltaTime);
            }
            else
            {
                // We are not grounded, so determine the extent to which we are able to move
                steeringEffectiveness *= airSteeringStrengthCurve.Evaluate(movementData.timeSinceGrounded) * airSteeringStrength;
                SteerInAir(ref movementData, inputs, controller, steeringEffectiveness);
                movementData.state = movementData.state | CharacterState.Moving;
                return;
            }

            // if there is any input and we can move, Move the character at their walking speed in the direction of their input, relative to the camera. Face the direction they are moving.
            if (inputs.movementVector.sqrMagnitude > 0.001f && steeringEffectiveness > 0)
            {
                movementData.directMovementVector = movementData.cameraLookFlat * inputs.movementVector * steeringEffectiveness * walkSpeed * controller.SpeedScale * Time.fixedDeltaTime;
                movementData.targetRotation = Quaternion.Lerp(movementData.initialRotation, Quaternion.LookRotation(movementData.directMovementVector.sqrMagnitude > 0 ? movementData.directMovementVector : movementData.initialRotation * Vector3.forward, Vector3.up), steeringEffectiveness);
            }

            // Affect the character's state
            movementData.state = movementData.state | CharacterState.Moving;
        }

        /// <summary>
        /// Steers the character's horizontal momentum in the air. Rather than adding walking movement on top of the
        /// momentum carried off the ground (which would double the character's speed), the horizontal velocity is
        /// accelerated toward the steering input at walking speed. Any speed beyond walking speed in the steering
        /// direction is kept (e.g. from a dash jump), and with no input the character slowly loses speed.
        /// </summary>
        /// <param name="movementData"></param>
        /// <param name="inputs"></param>
        /// <param name="controller"></param>
        /// <param name="steeringEffectiveness">How much control the character has right now (0 = none)</param>
        private void SteerInAir(ref MovementData movementData, Inputs inputs, MovementController controller, float steeringEffectiveness)
        {
            Vector3 inertia = movementData.initialInertia;
            Vector2 velocity = new Vector2(inertia.x, inertia.z);
            Vector3 steer = movementData.cameraLookFlat * inputs.movementVector;
            Vector2 steerDirection = new Vector2(steer.x, steer.z);
            float input = Mathf.Clamp01(steerDirection.magnitude);

            if (input > 0.001f && steeringEffectiveness > 0f)
            {
                steerDirection /= steerDirection.magnitude;
                float speedAlongInput = Vector2.Dot(velocity, steerDirection);
                Vector2 target = steerDirection * Mathf.Max(walkSpeed * controller.SpeedScale * controller.movementSpeedMultiplier * input, speedAlongInput);
                velocity = Vector2.MoveTowards(velocity, target, airAcceleration * steeringEffectiveness * movementData.deltaTime);
                movementData.targetRotation = Quaternion.Lerp(movementData.initialRotation, Quaternion.LookRotation(steer, Vector3.up), steeringEffectiveness);
            }
            else if (input <= 0.001f)
            {
                velocity = Vector2.MoveTowards(velocity, Vector2.zero, airDrag * movementData.deltaTime);
            }

            movementData.initialInertia = new Vector3(velocity.x, inertia.y, velocity.y);
        }

        /// <summary>
        /// Reads jump button input and translates that into satisfying jumps
        /// </summary>
        /// <param name="movementData"></param>
        /// <param name="inputs"></param>
        /// <param name="controller"></param>
        public void ProcessMovementDataForJump(ref MovementData movementData, Inputs inputs, MovementController controller)
        {
            // If we're on the ground, or just walked off it (coyote time), and not already in a jump, reset the number of
            // jumps available to us. Not if we left the ground by jumping, though: a quick tap jump would otherwise land
            // back inside the coyote window and get its jump refunded.
            if (!movementData.previousState.HasFlag(CharacterState.Jumping) && (movementData.timeSinceGrounded <= coyoteTime)
                && (movementData.initialGrounded || !controller.HasJumpedSinceGrounded))
            {
                controller.ResetJumps();
            }

            // Figure out what our velocity would be this frame of a jump
            // TODO: As noted before, this should ideally be calculated based on a jump height, but for now, this is based on a velocity curve
            // Scaled for the character's size: launch speed with SpeedScale (so jump height follows height under Natural
            // scaling), and how long the button can be held to jump higher with DurationScale.
            float jumpVelocity = controller.jumpStrengthModifier * controller.SpeedScale
                * jumpStrengthCurve.Evaluate(inputs.jump.holdDuration / controller.DurationScale) * jumpStrength;
            // Continue a jump while the button is held, until the character bumps their head, or start one on a fresh
            // press if a jump is left. Check the jump has force first, since starting one uses up a jump.
            bool continuingJump = movementData.previousState.HasFlag(CharacterState.Jumping) && inputs.jump.value
                && !movementData.previousCollisions.HasFlag(CollisionFlags.Above);
            // A jump from the ground or within coyote time, rather than in mid-air (checked before the jump is used)
            bool fromGround = movementData.initialGrounded || (movementData.timeSinceGrounded <= coyoteTime && !controller.HasJumpedSinceGrounded);
            bool startingJump = !continuingJump && jumpVelocity > 0 && IsNewJumpPress(inputs, controller) && controller.TryUseJump();
            if ((continuingJump && jumpVelocity > 0) || startingJump)
            {
                if (startingJump)
                {
                    controller.jumpPressConsumed = true;
                    movementData.events |= fromGround ? MovementEvents.Jumped : MovementEvents.AirJumped;
                }

                // Stop downwards momentum that previously applied to the character
                movementData.initialInertia = new Vector3(movementData.initialInertia.x, Mathf.Max(movementData.initialInertia.y, 0), movementData.initialInertia.z);
                // Add vertical velocity using the jump velocity calculated above. (Horizontal momentum is carried into the
                // air by the movement controller when the character leaves the ground.)
                movementData.velocityChange = new Vector3(movementData.velocityChange.x, jumpVelocity - movementData.initialVelocity.y, movementData.velocityChange.z);

                // Affect the character's state for animation implementation at a later date
                movementData.state = movementData.state | CharacterState.Jumping;
                movementData.state = movementData.state & (~CharacterState.Grounded);
            }

            // If a jump just ended, severely dampen vertical momentum to make the end of a jump feel responsive
            if (movementData.previousState.HasFlag(CharacterState.Jumping) && !movementData.state.HasFlag(CharacterState.Jumping))
            {
                movementData.initialInertia = new Vector3(movementData.initialInertia.x, Mathf.Lerp(movementData.initialVelocity.y, Mathf.Min(movementData.initialVelocity.y, 0f), 0.9f), movementData.initialInertia.z);
            }
        }
    }
}
