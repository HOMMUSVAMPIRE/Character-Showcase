using System.Collections.Generic;
using UnityEngine;

namespace MeridianMotion.Showcase.Audio
{
    /// <summary>
    /// Every Sound Event and bus the game has, so a request's id can be turned back into its sound. The AudioService is
    /// given one. Its editor fills it with every Sound Event in the project and gives the new ones their ids (the next
    /// free: ids are never reused).
    /// </summary>
    [CreateAssetMenu(menuName = "Meridian Motion/Audio/Sound Library", fileName = "Sound Library")]
    public class SoundLibrary : ScriptableObject
    {
        [Tooltip("Every Sound Event the game plays. Find Every Sound Event (in its inspector) fills it")]
        [SerializeField] private SoundEvent[] sounds = new SoundEvent[0];
        [Tooltip("Every bus, whose saved volumes the service reads as it starts")]
        [SerializeField] private AudioBus[] buses = new AudioBus[0];
        [Tooltip("The highest id it has ever given: new ones follow it, so an id is never given twice, even after its sound's deleted")]
        [SerializeField] private int lastGivenId;

        private Dictionary<int, SoundEvent> byId;

        /// <summary>The sounds.</summary>
        public IReadOnlyList<SoundEvent> Sounds => sounds;
        /// <summary>The buses.</summary>
        public IReadOnlyList<AudioBus> Buses => buses;

        /// <summary>The sound with this id, or null (none, or not in the library).</summary>
        public SoundEvent Find(SoundId id)
        {
            if (id.IsNone) { return null; }
            if (byId == null)
            {
                byId = new Dictionary<int, SoundEvent>(sounds.Length);
                foreach (SoundEvent sound in sounds)
                {
                    if (sound != null && !sound.Id.IsNone) { byId[sound.Id.Value] = sound; }
                }
            }
            return byId.TryGetValue(id.Value, out SoundEvent found) ? found : null;
        }

        /// <summary>The id for a new sound: one more than the highest ever given.</summary>
        public int NextFreeId()
        {
            int highest = lastGivenId;
            foreach (SoundEvent sound in sounds)
            {
                if (sound != null && sound.Id.Value > highest) { highest = sound.Id.Value; }
            }
            return highest + 1;
        }

        /// <summary>
        /// Makes the library the sounds found (its editor finds every Sound Event in the project), each with an id of its
        /// own: a sound keeps its id, unless it has none or one another has. When two share one (a Sound Event duplicated
        /// keeps its original's), the one the library already had keeps it. New ids follow the highest ever given, so a
        /// deleted sound's id is never given again. Returns the sounds whose id it changed, to be saved.
        /// </summary>
        public List<SoundEvent> AssignIds(IReadOnlyList<SoundEvent> found)
        {
            var wanted = new HashSet<SoundEvent>(found);
            var ordered = new List<SoundEvent>(found.Count);
            foreach (SoundEvent sound in sounds) { if (sound != null && wanted.Contains(sound) && !ordered.Contains(sound)) { ordered.Add(sound); } }
            foreach (SoundEvent sound in found) { if (sound != null && !ordered.Contains(sound)) { ordered.Add(sound); } }

            foreach (SoundEvent sound in ordered) { lastGivenId = Mathf.Max(lastGivenId, sound.Id.Value); }
            var changed = new List<SoundEvent>();
            var seen = new HashSet<int>();
            foreach (SoundEvent sound in ordered)
            {
                if (!sound.Id.IsNone && seen.Add(sound.Id.Value)) { continue; }
                sound.AssignId(new SoundId(++lastGivenId));
                seen.Add(lastGivenId);
                changed.Add(sound);
            }
            sounds = ordered.ToArray();
            byId = null;
            return changed;
        }

        /// <summary>Sets what's in it (its editor, and tests).</summary>
        public void Set(SoundEvent[] newSounds, AudioBus[] newBuses)
        {
            sounds = newSounds ?? new SoundEvent[0];
            buses = newBuses ?? new AudioBus[0];
            byId = null;
        }

        private void OnValidate() => byId = null;
    }
}
