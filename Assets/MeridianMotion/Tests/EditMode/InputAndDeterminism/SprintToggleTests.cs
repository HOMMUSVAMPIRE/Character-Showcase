using MeridianMotion.Characters;
using NUnit.Framework;
using UnityEngine;

namespace MeridianMotion.Tests.EditMode.InputAndDeterminism
{
    /// <summary>
    /// Tests of Sprint as a toggle (SprintToggle, behind PlayerInputReader.ToggleSprint): off, Sprint is read as the button
    /// is; on, a press starts sprinting (held from then on, the button let go) and the next press ends it (that press not a
    /// press); stopping after moving ends it, but a sprint started standing still waits for the character to set off; and
    /// turning the toggle off ends a sprint under way.
    /// </summary>
    [Category(TestArea.Movement)]
    [TestTiming]
    public class SprintToggleTests
    {
        private static (InputButtons down, InputButtons pressed) Read(SprintToggle toggle, Vector2 move, InputButtons down, InputButtons pressed)
        {
            toggle.Apply(move, ref down, ref pressed);
            return (down, pressed);
        }

        private static bool Sprinting(SprintToggle toggle, Vector2 move, InputButtons down = InputButtons.None, InputButtons pressed = InputButtons.None) =>
            (Read(toggle, move, down, pressed).down & InputButtons.Sprint) != 0;

        [Test]
        public void Off_SprintIsReadAsTheButtonIs()
        {
            var toggle = new SprintToggle();
            Assert.That(Read(toggle, Vector2.up, InputButtons.Sprint, InputButtons.Sprint), Is.EqualTo((InputButtons.Sprint, InputButtons.Sprint)));
            Assert.That(Sprinting(toggle, Vector2.up), Is.False, "Let go, it's not held.");
        }

        [Test]
        public void On_APressStartsIt_AndTheNextEndsIt()
        {
            var toggle = new SprintToggle { Enabled = true };
            (InputButtons down, InputButtons pressed) = Read(toggle, Vector2.up, InputButtons.Sprint, InputButtons.Sprint);
            Assert.That(down & InputButtons.Sprint, Is.EqualTo(InputButtons.Sprint));
            Assert.That(pressed & InputButtons.Sprint, Is.EqualTo(InputButtons.Sprint), "The press that starts it is a press.");
            Assert.That(Sprinting(toggle, Vector2.up), Is.True, "Let go, it's still held.");

            (down, pressed) = Read(toggle, Vector2.up, InputButtons.Sprint, InputButtons.Sprint);
            Assert.That(down & InputButtons.Sprint, Is.EqualTo(InputButtons.None), "The next press ends it, though the button's down.");
            Assert.That(pressed & InputButtons.Sprint, Is.EqualTo(InputButtons.None), "The press that ends it isn't a press.");
            Assert.That(Sprinting(toggle, Vector2.up), Is.False);
        }

        [Test]
        public void StoppingAfterMoving_EndsIt_ButOneStartedStandingStillWaitsToSetOff()
        {
            var toggle = new SprintToggle { Enabled = true };
            Assert.That(Sprinting(toggle, Vector2.up, InputButtons.Sprint, InputButtons.Sprint), Is.True);
            Assert.That(Sprinting(toggle, Vector2.zero), Is.False, "Stopping after moving should end it.");

            Assert.That(Sprinting(toggle, Vector2.zero, InputButtons.Sprint, InputButtons.Sprint), Is.True);
            Assert.That(Sprinting(toggle, Vector2.zero), Is.True, "Started standing still, it waits for the character to move.");
            Assert.That(Sprinting(toggle, Vector2.up), Is.True);
            Assert.That(Sprinting(toggle, Vector2.zero), Is.False, "...then ends when it stops.");
        }

        [Test]
        public void TurningItOff_EndsASprintUnderWay()
        {
            var toggle = new SprintToggle { Enabled = true };
            Assert.That(Sprinting(toggle, Vector2.up, InputButtons.Sprint, InputButtons.Sprint), Is.True);
            toggle.Enabled = false;
            Assert.That(toggle.Sprinting, Is.False);
            Assert.That(Sprinting(toggle, Vector2.up), Is.False, "Off, the button's let go, so it's not held.");
        }
    }
}
