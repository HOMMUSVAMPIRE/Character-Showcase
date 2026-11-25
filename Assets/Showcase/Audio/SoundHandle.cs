namespace MeridianMotion.Showcase.Audio
{
    /// <summary>
    /// A sound that's playing, as AudioService.Play returns it: for stopping a loop, moving it or changing its strength.
    /// It's a voice's index and the voice's generation when it was given: once the voice is freed or reused (stolen by a
    /// sound of higher priority) the generation has moved on, so an old handle does nothing rather than touching another
    /// sound. One-shots can ignore their handle.
    /// </summary>
    public readonly struct SoundHandle
    {
        internal readonly int voice;
        internal readonly int generation;

        internal SoundHandle(int voice, int generation)
        {
            this.voice = voice;
            this.generation = generation;
        }

        /// <summary>No sound (what Play returns when it played nothing).</summary>
        public static SoundHandle None => default;
        /// <summary>Whether this was given for a sound (it may since have ended: AudioService.IsPlaying says).</summary>
        public bool IsValid => generation != 0;
    }
}
