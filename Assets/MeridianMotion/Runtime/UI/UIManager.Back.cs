using System;

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

        /// <summary>
        /// The Back key, pressed: the top window that takes it gets it (OnCancelButton: its CancelActionOverrides, or it
        /// closes); with none, the fallback handler runs. Nothing while a text field has the keyboard. Update calls it
        /// for the Cancel action; a test, or another input layer, can call it itself.
        /// </summary>
        public void HandleBack()
        {
            if (IsFocusedOnText()) { return; }
            RootUIPrefab top = TopWindow;
            if (top != null) { top.OnCancelButton(); }
            else { fallbackBackHandler?.Invoke(); }
        }

        /// <summary>
        /// The Submit key (Enter), pressed: the top window gets it (OnSubmitButton: a dialog presses OK). Nothing while a
        /// text field has the keyboard (the field submits itself).
        /// </summary>
        public void HandleSubmit()
        {
            if (IsFocusedOnText()) { return; }
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
