using UnityEngine;
using static Character.CharacterUtilities;

namespace Character
{
    /// <summary>
    /// A Scriptable Object type which defines a very standard character movement scheme: walking, sprinting, jumping and
    /// dashing (here), and crouching, sliding, rolling, wall-running, wall sliding, ledge grabbing and climbing, vaulting,
    /// climbing climbable surfaces, pushing and pulling objects, swinging on ropes, riding ziplines and bouncing off
    /// bounce pads, each in a file of its own (BasicMovementMode.Crouch.cs and so on), with its settings under its own
    /// header in the inspector.
    /// </summary>
    [CreateAssetMenu(fileName = "BasicMovementMode", menuName = "Scriptable Objects/MovementModes/Basic")] [System.Serializable]
    public partial class BasicMovementMode : MovementMode, WalkRunMode, JumpMode, DashMode,
        CrouchMode, SlideMode, RollMode, WallRunningMode, WallSlideMode, LedgeMode, VaultMode, WallClimbMode, PushPullMode,
        RopeSwingMode, ZiplineMode, BounceMode
    {
        [Header("Speed")]
        [Tooltip("How fast (m/s) the character moves at full movement input, at its authored size (it scales with size: see the MovementController's Size Scaling)")]
        [SerializeField] private float walkSpeed;
        [Tooltip("How fast (m/s) the character moves while the Sprint button is held, at its authored size. At or below Walk Speed, sprinting does nothing.")]
        [SerializeField] private float runSpeed;

        [Header("Jumping")]
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

        [Header("Gravity & Falling")]
        [Tooltip("The base acceleration applied to the character when falling, at its authored size (it scales with size: see the MovementController's Size Scaling)")]
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
        [Tooltip("How quickly (m/s²) air steering changes the character's horizontal velocity, before the strength and curve above. Scales with the character's size (see the MovementController's Size Scaling).")]
        [SerializeField] private float airAcceleration = 80f;
        [Tooltip("How quickly (m/s²) the character slows down in the air with no steering input. Scales with the character's size.")]
        [SerializeField] private float airDrag = 4f;

        [Header("Dashing")]
        [Tooltip("How fast the character moves during a dash")]
        [SerializeField] private float dashSpeed = 30f;
        [Tooltip("How long a dash lasts, in seconds. 0 turns dashing off")]
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
        float JumpMode.minJumpHeight { get => minJumpHeight; set => minJumpHeight = value; }
        float JumpMode.maxJumpHeight { get => maxJumpHeight; set => maxJumpHeight = value; }
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
            // A released jump button frees the next press to start a jump (checked here, before the dash and the other
            // abilities, which can skip jump processing while they last)
            if (inputs.jump == null || !inputs.jump.value) { controller.jumpPressConsumed = false; }
            AdvanceAbilityTimers(ref movementData, inputs, controller);
            AdvanceDashTimers(ref movementData, inputs, controller);

            ApplyGravity(ref movementData, controller);

            // The abilities that take over the character's movement while they last, most committed first: each carries
            // on if it's under way, or starts if its inputs and surroundings call for it, and returns whether it has the
            // character this step. Otherwise the character walks, dashes and jumps (crouched or not).
            // A bounce pad first: it launches whatever the character is doing on it (rolling, sliding, dashing). Then what
            // the character hangs from (a zipline, a rope), as they hold it over everything below.
            bool abilityHasCharacter = ProcessMovementDataForBounce(ref movementData, inputs, controller)
                || ProcessMovementDataForZipline(ref movementData, inputs, controller)
                || ProcessMovementDataForRopeSwing(ref movementData, inputs, controller)
                || ProcessMovementDataForLedge(ref movementData, inputs, controller)
                || ProcessMovementDataForVault(ref movementData, inputs, controller)
                || ProcessMovementDataForPushPull(ref movementData, inputs, controller)
                // Before wall-running and wall sliding: a climbable surface is climbed instead.
                || ProcessMovementDataForWallClimb(ref movementData, inputs, controller)
                || ProcessMovementDataForWallRun(ref movementData, inputs, controller)
                || ProcessMovementDataForWallSlide(ref movementData, inputs, controller)
                || ProcessMovementDataForRoll(ref movementData, inputs, controller)
                || ProcessMovementDataForSlide(ref movementData, inputs, controller);
            if (!abilityHasCharacter)
            {
                ProcessMovementDataForCrouch(ref movementData, inputs, controller);
                // Apply movement input to the character
                (this as WalkRunMode).ProcessMovementDataForWalk(ref movementData, inputs, controller);
                // Apply dash input to the character. A dash takes over the character's movement while it lasts.
                ProcessMovementDataForDash(ref movementData, inputs, controller);
                // Apply jump input to the character (not while dashing, including the dash's last frame, where the dash's
                // velocity override would make the jump use up a jump without any effect, nor while crouched with no
                // room to stand up for it: see ProcessMovementDataForCrouch)
                if (!movementData.state.HasFlag(CharacterState.Dashing) && !controller.crouch.IsCrouched)
                {
                    (this as JumpMode).ProcessMovementDataForJump(ref movementData, inputs, controller);
                }
            }

            // The collider is lowered while crouched, sliding or rolling, and raised again where there's room.
            UpdatePosture(ref movementData, controller);
            ClampFallSpeed(ref movementData, controller);
        }

        /// <summary>
        /// Once the step's movement is resolved: moves a held object along with the character, remembers where the
        /// character is on a surface it's climbing, and swings a rope it's on to where it got to (see
        /// MovementMode.AfterMovementResolved).
        /// </summary>
        public override void AfterMovementResolved(MovementData movementData, MovementController controller)
        {
            AfterPushPullResolved(movementData, controller);
            AfterWallClimbResolved(movementData, controller);
            AfterRopeSwingResolved(movementData, controller);
        }

        /// <summary>
        /// Applies gravity to the character, and on the ground keeps just enough of it to stay on the ground. Abilities
        /// that move the character without gravity (a dash, a wall run, hanging from a ledge) override the velocity
        /// this gives.
        /// </summary>
        private void ApplyGravity(ref MovementData movementData, MovementController controller)
        {
            // How much gravity do we apply
            // The coyote gravity curve only applies after walking off the ground. After a jump, gravity is at full
            // strength, so a jump ended early (a short tap, or bumping a ceiling) doesn't float at its peak.
            float airGravity = controller.HasJumpedSinceGrounded ? 1f : coyoteTimeGravityCurve.Evaluate(movementData.timeSinceGrounded);
            // Gravity scales with the character's size as accelerations do (see MovementController.AccelerationScale).
            float g = gravityStrength * controller.AccelerationScale * (movementData.initialGrounded ? 0.5f : airGravity);
            // Apply gravity to the character
            movementData.velocityChange += Vector3.down * g * movementData.deltaTime;

            // If we are on the ground, limit the downward velocity - we do want some, so that we stick to the ground, but not too much
            if (movementData.initialGrounded)
            {
                // Note: g is an acceleration used as a speed here. As a speed it scales with height, not as g does (see
                // AccelerationScale). A jump sets its launch speed outright (see ProcessMovementDataForJump), so this
                // doesn't add to it. (It once did, giving every jump's first frame g as extra launch speed.)
                float groundedSpeed = g / controller.AccelerationScale * controller.HeightScale;
                movementData.initialVelocity = new Vector3(movementData.initialVelocity.x, -groundedSpeed, movementData.initialVelocity.z);
                // Inertia is what actually drives movement. Without this, gravity piles up in inertia.y every grounded
                // frame (the floor blocks the motion but never resets inertia), so walking off a ledge after standing
                // for a while launches the character downward far faster than a normal fall. This frame's gravity
                // (added via velocityChange above) still applies, which keeps a slight downward push to stay grounded.
                movementData.initialInertia = new Vector3(movementData.initialInertia.x, Mathf.Max(movementData.initialInertia.y, 0f), movementData.initialInertia.z);
            }
        }

        /// <summary>
        /// Frees the next press of each ability button once it's released, and advances the abilities' cooldowns. Every
        /// step, whichever ability has the character, so their cooldowns run out meanwhile.
        /// </summary>
        private static void AdvanceAbilityTimers(ref MovementData movementData, Inputs inputs, MovementController controller)
        {
            float deltaTime = movementData.deltaTime;
            controller.crouch.press.Track(inputs.crouch);
            controller.roll.press.Track(inputs.roll);
            controller.roll.cooldownRemaining = Mathf.Max(0f, controller.roll.cooldownRemaining - deltaTime);
            TrackFall(movementData, controller);
            controller.wallRun.cooldownRemaining = Mathf.Max(0f, controller.wallRun.cooldownRemaining - deltaTime);
            if (movementData.initialGrounded) { controller.wallRun.hasLastWall = false; controller.wallSlide.jumpsUsed = 0; controller.wallSlide.hasLastWall = false; }
            controller.wallSlide.cooldownRemaining = Mathf.Max(0f, controller.wallSlide.cooldownRemaining - deltaTime);
            controller.ledge.regrabCooldown = Mathf.Max(0f, controller.ledge.regrabCooldown - deltaTime);
            controller.wallClimb.regrabCooldown = Mathf.Max(0f, controller.wallClimb.regrabCooldown - deltaTime);
            controller.ropeSwing.regrabCooldown = Mathf.Max(0f, controller.ropeSwing.regrabCooldown - deltaTime);
            controller.zipline.regrabCooldown = Mathf.Max(0f, controller.zipline.regrabCooldown - deltaTime);
            controller.bounce.cooldownRemaining = Mathf.Max(0f, controller.bounce.cooldownRemaining - deltaTime);
            // Air steering comes back after a wall jump, and is all back on the ground.
            controller.wallJump.elapsed += deltaTime;
            if (movementData.initialGrounded) { controller.wallJump.recovering = false; controller.wallJump.hasLastWall = false; }
        }

        /// <summary>
        /// Whether a dash or one of the abilities has the character, so no other ability may start (leaving out a slide
        /// down a wall, or a dash, if asked, for an ability that may take over from one).
        /// </summary>
        private static bool AbilityInProgress(MovementController controller, bool exceptWallSlide = false, bool exceptDash = false) =>
            (controller.dash.IsDashing && !exceptDash) || controller.ledge.IsActive || controller.vault.IsActive || controller.grab.IsGrabbing
            || controller.wallRun.IsWallRunning || (controller.wallSlide.IsWallSliding && !exceptWallSlide)
            || controller.roll.IsRolling || controller.slide.IsSliding || controller.wallClimb.IsClimbing
            || controller.ropeSwing.IsSwinging || controller.zipline.IsRiding;

        /// <summary>
        /// Gives back the character's jumps and air dashes, as touching the ground does. For the abilities that count as
        /// touching it (starting a wall run, taking hold of a climbable surface, a ledge, a rope or a zipline, vaulting), and for a kick off a wall slid down that
        /// gives them back (off a different wall from the last one jumped off).
        /// </summary>
        private static void GiveBackAirMoves(MovementController controller)
        {
            controller.ResetJumps();
            controller.dash.airDashesUsed = 0;
        }

        /// <summary>The movement input in world space, relative to the camera: horizontal, and no longer than 1.</summary>
        private static Vector3 InputDirection(MovementData movementData, Inputs inputs)
        {
            Vector3 direction = movementData.cameraLookFlat * inputs.movementVector;
            direction.y = 0f;
            return Vector3.ClampMagnitude(direction, 1f);
        }

        /// <summary>The way the character faces, flattened onto the ground (unit length).</summary>
        private static Vector3 Facing(MovementData movementData)
        {
            Vector3 forward = movementData.initialRotation * Vector3.forward;
            forward.y = 0f;
            return forward.sqrMagnitude > 1e-6f ? forward.normalized : Vector3.forward;
        }

        /// <summary>Limits how fast the character falls.</summary>
        private void ClampFallSpeed(ref MovementData movementData, MovementController controller)
        {
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
            // A dash with no length would start (raising DashStarted, using the cooldown and an air dash) and be over
            // before its first step, so it would never raise DashEnded: no length means no dashing.
            return pressed && dashDuration > 0f && !dash.IsDashing && dash.cooldownRemaining <= 0f && hasAirDash;
        }

        /// <summary>
        /// Advances the dash's cooldown, gives back air dashes on the ground, and frees the next press once the button is
        /// released. Every step, including while another ability has the character, so its cooldown runs out meanwhile.
        /// </summary>
        private static void AdvanceDashTimers(ref MovementData movementData, Inputs inputs, MovementController controller)
        {
            DashState dash = controller.dash;
            dash.cooldownRemaining = Mathf.Max(0f, dash.cooldownRemaining - movementData.deltaTime);
            if (movementData.initialGrounded) { dash.airDashesUsed = 0; }
            if (inputs.dash == null || !inputs.dash.value) { dash.pressConsumed = false; }
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
            // Not while crouched with no room to stand up for the jump (it's skipped then, so the dash would just stop).
            bool hasJump = (movementData.initialGrounded || controller.jumpsRemaining > 0) && !controller.crouch.IsCrouched;
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
                // A rate per second: over the same share of a stride at any size (see MovementController.DurationScale).
                movementData.initialInertia = Vector3.Lerp(movementData.initialInertia, Vector3.up * movementData.initialInertia.y,
                    groundedDampenHorizontalInertia * movementData.deltaTime / controller.DurationScale);
            }
            else
            {
                // We are not grounded, so determine the extent to which we are able to move
                steeringEffectiveness *= airSteeringStrengthCurve.Evaluate(movementData.timeSinceGrounded) * airSteeringStrength
                                         * WallJumpSteering(controller);
                SteerInAir(ref movementData, inputs, controller, steeringEffectiveness);
                movementData.state = movementData.state | CharacterState.Moving;
                return;
            }

            // With movement input, move the way it points (relative to the camera) at walking speed (or running, while
            // sprinting), facing that way. Walking only makes up the difference between the momentum the character is
            // carrying that way (from a landing, say) and that speed: it moves at whichever is faster, momentum or
            // walking, and the momentum fades with the friction above. Walking used to be added on top: just after a
            // landing the character moved at its air speed plus its walking speed, and leaving the ground again before
            // the friction had taken the momentum away (a jump right after landing, or touching a ledge's lip for a step
            // or two) carried both into the air, where air steering keeps speed beyond walking. Every short touch of the
            // ground then added walking speed again, and hopping or clipping ledges built up to several times it.
            if (inputs.movementVector.sqrMagnitude > 0.001f && steeringEffectiveness > 0)
            {
                Vector3 steer = movementData.cameraLookFlat * inputs.movementVector;
                steer.y = 0f;
                float stick = Mathf.Min(steer.magnitude, 1f);
                float walking = stick * steeringEffectiveness * MoveSpeed(inputs, controller) * controller.SpeedScale;
                // No speed to walk at (a speed of 0), no walking: nor turning to face the input.
                if (stick > 1e-4f && walking > 1e-6f)
                {
                    Vector3 direction = steer.normalized;
                    Vector3 carried = movementData.initialInertia;
                    float carriedAlong = Mathf.Max(0f, Vector3.Dot(new Vector3(carried.x, 0f, carried.z), direction));
                    movementData.directMovementVector = direction * (Mathf.Max(0f, walking - carriedAlong) * movementData.deltaTime);
                    movementData.targetRotation = Quaternion.Lerp(movementData.initialRotation, Quaternion.LookRotation(direction, Vector3.up), steeringEffectiveness);
                }
            }

            // Affect the character's state
            movementData.state = movementData.state | CharacterState.Moving;
        }

        /// <summary>
        /// The speed to move at for these inputs: the crouch speed while crouched, otherwise the run speed while sprinting,
        /// or the walk speed (see WalkRunMode.MoveSpeed).
        /// </summary>
        private float MoveSpeed(Inputs inputs, MovementController controller) =>
            controller.crouch.IsCrouched ? crouchSpeed : ((WalkRunMode)this).MoveSpeed(inputs);

        /// <summary>
        /// Steers the character's horizontal momentum in the air. Rather than adding walking movement on top of the
        /// momentum carried off the ground (which would double the character's speed), the horizontal velocity is
        /// accelerated toward the steering input at walking speed (or running speed, while sprinting). Any speed beyond
        /// that in the steering direction is kept (e.g. from a dash jump), and with no input the character slowly loses speed.
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
                Vector2 target = steerDirection * Mathf.Max(MoveSpeed(inputs, controller) * controller.SpeedScale * controller.movementSpeedMultiplier * input, speedAlongInput);
                velocity = Vector2.MoveTowards(velocity, target, airAcceleration * controller.AccelerationScale * steeringEffectiveness * movementData.deltaTime);
                movementData.targetRotation = Quaternion.Lerp(movementData.initialRotation, Quaternion.LookRotation(steer, Vector3.up), steeringEffectiveness);
            }
            else if (input <= 0.001f)
            {
                velocity = Vector2.MoveTowards(velocity, Vector2.zero, airDrag * controller.AccelerationScale * movementData.deltaTime);
            }

            movementData.initialInertia = new Vector3(velocity.x, inertia.y, velocity.y);
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

            float g = gravityStrength * controller.AccelerationScale;
            float heightScale = controller.HeightScale * controller.jumpStrengthModifier * controller.jumpStrengthModifier;
            // A jump from the ground or within coyote time, rather than in mid-air (checked before the jump is used)
            bool fromGround = movementData.initialGrounded || (movementData.timeSinceGrounded <= coyoteTime && !controller.HasJumpedSinceGrounded);
            // Check the jump has height first, since starting one uses up a jump.
            bool startingJump = maxJumpHeight > 0f && IsNewJumpPress(inputs, controller) && controller.TryUseJump();
            Vector3 inertia = movementData.initialInertia;
            if (startingJump)
            {
                controller.jumpPressConsumed = true;
                controller.jumpStartHeight = movementData.initialPosition.y;
                movementData.events |= fromGround ? MovementEvents.Jumped : MovementEvents.AirJumped;
                // The launch speed outright, replacing whatever the character was rising or falling at (horizontal
                // momentum is carried into the air by the movement controller as it leaves the ground).
                float launch = JumpSpeedFor(maxJumpHeight * heightScale, g, movementData.deltaTime);
                movementData.velocityChange = new Vector3(movementData.velocityChange.x, launch - inertia.y, movementData.velocityChange.z);
                movementData.state = (movementData.state | CharacterState.Jumping) & ~CharacterState.Grounded;
                return;
            }

            // Still rising from a jump (not stopped by a ceiling): it carries on, under gravity (applied already).
            bool rising = movementData.previousState.HasFlag(CharacterState.Jumping) && inertia.y > 0f
                          && !movementData.previousCollisions.HasFlag(CollisionFlags.Above);
            if (!rising) { return; }
            movementData.state |= CharacterState.Jumping;
            if (inputs.jump != null && inputs.jump.value) { return; }

            // Let go of: no faster than it takes to rise the rest of the min jump height. Once cut, the rise under
            // gravity keeps to that from step to step on its own (see LaunchSpeedFor), so this changes it only once.
            float risen = movementData.initialPosition.y - controller.jumpStartHeight;
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
            g > 0f ? LaunchSpeedFor(height, g, deltaTime) : Mathf.Sqrt(2f * 9.81f * Mathf.Max(0f, height));
    }
}
