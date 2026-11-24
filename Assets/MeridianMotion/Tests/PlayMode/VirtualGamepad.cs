using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace MeridianMotion.Tests
{
    /// <summary>
    /// A gamepad of the test's own, alone: the machine's devices (its keyboard, mouse and any controller plugged in) are
    /// turned off until it's disposed, so in the editor the mouse moved over the Game view, or a real controller's
    /// pairing or drifting stick, can't come between the test and what it presses. Its presses are left for the frame's
    /// own input update, as a real controller's are, never an update of the test's (InputSystem.Update): the UI reads its
    /// keys in the frame's Update (WasPressedThisFrame), and in the editor an update between frames isn't the frame's.
    /// </summary>
    public sealed class VirtualGamepad : IDisposable
    {
        /// <summary>How many frames Press holds a button, and then waits with it let go.</summary>
        public const int PressFrames = 4;

        /// <summary>The test's gamepad.</summary>
        public Gamepad Device { get; }

        private readonly List<InputDevice> turnedOff = new();

        /// <summary>Turns off the machine's devices and adds the test's gamepad.</summary>
        public VirtualGamepad()
        {
            foreach (InputDevice device in InputSystem.devices.ToList())
            {
                if (!device.enabled) { continue; }
                InputSystem.DisableDevice(device);
                turnedOff.Add(device);
            }
            Device = InputSystem.AddDevice<Gamepad>();
        }

        /// <summary>Holds a button from the next frame's input update.</summary>
        public void Hold(GamepadButton button) => InputSystem.QueueStateEvent(Device, new GamepadState().WithButton(button));

        /// <summary>Lets every button go from the next frame's input update.</summary>
        public void Release() => InputSystem.QueueStateEvent(Device, new GamepadState());

        /// <summary>
        /// Presses a button for PressFrames frames, then lets it go for as many (the UI module moves on a fresh press, so it
        /// has to see the d-pad let go between two).
        /// </summary>
        public IEnumerator Press(GamepadButton button)
        {
            Hold(button);
            for (int i = 0; i < PressFrames; i++) { yield return null; }
            Release();
            for (int i = 0; i < PressFrames; i++) { yield return null; }
        }

        /// <summary>Removes the test's gamepad and turns the machine's devices back on.</summary>
        public void Dispose()
        {
            if (Device != null && Device.added) { InputSystem.RemoveDevice(Device); }
            foreach (InputDevice device in turnedOff) { if (device.added) { InputSystem.EnableDevice(device); } }
            turnedOff.Clear();
        }
    }
}
