using System;
using System.Globalization;
using UnityEngine;

namespace MeridianMotion.Characters
{
    /// <summary>
    /// 256 flags in four 64-bit words: the bits CharacterState and MovementEvents are made of. Blittable (four ulongs and
    /// nothing else), so it's copied, compared and sent as plain data, and saved by Unity (which drops enum fields wider
    /// than 32 bits, the reason these aren't enums). Bits 0 to PackageBits - 1 are the package's own flags; the rest are
    /// for a project's own abilities (UserFirstBit on), so a package upgrade adding flags can't take one a project uses.
    /// The width lives here alone: a project wanting more (or fewer) flags changes this struct and nothing else.
    /// </summary>
    [Serializable]
    public struct Flags256 : IEquatable<Flags256>
    {
        /// <summary>How many flags there are.</summary>
        public const int Size = 256;
        /// <summary>How many are the package's own (bits 0 to PackageBits - 1).</summary>
        public const int PackageBits = 128;
        /// <summary>The first of a project's own flags' bits (the bits from here to Size - 1).</summary>
        public const int UserFirstBit = PackageBits;

        [SerializeField] private ulong w0;
        [SerializeField] private ulong w1;
        [SerializeField] private ulong w2;
        [SerializeField] private ulong w3;

        private Flags256(ulong w0, ulong w1, ulong w2, ulong w3)
        {
            this.w0 = w0;
            this.w1 = w1;
            this.w2 = w2;
            this.w3 = w3;
        }

        /// <summary>The flag of one bit, 0 to Size - 1.</summary>
        public static Flags256 Bit(int index)
        {
            if ((uint)index >= Size) { throw new ArgumentOutOfRangeException(nameof(index), index, $"A flag's bit is 0 to {Size - 1}."); }
            ulong word = 1UL << (index & 63);
            return (index >> 6) switch
            {
                0 => new Flags256(word, 0, 0, 0),
                1 => new Flags256(0, word, 0, 0),
                2 => new Flags256(0, 0, word, 0),
                _ => new Flags256(0, 0, 0, word),
            };
        }

        /// <summary>Whether no flag is set.</summary>
        public bool IsEmpty => (w0 | w1 | w2 | w3) == 0;

        /// <summary>Whether this bit is set.</summary>
        public bool Has(int index) => (uint)index < Size && (Word(index >> 6) & (1UL << (index & 63))) != 0;

        /// <summary>Whether any of the flags in other is set.</summary>
        public bool HasAny(Flags256 other) => ((w0 & other.w0) | (w1 & other.w1) | (w2 & other.w2) | (w3 & other.w3)) != 0;

        /// <summary>Whether every flag in other is set.</summary>
        public bool HasAll(Flags256 other) => (this & other) == other;

        /// <summary>
        /// The bit of the one flag set, for a switch on it (switch (events.SingleBit) { case MovementEvents.JumpedBit: … });
        /// -1 if none is set, or several are.
        /// </summary>
        public int SingleBit
        {
            get
            {
                int found = -1;
                for (int word = 0; word < 4; word++)
                {
                    ulong bits = Word(word);
                    if (bits == 0) { continue; }
                    if (found >= 0 || (bits & (bits - 1)) != 0) { return -1; }
                    found = word * 64 + TrailingZeros(bits);
                }
                return found;
            }
        }

        /// <summary>
        /// The first bit set at or after from, or -1 if there's none: to go through the flags set without allocating
        /// (for (int bit = flags.NextBit(0); bit >= 0; bit = flags.NextBit(bit + 1)) …).
        /// </summary>
        public int NextBit(int from)
        {
            if (from < 0) { from = 0; }
            for (int word = from >> 6; word < 4; word++)
            {
                ulong bits = Word(word);
                if (word == from >> 6) { bits &= ~0UL << (from & 63); }
                if (bits != 0) { return word * 64 + TrailingZeros(bits); }
            }
            return -1;
        }

        /// <summary>The flags as hexadecimal, the highest bit first and without leading zeros ("0" when empty): for saving as text.</summary>
        public string ToHex()
        {
            if (IsEmpty) { return "0"; }
            string full = w3.ToString("x16") + w2.ToString("x16") + w1.ToString("x16") + w0.ToString("x16");
            return full.TrimStart('0');
        }

        /// <summary>Reads flags written by ToHex (up to 64 hex digits). False if text isn't one.</summary>
        public static bool TryParseHex(string text, out Flags256 flags)
        {
            flags = default;
            if (string.IsNullOrEmpty(text) || text.Length > 64) { return false; }
            text = text.PadLeft(64, '0');
            var words = new ulong[4];
            for (int i = 0; i < 4; i++)
            {
                if (!ulong.TryParse(text.Substring(i * 16, 16), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out words[3 - i])) { return false; }
            }
            flags = new Flags256(words[0], words[1], words[2], words[3]);
            return true;
        }

        public static Flags256 operator |(Flags256 a, Flags256 b) => new(a.w0 | b.w0, a.w1 | b.w1, a.w2 | b.w2, a.w3 | b.w3);
        public static Flags256 operator &(Flags256 a, Flags256 b) => new(a.w0 & b.w0, a.w1 & b.w1, a.w2 & b.w2, a.w3 & b.w3);
        public static Flags256 operator ^(Flags256 a, Flags256 b) => new(a.w0 ^ b.w0, a.w1 ^ b.w1, a.w2 ^ b.w2, a.w3 ^ b.w3);
        public static Flags256 operator ~(Flags256 a) => new(~a.w0, ~a.w1, ~a.w2, ~a.w3);
        public static bool operator ==(Flags256 a, Flags256 b) => a.w0 == b.w0 && a.w1 == b.w1 && a.w2 == b.w2 && a.w3 == b.w3;
        public static bool operator !=(Flags256 a, Flags256 b) => !(a == b);

        public bool Equals(Flags256 other) => this == other;
        public override bool Equals(object obj) => obj is Flags256 other && this == other;
        public override int GetHashCode() => (w0 ^ (w1 * 31) ^ (w2 * 961) ^ (w3 * 29791)).GetHashCode();
        public override string ToString() => ToHex();

        private ulong Word(int index) => index switch { 0 => w0, 1 => w1, 2 => w2, _ => w3 };

        private static int TrailingZeros(ulong bits)
        {
            int count = 0;
            while ((bits & 1) == 0)
            {
                bits >>= 1;
                count++;
            }
            return count;
        }
    }
}
