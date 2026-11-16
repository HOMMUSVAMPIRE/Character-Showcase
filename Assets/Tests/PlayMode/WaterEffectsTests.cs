using System.Collections;
using System.Collections.Generic;
using Character;
using Level;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CharacterShowcase.Tests
{
    /// <summary>
    /// Tests of the character's splashes and ripples (WaterEffects): falling in splashes once, on the surface, as strong
    /// as it fell fast, playing the splash prefab; jumping out splashes; swimming along ripples every ripple spacing it
    /// goes, and treading water every idle ripple interval; on dry land nothing plays; a prefab with its own effect
    /// (IWaterEffect) is played with where and how strong; no more of an effect play than the most at once (the oldest
    /// reused); and without prefabs the events are still raised. Expected values are read from the WaterEffects' settings.
    /// </summary>
    [Category(TestArea.Traversal)]
    public class WaterEffectsTests : CharacterRigFixture
    {
        private const float PoolDepth = 5f, PoolSize = 60f;

        // Ripples come every frame (Update), so these need Unity's frames.
        protected override bool MovementOnly => false;

        private Water water;
        private WaterEffects effects;
        private readonly List<WaterEffectContext> splashes = new(), ripples = new();

        /// <summary>An effect of a test's own, recording how it's played.</summary>
        private sealed class RecordingEffect : MonoBehaviour, IWaterEffect
        {
            public static readonly List<WaterEffectContext> Played = new();
            public bool playing;
            public void Play(WaterEffectContext context) { Played.Add(context); playing = true; }
            public bool IsPlaying => playing;
        }

        [SetUp]
        public void Clear()
        {
            splashes.Clear();
            ripples.Clear();
            RecordingEffect.Played.Clear();
        }

        /// <summary>A pool (its floor, and water over it) and the character spawned in mid-air above it, at the given height.</summary>
        private IEnumerator SpawnOverThePool(float height)
        {
            rig.Floor(-PoolDepth);
            water = rig.Water(Vector3.zero, new Vector3(PoolSize, PoolDepth, PoolSize));
            yield return rig.SpawnSettled(Vector3.up * height, settleSeconds: 0f);
            Listen();
        }

        private void Listen()
        {
            effects = rig.Character.GetComponent<WaterEffects>();
            Assume.That(effects, Is.Not.Null, "Test setup: the Character prefab should have WaterEffects.");
            effects.Splashed += c => splashes.Add(c);
            effects.Rippled += c => ripples.Add(c);
        }

        private IEnumerator FallInAndSettle()
        {
            yield return rig.RunUntil(() => rig.Swimming, _ => CharacterTestRig.Idle(), timeoutSeconds: 4f);
            yield return rig.Run(0.5f, _ => CharacterTestRig.Idle());
        }

        [UnityTest]
        public IEnumerator FallingIn_SplashesOnce_OnTheSurface_AsStrongAsItFell()
        {
            const float drop = 4f;
            yield return SpawnOverThePool(drop);
            yield return FallInAndSettle();
            Assert.That(splashes.Count, Is.EqualTo(1), "Falling in should splash once.");
            WaterEffectContext splash = splashes[0];
            Assert.That(splash.water, Is.SameAs(water));
            Assert.That(splash.position.y, Is.EqualTo(water.SurfaceHeight).Within(0.05f), "The splash should be on the water's surface.");
            Assert.That(new Vector2(splash.position.x, splash.position.z).magnitude, Is.LessThan(0.5f), "The splash should be where the character fell in.");
            float fallSpeed = Mathf.Sqrt(2f * rig.Gravity * drop);
            Assert.That(splash.strength, Is.EqualTo(Mathf.Clamp01(fallSpeed / (effects.SplashFullSpeed * rig.Movement.SpeedScale))).Within(0.15f),
                "The splash should be as strong as the character fell fast.");
            Assert.That(effects.SplashPrefab, Is.Not.Null, "Test setup: the Character prefab should have a splash prefab.");
            ParticleSystem played = Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None)
                .FirstOrDefaultAlive(effects.SplashPrefab.name);
            Assert.That(played, Is.Not.Null, "The splash prefab's particles should be playing.");
        }

        [UnityTest]
        public IEnumerator JumpingOut_Splashes()
        {
            yield return SpawnOverThePool(1f);
            yield return FallInAndSettle();
            splashes.Clear();
            yield return rig.Run(1, _ => CharacterTestRig.Jump(0));
            Assert.That(splashes.Count, Is.EqualTo(1), "Jumping out of the water should splash.");
            Assert.That(splashes[0].position.y, Is.EqualTo(water.SurfaceHeight).Within(0.05f));
        }

        [UnityTest]
        public IEnumerator SwimmingAlong_RipplesEveryRippleSpacing()
        {
            yield return SpawnOverThePool(1f);
            yield return FallInAndSettle();
            yield return rig.Run(0.5f, _ => CharacterTestRig.Move(Vector3.forward)); // Up to speed.
            ripples.Clear();
            float start = rig.Position.z;
            yield return rig.Run(3f, _ => CharacterTestRig.Move(Vector3.forward));
            float swum = rig.Position.z - start, spacing = effects.RippleSpacing * rig.Movement.HeightScale;
            Assert.That(ripples.Count, Is.EqualTo(Mathf.FloorToInt(swum / spacing)).Within(1), $"Swimming {swum:0.0} m should ripple every {spacing:0.0} m.");
            foreach (WaterEffectContext ripple in ripples)
            {
                Assert.That(ripple.position.y, Is.EqualTo(water.SurfaceHeight).Within(0.05f), "Ripples should be on the water's surface.");
            }
        }

        [UnityTest]
        public IEnumerator TreadingWater_RipplesEveryIdleRippleInterval()
        {
            yield return SpawnOverThePool(1f);
            yield return FallInAndSettle();
            ripples.Clear();
            splashes.Clear();
            const float seconds = 4f;
            yield return rig.Run(seconds, _ => CharacterTestRig.Idle());
            Assert.That(ripples.Count, Is.EqualTo(Mathf.FloorToInt(seconds / effects.IdleRippleInterval)).Within(1),
                "Treading water should ripple every idle ripple interval.");
            Assert.That(splashes, Is.Empty);
        }

        [UnityTest]
        public IEnumerator OnDryLand_NothingPlays()
        {
            rig.Floor();
            yield return rig.SpawnSettled(Vector3.up * 3f, settleSeconds: 0f);
            Listen();
            yield return rig.RunUntilLanded();
            yield return rig.Run(1f, _ => CharacterTestRig.Move(Vector3.forward));
            Assert.That(splashes, Is.Empty, "Landing and walking on dry land shouldn't splash.");
            Assert.That(ripples, Is.Empty);
        }

        [UnityTest]
        public IEnumerator APrefabWithItsOwnEffect_IsPlayedWithWhereAndHowStrong()
        {
            yield return SpawnOverThePool(4f);
            var template = new GameObject("Custom Splash");
            template.SetActive(false);
            template.AddComponent<RecordingEffect>();
            effects.SplashPrefab = template;
            yield return FallInAndSettle();
            Object.Destroy(template);
            Assert.That(RecordingEffect.Played.Count, Is.EqualTo(1), "The splash prefab's own effect should be played.");
            Assert.That(RecordingEffect.Played[0].position, Is.EqualTo(splashes[0].position));
            Assert.That(RecordingEffect.Played[0].strength, Is.EqualTo(splashes[0].strength));
            Assert.That(RecordingEffect.Played[0].size, Is.EqualTo(rig.Movement.HeightScale));
        }

        [UnityTest]
        public IEnumerator NoMoreOfAnEffectPlayThanTheMostAtOnce_TheOldestReused()
        {
            yield return SpawnOverThePool(1f);
            yield return FallInAndSettle();
            var template = new GameObject("Endless Splash");
            template.SetActive(false);
            template.AddComponent<RecordingEffect>(); // Never finishes.
            effects.SplashPrefab = template;
            int most = effects.MaxPlaying;
            for (int i = 0; i < most + 5; i++) { effects.Splash(rig.Position, 100f, Vector3.zero); }
            Object.Destroy(template);
            Assert.That(RecordingEffect.Played.Count, Is.EqualTo(most + 5), "Every splash should be played.");
            int instances = 0;
            foreach (RecordingEffect e in Object.FindObjectsByType<RecordingEffect>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (e.gameObject != template) { instances++; }
            }
            Assert.That(instances, Is.EqualTo(most), "No more than the most at once should be made; after that, the oldest are reused.");
        }

        [UnityTest]
        public IEnumerator WithoutPrefabs_TheEventsAreStillRaised()
        {
            yield return SpawnOverThePool(4f);
            effects.SplashPrefab = null;
            effects.RipplePrefab = null;
            yield return FallInAndSettle();
            Assert.That(splashes.Count, Is.EqualTo(1), "Without a splash prefab, Splashed should still be raised.");
            Assert.That(ripples, Is.Not.Empty, "Without a ripple prefab, Rippled should still be raised.");
        }

    }

    internal static class ParticleSearch
    {
        /// <summary>The first of these particle systems still alive whose object's name starts with prefabName (an instance of it), or null.</summary>
        public static ParticleSystem FirstOrDefaultAlive(this ParticleSystem[] systems, string prefabName)
        {
            foreach (ParticleSystem system in systems)
            {
                if (system.gameObject.name.StartsWith(prefabName) && system.IsAlive(true)) { return system; }
            }
            return null;
        }
    }
}
