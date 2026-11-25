using UnityEngine;

namespace MeridianMotion.Showcase.Audio
{
    /// <summary>
    /// Plays the music, beside the AudioService: one track at a time, crossfading from one to the next (Play). A track's
    /// intro plays once and its loop is scheduled to start on the sample the intro ends on. Music plays on while the game
    /// is paused (it ignores the listener's pause) and fades on unscaled time. Its volume is the track's times its bus's.
    ///
    /// It has two decks, each an intro source and a loop source: the track playing, and the one fading out. It keeps its
    /// own timing of when an intro ends (audio may not run in batch mode), so what it reports doesn't hang on the device.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioService))]
    [AddComponentMenu("Meridian Motion/Audio/Music Player")]
    public class MusicPlayer : MonoBehaviour
    {
        private sealed class Deck
        {
            public AudioSource intro, loop;
            public MusicTrack track;
            public float startedAt;
            public float introEndsAt;
            /// <summary>How loud it is in its fade, 0 to 1, and where it's going.</summary>
            public float level, target;
            public float fadeSeconds;

            public bool Active => track != null;

            public void Stop()
            {
                intro.Stop();
                loop.Stop();
                track = null;
                level = target = 0f;
            }
        }

        private Deck a, b, current;
        private AudioService service;

        /// <summary>The track playing (or fading in), or null.</summary>
        public MusicTrack Current => current?.track;

        /// <summary>Whether the current track is on its loop (past its intro, or it has none).</summary>
        public bool IsLooping => current != null && current.Active
                                 && (current.track.Intro == null || Time.unscaledTime >= current.introEndsAt);

        /// <summary>The clip the current track is on (its intro or its loop), or null.</summary>
        internal AudioClip PlayingClip => current == null || !current.Active ? null
            : IsLooping ? current.track.Loop : current.track.Intro;

        /// <summary>How many tracks are sounding (two during a crossfade).</summary>
        internal int PlayingTracks => (a.Active ? 1 : 0) + (b.Active ? 1 : 0);

        /// <summary>When (unscaled) the current track started.</summary>
        internal float CurrentStartedAt => current?.startedAt ?? 0f;

        /// <summary>Whether its sources play through the listener's pause.</summary>
        internal bool IgnoresPause => a.loop.ignoreListenerPause && a.intro.ignoreListenerPause;

        private void Awake()
        {
            service = GetComponent<AudioService>();
            a = MakeDeck("Music A");
            b = MakeDeck("Music B");
        }

        private Deck MakeDeck(string deckName)
        {
            var obj = new GameObject(deckName);
            obj.transform.SetParent(transform, false);
            return new Deck { intro = MakeSource(obj), loop = MakeSource(obj) };
        }

        private static AudioSource MakeSource(GameObject obj)
        {
            AudioSource source = obj.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.ignoreListenerPause = true;
            return source;
        }

        /// <summary>
        /// Plays a track, crossfading from what's playing over so many seconds (0: cuts to it). The track already playing:
        /// nothing changes. Null: the music fades out.
        /// </summary>
        public void Play(MusicTrack track, float crossfadeSeconds = 1.5f)
        {
            if (track == null) { Stop(crossfadeSeconds); return; }
            if (current != null && current.track == track) { return; }

            Deck next = current == a ? b : a;
            if (next.Active) { next.Stop(); }
            if (current != null && current.Active) { FadeOut(current, crossfadeSeconds); }

            next.track = track;
            next.startedAt = Time.unscaledTime;
            next.fadeSeconds = crossfadeSeconds;
            next.level = crossfadeSeconds > 0f ? 0f : 1f;
            next.target = 1f;
            StartClips(next);
            Apply(next);
            current = next;
        }

        /// <summary>Fades the music out over so many seconds (0: at once).</summary>
        public void Stop(float fadeSeconds = 1f)
        {
            if (current != null && current.Active) { FadeOut(current, fadeSeconds); }
            current = null;
        }

        private static void FadeOut(Deck deck, float seconds)
        {
            if (seconds <= 0f) { deck.Stop(); return; }
            deck.target = 0f;
            deck.fadeSeconds = seconds;
        }

        private static void StartClips(Deck deck)
        {
            MusicTrack track = deck.track;
            deck.loop.clip = track.Loop;
            deck.loop.loop = true;
            if (track.Intro != null)
            {
                deck.intro.clip = track.Intro;
                deck.intro.loop = false;
                deck.intro.Play();
                // Scheduled on the audio clock, to the sample, so the loop follows the intro without a gap.
                double introLength = (double)track.Intro.samples / track.Intro.frequency;
                if (track.Loop != null) { deck.loop.PlayScheduled(AudioSettings.dspTime + introLength); }
                deck.introEndsAt = deck.startedAt + (float)introLength;
            }
            else
            {
                deck.introEndsAt = deck.startedAt;
                if (track.Loop != null) { deck.loop.Play(); }
            }
        }

        private void Update()
        {
            float delta = Time.unscaledDeltaTime;
            Tick(a, delta);
            Tick(b, delta);
        }

        private void Tick(Deck deck, float delta)
        {
            if (!deck.Active) { return; }
            if (deck.level != deck.target)
            {
                float step = deck.fadeSeconds > 0f ? delta / deck.fadeSeconds : 1f;
                deck.level = Mathf.MoveTowards(deck.level, deck.target, step);
            }
            if (deck.level <= 0f && deck.target <= 0f) { deck.Stop(); return; }
            Apply(deck);
        }

        private void Apply(Deck deck)
        {
            float volume = deck.level * deck.track.Volume * (service != null ? service.GetVolume(deck.track.Bus) : 1f);
            deck.intro.volume = volume;
            deck.loop.volume = volume;
        }
    }
}
