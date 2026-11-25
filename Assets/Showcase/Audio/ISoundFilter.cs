namespace MeridianMotion.Showcase.Audio
{
    /// <summary>
    /// A rule over every sound the AudioService is asked to play, run before it plays (AudioService.AddFilter). It can
    /// say no (the sound isn't played) or change the request (its strength, its flags). The place for rules over all
    /// sounds: later, netcode's (no sounds while resimulating; another player's sounds in 3D), an option muting others.
    /// </summary>
    public interface ISoundFilter
    {
        /// <summary>Whether the request is played (it may be changed first).</summary>
        bool Allow(ref SoundRequest request);
    }
}
