using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MeridianMotion.UI
{
    /// <summary>
    /// A button on the controls page for one of the game's control options (an IControlOption, found by its Option Name
    /// among those registered: ControlOptions): it reads "SPRINT: HOLD" or "SPRINT: TOGGLE" (the option's name and its
    /// state), and pressing it flips the option. Its button's hidden while the game's registered no option of the name,
    /// so a game without it shows nothing.
    /// </summary>
    [AddComponentMenu("Meridian Motion/UI/Control Option Toggle")]
    public class ControlOptionToggle : MonoBehaviour
    {
        [Tooltip("The name of the option it shows and flips (the game's IControlOption's Name)")]
        [SerializeField] private string optionName = "Sprint";
        [Tooltip("Its button: pressing it calls Flip. Hidden while there's no option of the name")]
        [SerializeField] private Button button;
        [Tooltip("The button's label (none: the button's own text)")]
        [SerializeField] private TMP_Text label;

        /// <summary>The name of the option it shows.</summary>
        public string OptionName { get => optionName; set { optionName = value; Refresh(); } }
        /// <summary>Its button.</summary>
        public Button Button { get => button; set { button = value; Refresh(); } }
        /// <summary>What its label reads now ("" while there's no option).</summary>
        public string Text => label != null ? label.text : "";

        private void OnEnable()
        {
            ControlOptions.Changed += Refresh;
            Refresh();
        }

        private void OnDisable() => ControlOptions.Changed -= Refresh;

        /// <summary>Flips the option (its button calls this).</summary>
        public void Flip()
        {
            IControlOption option = ControlOptions.Find(optionName);
            if (option != null) { ControlOptions.Set(option, !option.Value); }
        }

        /// <summary>Shows the option's state, or hides the button if there's no option.</summary>
        public void Refresh()
        {
            IControlOption option = ControlOptions.Find(optionName);
            if (button != null && button.gameObject.activeSelf != (option != null)) { button.gameObject.SetActive(option != null); }
            if (label == null && button != null) { label = button.GetComponentInChildren<TMP_Text>(true); }
            if (label != null) { label.text = option != null ? $"{option.Name}: {(option.Value ? option.OnText : option.OffText)}".ToUpperInvariant() : ""; }
        }
    }
}
