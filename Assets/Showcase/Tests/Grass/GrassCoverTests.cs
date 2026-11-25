using System.Collections.Generic;
using MeridianMotion.Showcase.Grass;
using MeridianMotion.Tests;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using AllocIs = UnityEngine.TestTools.Constraints.Is;
using Is = NUnit.Framework.Is;

namespace MeridianMotion.Showcase.Tests.Grass
{
    /// <summary>
    /// Tests of asking a grass field how much grass there is somewhere (GrassField.CoverAt, CoverAtAny: the footsteps on
    /// grass ask it, since lawns have no collider): it's the mask's density there, none outside the mask or too far above
    /// the floor, in the field's own space; the highest of the fields; and asking allocates nothing. Only the mask on the
    /// CPU is read, so no compute shaders are needed.
    /// </summary>
    [Category(TestArea.Levels)]
    [TestTiming]
    public class GrassCoverTests
    {
        private readonly List<GameObject> made = new();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject obj in made) { Object.Destroy(obj); }
            made.Clear();
        }

        /// <summary>A field 4 m square (8 × 8 texels of 0.5 m from its corner at 0, 0), on a floor at height 0, with grass
        /// on its x &lt; 2 m half and none on the other.</summary>
        private GrassField Field(Vector3 at = default)
        {
            var obj = new GameObject("Grass Cover Test Field");
            made.Add(obj);
            obj.transform.position = at;
            GrassField field = obj.AddComponent<GrassField>();
            var density = new byte[64];
            for (int z = 0; z < 8; z++)
            {
                for (int x = 0; x < 4; x++) { density[z * 8 + x] = 255; }
            }
            field.SetMask(Vector2.zero, 0.5f, new Vector2Int(8, 8), new[] { new GrassField.Layer { height = 0f, density = density } });
            return field;
        }

        [Test]
        public void CoverAt_IsTheMasksDensity()
        {
            GrassField field = Field();
            Assert.That(field.CoverAt(new Vector3(0.5f, 0f, 0.5f)), Is.EqualTo(1f));
            Assert.That(field.CoverAt(new Vector3(3.5f, 0f, 0.5f)), Is.EqualTo(0f));
        }

        [Test]
        public void CoverAt_OutsideTheMask_IsZero()
        {
            GrassField field = Field();
            Assert.That(field.CoverAt(new Vector3(-1f, 0f, 0f)), Is.EqualTo(0f));
            Assert.That(field.CoverAt(new Vector3(0.5f, 0f, 9f)), Is.EqualTo(0f));
        }

        [Test]
        public void CoverAt_TooFarAboveTheLayer_IsZero()
        {
            GrassField field = Field();
            Assert.That(field.CoverAt(new Vector3(0.5f, 2f, 0.5f)), Is.EqualTo(0f));
            Assert.That(field.CoverAt(new Vector3(0.5f, 0.3f, 0.5f)), Is.EqualTo(1f));
            Assert.That(field.CoverAt(new Vector3(0.5f, -0.5f, 0.5f)), Is.EqualTo(0f), "below the floor");
            Assert.That(field.CoverAt(new Vector3(0.5f, -0.01f, 0.5f)), Is.EqualTo(1f), "feet a hair into the floor are on it");
        }

        [Test]
        public void CoverAt_FollowsTheFieldsTransform()
        {
            GrassField field = Field(new Vector3(10f, 0f, 0f));
            Assert.That(field.CoverAt(new Vector3(10.5f, 0f, 0.5f)), Is.EqualTo(1f));
            Assert.That(field.CoverAt(new Vector3(0.5f, 0f, 0.5f)), Is.EqualTo(0f));
        }

        [Test]
        public void CoverAtAny_TakesTheHighestOfTheLiveFields()
        {
            Field();
            Field(new Vector3(-2f, 0f, 0f));
            // At x 1, the first field's grass and the second's bare half overlap: the grass wins.
            Assert.That(GrassField.CoverAtAny(new Vector3(1f, 0f, 0.5f)), Is.EqualTo(1f));
            Assert.That(GrassField.CoverAtAny(new Vector3(30f, 0f, 0.5f)), Is.EqualTo(0f));
        }

        [Test]
        public void CoverAt_AllocatesNothing()
        {
            GrassField field = Field();
            field.CoverAt(Vector3.one);
            GrassField.CoverAtAny(Vector3.one);
            Assert.That(() => { field.CoverAt(new Vector3(0.5f, 0f, 0.5f)); GrassField.CoverAtAny(Vector3.one); },
                        AllocIs.Not.AllocatingGCMemory());
        }
    }
}
