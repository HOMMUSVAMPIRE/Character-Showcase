using MeridianMotion.Characters;
using MeridianMotion.UI;
using UnityEngine;

namespace MeridianMotion.CharacterUI
{
    /// <summary>
    /// The controls page's Sprint option (beside the player's input reader: Set Up Scene puts it there): whether Sprint is
    /// held or toggled (PlayerInputReader.ToggleSprint). It registers with the UI (ControlOptions), so the controls page's
    /// Sprint button shows and flips it, and keeps the player's choice between runs (in PlayerPrefs, under its Key),
    /// putting it back as it starts.
    /// </summary>
    [AddComponentMenu("Meridian Motion/Character UI/Sprint Mode Option")]
    public class SprintModeOption : MonoBehaviour, IControlOption
    {
        [Tooltip("The input reader whose Sprint it sets (none: the one beside it, else the scene's)")]
        [SerializeField] private PlayerInputReader reader;
        [Tooltip("Where the player's choice is kept between runs (a PlayerPrefs key)")]
        [SerializeField] private string key = "MeridianMotion.ToggleSprint";

        /// <summary>The input reader whose Sprint it sets.</summary>
        public PlayerInputReader Reader { get => reader; set => reader = value; }
        /// <summary>Where the player's choice is kept.</summary>
        public string Key { get => key; set => key = value; }

        public string Name => "Sprint";
        public string OffText => "Hold";
        public string OnText => "Toggle";

        /// <summary>Whether Sprint toggles (the reader's Toggle Sprint); setting it keeps the choice.</summary>
        public bool Value
        {
            get => Find() != null && reader.ToggleSprint;
            set
            {
                if (Find() == null) { return; }
                reader.ToggleSprint = value;
                PlayerPrefs.SetInt(key, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        private void OnEnable()
        {
            // The player's choice from an earlier run, if they made one; else the reader's own setting stands.
            if (Find() != null && PlayerPrefs.HasKey(key)) { reader.ToggleSprint = PlayerPrefs.GetInt(key) != 0; }
            ControlOptions.Register(this);
        }

        private void OnDisable() => ControlOptions.Unregister(this);

        private PlayerInputReader Find()
        {
            if (reader == null) { reader = GetComponent<PlayerInputReader>(); }
            if (reader == null) { reader = PlayerInputReader.instance; }
            return reader;
        }
    }
}
