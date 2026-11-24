using System;
using TMPro;
using UnityEngine;

namespace MeridianMotion.UI
{
    /// <summary>
    /// A modal dialog (UI_Framework_Blueprint, Modal dialogs): a window with a message and its buttons, opened by a static
    /// helper (Open, OpenError, OpenYesNoDialog) that keeps the caller's callbacks and returns at once: dialogs never
    /// block the caller. Any button closes it after its callback; the Submit key (Enter) presses OK, and the Back key
    /// declines (or, with no decline callback, presses OK). Its prefab's click blocker keeps the rest of the screen from
    /// being clicked meanwhile. Its buttons call OnOkButtonClicked and OnDeclineButtonClicked.
    /// </summary>
    [AddComponentMenu("Meridian Motion/UI/Message Box")]
    public class UIMessageBox : RootUIPrefab
    {
        [Header("Message box")]
        [Tooltip("Where its message is shown")]
        [SerializeField] protected TextMeshProUGUI bodyText;

        /// <summary>Called when OK is pressed (Yes, for a yes/no dialog).</summary>
        protected Action onOkayClickedCallback;
        /// <summary>Called when the dialog is declined (No, for a yes/no dialog; the Back key).</summary>
        protected Action onDeclinedClickedCallback;
        /// <summary>Called after onOkayClickedCallback when OK is pressed: for a dialog of its own to add to OK (the prompt, passing its text on).</summary>
        protected Action onOkAndCallback;

        private string text = "";

        /// <summary>Its message.</summary>
        public string Text => text;

        /// <summary>Opens the OK dialog (the UIManager's Message Box Prefab) with the text; onOk is called when it's dismissed.</summary>
        public static UIMessageBox Open(string text, Action onOk = null) =>
            OpenPrefab<UIMessageBox>(UIManager.instance != null ? UIManager.instance.MessageBoxPrefab : null, "Message Box Prefab")
                ?.Initialized(text, onOk);

        /// <summary>Opens the error dialog (the UIManager's Error Message Box Prefab) with the text; onOk is called when it's dismissed.</summary>
        public static UIMessageBox OpenError(string text, Action onOk = null) =>
            OpenPrefab<UIMessageBox>(UIManager.instance != null ? UIManager.instance.ErrorMessageBoxPrefab : null, "Error Message Box Prefab")
                ?.Initialized(text, onOk);

        /// <summary>Opens the yes/no dialog (the UIManager's Yes No Dialog Prefab) with the text: onYes or onNo is called with the answer.</summary>
        public static UIMessageBox OpenYesNoDialog(string text, Action onYes, Action onNo) =>
            OpenPrefab<UIMessageBox>(UIManager.instance != null ? UIManager.instance.YesNoDialogPrefab : null, "Yes No Dialog Prefab")
                ?.Initialized(text, onYes, onNo);

        /// <summary>
        /// Opens a dialog's prefab through the UIManager, as its T: null, with an error saying which of the manager's
        /// fields to set, without a manager or a prefab.
        /// </summary>
        protected static T OpenPrefab<T>(GameObject prefab, string field) where T : UIMessageBox
        {
            if (UIManager.instance == null)
            {
                Debug.LogError($"[{typeof(T).Name}] There's no UIManager in the scene to open the dialog with (Set Up Scene adds one).");
                return null;
            }
            if (prefab == null)
            {
                Debug.LogError($"[{typeof(T).Name}] The UIManager's {field} isn't set.", UIManager.instance);
                return null;
            }
            T box = UIManager.instance.OpenUI<T>(prefab);
            if (box == null) { Debug.LogError($"[{typeof(T).Name}] '{prefab.name}' has no {typeof(T).Name} on its root.", prefab); }
            return box;
        }

        /// <summary>
        /// Sets its message and its callbacks: onOk when OK is pressed, then onOkAnd (for a dialog adding to OK); onDeclined
        /// when it's declined. A dialog's static helper calls it; so can a caller that opened the dialog itself.
        /// </summary>
        public virtual void Initialize(string text, Action onOk, Action onDeclined = null, Action onOkAnd = null)
        {
            this.text = text ?? "";
            if (bodyText != null) { bodyText.text = this.text; }
            onOkayClickedCallback = onOk;
            onDeclinedClickedCallback = onDeclined;
            onOkAndCallback = onOkAnd;
        }

        private UIMessageBox Initialized(string text, Action onOk, Action onDeclined = null)
        {
            Initialize(text, onOk, onDeclined);
            return this;
        }

        /// <summary>OK (or Yes) pressed: its callbacks, then it closes. Nothing once it's closing, so a double press counts once.</summary>
        public void OnOkButtonClicked()
        {
            if (IsClosing) { return; }
            onOkayClickedCallback?.Invoke();
            onOkAndCallback?.Invoke();
            Close();
        }

        /// <summary>Declined (No pressed): its callback, then it closes. Nothing once it's closing.</summary>
        public void OnDeclineButtonClicked()
        {
            if (IsClosing) { return; }
            onDeclinedClickedCallback?.Invoke();
            Close();
        }

        /// <summary>Enter presses OK.</summary>
        public override void OnSubmitButton() => OnOkButtonClicked();

        /// <summary>
        /// The Back key declines it (or, with no decline callback, presses OK, so the caller always hears it was dismissed),
        /// unless its Cancel Action Overrides say otherwise.
        /// </summary>
        public override void OnCancelButton()
        {
            if (CancelActionOverrides.GetPersistentEventCount() > 0) { base.OnCancelButton(); }
            else if (onDeclinedClickedCallback != null) { OnDeclineButtonClicked(); }
            else { OnOkButtonClicked(); }
        }
    }
}
