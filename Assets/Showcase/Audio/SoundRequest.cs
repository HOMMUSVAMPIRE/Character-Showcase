using System;
using UnityEngine;

namespace MeridianMotion.Showcase.Audio
{
    /// <summary>
    /// A stable number for a SoundEvent, so a request names its sound by a plain value rather than an asset reference:
    /// what lets a request be sent over a network later. Given by the Sound Library, saved on the asset, never changed or
    /// reused. 0 is none.
    /// </summary>
    [Serializable]
    public readonly struct SoundId : IEquatable<SoundId>
    {
        /// <summary>The id's number (0: none).</summary>
        public readonly int Value;

        public SoundId(int value) => Value = value;

        /// <summary>No sound.</summary>
        public static SoundId None => default;
        /// <summary>Whether this is no sound.</summary>
        public bool IsNone => Value == 0;

        public bool Equals(SoundId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is SoundId other && Equals(other);
        public override int GetHashCode() => Value;
        public static bool operator ==(SoundId a, SoundId b) => a.Value == b.Value;
        public static bool operator !=(SoundId a, SoundId b) => a.Value != b.Value;
        public override string ToString() => $"Sound {Value}";
    }

    /// <summary>
    /// Who made a sound (a character, a level piece, the UI): so a rule can treat one emitter's sounds differently from
    /// another's (the local player's from others', later over a network), and the service can tell them apart. 0 is none.
    /// </summary>
    [Serializable]
    public readonly struct EmitterId : IEquatable<EmitterId>
    {
        /// <summary>The emitter's number (0: none).</summary>
        public readonly int Value;

        public EmitterId(int value) => Value = value;

        /// <summary>No emitter in particular.</summary>
        public static EmitterId None => default;

        private static int last;

        /// <summary>A new emitter's id, not given to any other in this run.</summary>
        public static EmitterId Next() => new(++last);

        public bool Equals(EmitterId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is EmitterId other && Equals(other);
        public override int GetHashCode() => Value;
        public static bool operator ==(EmitterId a, EmitterId b) => a.Value == b.Value;
        public static bool operator !=(EmitterId a, EmitterId b) => a.Value != b.Value;
    }

    /// <summary>How a sound is played, beyond what its Sound Event says.</summary>
    [Flags]
    public enum SoundFlags : byte
    {
        None = 0,
        /// <summary>The voice follows the position its handle is given each frame (SetPosition), rather than staying put.</summary>
        Follow = 1,
        /// <summary>Never sent to other machines (for later netcode: a sound only this machine should hear).</summary>
        LocalOnly = 2,
    }

    /// <summary>
    /// One sound to play: which, where, how strong, who made it. Everything played goes through this one plain value
    /// (AudioService.Play), so the rules over all sounds (the service's filters) and, later, sending a sound to other
    /// machines have one place to work on. Unmanaged: no references, nothing to allocate.
    /// </summary>
    [Serializable]
    public struct SoundRequest
    {
        /// <summary>The sound (its Sound Event's id).</summary>
        public SoundId sound;
        /// <summary>Where it's played (for a 2D sound, ignored).</summary>
        public Vector3 position;
        /// <summary>How strong, 0 to 1: scales its volume.</summary>
        public float strength;
        /// <summary>Multiplies its pitch (a small character's sounds are higher). 1: as the Sound Event says.</summary>
        public float pitchScale;
        /// <summary>Who made it.</summary>
        public EmitterId emitter;
        /// <summary>How it's played (follows a position; never sent).</summary>
        public SoundFlags flags;

        /// <summary>A request for this sound here, this strong, at its own pitch, from no emitter in particular.</summary>
        public static SoundRequest At(SoundId sound, Vector3 position, float strength = 1f) => new()
        {
            sound = sound,
            position = position,
            strength = strength,
            pitchScale = 1f,
        };
    }
}
