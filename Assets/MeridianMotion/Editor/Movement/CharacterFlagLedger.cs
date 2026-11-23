using System;
using System.Collections.Generic;
using MeridianMotion.Characters;
using UnityEngine;

namespace MeridianMotion.EditorTools
{
    /// <summary>
    /// The project's ledger of its own flags' bits (Character Flags.json, beside the abilities New Ability makes in the
    /// project): every CharacterState and MovementEvents bit it has given out, by name. New Ability gives an ability's
    /// flags the next bits past every one in it (and every one the project's code declares, [MovementFlags]), and never
    /// takes one out, so an ability deleted later doesn't free its bits for the next: a bit is saved data (in
    /// recordings, and an Animator Parameter Translator's triggers), and a trigger saved for the old ability would fire on
    /// the new one's event. Plain JSON, so it diffs and merges as text.
    /// </summary>
    [Serializable]
    public sealed class CharacterFlagLedger
    {
        /// <summary>A flag given out: its name ([Class].[Field]) and its bit.</summary>
        [Serializable]
        public struct Entry
        {
            public string name;
            public int bit;
        }

        public string note = "The bits New Ability has given this project's own flags (see MeridianMotion.Characters.MovementFlagsAttribute). " +
                             "Never remove or renumber one: a bit is saved data, so New Ability doesn't give one out twice.";
        public List<Entry> states = new();
        public List<Entry> events = new();

        /// <summary>The ledger in this text (an empty one for null or empty). Throws ArgumentException for text that isn't one.</summary>
        public static CharacterFlagLedger Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) { return new CharacterFlagLedger(); }
            CharacterFlagLedger ledger;
            try { ledger = JsonUtility.FromJson<CharacterFlagLedger>(json); }
            catch (Exception exception) { throw new ArgumentException($"It isn't a flag ledger: {exception.Message}", exception); }
            if (ledger == null) { throw new ArgumentException("It isn't a flag ledger."); }
            ledger.states ??= new List<Entry>();
            ledger.events ??= new List<Entry>();
            return ledger;
        }

        /// <summary>The ledger as text, to save.</summary>
        public string ToJson() => JsonUtility.ToJson(this, prettyPrint: true).Replace("\r\n", "\n") + "\n";

        /// <summary>
        /// The next bit for a project's state: past every state bit in the ledger and every one the project's code
        /// declares, and never below Flags256.UserFirstBit. Flags256.Size if none is left.
        /// </summary>
        public int NextStateBit() => Next(states, HighestProjectBit(CharacterState.Named, flag => flag.SingleBit));

        /// <summary>The next bit for a project's movement event, as NextStateBit.</summary>
        public int NextEventBit() => Next(events, HighestProjectBit(MovementEvents.Named, flag => flag.SingleBit));

        /// <summary>Records a state and the bit given to it.</summary>
        public void AddState(string name, int bit) => states.Add(new Entry { name = name, bit = bit });

        /// <summary>Records a movement event and the bit given to it.</summary>
        public void AddEvent(string name, int bit) => events.Add(new Entry { name = name, bit = bit });

        private static int Next(List<Entry> given, int declared)
        {
            int highest = Math.Max(declared, Flags256.UserFirstBit - 1);
            foreach (Entry entry in given) { highest = Math.Max(highest, entry.bit); }
            return Math.Min(highest + 1, Flags256.Size);
        }

        private static int HighestProjectBit<T>(IReadOnlyList<(string name, T flag)> named, Func<T, int> bitOf)
        {
            int highest = -1;
            // A project's own are named [Class].[Field]; the package's are bare.
            foreach ((string name, T flag) in named)
            {
                if (name.Contains(".")) { highest = Math.Max(highest, bitOf(flag)); }
            }
            return highest;
        }
    }
}
