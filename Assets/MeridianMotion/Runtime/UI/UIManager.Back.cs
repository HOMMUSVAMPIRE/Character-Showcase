using System;
using UnityEngine;

namespace MeridianMotion.UI
{
    // The Back and Submit keys (UI_Framework_Blueprint, Opening a window, and the Back key), and telling the game what the
    // windows need of it.
    public partial class UIManager
    {
        /// <summary>
        /// The top window that takes the Back key (the last opened that's still shown, not closing, and does something
        /// with Back), or null if none does.
        /// </summary>
        public RootUIPrefab TopWindow
        {
            get
            {
                TrimCancelStack();
                return cancelStack.Count > 0 ? cancelStack.Peek() : null;
            }
        }

        /// <summary>
        /// What runs when the Back key is pressed with no window to take it (a pause menu opening, say). Replaces the one
        /// registered before; null for none.
        /// </summary>
        public void RegisterFallbackHandler(Action handler) => fallbackBackHandler = handler;

        // How many are capturing the input (CaptureInput), and the frame the last let it go.
        private static int inputCaptures;
        private static int inputReleasedFrame = -1;

        /// <summary>
        /// Takes the Back and Submit keys from the windows until ReleaseInput: for something listening for a key of its
        /// own (a control being rebound, where Escape or Enter may be the key chosen, or cancel the listening).
        /// </summary>
        public static void CaptureInput() => inputCaptures++;

        /// <summary>
        /// Gives the Back and Submit keys back (CaptureInput), from the next frame: the key that ended the capture is
        /// pressed this frame, and isn't the windows'.
        /// </summary>
        public static void ReleaseInput()
        {
            inputCaptures = Mathf.Max(0, inputCaptures - 1);
            inputReleasedFrame = Time.frameCount;
        }

        /// <summary>Whether something's capturing the input (CaptureInput), so Back and Submit do nothing.</summary>
        public static bool IsInputCaptured => inputCaptures > 0 || Time.frameCount == inputReleasedFrame;

        /// <summary>
        /// The Back key, pressed: the top window that takes it gets it (OnCancelButton: its CancelActionOverrides, or it
        /// closes); with none, the fallback handler runs, if allowFallback (the Menu key's, not Back's alone: see Update).
        /// Nothing while a text field has the keyboard, or the input's captured (CaptureInput). Update calls it for the
        /// Menu and Cancel actions; a test, or another input layer, can call it itself.
        /// </summary>
        public void HandleBack(bool allowFallback = true)
        {
            if (IsFocusedOnText() || IsInputCaptured) { return; }
            RootUIPrefab top = TopWindow;
            if (top != null) { top.OnCancelButton(); }
            else if (allowFallback) { fallbackBackHandler?.Invoke(); }
        }

        /// <summary>
        /// The Submit key (Enter), pressed: the top window gets it (OnSubmitButton: a dialog presses OK). Nothing while a
        /// text field has the keyboard (the field submits itself).
        /// </summary>
        public void HandleSubmit()
        {
            if (IsFocusedOnText() || IsInputCaptured) { return; }
            TopWindow?.OnSubmitButton();
        }

        // Takes off the top what can't take Back any more: destroyed, hidden, closing, a blocker, or not doing anything with
        // Back. (A hidden window shown again by RestorePreviouslyShownUI goes back on.)
        private void TrimCancelStack()
        {
            while (cancelStack.Count > 0)
            {
                RootUIPrefab top = cancelStack.Peek();
                if (top != null && top.isActiveAndEnabled && !top.IsClosing && !top.IsBlocker && top.HasCancelAction) { return; }
                cancelStack.Pop();
            }
        }

        // Raises OnCursorNeededChanged and OnGameInputBlockedChanged when what the shown windows need changes.
        private void UpdateGameEvents()
        {
            bool needsCursor = false, blocksInput = false;
            foreach (RootUIPrefab window in openWindows)
            {
                if (window == null || window.IsBlocker || window.IsClosing || !window.isActiveAndEnabled) { continue; }
                needsCursor |= window.NeedsCursor;
                blocksInput |= window.BlocksGameInput;
            }
            if (needsCursor != cursorNeeded)
            {
                cursorNeeded = needsCursor;
                onCursorNeededChanged.Invoke(needsCursor);
            }
            if (blocksInput != gameInputBlocked)
            {
                gameInputBlocked = blocksInput;
                onGameInputBlockedChanged.Invoke(blocksInput);
            }
        }

        /// <summary>Whether a shown window needs the pointer (as OnCursorNeededChanged last said).</summary>
        public bool CursorNeeded => cursorNeeded;
        /// <summary>Whether a shown window stops the game's input (as OnGameInputBlockedChanged last said).</summary>
        public bool GameInputBlocked => gameInputBlocked;

        /// <summary>
        /// Shows a notification (Notifications.ShowNotification: its default duration, at the top middle, a passive toast).
        /// Null, with a warning, without the scene's notifications.
        /// </summary>
        public NotificationEntry ShowNotification(string title, string msg, float duration = 0f,
            Notifications.Location location = Notifications.Location.Top_Middle, Notifications.Type type = Notifications.Type.Passive)
        {
            if (Notifications.Instance == null)
            {
                UnityEngine.Debug.LogWarning("[UIManager] There are no notifications in the scene (the UI Manager prefab has them) to show it with.", this);
                return null;
            }
            return Notifications.Instance.ShowNotification(title, msg, duration, location, type);
        }
    }
}
