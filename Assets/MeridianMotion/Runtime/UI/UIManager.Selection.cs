using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace MeridianMotion.UI
{
    // Keeping something selected for a gamepad or the keyboard to navigate from. uGUI's navigation (the UI module's Move
    // and Submit) only moves from, and presses, what's selected, and nothing is until something selects it: a mouse
    // clicks what it points at, but a gamepad has nothing to start from, so it could neither move nor press anything.
    public partial class UIManager
    {
        // Whether the player's last UI input was a gamepad's or the keyboard's navigation (Move, Submit, Back or Menu), not the
        // pointer's: then the frontmost window that takes the pointer always has something selected.
        private bool navigatingByButtons;

        /// <summary>Whether the player is navigating the UI by a gamepad or the keyboard (their last UI input), not the pointer.</summary>
        public bool NavigatingByButtons => navigatingByButtons;

        /// <summary>
        /// The frontmost window something can be selected in: the last opened or brought to the front that's shown, not
        /// closing, not a blocker, takes the pointer or stops the game's input (not a HUD), and has a control that can be used.
        /// </summary>
        public RootUIPrefab FocusedWindow()
        {
            for (int i = openWindows.Count - 1; i >= 0; i--)
            {
                RootUIPrefab window = openWindows[i];
                if (window == null || !window.isActiveAndEnabled || window.IsClosing || window.IsBlocker) { continue; }
                if (!window.NeedsCursor && !window.BlocksGameInput) { continue; }
                if (window.FirstSelectable() != null) { return window; }
            }
            return null;
        }

        // Notes how the player last used the UI, and while it's a gamepad or the keyboard, keeps a control of the
        // frontmost window selected: its First Selected (or first control) when nothing of it is.
        private void KeepSelection()
        {
            EventSystem events = EventSystem.current;
            if (events == null) { return; }
            var module = events.currentInputModule as InputSystemUIInputModule;
            if (PressedByButtons(module != null && module.move != null ? module.move.action : null, true)
                || PressedByButtons(module != null && module.submit != null ? module.submit.action : null, false)
                || PressedByButtons(cancelAction != null ? cancelAction.action : null, false)
                || PressedByButtons(menuAction != null ? menuAction.action : null, false))
            {
                navigatingByButtons = true;
            }
            else if (PointerUsed(module)) { navigatingByButtons = false; }
            if (!navigatingByButtons || IsInputCaptured || IsFocusedOnText()) { return; }
            RootUIPrefab window = FocusedWindow();
            if (window == null) { return; }
            GameObject selected = events.currentSelectedGameObject;
            if (selected != null && selected.activeInHierarchy && selected.transform.IsChildOf(window.transform)
                && selected.TryGetComponent(out Selectable current) && current.IsInteractable())
            {
                return;
            }
            events.SetSelectedGameObject(window.FirstSelectable().gameObject);
        }

        // Whether the action was pressed (or, a direction, pushed) this frame by something other than a pointer.
        private static bool PressedByButtons(InputAction action, bool direction)
        {
            if (action == null || !action.enabled) { return false; }
            bool pressed = direction ? action.WasPerformedThisFrame() && action.ReadValue<Vector2>().sqrMagnitude > 0.25f : action.WasPressedThisFrame();
            return pressed && action.activeControl != null && action.activeControl.device is not Pointer;
        }

        // Whether the pointer moved or clicked this frame (the UI module's Point or Click).
        private static bool PointerUsed(InputSystemUIInputModule module)
        {
            if (module == null) { return false; }
            InputAction point = module.point != null ? module.point.action : null, click = module.leftClick != null ? module.leftClick.action : null;
            return (point != null && point.enabled && point.WasPerformedThisFrame() && point.activeControl?.device is Pointer)
                || (click != null && click.enabled && click.WasPressedThisFrame());
        }
    }
}
