using System.Collections.Generic;
using UnityEngine;

namespace MeridianMotion.Showcase.Audio
{
    /// <summary>
    /// The buses' volumes, as the player set them: saved in PlayerPrefs (a key per bus, its name after a prefix) and read
    /// once each, then kept. A bus nobody has set is at its Default Volume.
    /// </summary>
    internal sealed class AudioVolumes
    {
        private readonly Dictionary<AudioBus, float> volumes = new(8);
        private string prefix;

        public AudioVolumes(string prefix) => this.prefix = prefix ?? "";

        /// <summary>The prefix its keys are under. Changing it forgets what was read.</summary>
        public string Prefix
        {
            get => prefix;
            set
            {
                prefix = value ?? "";
                volumes.Clear();
            }
        }

        /// <summary>A bus's volume: saved, else its default.</summary>
        public float Load(AudioBus bus)
        {
            if (volumes.TryGetValue(bus, out float volume)) { return volume; }
            volume = Mathf.Clamp01(PlayerPrefs.GetFloat(prefix + bus.BusName, bus.DefaultVolume));
            volumes[bus] = volume;
            return volume;
        }

        /// <summary>Sets and saves a bus's volume.</summary>
        public void Save(AudioBus bus, float volume)
        {
            volumes[bus] = volume;
            PlayerPrefs.SetFloat(prefix + bus.BusName, volume);
            PlayerPrefs.Save();
        }
    }
}
