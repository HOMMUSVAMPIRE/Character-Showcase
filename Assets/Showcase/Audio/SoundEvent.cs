using UnityEngine;

namespace MeridianMotion.Showcase.Audio
{
    /// <summary>How a Sound Event chooses among its clips.</summary>
    public enum ClipOrder
    {
        /// <summary>At random, never the one it played last.</summary>
        RandomNoRepeat,
        /// <summary>At random.</summary>
        Random,
        /// <summary>One after another, round again.</summary>
        InOrder,
    }

    /// <summary>Whether a sound is heard from where it is (3D) or the same everywhere (2D: music, the UI).</summary>
    public enum Spatial
    {
        TwoD,
        ThreeD,
    }

    /// <summary>
    /// One kind of sound (a footstep on concrete, a landing, a button's click): its clips, how loud and how high, what bus,
    /// 2D or 3D, whether it loops, and how often and how many of it may play. Everything a designer tunes about a sound is
    /// here. A Sound Event with no clips plays nothing, silently: the slots the game has can be filled later.
    /// Its id (given by the Sound Library) is how requests name it.
    /// </summary>
    [CreateAssetMenu(menuName = "Meridian Motion/Audio/Sound Event", fileName = "Sound Event")]
    public class SoundEvent : ScriptableObject
    {
        [Header("Clips")]
        [Tooltip("The clips it plays, one each time (variations of the same sound). Empty: it plays nothing")]
        [SerializeField] private AudioClip[] clips = new AudioClip[0];
        [Tooltip("How it chooses among them")]
        [SerializeField] private ClipOrder order = ClipOrder.RandomNoRepeat;

        [Header("Playing")]
        [Tooltip("Its volume (0 to 1), chosen in this range each time it plays (then scaled by its strength and its bus)")]
        [SerializeField] private Vector2 volume = new(0.9f, 1f);
        [Tooltip("Its pitch, chosen in this range each time it plays (1: as recorded)")]
        [SerializeField] private Vector2 pitch = new(0.95f, 1.05f);
        [Tooltip("The bus it's on (its volume, whether it pauses with the game)")]
        [SerializeField] private AudioBus bus;
        [Tooltip("Whether it loops until it's stopped (a slide, a zipline), rather than playing once")]
        [SerializeField] private bool loop;

        [Header("Space")]
        [Tooltip("Heard from where it is (3D), or the same everywhere (2D: music, the UI)")]
        [SerializeField] private Spatial spatial = Spatial.ThreeD;
        [Tooltip("How near (m) it's heard at full volume, in 3D")]
        [Min(0.01f)] [SerializeField] private float minDistance = 1f;
        [Tooltip("How far (m) it's heard, in 3D: farther than this from the listener, it isn't started at all")]
        [Min(0.01f)] [SerializeField] private float maxDistance = 30f;

        [Header("Limits")]
        [Tooltip("The most of it playing at once: another isn't started")]
        [Min(1)] [SerializeField] private int maxInstances = 4;
        [Tooltip("The shortest time (s) between two of it starting: another sooner isn't started")]
        [Min(0f)] [SerializeField] private float cooldown = 0.05f;
        [Tooltip("How much it matters (0 to 255) when every voice is busy: it takes the voice of a sound of lower " +
                 "priority (the oldest), or isn't played")]
        [Range(0, 255)] [SerializeField] private int priority = 128;

        [Header("Identity")]
        [Tooltip("Its id, how requests name it: given by the Sound Library, never changed")]
        [SerializeField] private int id;

        /// <summary>Its id, how requests name it.</summary>
        public SoundId Id => new(id);
        /// <summary>Whether it has any clip to play.</summary>
        public bool HasClips => clips != null && clips.Length > 0;

        /// <summary>The clips it plays.</summary>
        public AudioClip[] Clips { get => clips; set => clips = value ?? new AudioClip[0]; }
        /// <summary>How it chooses among them.</summary>
        public ClipOrder Order { get => order; set => order = value; }
        /// <summary>Its volume range.</summary>
        public Vector2 Volume { get => volume; set => volume = value; }
        /// <summary>Its pitch range.</summary>
        public Vector2 Pitch { get => pitch; set => pitch = value; }
        /// <summary>Its bus.</summary>
        public AudioBus Bus { get => bus; set => bus = value; }
        /// <summary>Whether it loops.</summary>
        public bool Loop { get => loop; set => loop = value; }
        /// <summary>2D or 3D.</summary>
        public Spatial Spatial { get => spatial; set => spatial = value; }
        /// <summary>How near it's heard at full volume, in 3D.</summary>
        public float MinDistance { get => minDistance; set => minDistance = Mathf.Max(0.01f, value); }
        /// <summary>How far it's heard, in 3D.</summary>
        public float MaxDistance { get => maxDistance; set => maxDistance = Mathf.Max(0.01f, value); }
        /// <summary>The most of it playing at once.</summary>
        public int MaxInstances { get => maxInstances; set => maxInstances = Mathf.Max(1, value); }
        /// <summary>The shortest time between two of it starting.</summary>
        public float Cooldown { get => cooldown; set => cooldown = Mathf.Max(0f, value); }
        /// <summary>How much it matters when every voice is busy.</summary>
        public int Priority { get => priority; set => priority = Mathf.Clamp(value, 0, 255); }

        /// <summary>Gives it its id (the Sound Library's editor does, and tests).</summary>
        public void AssignId(SoundId soundId) => id = soundId.Value;
    }
}
