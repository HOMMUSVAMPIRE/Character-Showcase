using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Character
{
    /// <summary>The buttons a character's input can hold, as flags (see CharacterInputFrame.held).</summary>
    [Flags]
    public enum InputButtons : uint
    {
        None = 0,
        Jump = 1 << 0,
        Dash = 1 << 1,
        Sprint = 1 << 2,
        Crouch = 1 << 3,
        Roll = 1 << 4,
        Grab = 1 << 5,
        /// <summary>Defined in the input actions, read by nothing yet.</summary>
        Attack = 1 << 6,
    }

    /// <summary>
    /// What a character's input is doing for one physics step (tick): its move input, where it's looking, and which
    /// buttons are held. Nothing derived from earlier steps (a press, how long a button's been held): the character works
    /// that out from successive frames (see InputState), so a frame says only what the player is doing, and the same
    /// frames always give the same movement, whatever the frame rate they were made at.
    /// </summary>
    /// <remarks>
    /// Unmanaged (blittable: fixed-size fields, no references), so frames can be copied, kept in a ring (InputRecording),
    /// written to a file, compared, and sent as they are. Keep it so: InputRecording requires it at compile time.
    /// </remarks>
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct CharacterInputFrame : IEquatable<CharacterInputFrame>
    {
        /// <summary>The physics step it's for (the character's MovementController.Tick when it's stepped).</summary>
        public uint tick;
        /// <summary>The move input on the ground plane (x right, y forward, each -1 to 1), relative to the look.</summary>
        public Vector2 move;
        /// <summary>The look direction's yaw (degrees about up) and pitch (degrees, up positive), as the player aims.</summary>
        public float lookYaw, lookPitch;
        /// <summary>The buttons held.</summary>
        public InputButtons held;

        /// <summary>Whether any of the buttons given is held.</summary>
        public readonly bool Holds(InputButtons buttons) => (held & buttons) != 0;

        /// <summary>
        /// A frame of scripted input: the move input and which of its buttons are down. Its buttons' timings (presses, hold
        /// durations) aren't carried: the character works them out from successive frames. Allocates nothing.
        /// </summary>
        public static CharacterInputFrame Holding(Inputs inputs, uint tick)
        {
            var frame = new CharacterInputFrame { tick = tick };
            if (inputs == null) { return frame; }
            frame.move = new Vector2(inputs.movementVector.x, inputs.movementVector.z);
            if (Down(inputs.jump)) { frame.held |= InputButtons.Jump; }
            if (Down(inputs.dash)) { frame.held |= InputButtons.Dash; }
            if (Down(inputs.sprint)) { frame.held |= InputButtons.Sprint; }
            if (Down(inputs.crouch)) { frame.held |= InputButtons.Crouch; }
            if (Down(inputs.roll)) { frame.held |= InputButtons.Roll; }
            if (Down(inputs.grab)) { frame.held |= InputButtons.Grab; }
            return frame;
        }

        private static bool Down(InputButton button) => button != null && button.value;

        public readonly bool Equals(CharacterInputFrame other) =>
            tick == other.tick && move.Equals(other.move) && lookYaw.Equals(other.lookYaw) && lookPitch.Equals(other.lookPitch) && held == other.held;

        public override readonly bool Equals(object obj) => obj is CharacterInputFrame other && Equals(other);
        public override readonly int GetHashCode() => HashCode.Combine(tick, move, lookYaw, lookPitch, held);
        public override readonly string ToString() => $"tick {tick}: move {move}, look ({lookYaw:0.#}, {lookPitch:0.#}), held {held}";
    }
}
