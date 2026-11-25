using UnityEngine;

namespace MeridianMotion.Showcase.Audio
{
    /// <summary>
    /// A scene's music: as the scene starts, asks the scene's MusicPlayer (beside its AudioService) for its track. Without
    /// an audio service in the scene, it does nothing.
    /// </summary>
    [AddComponentMenu("Meridian Motion/Audio/Scene Music")]
    public class SceneMusic : MonoBehaviour
    {
        [Tooltip("The track this scene plays")]
        [SerializeField] private MusicTrack track;
        [Tooltip("How long (s) it crossfades from what was playing")]
        [Min(0f)] [SerializeField] private float crossfade = 1.5f;

        /// <summary>The track this scene plays.</summary>
        public MusicTrack Track { get => track; set => track = value; }
        /// <summary>How long it crossfades.</summary>
        public float Crossfade { get => crossfade; set => crossfade = Mathf.Max(0f, value); }

        private void Start()
        {
            AudioService service = AudioService.Instance;
            if (service == null || track == null) { return; }
            if (service.TryGetComponent(out MusicPlayer player)) { player.Play(track, crossfade); }
        }
    }
}
