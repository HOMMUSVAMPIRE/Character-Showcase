using MeridianMotion.Levels;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MeridianMotion.Showcase.TutorialRing
{
    /// <summary>
    /// Development keys for the Tutorial Ring, for testing it and recording footage, only in the editor and development
    /// builds (a playtest build has none: it turns itself off): Hide Prompts Key shows and hides every prompt, Open
    /// Shortcuts Key opens both shortcuts, and End Session Key saves the session (its replay and results).
    /// </summary>
    [AddComponentMenu("Meridian Motion/Showcase/Tutorial Ring Dev Keys")]
    public class TutorialRingDevKeys : MonoBehaviour
    {
        [Tooltip("Shows and hides every lesson prompt (for footage)")]
        [SerializeField] private Key hidePromptsKey = Key.F6;
        [Tooltip("Opens every shortcut gate (to reach either half straight away)")]
        [SerializeField] private Key openShortcutsKey = Key.F7;
        [Tooltip("Ends the play session: saves its replay and writes its results")]
        [SerializeField] private Key endSessionKey = Key.F8;
        [Tooltip("The scene's tutorial setup, whose session End Session Key ends")]
        [SerializeField] private TutorialRingSetup setup;

        /// <summary>Whether development keys are allowed: in the editor and development builds only. Tests may override it.</summary>
        public static System.Func<bool> Allowed = () => Application.isEditor || Debug.isDebugBuild;

        /// <summary>The scene's tutorial setup.</summary>
        public TutorialRingSetup Setup { get => setup; set => setup = value; }

        private void OnEnable()
        {
            if (!Allowed()) { enabled = false; }
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) { return; }
            if (keyboard[hidePromptsKey].wasPressedThisFrame) { TutorialPromptPresenter.Hidden = !TutorialPromptPresenter.Hidden; }
            if (keyboard[openShortcutsKey].wasPressedThisFrame) { ShortcutGate.OpenAll(); }
            if (keyboard[endSessionKey].wasPressedThisFrame && setup != null && setup.Session != null)
            {
                Debug.Log($"[TutorialRingDevKeys] Session saved: {setup.Session.End()}");
            }
        }
    }
}
