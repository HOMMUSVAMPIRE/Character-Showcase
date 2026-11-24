using System;
using UnityEngine;

namespace MeridianMotion.Levels
{
    /// <summary>
    /// A checkpoint: a volume that, when a character's feet come into it, becomes the place it's respawned at (by a
    /// hazard, or LevelPresence.RespawnAtCheckpoint): its feet at the checkpoint's position (its object's), facing its
    /// way (its forward, level). The one reached last wins, so going back through an earlier one makes it the
    /// respawn point again. A start checkpoint (Is Start) is where a character goes before it has reached any.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Meridian Motion/Levels/Checkpoint")]
    public class Checkpoint : LevelVolume
    {
        [Tooltip("A name for the checkpoint, unique in the level: what crates that reset with it (Reset On Respawn) and play logs name it by.")]
        [SerializeField] private string id = "";
        [Tooltip("Whether this is where a character respawns before it has reached any checkpoint (one per level).")]
        [SerializeField] private bool isStart;

        /// <summary>Raised when a character reaches a checkpoint other than the one it last reached.</summary>
        public static event Action<IRespawnable, Checkpoint> Reached;
        /// <summary>Raised when a character has been respawned at a checkpoint.</summary>
        public static event Action<IRespawnable, Checkpoint> Respawned;

        /// <summary>The checkpoint's name, unique in the level.</summary>
        public string Id { get => id; set => id = value ?? ""; }
        /// <summary>Whether this is where a character respawns before it has reached any checkpoint.</summary>
        public bool IsStart { get => isStart; set => isStart = value; }
        /// <summary>Where a character respawned here puts its feet: the checkpoint's position.</summary>
        public Vector3 Feet => transform.position;
        /// <summary>The way a character respawned here faces: the checkpoint's forward, level.</summary>
        public Quaternion Facing
        {
            get
            {
                Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
                return forward.sqrMagnitude < 1e-6f ? Quaternion.identity : Quaternion.LookRotation(forward.normalized, Vector3.up);
            }
        }

        /// <summary>The level's start checkpoint (the first enabled with Is Start, in StableId order), or null.</summary>
        public static Checkpoint Start
        {
            get
            {
                foreach (LevelVolume volume in All) { if (volume is Checkpoint { isStart: true } start) { return start; } }
                return null;
            }
        }

        /// <summary>The enabled checkpoint with this id, or null.</summary>
        public static Checkpoint Find(string checkpointId)
        {
            foreach (LevelVolume volume in All) { if (volume is Checkpoint checkpoint && checkpoint.id == checkpointId) { return checkpoint; } }
            return null;
        }

        public override void Entered(IRespawnable who, LevelPresence presence)
        {
            if (presence.active == this) { return; }
            presence.active = this;
            Reached?.Invoke(who, this);
        }

        internal static void RaiseRespawned(IRespawnable who, Checkpoint point) => Respawned?.Invoke(who, point);

        protected override Color GizmoColor => isStart ? new Color(0.3f, 1f, 0.4f) : new Color(0.3f, 0.8f, 1f);
    }
}
