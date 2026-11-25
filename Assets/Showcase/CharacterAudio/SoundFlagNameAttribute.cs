using UnityEngine;

namespace MeridianMotion.Showcase.CharacterAudio
{
    /// <summary>Which flags a Sound Set row's name is one of.</summary>
    public enum SoundFlagKind
    {
        /// <summary>A movement event (MovementEvents): Landed, Jumped.</summary>
        Event,
        /// <summary>A character state (CharacterState): Sliding, Ziplining.</summary>
        State,
    }

    /// <summary>
    /// On a Sound Set row's flag name: its inspector is a list of the flags of that kind (the package's and the project's
    /// own), so a name can't be mistyped.
    /// </summary>
    public sealed class SoundFlagNameAttribute : PropertyAttribute
    {
        /// <summary>Which flags it's one of.</summary>
        public readonly SoundFlagKind kind;

        public SoundFlagNameAttribute(SoundFlagKind kind) => this.kind = kind;
    }
}
