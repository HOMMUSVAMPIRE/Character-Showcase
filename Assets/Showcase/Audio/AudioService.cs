using System;
using System.Collections.Generic;
using UnityEngine;

namespace MeridianMotion.Showcase.Audio
{
    /// <summary>
    /// Plays the game's sounds: the one entry point (Play), one per scene. A request (SoundRequest) names its sound by id,
    /// found in the Sound Library; it goes through the filters (ISoundFilter: rules over all sounds), is dropped if it's a
    /// 3D sound beyond its Max Distance from the listener, is held to its sound's limits, and takes a voice from a fixed
    /// pool (VoicePool: the lowest-priority playing gives way to a higher one when all are busy). Loops are stopped, moved and made louder
    /// or quieter through the handle Play gives. A sound with no clips plays nothing, silently.
    ///
    /// Its sounds' volumes are their bus's (AudioBus) times their own. While the game is paused (Time.timeScale 0), the
    /// voices of buses paused with the game pause, and go on from where they were; the rest (music, the UI) play on.
    /// Everything here runs on unscaled time. Nothing it does reaches back into the game.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Meridian Motion/Audio/Audio Service")]
    public partial class AudioService : MonoBehaviour
    {
        [Header("Sounds")]
        [Tooltip("Every sound and bus the game has: requests name their sound by its id in this")]
        [SerializeField] private SoundLibrary library;

        [Header("Voices")]
        [Tooltip("How many sounds can play at once (the AudioSources made as it starts). When all are busy, the " +
                 "lowest-priority sound gives way to one that matters more")]
        [Min(4)] [SerializeField] private int voiceCount = 32;
        [Tooltip("The seed of its choices of clip, volume and pitch. 0: a new one each run")]
        [SerializeField] private int seed;

        [Header("Volumes")]
        [Tooltip("What the buses' saved volumes are kept under in PlayerPrefs (this, then the bus's name). Keep it once " +
                 "players have saved volumes")]
        [SerializeField] private string volumeKeyPrefix = "MeridianMotion.Audio.Volume.";

        /// <summary>The scene's audio service, or null when there's none (then nothing makes sound, silently).</summary>
        public static AudioService Instance { get; private set; }

        /// <summary>Raised for each request that was played (after its filters), for tests and, later, netcode.</summary>
        public event Action<SoundRequest> Played;

        private VoicePool pool;
        private SoundRateLimiter limiter;
        private AudioVolumes volumes;
        private ClipPicker picker;
        private readonly List<ISoundFilter> filters = new();
        private Transform listener;
        private float nextListenerSearch;

        // How often (s) it looks for a listener when there's none, so a scene without one doesn't search every sound.
        private const float ListenerSearchInterval = 1f;

        /// <summary>The library requests' ids are found in.</summary>
        public SoundLibrary Library { get => library; set => library = value; }

        /// <summary>How many voices it has. Changing it while playing stops every sound and makes them again.</summary>
        public int VoiceCount
        {
            get => voiceCount;
            set
            {
                value = Mathf.Max(4, value);
                if (value == voiceCount && pool != null) { return; }
                voiceCount = value;
                if (pool != null) { BuildPool(); }
            }
        }

        /// <summary>
        /// Where sounds are heard from, for dropping those too far to hear. Null: the scene's AudioListener, found when
        /// needed (and again if it goes).
        /// </summary>
        public Transform Listener { get => listener; set => listener = value; }

        /// <summary>How many voices are playing.</summary>
        public int PlayingCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < pool.Count; i++) { if (pool[i].inUse) { count++; } }
                return count;
            }
        }

        protected virtual void Awake()
        {
            // Here as well as on enable: what plays as a scene loads may wake before the service is enabled.
            Instance = this;
            limiter = new SoundRateLimiter();
            volumes = new AudioVolumes(volumeKeyPrefix);
            picker = new ClipPicker(seed != 0 ? seed : Environment.TickCount);
            BuildPool();
        }

        /// <summary>What the buses' saved volumes are kept under in PlayerPrefs.</summary>
        public string VolumeKeyPrefix
        {
            get => volumeKeyPrefix;
            set
            {
                volumeKeyPrefix = value ?? "";
                if (volumes != null) { volumes.Prefix = volumeKeyPrefix; }
            }
        }

        /// <summary>A bus's volume (0 to 1): as the player saved it, else its Default Volume.</summary>
        public float GetVolume(AudioBus bus) => bus == null ? 1f : (volumes ??= new AudioVolumes(volumeKeyPrefix)).Load(bus);

        /// <summary>Sets a bus's volume (held to 0 to 1), saved, and applied at once to what's playing on it.</summary>
        public void SetVolume(AudioBus bus, float volume)
        {
            if (bus == null) { return; }
            (volumes ??= new AudioVolumes(volumeKeyPrefix)).Save(bus, Mathf.Clamp01(volume));
            if (pool == null) { return; }
            for (int i = 0; i < pool.Count; i++)
            {
                if (pool[i].inUse && pool[i].sound.Bus == bus) { ApplyVolume(pool[i]); }
            }
        }

        private float BusVolume(AudioBus bus) => GetVolume(bus);

        protected virtual void OnEnable() => Instance = this;

        protected virtual void OnDisable()
        {
            if (Instance == this) { Instance = null; }
        }

        protected virtual void OnDestroy() => pool?.Destroy();

        private void BuildPool()
        {
            if (pool != null)
            {
                for (int i = 0; i < pool.Count; i++) { if (pool[i].inUse) { Release(pool[i]); } }
                pool.Destroy();
            }
            pool = new VoicePool(transform, voiceCount);
        }

        /// <summary>Adds a rule over every sound (run in the order added).</summary>
        public void AddFilter(ISoundFilter filter)
        {
            if (filter != null && !filters.Contains(filter)) { filters.Add(filter); }
        }

        /// <summary>Removes a rule.</summary>
        public void RemoveFilter(ISoundFilter filter) => filters.Remove(filter);

        /// <summary>Plays a sound by asset (it must have an id for its request; tests and level pieces use this).</summary>
        public SoundHandle Play(SoundEvent sound, Vector3 position, float strength = 1f, EmitterId emitter = default,
                                SoundFlags flags = SoundFlags.None)
        {
            if (sound == null) { return SoundHandle.None; }
            SoundRequest request = SoundRequest.At(sound.Id, position, strength);
            request.emitter = emitter;
            request.flags = flags;
            return Play(request, sound);
        }

        /// <summary>
        /// Plays a request: its handle, or SoundHandle.None when it wasn't played (no such sound, no clips, a filter said
        /// no, too far away, its limits, or every voice busy with something that matters more).
        /// </summary>
        public SoundHandle Play(in SoundRequest request) => Play(request, null);

        /// <summary>
        /// Plays a request for a sound given by asset (its id is the asset's): for what holds its sounds as assets (a
        /// character's sound set), so they play whether or not the library lists them. The request is as Play's otherwise.
        /// </summary>
        public SoundHandle Play(SoundRequest request, SoundEvent sound)
        {
            if (sound != null) { request.sound = sound.Id; }
            if (pool == null) { return SoundHandle.None; }
            if (sound == null) { sound = library != null ? library.Find(request.sound) : null; }
            if (sound == null || !sound.HasClips) { return SoundHandle.None; }
            for (int i = 0; i < filters.Count; i++)
            {
                if (!filters[i].Allow(ref request)) { return SoundHandle.None; }
            }
            if (sound.Spatial == Spatial.ThreeD && TooFar(request.position, sound.MaxDistance)) { return SoundHandle.None; }

            float now = Time.unscaledTime;
            if (!limiter.Allows(sound, now)) { return SoundHandle.None; }
            if (!pool.TryAcquire(sound.Priority, out int index)) { return SoundHandle.None; }

            Voice voice = pool[index];
            if (voice.inUse) { Release(voice); }
            StartVoice(voice, sound, request, now);
            limiter.Started(sound, now);
            Played?.Invoke(request);
            return new SoundHandle(index, voice.generation);
        }

        private bool TooFar(Vector3 position, float maxDistance)
        {
            Transform from = FindListener();
            return from != null && (position - from.position).sqrMagnitude > maxDistance * maxDistance;
        }

        private Transform FindListener()
        {
            if (listener != null) { return listener; }
            if (Time.unscaledTime < nextListenerSearch) { return null; }
            nextListenerSearch = Time.unscaledTime + ListenerSearchInterval;
            AudioListener found = FindAnyObjectByType<AudioListener>();
            listener = found != null ? found.transform : null;
            return listener;
        }

        private void StartVoice(Voice voice, SoundEvent sound, in SoundRequest request, float now)
        {
            AudioClip clip = picker.Pick(sound);
            float pitch = picker.Range(sound.Pitch) * (request.pitchScale > 0f ? request.pitchScale : 1f);
            voice.inUse = true;
            voice.sound = sound;
            voice.emitter = request.emitter;
            voice.priority = sound.Priority;
            voice.startedAt = now;
            voice.endsAt = sound.Loop ? float.PositiveInfinity : now + clip.length / Mathf.Max(0.01f, Mathf.Abs(pitch));
            voice.chosenVolume = Mathf.Clamp01(picker.Range(sound.Volume));
            voice.strength = Mathf.Clamp01(request.strength);
            voice.follow = (request.flags & SoundFlags.Follow) != 0;
            voice.paused = false;
            voice.fading = false;

            AudioSource source = voice.source;
            source.clip = clip;
            source.loop = sound.Loop;
            source.pitch = pitch;
            source.spatialBlend = sound.Spatial == Spatial.ThreeD ? 1f : 0f;
            source.minDistance = sound.MinDistance;
            source.maxDistance = sound.MaxDistance;
            source.outputAudioMixerGroup = sound.Bus != null ? sound.Bus.MixerGroup : null;
            source.ignoreListenerPause = sound.Bus != null && !sound.Bus.PausesWithGame;
            source.transform.position = request.position;
            ApplyVolume(voice);
            source.Play();
        }

        private void ApplyVolume(Voice voice) =>
            voice.source.volume = voice.chosenVolume * voice.strength * BusVolume(voice.sound.Bus) * voice.FadeFactor;

        private void Release(Voice voice)
        {
            limiter.Ended(voice.sound);
            if (voice.source != null) { voice.source.Stop(); }
            voice.inUse = false;
            voice.sound = null;
            voice.fading = false;
            voice.paused = false;
        }

        /// <summary>Stops a sound, at once or fading out over so many seconds. A stale handle does nothing.</summary>
        public void Stop(SoundHandle handle, float fadeSeconds = 0f)
        {
            Voice voice = pool?.Find(handle);
            if (voice == null) { return; }
            if (fadeSeconds <= 0f) { Release(voice); return; }
            if (voice.fading) { return; }
            voice.fading = true;
            voice.fadeElapsed = 0f;
            voice.fadeLength = fadeSeconds;
        }

        /// <summary>Whether a handle's sound is still playing (or fading out, or paused).</summary>
        public bool IsPlaying(SoundHandle handle) => pool?.Find(handle) != null;

        /// <summary>Whether a handle's sound is paused with the game.</summary>
        public bool IsPaused(SoundHandle handle) => pool?.Find(handle)?.paused ?? false;

        /// <summary>Moves a sound (a loop following what makes it). A stale handle does nothing.</summary>
        public void SetPosition(SoundHandle handle, Vector3 position)
        {
            Voice voice = pool?.Find(handle);
            if (voice != null) { voice.source.transform.position = position; }
        }

        /// <summary>Makes a sound louder or quieter (0 to 1: a loop following a speed). A stale handle does nothing.</summary>
        public void SetStrength(SoundHandle handle, float strength)
        {
            Voice voice = pool?.Find(handle);
            if (voice == null) { return; }
            voice.strength = Mathf.Clamp01(strength);
            ApplyVolume(voice);
        }

        /// <summary>The AudioSource a handle's sound plays on (for tests), or null.</summary>
        internal AudioSource SourceOf(SoundHandle handle) => pool?.Find(handle)?.source;
    }
}
