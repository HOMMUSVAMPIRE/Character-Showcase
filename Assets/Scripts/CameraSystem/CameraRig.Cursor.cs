using System;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CameraSystem
{
    // The camera rig: locking and showing the cursor.
    public partial class CameraRig
    {
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
    }
}
