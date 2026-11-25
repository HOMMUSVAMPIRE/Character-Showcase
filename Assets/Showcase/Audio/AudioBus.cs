using UnityEngine;
using UnityEngine.Audio;

namespace MeridianMotion.Showcase.Audio
{
    /// <summary>
    /// A group of sounds with one volume: Music, Effects, UI. The AudioService multiplies a bus's volume into its voices'
    /// (no Audio Mixer is needed) and saves it (AudioService.SetVolume). A bus paused with the game stops while the game
    /// is paused (Time.timeScale 0) and goes on from where it was; one that isn't (music, the UI) plays on.
    /// </summary>
    [CreateAssetMenu(menuName = "Meridian Motion/Audio/Audio Bus", fileName = "Audio Bus")]
    public class AudioBus : ScriptableObject
    {
        [Header("Bus")]
        [Tooltip("The bus's name, which its saved volume is kept under. Keep it once players have saved volumes")]
        [SerializeField] private string busName = "Effects";
        [Tooltip("Its volume (0 to 1) until the player sets one")]
        [Range(0f, 1f)] [SerializeField] private float defaultVolume = 1f;
        [Tooltip("Whether its sounds pause while the game is paused (Time.timeScale 0) and go on after. Off for music and " +
                 "the UI, which play on in the pause menu")]
        [SerializeField] private bool pausesWithGame = true;
        [Tooltip("An Audio Mixer group its sounds go through, for effects on the mix (reverb, ducking). Empty: none, " +
                 "straight out")]
        [SerializeField] private AudioMixerGroup mixerGroup;

        /// <summary>The bus's name, which its saved volume is kept under.</summary>
        public string BusName { get => busName; set => busName = value; }
        /// <summary>Its volume until the player sets one.</summary>
        public float DefaultVolume { get => defaultVolume; set => defaultVolume = Mathf.Clamp01(value); }
        /// <summary>Whether its sounds pause while the game is paused.</summary>
        public bool PausesWithGame { get => pausesWithGame; set => pausesWithGame = value; }
        /// <summary>The Audio Mixer group its sounds go through, or null.</summary>
        public AudioMixerGroup MixerGroup { get => mixerGroup; set => mixerGroup = value; }
    }
}
