using System;
using System.IO;
using MeridianMotion.Characters;
using MeridianMotion.UI;
using UnityEngine;

namespace MeridianMotion.CharacterUI
{
    /// <summary>
    /// The pause menu's Replays tab, for a scene's ReplayRecorder (beside it, on the Replay Recorder prefab): it registers
    /// as the UI's replay controls (ReplayControls), so the tab shows. Save Replay saves the run under way (and says
    /// where); Load Replay plays the newest replay saved for the scene. When a replay ends, a choice: Continue playing
    /// (from there, the player's input back, recording afresh), Replay again, or Reset the level (the scene again). And,
    /// as Save On Quit says (in builds, by default), Quit and Load Replay save the run first if nothing's been saved this
    /// session, so a playtest isn't lost.
    /// </summary>
    [AddComponentMenu("Meridian Motion/Character UI/Replay Menu")]
    public class ReplayMenu : MonoBehaviour, IReplayControls
    {
        /// <summary>When the run's saved before quitting (and before playing a replay), if nothing's been saved this session.</summary>
        public enum SaveOnQuit { InBuilds, Always, Never }

        [Tooltip("The recorder it drives (none: the one beside it)")]
        [SerializeField] private ReplayRecorder recorder;
        [Tooltip("When the run's saved before quitting or playing a replay, if nothing's been saved this session: in builds (a playtest's), always, or never")]
        [SerializeField] private SaveOnQuit saveOnQuit = SaveOnQuit.InBuilds;

        [Header("Text")]
        [Tooltip("What the dialog at a replay's end says")]
        [SerializeField] private string endText = "The replay's over.";
        [Tooltip("The end dialog's choice to play on from where the replay ended")]
        [SerializeField] private string continueLabel = "Continue playing";
        [Tooltip("The end dialog's choice to watch it again")]
        [SerializeField] private string replayLabel = "Replay again";
        [Tooltip("The end dialog's choice to start the level again")]
        [SerializeField] private string resetLabel = "Reset level";
        [Tooltip("The notification's title when a replay's saved (its message is the file's path)")]
        [SerializeField] private string savedTitle = "Replay saved";

        /// <summary>The recorder it drives.</summary>
        public ReplayRecorder Recorder => recorder;
        /// <summary>When the run's saved before quitting.</summary>
        public SaveOnQuit SavesOnQuit { get => saveOnQuit; set => saveOnQuit = value; }
        /// <summary>The end-of-replay dialog open now, or null.</summary>
        public UIChoiceDialog EndDialog { get; private set; }

        private void Awake()
        {
            if (recorder == null) { recorder = GetComponent<ReplayRecorder>(); }
        }

        private void OnEnable()
        {
            ReplayControls.Register(this);
            if (recorder != null) { recorder.PlaybackFinished += OnPlaybackFinished; }
        }

        private void OnDisable()
        {
            ReplayControls.Unregister(this);
            if (recorder != null) { recorder.PlaybackFinished -= OnPlaybackFinished; }
        }

        // ---- IReplayControls ----

        public bool CanSave => recorder != null && !recorder.IsPlaying && recorder.RecordedFrames > 0;

        public bool CanLoad => recorder != null && recorder.Character != null && (recorder.LatestReplayPath() != null || (SavesFirst && CanSave));

        public string Status
        {
            get
            {
                if (recorder == null) { return ""; }
                string now = recorder.IsPlaying ? "Playing a replay" : recorder.IsRecording ? $"Recording: {Clock(recorder.RecordedSeconds)}" : "Not recording";
                string saved = ReplayRecorder.LastSavedPath != null ? $"\nLast saved: {Path.GetFileName(ReplayRecorder.LastSavedPath)}" : "";
                return now + saved;
            }
        }

        public void SaveReplay()
        {
            string path = recorder != null ? recorder.Save() : null;
            if (path != null && UIManager.instance != null) { UIManager.instance.ShowNotification(savedTitle, path); }
        }

        public void LoadReplay()
        {
            if (recorder == null) { return; }
            if (SavesFirst && CanSave && !ReplayRecorder.SavedThisSession) { recorder.Save(); }
            string path = recorder.LatestReplayPath();
            if (path == null || !recorder.PlayFile(path))
            {
                UIMessageBox.OpenError(path == null ? "There's no replay of this level to play." : $"Couldn't play the replay {Path.GetFileName(path)}.");
            }
        }

        public void BeforeQuit()
        {
            if (SavesFirst && CanSave && !ReplayRecorder.SavedThisSession) { recorder.Save(); }
        }

        // ---- A replay's end ----

        /// <summary>Continue playing: the player's input back, where the replay ended, and a fresh recording.</summary>
        public void ContinuePlaying() => recorder?.StopPlayback(recordAfter: true);

        /// <summary>Replay again, from its start.</summary>
        public void ReplayAgain() => recorder?.PlayAgain();

        /// <summary>Reset the level: the scene again (the level's clock put back first).</summary>
        public void ResetLevel()
        {
            recorder?.StopPlayback(recordAfter: false);
            ReplayClock.Remove();
            PauseMenu.ReloadScene();
        }

        private void OnPlaybackFinished(ReplayRecorder finished)
        {
            EndDialog = UIChoiceDialog.Open(endText, 0,
                new UIChoiceDialog.Option(continueLabel, ContinuePlaying, ThemedButton.Kind.Primary),
                new UIChoiceDialog.Option(replayLabel, ReplayAgain),
                new UIChoiceDialog.Option(resetLabel, ResetLevel));
        }

        private bool SavesFirst => saveOnQuit == SaveOnQuit.Always || (saveOnQuit == SaveOnQuit.InBuilds && !Application.isEditor);

        private static string Clock(float seconds) => TimeSpan.FromSeconds(seconds).ToString(seconds >= 3600f ? @"h\:mm\:ss" : @"m\:ss");
    }
}
