using System.Collections;
using System.Collections.Generic;
using MeridianMotion.Characters;
using MeridianMotion.Levels;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MeridianMotion.Tests.Levels
{
    /// <summary>
    /// Tests of checkpoints, hazards and crates that reset (Checkpoint, HazardVolume, ResetOnRespawn), with the character
    /// walking through them: the checkpoint reached last is the one respawned at (going back through an earlier one makes
    /// it the one again), a hazard before any checkpoint respawns at the start, a crate that falls into a hazard or whose
    /// checkpoint a respawn goes to goes back where it started (and one elsewhere stays put), the respawn point is kept
    /// with the character's saved state, and a character with no model is respawned all the same.
    /// </summary>
    [Category(TestArea.Levels)]
    public class CheckpointTests : CharacterRigFixture
    {
        private static readonly Vector3 VolumeSize = new(4f, 3f, 2f);
        private readonly List<Checkpoint> respawnedAt = new();

        [SetUp]
        public void Listen() => Checkpoint.Respawned += Heard;

        [TearDown]
        public void StopListening() => Checkpoint.Respawned -= Heard;

        private void Heard(IRespawnable who, Checkpoint point) => respawnedAt.Add(point);

        private LevelPresence Presence => rig.Movement.levelPresence;

        /// <summary>A floor, the start checkpoint at the origin, and the character standing at z, facing +Z.</summary>
        private IEnumerator SetUpCourse(float z = 0f)
        {
            respawnedAt.Clear();
            rig.Floor();
            rig.Checkpoint("start", Vector3.zero, VolumeSize, isStart: true);
            yield return rig.SpawnSettled(new Vector3(0f, 0.05f, z));
        }

        private IEnumerator WalkTo(float z, float timeout = 6f) =>
            rig.RunUntil(() => rig.Position.z >= z, _ => CharacterTestRig.Move(Vector3.forward), timeoutSeconds: timeout);

        /// <summary>Walks forward until the character's been respawned (or the timeout), then stands a moment.</summary>
        private IEnumerator WalkUntilRespawned(float timeout = 8f)
        {
            yield return rig.RunUntil(() => respawnedAt.Count > 0, _ => CharacterTestRig.Move(Vector3.forward), timeoutSeconds: timeout);
            yield return rig.Run(CharacterTestRig.StepsFor(0.3f), _ => CharacterTestRig.Idle());
        }

        private IEnumerator WalkBackTo(float z, float timeout = 6f) =>
            rig.RunUntil(() => rig.Position.z <= z, _ => CharacterTestRig.Move(Vector3.back), timeoutSeconds: timeout);

        [UnityTest]
        public IEnumerator PassingACheckpoint_MakesItTheOneRespawnedAt()
        {
            yield return SetUpCourse(-4f);
            Checkpoint second = rig.Checkpoint("second", new Vector3(0f, 0f, 6f), VolumeSize);
            yield return WalkTo(6.5f);
            Assert.That(Presence.active, Is.SameAs(second), "The checkpoint walked through should be the respawn point.");
            Assert.That(Presence.RespawnPoint, Is.SameAs(second));
        }

        [UnityTest]
        public IEnumerator PassingALaterCheckpoint_ThenAnEarlierOne_MakesTheEarlierTheOne()
        {
            yield return SetUpCourse(-4f);
            Checkpoint first = rig.Checkpoint("first", new Vector3(0f, 0f, 4f), VolumeSize);
            Checkpoint second = rig.Checkpoint("second", new Vector3(0f, 0f, 9f), VolumeSize);
            yield return WalkTo(9.5f);
            Assume.That(Presence.active, Is.SameAs(second));
            yield return WalkBackTo(4f);
            Assert.That(Presence.active, Is.SameAs(first), "The checkpoint reached last should win.");
        }

        [UnityTest]
        public IEnumerator EnteringAHazard_RespawnsAtTheCheckpointReached()
        {
            yield return SetUpCourse(-4f);
            Checkpoint second = rig.Checkpoint("second", new Vector3(0f, 0f, 4f), VolumeSize, yaw: 180f);
            rig.Hazard(new Vector3(0f, 1f, 12f), new Vector3(6f, 3f, 2f));
            yield return WalkUntilRespawned();
            Assert.That(respawnedAt, Is.EqualTo(new[] { second }), "Walking into the hazard should respawn the character once, at the checkpoint it reached.");
            Assert.That(Vector3.Distance(rig.Position, second.Feet), Is.LessThan(0.3f), $"The character should be at the checkpoint (it's at {rig.Position}).");
            Assert.That(Quaternion.Angle(rig.Body.rotation, second.Facing), Is.LessThan(1f), "It should face the checkpoint's way.");
            Assert.That(Presence.respawns, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator EnteringAHazard_WithNoCheckpointReached_RespawnsAtTheStart()
        {
            respawnedAt.Clear();
            rig.Floor();
            Checkpoint start = rig.Checkpoint("start", new Vector3(0f, 0f, -10f), VolumeSize, isStart: true);
            rig.Hazard(new Vector3(0f, 1f, 3f), new Vector3(6f, 3f, 2f));
            yield return rig.SpawnSettled(new Vector3(0f, 0.05f, 0f));
            Assume.That(Presence.active, Is.Null, "Test setup: the character shouldn't have reached a checkpoint.");
            yield return WalkUntilRespawned();
            Assert.That(respawnedAt, Is.EqualTo(new[] { start }), "Before reaching a checkpoint, a hazard should respawn the character at the start.");
        }

        [UnityTest]
        public IEnumerator ACrateFallingIntoAHazard_GoesBackToItsStart()
        {
            yield return SetUpCourse();
            // Off the floor (it's 100 m wide, round the origin), with a hazard below: nothing under the crate but the hazard.
            rig.Hazard(new Vector3(80f, -5f, 0f), new Vector3(10f, 4f, 10f));
            PushableBlock crate = rig.PushableBox(new Vector3(80f, 3f, 0f), Vector3.one);
            var reset = crate.gameObject.AddComponent<ResetOnRespawn>();
            Vector3 start = crate.Position;
            float lowest = start.y;
            bool backUp = false;
            yield return rig.Run(CharacterTestRig.StepsFor(2f), _ => CharacterTestRig.Idle(), _ =>
            {
                backUp |= crate.Position.y > lowest + 1f;
                lowest = Mathf.Min(lowest, crate.Position.y);
            });
            Assert.That(lowest, Is.LessThan(-2.5f), "Test setup: the crate should fall into the hazard (its top is at -3).");
            Assert.That(lowest, Is.GreaterThan(-7.5f), "The crate should be put back as it reaches the hazard, not fall on through it.");
            Assert.That(backUp, Is.True, "Put back, the crate should be at its start again (and fall again).");
            Assert.That(reset.StartPosition, Is.EqualTo(start));
        }

        [UnityTest]
        public IEnumerator RespawningAtACratesCheckpoint_PutsTheCrateBack_AndElsewhere_LeavesItBe()
        {
            yield return SetUpCourse();
            PushableBlock mine = rig.PushableBox(new Vector3(-6f, 0.5f, 0f), Vector3.one);
            mine.gameObject.AddComponent<ResetOnRespawn>().CheckpointIds.Add("start");
            PushableBlock other = rig.PushableBox(new Vector3(6f, 0.5f, 0f), Vector3.one);
            other.gameObject.AddComponent<ResetOnRespawn>().CheckpointIds.Add("elsewhere");
            yield return rig.Run(0.1f, _ => CharacterTestRig.Idle());
            Vector3 mineStart = mine.Position, otherStart = other.Position;
            mine.Teleport(mineStart + new Vector3(0f, 0f, 3f));
            other.Teleport(otherStart + new Vector3(0f, 0f, 3f));
            yield return rig.Run(2, _ => CharacterTestRig.Idle());

            Assert.That(Presence.RespawnAtCheckpoint(rig.Movement), Is.True);
            yield return rig.Run(2, _ => CharacterTestRig.Idle());

            Assert.That(Vector3.Distance(mine.Position, mineStart), Is.LessThan(0.01f), "A respawn at the crate's checkpoint should put it back.");
            Assert.That(Vector3.Distance(other.Position, otherStart + new Vector3(0f, 0f, 3f)), Is.LessThan(0.01f), "A respawn at another checkpoint should leave it where it is.");
        }

        [UnityTest]
        public IEnumerator TheRespawnPoint_IsSavedAndPutBackWithTheCharacter()
        {
            yield return SetUpCourse(-4f);
            Checkpoint second = rig.Checkpoint("second", new Vector3(0f, 0f, 6f), VolumeSize);
            MovementStateSnapshot before = rig.Movement.SaveState();
            yield return WalkTo(6.5f);
            Assume.That(Presence.active, Is.SameAs(second));
            byte[] written = StateSerializer.WriteCharacter(rig.Movement);

            Assert.That(rig.Movement.RestoreState(before), Is.True);
            Assert.That(Presence.active, Is.Not.SameAs(second), "Put back, the checkpoint reached since should be forgotten.");
            StateSerializer.ReadCharacter(rig.Movement, written);
            Assert.That(Presence.active, Is.SameAs(second), "Read back, the checkpoint reached should be the one written (by its StableId).");
        }

        [UnityTest]
        public IEnumerator ACharacterWithNoModel_StillRespawns()
        {
            respawnedAt.Clear();
            rig.Floor();
            Checkpoint start = rig.Checkpoint("start", new Vector3(0f, 0f, -8f), VolumeSize, isStart: true);
            rig.Hazard(new Vector3(0f, 1f, 3f), new Vector3(6f, 3f, 2f));
            rig.SpawnCharacter(new Vector3(0f, 0.05f, 0f));
            rig.Models.LoadModelOnStart = false; // Before its Start, which would load it.
            yield return rig.Run(0.3f, _ => CharacterTestRig.Idle());
            Assume.That(rig.Models.IsModelReady, Is.False, "Test setup: the character should have no model.");
            yield return WalkUntilRespawned();
            Assert.That(respawnedAt, Is.EqualTo(new[] { start }), "With no model (so no hitboxes), the hazard should still respawn the character.");
        }
    }
}
