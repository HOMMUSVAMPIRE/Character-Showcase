using System.Collections;
using System.Collections.Generic;
using MeridianMotion.Characters;
using MeridianMotion.Showcase.CharacterAudio;
using MeridianMotion.Tests;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MeridianMotion.Showcase.Tests.Audio
{
    /// <summary>
    /// The audio only reads the game: the same course (walking, jumping, crouching, landing) run from the same start, once
    /// without the character's sounds and once with them (and an audio service playing them), moves the character the same,
    /// step for step, to the bit. (The package's determinism tests can't reach the showcase's audio, so this holds it.)
    /// </summary>
    [Category(TestArea.Audio)]
    public class AudioReadsOnlyTests : CharacterRigFixture
    {
        private static StepInputs Course(int step) => step switch
        {
            < 40 => CharacterTestRig.Move(Vector3.forward),
            < 43 => CharacterTestRig.Jump(Vector3.forward),
            < 90 => CharacterTestRig.Move(Vector3.forward),
            < 130 => CharacterTestRig.Crouch(Vector3.right),
            _ => CharacterTestRig.Idle(),
        };

        private IEnumerator RunCourse(List<(Vector3 feet, Vector3 velocity)> into)
        {
            yield return rig.Run(200, Course, _ => into.Add((rig.Movement.Feet, rig.Movement.lastStep.finalVelocity)));
        }

        [UnityTest]
        public IEnumerator AudioDoesNotChangeTheMovement()
        {
            var without = new List<(Vector3, Vector3)>();
            var with = new List<(Vector3, Vector3)>();
            rig.Floor();
            yield return rig.SpawnSettled(new Vector3(0f, 0.05f, 0f));
            Vector3 start = rig.Movement.Feet;
            yield return RunCourse(without);

            yield return ReplaceRig();
            rig.Floor();
            yield return rig.SpawnSettled(start);
            using var audio = new CharacterAudioRig();
            audio.set.Events = new[]
            {
                new EventSound { eventName = "Jumped", sound = audio.Sound() },
                new EventSound { eventName = "Landed", sound = audio.Sound(), strengthFrom = StrengthFrom.FallSpeed },
            };
            audio.set.Loops = new[] { new StateLoop { stateName = "Crouching", loop = audio.Sound(loop: true) } };
            rig.Character.AddComponent<CharacterSounds>().SoundSet = audio.set;
            yield return RunCourse(with);

            Assert.That(audio.played.Count, Is.GreaterThan(0), "it played something");
            Assert.That(with.Count, Is.EqualTo(without.Count));
            for (int i = 0; i < with.Count; i++)
            {
                Assert.That(with[i], Is.EqualTo(without[i]), $"step {i} differs");
            }
        }
    }
}
