using UnityEngine;

namespace MeridianMotion.Characters
{
    /// <summary>
    /// Sprint as a toggle rather than held (PlayerInputReader's Toggle Sprint): a press of Sprint starts sprinting and the
    /// next press ends it, as does stopping once the character's moved since it started (so a sprint started standing
    /// still waits for the character to set off). While it's on, Sprint reads as held; the press that ends it isn't a
    /// press. Plain logic, applied to each render frame's reading before the input frame's made, so the movement only
    /// ever sees Sprint held or not, as from a held button, and replays and determinism are unaffected.
    /// </summary>
    public sealed class SprintToggle
    {
        // Below this much move input (its length), the character's taken to have stopped.
        private const float StoppedBelow = 0.1f;

        private bool enabled, on, moved;

        /// <summary>Whether Sprint toggles (off: it's read as the button is, held). Setting it ends a sprint under way.</summary>
        public bool Enabled
        {
            get => enabled;
            set
            {
                enabled = value;
                on = moved = false;
            }
        }

        /// <summary>Whether it's toggled on now.</summary>
        public bool Sprinting => enabled && on;

        /// <summary>Filters one frame's reading: the move input, the buttons down, and those pressed this frame.</summary>
        public void Apply(Vector2 move, ref InputButtons down, ref InputButtons pressed)
        {
            if (!enabled) { return; }
            bool moving = move.sqrMagnitude > StoppedBelow * StoppedBelow;
            if ((pressed & InputButtons.Sprint) != 0)
            {
                on = !on;
                moved = moving;
            }
            else if (on)
            {
                if (moving) { moved = true; }
                else if (moved) { on = false; }
            }
            down = on ? down | InputButtons.Sprint : down & ~InputButtons.Sprint;
            if (!on) { pressed &= ~InputButtons.Sprint; }
        }
    }
}
