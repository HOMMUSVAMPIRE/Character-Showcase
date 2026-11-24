using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace MeridianMotion.UI
{
    /// <summary>
    /// A dialog of any number of choices (UIChoiceDialog.Open): its text, and a button for each option (made from its
    /// Button Template, a ThemedButton, in its Buttons Parent), the first selected for a gamepad. Pressing one closes
    /// it and runs its action. Back picks the option given for it (cancelIndex), or does nothing (the dialog stays) if
    /// there's none.
    /// </summary>
    [AddComponentMenu("Meridian Motion/UI/Choice Dialog")]
    public class UIChoiceDialog : UIMessageBox
    {
        /// <summary>One choice: its label, its kind of button, and what it does.</summary>
        public struct Option
        {
            public string Label;
            public Action Action;
            public ThemedButton.Kind Kind;

            public Option(string label, Action action, ThemedButton.Kind kind = ThemedButton.Kind.Outline)
            {
                Label = label;
                Action = action;
                Kind = kind;
            }
        }

        [Header("Choices")]
        [Tooltip("A button (hidden): each option's is a copy")]
        [SerializeField] private ThemedButton buttonTemplate;
        [Tooltip("Where the options' buttons go (a layout group)")]
        [SerializeField] private Transform buttonsParent;

        private readonly List<ThemedButton> buttons = new();
        private Option[] options = Array.Empty<Option>();
        private int cancelIndex = -1;
        private bool chosen;

        /// <summary>The options' buttons, in order.</summary>
        public IReadOnlyList<ThemedButton> Buttons => buttons;

        /// <summary>
        /// Opens the choice dialog (the UIManager's Choice Dialog Prefab) with the text and options; Back picks
        /// options[cancelIndex] (none, for -1).
        /// </summary>
        public static UIChoiceDialog Open(string text, int cancelIndex, params Option[] options)
        {
            UIChoiceDialog dialog = OpenPrefab<UIChoiceDialog>(UIManager.instance != null ? UIManager.instance.ChoiceDialogPrefab : null, "Choice Dialog Prefab");
            if (dialog != null) { dialog.Show(text, cancelIndex, options); }
            return dialog;
        }

        /// <summary>Sets its text and options (what Open does).</summary>
        public void Show(string text, int cancel, params Option[] choices)
        {
            Initialize(text, null);
            options = choices ?? Array.Empty<Option>();
            cancelIndex = cancel;
            chosen = false;
            foreach (ThemedButton button in buttons) { if (button != null) { Destroy(button.gameObject); } }
            buttons.Clear();
            if (buttonTemplate == null) { return; }
            for (int i = 0; i < options.Length; i++)
            {
                int index = i;
                ThemedButton button = Instantiate(buttonTemplate, buttonsParent != null ? buttonsParent : buttonTemplate.transform.parent, false);
                button.gameObject.SetActive(true);
                button.name = $"{options[i].Label} Button";
                button.ButtonKind = options[i].Kind;
                if (button.Label != null && button.Label.TryGetComponent(out TMPro.TMP_Text label)) { label.text = options[i].Label.ToUpperInvariant(); }
                button.onClick.AddListener(() => Choose(index));
                buttons.Add(button);
            }
            if (buttons.Count > 0 && EventSystem.current != null) { EventSystem.current.SetSelectedGameObject(buttons[0].gameObject); }
        }

        /// <summary>Picks an option: the dialog closes, then its action runs (once, however often it's pressed).</summary>
        public void Choose(int index)
        {
            if (chosen || index < 0 || index >= options.Length) { return; }
            chosen = true;
            Action action = options[index].Action;
            Close();
            action?.Invoke();
        }

        /// <summary>Back: the cancel option, if there's one; else nothing.</summary>
        public override void OnCancelButton() => Choose(cancelIndex);

        /// <summary>Submit: nothing (the selected button takes it, through the EventSystem).</summary>
        public override void OnSubmitButton()
        {
        }
    }
}
