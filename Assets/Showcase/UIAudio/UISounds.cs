using System.Collections.Generic;
using MeridianMotion.Showcase.Audio;
using MeridianMotion.UI;
using UnityEngine;
using UnityEngine.UI;

namespace MeridianMotion.Showcase.UIAudio
{
    /// <summary>
    /// The UI's sounds, from the UI framework's generic hooks (it knows nothing of sound): a window opening or closing,
    /// the Back key, a notification. As each window opens it gives the window's controls (its Selectables and tab groups)
    /// a UISoundRelay, once each, for their hover, click, toggle, slider and tab sounds (and again a frame later, for the
    /// rows its lists make as they start). Only windows the player uses (they need the cursor) play the open and close
    /// sounds: name tags and HUD prompts are windows to the framework too. It listens while it's enabled and stops as it
    /// goes, so a restarted scene doesn't hear the old one's. Without an audio service, nothing plays. Set Up Scene puts it
    /// on the scene's Audio object (it needs no UI Manager: the hooks are static).
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Meridian Motion/Audio/UI Sounds")]
    public class UISounds : MonoBehaviour
    {
        [Tooltip("The UI's sounds")]
        [SerializeField] private UISoundSet sounds;

        /// <summary>The UI's sounds.</summary>
        public UISoundSet Sounds { get => sounds; set => sounds = value; }

        private void OnEnable()
        {
            RootUIPrefab.AnyOpened += HandleOpened;
            RootUIPrefab.AnyClosed += HandleClosed;
            UIManager.BackPressed += HandleBack;
            Notifications.AnyShown += HandleNotification;
        }

        private void OnDisable()
        {
            RootUIPrefab.AnyOpened -= HandleOpened;
            RootUIPrefab.AnyClosed -= HandleClosed;
            UIManager.BackPressed -= HandleBack;
            Notifications.AnyShown -= HandleNotification;
        }

        /// <summary>Plays one of the UI's sounds (2D: where doesn't matter).</summary>
        public void Play(SoundEvent sound)
        {
            if (!isActiveAndEnabled || sound == null) { return; }
            AudioService service = AudioService.Instance;
            if (service != null) { service.Play(sound, Vector3.zero); }
        }

        private void HandleOpened(RootUIPrefab window)
        {
            if (sounds == null || window == null) { return; }
            if (IsAWindowToThePlayer(window)) { Play(sounds.WindowOpen); }
            // Once per window opened (not per frame): finding its controls allocates, which is fine here.
            RelayControls(window);
            // A window's lists fill their rows as they start, after it's opened: looked at again next frame.
            if (!lookAgain.Contains(window)) { lookAgain.Add(window); }
            lookAgainFrame = Time.frameCount + 1;
        }

        private readonly List<RootUIPrefab> lookAgain = new();
        private int lookAgainFrame;

        private void LateUpdate()
        {
            if (lookAgain.Count == 0 || Time.frameCount < lookAgainFrame) { return; }
            foreach (RootUIPrefab window in lookAgain) { if (window != null) { RelayControls(window); } }
            lookAgain.Clear();
        }

        private void RelayControls(RootUIPrefab window)
        {
            foreach (Selectable control in window.GetComponentsInChildren<Selectable>(true)) { Relay(control.gameObject); }
            foreach (TabGroup tabs in window.GetComponentsInChildren<TabGroup>(true)) { Relay(tabs.gameObject); }
        }

        private void Relay(GameObject control)
        {
            if (!control.TryGetComponent(out UISoundRelay relay)) { relay = control.AddComponent<UISoundRelay>(); }
            relay.Owner = this;
        }

        // A window the player opens and uses (a menu, a dialog: it needs the cursor), not a name tag, a HUD prompt or a
        // click blocker, which are windows only to the UI framework: they make no open or close sound.
        private static bool IsAWindowToThePlayer(RootUIPrefab window) => window.NeedsCursor && !window.IsBlocker;

        private void HandleClosed(RootUIPrefab window)
        {
            if (sounds != null && window != null && IsAWindowToThePlayer(window)) { Play(sounds.WindowClose); }
        }

        private void HandleBack()
        {
            if (sounds != null) { Play(sounds.Back); }
        }

        private void HandleNotification(NotificationEntry entry)
        {
            if (sounds != null) { Play(sounds.Notification); }
        }
    }
}
