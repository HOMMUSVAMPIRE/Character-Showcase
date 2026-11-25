using UnityEngine;

namespace MeridianMotion.Showcase.Audio
{
    /// <summary>
    /// A piece of music: an optional intro, played once, then a loop, played until it's stopped or another track takes
    /// over (MusicPlayer). The loop starts on the sample the intro ends on, so there's no gap.
    /// </summary>
    [CreateAssetMenu(menuName = "Meridian Motion/Audio/Music Track", fileName = "Music Track")]
    public class MusicTrack : ScriptableObject
    {
        [Header("Clips")]
        [Tooltip("Played once before the loop (optional). Empty: the loop starts at once")]
        [SerializeField] private AudioClip intro;
        [Tooltip("Played round and round after the intro. Empty: the track is silent")]
        [SerializeField] private AudioClip loop;

        [Header("Playing")]
        [Tooltip("Its volume (0 to 1), times its bus's")]
        [Range(0f, 1f)] [SerializeField] private float volume = 0.8f;
        [Tooltip("The bus it's on (the Music bus: its volume; music isn't paused with the game)")]
        [SerializeField] private AudioBus bus;

        /// <summary>Played once before the loop, or null.</summary>
        public AudioClip Intro { get => intro; set => intro = value; }
        /// <summary>Played round and round.</summary>
        public AudioClip Loop { get => loop; set => loop = value; }
        /// <summary>Its volume.</summary>
        public float Volume { get => volume; set => volume = Mathf.Clamp01(value); }
        /// <summary>Its bus.</summary>
        public AudioBus Bus { get => bus; set => bus = value; }
    }
}
