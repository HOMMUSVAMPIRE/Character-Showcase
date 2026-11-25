using System.Collections;
using System.Collections.Generic;
using MeridianMotion.Showcase.Audio;
using MeridianMotion.Tests;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MeridianMotion.Showcase.Tests.Audio
{
    /// <summary>
    /// Tests of a Sound Emitter (a sound on a level piece): played on enable, where it is; a looping one stops when it's
    /// disabled and follows its object; and without an audio service it does nothing.
    /// </summary>
    [Category(TestArea.Audio)]
    [TestTiming]
    public class SoundEmitterTests
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
            service.Library = kit.Library;
            obj.SetActive(true);
            played.Clear();
            service.Played += played.Add;
        }

        [TearDown]
        public void TearDown() => kit.Dispose();

        private SoundEmitter Emitter(SoundEvent sound, Vector3 position)
        {
            var obj = kit.Track(new GameObject("Emitter"));
            obj.transform.position = position;
            obj.SetActive(false);
            SoundEmitter emitter = obj.AddComponent<SoundEmitter>();
            emitter.Sound = sound;
            obj.SetActive(true);
            return emitter;
        }

        [Test]
        public void PlayOnEnable_PlaysAtItsPosition()
        {
            Emitter(kit.Sound(1), new Vector3(3f, 0f, 0f));
            Assert.That(played.Count, Is.EqualTo(1));
            Assert.That(played[0].position, Is.EqualTo(new Vector3(3f, 0f, 0f)));
        }

        [UnityTest]
        public IEnumerator ALoopingEmitter_StopsWhenDisabled()
        {
            SoundEmitter emitter = Emitter(kit.Sound(1, loop: true), Vector3.zero);
            Assert.That(service.IsPlaying(emitter.Handle));
            SoundHandle handle = emitter.Handle;
            emitter.gameObject.SetActive(false);
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.That(service.IsPlaying(handle), Is.False, "faded out (its Fade Out) and freed");
        }

        [UnityTest]
        public IEnumerator ALoopingEmitter_FollowsItsObject()
        {
            SoundEmitter emitter = Emitter(kit.Sound(1, loop: true, spatial: Spatial.ThreeD), Vector3.zero);
            emitter.transform.position = new Vector3(5f, 0f, 0f);
            yield return null;
            yield return null;
            Assert.That(Vector3.Distance(service.SourceOf(emitter.Handle).transform.position, emitter.transform.position),
                        Is.LessThan(0.01f));
        }

        [UnityTest]
        public IEnumerator WithNoService_DoesNothing()
        {
            SoundEvent sound = kit.Sound(1, loop: true);
            Object.Destroy(service.gameObject);
            yield return null;
            SoundEmitter emitter = Emitter(sound, Vector3.zero);
            yield return null;
            emitter.gameObject.SetActive(false);
            emitter.gameObject.SetActive(true);
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }
    
        [UnityTest]
        public IEnumerator AnEmitterEnabledBeforeTheService_StillPlays()
        {
            SoundEvent sound = kit.Sound(1, loop: true);
            Object.DestroyImmediate(service.gameObject);
            // As a scene loads: the emitter's object enabled before the service's, in the same frame.
            SoundEmitter emitter = Emitter(sound, Vector3.zero);
            var obj = kit.Track(new GameObject("Late Audio"));
            obj.SetActive(false);
            AudioService late = obj.AddComponent<AudioService>();
            late.Library = kit.Library;
            obj.SetActive(true);
            yield return null;
            Assert.That(late.IsPlaying(emitter.Handle), "it played once the service was there");
        }
}
}
