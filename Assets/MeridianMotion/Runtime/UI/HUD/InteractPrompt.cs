using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MeridianMotion.UI
{
    /// <summary>
    /// The interact prompt (ReferenceAssets/OpenTasks.md, H6): a hazard-striped call-out with an icon, a label, the binding
    /// to press (read from its Action, in the control scheme in use, so it follows a switch to a gamepad) and a hold ring.
    /// Shown, filled and hidden from code: Show(label, icon), SetHold(fraction) or Hold(seconds), Hide. It knows nothing
    /// of what's interacted with: the game says what to show. A HUD window: it doesn't close with the others, take Back,
    /// or want the cursor.
    /// </summary>
    [AddComponentMenu("Meridian Motion/UI/HUD/Interact Prompt")]
    public class InteractPrompt : RootUIPrefab
    {
        [Header("Prompt")]
        [Tooltip("What it says to do (\"Climb\", \"Open\")")]
        [SerializeField] private TMP_Text label;
        [Tooltip("The binding to press")]
        [SerializeField] private TMP_Text bindingText;
        [Tooltip("The icon by the label")]
        [SerializeField] private UIIcon icon;
        [Tooltip("The ring filled while a hold's under way (optional)")]
        [SerializeField] private UIRadialProgress holdRing;
        [Tooltip("The action whose binding it shows (the interact action)")]
        [SerializeField] private InputActionReference action;
        [Tooltip("Shown while the prompt's up (its contents: hidden, it's still open, ready to show again)")]
        [SerializeField] private GameObject contents;

        /// <summary>Whether it's showing.</summary>
        public bool Showing => contents == null ? gameObject.activeSelf : contents.activeSelf;
        /// <summary>The label shown.</summary>
        public string Label => label != null ? label.text : "";
        /// <summary>The binding shown.</summary>
        public string Binding => bindingText != null ? bindingText.text : "";
        /// <summary>The icon shown.</summary>
        public UIIcon Icon => icon;
        /// <summary>The hold ring.</summary>
        public UIRadialProgress HoldRing => holdRing;
        /// <summary>The action whose binding it shows.</summary>
        public InputAction Action { get; set; }

        /// <summary>The prompt (the window catalog's), opened if it isn't, and shown with the label and icon.</summary>
        public static InteractPrompt Open(string label, string iconName = null)
        {
            InteractPrompt prompt = UIManager.instance != null ? UIManager.instance.Open<InteractPrompt>() : null;
            if (prompt != null) { prompt.Show(label, iconName); }
            return prompt;
        }

        protected override void Awake()
        {
            base.Awake();
            if (Action == null && action != null) { Action = action.action; }
        }

        /// <summary>Shows the prompt with the label and icon (none: the icon's hidden), its hold ring empty.</summary>
        public void Show(string text, string iconName = null)
        {
            if (label != null) { label.text = text; }
            if (icon != null)
            {
                UIUtility.SetActive(icon, !string.IsNullOrEmpty(iconName));
                if (!string.IsNullOrEmpty(iconName)) { icon.IconName = iconName; }
            }
            UpdateBinding();
            if (holdRing != null) { holdRing.Release(); }
            UIUtility.SetActive(contents, true);
        }

        /// <summary>Hides the prompt (it stays open, to show again).</summary>
        public void Hide()
        {
            if (holdRing != null) { holdRing.Release(); }
            UIUtility.SetActive(contents, false);
        }

        /// <summary>Sets how far the hold is (0 to 1).</summary>
        public void SetHold(float fraction)
        {
            if (holdRing != null) { holdRing.Fraction = fraction; }
        }

        /// <summary>Fills the hold ring over seconds (its Completed says when it's full).</summary>
        public void Hold(float seconds)
        {
            if (holdRing != null) { holdRing.Hold(seconds); }
        }

        /// <summary>Shows the action's binding in the control scheme in use (as it's rebound, or the player switches devices).</summary>
        public void UpdateBinding()
        {
            if (bindingText == null) { return; }
            string scheme = Action != null ? ControlSchemeTracker.CurrentScheme(Action.actionMap?.asset) : null;
            string shown = Action == null ? "" : scheme != null ? Action.GetBindingDisplayString(InputBinding.MaskByGroup(scheme)) : Action.GetBindingDisplayString();
            bindingText.text = shown;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            ControlSchemeTracker.Listen();
            ControlSchemeTracker.Changed += OnDeviceChanged;
        }

        protected override void OnDisable()
        {
            ControlSchemeTracker.Changed -= OnDeviceChanged;
            base.OnDisable();
        }

        private void OnDeviceChanged(InputDevice device) => UpdateBinding();
    }
}
