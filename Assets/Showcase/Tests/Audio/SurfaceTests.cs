using System.Collections.Generic;
using MeridianMotion.Showcase.CharacterAudio;
using MeridianMotion.Showcase.Grass;
using MeridianMotion.Tests;
using NUnit.Framework;
using UnityEngine;

namespace MeridianMotion.Showcase.Tests.Audio
{
    /// <summary>
    /// Tests of finding the surface under the character (SurfaceFinder, SurfaceMap, SurfaceTag): a tag on the ground or
    /// its parents wins; a collider on a child with no renderer (as the graybox's are) takes its parent's material; grass
    /// under the feet is grass even on a mapped floor; an unmapped material, or no ground, is the fallback; and what a
    /// piece of ground is, is worked out once.
    /// </summary>
    [Category(TestArea.Audio)]
    [TestTiming]
    public class SurfaceTests
    {
        private readonly List<Object> made = new();
        private SurfaceMap map;
        private SoundSurface concrete, metal, grass, fallback;
        private Material concreteMaterial;

        private T Track<T>(T obj) where T : Object
        {
            made.Add(obj);
            return obj;
        }

        private SoundSurface Surface(string name)
        {
            SoundSurface surface = Track(ScriptableObject.CreateInstance<SoundSurface>());
            surface.SurfaceName = name;
            return surface;
        }

        [SetUp]
        public void SetUp()
        {
            concrete = Surface("Concrete");
            metal = Surface("Metal");
            grass = Surface("Grass");
            fallback = Surface("Fallback");
            concreteMaterial = Track(new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard")));
            map = Track(ScriptableObject.CreateInstance<SurfaceMap>());
            map.Set(new[] { new SurfaceMap.MaterialSurface { material = concreteMaterial, surface = concrete } }, fallback, grass);
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = made.Count - 1; i >= 0; i--) { if (made[i] != null) { Object.Destroy(made[i]); } }
            made.Clear();
        }

        /// <summary>A floor laid out as the graybox's solids are: a renderer, with its collider on a child.</summary>
        private Transform GrayboxFloor(Material material, out Transform collider)
        {
            GameObject floor = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            Object.DestroyImmediate(floor.GetComponent<Collider>());
            floor.GetComponent<Renderer>().sharedMaterial = material;
            var child = new GameObject("Collider", typeof(BoxCollider));
            child.transform.SetParent(floor.transform, false);
            collider = child.transform;
            return floor.transform;
        }

        [Test]
        public void ATag_WinsOverEverything()
        {
            Transform floor = GrayboxFloor(concreteMaterial, out Transform collider);
            var holder = Track(new GameObject("Tagged"));
            floor.SetParent(holder.transform);
            holder.AddComponent<SurfaceTag>().Surface = metal;
            Assert.That(new SurfaceFinder(map).Find(collider, Vector3.zero), Is.SameAs(metal));
        }

        [Test]
        public void AColliderChildWithNoRenderer_UsesItsParentsMaterial()
        {
            GrayboxFloor(concreteMaterial, out Transform collider);
            Assert.That(new SurfaceFinder(map).Find(collider, new Vector3(50f, 0f, 50f)), Is.SameAs(concrete));
        }

        [Test]
        public void GrassUnderTheFeet_IsGrass_EvenOnAConcreteFloor()
        {
            GrayboxFloor(concreteMaterial, out Transform collider);
            var obj = Track(new GameObject("Grass"));
            GrassField field = obj.AddComponent<GrassField>();
            var density = new byte[16];
            for (int i = 0; i < density.Length; i++) { density[i] = 255; }
            field.SetMask(Vector2.zero, 1f, new Vector2Int(4, 4), new[] { new GrassField.Layer { height = 0f, density = density } });
            var finder = new SurfaceFinder(map);
            Assert.That(finder.Find(collider, new Vector3(1f, 0f, 1f)), Is.SameAs(grass));
            Assert.That(finder.Find(collider, new Vector3(10f, 0f, 10f)), Is.SameAs(concrete), "off the mask");
        }

        [Test]
        public void AnUnmappedMaterial_OrNoGround_IsTheFallback()
        {
            Material other = Track(new Material(concreteMaterial));
            GrayboxFloor(other, out Transform collider);
            var finder = new SurfaceFinder(map);
            Assert.That(finder.Find(collider, Vector3.zero), Is.SameAs(fallback));
            Assert.That(finder.Find(null, Vector3.zero), Is.SameAs(fallback));
        }

        [Test]
        public void TheAnswerIsCached_PerGround()
        {
            Transform floor = GrayboxFloor(concreteMaterial, out Transform collider);
            var finder = new SurfaceFinder(map);
            Assert.That(finder.Find(collider, Vector3.zero), Is.SameAs(concrete));
            floor.GetComponent<Renderer>().sharedMaterial = Track(new Material(concreteMaterial));
            Assert.That(finder.Find(collider, Vector3.zero), Is.SameAs(concrete));
        }
    }
}
