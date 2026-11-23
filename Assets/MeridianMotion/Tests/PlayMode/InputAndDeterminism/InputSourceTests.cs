using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MeridianMotion.Characters;
using NUnit.Framework;
using UnityEngine.TestTools;
using UnityEngine;

namespace MeridianMotion.Tests.InputAndDeterminism
{
    /// <summary>
    /// Tests of what drives a character (its MMotionCharacter's input source, ICharacterInputSource): without one,
    /// its owner decides (an AI's stands idle, a remote one isn't stepped, the local player's reads the device, idle
    /// without an input manager); a ScriptedInputSource plays timed steps, and a recording exactly as it was recorded;
    /// and a WaypointInputSource, an AI, walks a course of a gap and a block to its end, jumping where it needs to, and
    /// its run replays identically from a recording of its frames. The rig steps the character through its controller
    /// (MMotionCharacter.StepMovement), as the game does.
    /// </summary>
    [Category(TestArea.Movement)]
    public class InputSourceTests : CharacterRigFixture
    {
        [UnityTest]
        public IEnumerator WithoutASource_TheOwnerDecides_AndNothingThrows([Values] ControlledBy owner)
        {
            yield return rig.StandOnFloor();
            rig.Controller.ControlledBy = owner;
            Vector3 start = rig.Position;
            uint tick = rig.Movement.InputTick;
            yield return rig.RunWithSource(0.5f, null);
            int steps = CharacterTestRig.StepsFor(0.5f);
            if (owner == ControlledBy.Remote)
            {
                Assert.That(rig.Movement.InputTick, Is.EqualTo(tick), "A remote character with no source isn't stepped: something else places it.");
            }
            else
            {
                Assert.That(rig.Movement.InputTick, Is.EqualTo(tick + (uint)steps), "The character should be stepped.");
                // The local player's reads the device: there's no input manager here, so its frames are idle too.
                Assert.That(Vector3.Distance(rig.Position, start), Is.LessThan(1e-3f), "With no input, the character should stand still.");
            }
        }

        [UnityTest]
        public IEnumerator AScriptedSource_PlaysItsTimedSteps_ThenStandsIdle()
        {
            yield return rig.StandOnFloor();
            var script = rig.Character.AddComponent<ScriptedInputSource>();
            script.Steps.Add(new ScriptedInputSource.TimedStep { seconds = 0.6f, move = Vector2.up, lookYaw = 90f });
            script.Steps.Add(new ScriptedInputSource.TimedStep { seconds = 0.1f, held = InputButtons.Jump });
            script.Steps.Add(new ScriptedInputSource.TimedStep { seconds = 0.2f });
            rig.ClearEvents();
            yield return rig.RunWithSource(0.9f, script);
            Assert.That(rig.Position.x, Is.GreaterThan(0.5f * rig.WalkSpeed * 0.6f), "Walking forward, looking along +X, it should go along +X.");
            Assert.That(rig.Count(MovementEvents.Jumped), Is.EqualTo(1), "It should jump once, as scripted.");
            Assert.That(script.Finished, Is.False, "Test setup: the script should just have run out.");
            yield return rig.RunWithSource(1, script);
            Assert.That(script.Finished, Is.True, "Past its last step, the script should be finished.");
        }

        // ---- An AI: walking a route ----

        private float gapStart, gapEnd, blockStart, blockHeight;

        /// <summary>
        /// A course: a floor, a gap the character can sprint-jump across, and a block on the far side taller than a step
        /// but within what it can get up; a lower floor under the gap to catch a fall. Sized from what the character can
        /// do (its MovementCapabilities), so retuning doesn't break it.
        /// </summary>
        private IEnumerator BuildCourse()
        {
            rig.Floor(-6f);
            rig.Box("Start", new Vector3(0f, -0.5f, 2f), new Vector3(6f, 1f, 10f));
            rig.SpawnCharacter(new Vector3(0f, 0.05f, 0f));
            yield return rig.WaitForModel();
            yield return rig.Settle();
            MovementCapabilities can = rig.Movement.Capabilities;
            float across = can.Jumps.Where(m => m.Name == "sprint jump").Select(m => m.Reach).DefaultIfEmpty(0f).First();
            float up = can.Climbs.Where(m => m.Name == "jump up").Select(m => m.Reach).DefaultIfEmpty(0f).First();
            Assume.That(across, Is.GreaterThan(1f), "Test setup: the character should sprint-jump across a gap.");
            Assume.That(up, Is.GreaterThan(can.StepHeight + 0.1f), "Test setup: the character should jump up onto a block taller than a step.");
            gapStart = 7f;
            gapEnd = gapStart + 0.5f * across;
            blockStart = gapEnd + 5f;
            blockHeight = Mathf.Lerp(can.StepHeight, up, 0.6f);
            rig.Box("Far Side", new Vector3(0f, -0.5f, gapEnd + 6f), new Vector3(6f, 1f, 12f));
            rig.Box("Block", new Vector3(0f, blockHeight / 2f, blockStart + 2f), new Vector3(6f, blockHeight, 4f));
            Physics.SyncTransforms();
        }

        private WaypointInputSource Walker()
        {
            var walker = rig.Character.AddComponent<WaypointInputSource>();
            walker.SetRoute(new[]
            {
                new Vector3(0f, 0f, gapEnd + 2f), new Vector3(0f, 0f, blockStart - 1f), new Vector3(0f, blockHeight, blockStart + 2.5f),
            }, sprinting: true);
            return walker;
        }

        [UnityTest]
        public IEnumerator AWaypointAI_WalksACourseToItsEnd_JumpingTheGapAndOntoTheBlock_AndReplaysIdentically()
        {
            yield return BuildCourse();
            WaypointInputSource walker = Walker();
            var recording = new InputRecording(CharacterTestRig.StepsFor(10f), Time.fixedDeltaTime);
            rig.Recording = recording;
            var path = new List<Vector3>();
            float lowest = float.PositiveInfinity;
            int steps = 0;
            for (; steps < CharacterTestRig.StepsFor(10f) && !walker.Arrived; steps++)
            {
                yield return rig.RunWithSource(1, walker, _ => { path.Add(rig.Position); lowest = Mathf.Min(lowest, rig.Position.y); });
            }
            rig.Recording = null;
            TestContext.WriteLine($"The AI took {steps} steps over a {gapEnd - gapStart:0.00} m gap and onto a {blockHeight:0.00} m block; " +
                                  $"it ended at {rig.Position:F2}, its lowest {lowest:0.00} m.");
            Assert.That(walker.Arrived, Is.True, $"The AI should reach the end of its route (it's at {rig.Position:F2}, going to point {walker.NextWaypoint}).");
            Assert.That(lowest, Is.GreaterThan(-0.2f), "It should jump the gap, not fall into it.");
            Assert.That(rig.Position.y, Is.EqualTo(blockHeight).Within(0.05f), "It should end on the block.");

            // Its frames, replayed on a new character on the same course, move it the same way, step for step.
            yield return ReplaceRig();
            yield return BuildCourse();
            var replay = rig.Character.AddComponent<ScriptedInputSource>();
            replay.Play(recording);
            int step = 0, differed = -1;
            yield return rig.RunWithSource(recording.Count, replay, _ =>
            {
                if (differed < 0 && !rig.Position.Equals(path[step])) { differed = step; }
                step++;
            });
            Assert.That(differed, Is.EqualTo(-1), $"Replayed from its recording, the AI's run should be the same: step {differed} differed.");
        }
    }
}
