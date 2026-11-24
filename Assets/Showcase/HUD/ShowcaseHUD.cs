using System.Collections.Generic;
using MeridianMotion.Levels;
using MeridianMotion.UI;
using UnityEngine;
using UnityEngine.Events;

namespace MeridianMotion.Showcase.HUD
{
    /// <summary>
    /// The showcase levels' HUD, built on the UI framework (its notifications and world labels): each lap or run a course
    /// timer finishes shown as a notification (a new best as a banner across the middle), and each split it records
    /// (with how far ahead of or behind the best run it is); a hint on the first spawn that
    /// the pause menu lists the controls; and a name tag over each NPC (NpcLabel). It knows the level elements (the course
    /// timers, found in the scene as it starts) but nothing of the character: Set Up Scene connects the spawner's On
    /// Character Spawned to ShowControlsHint and its On Npc Spawned to AddNpcLabel, in the showcase's own scenes only.
    /// </summary>
    [AddComponentMenu("Meridian Motion/Showcase/Showcase HUD")]
    public class ShowcaseHUD : MonoBehaviour
    {
        [Header("Laps")]
        [Tooltip("Added to a lap's time when it's the best yet (shown as a banner across the middle)")]
        [SerializeField] private string newBestText = "New best!";
        [Tooltip("A split's notification title, after the course's name ({0} is the split's number, from 1)")]
        [SerializeField] private string splitTitle = "Split {0}";
        [Tooltip("How long (s) a split's notification shows")]
        [Min(0f)] [SerializeField] private float splitSeconds = 2.5f;

        [Header("NPCs")]
        [Tooltip("The name tag put over each NPC (with an NpcLabel on its root)")]
        [SerializeField] private GameObject npcLabelPrefab;

        [Header("Controls hint")]
        [Tooltip("The hint's title, shown as the player's character first spawns")]
        [SerializeField] private string controlsHintTitle = "Controls";
        [Tooltip("The hint's message")]
        [TextArea] [SerializeField] private string controlsHintMessage = "Escape pauses the game: the pause menu's Controls tab lists every control.";
        [Tooltip("How long (s) the hint shows")]
        [Min(0f)] [SerializeField] private float controlsHintSeconds = 8f;

        private readonly List<(CourseTimer timer, UnityAction<float, bool> listener, System.Action<int, float, float> splits)> listening = new();
        private bool hintShown;

        /// <summary>The name tag prefab.</summary>
        public GameObject NpcLabelPrefab { get => npcLabelPrefab; set => npcLabelPrefab = value; }
        /// <summary>Added to a lap's time when it's the best yet.</summary>
        public string NewBestText => newBestText;
        /// <summary>The controls hint's title.</summary>
        public string ControlsHintTitle => controlsHintTitle;

        private void Start()
        {
            foreach (CourseTimer timer in FindObjectsByType<CourseTimer>(FindObjectsInactive.Include, FindObjectsSortMode.None)) { Listen(timer); }
        }

        private void OnDestroy()
        {
            foreach ((CourseTimer timer, UnityAction<float, bool> listener, System.Action<int, float, float> splits) in listening)
            {
                if (timer == null) { continue; }
                timer.OnLapFinished.RemoveListener(listener);
                timer.SplitPassed -= splits;
            }
            listening.Clear();
        }

        /// <summary>Shows the timer's finished laps and splits (Start does it for every timer in the scene; this, for one added later).</summary>
        public void Listen(CourseTimer timer)
        {
            if (timer == null || listening.Exists(entry => entry.timer == timer)) { return; }
            UnityAction<float, bool> listener = (time, best) => LapFinished(timer, time, best);
            System.Action<int, float, float> splits = (index, time, best) => SplitPassed(timer, index, time, best);
            timer.OnLapFinished.AddListener(listener);
            timer.SplitPassed += splits;
            listening.Add((timer, listener, splits));
        }

        /// <summary>Shows a split: its course and number, its time, and how far ahead of (-) or behind (+) the best run it is, if there's one.</summary>
        public void SplitPassed(CourseTimer timer, int index, float time, float bestRunsTime)
        {
            if (UIManager.instance == null) { return; }
            string course = timer != null ? timer.CourseName : "";
            string message = Clock(time);
            if (bestRunsTime > 0f) { message += $"  ({(time - bestRunsTime >= 0f ? "+" : "-")}{Mathf.Abs(time - bestRunsTime):0.00})"; }
            UIManager.instance.ShowNotification($"{course} {string.Format(splitTitle, index + 1)}", message, splitSeconds);
        }

        /// <summary>Shows a finished lap: its course and time, a new best as a banner across the middle.</summary>
        public void LapFinished(CourseTimer timer, float time, bool best)
        {
            if (UIManager.instance == null) { return; }
            string course = timer != null ? timer.CourseName : "";
            if (best) { UIManager.instance.ShowNotification(course, $"{Clock(time)}  {newBestText}", 0f, Notifications.Location.Center, Notifications.Type.Center); }
            else { UIManager.instance.ShowNotification(course, Clock(time)); }
        }

        /// <summary>The first time it's called (the player's character's first spawn), shows the controls hint.</summary>
        public void ShowControlsHint(Transform character)
        {
            if (hintShown || UIManager.instance == null) { return; }
            hintShown = true;
            UIManager.instance.ShowNotification(controlsHintTitle, controlsHintMessage, controlsHintSeconds);
        }

        /// <summary>Puts a name tag over the NPC (for the spawner's On Npc Spawned: a persistent listener returns nothing).</summary>
        public void AddNpcLabel(Transform npc, string npcName) => NpcLabel.Open(npcLabelPrefab, npc, npcName);

        // A time as minutes, seconds and hundredths, as the course timers show it.
        private static string Clock(float seconds) => $"{(int)(seconds / 60f)}:{seconds % 60f:00.00}";
    }
}
