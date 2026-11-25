using MeridianMotion.Showcase.Levels;
using MeridianMotion.Tests;
using NUnit.Framework;
using UnityEngine;

namespace MeridianMotion.Showcase.Tests.Levels
{
    /// <summary>
    /// Tests of the grass's mask the level builders lay over lawns (Graybox.LawnMask: ReferenceAssets/OpenTasks.md,
    /// O3, O6, O10): grass over lawn cells and none over paving; a lawn's corners cut across (as its mesh is), so an inside
    /// corner has grass and an outside one doesn't; thinning just in from the edge, full in the middle; the edge channel
    /// high at a lawn's edge and nothing well inside; and the density scaled by the piece's.
    /// </summary>
    [Category(TestArea.Levels)]
    [TestTiming]
    public class LawnMaskTests
    {
        private const float Cell = 1f;
        private static readonly int PerCell = Mathf.RoundToInt(Cell / Graybox.GrassTexel);

        /// <summary>A grid of cells nx by nz, lawn where lawn says.</summary>
        private static bool[,] Grid(int nx, int nz, System.Func<int, int, bool> lawn)
        {
            var grass = new bool[nx, nz];
            for (int i = 0; i < nx; i++) { for (int j = 0; j < nz; j++) { grass[i, j] = lawn(i, j); } }
            return grass;
        }

        private static (byte[] density, byte[] edge, Vector2Int size) Mask(bool[,] grass, float density = 1f)
        {
            var size = new Vector2Int(grass.GetLength(0) * PerCell, grass.GetLength(1) * PerCell);
            (byte[] d, byte[] e) = Graybox.LawnMask(grass, Cell, size, density);
            return (d, e, size);
        }

        /// <summary>The texel at a point (m from the grid's corner).</summary>
        private static int At(Vector2Int size, float x, float z) =>
            Mathf.FloorToInt(z / Graybox.GrassTexel) * size.x + Mathf.FloorToInt(x / Graybox.GrassTexel);

        [Test]
        public void LawnHasGrass_PavingHasNone()
        {
            // A 6 m lawn in the middle of a 10 m grid.
            (byte[] density, _, Vector2Int size) = Mask(Grid(10, 10, (i, j) => i >= 2 && i < 8 && j >= 2 && j < 8));
            Assert.That(density[At(size, 5f, 5f)], Is.EqualTo(255), "The lawn's middle should have the full density.");
            Assert.That(density[At(size, 0.5f, 5f)], Is.EqualTo(0), "Paving should have none.");
            Assert.That(density[At(size, 1.9f, 5f)], Is.EqualTo(0), "Just outside the lawn should have none.");
            Assert.That(density[At(size, 2.1f, 5f)], Is.LessThan(255), "Just inside the edge, the grass should thin out...");
            Assert.That(density[At(size, 2.6f, 5f)], Is.EqualTo(255), "...and be full a little way in.");
        }

        [Test]
        public void ALawnsCornersAreCutAcross_AsItsMeshIs()
        {
            // An L of lawn: cells (2..5, 2..3) and (2..3, 4..5), so the cell (4, 4) is paving in its inside corner.
            (byte[] density, _, Vector2Int size) = Mask(Grid(8, 8, (i, j) => i >= 2 && j >= 2 && (j < 4 && i < 6 || i < 4 && j < 6)));
            Assert.That(density[At(size, 4.1f, 4.1f)], Is.GreaterThan(0), "The inside corner should be filled across (with thinned grass).");
            Assert.That(density[At(size, 4.9f, 4.9f)], Is.EqualTo(0), "...but not the paving cell's far corner.");
            Assert.That(density[At(size, 2.05f, 2.05f)], Is.EqualTo(0), "An outside corner should be cut off.");
        }

        [Test]
        public void TheEdgeChannel_IsHighAtTheEdge_AndNothingWellInside()
        {
            (_, byte[] edge, Vector2Int size) = Mask(Grid(12, 12, (i, j) => i >= 1 && i < 11 && j >= 1 && j < 11));
            Assert.That(edge[At(size, 1.1f, 6f)], Is.GreaterThan(200), "At the lawn's edge, the edge channel should be high.");
            Assert.That(edge[At(size, 6f, 6f)], Is.EqualTo(0), "Well inside the lawn it should be nothing.");
            Assert.That(edge[At(size, 0.5f, 6f)], Is.EqualTo(0), "Off the lawn it should be nothing.");
        }

        [Test]
        public void ThePiecesDensity_ScalesTheGrass()
        {
            (byte[] density, _, Vector2Int size) = Mask(Grid(8, 8, (i, j) => true), 0.5f);
            Assert.That(density[At(size, 4f, 4f)], Is.EqualTo(128).Within(1), "Half the density should give half the grass.");
        }
    }
}
