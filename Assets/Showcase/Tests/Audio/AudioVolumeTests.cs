using MeridianMotion.Showcase.Audio;
using MeridianMotion.Tests;
using NUnit.Framework;
using UnityEngine;

namespace MeridianMotion.Showcase.Tests.Audio
{
    /// <summary>
    /// Tests of the buses' volumes (AudioService.SetVolume, GetVolume): a bus nobody has set is at its Default Volume; a
    /// volume set is saved and read back by a new service; setting one changes what's playing at once; and it's held to
    /// 0 to 1. They save under a prefix of their own, cleared after each.
    /// </summary>
    [Category(TestArea.Audio)]
    [TestTiming]
    public class AudioVolumeTests
    {
        private const string Prefix = "MeridianMotion.Audio.Test.";
        private AudioTestKit kit;
        private AudioBus bus;

        [SetUp]
        public void SetUp()
        {
            kit = new AudioTestKit();
            bus = kit.Bus("Volume Test Bus", pausesWithGame: true);
            PlayerPrefs.DeleteKey(Prefix + bus.BusName);
        }

        [TearDown]
        public void TearDown()
        {
            PlayerPrefs.DeleteKey(Prefix + bus.BusName);
            kit.Dispose();
        }

        private AudioService NewService()
        {
            var obj = kit.Track(new GameObject("Test Audio"));
            obj.SetActive(false);
            AudioService service = obj.AddComponent<AudioService>();
            service.Library = kit.Library;
            service.VolumeKeyPrefix = Prefix;
            obj.SetActive(true);
            return service;
        }

        [Test]
        public void AnUnsavedBus_HasItsDefaultVolume()
        {
            bus.DefaultVolume = 0.7f;
            Assert.That(NewService().GetVolume(bus), Is.EqualTo(0.7f).Within(1e-5f));
        }

        [Test]
        public void SetVolume_IsSaved_AndReadByANewService()
        {
            AudioService first = NewService();
            first.SetVolume(bus, 0.25f);
            Object.DestroyImmediate(first.gameObject);
            Assert.That(NewService().GetVolume(bus), Is.EqualTo(0.25f).Within(1e-5f));
        }

        [Test]
        public void SetVolume_ChangesWhatsPlaying()
        {
            AudioService service = NewService();
            SoundHandle handle = service.Play(kit.Sound(1, bus: bus), Vector3.zero);
            float before = service.SourceOf(handle).volume;
            service.SetVolume(bus, 0.5f);
            Assert.That(service.SourceOf(handle).volume, Is.EqualTo(before * 0.5f).Within(1e-4f));
        }

        [Test]
        public void SetVolume_Clamps()
        {
            AudioService service = NewService();
            service.SetVolume(bus, 3f);
            Assert.That(service.GetVolume(bus), Is.EqualTo(1f));
            service.SetVolume(bus, -1f);
            Assert.That(service.GetVolume(bus), Is.EqualTo(0f));
        }
    }
}
