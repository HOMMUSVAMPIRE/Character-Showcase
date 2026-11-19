using UnityEngine;

namespace MeridianMotion.Characters
{
    /// <summary>
    /// Turns input read every render frame into one input frame per physics step (CharacterInputFrame). A device is read
    /// in Update, at the frame rate; the movement steps in FixedUpdate, at its own rate, so a frame may run no physics
    /// step, or several. Sample is called each render frame, Take once a physics step: the frame it gives holds each
    /// button that's down now or went down at any time since the last step's frame, so a tap shorter than a step is
    /// still seen; the move input and the look are the latest. Taken again within the same physics step (two characters
    /// reading one device), it gives the same frame, so neither misses a tap the other took.
    /// </summary>
    public sealed class InputLatch
    {
        private Vector2 move;
        private float lookYaw, lookPitch;
        private InputButtons held, pressedSinceTaken;

        private bool taken;
        private float takenAtStep;
        private CharacterInputFrame lastTaken;

        /// <summary>
        /// Takes the device as it is this render frame: the move input, the look (yaw and pitch, degrees), the buttons down,
        /// and those that went down this frame (pressed and maybe released again within it, which held can miss).
        /// </summary>
        public void Sample(Vector2 moveInput, float yaw, float pitch, InputButtons down, InputButtons pressedThisFrame)
        {
            move = moveInput;
            lookYaw = yaw;
            lookPitch = pitch;
            held = down;
            pressedSinceTaken |= pressedThisFrame;
        }

        /// <summary>
        /// The frame for the physics step at stepTime (Time.fixedTime), tagged with tick: the latest move input and look,
        /// and every button down now or pressed since the last step's frame. The same frame again for the same step.
        /// </summary>
        public CharacterInputFrame Take(uint tick, float stepTime)
        {
            if (!taken || stepTime != takenAtStep)
            {
                lastTaken = new CharacterInputFrame
                {
                    move = move,
                    lookYaw = lookYaw,
                    lookPitch = lookPitch,
                    held = held | pressedSinceTaken,
                };
                pressedSinceTaken = InputButtons.None;
                taken = true;
                takenAtStep = stepTime;
            }
            CharacterInputFrame frame = lastTaken;
            frame.tick = tick;
            return frame;
        }
    }
}
