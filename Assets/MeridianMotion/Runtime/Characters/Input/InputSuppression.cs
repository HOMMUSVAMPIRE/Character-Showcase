using UnityEngine;

namespace MeridianMotion.Characters
{
    /// <summary>
    /// Stops a device's input reaching the character for a while (a window open over the game, say: PlayerInputReader's
    /// Suppressed). While it's suppressed, nothing is held and nothing is pressed. A button still held when it ends is
    /// left out until it's let go, so a key held through a menu (Jump, to close it with, say) isn't taken as a press the
    /// moment the menu closes. Plain logic, applied to each render frame's reading.
    /// </summary>
    public sealed class InputSuppression
    {
        // The buttons held when suppression ended, left out until each is let go.
        private InputButtons heldThrough;

        /// <summary>Whether the input is being suppressed.</summary>
        public bool Suppressed { get; set; }

        /// <summary>Filters one frame's reading: the move input, the buttons down, and those pressed this frame.</summary>
        public void Apply(ref Vector2 move, ref InputButtons down, ref InputButtons pressed)
        {
            if (Suppressed)
            {
                heldThrough |= down;
                move = Vector2.zero;
                down = InputButtons.None;
                pressed = InputButtons.None;
                return;
            }
            // A button let go is taken again from its next press.
            heldThrough &= down;
            down &= ~heldThrough;
            pressed &= ~heldThrough;
        }
    }
}
