using UnityEngine;

namespace Character
{
    /// <summary>
    /// A character's input as its movement reads it, worked out each physics step from that step's input frame and the
    /// ones before (see CharacterInputFrame): which buttons are down, which went down or came up this step, and how long
    /// each has been held, counted in steps (so the same frames give the same presses and holds at any frame rate). The
    /// movement reads it through View, an Inputs the character owns and nothing else changes. Part of the character's
    /// state: saved and restored with it (MovementSimulation.Save).
    /// </summary>
    public sealed class InputState
    {
        // The buttons View has, in the order the step counts below are kept in.
        private static readonly InputButtons[] Buttons =
            { InputButtons.Jump, InputButtons.Dash, InputButtons.Sprint, InputButtons.Crouch, InputButtons.Roll, InputButtons.Grab };

        // For each button: how many steps it's been held since the step it went down (0 on that step), and how many it was
        // held the last time it came up.
        private readonly int[] heldSteps = new int[Buttons.Length];
        private readonly int[] lastHeldSteps = new int[Buttons.Length];

        /// <summary>The input as the movement reads it, updated in place each step.</summary>
        public Inputs View { get; } = new()
        {
            movementVector = Vector3.zero,
            jump = new InputButton(),
            dash = new InputButton(),
            sprint = new InputButton(),
            crouch = new InputButton(),
            roll = new InputButton(),
            grab = new InputButton(),
        };

        /// <summary>The frame the last step was given.</summary>
        public CharacterInputFrame Frame { get; private set; }

        /// <summary>
        /// Takes the next step's frame: View's move input from it, and each button's state from whether the frame holds it
        /// and the steps before. stepLength (seconds) turns the steps a button's been held into the durations View gives.
        /// </summary>
        public void Advance(in CharacterInputFrame frame, float stepLength)
        {
            Frame = frame;
            View.movementVector = new Vector3(frame.move.x, 0f, frame.move.y);
            Advance(View.jump, 0, frame, stepLength);
            Advance(View.dash, 1, frame, stepLength);
            Advance(View.sprint, 2, frame, stepLength);
            Advance(View.crouch, 3, frame, stepLength);
            Advance(View.roll, 4, frame, stepLength);
            Advance(View.grab, 5, frame, stepLength);
        }

        private void Advance(InputButton button, int index, in CharacterInputFrame frame, float stepLength)
        {
            bool down = frame.Holds(Buttons[index]);
            bool wasDown = button.value;
            button.value = down;
            button.justPressed = down && !wasDown;
            button.justReleased = !down && wasDown;
            if (down)
            {
                heldSteps[index] = button.justPressed ? 0 : heldSteps[index] + 1;
            }
            else
            {
                if (button.justReleased) { lastHeldSteps[index] = heldSteps[index]; }
                heldSteps[index] = 0;
            }
            // Steps times the step's length, as a hold counted step by step was always given (and as a scripted hold of n
            // steps is), so a duration is the same float however it's reached.
            button.holdDuration = heldSteps[index] * stepLength;
            button.lastHoldDuration = lastHeldSteps[index] * stepLength;
        }
    }
}
