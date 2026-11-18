using System.Collections;
using System.Collections.Generic;
using System.IO;
using Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CharacterShowcase.Tests.InputAndDeterminism
{
    /// <summary>
    /// Tests of recording and replaying a character's input (InputRecording): a run's frames, recorded as it's stepped,
    /// saved to a file and read back, replayed on a new character in the same world, move it exactly as the first,
    /// step for step, bit for bit (pose and velocity). The input frames are all the movement depends on from outside.
    /// </summary>
    [Category(TestArea.Movement)]
    public class InputRecordingTests : CharacterRigFixture
    {
        /// <summary>The world the runs are in: a floor, and a block to jump onto.</summary>
        private IEnumerator BuildAndSpawn()
        {
            rig.Floor();
            rig.Box("Block", new Vector3(0f, 0.25f, 6f), new Vector3(4f, 0.5f, 2f));
            yield return rig.SpawnSettled(new Vector3(0f, 0.05f, 0f));
        }

        /// <summary>A run that walks, jumps (onto the block), sprints, dashes and crouches, turning as it goes.</summary>
        private static Inputs Script(int step)
        {
            Vector3 heading = Quaternion.Euler(0f, step * 0.5f, 0f) * Vector3.forward;
            if (step >= 20 && step < 30) { return CharacterTestRig.Jump(heading); }
            if (step >= 40 && step < 80) { return CharacterTestRig.Sprint(heading); }
            if (step == 90) { return CharacterTestRig.Dash(heading); }
            if (step >= 110 && step < 140) { return CharacterTestRig.Crouch(heading); }
            if (step >= 150 && step < 154) { return CharacterTestRig.Jump(-heading); }
            return CharacterTestRig.Move(heading);
        }

        private const int Steps = 200;

        private (Vector3 position, Quaternion rotation, Vector3 velocity) Pose() =>
            (rig.Position, rig.Body.rotation, rig.Movement.lastFrameMovementData.finalVelocity);

        [UnityTest]
        public IEnumerator ARecordedRun_SavedAndReplayed_MovesExactlyTheSame()
        {
            yield return BuildAndSpawn();
            var recording = new InputRecording(Steps, Time.fixedDeltaTime, rig.Movement.TuningFingerprint());
            rig.Recording = recording;
            var first = new List<(Vector3, Quaternion, Vector3)>();
            CharacterState did = CharacterState.Idle;
            yield return rig.Run(Steps, Script, _ =>
            {
                first.Add(Pose());
                did |= rig.Movement.lastFrameMovementData.state;
            });
            rig.Recording = null;
            Assume.That(did.HasFlag(CharacterState.Jumping) && did.HasFlag(CharacterState.Dashing) && did.HasFlag(CharacterState.Crouching), Is.True,
                $"Test setup: the run should jump, dash and crouch (it did: {did}).");
            Assert.That(recording.Count, Is.EqualTo(Steps), "Every step's frame should be recorded.");

            string path = Path.Combine(Application.temporaryCachePath, "Input Recording Test" + InputRecording.FileExtension);
            recording.Save(path);
            InputRecording loaded;
            try { loaded = InputRecording.Load(path); }
            finally { File.Delete(path); }
            Assert.That(loaded.Tuning, Is.EqualTo(recording.Tuning), "The file should keep the tuning it was recorded with.");

            yield return ReplaceRig();
            yield return BuildAndSpawn();
            Assert.That(rig.Movement.TuningFingerprint(), Is.EqualTo(loaded.Tuning), "Test setup: the replay should be of the same tuning.");
            int step = 0, differed = -1;
            string difference = null;
            yield return rig.Replay(loaded, _ =>
            {
                (Vector3 position, Quaternion rotation, Vector3 velocity) now = Pose();
                (Vector3 position, Quaternion rotation, Vector3 velocity) then = first[step];
                if (differed < 0 && !(now.position.Equals(then.position) && now.rotation.Equals(then.rotation) && now.velocity.Equals(then.velocity)))
                {
                    differed = step;
                    difference = $"at {now.position:F6} (was {then.position:F6}), velocity {now.velocity:F6} (was {then.velocity:F6})";
                }
                step++;
            });
            Assert.That(step, Is.EqualTo(Steps), "The replay should run every recorded step.");
            Assert.That(differed, Is.EqualTo(-1), $"Replayed, the character should move exactly as recorded: step {differed} differed, {difference}.");
        }
    }
}
