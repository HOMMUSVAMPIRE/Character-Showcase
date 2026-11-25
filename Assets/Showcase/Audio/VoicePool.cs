using UnityEngine;

namespace MeridianMotion.Showcase.Audio
{
    /// <summary>
    /// One of the AudioService's voices: an AudioSource made once, and what it's playing. Its timing (when it ends, a fade,
    /// a pause) is kept here rather than read from the AudioSource, so it's the same whether or not audio is running (in
    /// batch mode it isn't).
    /// </summary>
    internal sealed class Voice
    {
        public AudioSource source;
        public bool inUse;
        /// <summary>Bumped each time the voice is given out, so an old handle to it does nothing.</summary>
        public int generation;
        public SoundEvent sound;
        public EmitterId emitter;
        public int priority;
        public float startedAt;
        /// <summary>When it ends (unscaled seconds); infinity for a loop.</summary>
        public float endsAt;
        /// <summary>Its volume as chosen (the Sound Event's range), before its strength, its bus and a fade.</summary>
        public float chosenVolume;
        public float strength;
        public bool follow;
        public bool paused;
        public float pausedAt;
        public bool fading;
        public float fadeElapsed;
        public float fadeLength;

        /// <summary>How much of its volume a fade leaves (1: no fade).</summary>
        public float FadeFactor => fading && fadeLength > 0f ? Mathf.Clamp01(1f - fadeElapsed / fadeLength) : 1f;
    }

    /// <summary>
    /// The AudioService's voices: a fixed number of AudioSources made when it starts, never more and never made or
    /// destroyed while playing. When all are busy, a new sound takes the voice of the lowest-priority sound playing (the
    /// oldest of those), if its own priority is higher; else it isn't played. (Not at equal priority: a loop restarted
    /// as soon as it's taken would take another's in turn, and in a crowd they'd all start over every frame.)
    /// </summary>
    internal sealed class VoicePool
    {
        private readonly Voice[] voices;
        private int nextGeneration = 1;

        public VoicePool(Transform parent, int count)
        {
            voices = new Voice[count];
            for (int i = 0; i < count; i++)
            {
                var obj = new GameObject($"Voice {i}");
                obj.transform.SetParent(parent, false);
                AudioSource source = obj.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.dopplerLevel = 0f;
                voices[i] = new Voice { source = source };
            }
        }

        /// <summary>How many voices there are.</summary>
        public int Count => voices.Length;
        /// <summary>The voice at this index.</summary>
        public Voice this[int index] => voices[index];

        /// <summary>
        /// A voice for a sound of this priority: a free one, else the lowest-priority (the oldest of those) whose priority
        /// is lower, given a new generation. False when every voice is playing something that matters more.
        /// </summary>
        public bool TryAcquire(int priority, out int index)
        {
            index = -1;
            for (int i = 0; i < voices.Length; i++)
            {
                if (!voices[i].inUse) { index = i; break; }
            }
            if (index < 0)
            {
                for (int i = 0; i < voices.Length; i++)
                {
                    Voice voice = voices[i];
                    if (voice.priority >= priority) { continue; }
                    if (index < 0 || voice.priority < voices[index].priority
                        || (voice.priority == voices[index].priority && voice.startedAt < voices[index].startedAt))
                    {
                        index = i;
                    }
                }
                if (index < 0) { return false; }
            }
            Voice chosen = voices[index];
            chosen.generation = nextGeneration++;
            if (nextGeneration == int.MaxValue) { nextGeneration = 1; }
            return true;
        }

        /// <summary>The voice a handle names, or null if the handle is stale (the voice since freed or reused).</summary>
        public Voice Find(SoundHandle handle)
        {
            if (!handle.IsValid || (uint)handle.voice >= (uint)voices.Length) { return null; }
            Voice voice = voices[handle.voice];
            return voice.inUse && voice.generation == handle.generation ? voice : null;
        }

        /// <summary>Destroys the voices' objects (the service going away).</summary>
        public void Destroy()
        {
            foreach (Voice voice in voices)
            {
                if (voice.source != null) { Object.Destroy(voice.source.gameObject); }
            }
        }
    }
}
