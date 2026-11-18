using System;
using System.Collections.Generic;
using Unity.Profiling;
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
    public partial class CameraRig : MonoBehaviour
    {
        private static readonly List<CameraRig> Rigs = new();

        [Tooltip("The transform to follow. Its origin should be at the feet.")]
        [SerializeField] private Transform target;
        [Tooltip("Optional: a transform whose scale is the target's size (a character's model parent, say). The third " +
                 "person pivot height, shoulder offset and distance, and the head bob, scale with it, so the camera frames " +
                 "a resized target as it did before. Its height (y) scales heights and distances, its width (x) the " +
                 "shoulder offset.")]
        [SerializeField] private Transform targetSizeReference;

        [Tooltip("Where camera input comes from, and the names of its actions.")]
        [SerializeField] private CameraInputSettings input = new();
        [Tooltip("Which view modes are allowed, the one it starts in, and the blend between them.")]
        [SerializeField] private CameraModeSettings modes = new();
        [Tooltip("How fast the mouse and stick turn the camera, inverting, and smoothing.")]
        [SerializeField] private CameraLookSettings look = new();
        [Tooltip("Locking the cursor during play, and releasing and re-locking it.")]
        [SerializeField] private CameraCursorSettings cursor = new();
        [Tooltip("The first person view: eye height and offset, field of view, pitch limits, hiding the target.")]
        [SerializeField] private FirstPersonSettings firstPerson = new();
        [Tooltip("The third person view: pivot, shoulder offset and swapping, field of view, pitch limits, follow smoothing, hiding the target up close.")]
        [SerializeField] private ThirdPersonSettings thirdPerson = new();
        [Tooltip("Zooming the third person camera in and out, and into first person.")]
        [SerializeField] private CameraZoomSettings zoom = new();
        [Tooltip("Aiming: how the button works, and the view while aimed.")]
        [SerializeField] private CameraAimSettings aim = new();
        [Tooltip("Keeping the camera in front of what's between it and the target.")]
        [SerializeField] private CameraCollisionSettings collision = new();
        [Tooltip("Following a target that moves in physics steps: interpolation, teleports, how far it may trail.")]
        [SerializeField] private CameraFollowSettings follow = new();
        [Tooltip("Widening the field of view as the target moves faster.")]
        [SerializeField] private SpeedFovSettings speedFov = new();
        [Tooltip("Bobbing the view while walking.")]
        [SerializeField] private HeadBobSettings headBob = new();
        [Tooltip("Dipping the camera when the target lands.")]
        [SerializeField] private LandingSettings landing = new();
        [Tooltip("Trauma-based camera shake.")]
        [SerializeField] private CameraShakeSettings shake = new();
        [Tooltip("Swinging the third person camera behind the target after a while without look input.")]
        [SerializeField] private RecenterSettings recenter = new();
        [Tooltip("Drawing the camera's pivot, shoulder point and collision probe in the Scene view.")]
        [SerializeField] private CameraDebugSettings debug = new();

        /// <summary>Raised whenever the active view mode changes (including when a CameraZone forces one).</summary>
        public event Action<CameraMode> ModeChanged;

        // Settings groups, exposed so other systems (e.g. an options menu) can tweak them at runtime.
        /// <summary>The look settings: sensitivity, inversion and smoothing.</summary>
        public CameraLookSettings Look => look;
        /// <summary>The view mode settings: allowed modes, starting mode and the blend between them.</summary>
        public CameraModeSettings Modes => modes;
        /// <summary>The first person view settings.</summary>
        public FirstPersonSettings FirstPerson => firstPerson;
        /// <summary>The third person view settings.</summary>
        public ThirdPersonSettings ThirdPerson => thirdPerson;
        /// <summary>The third person zoom settings.</summary>
        public CameraZoomSettings Zoom => zoom;
        /// <summary>The aiming settings.</summary>
        public CameraAimSettings Aim => aim;
        /// <summary>The settings that keep the third person camera out of walls.</summary>
        public CameraCollisionSettings Collision => collision;
        /// <summary>The settings that widen the field of view with the target's speed.</summary>
        public SpeedFovSettings SpeedFov => speedFov;
        /// <summary>The head bob settings.</summary>
        public HeadBobSettings HeadBob => headBob;
        /// <summary>The landing dip settings.</summary>
        public LandingSettings Landing => landing;
        /// <summary>The camera shake settings.</summary>
        public CameraShakeSettings Shake => shake;
        /// <summary>The settings for swinging the third person camera back behind the target.</summary>
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

        /// <summary>
        /// The transform being followed (its origin at its feet). Change it with SetTarget or Follow.
        /// </summary>
        public Transform Target => target;
        /// <summary>
        /// The transform whose scale is the target's size (see targetSizeReference). May be null: the target is then
        /// framed at size 1.
        /// </summary>
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
        /// <summary>
        /// Height of the third person pivot above the target's origin: the Pivot Height at the target's size, but no higher
        /// than its eyes when it has an eye anchor (see EyeHeight). So a target that lowers its eyes (ducking under
        /// something, lying down) is framed from lower down, and the camera's collision probe starts inside the room it's
        /// in, not in the low ceiling over it.
        /// </summary>
        public float PivotHeight
        {
            get
            {
                float height = thirdPerson.pivotHeight * TargetSize.y;
                return firstPerson.eyeAnchor != null && target != null ? Mathf.Min(height, EyeHeight) : height;
            }
        }
        /// <summary>The view mode currently in effect (a CameraZone may override the requested one).</summary>
        public CameraMode Mode => activeMode;
        /// <summary>The view mode last requested via input or SetMode, ignoring zone overrides.</summary>
        public CameraMode RequestedMode => requestedMode;
        /// <summary>0 = fully first person, 1 = fully third person, in between while transitioning.</summary>
        public float ThirdPersonBlend => modes.transitionCurve.Evaluate(modeProgress);
        /// <summary>Whether the camera is more than halfway into aiming.</summary>
        public bool IsAiming => aimProgress > 0.5f;
        /// <summary>
        /// The look direction's heading in degrees (0-360, round the world's up axis), without shake.
        /// </summary>
        public float Yaw => yaw;
        /// <summary>
        /// The look direction's pitch in degrees (positive looks down), without shake, within the current mode's pitch
        /// limits.
        /// </summary>
        public float Pitch => pitch;
        /// <summary>The look rotation, without shake. Prefer this over transform.rotation for aiming logic.</summary>
        public Quaternion LookRotation => Quaternion.Euler(pitch, yaw, 0f);
        /// <summary>
        /// The third person camera distance (m) zoomed to, before smoothing and before collision pulls it in.
        /// </summary>
        public float Distance => targetDistance;
        /// <summary>The current shake trauma, 0 (none) to 1 (full).</summary>
        public float Trauma => shaker.Trauma;
        /// <summary>How far (m) a landing has dipped the camera now (0 at rest; negative while dipped).</summary>
        public float LandingDip => landingOffset;

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

        /// <summary>Requests the other view mode. Returns false if that mode is disabled in the inspector.</summary>
        public bool ToggleMode() => SetMode(requestedMode == CameraMode.FirstPerson ? CameraMode.ThirdPerson : CameraMode.FirstPerson);

        /// <summary>
        /// Points the camera at the given yaw and pitch (degrees), clamping the pitch to the current mode's limits.
        /// </summary>
        public void SetLookRotation(float newYaw, float newPitch)
        {
            yaw = Mathf.Repeat(newYaw, 360f);
            pitch = ClampPitch(newPitch);
        }

        /// <summary>
        /// Sets the third person distance (m) to zoom to, clamped to the zoom settings' range. With instant, it skips
        /// the zoom smoothing.
        /// </summary>
        public void SetDistance(float newDistance, bool instant = false)
        {
            targetDistance = Mathf.Clamp(newDistance, zoom.minDistance, zoom.maxDistance);
            if (instant) { distance = targetDistance; distanceVelocity = 0f; }
        }

        /// <summary>
        /// Puts the third person camera over the right shoulder (true) or the left (false). It slides across over the
        /// shoulder swap time.
        /// </summary>
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

        // The Profiler's CPU Usage marker for updating the camera each frame (see docs/testing.md, Performance).
        private static readonly ProfilerMarker UpdateMarker = new("Camera.Update");

        // Landings are judged step by step (see CameraTargetTracker.Step).
        private void FixedUpdate()
        {
            if (target != null) { tracker.Step(target, follow, landing, SizeScale); }
        }

        private void LateUpdate()
        {
            using (UpdateMarker.Auto()) { UpdateCamera(); }
        }

        /// <summary>The camera's whole update for a frame: input, the target's tracking, the view mode, look, follow, collision and lens.</summary>
        private void UpdateCamera()
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

            Vector3 pivot = followPosition + up * (PivotHeight + landingOffset) + bob;
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
            Gizmos.DrawWireSphere(debugCamera, probeRadius > 0f ? probeRadius : collision.probeRadius * SizeScale);
        }

        [ContextMenu("Test Shake (0.6 trauma)")]
        private void TestShake() => AddTrauma(0.6f);
    }
}
