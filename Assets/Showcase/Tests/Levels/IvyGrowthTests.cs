using System.Collections.Generic;
using System.Linq;
using MeridianMotion.Showcase.Ivy;
using MeridianMotion.Tests;
using NUnit.Framework;
using UnityEngine;

namespace MeridianMotion.Showcase.Tests.Levels
{
    /// <summary>
    /// Tests of the ivy's grower on its own (IvyGrowth: ReferenceAssets/OpenTasks.md, Q2 and Q8), on faces described in
    /// code rather than built: ivy grows from the foot up, fading out at its front; none grows on a wall it mayn't (a
    /// climbable or marked one), nor do its stems cross one; none where there's no wall; the same seed grows the same mask
    /// and another seed another. (FloraPlacementTests holds the Ivy and Vines pieces to the same rules on built walls.)
    /// </summary>
    [Category(TestArea.Levels)]
    [TestTiming]
    public class IvyGrowthTests
    {
        private static readonly Vector2 Size = new(10f, 6f);

        /// <summary>The share of a band of rows (v from y0 to y1, m) that has growth, and how dense it is there on average (0 to 255).</summary>
        private static float Density(IvyGrowth.Grown g, float u0, float u1, float v0, float v1)
        {
            float sum = 0f;
            int count = 0;
            for (int y = Mathf.FloorToInt(v0 / g.texel); y < Mathf.Min(g.height, Mathf.CeilToInt(v1 / g.texel)); y++)
            {
                for (int x = Mathf.FloorToInt(u0 / g.texel); x < Mathf.Min(g.width, Mathf.CeilToInt(u1 / g.texel)); x++)
                {
                    sum += g.density[y * g.width + x];
                    count++;
                }
            }
            return count > 0 ? sum / count : 0f;
        }

        /// <summary>The highest any growth reaches (m).</summary>
        private static float Top(IvyGrowth.Grown g)
        {
            for (int y = g.height - 1; y >= 0; y--)
            {
                for (int x = 0; x < g.width; x++) { if (g.density[y * g.width + x] > 0) { return (y + 1) * g.texel; } }
            }
            return 0f;
        }

        [Test]
        public void Ivy_GrowsFromTheFootUp_FadingOutAtItsFront()
        {
            IvyGrowth.Grown g = IvyGrowth.Grow(Size, 7, IvyGrowth.Settings.Defaults);
            Assert.That(g.stems, Is.Not.Empty, "The ivy should have stems.");
            List<List<Vector3>> mains = g.stems.Where(s => s[0].y < 0.1f).ToList();
            Assert.That(mains, Is.Not.Empty, "Its main stems should be rooted at the face's foot.");
            foreach (List<Vector3> stem in mains)
            {
                Assert.That(stem[stem.Count - 1].y, Is.GreaterThan(stem[0].y), "A main stem should climb from its root.");
            }
            float top = Top(g);
            Assert.That(top, Is.GreaterThan(1f), "The ivy should climb a good way up the face.");
            float foot = Density(g, 0f, Size.x, 0f, top / 3f), front = Density(g, 0f, Size.x, top * 0.85f, top);
            Assert.That(front, Is.LessThan(foot * 0.6f), "The growth should thin out toward its front (its top), not stop in a hard edge.");
        }

        [Test]
        public void Ivy_NeverGrowsOnOrAcrossAWallItMayNot()
        {
            // A strip from 4 to 6 m along that it mayn't grow on (a climbable or marked wall: Surface.None).
            static IvyGrowth.Surface WallAt(Vector2 uv) => uv.x >= 4f && uv.x <= 6f ? IvyGrowth.Surface.None : IvyGrowth.Surface.Face;
            IvyGrowth.Grown g = IvyGrowth.Grow(Size, 11, IvyGrowth.Settings.Defaults, WallAt);
            Assert.That(Density(g, 0f, 3.9f, 0f, Size.y), Is.GreaterThan(0f), "There should be ivy left of the strip.");
            Assert.That(Density(g, 6.1f, Size.x, 0f, Size.y), Is.GreaterThan(0f), "...and right of it.");
            Assert.That(Density(g, 4.05f, 5.95f, 0f, Size.y), Is.Zero, "No leaves on the strip.");
            foreach (List<Vector3> stem in g.stems)
            {
                Assert.That(stem.All(p => p.x < 4f || p.x > 6f), Is.True, "No stem should run over the strip.");
                Assert.That(stem.All(p => p.x < 4f) || stem.All(p => p.x > 6f), Is.True, "No stem should cross from one side of the strip to the other.");
            }
        }

        [Test]
        public void Ivy_NeverGrowsWhereThereIsNoWall()
        {
            // The wall stops 2.5 m up the face (above it, nothing: Surface.None).
            static IvyGrowth.Surface WallAt(Vector2 uv) => uv.y <= 2.5f ? IvyGrowth.Surface.Face : IvyGrowth.Surface.None;
            IvyGrowth.Grown g = IvyGrowth.Grow(Size, 3, IvyGrowth.Settings.Defaults, WallAt);
            Assert.That(Density(g, 0f, Size.x, 0f, 2.4f), Is.GreaterThan(0f), "There should be ivy on the wall.");
            Assert.That(Density(g, 0f, Size.x, 2.55f, Size.y), Is.Zero, "No leaves above where the wall stops.");
            Assert.That(g.stems.SelectMany(s => s).All(p => p.y <= 2.5f), Is.True, "No stem should grow past where the wall stops.");
        }

        [Test]
        public void TheSameSeed_GrowsTheSameMask_AndAnotherSeedAnother()
        {
            byte[] once = IvyGrowth.Grow(Size, 5, IvyGrowth.Settings.Defaults).density;
            Assert.That(IvyGrowth.Grow(Size, 5, IvyGrowth.Settings.Defaults).density, Is.EqualTo(once), "The same seed should grow the same ivy.");
            Assert.That(IvyGrowth.Grow(Size, 6, IvyGrowth.Settings.Defaults).density, Is.Not.EqualTo(once), "Another seed should grow other ivy.");
            byte[] hung = IvyGrowth.Hang(Size, 5, IvyGrowth.Settings.VineDefaults).density;
            Assert.That(IvyGrowth.Hang(Size, 5, IvyGrowth.Settings.VineDefaults).density, Is.EqualTo(hung), "The same seed should hang the same vines.");
        }

        [Test]
        public void StemsPacked_AreUnpackedWhereTheyWere()
        {
            IvyGrowth.Grown g = IvyGrowth.Grow(Size, 9, IvyGrowth.Settings.Defaults);
            List<List<Vector3>> unpacked = IvyGrowth.Unpack(IvyGrowth.Pack(g.stems));
            Assert.That(unpacked.Count, Is.EqualTo(g.stems.Count(s => s.Count >= 2)), "Every stem should be saved.");
            for (int i = 0; i < unpacked.Count; i++)
            {
                List<Vector3> before = g.stems.Where(s => s.Count >= 2).ElementAt(i), after = unpacked[i];
                Assert.That(Vector2.Distance(after[0], before[0]), Is.LessThan(0.005f), "A stem's root should be saved where it was.");
                Assert.That(Vector2.Distance(after[after.Count - 1], before[before.Count - 1]), Is.LessThan(0.005f), "...and its tip.");
            }
        }
    }
}
