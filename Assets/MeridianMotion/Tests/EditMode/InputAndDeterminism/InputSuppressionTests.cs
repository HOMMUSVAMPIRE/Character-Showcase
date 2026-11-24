using MeridianMotion.Characters;
using NUnit.Framework;
using UnityEngine;

namespace MeridianMotion.Tests.EditMode.InputAndDeterminism
{
    /// <summary>
    /// Tests of suppressing the player's input (InputSuppression, behind PlayerInputReader.Suppressed, which a window over
    /// the game sets): while it's suppressed nothing is held or pressed; a button held through it isn't taken as a press
    /// when it ends, but counts again once it's let go and pressed again; and a button pressed only after it ends counts
    /// at once.
    /// </summary>
    [Category(TestArea.Movement)]
    [TestTiming]
    public class InputSuppressionTests
    {
        private static (Vector2 move, InputButtons down, InputButtons pressed) Read(InputSuppression suppression, Vector2 move, InputButtons down, InputButtons pressed)
        {
            suppression.Apply(ref move, ref down, ref pressed);
            return (move, down, pressed);
        }

        [Test]
        public void WhileSuppressed_NothingIsHeldOrPressed()
        {
            var suppression = new InputSuppression { Suppressed = true };
            (Vector2 move, InputButtons down, InputButtons pressed) = Read(suppression, Vector2.up, InputButtons.Jump | InputButtons.Sprint, InputButtons.Jump);
            Assert.That(move, Is.EqualTo(Vector2.zero));
            Assert.That(down, Is.EqualTo(InputButtons.None));
            Assert.That(pressed, Is.EqualTo(InputButtons.None));
        }

        [Test]
        public void AButtonHeldThroughIt_IsLeftOutUntilLetGo_ThenCountsFromItsNextPress()
        {
            var suppression = new InputSuppression { Suppressed = true };
            Read(suppression, Vector2.zero, InputButtons.Jump, InputButtons.Jump);
            suppression.Suppressed = false;

            (Vector2 move, InputButtons down, InputButtons pressed) = Read(suppression, Vector2.up, InputButtons.Jump, InputButtons.None);
            Assert.That(down, Is.EqualTo(InputButtons.None), "Jump, held through the suppression, shouldn't count as held when it ends.");
            Assert.That(move, Is.EqualTo(Vector2.up), "The move input should come back at once.");

            Read(suppression, Vector2.zero, InputButtons.None, InputButtons.None); // Let go.
            (_, down, pressed) = Read(suppression, Vector2.zero, InputButtons.Jump, InputButtons.Jump);
            Assert.That(down, Is.EqualTo(InputButtons.Jump), "Pressed again, Jump should count.");
            Assert.That(pressed, Is.EqualTo(InputButtons.Jump));
        }

        [Test]
        public void AButtonPressedAfterItEnds_CountsAtOnce()
        {
            var suppression = new InputSuppression { Suppressed = true };
            Read(suppression, Vector2.zero, InputButtons.None, InputButtons.None);
            suppression.Suppressed = false;
            (_, InputButtons down, InputButtons pressed) = Read(suppression, Vector2.zero, InputButtons.Dash, InputButtons.Dash);
            Assert.That(down, Is.EqualTo(InputButtons.Dash));
            Assert.That(pressed, Is.EqualTo(InputButtons.Dash));
        }
    }
}
