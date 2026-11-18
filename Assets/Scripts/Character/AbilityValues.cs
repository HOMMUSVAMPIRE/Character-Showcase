using System;

namespace Character
{
    /// <summary>
    /// Named values an ability publishes for its look, with no animation code: a component sets them during its step
    /// (`controller.AbilityValues.Set(id, value)`), declares their names (MovementComponent.AbilityValueNames), and an
    /// Animator Parameter Translator's float parameter shows one (Shows > Ability Value > its name). The movement never
    /// reads them back: they're what it shows, not what it does, so they don't change how the character moves.
    ///
    /// A value is kept by the hash of its name (AbilityValueId), not an index handed out as names turn up, so it's the
    /// same in every process and a character's saved or sent state (which carries this table) means the same wherever
    /// it's read. Up to Capacity values a character, held in a fixed table, set and read without allocating. A value keeps
    /// its last setting until it's set again.
    /// </summary>
    [Serializable]
    public sealed class AbilityValues
    {
        /// <summary>How many values a character can hold.</summary>
        public const int Capacity = 32;

        // Open addressing by the name's hash: a slot is empty while its key is 0.
        private readonly long[] keys = new long[Capacity];
        private readonly float[] values = new float[Capacity];

        /// <summary>Sets the value of the given name.</summary>
        public void Set(AbilityValueId id, float value)
        {
            int slot = Find(id.hash, true);
            if (slot < 0) { throw new InvalidOperationException($"A character holds at most {Capacity} ability values."); }
            keys[slot] = id.hash;
            values[slot] = value;
        }

        /// <summary>The value of the given name, if it's been set.</summary>
        public bool TryGet(AbilityValueId id, out float value)
        {
            int slot = Find(id.hash, false);
            value = slot >= 0 ? values[slot] : 0f;
            return slot >= 0;
        }

        // The slot holding hash, or (for adding) the first empty one on its probe; -1 if neither.
        private int Find(long hash, bool orEmpty)
        {
            int start = (int)((ulong)hash % Capacity);
            for (int i = 0; i < Capacity; i++)
            {
                int slot = (start + i) % Capacity;
                if (keys[slot] == hash) { return slot; }
                if (keys[slot] == 0) { return orEmpty ? slot : -1; }
            }
            return -1;
        }
    }

    /// <summary>An ability value's name, kept as its hash (64-bit FNV-1a), found once from the name (see AbilityValues).</summary>
    public readonly struct AbilityValueId
    {
        public readonly long hash;
        /// <summary>The name (kept for showing; not compared).</summary>
        public readonly string name;

        public AbilityValueId(string name)
        {
            this.name = name ?? "";
            ulong h = 14695981039346656037UL;
            foreach (char c in this.name)
            {
                h ^= c;
                h *= 1099511628211UL;
            }
            hash = (long)(h == 0 ? 1 : h); // 0 marks an empty slot.
        }
    }

    // The values' place on the character (see MovementController): what its abilities publish for their look.
    public partial class MovementController
    {
        /// <summary>The values the character's abilities publish for their look (see AbilityValues).</summary>
        public AbilityValues AbilityValues { get; } = new AbilityValues();
    }
}
