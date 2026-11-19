using System.IO;
using MeridianMotion.Characters;
using NUnit.Framework;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace MeridianMotion.Tests.EditMode.InputAndDeterminism
{
    /// <summary>
    /// Tests of a character's input as frames (CharacterInputFrame): the frame stays unmanaged and small; the presses and
    /// hold durations the movement reads (InputState) are worked out from successive frames, in steps; the device read
    /// each render frame is latched into one frame per physics step (InputLatch), so a tap between steps isn't lost and a
    /// render frame that runs two steps doesn't press twice; and a recording of frames (InputRecording) keeps the latest
    /// and survives a round trip through its file format.
    /// </summary>
    [Category(TestArea.Movement)]
    [TestTiming]
    public class InputFrameTests
    {
        private const float Step = 0.02f;

        private static CharacterInputFrame Holding(InputButtons buttons, uint tick = 0) => new() { tick = tick, held = buttons };

        [Test]
        public void TheFrame_IsUnmanaged_AndSmall()
        {
            Assert.That(UnsafeUtility.IsUnmanaged<CharacterInputFrame>(), Is.True, "The frame should be unmanaged, to be copied, kept and sent as it is.");
            Assert.That(UnsafeUtility.IsBlittable<CharacterInputFrame>(), Is.True, "The frame should be blittable.");
            Assert.That(UnsafeUtility.SizeOf<CharacterInputFrame>(), Is.EqualTo(24),
                "The frame is a tick, a move input, a look and the buttons held: 24 bytes. A change of size is a change of what's sent.");
        }

        [Test]
        public void PressesAndHolds_AreCountedInSteps_FromTheFrames()
        {
            var state = new InputState();
            InputButton jump = state.Current.jump;

            state.Advance(Holding(InputButtons.None), Step);
            Assert.That(jump.held || jump.justPressed, Is.False);

            state.Advance(Holding(InputButtons.Jump), Step);
            Assert.That(jump.justPressed, Is.True, "The first step a frame holds it is a press.");
            Assert.That(jump.holdDuration, Is.EqualTo(0f), "Held for no time on the step it's pressed.");

            state.Advance(Holding(InputButtons.Jump), Step);
            state.Advance(Holding(InputButtons.Jump), Step);
            Assert.That(jump.justPressed, Is.False, "Held on, it isn't pressed again.");
            Assert.That(jump.holdDuration, Is.EqualTo(2 * Step), "Two steps after the press, it's been held two steps.");

            state.Advance(Holding(InputButtons.None), Step);
            Assert.That(jump.held, Is.False);
            Assert.That(jump.justReleased, Is.True, "The first step a frame doesn't hold it is a release.");
            Assert.That(jump.lastHoldDuration, Is.EqualTo(2 * Step), "Released, it was held as long as it last was.");
            Assert.That(jump.holdDuration, Is.EqualTo(0f));

            state.Advance(Holding(InputButtons.Jump | InputButtons.Sprint), Step);
            Assert.That(jump.justPressed && state.Current.sprint.justPressed, Is.True, "Down again, each is a new press.");
        }

        [Test]
        public void TheMoveInput_IsTheFramesOnTheGround()
        {
            var state = new InputState();
            state.Advance(new CharacterInputFrame { move = new Vector2(0.25f, -0.5f) }, Step);
            Assert.That(state.Current.movementVector, Is.EqualTo(new Vector3(0.25f, 0f, -0.5f)));
        }

        [Test]
        public void ATapBetweenTwoSteps_IsHeldInTheNextStepsFrame_AndOnlyThat()
        {
            var latch = new InputLatch();
            // Pressed and let go again between the steps at 0.02 and 0.04 (two render frames).
            latch.Sample(Vector2.zero, 0f, 0f, InputButtons.Jump, InputButtons.Jump);
            latch.Sample(Vector2.zero, 0f, 0f, InputButtons.None, InputButtons.None);
            Assert.That(latch.Take(1, 0.04f).Holds(InputButtons.Jump), Is.True, "A tap shorter than a step should still be seen.");
            Assert.That(latch.Take(2, 0.06f).Holds(InputButtons.Jump), Is.False, "It should be seen once.");
        }

        [Test]
        public void ARenderFrameThatRunsTwoSteps_PressesOnce()
        {
            var latch = new InputLatch();
            var state = new InputState();
            latch.Sample(Vector2.zero, 0f, 0f, InputButtons.Jump, InputButtons.Jump);
            state.Advance(latch.Take(1, 0.02f), Step);
            Assert.That(state.Current.jump.justPressed, Is.True);
            // No render frame between the two steps: the button's still down, not pressed again.
            state.Advance(latch.Take(2, 0.04f), Step);
            Assert.That(state.Current.jump.held, Is.True);
            Assert.That(state.Current.jump.justPressed, Is.False, "A frame that runs two steps shouldn't press the button twice.");
        }

        [Test]
        public void TakenAgainInTheSameStep_GivesTheSameFrame()
        {
            var latch = new InputLatch();
            latch.Sample(new Vector2(1f, 0f), 90f, 10f, InputButtons.None, InputButtons.Dash);
            CharacterInputFrame first = latch.Take(5, 0.1f), second = latch.Take(7, 0.1f);
            Assert.That(second.Holds(InputButtons.Dash), Is.True, "Two characters reading one device in a step should both see a tap.");
            Assert.That(second.tick, Is.EqualTo(7u), "Each frame is tagged with its own character's tick.");
            second.tick = first.tick;
            Assert.That(second, Is.EqualTo(first));
        }

        [Test]
        public void ARecording_KeepsTheLatestFrames_AndSurvivesItsFile()
        {
            var recording = new InputRecording(3, Step, tuning: 0x1234_5678_9ABC_DEF0UL);
            for (uint tick = 0; tick < 5; tick++)
            {
                recording.Add(new CharacterInputFrame
                {
                    tick = tick, move = new Vector2(tick * 0.1f, -0.3f), lookYaw = tick * 7.5f, lookPitch = -12.25f,
                    held = (InputButtons)(1u << (int)tick),
                });
            }
            Assert.That(recording.Count, Is.EqualTo(3), "Full, it should keep only as many as it holds.");
            Assert.That(recording[0].tick, Is.EqualTo(2u), "It should keep the latest, oldest first.");

            using var stream = new MemoryStream();
            recording.Write(stream);
            stream.Position = 0;
            InputRecording read = InputRecording.Read(stream);
            Assert.That(read.Count, Is.EqualTo(recording.Count));
            Assert.That(read.StepLength, Is.EqualTo(recording.StepLength));
            Assert.That(read.Tuning, Is.EqualTo(recording.Tuning));
            for (int i = 0; i < read.Count; i++) { Assert.That(read[i], Is.EqualTo(recording[i]), $"Frame {i} should read back exactly."); }

            Assert.That(() => InputRecording.Read(new MemoryStream(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 })), Throws.TypeOf<InvalidDataException>(),
                "Something that isn't a recording should be refused.");
        }
    }
}
