using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MeridianMotion.UI
{
    /// <summary>
    /// Which input device the player's using now (the device of the last action that started or performed, any action:
    /// the UI's while the game's paused too), so what's shown can follow a switch from keyboard and mouse to a gamepad and
    /// back. SchemeFor names an action asset's control scheme for a device; CurrentScheme, for the device in use (before
    /// any's used: the keyboard's, if there's a keyboard, else the gamepad's). Changed is raised when the device in use
    /// changes. It knows nothing of the game: it listens to the Input System itself.
    /// </summary>
    public static class ControlSchemeTracker
    {
        private static bool listening;
        private static InputDevice current;

        /// <summary>Raised with the device now in use, when it changes.</summary>
        public static event Action<InputDevice> Changed;

        /// <summary>The device used last (null before any's used).</summary>
        public static InputDevice CurrentDevice
        {
            get
            {
                Listen();
                return current;
            }
        }

        /// <summary>Starts following the devices used (its getters do this themselves).</summary>
        public static void Listen()
        {
            if (listening) { return; }
            listening = true;
            InputSystem.onActionChange += OnActionChange;
        }

        /// <summary>Notes the device as the one in use (Changed is raised if it's another): what an action's use does.</summary>
        public static void NoteDeviceUsed(InputDevice device)
        {
            if (device == null || device == current) { return; }
            current = device;
            Changed?.Invoke(device);
        }

        /// <summary>Forgets the device in use (as before any's used): for tests.</summary>
        public static void Forget() => current = null;

        /// <summary>The name of the first of the asset's control schemes the device suits, or null.</summary>
        public static string SchemeFor(InputActionAsset actions, InputDevice device)
        {
            if (actions == null || device == null) { return null; }
            foreach (InputControlScheme scheme in actions.controlSchemes)
            {
                if (scheme.SupportsDevice(device)) { return scheme.name; }
            }
            return null;
        }

        /// <summary>
        /// The name of the asset's control scheme for the device in use; before any's used, the keyboard's (if there's a
        /// keyboard), else the gamepad's, else the asset's first; null if it has none.
        /// </summary>
        public static string CurrentScheme(InputActionAsset actions)
        {
            if (actions == null || actions.controlSchemes.Count == 0) { return null; }
            string scheme = SchemeFor(actions, CurrentDevice);
            scheme ??= SchemeFor(actions, Keyboard.current);
            scheme ??= SchemeFor(actions, Gamepad.current);
            return scheme ?? actions.controlSchemes[0].name;
        }

        private static void OnActionChange(object changed, InputActionChange change)
        {
            if (change != InputActionChange.ActionStarted && change != InputActionChange.ActionPerformed) { return; }
            if (changed is InputAction action && action.activeControl != null) { NoteDeviceUsed(action.activeControl.device); }
        }

        // Entering Play mode with domain reloading off keeps statics: start afresh.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            current = null;
            Changed = null;
        }
    }
}
