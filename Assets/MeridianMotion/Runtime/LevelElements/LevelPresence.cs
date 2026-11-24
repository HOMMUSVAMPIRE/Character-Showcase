using System;
using System.Collections.Generic;
using UnityEngine;

namespace MeridianMotion.Levels
{
    /// <summary>
    /// Where a character is in the level's volumes (see LevelVolume): those its feet are in, the checkpoint it last
    /// reached (Active) and how often it's been respawned. Plain data, kept on the character (MMotionMotor.levelPresence),
    /// so it's saved and put back with its movement state, and written with it (StateSerializer: the volumes by their
    /// StableIds), so a replay's keyframes restore it. AfterStep, called after each of the character's steps, keeps it up
    /// to date and does what the volumes entered do.
    /// </summary>
    public class LevelPresence
    {
        /// <summary>The volumes the character's feet are in, as of its last step (in the order it came into them).</summary>
        public readonly List<LevelVolume> inside = new();
        /// <summary>The checkpoint the character last reached (null before it reaches one: Checkpoint.Start is used then).</summary>
        public Checkpoint active;
        /// <summary>How many times the character has been respawned at a checkpoint.</summary>
        public int respawns;
        /// <summary>Whether a volume entered this step wants the character respawned (a hazard), once the step's volumes are done.</summary>
        [NonSerialized] public bool respawnWanted;

        /// <summary>Raised when a character's feet come into a volume (after its step).</summary>
        public static event Action<IRespawnable, LevelVolume> VolumeEntered;
        /// <summary>Raised when a character's feet leave a volume (after its step).</summary>
        public static event Action<IRespawnable, LevelVolume> VolumeExited;

        /// <summary>The checkpoint a respawn goes to: the last reached, else the level's start.</summary>
        public Checkpoint RespawnPoint => active != null && active.isActiveAndEnabled ? active : Checkpoint.Start;

        /// <summary>Whether the character's feet are in the volume, as of its last step.</summary>
        public bool IsIn(LevelVolume volume) => inside.Contains(volume);

        /// <summary>
        /// After one of the character's steps (between steps): which volumes its feet are in now. Those it left are told
        /// (Exited) first, then those it came into (Entered), in StableId order; then, if one of them asked (a hazard), the
        /// character is respawned. With no volumes in the level and none held, it does nothing (and allocates nothing).
        /// </summary>
        public static void AfterStep(IRespawnable who, LevelPresence presence)
        {
            if (who == null || presence == null) { return; }
            IReadOnlyList<LevelVolume> all = LevelVolume.All;
            if (all.Count == 0 && presence.inside.Count == 0) { return; }
            Vector3 feet = who.Feet;
            for (int i = presence.inside.Count - 1; i >= 0; i--)
            {
                LevelVolume volume = presence.inside[i];
                if (volume != null && volume.isActiveAndEnabled && volume.Contains(feet)) { continue; }
                presence.inside.RemoveAt(i);
                if (volume == null) { continue; }
                volume.Exited(who, presence);
                VolumeExited?.Invoke(who, volume);
            }
            for (int i = 0; i < all.Count; i++)
            {
                LevelVolume volume = all[i];
                if (presence.inside.Contains(volume) || !volume.Contains(feet)) { continue; }
                presence.inside.Add(volume);
                volume.Entered(who, presence);
                VolumeEntered?.Invoke(who, volume);
            }
            if (presence.respawnWanted)
            {
                presence.respawnWanted = false;
                presence.RespawnAtCheckpoint(who);
            }
        }

        /// <summary>
        /// Respawns the character at RespawnPoint (between steps), raising Checkpoint.Respawned. False, with a warning,
        /// if the level has no checkpoint to go to.
        /// </summary>
        public bool RespawnAtCheckpoint(IRespawnable who)
        {
            Checkpoint point = RespawnPoint;
            if (point == null)
            {
                Debug.LogWarning("[LevelPresence] There's no checkpoint to respawn at: give the level a start checkpoint.");
                return false;
            }
            who.Respawn(point.Feet, point.Facing);
            respawns++;
            Checkpoint.RaiseRespawned(who, point);
            return true;
        }
    }
}
