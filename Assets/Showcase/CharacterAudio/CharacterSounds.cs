using System.Collections.Generic;
using MeridianMotion.Characters;
using MeridianMotion.Showcase.Audio;
using UnityEngine;

namespace MeridianMotion.Showcase.CharacterAudio
{
    /// <summary>
    /// A character's sounds, from what its movement reports: each movement event its Sound Set has a row for plays that
    /// row's sound (the one for the surface under it), as strong as the row says; its states loop their sounds while they
    /// last; it steps every stride on the ground; its splashes and the wind as it falls play too. It only asks the scene's
    /// AudioService to play (requests with its own emitter id, higher pitched the smaller it is), and never plays or
    /// changes anything itself. It reads the motor, not the animator, so it works without a model or animation.
    /// Without an audio service in the scene, it does nothing.
    ///
    /// The package's Character prefab can't hold it (it's the showcase's): a scene's CharacterAudioDirector adds it to each
    /// character the spawner spawns.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Meridian Motion/Audio/Character Sounds")]
    public partial class CharacterSounds : MonoBehaviour
    {
        [Tooltip("What the character sounds like")]
        [SerializeField] private CharacterSoundSet soundSet;

        // An event row's least strength: the softest landing is still heard.
        private const float MinStrength = 0.25f;
        // How small (relative to its authored size) a character's pitch is scaled for, at most and at least.
        private const float MinPitchScale = 0.5f, MaxPitchScale = 2f;

        private MMotionMotor motor;
        private WaterEffects water;
        private EmitterId emitter;
        private SurfaceFinder surfaces;
        private MovementEvents[] eventFlags = new MovementEvents[0];
        private bool[] eventParsed = new bool[0];

        // The names each set has been warned about, so a bad row is said once.
        private static readonly Dictionary<CharacterSoundSet, HashSet<string>> warned = new();

        /// <summary>What the character sounds like. Setting it reads its rows again (and stops its loops).</summary>
        public CharacterSoundSet SoundSet
        {
            get => soundSet;
            set
            {
                StopLoops();
                soundSet = value;
                ReadRows();
            }
        }

        /// <summary>
        /// Whether this is the player's character (the director says, as it attaches it). Only the player's plays the wind
        /// as it falls: the wind is heard the same wherever it is, so an NPC's would sound as if the player were falling.
        /// </summary>
        public bool IsPlayer { get; set; }

        /// <summary>The id its sounds carry.</summary>
        public EmitterId Emitter => emitter;

        private void Awake()
        {
            motor = GetComponent<MMotionMotor>();
            water = GetComponent<WaterEffects>();
            emitter = EmitterId.Next();
            ReadRows();
        }

        private void OnEnable()
        {
            if (motor != null) { motor.MovementEventRaised += HandleMovementEvent; }
            if (water != null) { water.Splashed += HandleSplash; }
            HookFootsteps(true);
        }

        private void OnDisable()
        {
            if (motor != null) { motor.MovementEventRaised -= HandleMovementEvent; }
            if (water != null) { water.Splashed -= HandleSplash; }
            HookFootsteps(false);
            StopLoops();
        }

        private void ReadRows()
        {
            surfaces = soundSet != null ? new SurfaceFinder(soundSet.Surfaces) : null;
            EventSound[] rows = soundSet != null ? soundSet.Events : new EventSound[0];
            eventFlags = new MovementEvents[rows.Length];
            eventParsed = new bool[rows.Length];
            for (int i = 0; i < rows.Length; i++)
            {
                if (rows[i] == null) { continue; }
                eventParsed[i] = MovementEvents.TryParse(rows[i].eventName, out eventFlags[i]);
                if (!eventParsed[i]) { Warn(rows[i].eventName, "movement event"); }
            }
            ReadLoopRows();
        }

        private void Warn(string flagName, string kind)
        {
            if (!warned.TryGetValue(soundSet, out HashSet<string> names)) { warned[soundSet] = names = new HashSet<string>(); }
            if (!names.Add(flagName ?? "")) { return; }
            Debug.LogWarning($"[{nameof(CharacterSounds)}] The sound set '{soundSet.name}' has a row for '{flagName}', which " +
                             $"isn't a {kind}: it's left out.", soundSet);
        }

        private float PitchScale => Mathf.Clamp(1f / Mathf.Sqrt(Mathf.Max(0.01f, motor.HeightScale)), MinPitchScale, MaxPitchScale);

        private SoundHandle Play(SoundEvent sound, Vector3 position, float strength, SoundFlags flags = SoundFlags.None)
        {
            AudioService service = AudioService.Instance;
            if (service == null || sound == null) { return SoundHandle.None; }
            SoundRequest request = SoundRequest.At(sound.Id, position, strength);
            request.pitchScale = PitchScale;
            request.emitter = emitter;
            request.flags = flags;
            return service.Play(request, sound);
        }

        private void HandleMovementEvent(MovementEvents raised, MovementStep step)
        {
            if (soundSet == null || AudioService.Instance == null) { return; }
            EventSound[] rows = soundSet.Events;
            int best = -1;
            float bestSpeed = 0f;
            for (int i = 0; i < rows.Length && i < eventFlags.Length; i++)
            {
                if (!eventParsed[i] || eventFlags[i] != raised) { continue; }
                float speed = SpeedFor(rows[i].strengthFrom, step);
                if (speed < rows[i].minSpeed * motor.SpeedScale) { continue; }
                if (best < 0 || rows[i].minSpeed > rows[best].minSpeed) { best = i; bestSpeed = speed; }
            }
            if (best < 0) { return; }

            EventSound row = rows[best];
            Vector3 feet = motor.Feet;
            SoundSurface surface = surfaces?.Find(step.initialGroundTransform != null ? step.initialGroundTransform : motor.groundTransform, feet);
            float strength = row.strengthFrom == StrengthFrom.Full ? 1f
                : Mathf.Lerp(MinStrength, 1f, bestSpeed / (row.fullStrengthSpeed * motor.SpeedScale));
            Play(CharacterSoundSet.For(row.sound, row.bySurface, surface), feet, strength);
        }

        // The speed a row's strength and Min Speed are measured by, as the character was going just before the step that
        // raised the event: the step before's, the faster of how it began and ended. (The raising step's own initial
        // velocity isn't it: a landing step's is the motor's snap down onto the ground, far faster than the fall. And the
        // step before's end alone isn't either: the step that meets the ground ends with its fall already cut short by
        // the collision, the landing seen only on the next.) Events are raised before the motor keeps the step as its
        // last, so its last step is still the one before.
        private float SpeedFor(StrengthFrom from, MovementStep step)
        {
            MovementStep before = motor.lastStep != null && motor.lastStep != step ? motor.lastStep : step;
            return Mathf.Max(SpeedOf(from, before.initialVelocity), SpeedOf(from, before.finalVelocity));
        }

        private static float SpeedOf(StrengthFrom from, Vector3 velocity) => from switch
        {
            StrengthFrom.FallSpeed => Mathf.Max(0f, -velocity.y),
            StrengthFrom.Speed or StrengthFrom.SpeedIntoWall => new Vector2(velocity.x, velocity.z).magnitude,
            _ => 0f,
        };

        private void HandleSplash(WaterEffectContext splash)
        {
            if (soundSet != null) { Play(soundSet.Splash, splash.position, splash.strength); }
        }
    }
}
