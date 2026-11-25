using System.Collections.Generic;
using UnityEngine;

namespace MeridianMotion.Showcase.Audio
{
    /// <summary>
    /// Chooses which of a Sound Event's clips plays (as its Clip Order says), remembering the last of each so it doesn't
    /// repeat it. Its randomness is its own (System.Random), so playing sounds never disturbs anything else's.
    /// </summary>
    public sealed class ClipPicker
    {
        private readonly System.Random random;
        private readonly Dictionary<SoundEvent, int> last = new(64);

        public ClipPicker(int seed) => random = new System.Random(seed);

        /// <summary>The clip to play this time, or null for a sound with none.</summary>
        public AudioClip Pick(SoundEvent sound)
        {
            if (sound == null || !sound.HasClips) { return null; }
            AudioClip[] clips = sound.Clips;
            int count = clips.Length;
            bool had = last.TryGetValue(sound, out int previous);
            int index;
            switch (sound.Order)
            {
                case ClipOrder.InOrder:
                    index = had ? (previous + 1) % count : 0;
                    break;
                case ClipOrder.RandomNoRepeat when count > 1 && had:
                    // One of the others, evenly: a number below count - 1, stepped over the last one.
                    index = random.Next(count - 1);
                    if (index >= previous) { index++; }
                    break;
                default:
                    index = random.Next(count);
                    break;
            }
            last[sound] = index;
            return clips[index];
        }

        /// <summary>A number in [min, max], for a sound's volume and pitch.</summary>
        public float Range(Vector2 range) => range.x + (float)random.NextDouble() * (range.y - range.x);
    }
}
