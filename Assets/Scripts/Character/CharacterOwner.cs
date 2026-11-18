namespace Character
{
    /// <summary>
    /// Who a character is for: the player at this machine, an AI, or someone elsewhere. It decides what drives the
    /// character's input and whether the camera follows it (only the local player's: see CharacterSpawner).
    /// </summary>
    public enum CharacterOwner
    {
        /// <summary>The player at this machine: driven by their device (ACInputManager), followed by the camera.</summary>
        LocalPlayer,
        /// <summary>Driven by something other than a player (an NPC). Not followed by the camera.</summary>
        AI,
        /// <summary>Someone else's, driven from elsewhere (another player, over a network). Not followed by the camera.</summary>
        Remote,
    }
}
