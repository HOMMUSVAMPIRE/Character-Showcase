using System;
using TMPro;
using UnityEngine;

namespace MeridianMotion.UI
{
    /// <summary>
    /// A dialog asking for a line of text (UI_Framework_Blueprint, Modal dialogs): a message box with a text field, whose
    /// text OK passes to the caller. Enter in the field presses OK too. Open it with Open (the UIManager's Prompt For
    /// String Prefab).
    /// </summary>
    [AddComponentMenu("Meridian Motion/UI/Prompt For String")]
    public class UIMessageBox_PromptForString : UIMessageBox
    {
        [Tooltip("The field the text is typed in")]
        [SerializeField] private TMP_InputField inputField;

        /// <summary>The field the text is typed in.</summary>
        public TMP_InputField InputField { get => inputField; set => inputField = value; }

        /// <summary>What's typed in it.</summary>
        public string Value
        {
            get => inputField != null ? inputField.text : "";
            set { if (inputField != null) { inputField.text = value ?? ""; } }
        }

        /// <summary>
        /// Opens the prompt with the text and the field starting as initial: onSubmit gets what's typed when OK is pressed;
        /// onCancelled is called if it's declined.
        /// </summary>
        public static UIMessageBox_PromptForString Open(string prompt, Action<string> onSubmit, Action onCancelled = null, string initial = "")
        {
            var box = OpenPrefab<UIMessageBox_PromptForString>(UIManager.instance != null ? UIManager.instance.PromptForStringPrefab : null,
                "Prompt For String Prefab");
            if (box == null) { return null; }
            box.Value = initial;
            box.Initialize(prompt, null, onCancelled ?? (() => { }), () => onSubmit?.Invoke(box.Value));
            return box;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            if (inputField == null) { return; }
            inputField.onSubmit.AddListener(OnFieldSubmitted);
            inputField.Select();
            inputField.ActivateInputField();
        }

        protected override void OnDisable()
        {
            if (inputField != null) { inputField.onSubmit.RemoveListener(OnFieldSubmitted); }
            base.OnDisable();
        }

        private void OnFieldSubmitted(string _) => OnOkButtonClicked();
    }
}
