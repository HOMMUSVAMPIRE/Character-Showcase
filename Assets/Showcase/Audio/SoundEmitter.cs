using UnityEngine;

namespace MeridianMotion.Showcase.Audio
{
    /// <summary>
    /// A sound on a scene object (a fountain, a machine): played where it is when it's enabled, or when Play is called (by
    /// a UnityEvent, say). A looping sound follows the object and stops when it's disabled. How a level piece that isn't
    /// the character makes sound, with no code. Without an audio service in the scene, it does nothing.
    /// </summary>
    [AddComponentMenu("Meridian Motion/Audio/Sound Emitter")]
    public class SoundEmitter : MonoBehaviour
    {
        [Tooltip("The sound it plays")]
        [SerializeField] private SoundEvent sound;
        [Tooltip("Whether it plays as soon as it's enabled (else only when Play is called)")]
        [SerializeField] private bool playOnEnable = true;
        [Tooltip("How strong (0 to 1): scales its volume")]
        [Range(0f, 1f)] [SerializeField] private float strength = 1f;
        [Tooltip("How long (s) a looping sound fades out as it's stopped or the object's disabled. 0: at once")]
        [Min(0f)] [SerializeField] private float fadeOut = 0.2f;

        private SoundHandle handle;
        private EmitterId emitter;

        /// <summary>The sound it plays.</summary>
        public SoundEvent Sound { get => sound; set => sound = value; }
        /// <summary>Whether it plays as it's enabled.</summary>
        public bool PlayOnEnable { get => playOnEnable; set => playOnEnable = value; }
        /// <summary>How strong it is.</summary>
        public float Strength { get => strength; set => strength = Mathf.Clamp01(value); }
        /// <summary>Its sound's handle (for a loop: what it stops), or none.</summary>
        public SoundHandle Handle => handle;

        private void Awake() => emitter = EmitterId.Next();

        // Waiting to play on enable: the scene's service wasn't there yet (a scene's objects wake in no set order).
        private bool waiting;

        private void OnEnable()
        {
            if (!playOnEnable) { return; }
            waiting = AudioService.Instance == null;
            if (!waiting) { Play(); }
        }

        private void OnDisable()
        {
            waiting = false;
            Stop(fadeOut);
        }

        /// <summary>Plays its sound where it is.</summary>
        public void Play()
        {
            AudioService service = AudioService.Instance;
            if (service == null || sound == null) { return; }
            if (sound.Loop) { service.Stop(handle); }
            SoundHandle played = service.Play(sound, transform.position, strength, emitter,
                                              sound.Loop ? SoundFlags.Follow : SoundFlags.None);
            if (sound.Loop) { handle = played; }
        }

        /// <summary>Stops its looping sound, fading out over so many seconds.</summary>
        public void Stop(float fadeSeconds)
        {
            AudioService service = AudioService.Instance;
            if (service != null) { service.Stop(handle, fadeSeconds); }
            handle = SoundHandle.None;
        }

        private void LateUpdate()
        {
            if (waiting && AudioService.Instance != null)
            {
                waiting = false;
                Play();
            }
            if (!handle.IsValid) { return; }
            AudioService service = AudioService.Instance;
            if (service != null) { service.SetPosition(handle, transform.position); }
        }
    }
}
