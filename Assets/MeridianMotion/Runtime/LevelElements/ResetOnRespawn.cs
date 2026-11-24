using System.Collections.Generic;
using UnityEngine;

namespace MeridianMotion.Levels
{
    /// <summary>
    /// Puts a pushable crate back where it started, so it can't be left where it blocks the way: when it falls into a
    /// hazard (HazardVolume), and when a character is respawned at one of its checkpoints (its lesson's: Checkpoint Ids).
    /// Its start is where it was when it woke.
    /// </summary>
    [RequireComponent(typeof(PushableBlock))]
    [DisallowMultipleComponent]
    // After the crates' own steps (PushableBlock's FixedUpdate, at the default order) and the characters' (the tests' rig
    // steps its at 100), so a fall into a hazard is seen the step it happens.
    [DefaultExecutionOrder(ResetOrder)]
    [AddComponentMenu("Meridian Motion/Levels/Reset On Respawn")]
    public class ResetOnRespawn : MonoBehaviour
    {
        [Tooltip("The checkpoints (their ids) a respawn at which puts this crate back: those of the stretch it's in.")]
        [SerializeField] private List<string> checkpointIds = new();

        /// <summary>The script execution order it checks for hazards at: after the crates move themselves.</summary>
        public const int ResetOrder = 150;

        private PushableBlock block;
        private Vector3 startPosition;

        /// <summary>The checkpoints (their ids) a respawn at which puts the crate back.</summary>
        public List<string> CheckpointIds => checkpointIds;
        /// <summary>Where the crate goes back to.</summary>
        public Vector3 StartPosition => startPosition;

        private void Awake()
        {
            block = GetComponent<PushableBlock>();
            // Its Rigidbody's, not the block's own Position (set in its Awake, which may not have run yet).
            startPosition = GetComponent<Rigidbody>().position;
        }

        private void OnEnable() => Checkpoint.Respawned += OnRespawned;
        private void OnDisable() => Checkpoint.Respawned -= OnRespawned;

        private void OnRespawned(IRespawnable who, Checkpoint point)
        {
            if (point != null && checkpointIds.Contains(point.Id)) { ResetToStart(); }
        }

        /// <summary>Puts the crate back where it started, at once, letting go of it if something held it.</summary>
        public void ResetToStart()
        {
            if (block == null) { return; }
            if (block.IsHeld) { block.SetHolder(null); }
            block.Teleport(startPosition);
            // A move it made this step (a fall) would otherwise still be carried out at the physics step.
            GetComponent<Rigidbody>().MovePosition(startPosition);
        }

        private void FixedUpdate()
        {
            if (LevelVolume.At<HazardVolume>(block.Position) != null) { ResetToStart(); }
        }
    }
}
