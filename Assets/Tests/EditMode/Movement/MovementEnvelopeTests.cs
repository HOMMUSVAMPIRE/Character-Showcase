using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using CharacterShowcase.EditorTools;
using NUnit.Framework;

namespace CharacterShowcase.Tests.EditMode.Movement
{
    /// <summary>
    /// Checks that Tools/LevelSpec/envelope.json (the limits the levelspec tool's movement checks hold levels to) is what
    /// the default character can do now: it's written from the character's measured capabilities (MovementEnvelopeWriter),
    /// so this fails after a change to the tuning, the character or its model until it's written again (Tools > Character
    /// Showcase > Level > Specs > Write Movement Envelope).
    /// </summary>
    [Category(TestArea.Movement)]
    [TestTiming]
    public class MovementEnvelopeTests
    {
        // Measurements repeat exactly on one machine; this allows for float differences between machines.
        private const double Tolerance = 0.02;

        [Test]
        public void TheMovementEnvelope_IsWhatTheCharacterCanDoNow()
        {
            Assert.That(File.Exists(MovementEnvelopeWriter.EnvelopePath), Is.True, $"There's no {MovementEnvelopeWriter.EnvelopePath}: write it.");
            string written = File.ReadAllText(MovementEnvelopeWriter.EnvelopePath), now = MovementEnvelopeWriter.Envelope();
            string Shape(string json) => Regex.Replace(json, @"-?\d+(\.\d+)?|null", "#");
            Assert.That(Shape(written), Is.EqualTo(Shape(now)),
                $"{MovementEnvelopeWriter.EnvelopePath} lists other moves than the character has now: write it again (Write Movement Envelope).");
            double[] Numbers(string json) => Regex.Matches(json, @"-?\d+(\.\d+)?").Select(m => double.Parse(m.Value, CultureInfo.InvariantCulture)).ToArray();
            double[] a = Numbers(written), b = Numbers(now);
            for (int i = 0; i < a.Length; i++)
            {
                Assert.That(a[i], Is.EqualTo(b[i]).Within(Tolerance),
                    $"{MovementEnvelopeWriter.EnvelopePath} is out of date (value {i}: written {a[i]}, now {b[i]}): write it again (Write Movement Envelope).\nNow:\n{now}");
            }
        }
    }
}
