using UnityEngine;

namespace MeridianMotion.Levels
{
    /// <summary>
    /// Something the level can put back at a checkpoint (see Checkpoint, HazardVolume): a character, whose movement does
    /// it. The level elements know nothing of the character, so they respawn it through this.
    /// </summary>
    public interface IRespawnable
    {
        /// <summary>
        /// Puts it at feet, facing rotation, between physics steps, as if it had just arrived there standing still: no
        /// momentum, and nothing under way (holding on, riding, sliding) carried over.
        /// </summary>
        void Respawn(Vector3 feet, Quaternion rotation);

        /// <summary>Where its feet are now, in the physics pose (not an interpolated transform): what the level's volumes are checked against.</summary>
        Vector3 Feet { get; }
    }
}
