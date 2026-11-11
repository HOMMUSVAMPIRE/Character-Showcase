using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CameraSystem
{
    /// <summary>The two view modes the camera rig can be in.</summary>
    public enum CameraMode
    {
        FirstPerson,
        ThirdPerson,
    }

    /// <summary>How the followed target's renderers are treated when the camera would otherwise see inside them.</summary>
    public enum TargetHideMode
    {
        [Tooltip("Leave the target fully visible.")]
        None,
        [Tooltip("Stop rendering the target entirely.")]
        Hide,
        [Tooltip("Stop rendering the target but keep its shadow.")]
        ShadowsOnly,
    }

    public enum AimInputMode
    {
        [Tooltip("Aim while the aim button is held.")]
        Hold,
        [Tooltip("Each press of the aim button toggles aiming on or off.")]
        Toggle,
    }

    public enum RecenterHeading
    {
        [Tooltip("Swing behind the direction the target is moving.")]
        MovementDirection,
        [Tooltip("Swing behind the direction the target's transform is facing.")]
        TargetForward,
    }

    // Each settings group below is drawn as its own foldout in the CameraRig inspector.

    [Serializable]
    public class CameraInputSettings
    {
        [Tooltip("PlayerInput to read camera actions from. If empty, the first PlayerInput in the scene is used.")]
        public PlayerInput playerInput;
        [Tooltip("Optional action asset to read from instead of a PlayerInput. Its actions are enabled automatically.")]
        public InputActionAsset actionsOverride;

        [Header("Action names")]
        [Tooltip("Vector2 action used to rotate the camera (mouse delta / right stick).")]
        public string lookAction = "Look";
        [Tooltip("Axis action used to zoom in and out (scroll wheel / d-pad up & down). Positive zooms in.")]
        public string zoomAction = "Zoom";
        [Tooltip("Button action that switches between first and third person.")]
        public string toggleViewAction = "ToggleView";
        [Tooltip("Button action that swaps the over-the-shoulder side.")]
        public string swapShoulderAction = "SwapShoulder";
        [Tooltip("Button action used to aim (zooms the view in and lowers sensitivity).")]
        public string aimAction = "Aim";
    }

    [Serializable]
    public class CameraLookSettings
    {
        [Tooltip("Degrees of rotation per pixel of mouse movement.")]
        [Min(0f)] public float mouseSensitivity = 0.12f;
        [Tooltip("Degrees per second of rotation at full gamepad stick deflection.")]
        [Min(0f)] public float gamepadSensitivity = 200f;
        [Tooltip("Response curve exponent for the gamepad stick. 1 = linear, higher = finer control near the center.")]
        [Range(1f, 4f)] public float gamepadResponseExponent = 1.6f;
        [Tooltip("Ratio of vertical to horizontal sensitivity.")]
        [Range(0.1f, 2f)] public float verticalSensitivityScale = 0.8f;
        public bool invertX = false;
        public bool invertY = false;
        [Tooltip("Smooths rotation input over time. 0 disables smoothing (most responsive).")]
        [Range(0f, 0.2f)] public float smoothing = 0f;
    }

    [Serializable]
    public class CameraCursorSettings
    {
        [Tooltip("Lock and hide the cursor during play, with a key to release it and a click to re-lock it.")]
        public bool manageCursor = true;
        [Tooltip("Key that releases the cursor.")]
        public Key unlockKey = Key.Escape;
        [Tooltip("Clicking in the game view while the cursor is free re-locks it.")]
        public bool relockOnClick = true;
        [Tooltip("Ignore mouse look input while the cursor is unlocked.")]
        public bool ignoreMouseWhileUnlocked = true;
    }

    [Serializable]
    public class CameraModeSettings
    {
        [Tooltip("The view mode the camera starts in.")]
        public CameraMode startMode = CameraMode.ThirdPerson;
        public bool allowFirstPerson = true;
        public bool allowThirdPerson = true;
        [Tooltip("Seconds taken to blend between first and third person.")]
        [Min(0f)] public float transitionDuration = 0.35f;
        [Tooltip("Easing applied to the first/third person blend (0 = first person, 1 = third person).")]
        public AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    }

    [Serializable]
    public class FirstPersonSettings
    {
        [Tooltip("Height of the eyes above the target's origin.")]
        public float eyeHeight = 1.7f;
        [Tooltip("Optional: the eyes sit at this transform's height above the target's origin instead of Eye Height, so a " +
                 "target whose size can change (a swappable character model, say) can keep the view at its eyes.")]
        public Transform eyeAnchor;
        [Tooltip("Forward offset of the eyes from the target's origin, so the view isn't from the center of the head.")]
        public float eyeForwardOffset = 0.1f;
        [Range(20f, 120f)] public float fieldOfView = 75f;
        [Range(-89f, 0f)] public float minPitch = -85f;
        [Range(0f, 89f)] public float maxPitch = 85f;
        [Min(0.01f)] public float nearClipPlane = 0.05f;
        [Tooltip("How the target's own renderers are handled in first person.")]
        public TargetHideMode hideTarget = TargetHideMode.ShadowsOnly;
        [Tooltip("Seconds to smooth vertical eye movement (e.g. stepping up stairs). 0 = no smoothing.")]
        [Range(0f, 0.3f)] public float verticalDamping = 0.05f;
    }

    [Serializable]
    public class ThirdPersonSettings
    {
        [Tooltip("Height above the target's origin that the camera orbits around.")]
        public float pivotHeight = 1.6f;
        [Tooltip("Over-the-shoulder offset: x = sideways distance, y = extra height.")]
        public Vector2 shoulderOffset = new Vector2(0.6f, 0.1f);
        [Tooltip("Start on the right shoulder (otherwise the left).")]
        public bool startOnRightShoulder = true;
        [Tooltip("Seconds taken to slide to the other shoulder.")]
        [Min(0f)] public float shoulderSwapTime = 0.2f;
        [Tooltip("Swap shoulders temporarily when the current side is blocked by a wall and the other is free.")]
        public bool autoSwapWhenBlocked = true;
        [Range(20f, 120f)] public float fieldOfView = 60f;
        [Range(-89f, 0f)] public float minPitch = -60f;
        [Range(0f, 89f)] public float maxPitch = 75f;
        [Min(0.01f)] public float nearClipPlane = 0.1f;
        [Tooltip("Seconds to smooth horizontal follow. 0 = locked to the target.")]
        [Range(0f, 0.5f)] public float horizontalDamping = 0.04f;
        [Tooltip("Seconds to smooth vertical follow, so jumps and steps don't jolt the view.")]
        [Range(0f, 0.5f)] public float verticalDamping = 0.15f;
        [Tooltip("How the target's renderers are handled when the camera gets very close to it.")]
        public TargetHideMode hideWhenClose = TargetHideMode.Hide;
        [Tooltip("Camera-to-pivot distance below which the target is hidden.")]
        [Min(0f)] public float hideDistance = 0.6f;
    }

    [Serializable]
    public class CameraZoomSettings
    {
        public bool enabled = true;
        [Min(0.1f)] public float defaultDistance = 3.5f;
        [Min(0.1f)] public float minDistance = 1.5f;
        [Min(0.1f)] public float maxDistance = 8f;
        [Tooltip("Distance zoomed per scroll-wheel notch.")]
        [Min(0f)] public float scrollStep = 0.75f;
        [Tooltip("Distance zoomed per second at full analog input (gamepad).")]
        [Min(0f)] public float analogSpeed = 6f;
        [Tooltip("Seconds to smooth zoom changes.")]
        [Range(0f, 0.5f)] public float smoothTime = 0.12f;
        [Tooltip("Zooming in past the minimum distance switches to first person; zooming out of first person switches back.")]
        public bool zoomIntoFirstPerson = true;
    }

    [Serializable]
    public class CameraAimSettings
    {
        public bool enabled = true;
        public AimInputMode inputMode = AimInputMode.Hold;
        [Tooltip("Field of view while fully aimed (both modes).")]
        [Range(10f, 120f)] public float fieldOfView = 40f;
        [Tooltip("Third person distance multiplier while aiming.")]
        [Range(0.1f, 1f)] public float distanceMultiplier = 0.55f;
        [Tooltip("Third person shoulder offset multiplier while aiming.")]
        [Range(0f, 2f)] public float shoulderMultiplier = 1.15f;
        [Tooltip("Look sensitivity multiplier while aiming.")]
        [Range(0.05f, 1f)] public float sensitivityMultiplier = 0.5f;
        [Tooltip("Seconds to blend into and out of aiming.")]
        [Min(0f)] public float blendTime = 0.12f;
    }

    [Serializable]
    public class CameraCollisionSettings
    {
        [Tooltip("Pull the camera in front of obstacles between it and the target.")]
        public bool enabled = true;
        [Tooltip("Layers the camera collides with. The target's own colliders are always ignored.")]
        public LayerMask layers = ~0;
        [Tooltip("Radius of the sphere swept from the pivot to the camera.")]
        [Min(0.01f)] public float probeRadius = 0.2f;
        [Tooltip("Closest the camera may be pulled to the pivot.")]
        [Min(0f)] public float minDistance = 0.25f;
        [Tooltip("Seconds to ease back out once an obstacle is gone. Pulling in is always instant to avoid clipping.")]
        [Range(0f, 1f)] public float returnSmoothTime = 0.25f;
        public QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;
    }

    [Serializable]
    public class CameraFollowSettings
    {
        [Tooltip("Interpolate the target between physics steps. Removes camera stutter when the target moves in FixedUpdate " +
                 "without Rigidbody interpolation. Adds up to one physics step of latency. Skipped automatically when the " +
                 "target's Rigidbody already interpolates. Note this only smooths the camera: the target itself will " +
                 "still visibly step unless its Rigidbody interpolates, so prefer enabling Rigidbody interpolation.")]
        public bool interpolateTarget = true;
        [Tooltip("A jump in target position larger than this in a single step is treated as a teleport: smoothing is skipped.")]
        [Min(0.1f)] public float teleportDistance = 5f;
        [Tooltip("Furthest the smoothed follow point may trail the target horizontally, so damping can't leave it far behind at speed.")]
        [Min(0f)] public float maxHorizontalLag = 0.75f;
        [Tooltip("Furthest the smoothed follow point may trail the target vertically (e.g. during long falls).")]
        [Min(0f)] public float maxVerticalLag = 0.5f;
    }

    [Serializable]
    public class SpeedFovSettings
    {
        [Tooltip("Widen the field of view as the target moves faster.")]
        public bool enabled = true;
        [Tooltip("Horizontal speed at which the effect starts, for a target of normal size (see CameraRig.RelativeSpeed).")]
        [Min(0f)] public float minSpeed = 4f;
        [Tooltip("Horizontal speed at which the effect reaches full strength, for a target of normal size.")]
        [Min(0f)] public float maxSpeed = 10f;
        [Tooltip("Degrees of field of view added at full strength.")]
        [Range(0f, 30f)] public float extraFov = 8f;
        [Range(0f, 1f)] public float smoothTime = 0.3f;
    }

    [Serializable]
    public class HeadBobSettings
    {
        [Tooltip("Bob the first person view while walking on the ground.")]
        public bool enabled = true;
        [Tooltip("Bob size: x = side sway, y = up/down.")]
        public Vector2 amplitude = new Vector2(0.03f, 0.045f);
        [Tooltip("Full side-to-side sway cycles (two footsteps) per meter travelled.")]
        [Min(0f)] public float cyclesPerMeter = 0.55f;
        [Tooltip("Horizontal speed at which the bob reaches full amplitude, for a target of normal size.")]
        [Min(0.01f)] public float fullAmplitudeSpeed = 5f;
        [Tooltip("Seconds to fade the bob in and out.")]
        [Range(0f, 0.5f)] public float fadeTime = 0.15f;
        [Tooltip("Also apply a gentler bob in third person.")]
        public bool applyInThirdPerson = false;
        [Range(0f, 1f)] public float thirdPersonScale = 0.3f;
    }

    [Serializable]
    public class LandingSettings
    {
        [Tooltip("Dip the camera when the target lands from a fall.")]
        public bool enabled = true;
        [Tooltip("Downward speed the target must be falling at for a landing to register.")]
        [Min(0f)] public float minImpactSpeed = 4f;
        [Tooltip("Meters of dip per m/s of impact speed above the minimum.")]
        [Min(0f)] public float dipPerSpeed = 0.025f;
        [Min(0f)] public float maxDip = 0.35f;
        [Tooltip("Spring stiffness pulling the camera back up after a landing.")]
        [Min(1f)] public float springStiffness = 140f;
        [Tooltip("Spring damping. ~2*sqrt(stiffness) is critically damped (no overshoot).")]
        [Min(0f)] public float springDamping = 20f;
        [Tooltip("Shake trauma added per m/s of impact speed above the minimum (requires Shake to be enabled).")]
        [Min(0f)] public float traumaPerSpeed = 0.02f;
    }

    [Serializable]
    public class CameraShakeSettings
    {
        [Tooltip("Allow camera shake (from landings, or from other systems calling CameraRig.AddTrauma / CameraRig.ShakeAll).")]
        public bool enabled = true;
        [Tooltip("Trauma (0-1) lost per second.")]
        [Min(0f)] public float decayPerSecond = 1.5f;
        [Tooltip("Shake intensity = trauma ^ exponent. Higher values make small trauma subtler.")]
        [Range(1f, 3f)] public float traumaExponent = 2f;
        [Tooltip("Maximum positional shake in meters at full trauma.")]
        [Min(0f)] public float maxOffset = 0.08f;
        [Tooltip("Maximum rotational shake in degrees at full trauma: x = pitch, y = yaw, z = roll. " +
                 "Yaw shake also nudges camera-relative movement, so it defaults low.")]
        public Vector3 maxAngles = new Vector3(3f, 0.5f, 3f);
        [Tooltip("Shake noise frequency.")]
        [Min(0f)] public float frequency = 22f;
    }

    [Serializable]
    public class RecenterSettings
    {
        [Tooltip("Automatically swing the third person camera behind the target after a period without look input.")]
        public bool enabled = false;
        public RecenterHeading heading = RecenterHeading.MovementDirection;
        [Tooltip("Seconds without look input before recentering starts.")]
        [Min(0f)] public float delay = 1.5f;
        [Tooltip("Seconds to swing behind the target.")]
        [Min(0.01f)] public float smoothTime = 0.6f;
        [Tooltip("Only recenter while the target is moving at least this fast (0 = always).")]
        [Min(0f)] public float minTargetSpeed = 1f;
        [Tooltip("Also ease the pitch to the resting pitch below.")]
        public bool recenterPitch = false;
        [Range(-60f, 60f)] public float restingPitch = 10f;
    }

    [Serializable]
    public class CameraDebugSettings
    {
        [Tooltip("Draw the pivot, shoulder point and collision probe in the Scene view.")]
        public bool drawGizmos = true;
    }
}
