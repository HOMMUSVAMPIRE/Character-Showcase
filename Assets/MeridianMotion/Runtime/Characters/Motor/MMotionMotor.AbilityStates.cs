namespace MeridianMotion.Characters
{
    // The state of abilities made outside the package (see StateOf).
    public partial class MMotionMotor
    {
        // One object of each state type asked for, in the order they were first asked for. An array of objects, so saving
        // and putting back (ObjectGraphSnapshot) and writing to bytes (StateSerializer) carry each by its own type, as they
        // do any of the character's plain objects: the slots are found by type, not place, so the order needn't match.
        private object[] abilityStates;

        /// <summary>
        /// The character's state of type T: what an ability made outside the package (in a project's own assembly, which
        /// can't add to this partial class as the package's abilities do) keeps per character, since its movement mode is
        /// a shared asset. Made the first time it's asked for (once per character and type, not every step), kept for the
        /// character's life, and saved and put back with the rest of its movement (SaveState, StateSerializer). Each
        /// ability gives its own state class; two asking for the same type share one object.
        /// </summary>
        public T StateOf<T>() where T : class, new()
        {
            if (abilityStates != null)
            {
                for (int i = 0; i < abilityStates.Length; i++)
                {
                    if (abilityStates[i] is T found) { return found; }
                }
            }
            var made = new T();
            int count = abilityStates?.Length ?? 0;
            var grown = new object[count + 1];
            if (count > 0) { System.Array.Copy(abilityStates, grown, count); }
            grown[count] = made;
            abilityStates = grown;
            return made;
        }
    }
}
