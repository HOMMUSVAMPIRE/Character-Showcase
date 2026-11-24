using TMPro;
using UnityEngine;

namespace MeridianMotion.UI
{
    /// <summary>
    /// A controls list's row (ControlsList): the action's name and its binding. A button's binding is on a button that
    /// rebinds it (OnBindingClicked, wired to it), reading Listening Text while it waits for the new button; a binding that
    /// can't be rebound (a stick, a composite) is plain text. Overridden Mark shows while its binding isn't the asset's.
    /// </summary>
    [AddComponentMenu("Meridian Motion/UI/Control Binding Display")]
    public class ControlBindingDisplay : DataDisplay<ControlBinding>
    {
        [Tooltip("The action's name")]
        [SerializeField] private TMP_Text nameText;
        [Tooltip("The button that rebinds it (its label shows the binding)")]
        [SerializeField] private ThemedButton bindingButton;
        [Tooltip("The binding button's label")]
        [SerializeField] private TMP_Text bindingButtonText;
        [Tooltip("The binding as plain text, for one that can't be rebound")]
        [SerializeField] private TMP_Text bindingText;
        [Tooltip("Shown while its binding isn't the asset's (optional)")]
        [SerializeField] private GameObject overriddenMark;
        [Tooltip("What the binding button reads while it waits for the new button")]
        [SerializeField] private string listeningText = "PRESS A BUTTON";

        /// <summary>Whether it's waiting for its new button.</summary>
        public bool Listening { get; private set; }

        /// <summary>What its binding button reads (or its plain text, if it can't be rebound).</summary>
        public string ShownBinding => Data != null && Data.Rebindable ? (bindingButtonText != null ? bindingButtonText.text : "") : (bindingText != null ? bindingText.text : "");

        protected override void Refresh()
        {
            if (Data == null) { return; }
            if (nameText != null) { nameText.text = Data.DisplayName; }
            UIUtility.SetActive(bindingButton, Data.Rebindable);
            UIUtility.SetActive(bindingText, !Data.Rebindable);
            if (bindingText != null) { bindingText.text = Data.BindingText; }
            UIUtility.SetActive(overriddenMark, Data.Overridden);
            SetListening(false);
        }

        public override void InitializeFromID(string id)
        {
            ControlsList list = GetComponentInParent<ControlsList>();
            if (list != null && list.Bindings(list.Scheme).TryGetValue(id, out ControlBinding binding)) { Initialize(binding, null); }
        }

        /// <summary>Rebinds it (its list's StartRebind): for its binding button.</summary>
        public void OnBindingClicked()
        {
            ControlsList list = GetComponentInParent<ControlsList>();
            if (list != null) { list.StartRebind(Data); }
        }

        /// <summary>Shows it waiting for its new button (its button reads Listening Text, in the highlight), or not.</summary>
        public void SetListening(bool listening)
        {
            Listening = listening;
            if (bindingButtonText != null && Data != null) { bindingButtonText.text = listening ? listeningText : Data.BindingText; }
            if (bindingButton != null) { bindingButton.ButtonKind = listening ? ThemedButton.Kind.Highlight : ThemedButton.Kind.Outline; }
        }
    }
}
