using MeridianMotion.Characters;
using MeridianMotion.Showcase.Audio;
using UnityEngine;

namespace MeridianMotion.Showcase.CharacterAudio
{
    public partial class CharacterSounds
    {
        // How fast (m/s, at the authored size) the character must go for a loop that plays only while moving.
        private const float MovingSpeed = 0.3f;
        // A loop's least strength, however slow.
        private const float MinLoopStrength = 0.3f;

        private CharacterState[] loopFlags = new CharacterState[0];
        private bool[] loopParsed = new bool[0];
        private SoundHandle[] loopHandles = new SoundHandle[0];
        private SoundHandle windHandle;

        private void ReadLoopRows()
        {
            StateLoop[] rows = soundSet != null ? soundSet.Loops : new StateLoop[0];
            loopFlags = new CharacterState[rows.Length];
            loopParsed = new bool[rows.Length];
            loopHandles = new SoundHandle[rows.Length];
            for (int i = 0; i < rows.Length; i++)
            {
                if (rows[i] == null) { continue; }
                loopParsed[i] = CharacterState.TryParse(rows[i].stateName, out loopFlags[i]);
                if (!loopParsed[i]) { Warn(rows[i].stateName, "character state"); }
            }
        }

        /// <summary>Stops every loop at once (the character going, or its set changing).</summary>
        private void StopLoops()
        {
            AudioService service = AudioService.Instance;
            for (int i = 0; i < loopHandles.Length; i++)
            {
                if (service != null) { service.Stop(loopHandles[i]); }
                loopHandles[i] = SoundHandle.None;
            }
            if (service != null) { service.Stop(windHandle); }
            windHandle = SoundHandle.None;
        }

        private void Update()
        {
            AudioService service = AudioService.Instance;
            if (service == null || soundSet == null || motor == null || motor.lastStep == null) { return; }
            MovementStep step = motor.lastStep;
            CharacterState state = step.state;
            Vector3 velocity = step.finalVelocity;
            float speed = velocity.magnitude;
            Vector3 feet = motor.Feet;

            StateLoop[] rows = soundSet.Loops;
            for (int i = 0; i < rows.Length && i < loopHandles.Length; i++)
            {
                if (!loopParsed[i]) { continue; }
                StateLoop row = rows[i];
                bool on = state.HasAny(loopFlags[i]) && (!row.onlyWhileMoving || speed > MovingSpeed * motor.SpeedScale);
                float strength = Mathf.Lerp(MinLoopStrength, 1f, speed / (row.fullStrengthSpeed * motor.SpeedScale));
                loopHandles[i] = Keep(service, loopHandles[i], on, row.loop, feet, strength, row.fadeOut);
            }

            bool falling = IsPlayer && !state.HasAny(CharacterState.Grounded) && -velocity.y > soundSet.WindSpeed * motor.SpeedScale;
            float windStrength = Mathf.Lerp(MinLoopStrength, 1f, (-velocity.y - soundSet.WindSpeed * motor.SpeedScale)
                                                                 / Mathf.Max(0.01f, soundSet.WindSpeed * motor.SpeedScale));
            windHandle = Keep(service, windHandle, falling, soundSet.Wind, feet, windStrength, fadeOut: 0.3f);
        }

        /// <summary>Starts, follows or fades out a loop, as whether it should be on says; its handle after.</summary>
        private SoundHandle Keep(AudioService service, SoundHandle handle, bool on, SoundEvent sound, Vector3 at, float strength,
                                 float fadeOut)
        {
            bool playing = service.IsPlaying(handle);
            if (on && !playing) { return Play(sound, at, strength, SoundFlags.Follow); }
            if (on)
            {
                service.SetPosition(handle, at);
                service.SetStrength(handle, strength);
                return handle;
            }
            if (playing) { service.Stop(handle, fadeOut); }
            return SoundHandle.None;
        }
    }
}
