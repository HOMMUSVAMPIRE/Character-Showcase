using UnityEngine;

namespace Character
{
    /// <summary>
    /// Turns a button into one-off presses: a press counts once, and only within a buffer of when it was made, so a
    /// press made just before an ability is ready still starts it, but holding the button doesn't start it again.
    /// </summary>
    public class ButtonPress
    {
        private bool consumed;

        /// <summary>Call every step: a released button frees its next press.</summary>
        public void Track(InputButton button)
        {
            if (button == null || !button.value) { consumed = false; }
        }

        /// <summary>Whether the button is down on a press that hasn't been used yet, made at most buffer seconds ago.</summary>
        public bool IsFresh(InputButton button, float buffer) =>
            button != null && button.value && !consumed && button.holdDuration <= buffer;

        /// <summary>
        /// Marks the current press as used, so it starts nothing else until the button is released and pressed again
        /// (for something that used it without asking Consume, as a jump off a wall checks the press first).
        /// </summary>
        public void Use() => consumed = true;

        /// <summary>Uses up the press if it's fresh (see IsFresh), returning whether it was.</summary>
        public bool Consume(InputButton button, float buffer)
        {
            if (!IsFresh(button, buffer)) { return false; }
            consumed = true;
            return true;
        }
    }
}
