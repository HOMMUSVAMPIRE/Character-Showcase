using System.Collections;
using MeridianMotion.Characters;
using MeridianMotion.Levels;
using MeridianMotion.Showcase.TutorialRing;
using MeridianMotion.Tests;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MeridianMotion.Showcase.Tests.TutorialRing
{
    /// <summary>
    /// Tests of the Tutorial Ring's game set-up: the development keys turn themselves off outside the editor and
    /// development builds; and a run recorded through a shortcut gate and a respawn (ReplayRecorder) replays to the same
    /// end, with the same respawns and the gate opened at the same tick.
    /// </summary>
    [Category(TestArea.Levels)]
    public class TutorialReplayTests : CharacterRigFixture
    {
        private ReplayRecorder recorder;

        [TearDown]
        public void TearDown()
        {
            TutorialRingDevKeys.Allowed = () => Application.isEditor || Debug.isDebugBuild;
            if (recorder != null) { Object.Destroy(recorder.gameObject); }
        }

        [Test]
        public void TheDevKeys_TurnThemselvesOff_InAPlaytestBuild()
        {
            TutorialRingDevKeys.Allowed = () => false;
            var keys = rig.Track(new GameObject("Dev Keys")).AddComponent<TutorialRingDevKeys>();
            Assert.That(keys.enabled, Is.False, "Outside the editor and development builds, the keys should be off.");
        }

        [UnityTest]
        public IEnumerator ARunThroughAGateAndARespawn_ReplaysToTheSameEnd()
        {
            rig.Floor();
            rig.Checkpoint("start", Vector3.zero, new Vector3(6f, 3f, 2f), isStart: true);
            ShortcutGate gate = rig.Gate("gate", new Vector3(0f, 1.5f, 8f), new Vector3(8f, 3f, 0.5f), new Vector3(0f, -4f, 0f), 0.3f);
            rig.GateTrigger(gate, new Vector3(0f, 1f, 4f), new Vector3(8f, 3f, 2f));
            rig.Hazard(new Vector3(0f, 1f, 14f), new Vector3(8f, 3f, 2f));
            yield return rig.SpawnSettled(new Vector3(0f, 0.05f, 0f));
            recorder = new GameObject("Replay Recorder").AddComponent<ReplayRecorder>();
            recorder.KeyframeSeconds = 0.5f;
            recorder.Attach(rig.Controller);

            const int steps = 300;
            yield return rig.Run(steps, _ => CharacterTestRig.Move(Vector3.forward));
            Vector3 end = rig.Position;
            int respawns = rig.Movement.levelPresence.respawns;
            long opened = gate.OpenedTick;
            Assume.That(gate.IsOpen, Is.True, "Test setup: the run should open the gate.");
            Assume.That(respawns, Is.GreaterThan(0), "Test setup: the run should reach the hazard.");
            Replay run = recorder.CurrentRun();

            Assert.That(recorder.Play(run), Is.True);
            Assert.That(gate.IsOpen, Is.False, "Played, the level should be put back as it was when the run began.");
            yield return rig.RunWithSource(steps, recorder.PlaybackSource);

            Assert.That(Vector3.Distance(rig.Position, end), Is.LessThan(1e-4f), "Replayed, the run should end where it ended.");
            Assert.That(rig.Movement.levelPresence.respawns, Is.EqualTo(respawns), "Replayed, it should respawn as often.");
            Assert.That(gate.OpenedTick, Is.EqualTo(opened), "Replayed, the gate should open at the same tick.");
        }
    }
}
