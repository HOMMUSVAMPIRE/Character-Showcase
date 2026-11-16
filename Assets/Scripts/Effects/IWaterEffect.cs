using Level;
using UnityEngine;

namespace Character
{
    /// <summary>
    /// A water effect of one's own (a splash, a ripple): a component on the root of an effect prefab given to
    /// WaterEffects, which it plays instead of the prefab's particle systems. Effects are reused (see WaterEffects), so
    /// Play may be called again on one that has finished, and should start it afresh.
    /// </summary>
    public interface IWaterEffect
    {
        /// <summary>Plays the effect where and as the context says (its object is already moved to the context's position).</summary>
        void Play(WaterEffectContext context);

        /// <summary>Whether the effect is still playing (it isn't reused until it's done).</summary>
        bool IsPlaying { get; }
    }

    /// <summary>Where and how a water effect happens, for the effect to play (see IWaterEffect) and for WaterEffects' events.</summary>
    public struct WaterEffectContext
    {
        /// <summary>The point on the water's surface where it happens.</summary>
        public Vector3 position;
        /// <summary>The water it happens on.</summary>
        public Water water;
        /// <summary>How strong it is, 0 to 1 (a splash: by how fast the character went into or out of the water).</summary>
        public float strength;
        /// <summary>The character's size (its height relative to as authored, 1 as authored), which effects scale with.</summary>
        public float size;
        /// <summary>The character's velocity (m/s) when it happened.</summary>
        public Vector3 velocity;
    }
}
