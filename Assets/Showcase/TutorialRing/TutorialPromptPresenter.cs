using System;
using MeridianMotion.Characters;
using MeridianMotion.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MeridianMotion.Showcase.TutorialRing
{
    /// <summary>
    /// Shows the Tutorial Ring's lesson prompts: the player's current lesson (its TutorialLessonTracker) as text, each
    /// {Action} written as its binding on the device in use (PromptText, ControlSchemeTracker), written again when the
    /// player switches device or the lesson's state changes (a rebinding shows from the next of those); shown in the Lesson Prompt window (its prefab, opened on the
    /// UI Manager). Hidden hides every prompt (for footage; remembered between sessions, and the development keys toggle
    /// it) while the lessons are still tracked; a replay playing (the scene's ReplayRecorder) hides them too.
    /// </summary>
    [AddComponentMenu("Meridian Motion/Showcase/Tutorial Prompt Presenter")]
    public class TutorialPromptPresenter : MonoBehaviour
    {
        /// <summary>The PlayerPrefs key Hidden is remembered under.</summary>
        public const string HiddenKey = "Tutorial.PromptsHidden";

        [Tooltip("The Lesson Prompt window's prefab (a TutorialLessonPrompt)")]
        [SerializeField] private GameObject promptPrefab;
        [Tooltip("The input actions the prompts name (the player's); empty: the player input reader's")]
        [SerializeField] private InputActionAsset actions;

        private TutorialLessonTracker tracker;
        private TutorialLessonPrompt window;
        private ReplayRecorder replays;
        private bool replaying;
        private static bool? hidden;

        /// <summary>Raised with the prompt's text whenever it changes ("" for none).</summary>
        public event Action<string> Changed;

        /// <summary>What the prompt says now ("" for none), hidden or not.</summary>
        public string Text { get; private set; } = "";
        /// <summary>What's on the screen: Text, unless prompts are hidden or a replay's playing.</summary>
        public string ShownText => Hidden || replaying ? "" : Text;
        /// <summary>The Lesson Prompt window's prefab.</summary>
        public GameObject PromptPrefab { get => promptPrefab; set => promptPrefab = value; }
        /// <summary>The input actions the prompts name.</summary>
        public InputActionAsset Actions { get => actions; set => actions = value; }
        /// <summary>The tracker it shows the lessons of.</summary>
        public TutorialLessonTracker Tracker => tracker;

        /// <summary>Whether every prompt is hidden (for footage): remembered between sessions.</summary>
        public static bool Hidden
        {
            get => hidden ??= PlayerPrefs.GetInt(HiddenKey, 0) != 0;
            set
            {
                if (Hidden == value) { return; }
                hidden = value;
                PlayerPrefs.SetInt(HiddenKey, value ? 1 : 0);
                HiddenChanged?.Invoke(value);
            }
        }

        /// <summary>Raised when Hidden changes.</summary>
        public static event Action<bool> HiddenChanged;

        /// <summary>Shows the tracker's lessons (the player's character's).</summary>
        public void Show(TutorialLessonTracker lessons)
        {
            if (tracker != null) { tracker.Changed -= Rewrite; }
            tracker = lessons;
            if (tracker != null) { tracker.Changed += Rewrite; }
            Rewrite();
        }

        private InputActionAsset ActionsInUse
        {
            get
            {
                if (actions != null) { return actions; }
                PlayerInputReader reader = PlayerInputReader.instance;
                return reader != null && reader.playerInput != null ? reader.playerInput.actions : null;
            }
        }

        /// <summary>The lesson's text for what it's showing, its actions written out for the device in use.</summary>
        public string TextFor(TutorialLesson lesson, TutorialLessonTracker.Shown shown)
        {
            if (lesson == null || shown == TutorialLessonTracker.Shown.Nothing) { return ""; }
            string template = shown == TutorialLessonTracker.Shown.FollowUp ? lesson.followUp : lesson.prompt;
            InputActionAsset asset = ActionsInUse;
            return PromptText.Format(template, asset, ControlSchemeTracker.CurrentScheme(asset));
        }

        /// <summary>
        /// Writes the prompt again: done when the lesson's state or the device in use changes. A rebinding in the pause
        /// menu shows from the next of those (call this to show it at once).
        /// </summary>
        public void Rewrite()
        {
            string text = tracker != null ? TextFor(tracker.Current, tracker.Showing) : "";
            bool changed = text != Text;
            Text = text;
            Present();
            if (changed) { Changed?.Invoke(Text); }
        }

        private void Present()
        {
            string shown = ShownText;
            if (window == null && shown.Length > 0 && promptPrefab != null && UIManager.instance != null)
            {
                window = UIManager.instance.OpenUI<TutorialLessonPrompt>(promptPrefab);
            }
            if (window != null) { window.Show(shown); }
        }

        private void OnEnable()
        {
            ControlSchemeTracker.Listen();
            ControlSchemeTracker.Changed += OnDeviceChanged;
            HiddenChanged += OnHiddenChanged;
        }

        private void OnDisable()
        {
            ControlSchemeTracker.Changed -= OnDeviceChanged;
            HiddenChanged -= OnHiddenChanged;
        }

        private void OnDestroy()
        {
            if (tracker != null) { tracker.Changed -= Rewrite; }
        }

        private void Update()
        {
            // Looked for now and then, not every frame, in a scene that has none.
            if (replays == null && Time.frameCount % 30 == 0) { replays = FindAnyObjectByType<ReplayRecorder>(); }
            bool playing = replays != null && replays.IsPlaying;
            if (playing == replaying) { return; }
            replaying = playing;
            Present();
        }

        private void OnDeviceChanged(InputDevice device) => Rewrite();
        private void OnHiddenChanged(bool nowHidden) => Present();
    }
}
