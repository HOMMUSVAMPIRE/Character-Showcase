using System.Collections;
using System.Collections.Generic;
using MeridianMotion.Characters;
using MeridianMotion.Showcase.Audio;
using MeridianMotion.Showcase.CharacterAudio;
using MeridianMotion.Showcase.Grass;
using MeridianMotion.Tests;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MeridianMotion.Showcase.Tests.Audio
{
    /// <summary>
    /// Tests of the character's footsteps without a model (CharacterSounds, FootstepCadence: with one, they follow its feet,
    /// AnimatedFootstepTests): the cadence steps every stride, keeping what's left over; walking and running step at their
    /// cadences (Walk Cadence, Run Cadence); running is louder than walking; standing still or in the air
    /// makes none; on a lawn they're the grass's; and sliding makes none. Footsteps are counted on the movement's steps,
    /// so they come out the same however the frames fall.
    /// </summary>
    [Category(TestArea.Audio)]
    public class FootstepTests : CharacterRigFixture
    {
        private CharacterAudioRig audio;
        private SoundEvent step;

        [SetUp]
        public void MakeAudio() => audio = new CharacterAudioRig();

        [TearDown]
        public void RemoveAudio() => audio.Dispose();

        [Test]
        public void FootstepCadence_StepsEveryStride_KeepingTheRemainder()
        {
            var cadence = new FootstepCadence();
            cadence.Reset(1f);
            var steps = new List<bool>();
            for (int i = 0; i < 5; i++) { steps.Add(cadence.Advance(0.4f, 1f)); }
            Assert.That(steps, Is.EqualTo(new[] { false, true, false, true, false }));
        }

        private IEnumerator SpawnWithFootsteps()
        {
            rig.Floor();
            yield return rig.SpawnSettled(new Vector3(0f, 0.05f, 0f));
            // No model: these tests step the movement only, so a model's feet would never move (AnimatedFootstepTests
            // has them); without one, footsteps go by the cadence.
            yield return rig.WaitForModel();
            rig.Models.UnloadModel();
            step = audio.Sound();
            audio.set.Footsteps = new FootstepSettings { step = step };
            rig.Character.AddComponent<CharacterSounds>().SoundSet = audio.set;
            audio.played.Clear();
        }

        private List<float> StepStrengths()
        {
            var strengths = new List<float>();
            foreach (SoundRequest request in audio.played) { if (request.sound == step.Id) { strengths.Add(request.strength); } }
            return strengths;
        }

        private IEnumerator StepsPerSecond(System.Func<int, StepInputs> input, float expectedPerSecond)
        {
            yield return SpawnWithFootsteps();
            // Up to speed first: the cadence is the speed's.
            yield return rig.Run(CharacterTestRig.StepsFor(1f), input);
            audio.played.Clear();
            yield return rig.Run(CharacterTestRig.StepsFor(3f), input);
            float expected = expectedPerSecond * 3f;
            Assert.That(audio.Count(step), Is.InRange(Mathf.FloorToInt(expected) - 1, Mathf.CeilToInt(expected) + 1),
                        $"{expectedPerSecond} steps a second for 3 s");
        }

        [UnityTest]
        public IEnumerator WithoutAModel_WalkingStepsAtTheWalkingCadence()
        {
            // Walking is at the movement's Walk Speed (Walking And Running), whose cadence is the set's Walk Cadence.
            yield return StepsPerSecond(_ => CharacterTestRig.Move(Vector3.forward), new FootstepSettings().walkCadence);
        }

        [UnityTest]
        public IEnumerator WithoutAModel_RunningStepsAtTheRunningCadence()
        {
            yield return StepsPerSecond(_ => CharacterTestRig.Sprint(Vector3.forward), new FootstepSettings().runCadence);
        }

        [UnityTest]
        public IEnumerator RunningIsLouderThanWalking()
        {
            yield return SpawnWithFootsteps();
            yield return rig.Run(CharacterTestRig.StepsFor(1.5f), _ => CharacterTestRig.Move(Vector3.forward));
            List<float> walking = StepStrengths();
            yield return rig.RunUntilStill(_ => CharacterTestRig.Idle());
            audio.played.Clear();
            yield return rig.Run(CharacterTestRig.StepsFor(1.5f), _ => CharacterTestRig.Sprint(Vector3.forward));
            List<float> running = StepStrengths();
            Assume.That(walking.Count > 1 && running.Count > 1);
            Assert.That(Average(running), Is.GreaterThan(Average(walking)));
        }

        private static float Average(List<float> values)
        {
            float sum = 0f;
            foreach (float value in values) { sum += value; }
            return sum / values.Count;
        }

        [UnityTest]
        public IEnumerator StandingStill_OrInTheAir_NoSteps()
        {
            yield return SpawnWithFootsteps();
            yield return rig.Run(CharacterTestRig.StepsFor(1f), _ => CharacterTestRig.Idle());
            Assert.That(audio.Count(step), Is.Zero, "standing still");
            int inTheAir = 0;
            yield return rig.RunUntil(() => !rig.Grounded, s => s < 3 ? CharacterTestRig.Jump() : CharacterTestRig.Idle());
            audio.played.Clear();
            yield return rig.RunUntilLanded(_ => inTheAir = audio.Count(step));
            Assert.That(inTheAir, Is.Zero, "in the air");
        }

        [UnityTest]
        public IEnumerator OnGrass_PlaysTheGrassStep()
        {
            yield return SpawnWithFootsteps();
            SoundEvent grassStep = audio.Sound();
            audio.set.Footsteps.bySurface = new[] { new SurfaceSound { surface = audio.grass, sound = grassStep } };
            GameObject lawn = audio.kit.Track(new GameObject("Lawn"));
            GrassField field = lawn.AddComponent<GrassField>();
            var density = new byte[40 * 40];
            for (int i = 0; i < density.Length; i++) { density[i] = 255; }
            field.SetMask(new Vector2(-20f, -20f), 1f, new Vector2Int(40, 40), new[] { new GrassField.Layer { height = 0f, density = density } });
            yield return rig.Run(CharacterTestRig.StepsFor(1.5f), _ => CharacterTestRig.Move(Vector3.forward));
            Assert.That(audio.Count(grassStep), Is.GreaterThan(0));
            Assert.That(audio.Count(step), Is.Zero, "every step was on grass");
        }

        [UnityTest]
        public IEnumerator Sliding_PlaysNoSteps()
        {
            yield return SpawnWithFootsteps();
            yield return rig.Run(CharacterTestRig.StepsFor(1.2f), _ => CharacterTestRig.Sprint(Vector3.forward));
            bool slid = false;
            int whileSliding = 0;
            yield return rig.Run(CharacterTestRig.StepsFor(0.4f), _ =>
            {
                StepInputs inputs = CharacterTestRig.Sprint(Vector3.forward);
                inputs.crouch = CharacterTestRig.Held();
                return inputs;
            }, _ =>
            {
                if (!rig.InState(CharacterState.Sliding)) { return; }
                if (!slid) { audio.played.Clear(); }
                slid = true;
                whileSliding = audio.Count(step);
            });
            Assume.That(slid, "it slid");
            Assert.That(whileSliding, Is.Zero);
        }
    }
}
