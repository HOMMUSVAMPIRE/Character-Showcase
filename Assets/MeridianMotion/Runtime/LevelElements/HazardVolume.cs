using UnityEngine;

namespace MeridianMotion.Levels
{
    /// <summary>
    /// A hazard: a character whose feet come into it is respawned at its checkpoint (LevelPresence.RespawnPoint), once its
    /// step's volumes are done; a crate that resets (ResetOnRespawn) falling into it goes back where it started. For
    /// pits, and a catch-all under a level.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Meridian Motion/Levels/Hazard Volume")]
    public class HazardVolume : LevelVolume
    {
        public override void Entered(IRespawnable who, LevelPresence presence) => presence.respawnWanted = true;

        protected override Color GizmoColor => new(1f, 0.3f, 0.2f);
    }
}
