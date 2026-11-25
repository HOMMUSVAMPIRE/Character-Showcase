using System.Collections;
using System.Collections.Generic;
using MeridianMotion.Showcase.Audio;
using MeridianMotion.Tests;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Constraints;
using AllocIs = UnityEngine.TestTools.Constraints.Is;
using Is = NUnit.Framework.Is;

namespace MeridianMotion.Showcase.Tests.Audio
{
    /// <summary>
    /// Tests of the AudioService and its voice pool: a request takes a voice and is reported; never more voices than the
    /// pool; a full pool gives a higher-priority sound the oldest lowest-priority voice, and drops a lower one; a sound's
    /// Max Instances and Cooldown hold; a 3D sound beyond its Max Distance isn't started; filters can say no or change the
    /// request; a stale handle does nothing; a fade-out frees its voice; buses paused with the game pause, others don't;
    /// playing allocates nothing; and a sound with no clips is silent. The service keeps its voices' timing itself (audio
    /// may not play in batch mode), so these read its state, not the AudioSources'.
    /// </summary>
    [Category(TestArea.Audio)]
    [TestTiming]
    public class AudioServiceTests
    {
        private AudioTestKit kit;
        private AudioService service;
        private readonly List<SoundRequest> played = new();

        [SetUp]
        public void SetUp()
        {
            kit = new AudioTestKit();
            var obj = kit.Track(new GameObject("Test Audio"));
            obj.SetActive(false);
            service = obj.AddComponent<AudioService>();
            service.VoiceCount = 4;
            service.Library = kit.Library;
            service.Listener = kit.Listener.transform;
            obj.SetActive(true);
            played.Clear();
            service.Played += played.Add;
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
            kit.Dispose();
        }

        private SoundRequest Request(SoundEvent sound, Vector3 position = default) => SoundRequest.At(sound.Id, position);

        [Test]
        public void Play_TakesAVoice_AndRaisesPlayed()
        {
            SoundEvent sound = kit.Sound(1);
            SoundHandle handle = service.Play(Request(sound));
            Assert.That(handle.IsValid);
            Assert.That(service.IsPlaying(handle));
            Assert.That(service.PlayingCount, Is.EqualTo(1));
            Assert.That(played.Count, Is.EqualTo(1));
            Assert.That(played[0].sound, Is.EqualTo(sound.Id));
        }

        [Test]
        public void NeverMoreVoicesThanThePool()
        {
            for (int i = 1; i <= 10; i++) { service.Play(Request(kit.Sound(i))); }
            Assert.That(service.PlayingCount, Is.EqualTo(4));
        }

        [Test]
        public void AFullPool_StealsTheLowestPriorityOldest_ForAHigherPriority()
        {
            var handles = new List<SoundHandle>();
            for (int i = 1; i <= 4; i++) { handles.Add(service.Play(Request(kit.Sound(i, priority: 100)))); }
            SoundHandle high = service.Play(Request(kit.Sound(5, priority: 200)));
            Assert.That(service.IsPlaying(high));
            Assert.That(service.IsPlaying(handles[0]), Is.False, "the oldest gave up its voice");
            Assert.That(service.IsPlaying(handles[1]) && service.IsPlaying(handles[2]) && service.IsPlaying(handles[3]));
        }

        [Test]
        public void AFullPool_DropsALowerPriority()
        {
            for (int i = 1; i <= 4; i++) { service.Play(Request(kit.Sound(i, priority: 200))); }
            played.Clear();
            SoundHandle low = service.Play(Request(kit.Sound(5, priority: 100)));
            Assert.That(low.IsValid, Is.False);
            Assert.That(played, Is.Empty);
        }

        [Test]
        public void MaxInstances_AndCooldown_Hold()
        {
            SoundEvent two = kit.Sound(1);
            two.MaxInstances = 2;
            for (int i = 0; i < 3; i++) { service.Play(Request(two)); }
            Assert.That(service.PlayingCount, Is.EqualTo(2));

            SoundEvent cooled = kit.Sound(2);
            cooled.MaxInstances = 9;
            cooled.Cooldown = 1f;
            service.Play(Request(cooled));
            service.Play(Request(cooled));
            Assert.That(service.PlayingCount, Is.EqualTo(3));
        }

        [Test]
        public void ASoundBeyondItsMaxDistance_IsNotStarted()
        {
            SoundEvent far = kit.Sound(1, spatial: Spatial.ThreeD);
            far.MaxDistance = 10f;
            Assert.That(service.Play(Request(far, new Vector3(20f, 0f, 0f))).IsValid, Is.False);
            Assert.That(service.Play(Request(far, new Vector3(5f, 0f, 0f))).IsValid, Is.True);
            SoundEvent flat = kit.Sound(2, spatial: Spatial.TwoD);
            flat.MaxDistance = 10f;
            Assert.That(service.Play(Request(flat, new Vector3(20f, 0f, 0f))).IsValid, Is.True, "2D sounds aren't culled");
        }

        private sealed class Refuse : ISoundFilter
        {
            public bool Allow(ref SoundRequest request) => false;
        }

        private sealed class Halve : ISoundFilter
        {
            public bool Allow(ref SoundRequest request)
            {
                request.strength *= 0.5f;
                return true;
            }
        }

        [Test]
        public void AFilterSayingNo_StopsIt_AndOneChangingTheRequest_ChangesWhatPlays()
        {
            SoundEvent sound = kit.Sound(1);
            sound.MaxInstances = 9;
            float full = service.SourceOf(service.Play(Request(sound))).volume;

            var refuse = new Refuse();
            service.AddFilter(refuse);
            Assert.That(service.Play(Request(sound)).IsValid, Is.False);
            service.RemoveFilter(refuse);

            service.AddFilter(new Halve());
            float halved = service.SourceOf(service.Play(Request(sound))).volume;
            Assert.That(halved, Is.EqualTo(full * 0.5f).Within(1e-4f));
        }

        [Test]
        public void AStaleHandle_DoesNothing()
        {
            service.VoiceCount = 4;
            SoundEvent sound = kit.Sound(1, loop: true);
            sound.MaxInstances = 9;
            SoundHandle old = service.Play(Request(sound));
            service.Stop(old);
            Assert.That(service.IsPlaying(old), Is.False);
            // Fill every voice, so one of them is the old one's.
            var fresh = new List<SoundHandle>();
            for (int i = 0; i < 4; i++) { fresh.Add(service.Play(Request(sound))); }
            service.Stop(old);
            service.SetPosition(old, Vector3.one * 100f);
            foreach (SoundHandle handle in fresh) { Assert.That(service.IsPlaying(handle)); }
        }

        [UnityTest]
        public IEnumerator Stop_WithAFade_FadesOutThenFrees()
        {
            SoundHandle handle = service.Play(Request(kit.Sound(1, loop: true)));
            service.Stop(handle, 0.2f);
            yield return new WaitForSecondsRealtime(0.1f);
            Assert.That(service.IsPlaying(handle), "still fading");
            Assert.That(service.SourceOf(handle).volume, Is.LessThan(1f));
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.That(service.IsPlaying(handle), Is.False);
            Assert.That(service.PlayingCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator AOneShot_IsFreedWhenItsClipEnds()
        {
            SoundHandle handle = service.Play(Request(kit.Sound(1)));
            yield return new WaitForSecondsRealtime(1.2f);
            Assert.That(service.IsPlaying(handle), Is.False);
            Assert.That(service.PlayingCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator Pausing_PausesEffectBuses_NotOthers()
        {
            AudioBus music = kit.Bus("Music", pausesWithGame: false);
            SoundHandle effect = service.Play(Request(kit.Sound(1)));
            SoundHandle tune = service.Play(Request(kit.Sound(2, bus: music)));
            Time.timeScale = 0f;
            yield return null;
            yield return null;
            Assert.That(service.IsPaused(effect));
            Assert.That(service.IsPaused(tune), Is.False);
            yield return new WaitForSecondsRealtime(0.5f);
            Time.timeScale = 1f;
            yield return null;
            Assert.That(service.IsPaused(effect), Is.False);
            // The one-second clip was paused for half a second: it's still going at 1.2 s, the unpaused one isn't.
            yield return new WaitForSecondsRealtime(0.7f);
            Assert.That(service.IsPlaying(effect), "it went on from where it paused");
            Assert.That(service.IsPlaying(tune), Is.False);
        }

        [Test]
        public void Play_AllocatesNothing()
        {
            SoundEvent sound = kit.Sound(1);
            sound.MaxInstances = 99;
            SoundRequest request = Request(sound);
            for (int i = 0; i < 8; i++) { service.Play(request); }
            Assert.That(() => { service.Play(request); }, AllocIs.Not.AllocatingGCMemory());
        }

        [Test]
        public void NoClips_IsSilent_AndLogsNothing()
        {
            Assert.That(service.Play(Request(kit.Sound(1, clips: 0))).IsValid, Is.False);
            Assert.That(played, Is.Empty);
            LogAssert.NoUnexpectedReceived();
        }
    
        [Test]
        public void AFullPool_DropsAnEqualPriority_RatherThanStealing()
        {
            var handles = new List<SoundHandle>();
            for (int i = 1; i <= 4; i++) { handles.Add(service.Play(Request(kit.Sound(i, priority: 128)))); }
            Assert.That(service.Play(Request(kit.Sound(5, priority: 128))).IsValid, Is.False);
            foreach (SoundHandle handle in handles) { Assert.That(service.IsPlaying(handle)); }
        }
}
}
