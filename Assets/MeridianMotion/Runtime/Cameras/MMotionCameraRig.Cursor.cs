using System;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MeridianMotion.Cameras
{
    // The camera rig: locking and showing the cursor.
    public partial class MMotionCameraRig
    {
        /// <summary>
        /// Whether something over the game wants the pointer (a window open: the UI's On Cursor Needed Changed sets it):
        /// while it's set, the cursor is unlocked and shown, and a click doesn't lock it again; once it's cleared, the
        /// cursor locks again. Only while the rig manages the cursor (Manage Cursor).
        /// </summary>
        public bool CursorRequested
        {
            get => cursorRequested;
            set
            {
                if (cursorRequested == value) { return; }
                cursorRequested = value;
                if (cursor.manageCursor && isActiveAndEnabled) { LockCursor(!value); }
            }
        }

        /// <summary>Sets CursorRequested (for a UnityEvent&lt;bool&gt; to call).</summary>
        public void SetCursorRequested(bool requested) => CursorRequested = requested;

        private bool cursorRequested;

        private void UpdateCursor()
        {
            if (!cursor.manageCursor) { return; }
            if (cursorRequested)
            {
                // Kept free while it's wanted: neither the unlock key nor a click changes it.
                if (CursorLockWanted || Cursor.lockState != CursorLockMode.None) { LockCursor(false); }
                return;
            }

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

        /// <summary>
        /// Whether the rig last locked the cursor (true) or freed it (false): what it asked of Unity's Cursor, which a
        /// player without a window (batch mode) can't lock.
        /// </summary>
        public bool CursorLockWanted { get; private set; }

        private void LockCursor(bool locked)
        {
            CursorLockWanted = locked;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
