using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MeridianMotion.UI
{
    /// <summary>
    /// A dialogue panel (ReferenceAssets/OpenTasks.md, H6): a portrait, the speaker's name on its header tab, their line
    /// (written out over the UI's clock, Characters Per Second; none: at once) and a "next" arrow once it's all out.
    /// Submit (or Advance) shows the rest of a line being written, else goes on to the next (Play's lines in turn), and
    /// after the last closes it and calls the onFinished given. It knows nothing of who's talking: the game gives it the lines.
    /// </summary>
    [AddComponentMenu("Meridian Motion/UI/HUD/Dialogue Panel")]
    public class DialoguePanel : RootUIPrefab
    {
        /// <summary>A line: who says it, what, and their portrait (or none).</summary>
        public struct Line
        {
            public string Speaker;
            public string Text;
            public Sprite Portrait;

            public Line(string speaker, string text, Sprite portrait = null)
            {
                Speaker = speaker;
                Text = text;
                Portrait = portrait;
            }
        }

        [Header("Dialogue")]
        [Tooltip("The speaker's name (on the header tab)")]
        [SerializeField] private TMP_Text speakerText;
        [Tooltip("The line")]
        [SerializeField] private TMP_Text lineText;
        [Tooltip("The speaker's portrait (hidden without one; its frame stays)")]
        [SerializeField] private Image portrait;
        [Tooltip("The \"next\" arrow, shown once the line's all out")]
        [SerializeField] private GameObject nextArrow;
        [Tooltip("How many characters a second a line's written out at (0: at once)")]
        [Min(0f)] [SerializeField] private float charactersPerSecond = 60f;

        private readonly Queue<Line> lines = new();
        private Action onFinished;
        private float lineStartedAt;
        private int lineLength;
        // Whether the line's been shown whole at once (Submit while it was being written).
        private bool shownWhole;

        /// <summary>The line shown.</summary>
        public Line Current { get; private set; }
        /// <summary>Whether the line's all out (so Advance goes on to the next).</summary>
        public bool LineComplete => VisibleCharacters >= lineLength;
        /// <summary>How many of the line's characters are out.</summary>
        public int VisibleCharacters => charactersPerSecond <= 0f || shownWhole
            ? lineLength
            : Mathf.FloorToInt(Mathf.Clamp((UIClock.Now - lineStartedAt) * charactersPerSecond, 0f, lineLength));
        /// <summary>How many lines are still to come after this one.</summary>
        public int LinesLeft => lines.Count;

        /// <summary>Opens the panel (the window catalog's) and plays the lines; onFinished is called after the last.</summary>
        public static DialoguePanel Open(IEnumerable<Line> lines, Action onFinished = null)
        {
            DialoguePanel panel = UIManager.instance != null ? UIManager.instance.Open<DialoguePanel>() : null;
            if (panel != null) { panel.Play(lines, onFinished); }
            return panel;
        }

        /// <summary>Plays the lines, from the first.</summary>
        public void Play(IEnumerable<Line> toPlay, Action finished = null)
        {
            lines.Clear();
            foreach (Line line in toPlay) { lines.Enqueue(line); }
            onFinished = finished;
            Next();
        }

        /// <summary>Submit: shows the rest of the line, else goes on to the next (after the last, closes and finishes).</summary>
        public void Advance()
        {
            if (!LineComplete)
            {
                shownWhole = true;
                Show();
                return;
            }
            Next();
        }

        public override void OnSubmitButton() => Advance();

        /// <summary>Back: skips the rest of the conversation (it closes, and its onFinished is called).</summary>
        public override void OnCancelButton()
        {
            lines.Clear();
            shownWhole = true;
            Next();
        }

        private void Next()
        {
            if (lines.Count == 0)
            {
                Action finished = onFinished;
                onFinished = null;
                Close();
                finished?.Invoke();
                return;
            }
            Current = lines.Dequeue();
            if (speakerText != null) { speakerText.text = Current.Speaker; }
            if (lineText != null)
            {
                lineText.text = Current.Text;
                lineText.ForceMeshUpdate();
            }
            if (portrait != null)
            {
                portrait.sprite = Current.Portrait;
                portrait.enabled = Current.Portrait != null;
            }
            lineLength = Current.Text?.Length ?? 0;
            lineStartedAt = UIClock.Now;
            shownWhole = false;
            Show();
        }

        private void Update()
        {
            if (!LineComplete || (nextArrow != null && !nextArrow.activeSelf)) { Show(); }
        }

        // The line written out so far, and the arrow once it's all out.
        private void Show()
        {
            if (lineText != null) { lineText.maxVisibleCharacters = VisibleCharacters; }
            UIUtility.SetActive(nextArrow, LineComplete);
        }
    }
}
