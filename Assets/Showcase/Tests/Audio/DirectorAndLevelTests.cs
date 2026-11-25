using System.Collections;
using MeridianMotion.Levels;
using MeridianMotion.Showcase.Audio;
using MeridianMotion.Showcase.CharacterAudio;
using MeridianMotion.Tests;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MeridianMotion.Showcase.Tests.Audio
{
    /// <summary>
    /// Tests of the scene's character audio director (CharacterAudioDirector: what gives each spawned character its
    /// sounds) and the level's sounds (LevelSounds): a player gets the default set, an NPC its prefab's; attaching twice
    /// keeps one; a checkpoint reached by the player plays, by an NPC doesn't; a gate opening plays where it is; and once
    /// the level's sounds are gone (the scene restarted), nothing is listening.
    /// </summary>
    [Category(TestArea.Audio)]
    public class DirectorAndLevelTests : CharacterRigFixture
    {
        private CharacterAudioRig audio;
        private CharacterAudioDirector director;
        private CharacterSoundSet foxSet;

        [SetUp]
        public void MakeAudio()
        {
            audio = new CharacterAudioRig();
            director = audio.kit.Track(new GameObject("Director")).AddComponent<CharacterAudioDirector>();
            foxSet = audio.kit.Track(ScriptableObject.CreateInstance<CharacterSoundSet>());
            director.DefaultSet = audio.set;
            director.ByPrefab = new[] { new CharacterAudioDirector.PrefabSoundSet { prefabName = "Fox", set = foxSet } };
        }

        [TearDown]
        public void RemoveAudio() => audio.Dispose();

        private GameObject Named(string name) => audio.kit.Track(new GameObject(name));

        [Test]
        public void AttachPlayer_AddsCharacterSounds_WithTheDefaultSet()
        {
            GameObject player = Named("Character(Clone)");
            director.AttachPlayer(player.transform);
            Assert.That(player.GetComponent<CharacterSounds>().SoundSet, Is.SameAs(audio.set));
            Assert.That(director.Player, Is.SameAs(player.transform));
        }

        [Test]
        public void AttachNpc_ChoosesTheSetByPrefabName()
        {
            GameObject fox = Named("Fox(Clone)");
            director.AttachNpc(fox.transform, "route");
            Assert.That(fox.GetComponent<CharacterSounds>().SoundSet, Is.SameAs(foxSet));
            Assert.That(director.Player, Is.Null, "an NPC isn't the player");
        }

        [Test]
        public void AttachingTwice_KeepsOneCharacterSounds()
        {
            GameObject player = Named("Character(Clone)");
            director.AttachPlayer(player.transform);
            director.AttachPlayer(player.transform);
            Assert.That(player.GetComponents<CharacterSounds>().Length, Is.EqualTo(1));
        }

        private (LevelSounds sounds, LevelSoundSet set) MakeLevelSounds()
        {
            LevelSoundSet set = audio.kit.Track(ScriptableObject.CreateInstance<LevelSoundSet>());
            set.CheckpointReached = audio.Sound();
            set.GateOpened = audio.Sound();
            GameObject obj = Named("Level Sounds");
            obj.SetActive(false);
            LevelSounds sounds = obj.AddComponent<LevelSounds>();
            sounds.Sounds = set;
            sounds.Director = director;
            obj.SetActive(true);
            return (sounds, set);
        }

        [UnityTest]
        public IEnumerator ACheckpointReachedByThePlayer_Plays_ByAnNpc_DoesNot()
        {
            rig.Floor();
            yield return rig.SpawnSettled(new Vector3(0f, 0.05f, 0f));
            (_, LevelSoundSet set) = MakeLevelSounds();
            director.AttachPlayer(rig.Character.transform);
            Checkpoint checkpoint = Named("Checkpoint").AddComponent<Checkpoint>();
            checkpoint.Entered(rig.Movement, new LevelPresence());
            Assert.That(audio.Count(set.CheckpointReached), Is.EqualTo(1), "the player reached it");

            director.AttachPlayer(Named("Someone Else").transform);
            checkpoint.Entered(rig.Movement, new LevelPresence());
            Assert.That(audio.Count(set.CheckpointReached), Is.EqualTo(1), "not the player now: no sound");
        }

        [Test]
        public void AGateOpening_PlaysAtTheGate()
        {
            (_, LevelSoundSet set) = MakeLevelSounds();
            set.GateOpened.Spatial = Spatial.ThreeD;
            GameObject gateObj = Named("Gate");
            gateObj.transform.position = new Vector3(4f, 1f, 2f);
            gateObj.AddComponent<ShortcutGate>().Open();
            Assert.That(audio.Count(set.GateOpened), Is.EqualTo(1));
            Assert.That(audio.played[audio.played.Count - 1].position, Is.EqualTo(new Vector3(4f, 1f, 2f)));
        }

        [UnityTest]
        public IEnumerator AfterLevelSoundsIsDestroyed_NothingIsListening()
        {
            rig.Floor();
            yield return rig.SpawnSettled(new Vector3(0f, 0.05f, 0f));
            (LevelSounds sounds, LevelSoundSet set) = MakeLevelSounds();
            director.AttachPlayer(rig.Character.transform);
            Object.Destroy(sounds.gameObject);
            yield return null;
            Named("Checkpoint").AddComponent<Checkpoint>().Entered(rig.Movement, new LevelPresence());
            Named("Gate").AddComponent<ShortcutGate>().Open();
            Assert.That(audio.Count(set.CheckpointReached) + audio.Count(set.GateOpened), Is.Zero);
            LogAssert.NoUnexpectedReceived();
        }
    }
}
