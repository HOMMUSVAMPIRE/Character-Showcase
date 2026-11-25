using System.Collections.Generic;
using MeridianMotion.Showcase.Audio;
using MeridianMotion.Tests;
using NUnit.Framework;
using UnityEngine;

namespace MeridianMotion.Showcase.Tests.Audio
{
    /// <summary>
    /// Tests of the audio core's values and assets: a library finds a sound by its id (and nothing for none or an unknown
    /// one) and says the next free id; a clip picker never picks the same clip twice running when told not to, and goes
    /// round them in order when told to; a sound with no clips picks none; and a request made with At has pitch scale 1.
    /// </summary>
    [Category(TestArea.Audio)]
    [TestTiming]
    public class SoundLibraryTests
    {
        private AudioTestKit kit;

        [SetUp]
        public void SetUp() => kit = new AudioTestKit();

        [TearDown]
        public void TearDown() => kit.Dispose();

        [Test]
        public void Find_ReturnsTheSoundWithThatId_AndNullForNoneOrUnknown()
        {
            kit.Sound(3);
            SoundEvent seven = kit.Sound(7);
            Assert.That(kit.Library.Find(new SoundId(7)), Is.SameAs(seven));
            Assert.That(kit.Library.Find(SoundId.None), Is.Null);
            Assert.That(kit.Library.Find(new SoundId(9)), Is.Null);
        }

        [Test]
        public void NextFreeId_IsOneMoreThanTheHighest()
        {
            Assert.That(kit.Library.NextFreeId(), Is.EqualTo(1));
            kit.Sound(3);
            kit.Sound(7);
            Assert.That(kit.Library.NextFreeId(), Is.EqualTo(8));
        }

        [Test]
        public void RandomNoRepeat_NeverPicksTheSameClipTwiceInARow()
        {
            SoundEvent sound = kit.Sound(1, clips: 3);
            sound.Order = ClipOrder.RandomNoRepeat;
            var picker = new ClipPicker(1234);
            var picked = new HashSet<AudioClip>();
            AudioClip last = null;
            for (int i = 0; i < 200; i++)
            {
                AudioClip clip = picker.Pick(sound);
                Assert.That(clip, Is.Not.SameAs(last), $"pick {i} repeated the one before");
                picked.Add(clip);
                last = clip;
            }
            Assert.That(picked.Count, Is.EqualTo(3));
        }

        [Test]
        public void InOrder_CyclesThroughTheClips()
        {
            SoundEvent sound = kit.Sound(1, clips: 3);
            sound.Order = ClipOrder.InOrder;
            var picker = new ClipPicker(1);
            AudioClip[] clips = sound.Clips;
            var expected = new[] { clips[0], clips[1], clips[2], clips[0], clips[1] };
            for (int i = 0; i < expected.Length; i++) { Assert.That(picker.Pick(sound), Is.SameAs(expected[i]), $"pick {i}"); }
        }

        [Test]
        public void ASoundWithNoClips_PicksNull()
        {
            SoundEvent sound = kit.Sound(1, clips: 0);
            Assert.That(sound.HasClips, Is.False);
            Assert.That(new ClipPicker(1).Pick(sound), Is.Null);
        }

        [Test]
        public void SoundRequestAt_HasPitchScaleOne()
        {
            SoundRequest request = SoundRequest.At(new SoundId(2), new Vector3(1f, 2f, 3f), 0.5f);
            Assert.That(request.pitchScale, Is.EqualTo(1f));
            Assert.That(request.strength, Is.EqualTo(0.5f));
            Assert.That(request.sound, Is.EqualTo(new SoundId(2)));
        }
    
        [Test]
        public void AssignIds_KeepsTheOriginalsId_ForADuplicate()
        {
            SoundEvent original = kit.Sound(5);
            kit.Library.Set(new[] { original }, new AudioBus[0]);
            kit.Library.AssignIds(new[] { original });
            // A copy made with Duplicate keeps the id, and its path sorts first ("X 1.asset" before "X.asset").
            SoundEvent copy = kit.Sound(5);
            kit.Library.AssignIds(new[] { copy, original });
            Assert.That(original.Id.Value, Is.EqualTo(5), "the one the library had keeps its id");
            Assert.That(copy.Id.Value, Is.Not.EqualTo(5).And.Not.Zero);
        }

        [Test]
        public void AssignIds_NeverReusesADeletedId()
        {
            SoundEvent a = kit.Sound(0), b = kit.Sound(0);
            kit.Library.Set(new SoundEvent[0], new AudioBus[0]);
            kit.Library.AssignIds(new[] { a, b });
            int bId = b.Id.Value;
            SoundEvent c = kit.Sound(0);
            kit.Library.AssignIds(new[] { a, c });
            Assert.That(c.Id.Value, Is.GreaterThan(bId), "the deleted sound's id isn't given again");
        }
}
}
