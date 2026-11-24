using MeridianMotion.UI;
using TMPro;
using UnityEngine;

namespace MeridianMotion.Showcase.TutorialRing
{
    /// <summary>
    /// The Tutorial Ring's lesson prompt: a line of large text at the lower centre of the screen, over a backing for
    /// legibility, shown and hidden by the TutorialPromptPresenter. A HUD window: it doesn't close with the others, take
    /// Back, or want the cursor.
    /// </summary>
    [AddComponentMenu("Meridian Motion/Showcase/Tutorial Lesson Prompt")]
    public class TutorialLessonPrompt : RootUIPrefab
    {
        [Header("Prompt")]
        [Tooltip("Where the prompt's text is shown")]
        [SerializeField] private TMP_Text text;
        [Tooltip("Shown while there's a prompt (its backing and text: hidden, the window stays open to show the next)")]
        [SerializeField] private GameObject contents;

        /// <summary>The text shown ("" while hidden).</summary>
        public string Text => text != null && Showing ? text.text : "";
        /// <summary>Whether a prompt is showing.</summary>
        public bool Showing => contents == null ? gameObject.activeSelf : contents.activeSelf;

        /// <summary>Shows the text (empty: hides it).</summary>
        public void Show(string shown)
        {
            if (string.IsNullOrEmpty(shown))
            {
                UIUtility.SetActive(contents, false);
                return;
            }
            if (text != null) { text.text = shown; }
            UIUtility.SetActive(contents, true);
        }
    }
}
