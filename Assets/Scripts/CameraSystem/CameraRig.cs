using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CameraSystem
{
    /// <summary>
    /// A first person / over-the-shoulder third person camera that follows any Transform.
    ///
    /// The rig is deliberately independent of the character code: it reads its own input actions, and infers the
    /// target's velocity, landings and so on purely from how the target's transform moves. Anything that wants
    /// camera-relative controls can simply read this camera's transform (e.g. via Camera.main).
    ///
    /// Put this on the camera itself and assign a target whose origin is at its feet.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(1000)] // After anything else that moves the target in LateUpdate.
    public class CameraRig : MonoBehaviour
    {
        private static readonly List<CameraRig> Rigs = new();

        [Tooltip("The transform to follow. Its origin should be at the feet.")]
        [SerializeField] private Transform target;
        [Tooltip("Optional: a transform whose scale is the target's size (a character's model parent, say). The third " +
                 "person pivot height, shoulder offset and distance, and the head bob, scale with it, so the camera frames " +
                 "a resized target as it did before. Its height (y) scales heights and distances, its width (x) the " +
                 "shoulder offset.")]
        [SerializeField] private Transform targetSizeReference;

        [SerializeField] private CameraInputSettings input = new();
        [SerializeField] private CameraModeSettings modes = new();
        [SerializeField] private CameraLookSettings look = new();
        [SerializeField] private CameraCursorSettings cursor = new();
        [SerializeField] private FirstPersonSettings firstPerson = new();
        [SerializeField] private ThirdPersonSettings thirdPerson = new();
        [SerializeField] private CameraZoomSettings zoom = new();
        [SerializeField] private CameraAimSettings aim = new();
        [SerializeField] private CameraCollisionSettings collision = new();
        [SerializeField] private CameraFollowSettings follow = new();
        [SerializeField] private SpeedFovSettings speedFov = new();
        [SerializeField] private HeadBobSettings headBob = new();
        [SerializeField] private LandingSettings landing = new();
        [SerializeField] private CameraShakeSettings shake = new();
        [SerializeField] private RecenterSettings recenter = new();
        [SerializeField] private CameraDebugSettings debug = new();

        /// <summary>Raised whenever the active view mode changes (including when a CameraZone forces one).</summary>
        public event Action<CameraMode> ModeChanged;

        // Settings groups, exposed so other systems (e.g. an options menu) can tweak them at runtime.
        public CameraLookSettings Look => look;
        public CameraModeSettings Modes => modes;
        public FirstPersonSettings FirstPerson => firstPerson;
        public ThirdPersonSettings ThirdPerson => thirdPerson;
        public CameraZoomSettings Zoom => zoom;
        public CameraAimSettings Aim => aim;
        public CameraCollisionSettings Collision => collision;
        public SpeedFovSettings SpeedFov => speedFov;
        public HeadBobSettings HeadBob => headBob;
        public LandingSettings Landing => landing;
        public CameraShakeSettings Shake => shake;
        public RecenterSettings Recenter => recenter;

        private Camera cam;
        // The camera's far clip plane as set up, for a target of normal size.
        private float baseFarClipPlane = 1000f;
        private readonly CameraInputReader reader = new();
        private readonly CameraTargetTracker tracker = new();
        private readonly CameraTargetVisibility visibility = new();
        private readonly CameraShake shaker = new();
        private bool started;

        // View mode
        private CameraMode requestedMode;
        private CameraMode activeMode;
        private float modeProgress; // 0 = first person, 1 = third person (linear; eased via the transition curve)

        // Look
        private float yaw, pitch;
        private Vector2 smoothedLookRate;
        private float timeSinceLookInput;
        private float recenterYawVelocity, recenterPitchVelocity;

        // Zoom & collision
        private float targetDistance, distance, distanceVelocity;
        private float collisionDistance, collisionVelocity;

        // Shoulder
        private bool rightShoulder;
        private bool autoSwapped;
        private float shoulderSide, shoulderVelocity;

        // Aim
        private bool aimToggled;
        private float aimProgress;

        // Follow
        private Vector3 followPosition;
        private Vector2 followHorizontalVelocity;
        private float followVerticalVelocity;

        // Effects
        private float speedFovAmount, speedFovVelocity;
        private float zoneFov, zoneFovVelocity;
        private float bobPhase, bobWeight, bobWeightVelocity;
        private float landingOffset, landingVelocity;

        private CameraZone activeZone;

        // Debug
        private Vector3 debugPivot, debugShoulder, debugCamera;

        #region Public API

        public Transform Target => target;
        public Transform TargetSizeReference { get => targetSizeReference; set => targetSizeReference = value; }

        /// <summary>
        /// The target's horizontal speed as it would be at its normal size: divided by its height. How fast something
        /// looks depends on its size (crossing its own height in a second looks the same at any size), and this matches
        /// how the character's movement scales (Proportional, in its MovementController), so a target at its own top
        /// speed gets the same speed effects at any size.
        /// </summary>
        public float RelativeSpeed => tracker.HorizontalSpeed / SizeScale;

        /// <summary>
        /// The target's height relative to its normal size (see TargetSize). Every distance and speed the camera uses
        /// (follow lag, landing dip, collision, clip planes, shake) is multiplied by it, so the camera frames and follows
        /// a target of any size the same way.
        /// </summary>
        public float SizeScale => Mathf.Max(TargetSize.y, 1e-4f);

        /// <summary>The target's size (1, 1, 1 without a size reference). See targetSizeReference.</summary>
        public Vector3 TargetSize
        {
            get
            {
                if (targetSizeReference == null) { return Vector3.one; }
                Vector3 size = targetSizeReference.lossyScale;
                return new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
            }
        }

        /// <summary>
        /// Height of the eyes above the target's origin: from the eye anchor if there is one (measured from the target's
        /// own position, which the anchor moves with), otherwise the first-person Eye Height setting.
        /// </summary>
        public float EyeHeight => firstPerson.eyeAnchor != null && target != null
            ? Vector3.Dot(firstPerson.eyeAnchor.position - target.position, Vector3.up)
            : firstPerson.eyeHeight;
        /// <summary>The view mode currently in effect (a CameraZone may override the requested one).</summary>
        public CameraMode Mode => activeMode;
        /// <summary>The view mode last requested via input or SetMode, ignoring zone overrides.</summary>
        public CameraMode RequestedMode => requestedMode;
        /// <summary>0 = fully first person, 1 = fully third person, in between while transitioning.</summary>
        public float ThirdPersonBlend => modes.transitionCurve.Evaluate(modeProgress);
        public bool IsAiming => aimProgress > 0.5f;
        public float Yaw => yaw;
        public float Pitch => pitch;
        /// <summary>The look rotation, without shake. Prefer this over transform.rotation for aiming logic.</summary>
        public Quaternion LookRotation => Quaternion.Euler(pitch, yaw, 0f);
        public float Distance => targetDistance;
        public float Trauma => shaker.Trauma;

        /// <summary>Follow a new target. Smoothing is snapped so the camera doesn't fly across the level.</summary>
        public void SetTarget(Transform newTarget, bool snap = true)
        {
            visibility.Restore();
            target = newTarget;
            visibility.Collect(target);
            tracker.Reset(target);
            if (snap) { Snap(); }
        }

        /// <summary>
        /// Follow a new target and look the way it faces, as the camera does at the start: for a target that's just
        /// appeared, like a spawned character. Takes one argument, so it can be wired to a UnityEvent in the inspector.
        /// </summary>
        public void Follow(Transform newTarget)
        {
            SetTarget(newTarget);
            if (newTarget != null) { SetLookRotation(newTarget.eulerAngles.y, pitch); }
        }

        /// <summary>
        /// Follow a new target as Follow(Transform) does, with its first-person eye anchor and size reference (see
        /// targetSizeReference) replacing the old target's. Either may be null: no eye anchor uses the fixed eye height,
        /// and no size reference frames the target at size 1. Wire it to a UnityEvent with these three arguments.
        /// </summary>
        public void Follow(Transform newTarget, Transform eyeAnchor, Transform sizeReference)
        {
            firstPerson.eyeAnchor = eyeAnchor;
            targetSizeReference = sizeReference; // Before snapping to the target, which frames it at this size.
            Follow(newTarget);
        }

        /// <summary>Requests a view mode. Returns false if that mode is disabled in the inspector.</summary>
        public bool SetMode(CameraMode mode)
        {
            if (!IsModeAllowed(mode)) { return false; }
            requestedMode = mode;
            return true;
        }

        public bool ToggleMode() => SetMode(requestedMode == CameraMode.FirstPerson ? CameraMode.ThirdPerson : CameraMode.FirstPerson);

        public void SetLookRotation(float newYaw, float newPitch)
        {
            yaw = Mathf.Repeat(newYaw, 360f);
            pitch = ClampPitch(newPitch);
        }

        public void SetDistance(float newDistance, bool instant = false)
        {
            targetDistance = Mathf.Clamp(newDistance, zoom.minDistance, zoom.maxDistance);
            if (instant) { distance = targetDistance; distanceVelocity = 0f; }
        }

        public void SetShoulder(bool right) => rightShoulder = right;

        /// <summary>Adds shake trauma (0-1) to this camera.</summary>
        public void AddTrauma(float amount) => shaker.AddTrauma(amount);

        /// <summary>Adds shake trauma to every active CameraRig. Handy for explosions etc. without a reference.</summary>
        public static void ShakeAll(float amount)
        {
            foreach (var rig in Rigs) { rig.AddTrauma(amount); }
        }

        /// <summary>Skips all smoothing this frame, jumping the camera straight to where it should be.</summary>
        public void Snap()
        {
            followPosition = tracker.Position;
            followHorizontalVelocity = Vector2.zero;
            followVerticalVelocity = 0f;
            distance = targetDistance;
            distanceVelocity = 0f;
            collisionDistance = targetDistance * SizeScale; // In world units, like the distance it's pulled in to.
            collisionVelocity = 0f;
            shoulderSide = rightShoulder ? 1f : -1f;
            shoulderVelocity = 0f;
            landingOffset = landingVelocity = 0f;
            modeProgress = activeMode == CameraMode.ThirdPerson ? 1f : 0f;
        }

        #endregion

        #region Unity lifecycle

        private void Awake()
        {
            cam = GetComponent<Camera>();
            if (cam != null) { baseFarClipPlane = cam.farClipPlane; }
        }

        private void OnEnable()
        {
            Rigs.Add(this);
            if (started) { reader.Bind(input); }
        }

        private void Start()
        {
            // Bind in Start rather than Awake/OnEnable so PlayerInput has set up its actions first.
            reader.Bind(input);
            started = true;

            requestedMode = IsModeAllowed(modes.startMode) ? modes.startMode : OtherMode(modes.startMode);
            activeMode = requestedMode;
            rightShoulder = thirdPerson.startOnRightShoulder;
            targetDistance = Mathf.Clamp(zoom.defaultDistance, zoom.minDistance, zoom.maxDistance);

            // Start looking the way the target faces, keeping the camera's own pitch.
            float startYaw = target != null ? target.eulerAngles.y : transform.eulerAngles.y;
            SetLookRotation(startYaw, Mathf.DeltaAngle(0f, transform.eulerAngles.x));

            if (target == null)
            {
                Debug.LogWarning("[CameraRig] No target assigned; the camera will stay where it is until SetTarget is called.", this);
            }

            visibility.Collect(target);
            tracker.Reset(target);
            Snap();

            if (cursor.manageCursor) { LockCursor(true); }
        }

        private void OnDisable()
        {
            Rigs.Remove(this);
            reader.Unbind();
            visibility.Restore();
            if (cursor.manageCursor) { LockCursor(false); }
        }

        private void OnValidate()
        {
            if (!modes.allowFirstPerson && !modes.allowThirdPerson) { modes.allowThirdPerson = true; }
            zoom.maxDistance = Mathf.Max(zoom.maxDistance, zoom.minDistance);
            zoom.defaultDistance = Mathf.Clamp(zoom.defaultDistance, zoom.minDistance, zoom.maxDistance);
            speedFov.maxSpeed = Mathf.Max(speedFov.maxSpeed, speedFov.minSpeed);
        }

        private void LateUpdate()
        {
            float dt = Time.deltaTime;

            UpdateCursor();

            if (target == null) { return; }

            tracker.Update(target, follow, landing, SizeScale);
            if (tracker.TeleportedThisFrame) { Snap(); }

            activeZone = CameraZone.Find(tracker.Position);

            bool allowMouseLook = !cursor.manageCursor || !cursor.ignoreMouseWhileUnlocked || Cursor.lockState == CursorLockMode.Locked;
            CameraInputFrame frame = reader.Read(look, zoom, allowMouseLook, dt);

            HandleModeInput(frame);
            UpdateModeTransition(dt);
            float tpBlend = ThirdPersonBlend;

            HandleZoomInput(frame);
            HandleAimInput(frame, dt);
            if (frame.swapShoulderPressed && activeMode == CameraMode.ThirdPerson) { rightShoulder = !rightShoulder; }

            UpdateLook(frame, tpBlend, dt);
            UpdateRecenter(dt);
            UpdateFollow(tpBlend, dt);
            UpdateLanding(dt);
            Vector3 bob = UpdateHeadBob(tpBlend, dt);

            // --- Compose the final pose ---
            Quaternion lookRotation = LookRotation;
            Quaternion yawRotation = Quaternion.Euler(0f, yaw, 0f);
            Vector3 up = Vector3.up;

            Vector3 firstPersonPosition = followPosition
                + up * (EyeHeight + landingOffset)
                + yawRotation * Vector3.forward * (firstPerson.eyeForwardOffset * TargetSize.z)
                + bob;

            Vector3 pivot = followPosition + up * (thirdPerson.pivotHeight * TargetSize.y + landingOffset) + bob;
            Vector3 thirdPersonPosition = ComputeThirdPersonPosition(pivot, lookRotation, yawRotation, dt);

            Vector3 position = Vector3.LerpUnclamped(firstPersonPosition, thirdPersonPosition, tpBlend);
            Quaternion rotation = lookRotation;

            shaker.Evaluate(shake, dt, out Vector3 shakeOffset, out Quaternion shakeRotation);
            position += rotation * (shakeOffset * SizeScale);
            rotation *= shakeRotation;

            transform.SetPositionAndRotation(position, rotation);

            UpdateLens(tpBlend, dt);
            UpdateTargetVisibility(tpBlend, position, pivot);

            debugPivot = pivot;
            debugCamera = position;
        }

        #endregion

        #region Mode

        private bool IsModeAllowed(CameraMode mode) =>
            mode == CameraMode.FirstPerson ? modes.allowFirstPerson : modes.allowThirdPerson;

        private static CameraMode OtherMode(CameraMode mode) =>
            mode == CameraMode.FirstPerson ? CameraMode.ThirdPerson : CameraMode.FirstPerson;

        private bool ModeLockedByZone => activeZone != null && activeZone.forceMode;

        private void HandleModeInput(CameraInputFrame frame)
        {
            if (frame.toggleViewPressed && !ModeLockedByZone) { ToggleMode(); }

            CameraMode newMode = ModeLockedByZone ? activeZone.mode : requestedMode;
            if (newMode != activeMode)
            {
                activeMode = newMode;
                ModeChanged?.Invoke(activeMode);
            }
        }

        private void UpdateModeTransition(float dt)
        {
            float goal = activeMode == CameraMode.ThirdPerson ? 1f : 0f;
            modeProgress = modes.transitionDuration > 0f
                ? Mathf.MoveTowards(modeProgress, goal, dt / modes.transitionDuration)
                : goal;
        }

        #endregion

        #region Input-driven state

        private float MaxDistance => activeZone != null && activeZone.limitDistance
            ? Mathf.Clamp(activeZone.maxDistance, zoom.minDistance, zoom.maxDistance)
            : zoom.maxDistance;

        private void HandleZoomInput(CameraInputFrame frame)
        {
            if (zoom.enabled && Mathf.Abs(frame.zoomDelta) > 1e-5f)
            {
                bool canSwitch = zoom.zoomIntoFirstPerson && !ModeLockedByZone;
                if (activeMode == CameraMode.ThirdPerson)
                {
                    bool atMinimum = targetDistance <= zoom.minDistance + 1e-3f;
                    if (frame.zoomDelta < 0f && atMinimum && canSwitch && modes.allowFirstPerson)
                    {
                        SetMode(CameraMode.FirstPerson);
                    }
                    else
                    {
                        targetDistance = Mathf.Clamp(targetDistance + frame.zoomDelta, zoom.minDistance, MaxDistance);
                    }
                }
                else if (frame.zoomDelta > 0f && canSwitch && modes.allowThirdPerson)
                {
                    targetDistance = zoom.minDistance;
                    SetMode(CameraMode.ThirdPerson);
                }
            }

            float goal = Mathf.Min(targetDistance, MaxDistance);
            distance = zoom.smoothTime > 0f
                ? Mathf.SmoothDamp(distance, goal, ref distanceVelocity, zoom.smoothTime)
                : goal;
        }

        private void HandleAimInput(CameraInputFrame frame, float dt)
        {
            bool aiming = false;
            if (aim.enabled)
            {
                if (aim.inputMode == AimInputMode.Toggle)
                {
                    if (frame.aimPressed) { aimToggled = !aimToggled; }
                    aiming = aimToggled;
                }
                else
                {
                    aiming = frame.aimHeld;
                }
            }
            else
            {
                aimToggled = false;
            }

            float goal = aiming ? 1f : 0f;
            aimProgress = aim.blendTime > 0f ? Mathf.MoveTowards(aimProgress, goal, dt / aim.blendTime) : goal;
        }

        private float AimBlend => Mathf.SmoothStep(0f, 1f, aimProgress);

        private float ClampPitch(float value)
        {
            float t = ThirdPersonBlend;
            float min = Mathf.Lerp(firstPerson.minPitch, thirdPerson.minPitch, t);
            float max = Mathf.Lerp(firstPerson.maxPitch, thirdPerson.maxPitch, t);
            return Mathf.Clamp(value, min, max);
        }

        private void UpdateLook(CameraInputFrame frame, float tpBlend, float dt)
        {
            Vector2 degrees = frame.lookDegrees * Mathf.Lerp(1f, aim.sensitivityMultiplier, AimBlend);

            if (look.smoothing > 0f && dt > 0f)
            {
                // Smooth the rotation *rate* so the result is frame-rate independent.
                Vector2 rate = degrees / dt;
                smoothedLookRate = Vector2.Lerp(smoothedLookRate, rate, 1f - Mathf.Exp(-dt / look.smoothing));
                degrees = smoothedLookRate * dt;
            }
            else
            {
                smoothedLookRate = Vector2.zero;
            }

            yaw = Mathf.Repeat(yaw + degrees.x, 360f);
            pitch = ClampPitch(pitch + degrees.y);
            timeSinceLookInput = frame.hasLookInput ? 0f : timeSinceLookInput + dt;
        }

        private void UpdateRecenter(float dt)
        {
            bool active = recenter.enabled
                && activeMode == CameraMode.ThirdPerson
                && timeSinceLookInput >= recenter.delay
                && aimProgress <= 0f
                && tracker.HorizontalSpeed >= recenter.minTargetSpeed;

            float headingYaw = yaw;
            if (active)
            {
                if (recenter.heading == RecenterHeading.MovementDirection)
                {
                    Vector3 v = tracker.Velocity;
                    if (new Vector2(v.x, v.z).sqrMagnitude < 0.01f) { active = false; }
                    else { headingYaw = Mathf.Atan2(v.x, v.z) * Mathf.Rad2Deg; }
                }
                else
                {
                    Vector3 f = Vector3.ProjectOnPlane(target.forward, Vector3.up);
                    if (f.sqrMagnitude < 1e-4f) { active = false; }
                    else { headingYaw = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg; }
                }

                // Don't spin the camera around when the target walks back toward it.
                if (Mathf.Abs(Mathf.DeltaAngle(yaw, headingYaw)) > 135f) { active = false; }
            }

            if (!active)
            {
                recenterYawVelocity = recenterPitchVelocity = 0f;
                return;
            }

            yaw = Mathf.Repeat(Mathf.SmoothDampAngle(yaw, headingYaw, ref recenterYawVelocity, recenter.smoothTime, Mathf.Infinity, dt), 360f);
            if (recenter.recenterPitch)
            {
                pitch = ClampPitch(Mathf.SmoothDamp(pitch, recenter.restingPitch, ref recenterPitchVelocity, recenter.smoothTime, Mathf.Infinity, dt));
            }
        }

        #endregion

        #region Follow & effects

        private void UpdateFollow(float tpBlend, float dt)
        {
            Vector3 goal = tracker.Position;
            float horizontalDamping = Mathf.Lerp(0f, thirdPerson.horizontalDamping, tpBlend);
            float verticalDamping = Mathf.Lerp(firstPerson.verticalDamping, thirdPerson.verticalDamping, tpBlend);

            Vector2 current = new Vector2(followPosition.x, followPosition.z);
            Vector2 goalH = new Vector2(goal.x, goal.z);
            current = horizontalDamping > 0f
                ? Vector2.SmoothDamp(current, goalH, ref followHorizontalVelocity, horizontalDamping, Mathf.Infinity, dt)
                : goalH;
            current = goalH + Vector2.ClampMagnitude(current - goalH, follow.maxHorizontalLag * SizeScale);

            float y = verticalDamping > 0f
                ? Mathf.SmoothDamp(followPosition.y, goal.y, ref followVerticalVelocity, verticalDamping, Mathf.Infinity, dt)
                : goal.y;
            float verticalLag = follow.maxVerticalLag * SizeScale;
            y = Mathf.Clamp(y, goal.y - verticalLag, goal.y + verticalLag);

            followPosition = new Vector3(current.x, y, current.y);
        }

        private void UpdateLanding(float dt)
        {
            if (!landing.enabled)
            {
                landingOffset = landingVelocity = 0f;
                return;
            }

            // Judged as for a target of normal size (speeds divided by its size), and the dip scaled back up to its size.
            float size = SizeScale;
            float impact = tracker.LandingSpeedThisFrame / size - landing.minImpactSpeed;
            if (tracker.LandingSpeedThisFrame > 0f && impact > 0f)
            {
                float dip = Mathf.Min(impact * landing.dipPerSpeed, landing.maxDip) * size;
                // For a roughly critically damped spring kicked with velocity v, the peak displacement is about
                // v / (omega * e), so choose v to make the peak match the requested dip.
                landingVelocity -= dip * Mathf.Sqrt(landing.springStiffness) * (float)Math.E;
                shaker.AddTrauma(impact * landing.traumaPerSpeed);
            }

            // Semi-implicit Euler with substeps so the spring stays stable at low frame rates.
            const float maxStep = 1f / 120f;
            float remaining = dt;
            while (remaining > 1e-6f)
            {
                float step = Mathf.Min(remaining, maxStep);
                float acceleration = -landing.springStiffness * landingOffset - landing.springDamping * landingVelocity;
                landingVelocity += acceleration * step;
                landingOffset += landingVelocity * step;
                remaining -= step;
            }
            landingOffset = Mathf.Max(landingOffset, -landing.maxDip * 1.5f * size);
        }

        private Vector3 UpdateHeadBob(float tpBlend, float dt)
        {
            float scale = Mathf.Lerp(1f, headBob.applyInThirdPerson ? headBob.thirdPersonScale : 0f, tpBlend);
            if (!headBob.enabled || scale <= 0f)
            {
                bobWeight = bobWeightVelocity = 0f;
                return Vector3.zero;
            }

            float speed = tracker.HorizontalSpeed;
            bool walking = tracker.ProbablyGrounded && RelativeSpeed > 0.1f;
            float goalWeight = walking ? Mathf.Clamp01(RelativeSpeed / headBob.fullAmplitudeSpeed) : 0f;
            bobWeight = headBob.fadeTime > 0f
                ? Mathf.SmoothDamp(bobWeight, goalWeight, ref bobWeightVelocity, headBob.fadeTime, Mathf.Infinity, dt)
                : goalWeight;

            // A bigger target takes longer strides, so it bobs fewer times per meter, and further.
            Vector3 size = TargetSize;
            if (walking) { bobPhase += speed * headBob.cyclesPerMeter / size.y * dt * Mathf.PI * 2f; }
            bobPhase = Mathf.Repeat(bobPhase, Mathf.PI * 2f);

            // One side-to-side sway per cycle, and one up/down bounce per footstep (two per cycle).
            Vector3 local = new Vector3(
                Mathf.Sin(bobPhase) * headBob.amplitude.x,
                Mathf.Cos(bobPhase * 2f) * headBob.amplitude.y,
                0f);
            return Quaternion.Euler(0f, yaw, 0f) * Vector3.Scale(local, size) * (bobWeight * scale);
        }

        private void UpdateLens(float tpBlend, float dt)
        {
            float fov = Mathf.Lerp(firstPerson.fieldOfView, thirdPerson.fieldOfView, tpBlend);
            float aimBlend = AimBlend;
            fov = Mathf.Lerp(fov, aim.fieldOfView, aimBlend);

            float speedGoal = speedFov.enabled
                ? Mathf.InverseLerp(speedFov.minSpeed, speedFov.maxSpeed, RelativeSpeed) * speedFov.extraFov
                : 0f;
            speedFovAmount = speedFov.smoothTime > 0f
                ? Mathf.SmoothDamp(speedFovAmount, speedGoal, ref speedFovVelocity, speedFov.smoothTime, Mathf.Infinity, dt)
                : speedGoal;
            fov += speedFovAmount * (1f - aimBlend);

            zoneFov = Mathf.SmoothDamp(zoneFov, activeZone != null ? activeZone.fovOffset : 0f, ref zoneFovVelocity, 0.3f, Mathf.Infinity, dt);
            fov += zoneFov;

            cam.fieldOfView = Mathf.Clamp(fov, 1f, 179f);
            // Clip planes follow the target's size: near, so a small target's close-up camera doesn't cut into it, and
            // far for a bigger one, so it sees as far for its size. (Not nearer for a small one: the world stays in view.)
            cam.nearClipPlane = Mathf.Lerp(firstPerson.nearClipPlane, thirdPerson.nearClipPlane, tpBlend) * SizeScale;
            cam.farClipPlane = baseFarClipPlane * Mathf.Max(1f, SizeScale);
        }

        private void UpdateTargetVisibility(float tpBlend, Vector3 cameraPosition, Vector3 pivot)
        {
            TargetHideMode mode = TargetHideMode.None;
            if (tpBlend < 0.1f)
            {
                mode = firstPerson.hideTarget;
            }
            else if (thirdPerson.hideWhenClose != TargetHideMode.None
                     && (cameraPosition - pivot).sqrMagnitude < Square(thirdPerson.hideDistance * SizeScale))
            {
                mode = thirdPerson.hideWhenClose;
            }
            visibility.RefreshIfChanged();
            visibility.Apply(mode);
        }

        #endregion

        #region Third person placement & collision

        private Vector3 ComputeThirdPersonPosition(Vector3 pivot, Quaternion lookRotation, Quaternion yawRotation, float dt)
        {
            float aimBlend = AimBlend;
            Vector3 size = TargetSize;
            float shoulderX = thirdPerson.shoulderOffset.x * size.x * Mathf.Lerp(1f, aim.shoulderMultiplier, aimBlend);
            Vector3 right = yawRotation * Vector3.right;
            Vector3 lift = Vector3.up * (thirdPerson.shoulderOffset.y * size.y);

            // Pick the side, auto-swapping temporarily if the preferred shoulder is pressed against a wall.
            float preferredSide = rightShoulder ? 1f : -1f;
            float side = preferredSide;
            if (thirdPerson.autoSwapWhenBlocked && collision.enabled && shoulderX > 1e-3f)
            {
                float preferredFree = SweepFraction(pivot, pivot + right * (shoulderX * preferredSide) + lift);
                if (autoSwapped)
                {
                    // Hysteresis: only return once the preferred side is clearly free again.
                    if (preferredFree > 0.9f) { autoSwapped = false; }
                }
                else if (preferredFree < 0.5f)
                {
                    float otherFree = SweepFraction(pivot, pivot - right * (shoulderX * preferredSide) + lift);
                    autoSwapped = otherFree > 0.9f;
                }
                if (autoSwapped) { side = -preferredSide; }
            }
            else
            {
                autoSwapped = false;
            }

            shoulderSide = thirdPerson.shoulderSwapTime > 0f
                ? Mathf.SmoothDamp(shoulderSide, side, ref shoulderVelocity, thirdPerson.shoulderSwapTime, Mathf.Infinity, dt)
                : side;

            Vector3 desiredShoulder = pivot + right * (shoulderX * shoulderSide) + lift;
            Vector3 shoulder = pivot + (desiredShoulder - pivot) * SweepFraction(pivot, desiredShoulder);
            debugShoulder = shoulder;

            // Pull the camera in front of anything between the shoulder point and the desired camera position.
            // Zoom works in the target's size, so a bigger target is framed the same way from further away.
            float desiredDistance = distance * size.y * Mathf.Lerp(1f, aim.distanceMultiplier, aimBlend);
            Vector3 back = lookRotation * Vector3.back;
            float allowed = desiredDistance;
            if (collision.enabled && Sweep(shoulder, back, desiredDistance, out float hitDistance))
            {
                allowed = Mathf.Max(collision.minDistance * size.y, hitDistance);
            }

            if (allowed < collisionDistance || collision.returnSmoothTime <= 0f)
            {
                collisionDistance = allowed; // Pull in instantly so geometry never clips into view.
                collisionVelocity = 0f;
            }
            else
            {
                collisionDistance = Mathf.SmoothDamp(collisionDistance, allowed, ref collisionVelocity, collision.returnSmoothTime, Mathf.Infinity, dt);
            }

            return shoulder + back * collisionDistance;
        }

        private static float Square(float value) => value * value;

        /// <summary>Fraction (0-1) of the way from 'from' to 'to' the collision probe can travel.</summary>
        private float SweepFraction(Vector3 from, Vector3 to)
        {
            if (!collision.enabled) { return 1f; }
            Vector3 delta = to - from;
            float length = delta.magnitude;
            if (length < 1e-5f) { return 1f; }
            return Sweep(from, delta / length, length, out float hit) ? Mathf.Clamp01(hit / length) : 1f;
        }

        // Reused for every collision sweep, so sweeping doesn't allocate a new array each time.
        private readonly RaycastHit[] sweepHits = new RaycastHit[32];

        /// <summary>Sphere-casts the collision probe, ignoring the target's own colliders. Returns the safe travel distance.</summary>
        private bool Sweep(Vector3 origin, Vector3 direction, float maxDistance, out float safeDistance)
        {
            safeDistance = maxDistance;
            int count = Physics.SphereCastNonAlloc(origin, collision.probeRadius * SizeScale, direction, sweepHits, maxDistance, collision.layers, collision.triggerInteraction);
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = sweepHits[i];
                if (target != null && hit.transform.IsChildOf(target)) { continue; }
                // A distance of 0 means the probe started inside geometry: treat as fully blocked.
                float d = hit.distance;
                if (d < safeDistance) { safeDistance = d; found = true; }
            }
            return found;
        }

        #endregion

        #region Cursor

        private void UpdateCursor()
        {
            if (!cursor.manageCursor) { return; }

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && cursor.unlockKey != Key.None && keyboard[cursor.unlockKey].wasPressedThisFrame)
            {
                LockCursor(false);
            }
            else if (cursor.relockOnClick && Cursor.lockState != CursorLockMode.Locked
                     && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                LockCursor(true);
            }
        }

        private static void LockCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        #endregion

        private void OnDrawGizmos()
        {
            if (!debug.drawGizmos || !Application.isPlaying || target == null) { return; }

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(debugPivot, 0.08f);
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(debugPivot, debugShoulder);
            Gizmos.DrawWireSphere(debugShoulder, 0.06f);
            Gizmos.color = autoSwapped ? new Color(1f, 0.5f, 0f) : Color.green;
            Gizmos.DrawLine(debugShoulder, debugCamera);
            Gizmos.DrawWireSphere(debugCamera, collision.probeRadius * SizeScale);
        }

        [ContextMenu("Test Shake (0.6 trauma)")]
        private void TestShake() => AddTrauma(0.6f);
    }
}
