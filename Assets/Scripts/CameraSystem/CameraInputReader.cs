using UnityEngine;
using UnityEngine.InputSystem;

namespace CameraSystem
{
    /// <summary>
    /// One frame of camera input, already converted to camera units (degrees, zoom distance).
    /// </summary>
    public struct CameraInputFrame
    {
        /// <summary>Look rotation this frame in degrees (x = yaw, y = pitch), before aim sensitivity scaling.</summary>
        public Vector2 lookDegrees;
        /// <summary>Whether any look input was received this frame.</summary>
        public bool hasLookInput;
        /// <summary>Requested change in third person distance this frame. Negative = zoom in.</summary>
        public float zoomDelta;
        public bool toggleViewPressed;
        public bool swapShoulderPressed;
        public bool aimPressed;
        public bool aimHeld;
    }

    /// <summary>
    /// Reads camera actions from the Input System by name. Has no knowledge of the character's input code, so the
    /// camera can be driven by any PlayerInput or action asset. Missing actions are treated as "no input".
    /// </summary>
    public class CameraInputReader
    {
        private InputAction look, zoom, toggleView, swapShoulder, aim;
        private bool ownsActions;
        private InputActionAsset ownedAsset;

        public bool IsBound => look != null;

        public void Bind(CameraInputSettings settings)
        {
            Unbind();

            InputActionAsset asset = settings.actionsOverride;
            if (asset != null)
            {
                ownsActions = true;
                ownedAsset = asset;
            }
            else
            {
                PlayerInput playerInput = settings.playerInput != null ? settings.playerInput : Object.FindAnyObjectByType<PlayerInput>();
                asset = playerInput != null ? playerInput.actions : null;
            }

            if (asset == null)
            {
                Debug.LogWarning("[CameraRig] No PlayerInput or action asset found; camera input is disabled.");
                return;
            }

            look = Find(asset, settings.lookAction, required: true);
            zoom = Find(asset, settings.zoomAction);
            toggleView = Find(asset, settings.toggleViewAction);
            swapShoulder = Find(asset, settings.swapShoulderAction);
            aim = Find(asset, settings.aimAction);

            if (ownsActions)
            {
                look?.Enable(); zoom?.Enable(); toggleView?.Enable(); swapShoulder?.Enable(); aim?.Enable();
            }
        }

        public void Unbind()
        {
            if (ownsActions && ownedAsset != null)
            {
                look?.Disable(); zoom?.Disable(); toggleView?.Disable(); swapShoulder?.Disable(); aim?.Disable();
            }
            look = zoom = toggleView = swapShoulder = aim = null;
            ownsActions = false;
            ownedAsset = null;
        }

        private static InputAction Find(InputActionAsset asset, string name, bool required = false)
        {
            if (string.IsNullOrEmpty(name)) { return null; }
            InputAction action = asset.FindAction(name, throwIfNotFound: false);
            if (action == null && required)
            {
                Debug.LogWarning($"[CameraRig] Input action '{name}' was not found in '{asset.name}'.");
            }
            return action;
        }

        public CameraInputFrame Read(CameraLookSettings lookSettings, CameraZoomSettings zoomSettings, bool allowMouseLook, float deltaTime)
        {
            var frame = new CameraInputFrame();

            if (look != null)
            {
                Vector2 raw = look.ReadValue<Vector2>();
                bool fromPointer = look.activeControl?.device is Pointer;
                if (fromPointer)
                {
                    // Pointer delta is already "per frame", so it must not be scaled by deltaTime.
                    frame.lookDegrees = allowMouseLook ? raw * lookSettings.mouseSensitivity : Vector2.zero;
                }
                else
                {
                    // Sticks report deflection: apply a response curve, then convert to degrees for this frame.
                    float mag = Mathf.Clamp01(raw.magnitude);
                    Vector2 curved = mag > 1e-4f ? raw / raw.magnitude * Mathf.Pow(mag, lookSettings.gamepadResponseExponent) : Vector2.zero;
                    frame.lookDegrees = curved * lookSettings.gamepadSensitivity * deltaTime;
                }

                frame.lookDegrees.y *= lookSettings.verticalSensitivityScale;
                if (lookSettings.invertX) { frame.lookDegrees.x = -frame.lookDegrees.x; }
                if (!lookSettings.invertY) { frame.lookDegrees.y = -frame.lookDegrees.y; } // Positive pitch looks down in Unity.
                frame.hasLookInput = frame.lookDegrees.sqrMagnitude > 1e-8f;
            }

            if (zoom != null)
            {
                float raw = zoom.ReadValue<float>();
                if (Mathf.Abs(raw) > 1e-3f)
                {
                    if (zoom.activeControl?.device is Pointer)
                    {
                        // Scroll magnitude per notch differs by platform and settings, so count notches by sign only.
                        frame.zoomDelta = -Mathf.Sign(raw) * zoomSettings.scrollStep;
                    }
                    else
                    {
                        frame.zoomDelta = -raw * zoomSettings.analogSpeed * deltaTime;
                    }
                }
            }

            frame.toggleViewPressed = toggleView != null && toggleView.WasPressedThisFrame();
            frame.swapShoulderPressed = swapShoulder != null && swapShoulder.WasPressedThisFrame();
            frame.aimPressed = aim != null && aim.WasPressedThisFrame();
            frame.aimHeld = aim != null && aim.IsPressed();
            return frame;
        }
    }
}
