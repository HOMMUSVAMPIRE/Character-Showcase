using System.Collections.Generic;

namespace MeridianMotion.Showcase.Audio
{
    /// <summary>
    /// Holds each Sound Event to its limits: no more than its Max Instances playing at once, and none sooner than its
    /// Cooldown after the last one started (so twenty footsteps in one frame are one).
    /// </summary>
    internal sealed class SoundRateLimiter
    {
        private readonly Dictionary<SoundEvent, float> lastStarted = new(64);
        private readonly Dictionary<SoundEvent, int> playing = new(64);

        /// <summary>Whether another of this sound may start now.</summary>
        public bool Allows(SoundEvent sound, float now)
        {
            if (playing.TryGetValue(sound, out int count) && count >= sound.MaxInstances) { return false; }
            return !lastStarted.TryGetValue(sound, out float last) || now - last >= sound.Cooldown;
        }

        /// <summary>One of this sound started.</summary>
        public void Started(SoundEvent sound, float now)
        {
            lastStarted[sound] = now;
            playing[sound] = playing.TryGetValue(sound, out int count) ? count + 1 : 1;
        }

        /// <summary>One of this sound ended (or was stolen).</summary>
        public void Ended(SoundEvent sound)
        {
            if (sound != null && playing.TryGetValue(sound, out int count)) { playing[sound] = count > 1 ? count - 1 : 0; }
        }
    }
}
