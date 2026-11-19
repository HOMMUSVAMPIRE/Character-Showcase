using MeridianMotion.Characters;
using NUnit.Framework;

namespace MeridianMotion.Tests.EditMode.Animations
{
    /// <summary>
    /// Tests of the values abilities publish for their look (AbilityValues): a value is kept by its name's hash, the same
    /// in every process; set, it's read back, and a name never set isn't found; a table holds its capacity of values, and
    /// says so past it; and setting and reading allocate nothing (they're per step).
    /// </summary>
    [Category(TestArea.Animation)]
    [TestTiming]
    public class AbilityValuesTests
    {
        [Test]
        public void AValuesId_IsItsNamesHash_TheSameEveryTime()
        {
            Assert.That(new AbilityValueId("Glide Tilt").hash, Is.EqualTo(new AbilityValueId("Glide Tilt").hash));
            Assert.That(new AbilityValueId("Glide Tilt").hash, Is.Not.EqualTo(new AbilityValueId("Glide Speed").hash));
            // FNV-1a's 64-bit hash of the empty name, which no process can disagree on.
            Assert.That(unchecked((ulong)new AbilityValueId("").hash), Is.EqualTo(14695981039346656037UL));
        }

        [Test]
        public void AValueSet_IsReadBack_AndOneNeverSet_IsntFound()
        {
            var values = new AbilityValues();
            var tilt = new AbilityValueId("Glide Tilt");
            Assert.That(values.TryGet(tilt, out _), Is.False);
            values.Set(tilt, 0.5f);
            values.Set(tilt, 0.75f);
            Assert.That(values.TryGet(tilt, out float value), Is.True);
            Assert.That(value, Is.EqualTo(0.75f));
            Assert.That(values.TryGet(new AbilityValueId("Something Else"), out _), Is.False);
        }

        [Test]
        public void ATable_HoldsItsCapacity_AndSaysSoPastIt()
        {
            var values = new AbilityValues();
            for (int i = 0; i < AbilityValues.Capacity; i++) { values.Set(new AbilityValueId("Value " + i), i); }
            for (int i = 0; i < AbilityValues.Capacity; i++)
            {
                Assert.That(values.TryGet(new AbilityValueId("Value " + i), out float value), Is.True);
                Assert.That(value, Is.EqualTo(i));
            }
            Assert.Throws<System.InvalidOperationException>(() => values.Set(new AbilityValueId("One Too Many"), 1f));
        }

        [Test]
        public void SettingAndReading_AllocateNothing()
        {
            var values = new AbilityValues();
            var tilt = new AbilityValueId("Glide Tilt");
            values.Set(tilt, 0f);
            long before = System.GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++)
            {
                values.Set(tilt, i);
                values.TryGet(tilt, out _);
            }
            Assert.That(System.GC.GetAllocatedBytesForCurrentThread() - before, Is.EqualTo(0));
        }
    }
}
