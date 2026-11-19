using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace MeridianMotion.Characters
{
    /// <summary>
    /// How MovementEvents works as a set of flags (its flags themselves are in MovementEvents.cs): a Flags256 of its own type, so it can't be
    /// mixed with another kind of flag. Combine with | and ~, test with HasAny (a flag set: HasAny(T.X)) or
    /// HasAll, and switch on one flag's bit: switch (value.SingleBit) { case MovementEvents.JumpedBit: … }. Its names (for
    /// ToString, TryParse and the inspectors) are its flags' field names, found once by reflection.
    /// </summary>
    [Serializable]
    public partial struct MovementEvents : IEquatable<MovementEvents>
    {
        [SerializeField] private Flags256 bits;

        private MovementEvents(Flags256 bits) => this.bits = bits;

        /// <summary>The flag of one bit (0 to Flags256.Size - 1: the package's below Flags256.PackageBits).</summary>
        public static MovementEvents Bit(int index) => new(Flags256.Bit(index));

        /// <summary>The bits, as plain data.</summary>
        public Flags256 Bits => bits;

        /// <summary>The flags of these bits (read back from Bits, or from saved text: Flags256.TryParseHex).</summary>
        public static MovementEvents FromBits(Flags256 bits) => new(bits);

        /// <summary>Whether none of its flags is set (it's None).</summary>
        public bool IsEmpty => bits.IsEmpty;

        /// <summary>Whether any of these flags is set.</summary>
        public bool HasAny(MovementEvents flags) => bits.HasAny(flags.bits);

        /// <summary>Whether all of these flags are set.</summary>
        public bool HasAll(MovementEvents flags) => bits.HasAll(flags.bits);

        /// <summary>Whether this bit is set.</summary>
        public bool Has(int bit) => bits.Has(bit);

        /// <summary>The bit of the one flag set (for a switch on it), or -1 if none is, or several are.</summary>
        public int SingleBit => bits.SingleBit;

        /// <summary>The first bit set at or after from, or -1: to go through the flags set without allocating.</summary>
        public int NextBit(int from) => bits.NextBit(from);

        public static MovementEvents operator |(MovementEvents a, MovementEvents b) => new(a.bits | b.bits);
        public static MovementEvents operator &(MovementEvents a, MovementEvents b) => new(a.bits & b.bits);
        public static MovementEvents operator ^(MovementEvents a, MovementEvents b) => new(a.bits ^ b.bits);
        public static MovementEvents operator ~(MovementEvents a) => new(~a.bits);
        public static bool operator ==(MovementEvents a, MovementEvents b) => a.bits == b.bits;
        public static bool operator !=(MovementEvents a, MovementEvents b) => a.bits != b.bits;

        public bool Equals(MovementEvents other) => bits == other.bits;
        public override bool Equals(object obj) => obj is MovementEvents other && bits == other.bits;
        public override int GetHashCode() => bits.GetHashCode();

        /// <summary>The names of the flags set, lowest bit first ("Grounded, Moving"), "None" if none is, an unnamed bit by its number.</summary>
        public override string ToString()
        {
            if (IsEmpty) { return "None"; }
            var text = new StringBuilder();
            for (int bit = NextBit(0); bit >= 0; bit = NextBit(bit + 1))
            {
                if (text.Length > 0) { text.Append(", "); }
                text.Append(NameOf(bit) ?? $"bit {bit}");
            }
            return text.ToString();
        }

        /// <summary>The flag of this name (a field's name, Grounded), or false if there's none.</summary>
        public static bool TryParse(string name, out MovementEvents flag)
        {
            foreach ((string each, MovementEvents value) in Named)
            {
                if (each == name) { flag = value; return true; }
            }
            flag = default;
            return false;
        }

        /// <summary>The name of the flag of this bit, or null if no flag has it.</summary>
        public static string NameOf(int bit) => (uint)bit < Flags256.Size ? Names()[bit] : null;

        /// <summary>Every named flag, lowest bit first.</summary>
        public static IReadOnlyList<(string name, MovementEvents flag)> Named
        {
            get
            {
                Names();
                return named;
            }
        }

        private static string[] names;
        private static List<(string name, MovementEvents flag)> named;

        private static string[] Names()
        {
            if (names != null) { return names; }
            var byBit = new string[Flags256.Size];
            var list = new List<(string, MovementEvents)>();
            foreach (FieldInfo field in typeof(MovementEvents).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (field.FieldType != typeof(MovementEvents) || !field.IsInitOnly) { continue; }
                var value = (MovementEvents)field.GetValue(null);
                int bit = value.SingleBit;
                if (bit < 0 || byBit[bit] != null) { continue; }
                byBit[bit] = field.Name;
                list.Add((field.Name, value));
            }
            list.Sort((a, b) => a.Item2.SingleBit.CompareTo(b.Item2.SingleBit));
            named = list;
            return names = byBit;
        }
    }
}
