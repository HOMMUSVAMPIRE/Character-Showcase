using System;
using System.Collections.Generic;
using Level;
using UnityEngine;

namespace Character
{
    /// <summary>
    /// The character's splashes and ripples on water (see Level.Water). A splash where it goes in or comes out (falling
    /// or diving in, jumping, climbing or vaulting out, surfacing, landing or running into shallow water), as strong as
    /// it was fast; ripples on the surface round it as it swims or wades, every so far it goes, and every so often while
    /// it's still. Movement only reports what happens (its events and state); this plays the effects.
    ///
    /// The effects are prefabs, played on the water's surface: one with a component implementing IWaterEffect plays
    /// that; otherwise its particle systems play as they are, scaled with the character's size and the effect's strength
    /// (give them Hierarchy scaling). The defaults (Tools > Character Showcase > Create Water Effects) are simple
    /// particle systems; replace them with anything. Each is pooled: no more than so many of one play at once, the oldest
    /// reused after that. Splashed and Rippled are raised for each (with or without a prefab), for sound and the like.
    /// </summary>
    [DisallowMultipleComponent]
    public class WaterEffects : MonoBehaviour
    {
        [Header("Effects")]
        [Tooltip("Played on the surface where the character goes into the water or comes out of it. A prefab whose particle " +
                 "systems play as they are, or with a component implementing IWaterEffect. Empty: no splashes")]
        [SerializeField] private GameObject splashPrefab;
        [Tooltip("Played on the surface round the character as it swims or wades. A prefab whose particle systems play as " +
                 "they are, or with a component implementing IWaterEffect. Empty: no ripples")]
        [SerializeField] private GameObject ripplePrefab;

        [Header("Splashes")]
        [Tooltip("How fast (m/s, at the character's authored size) going into or out of the water makes a splash at its " +
                 "full strength (straight down or up; running into shallow water, along)")]
        [Min(0.1f)] [SerializeField] private float splashFullSpeed = 10f;
        [Tooltip("The weakest splash played, as a share of full strength: slower than that, there's none")]
        [Range(0f, 1f)] [SerializeField] private float splashMinStrength = 0.15f;
        [Tooltip("Whether a splash also sets off a ripple where it happens")]
        [SerializeField] private bool splashRipples = true;

        [Header("Ripples")]
        [Tooltip("How far (m, at the character's authored size) the character goes through the water between ripples as it " +
                 "swims or wades. 0: none as it moves")]
        [Min(0f)] [SerializeField] private float rippleSpacing = 1.2f;
        [Tooltip("Seconds between ripples while the character is still in the water (treading water, or standing in it). " +
                 "0: none while it's still")]
        [Min(0f)] [SerializeField] private float idleRippleInterval = 1.5f;
        [Tooltip("The slowest (m/s, at the character's authored size) the character counts as moving through the water, " +
                 "rather than still")]
        [Min(0f)] [SerializeField] private float rippleMovingSpeed = 0.3f;

        [Header("Playing")]
        [Tooltip("How big a particle effect plays at the weakest (as a share of its size at full strength): it grows with " +
                 "the strength up to its full size")]
        [Range(0f, 1f)] [SerializeField] private float weakestScale = 0.5f;
        [Tooltip("The most of each effect playing at once; another reuses the oldest")]
        [Min(1)] [SerializeField] private int maxPlaying = 8;

        // How far (m, at the authored size) above the water's surface effects are put, so they draw over it.
        private const float SurfaceLift = 0.02f;
        // A ripple while the character's still is this strong; moving, full strength.
        private const float IdleRippleStrength = 0.5f;

        /// <summary>Raised for each splash, where it happens and how strong (whether or not there's a splash prefab).</summary>
        public event Action<WaterEffectContext> Splashed;
        /// <summary>Raised for each ripple, where it happens (whether or not there's a ripple prefab).</summary>
        public event Action<WaterEffectContext> Rippled;

        /// <summary>The splash's prefab (null for none). Changing it lets the old one's effects finish and drops them.</summary>
        public GameObject SplashPrefab { get => splashPrefab; set => splashPrefab = value; }
        /// <summary>The ripple's prefab (null for none).</summary>
        public GameObject RipplePrefab { get => ripplePrefab; set => ripplePrefab = value; }
        /// <summary>How far (m, at the authored size) the character goes through the water between ripples.</summary>
        public float RippleSpacing { get => rippleSpacing; set => rippleSpacing = Mathf.Max(0f, value); }
        /// <summary>Seconds between ripples while the character is still in the water.</summary>
        public float IdleRippleInterval { get => idleRippleInterval; set => idleRippleInterval = Mathf.Max(0f, value); }
        /// <summary>How fast (m/s, at the authored size) going into or out of the water makes a full-strength splash.</summary>
        public float SplashFullSpeed => splashFullSpeed;
        /// <summary>The weakest splash played, as a share of full strength.</summary>
        public float SplashMinStrength => splashMinStrength;
        /// <summary>The most of each effect playing at once; another reuses the oldest.</summary>
        public int MaxPlaying { get => maxPlaying; set => maxPlaying = Mathf.Max(1, value); }

        private MovementController movement;
        // Effects played, by prefab, to be reused; and where the next one to reuse is, once they're all playing.
        private readonly Dictionary<GameObject, List<PlayedEffect>> played = new();
        private readonly Dictionary<GameObject, int> oldest = new();
        private Transform effectsRoot;
        // Ripples: where the character was last frame, how far it's gone since the last, and how long it's been still.
        private Vector3 lastPosition;
        private bool hasLastPosition;
        private float rippleDistance, stillTime;
        // The physics step (its StepTime) of the last splash, and how fast that was: several events in one step (landing
        // in shallow water starts wading too) make one splash, the strongest.
        private float splashStep = float.NegativeInfinity, splashStepSpeed;

        /// <summary>An effect played from a prefab: its object, and what plays it.</summary>
        private sealed class PlayedEffect
        {
            public GameObject gameObject;
            public IWaterEffect custom;
            public ParticleSystem[] systems;

            public bool IsPlaying
            {
                get
                {
                    if (gameObject == null) { return false; }
                    if (custom != null) { return custom.IsPlaying; }
                    foreach (ParticleSystem system in systems) { if (system != null && system.IsAlive(false)) { return true; } }
                    return false;
                }
            }
        }

        private void Awake() => movement = GetComponent<MovementController>();

        private void OnEnable()
        {
            if (movement != null) { movement.MovementEventRaised += HandleMovementEvent; }
            hasLastPosition = false;
        }

        private void OnDisable()
        {
            if (movement != null) { movement.MovementEventRaised -= HandleMovementEvent; }
        }

        private void OnDestroy()
        {
            if (effectsRoot != null) { Destroy(effectsRoot.gameObject); }
        }

        private float Size => movement != null ? movement.HeightScale : 1f;

        /// <summary>
        /// Splashes for the movement events that take the character into or out of the water: starting or ending a swim
        /// or a dive (by its speed up or down), starting to wade (by its speed along), and landing in water (by its fall).
        /// </summary>
        private void HandleMovementEvent(MovementEvents movementEvent, MovementData data)
        {
            float speed;
            switch (movementEvent)
            {
                case MovementEvents.SwimStarted:
                case MovementEvents.SwimEnded:
                case MovementEvents.DiveStarted:
                case MovementEvents.DiveEnded:
                    // In or out, up or down: as fast as it was going that way, before or after the step.
                    speed = Mathf.Max(Mathf.Abs(data.initialVelocity.y), Mathf.Abs(data.finalVelocity.y));
                    break;
                case MovementEvents.WadeStarted:
                    speed = new Vector2(data.finalVelocity.x, data.finalVelocity.z).magnitude;
                    break;
                case MovementEvents.Landed:
                    speed = -data.initialVelocity.y;
                    break;
                default:
                    return;
            }
            float step = movement.StepTime;
            if (step == splashStep && speed <= splashStepSpeed) { return; }
            if (Splash(data.finalPosition, speed, data.finalVelocity))
            {
                splashStep = step;
                splashStepSpeed = speed;
            }
        }

        /// <summary>
        /// Splashes on the water's surface under (or round) feet, as strong as speed (m/s) is of the full splash speed, if
        /// there's water there and it's strong enough to play. Returns whether it splashed.
        /// </summary>
        public bool Splash(Vector3 feet, float speed, Vector3 velocity)
        {
            float size = Size;
            float strength = Mathf.Clamp01(speed / (splashFullSpeed * (movement != null ? movement.SpeedScale : 1f)));
            if (strength < splashMinStrength || strength <= 0f) { return false; }
            float height = movement != null ? movement.Body.StandingHeight : 2f * size;
            Water water = Water.Under(feet, height);
            if (water == null) { return false; }
            var context = Context(water, feet, strength, velocity);
            Play(splashPrefab, context);
            Splashed?.Invoke(context);
            if (splashRipples) { Ripple(context); }
            return true;
        }

        private void Ripple(WaterEffectContext context)
        {
            Play(ripplePrefab, context);
            Rippled?.Invoke(context);
        }

        private WaterEffectContext Context(Water water, Vector3 at, float strength, Vector3 velocity) => new()
        {
            position = new Vector3(at.x, water.SurfaceHeight + SurfaceLift * Size, at.z),
            water = water,
            strength = strength,
            size = Size,
            velocity = velocity,
        };

        /// <summary>
        /// Ripples round the character as it swims or wades at the surface: one every Ripple Spacing it goes, and every
        /// Idle Ripple Interval while it's still. From its rendered position, every frame.
        /// </summary>
        private void Update()
        {
            MovementData data = movement != null ? movement.lastFrameMovementData : null;
            Vector3 position = transform.position;
            float moved = hasLastPosition ? new Vector2(position.x - lastPosition.x, position.z - lastPosition.z).magnitude : 0f;
            lastPosition = position;
            hasLastPosition = true;
            bool atSurface = data != null && (data.state & (CharacterState.Swimming | CharacterState.Wading)) != 0
                             && (data.state & CharacterState.Diving) == 0;
            Water water = atSurface ? Water.Under(position, movement.Body.StandingHeight) : null;
            if (water == null)
            {
                rippleDistance = 0f;
                stillTime = 0f;
                return;
            }

            float size = Size, dt = Time.deltaTime;
            bool moving = dt > 0f && moved / dt > rippleMovingSpeed * movement.SpeedScale;
            if (moving)
            {
                stillTime = 0f;
                rippleDistance += moved;
                if (rippleSpacing > 0f && rippleDistance >= rippleSpacing * size)
                {
                    rippleDistance = 0f;
                    Ripple(Context(water, position, 1f, data.finalVelocity));
                }
            }
            else
            {
                stillTime += dt;
                if (idleRippleInterval > 0f && stillTime >= idleRippleInterval)
                {
                    stillTime = 0f;
                    Ripple(Context(water, position, IdleRippleStrength, data.finalVelocity));
                }
            }
        }

        /// <summary>
        /// Plays prefab's effect for context: one that's finished if there is one, a new one while fewer than the most
        /// are playing, or else the oldest, moved to where it happens.
        /// </summary>
        private void Play(GameObject prefab, WaterEffectContext context)
        {
            if (prefab == null) { return; }
            if (!played.TryGetValue(prefab, out List<PlayedEffect> effects))
            {
                effects = new List<PlayedEffect>();
                played[prefab] = effects;
            }
            PlayedEffect effect = null;
            foreach (PlayedEffect candidate in effects)
            {
                if (candidate.gameObject != null && !candidate.IsPlaying) { effect = candidate; break; }
            }
            effects.RemoveAll(e => e.gameObject == null);
            if (effect == null && effects.Count < maxPlaying) { effect = Create(prefab, effects); }
            if (effect == null)
            {
                int next = oldest.TryGetValue(prefab, out int index) ? index % effects.Count : 0;
                effect = effects[next];
                oldest[prefab] = next + 1;
            }

            Transform t = effect.gameObject.transform;
            t.SetPositionAndRotation(context.position, prefab.transform.rotation);
            effect.gameObject.SetActive(true);
            if (effect.custom != null)
            {
                effect.custom.Play(context);
                return;
            }
            t.localScale = prefab.transform.localScale * (context.size * Mathf.Lerp(weakestScale, 1f, context.strength));
            foreach (ParticleSystem system in effect.systems)
            {
                if (system == null) { continue; }
                system.Clear(false);
                system.Play(false);
            }
        }

        private PlayedEffect Create(GameObject prefab, List<PlayedEffect> effects)
        {
            if (effectsRoot == null)
            {
                // In the world, not on the character: effects stay where they happened as it moves on.
                effectsRoot = new GameObject($"Water Effects ({name})").transform;
            }
            GameObject instance = Instantiate(prefab, effectsRoot);
            var effect = new PlayedEffect
            {
                gameObject = instance,
                custom = instance.GetComponent<IWaterEffect>(),
                systems = instance.GetComponentsInChildren<ParticleSystem>(true),
            };
            effects.Add(effect);
            return effect;
        }
    }
}
