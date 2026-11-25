using System.Collections;
using System.Text.RegularExpressions;
using MeridianMotion.Characters;
using MeridianMotion.Showcase.Audio;
using MeridianMotion.Showcase.CharacterAudio;
using MeridianMotion.Tests;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MeridianMotion.Showcase.Tests.Audio
{
    /// <summary>
    /// Tests of the character's sounds (CharacterSounds, CharacterSoundSet), on the character's test rig: a landing plays
    /// the land sound for the surface under it, and a hard one the hard row; a jump plays its sound once; a state's loop
    /// starts and stops with the state, and stops at once when the character goes; a row naming no flag is warned about
    /// once; with no audio service it does nothing; and a smaller character's sounds are higher.
    /// </summary>
    [Category(TestArea.Audio)]
    public class CharacterAudioTests : CharacterRigFixture
    {
        private CharacterAudioRig audio;

        [SetUp]
        public void MakeAudio() => audio = new CharacterAudioRig();

        [TearDown]
        public void RemoveAudio() => audio.Dispose();

        private IEnumerator SpawnWithSounds(params EventSound[] events)
        {
            rig.Floor();
            yield return rig.SpawnSettled(new Vector3(0f, 0.05f, 0f));
            audio.set.Events = events;
            rig.Character.AddComponent<CharacterSounds>().SoundSet = audio.set;
            audio.played.Clear();
        }

        private IEnumerator DropFrom(float height)
        {
            rig.Teleport(rig.Position + Vector3.up * height);
            // The rig's Grounded reads the last step, from before the teleport: step until it's in the air first.
            yield return rig.RunUntil(() => !rig.Grounded, _ => CharacterTestRig.Idle(), timeoutSeconds: 1f);
            Assert.That(rig.Grounded, Is.False, "it's falling");
            yield return rig.RunUntilLanded();
            yield return rig.Run(2, _ => CharacterTestRig.Idle());
        }

        [UnityTest]
        public IEnumerator Landing_PlaysTheLandSound_ForTheSurfaceUnderIt()
        {
            SoundEvent land = audio.Sound(), landOnMetal = audio.Sound();
            GameObject floor = rig.Floor();
            audio.Map(floor.GetComponent<Renderer>().sharedMaterial, audio.metal);
            yield return SpawnWithSounds(new EventSound
            {
                eventName = "Landed", sound = land, strengthFrom = StrengthFrom.FallSpeed,
                bySurface = new[] { new SurfaceSound { surface = audio.metal, sound = landOnMetal } },
            });
            yield return DropFrom(2f);
            Assert.That(audio.Count(landOnMetal), Is.EqualTo(1));
            Assert.That(audio.Count(land), Is.Zero);
        }

        [UnityTest]
        public IEnumerator AHardLanding_PlaysTheHardRow()
        {
            SoundEvent soft = audio.Sound(), hard = audio.Sound();
            yield return SpawnWithSounds();
            // The hard row's Min Speed: as fast as a fall of 3 m lands, from the tuning's gravity. A drop of 1 m is under it
            // (unless falling's gravity were over 3 times the tuning's), one of 6 m over it. (A shorter drop than 1 m may be
            // stepped down, with no landing at all.)
            float hardSpeed = Mathf.Sqrt(2f * rig.Gravity * 3f);
            // ... or just under the tuning's fall speed limit, if that's slower: a long drop lands at the limit.
            float limit = rig.MovementSetting<float>("maxFallSpeed");
            if (limit > 0f) { hardSpeed = Mathf.Min(hardSpeed, 0.9f * limit); }
            audio.set.Events = new[]
            {
                new EventSound { eventName = "Landed", sound = soft, strengthFrom = StrengthFrom.FallSpeed, minSpeed = 0f },
                new EventSound { eventName = "Landed", sound = hard, strengthFrom = StrengthFrom.FallSpeed, minSpeed = hardSpeed },
            };
            rig.Character.GetComponent<CharacterSounds>().SoundSet = audio.set;
            yield return DropFrom(1f);
            Assert.That(audio.Count(soft), Is.EqualTo(1), "a short drop is a soft landing");
            Assert.That(audio.Count(hard), Is.Zero);
            audio.played.Clear();
            yield return DropFrom(6f);
            Assert.That(audio.Count(hard), Is.EqualTo(1), "a long drop is a hard landing");
            Assert.That(audio.Count(soft), Is.Zero);
        }

        [UnityTest]
        public IEnumerator Jumping_PlaysItsSoundOnce()
        {
            SoundEvent jump = audio.Sound();
            yield return SpawnWithSounds(new EventSound { eventName = "Jumped", sound = jump });
            yield return rig.RunUntilBackOnTheGround(step => step < 3 ? CharacterTestRig.Jump() : CharacterTestRig.Idle());
            Assert.That(audio.Count(jump), Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator AStateLoop_StartsAndStops_WithTheState()
        {
            SoundEvent loop = audio.Sound(loop: true);
            yield return SpawnWithSounds();
            audio.set.Loops = new[] { new StateLoop { stateName = "Jumping", loop = loop, fadeOut = 0f } };
            rig.Character.GetComponent<CharacterSounds>().SoundSet = audio.set;
            int playingInTheAir = 0;
            yield return rig.RunUntil(() => rig.InState(CharacterState.Jumping), step => step < 3 ? CharacterTestRig.Jump() : CharacterTestRig.Idle());
            yield return null;
            playingInTheAir = audio.service.PlayingCount;
            yield return rig.RunUntilLanded();
            yield return rig.Run(3, _ => CharacterTestRig.Idle());
            yield return null;
            Assert.That(audio.Count(loop), Is.EqualTo(1));
            Assert.That(playingInTheAir, Is.EqualTo(1), "jumping: the loop plays");
            Assert.That(rig.InState(CharacterState.Jumping), Is.False, "it landed");
            Assert.That(audio.service.PlayingCount, Is.Zero, "landed: the loop stopped");
        }

        [UnityTest]
        public IEnumerator DespawningMidLoop_StopsTheLoopAtOnce()
        {
            SoundEvent loop = audio.Sound(loop: true);
            yield return SpawnWithSounds();
            audio.set.Loops = new[] { new StateLoop { stateName = "Grounded", loop = loop, fadeOut = 1f } };
            rig.Character.GetComponent<CharacterSounds>().SoundSet = audio.set;
            yield return null;
            yield return null;
            Assert.That(audio.service.PlayingCount, Is.EqualTo(1));
            Object.Destroy(rig.Character);
            yield return null;
            Assert.That(audio.service.PlayingCount, Is.Zero);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator ARowWhoseNameDoesntParse_WarnsOnce()
        {
            LogAssert.Expect(LogType.Warning, new Regex("NotAnEvent"));
            yield return SpawnWithSounds(new EventSound { eventName = "NotAnEvent", sound = audio.Sound() });
            rig.Character.GetComponent<CharacterSounds>().SoundSet = audio.set;
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator WithNoService_ItDoesNothing()
        {
            SoundEvent jump = audio.Sound(), loop = audio.Sound(loop: true);
            yield return SpawnWithSounds(new EventSound { eventName = "Jumped", sound = jump });
            audio.set.Loops = new[] { new StateLoop { stateName = "Grounded", loop = loop } };
            Object.Destroy(audio.service.gameObject);
            yield return null;
            yield return rig.RunUntilBackOnTheGround(step => step < 3 ? CharacterTestRig.Jump() : CharacterTestRig.Idle());
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator ASmallerCharacter_PlaysHigher()
        {
            SoundEvent jump = audio.Sound();
            yield return SpawnWithSounds(new EventSound { eventName = "Jumped", sound = jump });
            rig.Controller.SetScale(Vector3.one * 0.5f, immediately: true);
            yield return rig.Run(CharacterTestRig.StepsFor(0.3f), _ => CharacterTestRig.Idle());
            audio.played.Clear();
            yield return rig.RunUntilBackOnTheGround(step => step < 3 ? CharacterTestRig.Jump() : CharacterTestRig.Idle());
            Assert.That(audio.played.Count, Is.GreaterThanOrEqualTo(1));
            Assert.That(audio.played[0].pitchScale, Is.GreaterThan(1f));
        }
    
        private IEnumerator FallWithWind(bool player)
        {
            SoundEvent wind = audio.Sound(loop: true);
            yield return SpawnWithSounds();
            audio.set.Wind = wind;
            audio.set.WindSpeed = 1f;
            var sounds = rig.Character.GetComponent<CharacterSounds>();
            sounds.IsPlayer = player;
            rig.Teleport(rig.Position + Vector3.up * 6f);
            yield return rig.RunUntil(() => !rig.Grounded, _ => CharacterTestRig.Idle(), timeoutSeconds: 1f);
            yield return rig.Run(CharacterTestRig.StepsFor(0.3f), _ => CharacterTestRig.Idle());
            yield return null;
            windPlayed = audio.Count(wind);
        }

        private int windPlayed;

        [UnityTest]
        public IEnumerator ThePlayerFallingFast_PlaysTheWind()
        {
            yield return FallWithWind(player: true);
            Assert.That(windPlayed, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator AnNpcFallingFast_PlaysNoWind()
        {
            yield return FallWithWind(player: false);
            Assert.That(windPlayed, Is.Zero, "the wind is the player's own, heard the same wherever it is");
        }
}
}
