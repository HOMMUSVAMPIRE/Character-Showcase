using UnityEngine;

namespace MeridianMotion.UI
{
    /// <summary>
    /// Makes the Back key open the pause menu when no window takes it: registers PauseMenu.Open as its UIManager's fallback
    /// handler while it's enabled. On the UI Manager prefab; take it off (or turn it off) for a game that does something
    /// else with Back, and register that with UIManager.RegisterFallbackHandler.
    /// </summary>
    [AddComponentMenu("Meridian Motion/UI/Pause Menu On Back")]
    [RequireComponent(typeof(UIManager))]
    public class PauseMenuOnBack : MonoBehaviour
    {
        // Both: on the same object as its UIManager, it may be enabled before the manager's Awake has made it the
        // instance; Start comes after every Awake.
        private void OnEnable() => Register();
        private void Start() => Register();

        private void Register()
        {
            var manager = GetComponent<UIManager>();
            if (manager == UIManager.instance) { manager.RegisterFallbackHandler(OpenPauseMenu); }
        }

        private void OnDisable()
        {
            var manager = GetComponent<UIManager>();
            if (manager == UIManager.instance) { manager.RegisterFallbackHandler(null); }
        }

        private static void OpenPauseMenu() => PauseMenu.Open();
    }
}
