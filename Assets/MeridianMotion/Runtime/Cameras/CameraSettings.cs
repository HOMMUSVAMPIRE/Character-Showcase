using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MeridianMotion.Cameras
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

    /// <summary>How the aim button turns aiming on and off.</summary>
    public enum AimInputMode
    {
        [Tooltip("Aim while the aim button is held.")]
        Hold,
        [Tooltip("Each press of the aim button toggles aiming on or off.")]
        Toggle,
    }

    /// <summary>Which way the third person camera swings behind the target when it recenters.</summary>
    public enum RecenterHeading
    {
        [Tooltip("Swing behind the direction the target is moving.")]
        MovementDirection,
        [Tooltip("Swing behind the direction the target's transform is facing.")]
        TargetForward,
    }

    // Each settings group below is drawn as its own foldout in the MMotionCameraRig inspector.

    /// <summary>
    /// Where the camera reads its input from: the PlayerInput or action asset, and the names of the look, zoom, view,
    /// shoulder and aim actions.
    /// </summary>
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

    /// <summary>
    /// How look input turns the camera: mouse and gamepad sensitivity, the stick's response curve, inversion and
    /// smoothing.
    /// </summary>
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
        [Tooltip("Reverse horizontal look: moving the mouse or stick right turns the camera left.")]
        public bool invertX = false;
        [Tooltip("Reverse vertical look: moving the mouse or stick up looks down.")]
        public bool invertY = false;
        [Tooltip("Smooths rotation input over time. 0 disables smoothing (most responsive).")]
        [Range(0f, 0.2f)] public float smoothing = 0f;
    }

    /// <summary>
    /// Whether the rig locks and hides the cursor during play, and how it is released and locked again.
    /// </summary>
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

    /// <summary>
    /// Which view modes are allowed, the one the camera starts in, and how it blends between first and third person.
    /// </summary>
    [Serializable]
    public class CameraModeSettings
    {
        [Tooltip("The view mode the camera starts in.")]
        public CameraMode startMode = CameraMode.ThirdPerson;
        [Tooltip("Whether the camera can be in first person (toggling and zooming in can reach it). If neither mode is allowed, third person is.")]
        public bool allowFirstPerson = true;
        [Tooltip("Whether the camera can be in third person. If neither mode is allowed, third person is.")]
        public bool allowThirdPerson = true;
        [Tooltip("Seconds taken to blend between first and third person.")]
        [Min(0f)] public float transitionDuration = 0.35f;
        [Tooltip("Easing applied to the first/third person blend (0 = first person, 1 = third person).")]
        public AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    }

    /// <summary>
    /// The first person view: where the eyes sit on the target, the field of view, pitch limits and near clip plane,
    /// and how the target's own renderers are hidden.
    /// </summary>
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
        [Tooltip("Vertical field of view in first person, in degrees.")]
        [Range(20f, 120f)] public float fieldOfView = 75f;
        [Tooltip("How far up the view can look in first person, in degrees (pitch is negative looking up).")]
        [Range(-89f, 0f)] public float minPitch = -85f;
        [Tooltip("How far down the view can look in first person, in degrees (pitch is positive looking down).")]
        [Range(0f, 89f)] public float maxPitch = 85f;
        [Tooltip("Near clip plane for a target of normal size (scaled with its size).")]
        [Min(0.01f)] public float nearClipPlane = 0.05f;
        [Tooltip("How the target's own renderers are handled in first person.")]
        public TargetHideMode hideTarget = TargetHideMode.ShadowsOnly;
        [Tooltip("Seconds to smooth vertical eye movement (e.g. stepping up stairs). 0 = no smoothing.")]
        [Range(0f, 0.3f)] public float verticalDamping = 0.05f;
    }

    /// <summary>
    /// The over-the-shoulder third person view: the orbit pivot, shoulder offset and swapping, field of view, pitch
    /// limits, follow damping, and hiding the target when the camera gets close.
    /// </summary>
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
        [Tooltip("Auto swap: the share (0 to 1) of the way out to the shoulder that must be free of walls for it to count " +
                 "as clear; less, and it's blocked enough to swap from, if the other side is freer (see below). Only " +
                 "partly blocked is no good either: the camera's backward sweep grazes the wall and pulls it in close.")]
        [Range(0f, 1f)] public float autoSwapBlockedFree = 0.9f;
        [Tooltip("Auto swap: how much freer (as a share of the way out) the other shoulder must be to swap to it.")]
        [Range(0f, 1f)] public float autoSwapFreerBy = 0.15f;
        [Tooltip("Auto swap: the share of the way out to the preferred shoulder that must be free to swap back to it " +
                 "(or no less free than the other). Above Auto Swap Blocked Free, so it doesn't swap back and forth.")]
        [Range(0f, 1f)] public float autoSwapReturnFree = 0.95f;
        [Tooltip("Vertical field of view in third person, in degrees.")]
        [Range(20f, 120f)] public float fieldOfView = 60f;
        [Tooltip("How far up the camera can look in third person, in degrees (pitch is negative looking up).")]
        [Range(-89f, 0f)] public float minPitch = -60f;
        [Tooltip("How far down the camera can look in third person, in degrees (pitch is positive looking down).")]
        [Range(0f, 89f)] public float maxPitch = 75f;
        [Tooltip("Near clip plane for a target of normal size (scaled with its size).")]
        [Min(0.01f)] public float nearClipPlane = 0.1f;
        [Tooltip("Seconds to smooth horizontal follow. 0 = locked to the target.")]
        [Range(0f, 0.5f)] public float horizontalDamping = 0.04f;
        [Tooltip("Seconds to smooth vertical follow, so jumps and steps don't jolt the view.")]
        [Range(0f, 0.5f)] public float verticalDamping = 0.15f;
        [Tooltip("How the target's renderers are handled when the camera gets very close to it.")]
        public TargetHideMode hideWhenClose = TargetHideMode.Hide;
        [Tooltip("Camera-to-pivot distance below which the target is hidden, for a target of normal size.")]
        [Min(0f)] public float hideDistance = 0.6f;
    }

    /// <summary>
    /// Third person zoom: the default, closest and furthest camera distances (m), how fast scrolling and analog input
    /// zoom, and zooming into first person.
    /// </summary>
    [Serializable]
    public class CameraZoomSettings
    {
        [Tooltip("Allow zooming the third person camera in and out.")]
        public bool enabled = true;
        [Tooltip("The third person distance (m) the camera starts at, for a target of normal size; kept between the min and max distance.")]
        [Min(0.1f)] public float defaultDistance = 3.5f;
        [Tooltip("The closest the third person camera can be zoomed (m), for a target of normal size.")]
        [Min(0.1f)] public float minDistance = 1.5f;
        [Tooltip("The furthest the third person camera can be zoomed (m), for a target of normal size.")]
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

    /// <summary>
    /// Aiming: how the button works, and the field of view, distance, shoulder offset and look sensitivity while aimed.
    /// </summary>
    [Serializable]
    public class CameraAimSettings
    {
        [Tooltip("Allow aiming with the aim button.")]
        public bool enabled = true;
        [Tooltip("Whether aiming lasts while the button is held, or each press turns it on or off.")]
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

    /// <summary>
    /// Keeping the third person camera out of walls: the sphere swept from the pivot to the camera, the layers it hits,
    /// and how it eases back out.
    /// </summary>
    [Serializable]
    public class CameraCollisionSettings
    {
        [Tooltip("Pull the camera in front of obstacles between it and the target.")]
        public bool enabled = true;
        [Tooltip("Layers the camera collides with. The target's own colliders are always ignored.")]
        public LayerMask layers = ~0;
        [Tooltip("Radius of the sphere swept from the pivot to the camera, for a target of normal size (scaled with its size, as are the other distances here).")]
        [Min(0.01f)] public float probeRadius = 0.2f;
        [Tooltip("Closest the camera may be pulled to the pivot, for a target of normal size.")]
        [Min(0f)] public float minDistance = 0.25f;
        [Tooltip("Seconds to ease back out once an obstacle is gone. Pulling in is always instant to avoid clipping.")]
        [Range(0f, 1f)] public float returnSmoothTime = 0.25f;
        [Tooltip("Whether trigger colliders block the camera.")]
        public QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;
    }

    /// <summary>
    /// How the camera follows the target's position: interpolation between physics steps, teleport detection, and how
    /// far smoothing may let it lag behind.
    /// </summary>
    [Serializable]
    public class CameraFollowSettings
    {
        [Tooltip("Interpolate the target between physics steps. Removes camera stutter when the target moves in FixedUpdate " +
                 "without Rigidbody interpolation. Adds up to one physics step of latency. Skipped automatically when the " +
                 "target's Rigidbody already interpolates. Note this only smooths the camera: the target itself will " +
                 "still visibly step unless its Rigidbody interpolates, so prefer enabling Rigidbody interpolation.")]
        public bool interpolateTarget = true;
        [Tooltip("A jump in target position larger than this in a single step is treated as a teleport: smoothing is skipped. For a target of normal size; scaled with its size.")]
        [Min(0.1f)] public float teleportDistance = 5f;
        [Tooltip("Furthest the smoothed follow point may trail the target horizontally, so damping can't leave it far behind at speed. For a target of normal size; scaled with its size.")]
        [Min(0f)] public float maxHorizontalLag = 0.75f;
        [Tooltip("Furthest the smoothed follow point may trail the target vertically (e.g. during long falls). For a target of normal size; scaled with its size.")]
        [Min(0f)] public float maxVerticalLag = 0.5f;
    }

    /// <summary>Widening the field of view as the target moves faster.</summary>
    [Serializable]
    public class SpeedFovSettings
    {
        [Tooltip("Widen the field of view as the target moves faster.")]
        public bool enabled = true;
        [Tooltip("Horizontal speed at which the effect starts, for a target of normal size (see MMotionCameraRig.RelativeSpeed).")]
        [Min(0f)] public float minSpeed = 4f;
        [Tooltip("Horizontal speed at which the effect reaches full strength, for a target of normal size.")]
        [Min(0f)] public float maxSpeed = 10f;
        [Tooltip("Degrees of field of view added at full strength.")]
        [Range(0f, 30f)] public float extraFov = 8f;
        [Tooltip("Seconds to smooth changes to the field of view, so it eases wider and back rather than jumping.")]
        [Range(0f, 1f)] public float smoothTime = 0.3f;
    }

    /// <summary>
    /// Bobbing the view while the target walks on the ground, in first person and optionally in third.
    /// </summary>
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
        [Tooltip("The share of the first person bob applied in third person, if third person bob is on.")]
        [Range(0f, 1f)] public float thirdPersonScale = 0.3f;
    }

    /// <summary>The dip and spring back when the target lands from a fall, and the shake it adds.</summary>
    [Serializable]
    public class LandingSettings
    {
        [Tooltip("Dip the camera when the target lands from a fall.")]
        public bool enabled = true;
        [Tooltip("Downward speed the target must be falling at for a landing to register, for a target of normal size (scaled with its size, as is the dip).")]
        [Min(0f)] public float minImpactSpeed = 4f;
        [Tooltip("Seconds the target must have been falling for its stop to be a landing: stepping down onto the next stair " +
                 "drops it as fast as a hard landing, but in a physics step or two, which mustn't dip or shake the camera. " +
                 "A real fall fast enough to land hard takes far longer than this.")]
        [Min(0f)] public float minFallTime = 0.1f;
        [Tooltip("Meters of dip per m/s of impact speed above the minimum.")]
        [Min(0f)] public float dipPerSpeed = 0.025f;
        [Tooltip("The deepest a landing can dip the camera (m), for a target of normal size, however hard the landing.")]
        [Min(0f)] public float maxDip = 0.35f;
        [Tooltip("Spring stiffness pulling the camera back up after a landing.")]
        [Min(1f)] public float springStiffness = 140f;
        [Tooltip("Spring damping. ~2*sqrt(stiffness) is critically damped (no overshoot).")]
        [Min(0f)] public float springDamping = 20f;
        [Tooltip("Shake trauma added per m/s of impact speed above the minimum (requires Shake to be enabled).")]
        [Min(0f)] public float traumaPerSpeed = 0.02f;
    }

    /// <summary>Trauma-based camera shake: how fast trauma decays and how far it moves and turns the camera.</summary>
    [Serializable]
    public class CameraShakeSettings
    {
        [Tooltip("Allow camera shake (from landings, or from other systems calling MMotionCameraRig.AddTrauma / MMotionCameraRig.ShakeAll).")]
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

    /// <summary>Swinging the third person camera back behind the target after a while without look input.</summary>
    [Serializable]
    public class RecenterSettings
    {
        [Tooltip("Automatically swing the third person camera behind the target after a period without look input.")]
        public bool enabled = false;
        [Tooltip("What the camera swings behind: the direction the target is moving, or the way it faces.")]
        public RecenterHeading heading = RecenterHeading.MovementDirection;
        [Tooltip("Seconds without look input before recentering starts.")]
        [Min(0f)] public float delay = 1.5f;
        [Tooltip("Seconds to swing behind the target.")]
        [Min(0.01f)] public float smoothTime = 0.6f;
        [Tooltip("Only recenter while the target is moving at least this fast (0 = always).")]
        [Min(0f)] public float minTargetSpeed = 1f;
        [Tooltip("Don't recenter when the way to swing to is more than this many degrees round from the camera (the target " +
                 "walking back toward it), so the camera doesn't spin round.")]
        [Range(0f, 180f)] public float maxTurnAngle = 135f;
        [Tooltip("Also ease the pitch to the resting pitch below.")]
        public bool recenterPitch = false;
        [Tooltip("The pitch (degrees, positive looking down) recentering eases to, if it eases the pitch.")]
        [Range(-60f, 60f)] public float restingPitch = 10f;
    }

    /// <summary>Scene view gizmos for the rig's pivot, shoulder point and collision probe.</summary>
    [Serializable]
    public class CameraDebugSettings
    {
        [Tooltip("Draw the pivot, shoulder point and collision probe in the Scene view.")]
        public bool drawGizmos = true;
    }
}
