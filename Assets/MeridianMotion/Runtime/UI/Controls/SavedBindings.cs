using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MeridianMotion.UI
{
    /// <summary>
    /// Keeps the player's rebinding of an input action asset between runs: its binding overrides are loaded from
    /// PlayerPrefs (under Key) as it wakes, and saved there when a controls list changes one (SaveFor). Put it where it
    /// wakes with the game (the UI Manager has one, for the package's input actions).
    /// </summary>
    [AddComponentMenu("Meridian Motion/UI/Saved Bindings")]
    public class SavedBindings : MonoBehaviour
    {
        [Tooltip("The input actions whose rebinding is kept (the player's)")]
        [SerializeField] private InputActionAsset actions;
        [Tooltip("The PlayerPrefs key they're kept under")]
        [SerializeField] private string key = "MeridianMotion.BindingOverrides";

        private static readonly List<SavedBindings> active = new();

        /// <summary>The input actions whose rebinding is kept.</summary>
        public InputActionAsset Actions { get => actions; set => actions = value; }
        /// <summary>The PlayerPrefs key they're kept under.</summary>
        public string Key { get => key; set => key = value; }

        /// <summary>Saves the overrides of every SavedBindings keeping these actions (what a controls list calls).</summary>
        public static void SaveFor(InputActionAsset asset)
        {
            if (asset == null) { return; }
            foreach (SavedBindings saved in active) { if (saved.actions == asset) { saved.Save(); } }
        }

        /// <summary>Puts the saved overrides on the actions (the asset's own bindings, if none are saved).</summary>
        public void Load()
        {
            if (actions == null) { return; }
            string json = PlayerPrefs.GetString(key, "");
            if (string.IsNullOrEmpty(json)) { return; }
            actions.LoadBindingOverridesFromJson(json, removeExisting: true);
        }

        /// <summary>Saves the actions' overrides.</summary>
        public void Save()
        {
            if (actions == null) { return; }
            PlayerPrefs.SetString(key, actions.SaveBindingOverridesAsJson());
            PlayerPrefs.Save();
        }

        private void Awake() => Load();

        private void OnEnable() => active.Add(this);

        private void OnDisable() => active.Remove(this);
    }
}
