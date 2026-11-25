using System.Collections;
using MeridianMotion.Showcase.Audio;
using MeridianMotion.Tests;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MeridianMotion.Showcase.Tests.Audio
{
    /// <summary>
    /// Tests of the music (MusicPlayer, MusicTrack, SceneMusic): a track without an intro loops; one with an intro goes on
    /// to its loop; a crossfade ends with only the new track playing; asking for the track already playing doesn't start
    /// it again; music plays on while the game is paused; and Scene Music in a scene without an audio service does nothing.
    /// The player keeps its own timing (audio may not run in batch mode), so these read its state.
    /// </summary>
    [Category(TestArea.Audio)]
    [TestTiming]
    public class MusicPlayerTests
    {
        private AudioTestKit kit;
        private MusicPlayer music;
        private AudioBus musicBus;

        [SetUp]
        public void SetUp()
        {
            kit = new AudioTestKit();
            musicBus = kit.Bus("Music", pausesWithGame: false);
            var obj = kit.Track(new GameObject("Test Audio"));
            obj.SetActive(false);
            obj.AddComponent<AudioService>().Library = kit.Library;
            music = obj.AddComponent<MusicPlayer>();
            obj.SetActive(true);
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            kit.Dispose();
        }

        private MusicTrack Track(string name, float loopSeconds, float introSeconds = 0f)
        {
            MusicTrack track = kit.Track(ScriptableObject.CreateInstance<MusicTrack>());
            track.name = name;
            track.Loop = kit.Track(AudioClip.Create(name + " Loop", Mathf.CeilToInt(44100 * loopSeconds), 1, 44100, false));
            if (introSeconds > 0f) { track.Intro = kit.Track(AudioClip.Create(name + " Intro", Mathf.CeilToInt(44100 * introSeconds), 1, 44100, false)); }
            track.Bus = musicBus;
            return track;
        }

        [Test]
        public void ATrackWithoutIntro_Loops()
        {
            MusicTrack track = Track("A", 0.5f);
            music.Play(track);
            Assert.That(music.Current, Is.SameAs(track));
            Assert.That(music.IsLooping);
            Assert.That(music.PlayingClip, Is.SameAs(track.Loop));
        }

        [UnityTest]
        public IEnumerator ATrackWithAnIntro_GoesOnToItsLoop()
        {
            MusicTrack track = Track("A", 0.5f, introSeconds: 0.2f);
            music.Play(track);
            Assert.That(music.IsLooping, Is.False);
            Assert.That(music.PlayingClip, Is.SameAs(track.Intro));
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.That(music.IsLooping);
            Assert.That(music.PlayingClip, Is.SameAs(track.Loop));
        }

        [UnityTest]
        public IEnumerator ACrossfade_EndsWithOneTrackPlaying_TheNewOne()
        {
            MusicTrack a = Track("A", 1f), b = Track("B", 1f);
            music.Play(a, 0f);
            music.Play(b, 0.2f);
            Assert.That(music.PlayingTracks, Is.EqualTo(2), "both during the crossfade");
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.That(music.PlayingTracks, Is.EqualTo(1));
            Assert.That(music.Current, Is.SameAs(b));
        }

        [UnityTest]
        public IEnumerator PlayingTheSameTrack_DoesNotRestartIt()
        {
            MusicTrack a = Track("A", 1f);
            music.Play(a);
            float started = music.CurrentStartedAt;
            yield return new WaitForSecondsRealtime(0.1f);
            music.Play(a);
            Assert.That(music.CurrentStartedAt, Is.EqualTo(started));
            Assert.That(music.PlayingTracks, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator MusicGoesOn_WhileThePauseMenuPauses()
        {
            MusicTrack a = Track("A", 1f), b = Track("B", 1f);
            music.Play(a, 0f);
            Time.timeScale = 0f;
            AudioListener.pause = true;
            music.Play(b, 0.2f);
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.That(music.PlayingTracks, Is.EqualTo(1), "the crossfade went on while paused");
            Assert.That(music.Current, Is.SameAs(b));
            Assert.That(music.IgnoresPause);
        }

        [UnityTest]
        public IEnumerator SceneMusic_WithNoService_DoesNothing()
        {
            kit.Dispose();
            kit = new AudioTestKit();
            var obj = kit.Track(new GameObject("Scene Music"));
            obj.SetActive(false);
            obj.AddComponent<SceneMusic>().Track = kit.Track(ScriptableObject.CreateInstance<MusicTrack>());
            obj.SetActive(true);
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator SceneMusic_AsksThePlayerForItsTrack()
        {
            MusicTrack a = Track("A", 1f);
            var obj = kit.Track(new GameObject("Scene Music"));
            obj.SetActive(false);
            obj.AddComponent<SceneMusic>().Track = a;
            obj.SetActive(true);
            yield return null;
            Assert.That(music.Current, Is.SameAs(a));
        }
    }
}
