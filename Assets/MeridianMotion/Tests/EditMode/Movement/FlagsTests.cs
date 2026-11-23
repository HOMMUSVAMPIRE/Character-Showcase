using System.Linq;
using System.Text.RegularExpressions;
using MeridianMotion.Characters;
using NUnit.Framework;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.TestTools;

namespace MeridianMotion.Tests.EditMode.Movement
{
    /// <summary>
    /// Tests of the 256-bit flags the character's states and movement events are made of (Flags256, CharacterState,
    /// MovementEvents): every bit, the highest included, is set, tested and found again; one flag's bit is what a switch
    /// is on; going through the flags set finds each once, in order; they're written as text and read back, and saved by
    /// Unity; their names are their fields'; and each flag is the bit it was as an enum (the recorded golden paths and
    /// the translator's saved triggers were converted by bit).
    /// </summary>
    [Category(TestArea.Movement)]
    [TestTiming]
    public class FlagsTests
    {
        [Test]
        public void EveryBit_IsSetAndFound_TheHighestIncluded([Values(0, 1, 63, 64, 127, 128, 200, 255)] int bit)
        {
            Flags256 flags = Flags256.Bit(bit);
            Assert.That(flags.IsEmpty, Is.False);
            Assert.That(flags.Has(bit), Is.True);
            Assert.That(flags.SingleBit, Is.EqualTo(bit));
            Assert.That(flags.NextBit(0), Is.EqualTo(bit));
            Assert.That(flags.NextBit(bit + 1), Is.EqualTo(-1));
            Assert.That(flags.HasAny(Flags256.Bit(bit)), Is.True);
            Assert.That(flags.HasAny(Flags256.Bit((bit + 1) % Flags256.Size)), Is.False);
            Assert.That((~flags).Has(bit), Is.False);
            Assert.That((~flags).NextBit(0), Is.EqualTo(bit == 0 ? 1 : 0));
        }

        [Test]
        public void SeveralFlags_HaveNoSingleBit_AndAreGoneThroughInOrder()
        {
            Flags256 flags = Flags256.Bit(3) | Flags256.Bit(70) | Flags256.Bit(250);
            Assert.That(flags.SingleBit, Is.EqualTo(-1));
            Assert.That(default(Flags256).SingleBit, Is.EqualTo(-1));
            var found = new System.Collections.Generic.List<int>();
            for (int bit = flags.NextBit(0); bit >= 0; bit = flags.NextBit(bit + 1)) { found.Add(bit); }
            Assert.That(found, Is.EqualTo(new[] { 3, 70, 250 }));
            Assert.That(flags.HasAll(Flags256.Bit(3) | Flags256.Bit(250)), Is.True);
            Assert.That(flags.HasAll(Flags256.Bit(3) | Flags256.Bit(4)), Is.False);
            Assert.That((flags & ~Flags256.Bit(70)).NextBit(4), Is.EqualTo(250));
        }

        [Test]
        public void Flags_AreWrittenAsHex_AndReadBack()
        {
            Flags256 flags = Flags256.Bit(0) | Flags256.Bit(35) | Flags256.Bit(255);
            Assert.That(Flags256.TryParseHex(flags.ToHex(), out Flags256 read), Is.True);
            Assert.That(read, Is.EqualTo(flags));
            Assert.That(default(Flags256).ToHex(), Is.EqualTo("0"));
            Assert.That((Flags256.Bit(0) | Flags256.Bit(1)).ToHex(), Is.EqualTo("3"), "Below 64 bits, it's the number the enum was, in hex.");
            Assert.That(Flags256.TryParseHex("not hex", out _), Is.False);
        }

        /// <summary>Plain data, to copy, compare and send: the flags, and the compact state that holds them, stay unmanaged and blittable.</summary>
        [Test]
        public void TheFlags_ArePlainData_ThirtyTwoBytes()
        {
            Assert.That(UnsafeUtility.IsBlittable<Flags256>(), Is.True);
            Assert.That(UnsafeUtility.IsBlittable<CharacterState>() && UnsafeUtility.IsBlittable<MovementEvents>(), Is.True);
            Assert.That(UnsafeUtility.IsUnmanaged<MovementStateSummary>(), Is.True, "The compact state stays unmanaged with its flags.");
            Assert.That(UnsafeUtility.SizeOf<MovementEvents>(), Is.EqualTo(Flags256.Size / 8));
        }

        [Test]
        public void Flags_AreSavedByUnity()
        {
            var trigger = new AnimatorParameterTranslator.EventTrigger(MovementEvents.Jumped | MovementEvents.Bit(200), "Jump");
            var read = JsonUtility.FromJson<AnimatorParameterTranslator.EventTrigger>(JsonUtility.ToJson(trigger));
            Assert.That(read.Events, Is.EqualTo(MovementEvents.Jumped | MovementEvents.Bit(200)));
        }

        [Test]
        public void FlagsAreNamed_ByTheirFields()
        {
            Assert.That((CharacterState.Grounded | CharacterState.Moving).ToString(), Is.EqualTo("Moving, Grounded"));
            Assert.That(CharacterState.None.ToString(), Is.EqualTo("None"));
            Assert.That(CharacterState.Bit(200).ToString(), Is.EqualTo("bit 200"), "A bit no flag has is named by its number.");
            Assert.That(MovementEvents.TryParse("WallJumped", out MovementEvents parsed) && parsed == MovementEvents.WallJumped, Is.True);
            Assert.That(MovementEvents.TryParse("None", out _), Is.False, "None isn't a flag.");
            Assert.That(MovementEvents.NameOf(MovementEvents.LandedBit), Is.EqualTo("Landed"));
            Assert.That(CharacterState.Named.Select(n => n.flag.SingleBit), Is.Ordered, "Lowest bit first.");
        }

        /// <summary>The bits the flags had as enums (Moving = 1, Jumped = 1, Landed = 4, RopeGrabbed = 1L &lt;&lt; 31): kept, so what was saved by them still reads the same.</summary>
        [Test]
        public void EachFlag_IsTheBitItWasAsAnEnum()
        {
            Assert.That(CharacterState.MovingBit, Is.EqualTo(0));
            Assert.That(CharacterState.DivingBit, Is.EqualTo(21));
            Assert.That(MovementEvents.LandedBit, Is.EqualTo(2));
            Assert.That(MovementEvents.RopeGrabbedBit, Is.EqualTo(31));
            Assert.That(MovementEvents.DiveEndedBit, Is.EqualTo(41));
            // The package's are named bare; a project's own ([MovementFlags]) are [Class].[Field], from its bits on.
            Assert.That(CharacterState.Named.Where(n => !n.name.Contains(".")).All(n => n.flag.SingleBit < Flags256.PackageBits)
                        && MovementEvents.Named.Where(n => !n.name.Contains(".")).All(n => n.flag.SingleBit < Flags256.PackageBits), Is.True,
                "The package's flags are below its bits' end: the rest are a project's own.");
        }

        [Test]
        public void AProjectsOwnFlags_AreNamedByTheirClass_OnTheirOwnBits()
        {
            // Test assemblies aren't looked in for the project's names, so this one's class is given to the finder.
            LogAssert.Expect(LogType.Warning, new Regex("FlagsTestsProjectFlags.Misplaced is on bit 127, one of the package's"));
            (string[] byBit, var named) = FlagNames.Find<CharacterState>(flag => flag.SingleBit, new[] { typeof(FlagsTestsProjectFlags) });
            Assert.That(byBit[FlagsTestsProjectFlags.StateBit], Is.EqualTo("FlagsTestsProjectFlags.State"), "A [MovementFlags] class's flags should be named [Class].[Field].");
            Assert.That(byBit[CharacterState.GroundedBit], Is.EqualTo(nameof(CharacterState.Grounded)), "The package's keep their bare names.");
            Assert.That(named.Any(n => n.name == "FlagsTestsProjectFlags.Misplaced"), Is.False, "A project's flag on a bit of the package's should be left out.");
            Assert.That(named.Select(n => n.flag.SingleBit), Is.Ordered);

            (string[] events, _) = FlagNames.Find<MovementEvents>(flag => flag.SingleBit, new[] { typeof(FlagsTestsProjectFlags) });
            Assert.That(events[FlagsTestsProjectFlags.EndedBit], Is.EqualTo("FlagsTestsProjectFlags.Ended"));
            Assert.That(CharacterState.TryParse("FlagsTestsProjectFlags.State", out _), Is.False, "A test's flags shouldn't be among the project's names.");
        }
    }

    /// <summary>A project's own flags, as New Ability writes them ([Name].Flags.cs), for FlagsTests.</summary>
    [MovementFlags]
    public static class FlagsTestsProjectFlags
    {
        public const int StateBit = 250;
        public static readonly CharacterState State = CharacterState.Bit(StateBit);
        public const int StartedBit = 250;
        public static readonly MovementEvents Started = MovementEvents.Bit(StartedBit);
        public const int EndedBit = 251;
        public static readonly MovementEvents Ended = MovementEvents.Bit(EndedBit);
        // On a bit of the package's: left out (with a warning).
        public static readonly CharacterState Misplaced = CharacterState.Bit(Flags256.PackageBits - 1);
    }
}
