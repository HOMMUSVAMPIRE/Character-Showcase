using System.Collections;
using MeridianMotion.Characters;
using MeridianMotion.Levels;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MeridianMotion.Tests.Levels
{
    /// <summary>
    /// Tests of shortcut gates (ShortcutGate, ShortcutTrigger): closed, a gate blocks the way both ways; reaching its
    /// trigger on its far side opens it, and then it can be walked through both ways; it stays open through a respawn;
    /// gates opened on the same tick move alike; Open All opens every one; and a gate's opening is saved and put back with
    /// the level's moving parts (WorldStateSnapshot, StateSerializer.WriteWorld), as a replay's keyframes need.
    /// </summary>
    [Category(TestArea.Levels)]
    public class ShortcutGateTests : CharacterRigFixture
    {
        private const float GateZ = 5f;
        private static readonly Vector3 GateSize = new(8f, 3f, 0.5f);
        private static readonly Vector3 Down = new(0f, -4f, 0f);

        private ShortcutGate MakeGate(string id = "gate") => rig.Gate(id, new Vector3(0f, GateSize.y / 2f, GateZ), GateSize, Down);

        private IEnumerator Walk(Vector3 direction, float seconds) => rig.Run(seconds, _ => CharacterTestRig.Move(direction));

        [UnityTest]
        public IEnumerator AClosedGate_BlocksBothWays()
        {
            rig.Floor();
            ShortcutGate gate = MakeGate();
            yield return rig.SpawnSettled(new Vector3(0f, 0.05f, 0f));
            yield return Walk(Vector3.forward, 2f);
            Assert.That(rig.Position.z, Is.LessThan(GateZ), "A closed gate should stop the character from the near side.");
            rig.Teleport(new Vector3(0f, 0.05f, 10f));
            yield return Walk(Vector3.back, 2f);
            Assert.That(rig.Position.z, Is.GreaterThan(GateZ), "A closed gate should stop the character from the far side.");
            Assert.That(gate.IsOpen, Is.False);
        }

        [UnityTest]
        public IEnumerator ReachingTheTrigger_OpensTheGate_ThenItsOpenBothWays()
        {
            rig.Floor();
            ShortcutGate gate = MakeGate();
            rig.GateTrigger(gate, new Vector3(0f, 1f, 8f), new Vector3(8f, 3f, 2f));
            yield return rig.SpawnSettled(new Vector3(0f, 0.05f, 12f));
            yield return rig.RunUntil(() => rig.Position.z < 0f, _ => CharacterTestRig.Move(Vector3.back), timeoutSeconds: 6f);
            Assert.That(gate.IsOpen, Is.True, "Reaching the trigger should open the gate.");
            Assert.That(rig.Position.z, Is.LessThan(0f), "Once it's open, the character should get through from the far side.");
            yield return rig.RunUntil(() => rig.Position.z > 10f, _ => CharacterTestRig.Move(Vector3.forward), timeoutSeconds: 6f);
            Assert.That(rig.Position.z, Is.GreaterThan(10f), "Open, the gate should let the character through from the near side too.");
        }

        [UnityTest]
        public IEnumerator AGate_StaysOpenThroughARespawn()
        {
            rig.Floor();
            ShortcutGate gate = MakeGate();
            yield return rig.SpawnSettled(new Vector3(0f, 0.05f, 0f));
            gate.Open();
            yield return rig.Run(1f, _ => CharacterTestRig.Idle());
            Vector3 open = gate.transform.position;
            rig.Movement.Respawn(new Vector3(0f, 0f, -3f), Quaternion.identity);
            yield return rig.Run(0.5f, _ => CharacterTestRig.Idle());
            Assert.That(gate.IsOpen, Is.True);
            Assert.That(Vector3.Distance(gate.transform.position, open), Is.LessThan(1e-4f), "The gate should stay where it opened to.");
        }

        [UnityTest]
        public IEnumerator GatesOpenedOnTheSameTick_MoveAlike()
        {
            rig.Floor();
            ShortcutGate a = rig.Gate("a", new Vector3(-10f, 1.5f, 5f), GateSize, Down);
            ShortcutGate b = rig.Gate("b", new Vector3(10f, 1.5f, 5f), GateSize, Down);
            yield return rig.SpawnSettled(Vector3.zero);
            a.Open();
            b.Open();
            Assert.That(a.OpenedTick, Is.EqualTo(b.OpenedTick));
            for (int i = 0; i < 10; i++)
            {
                yield return rig.Run(2, _ => CharacterTestRig.Idle());
                Vector3 da = a.GetComponent<Rigidbody>().position - new Vector3(-10f, 1.5f, 5f);
                Vector3 db = b.GetComponent<Rigidbody>().position - new Vector3(10f, 1.5f, 5f);
                Assert.That(da, Is.EqualTo(db), $"Opened on the same tick, two gates should be as far open (step {i * 2}).");
            }
        }

        [Test]
        public void OpenAll_OpensEveryGate()
        {
            ShortcutGate a = rig.Gate("a", new Vector3(-10f, 1.5f, 5f), GateSize, Down);
            ShortcutGate b = rig.Gate("b", new Vector3(10f, 1.5f, 5f), GateSize, Down);
            ShortcutGate.OpenAll();
            Assert.That(a.IsOpen && b.IsOpen, Is.True);
            Assert.That(ShortcutGate.Find("b"), Is.SameAs(b));
        }

        [UnityTest]
        public IEnumerator AGatesOpening_IsSavedAndPutBack_WithTheLevel()
        {
            rig.Floor();
            ShortcutGate gate = MakeGate();
            yield return rig.SpawnSettled(Vector3.zero);
            WorldStateSnapshot closed = WorldStateSnapshot.Save();
            byte[] closedBytes = StateSerializer.WriteWorld();
            gate.Open();
            yield return rig.Run(1f, _ => CharacterTestRig.Idle());
            byte[] openBytes = StateSerializer.WriteWorld();

            closed.Restore();
            Assert.That(gate.IsOpen, Is.False, "Put back, the gate should be closed again.");
            StateSerializer.ReadWorld(openBytes);
            Assert.That(gate.IsOpen, Is.True, "Read back, the gate should be open.");
            StateSerializer.ReadWorld(closedBytes);
            Assert.That(gate.IsOpen, Is.False, "Read back, the gate should be closed.");
        }
    }
}
