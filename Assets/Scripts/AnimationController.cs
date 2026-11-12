using System;
using UnityEngine;

namespace Character
{
    /// <summary>
    /// The character's link between movement and animation. It turns each frame's movement into a
    /// CharacterAnimationState, passes that state and the movement events to the model's animation translator, and
    /// turns the model smoothly to face the character's direction.
    ///
    /// Nothing here depends on a particular model, how it's stored, its Animator Controller or animation system: the
    /// model comes from the CharacterModelHost, and how it's animated from the translator its source names, so a model
    /// can be replaced (or swapped at runtime) without touching the character.
    /// </summary>
    [DisallowMultipleComponent]
    public class AnimationController : MonoBehaviour, ICharacterAnimationStateSource
    {
        [SerializeField] [Tooltip("A reference to the parent character controller")]
        private ACCharacterController characterController;
        [SerializeField] [Tooltip("The character's movement, whose events this passes on. Found automatically if left empty.")]
        private MovementController movementController;
        [SerializeField] [Tooltip("The character's model, which this animates. Found automatically if left empty.")]
        private CharacterModelHost modelHost;

        [Header("Animation State")]
        [SerializeField] [Tooltip("Seconds the character must be off the ground before animating as airborne (unless they jumped), so stepping off small ledges or bumps doesn't flash the falling pose")]
        private float airborneDelay = 0.12f;
        [SerializeField] [Tooltip("Horizontal speed (m/s) above which the character counts as moving")]
        private float movingSpeedThreshold = 0.1f;

        [Header("Smooth Rotation Variables")]
        [SerializeField] [Tooltip("How quickly the model turns to face the way the character does (which turns at once, " +
                                  "for responsiveness): about the inverse of the seconds a turn takes. The model eases into " +
                                  "and out of each turn, rather than snapping round at its start.")]
        private float rotationFixSpeed = 10f;
        [SerializeField] [Tooltip("The fastest (degrees per second) the model turns while standing still")]
        private float maxTurnSpeedStanding = 900f;
        [SerializeField] [Tooltip("The fastest (degrees per second) the model turns at full speed (see Full Turn Speed At), so " +
                                  "a sprinting character sweeps round a turn rather than spinning on the spot")]
        private float maxTurnSpeedMoving = 420f;
        [SerializeField] [Tooltip("The speed (m/s, at the character's authored size) at and above which the model turns at most " +
                                  "at Max Turn Speed Moving")]
        private float fullTurnSpeedAt = 8f;
        [SerializeField] [Tooltip("Seconds the model takes to turn to a facing an ability sets (a dash, roll, slide, wall run, " +
                                  "ledge, wall slide or push): those turn crisply, as the ability does")]
        private float abilityTurnTime = 0.04f;

        [Header("Leaning")]
        [SerializeField] [Tooltip("How far the model leans into turns, as a share of the lean that would balance it (the " +
                                  "angle whose tangent is how hard its path is turning it, its speed the way it faces " +
                                  "times how fast its path curves, over gravity). 0 = no leaning.")]
        [Range(0f, 1f)] private float turnLean = 0.5f;
        [SerializeField] [Tooltip("How far the model leans forward speeding up and back slowing down, as a share of the lean " +
                                  "that would balance it (the angle whose tangent is its acceleration over gravity). 0 = none.")]
        [Range(0f, 1f)] private float accelerationLean = 0.25f;
        [SerializeField] [Tooltip("The furthest (degrees) the model leans, either way")]
        [Range(0f, 45f)] private float maxLean = 15f;
        [SerializeField] [Tooltip("The furthest the model leans for how fast it's actually moving: degrees per m/s (at the " +
                                  "character's authored size), up to Max Lean. Walking a sharp corner leans less than " +
                                  "sprinting it, rather than both leaning as far as they can.")]
        [Min(0f)] private float maxLeanPerSpeed = 1.5f;
        [SerializeField] [Tooltip("Seconds over which leaning eases in and out")]
        [Min(0.01f)] private float leanSmoothTime = 0.12f;

        // Gravity (m/s²) leans are balanced against, at the character's authored size.
        private const float LeanGravity = 9.81f;

        // The model's yaw (degrees) and how fast it's turning (degrees per second), and its lean: bank (degrees, positive
        // into a right turn) and pitch (degrees, positive forward), with their smoothing velocities.
        private float visualYaw, yawVelocity;
        private float bank, bankVelocity, pitch, pitchVelocity;
        private bool hasVisualYaw;
        // The character's horizontal velocity last frame, and its acceleration, smoothed, for leaning; and its velocity
        // smoothed, whose heading turning is the character's path curving.
        private Vector3 lastHorizontalVelocity;
        private Vector3 acceleration;
        private Vector3 smoothedVelocity;
        private float lastPathHeading, pathTurnRate;
        private bool hasLastVelocity;
        // State carried between frames to build the animation state
        private float airTime;
        private Quaternion lastFacing = Quaternion.identity;
        private bool hasLastFacing;

        private ICharacterAnimationDriver driver;
        // The driver of a model being replaced, which the new model's driver carries on from.
        private ICharacterAnimationDriver retiredDriver;

        /// <summary>The character's animation state as of the latest frame.</summary>
        public CharacterAnimationState AnimationState { get; private set; }
        /// <summary>The character's model, which this animates.</summary>
        public CharacterModelHost ModelHost => modelHost;

        /// <summary>Raised for each movement event (one flag at a time), with the animation state when it happened. For
        /// effects, sound and anything else that reacts to the character's actions.</summary>
        public event Action<MovementEvents, CharacterAnimationState> MovementEventReceived;

        private void Awake()
        {
            if (movementController == null) { movementController = GetComponent<MovementController>(); }
            if (modelHost == null) { modelHost = GetComponent<CharacterModelHost>(); }
        }

        private void OnEnable()
        {
            if (movementController != null) { movementController.MovementEventRaised += HandleMovementEvent; }
            if (modelHost != null)
            {
                modelHost.ModelLoaded += HandleModelLoaded;
                modelHost.ModelUnloaded += HandleModelUnloaded;
                modelHost.ModelReplacing += HandleModelReplacing;
                if (modelHost.CurrentModel != null) { HandleModelLoaded(modelHost.CurrentModel); }
            }
        }

        private void OnDisable()
        {
            if (movementController != null) { movementController.MovementEventRaised -= HandleMovementEvent; }
            if (modelHost != null)
            {
                modelHost.ModelLoaded -= HandleModelLoaded;
                modelHost.ModelUnloaded -= HandleModelUnloaded;
                modelHost.ModelReplacing -= HandleModelReplacing;
            }
        }

        private void HandleModelLoaded(CharacterModel model)
        {
            driver = model.Translator != null ? model.Translator.CreateDriver(model.Root, model.Animator) : null;
            if (driver != null && retiredDriver != null) { driver.ContinueFrom(retiredDriver); }
            retiredDriver = null;
            if (modelHost.ModelParent != null)
            {
                visualYaw = modelHost.ModelParent.eulerAngles.y;
                hasVisualYaw = true;
            }
        }

        private void HandleModelReplacing(CharacterModel model)
        {
            driver?.Retire();
            retiredDriver = driver;
        }

        private void HandleModelUnloaded(CharacterModel model) => driver = null;

        /// <summary>
        /// Works out the character's animation state from their latest movement and passes it to the model's animation.
        /// Call this every rendered frame with the most recently completed movement data.
        /// </summary>
        /// <param name="movementData">The latest completed movement data, or null if none yet</param>
        /// <param name="deltaTime"></param>
        public void UpdateAnimator(MovementData movementData, float deltaTime)
        {
            if (movementData == null) { return; }
            AnimationState = BuildState(movementData, deltaTime);
            driver?.UpdateAnimation(AnimationState, deltaTime);
        }

        private void HandleMovementEvent(MovementEvents movementEvent, MovementData movementData)
        {
            // The state at the moment of the event (no time passes, so the frame-to-frame tracking is unaffected).
            CharacterAnimationState state = BuildState(movementData, 0f);
            driver?.HandleMovementEvent(movementEvent, state);
            MovementEventReceived?.Invoke(movementEvent, state);
        }

        /// <summary>The layer between movement and animation: everything animations need, and nothing about how movement works.</summary>
        private CharacterAnimationState BuildState(MovementData movementData, float deltaTime)
        {
            Vector3 velocity = movementData.finalVelocity;
            Quaternion facing = Quaternion.Euler(0f, movementData.finalRotation.eulerAngles.y, 0f);
            bool touchingGround = movementData.state.HasFlag(CharacterState.Grounded);
            bool jumping = movementData.state.HasFlag(CharacterState.Jumping);

            airTime = touchingGround ? 0f : airTime + deltaTime;
            float turnSpeed = AnimationState.turnSpeed;
            if (deltaTime > 0f)
            {
                turnSpeed = hasLastFacing ? Mathf.DeltaAngle(lastFacing.eulerAngles.y, facing.eulerAngles.y) / deltaTime : 0f;
                lastFacing = facing;
                hasLastFacing = true;
            }

            float horizontalSpeed = new Vector2(velocity.x, velocity.z).magnitude;
            float speedScale = movementController != null ? movementController.SpeedScale : 1f;
            Vector3 ledgeEdge = Vector3.zero, ledgeNormal = Vector3.zero;
            bool onLedge = movementData.state.HasFlag(CharacterState.LedgeHanging) || movementData.state.HasFlag(CharacterState.LedgeClimbing);
            if (onLedge && movementController != null) { movementController.ledge.TryGetEdge(out ledgeEdge, out ledgeNormal); }
            bool wallSliding = movementData.state.HasFlag(CharacterState.WallSliding) && movementController != null;
            return new CharacterAnimationState
            {
                velocity = velocity,
                localVelocity = Quaternion.Inverse(facing) * velocity,
                horizontalSpeed = horizontalSpeed,
                // Touching the ground, the character's vertical speed is only it following the ground up or down (a
                // step up a stair lifts it at several metres a second): not rising or falling. Reported as such, the
                // Animator, which lands only once no longer rising, stayed in its jump all the way up a staircase landed
                // on while moving. A jump leaves the ground on the step it starts, so its rise is still reported.
                verticalSpeed = touchingGround ? 0f : velocity.y,
                turnSpeed = turnSpeed,
                facing = facing,
                grounded = touchingGround || (!jumping && airTime < airborneDelay),
                touchingGround = touchingGround,
                timeInAir = airTime,
                moving = horizontalSpeed > movingSpeedThreshold * speedScale, // The threshold is for the authored size.
                jumping = jumping,
                dashing = movementData.state.HasFlag(CharacterState.Dashing),
                crouching = movementData.state.HasFlag(CharacterState.Crouching),
                sliding = movementData.state.HasFlag(CharacterState.Sliding),
                rolling = movementData.state.HasFlag(CharacterState.Rolling),
                wallRunning = movementData.state.HasFlag(CharacterState.WallRunning),
                wallSide = movementData.state.HasFlag(CharacterState.WallRunning) && movementController != null ? movementController.wallRun.side : 0f,
                rollProgress = movementController != null ? movementController.roll.Progress : 0f,
                climbProgress = movementController != null && movementData.state.HasFlag(CharacterState.LedgeClimbing) ? movementController.ledge.ClimbProgress : 0f,
                hanging = movementData.state.HasFlag(CharacterState.LedgeHanging),
                climbing = movementData.state.HasFlag(CharacterState.LedgeClimbing),
                grabbing = movementData.state.HasFlag(CharacterState.Grabbing),
                ledgeEdge = ledgeEdge,
                ledgeNormal = ledgeNormal,
                wallSliding = wallSliding,
                wallPoint = wallSliding ? movementController.wallSlide.wallPoint : Vector3.zero,
                wallNormal = wallSliding ? movementController.wallSlide.wallNormal : Vector3.zero,
                scale = modelHost != null && modelHost.ModelParent != null && modelHost.ModelParent != transform
                    ? modelHost.ModelParent.localScale : Vector3.one,
                speedScale = speedScale,
            };
        }

        /// <summary>
        /// Turns the character's model (its model parent) after the character, which itself turns at once where it's
        /// steered, so movement stays responsive while the model turns naturally: easing into and out of each turn (a
        /// damped spring on its yaw), no faster than a speed that falls from brisk standing still to a sweeping turn at
        /// a run, and leaning into turns and with acceleration, as a runner does. Facings that abilities set (a dash,
        /// roll, wall run, ledge...) are turned to crisply, without leaning. Only the model turns and leans: the
        /// character's own transform, its collision and the camera following it don't.
        /// Call this every rendered frame (not from FixedUpdate), so the rotation updates smoothly rather than in
        /// physics-rate steps.
        /// </summary>
        /// <param name="deltaTime"></param>
        /// <param name="groundRotation">Rotation applied to the whole character by the ground they stand on since the
        /// last call (e.g. a spinning platform). The visuals take it on immediately instead of smoothing toward it, so
        /// they turn with the platform rather than lagging behind.</param>
        public void SmoothRotateVisuals(float deltaTime, Quaternion groundRotation)
        {
            // Turning the character's own transform would turn the whole character, not just its visuals.
            Transform modelParent = modelHost != null ? modelHost.ModelParent : null;
            if (modelParent == null || modelParent == transform || deltaTime <= 0f) { return; }

            CharacterAnimationState state = AnimationState;
            float targetYaw = transform.eulerAngles.y;
            if (!hasVisualYaw)
            {
                visualYaw = targetYaw;
                hasVisualYaw = true;
            }
            visualYaw += groundRotation.eulerAngles.y;

            // Turn: a critically damped spring, so a turn eases in and out, limited to a turn rate that slows with speed.
            bool abilityFacing = state.dashing || state.rolling || state.sliding || state.wallRunning || state.wallSliding
                                 || state.hanging || state.climbing || state.grabbing;
            float speedScale = state.speedScale > 0f ? state.speedScale : 1f;
            float authoredSpeed = state.horizontalSpeed / speedScale;
            float maxTurnSpeed = abilityFacing ? float.PositiveInfinity
                : Mathf.Lerp(maxTurnSpeedStanding, maxTurnSpeedMoving, fullTurnSpeedAt > 0f ? authoredSpeed / fullTurnSpeedAt : 1f);
            float smoothTime = abilityFacing ? abilityTurnTime : 1f / Mathf.Max(rotationFixSpeed, 0.01f);
            visualYaw = Mathf.SmoothDampAngle(visualYaw, targetYaw, ref yawVelocity, smoothTime, maxTurnSpeed, deltaTime);

            // Lean into turns and with acceleration, by a share of the angle that would balance it (as an authored-size
            // character would: speeds and accelerations as at the authored size), on the ground, not during abilities,
            // which have their own poses. (Not only while moving: walking stops dead, so the step that brakes is
            // already a standstill, and standing still leans nowhere anyway, with no speed or acceleration.)
            Vector3 horizontal = new Vector3(state.velocity.x, 0f, state.velocity.z) / speedScale;
            float smoothing = 1f - Mathf.Exp(-deltaTime / leanSmoothTime);
            if (hasLastVelocity)
            {
                Vector3 measured = (horizontal - lastHorizontalVelocity) / deltaTime;
                acceleration = Vector3.Lerp(acceleration, measured, smoothing);
            }
            smoothedVelocity = hasLastVelocity ? Vector3.Lerp(smoothedVelocity, horizontal, smoothing) : horizontal;
            // How fast the character's path is curving: its (smoothed) velocity's heading turning, in degrees per
            // second. Its own motion, not how fast the model turns (which its turn speed limits set).
            float pathHeading = Mathf.Atan2(smoothedVelocity.x, smoothedVelocity.z) * Mathf.Rad2Deg;
            pathTurnRate = hasLastVelocity && smoothedVelocity.sqrMagnitude > 0.09f
                ? Mathf.DeltaAngle(lastPathHeading, pathHeading) / deltaTime : 0f;
            lastPathHeading = pathHeading;
            lastHorizontalVelocity = horizontal;
            hasLastVelocity = true;
            bool leaning = state.touchingGround && !abilityFacing;
            float targetBank = 0f, targetPitch = 0f;
            if (leaning)
            {
                // Into the curve the character is actually running: its velocity projected onto the way the model faces
                // (so a sharp turn the model is still coming round to, which it isn't yet running into, leans little),
                // times how fast its path curves, is how hard it's being turned.
                Vector3 forward = Quaternion.Euler(0f, visualYaw, 0f) * Vector3.forward;
                float projectedSpeed = Mathf.Max(0f, Vector3.Dot(smoothedVelocity, forward));
                targetBank = turnLean * Mathf.Atan(projectedSpeed * pathTurnRate * Mathf.Deg2Rad / LeanGravity) * Mathf.Rad2Deg;
                targetPitch = accelerationLean * Mathf.Atan(Vector3.Dot(acceleration, forward) / LeanGravity) * Mathf.Rad2Deg;
            }
            // No further than the character's actual speed allows: a walk leans less than a sprint, however sharp the turn.
            // (Its speed smoothed, so stopping dead still leans back from the speed it was going.)
            float leanLimit = Mathf.Min(maxLean, maxLeanPerSpeed * smoothedVelocity.magnitude);
            bank = Mathf.SmoothDamp(bank, Mathf.Clamp(targetBank, -leanLimit, leanLimit), ref bankVelocity, leanSmoothTime, Mathf.Infinity, deltaTime);
            pitch = Mathf.SmoothDamp(pitch, Mathf.Clamp(targetPitch, -leanLimit, leanLimit), ref pitchVelocity, leanSmoothTime, Mathf.Infinity, deltaTime);

            // The model parent pivots at the character's feet, so leaning tilts the body over them. (Banking right is a
            // turn about the forward axis toward -z.)
            modelParent.rotation = Quaternion.Euler(0f, visualYaw, 0f) * Quaternion.Euler(pitch, 0f, -bank);
        }

        /// <summary>The model's facing (yaw, degrees) as SmoothRotateVisuals has turned it, without its lean.</summary>
        public float VisualYaw => visualYaw;
        /// <summary>How far the model leans into a turn (degrees, positive into a right turn) and forward (degrees, positive forward).</summary>
        public float Bank => bank;
        public float Pitch => pitch;
    }
}
