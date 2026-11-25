using System;
using MeridianMotion.Showcase.Audio;
using UnityEngine;

namespace MeridianMotion.Showcase.CharacterAudio
{
    /// <summary>What an event row's strength (how loud) comes from, and what its Min Speed is measured by.</summary>
    public enum StrengthFrom
    {
        /// <summary>Always full strength (its Min Speed is never met above 0).</summary>
        Full,
        /// <summary>How fast the character was falling (a landing).</summary>
        FallSpeed,
        /// <summary>How fast the character was going along the ground (a slide, a vault).</summary>
        Speed,
        /// <summary>How fast the character was going into what it hit (a wall).</summary>
        SpeedIntoWall,
    }

    /// <summary>A sound for a surface: what a row plays on it instead of its own sound.</summary>
    [Serializable]
    public struct SurfaceSound
    {
        [Tooltip("The surface")]
        public SoundSurface surface;
        [Tooltip("What's played on it")]
        public SoundEvent sound;
    }

    /// <summary>
    /// One of a Sound Set's events: a movement event (by its flag's name) and the sound it plays, or the sound for the
    /// surface under the character. Several rows may name the same event: the one whose Min Speed is the highest the
    /// character's speed meets plays (a landing and a hard landing).
    /// </summary>
    [Serializable]
    public class EventSound
    {
        [Tooltip("The movement event (its flag's name: Landed, Jumped, ...)")]
        [SoundFlagName(SoundFlagKind.Event)] public string eventName;
        [Tooltip("What it plays")]
        public SoundEvent sound;
        [Tooltip("What it plays on these surfaces instead")]
        public SurfaceSound[] bySurface = new SurfaceSound[0];
        [Tooltip("What its strength comes from (and its Min Speed is measured by)")]
        public StrengthFrom strengthFrom = StrengthFrom.Full;
        [Tooltip("The speed (m/s, at the character's authored size) it plays at full strength")]
        [Min(0.01f)] public float fullStrengthSpeed = 10f;
        [Tooltip("The least speed (m/s, at the authored size) it plays at: of the rows for one event, the one with the " +
                 "highest Min Speed the character meets plays")]
        [Min(0f)] public float minSpeed;
    }

    /// <summary>A sound looped while the character is in a state (by its flag's name), following it.</summary>
    [Serializable]
    public class StateLoop
    {
        [Tooltip("The state (its flag's name: Sliding, Ziplining, ...)")]
        [SoundFlagName(SoundFlagKind.State)] public string stateName;
        [Tooltip("What's looped (a Sound Event that loops)")]
        public SoundEvent loop;
        [Tooltip("Whether it plays only while the character's moving in the state (climbing, not hanging still)")]
        public bool onlyWhileMoving;
        [Tooltip("The speed (m/s, at the authored size) it plays at full strength; slower, quieter")]
        [Min(0.01f)] public float fullStrengthSpeed = 6f;
        [Tooltip("How long (s) it fades out as the state ends")]
        [Min(0f)] public float fadeOut = 0.25f;
    }

    /// <summary>
    /// The character's footsteps, by the surface: a step as each of its model's feet comes down (as its foot IK says:
    /// lifted, then planted), or, without a model's feet to follow, at a cadence from Walk Cadence at the movement's Walk
    /// Speed to Run Cadence at its Run Speed. Louder from walking to running.
    /// </summary>
    [Serializable]
    public class FootstepSettings
    {
        [Tooltip("A footstep")]
        public SoundEvent step;
        [Tooltip("A footstep while crouching. Empty: the footstep")]
        public SoundEvent crouchedStep;
        [Tooltip("The footstep on these surfaces instead")]
        public SurfaceSound[] bySurface = new SurfaceSound[0];
        [Tooltip("The crouched footstep on these surfaces instead")]
        public SurfaceSound[] crouchedBySurface = new SurfaceSound[0];
        [Tooltip("The least speed (m/s, at the authored size) that makes footsteps")]
        [Min(0f)] public float minSpeed = 0.3f;
        [Tooltip("Without a model's feet to follow (none loaded): footsteps a second at the movement's Walk Speed")]
        [Min(0.1f)] public float walkCadence = 2.2f;
        [Tooltip("Without a model's feet to follow: footsteps a second at the movement's Run Speed")]
        [Min(0.1f)] public float runCadence = 3f;
    }

    /// <summary>
    /// What a character sounds like: which movement events play which sound (by the surface under it), which states loop
    /// a sound, its footsteps, its splashes and the wind as it falls. Rows name their event or state by its flag's name,
    /// so a project's own flags work as the package's do. A new sound for a new ability is a row here. The mannequin and
    /// the fox each have one (Create Audio Assets makes them).
    /// </summary>
    [CreateAssetMenu(menuName = "Meridian Motion/Audio/Character Sound Set", fileName = "Character Sound Set")]
    public class CharacterSoundSet : ScriptableObject
    {
        [Header("Events")]
        [Tooltip("The movement events that play a sound")]
        [SerializeField] private EventSound[] events = new EventSound[0];

        [Header("Loops")]
        [Tooltip("The states that loop a sound while the character's in them")]
        [SerializeField] private StateLoop[] loops = new StateLoop[0];

        [Header("Footsteps")]
        [Tooltip("Its footsteps")]
        [SerializeField] private FootstepSettings footsteps = new();

        [Header("Surfaces")]
        [Tooltip("What surface the ground under it is")]
        [SerializeField] private SurfaceMap surfaces;

        [Header("Water")]
        [Tooltip("Played for each splash (going into or out of water), as strong as the splash")]
        [SerializeField] private SoundEvent splash;

        [Header("Wind")]
        [Tooltip("Looped while it falls fast, louder the faster")]
        [SerializeField] private SoundEvent wind;
        [Tooltip("How fast (m/s, at the authored size) it must fall for the wind")]
        [Min(0f)] [SerializeField] private float windSpeed = 12f;

        /// <summary>Its event rows.</summary>
        public EventSound[] Events { get => events; set => events = value ?? new EventSound[0]; }
        /// <summary>Its loop rows.</summary>
        public StateLoop[] Loops { get => loops; set => loops = value ?? new StateLoop[0]; }
        /// <summary>Its footsteps.</summary>
        public FootstepSettings Footsteps { get => footsteps; set => footsteps = value ?? new FootstepSettings(); }
        /// <summary>Its surface map.</summary>
        public SurfaceMap Surfaces { get => surfaces; set => surfaces = value; }
        /// <summary>Its splash.</summary>
        public SoundEvent Splash { get => splash; set => splash = value; }
        /// <summary>Its wind.</summary>
        public SoundEvent Wind { get => wind; set => wind = value; }
        /// <summary>How fast it must fall for the wind.</summary>
        public float WindSpeed { get => windSpeed; set => windSpeed = Mathf.Max(0f, value); }

        /// <summary>A row's sound for a surface: its surface's, else its own.</summary>
        public static SoundEvent For(SoundEvent sound, SurfaceSound[] bySurface, SoundSurface surface)
        {
            if (surface != null && bySurface != null)
            {
                for (int i = 0; i < bySurface.Length; i++)
                {
                    if (bySurface[i].surface == surface && bySurface[i].sound != null) { return bySurface[i].sound; }
                }
            }
            return sound;
        }
    }
}
