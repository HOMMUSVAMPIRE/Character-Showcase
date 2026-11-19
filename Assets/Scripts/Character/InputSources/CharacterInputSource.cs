using UnityEngine;

namespace MeridianMotion.Characters
{
    /// <summary>
    /// What drives a character's input: gives its input frame for each physics step (see CharacterInputFrame). The
    /// character's MMotionCharacter asks its source once a step. The player's device (PlayerInputSource), a script
    /// or recording (ScriptedInputSource), an AI (WaypointInputSource), or, in a networked game, the frames another
    /// machine sent: the character moves the same whatever its frames came from.
    /// </summary>
    public interface ICharacterInputSource
    {
        /// <summary>The frame for the character's step tick (its MMotionMotor.Tick), given the character as it is.</summary>
        CharacterInputFrame FrameFor(uint tick, MMotionMotor character);
    }

    /// <summary>
    /// An input source that's a component, to put on a character (its MMotionCharacter uses the one on it, or the
    /// one it's given: see MMotionCharacter.InputSource).
    /// </summary>
    public abstract class CharacterInputSource : MonoBehaviour, ICharacterInputSource
    {
        public abstract CharacterInputFrame FrameFor(uint tick, MMotionMotor character);
    }

    /// <summary>
    /// The player's device, as the scene's PlayerInputReader latches it (see InputLatch): what drives the local player's
    /// character when it's given no other source. An idle frame without an input manager.
    /// </summary>
    public sealed class PlayerInputSource : ICharacterInputSource
    {
        /// <summary>The one there is (it holds nothing of its own: the input manager does).</summary>
        public static readonly PlayerInputSource Instance = new();

        private PlayerInputSource() { }

        public CharacterInputFrame FrameFor(uint tick, MMotionMotor character) =>
            PlayerInputReader.instance != null ? PlayerInputReader.instance.FrameFor(tick) : new CharacterInputFrame { tick = tick };
    }
}
