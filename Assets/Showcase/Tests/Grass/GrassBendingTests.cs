using System.Collections;
using MeridianMotion.Tests;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

namespace MeridianMotion.Showcase.Tests.Grass
{
    /// <summary>
    /// Tests of the grass being pushed aside (GrassBender, GrassBending: ReferenceAssets/OpenTasks.md, O8), on a small
    /// grass field made in code with a camera looking at it: a bender pushes the grass at its feet out from them, at its
    /// height; with leg capsules (hitboxes), its lower legs push and its upper body doesn't; a moving one drags the grass
    /// along; the push springs back once it's gone; and it's kept for the height it was made at, so a lawn above or below
    /// isn't pushed.
    /// </summary>
    [Category(TestArea.Levels)]
    [TestTiming]
    public class GrassBendingTests
    {
        private GameObject field, view, bender;

        [SetUp]
        public void SetUp()
        {
            Assume.That(Showcase.Grass.GrassField.Supported, "This platform can't run the grass's compute shaders.");
            // Each test starts with no pushes (the map outlives a test, and its pushes spring back in real time).
            Showcase.Grass.GrassBending.Clear();
            view = new GameObject("Grass Test Camera") { tag = "MainCamera" };
            var camera = view.AddComponent<Camera>();
            view.transform.SetPositionAndRotation(new Vector3(0f, 3f, -6f), Quaternion.Euler(25f, 0f, 0f));
            camera.targetTexture = new RenderTexture(320, 180, 24);

            field = new GameObject("Grass");
            field.SetActive(false);
            var grass = field.AddComponent<Showcase.Grass.GrassField>();
            const int texels = 64;
            var density = new byte[texels * texels];
            for (int i = 0; i < density.Length; i++) { density[i] = 255; }
            grass.SetMask(new Vector2(-8f, -8f), 0.25f, new Vector2Int(texels, texels), new[] { new Showcase.Grass.GrassField.Layer { height = 0f, density = density } });
#if UNITY_EDITOR
            grass.SetShaders(UnityEditor.AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/Showcase/Grass/GrassBlades.compute"),
                UnityEditor.AssetDatabase.LoadAssetAtPath<Shader>("Assets/Showcase/Grass/Grass.shader"));
#endif
            field.SetActive(true);
        }

        [TearDown]
        public void TearDown()
        {
            if (bender != null) { Object.Destroy(bender); }
            if (field != null) { Object.Destroy(field); }
            if (view != null)
            {
                RenderTexture target = view.GetComponent<Camera>().targetTexture;
                Object.Destroy(view);
                if (target != null) { target.Release(); }
            }
        }

        private void AddBender(Vector3 feet)
        {
            bender = new GameObject("Grass Bender");
            bender.transform.position = feet;
            bender.AddComponent<Showcase.Grass.GrassBender>().radius = 0.4f;
        }

        /// <summary>The bend map's texel at a world point (x, z): its push and the height it was made at.</summary>
        private static Vector4 BendAt(float x, float z)
        {
            Texture map = Showcase.Grass.GrassBending.Map;
            Assert.That(map, Is.Not.Null, "There should be a bend map once there's a bender.");
            int resolution = Showcase.Grass.GrassBending.MapResolution;
            float size = Showcase.Grass.GrassBending.MapSize;
            int Texel(float w) => (int)(((w / size) % 1f + 1f) % 1f * resolution) % resolution;
            AsyncGPUReadbackRequest request = AsyncGPUReadback.Request(map, 0, Texel(x), 1, Texel(z), 1, 0, 1, TextureFormat.RGBAFloat);
            request.WaitForCompletion();
            Color c = request.GetData<Color>()[0];
            return new Vector4(c.r, c.g, c.b, c.a);
        }

        private static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++) { yield return null; }
        }

        [UnityTest]
        public IEnumerator ABenderPushesTheGrassOutFromItsFeet()
        {
            AddBender(new Vector3(0f, 0f, 0f));
            yield return Frames(5);
            Vector4 east = BendAt(0.3f, 0f), north = BendAt(0f, 0.3f);
            Assert.That(east.x, Is.GreaterThan(0.2f), "Grass east of the feet should be pushed east.");
            Assert.That(Mathf.Abs(east.y), Is.LessThan(east.x), "...not much north or south.");
            Assert.That(north.y, Is.GreaterThan(0.2f), "Grass north of the feet should be pushed north.");
            Assert.That(east.z, Is.EqualTo(0f).Within(0.05f), "The push should be kept for the height it was made at.");
            Vector4 far = BendAt(2f, 0f);
            Assert.That(new Vector2(far.x, far.y).magnitude, Is.LessThan(0.01f), "Grass beyond the bender's radius shouldn't be pushed.");
        }

        [UnityTest]
        public IEnumerator ThePushSpringsBackOnceTheBenderHasGone()
        {
            AddBender(new Vector3(0f, 0f, 0f));
            yield return Frames(5);
            float pushed = BendAt(0.3f, 0f).x;
            Object.Destroy(bender);
            float until = Time.time + 4f * Showcase.Grass.GrassBending.SpringBack;
            while (Time.time < until) { yield return null; }
            Assert.That(BendAt(0.3f, 0f).x, Is.LessThan(0.1f * pushed), "The push should have sprung back to almost nothing.");
        }

        [UnityTest]
        public IEnumerator ACharactersLowerLegsPush_NotItsUpperBody()
        {
            // A "character" of three hitbox capsules: a lower leg at x = 1, a foot at x = -1, and a chest over the middle.
            AddBender(new Vector3(0f, 0f, 0f));
            Capsule("Lower Leg", new Vector3(1f, 0.3f, 0f), 0.06f, 0.45f);
            Capsule("Foot", new Vector3(-1f, 0.05f, 0f), 0.05f, 0.1f);
            Capsule("Chest", new Vector3(0f, 1.3f, 0f), 0.2f, 0.6f);
            yield return Frames(40);
            Assert.That(bender.GetComponent<Showcase.Grass.GrassBender>().Legs.Count, Is.EqualTo(2), "The lower leg and the foot should push; the chest shouldn't.");
            Assert.That(BendAt(1.12f, 0f).x, Is.GreaterThan(0.2f), "Grass beside the leg should be pushed out from it.");
            Assert.That(BendAt(-1.12f, 0f).x, Is.LessThan(-0.2f), "Grass beside the foot should be pushed out from it.");
            Vector2 middle = BendAt(0f, 0.25f);
            Assert.That(middle.magnitude, Is.LessThan(0.01f), "Under the chest (and the bender's feet, which have legs), the grass shouldn't be pushed.");
            Assert.That(BendAt(1.12f, 0f).z, Is.EqualTo(0.3f - 0.225f).Within(0.08f), "The push should be kept for the leg's bottom.");
        }

        [UnityTest]
        public IEnumerator AMovingPusher_DragsTheGrassAlong()
        {
            AddBender(new Vector3(-1f, 0f, 0f));
            yield return null;
            // Walking +x at a brisk pace, past a point beside its path.
            float start = Time.time;
            while (bender.transform.position.x < 0.3f)
            {
                bender.transform.position = new Vector3(-1f + 2.5f * (Time.time - start), 0f, 0f);
                yield return null;
            }
            Vector4 beside = BendAt(0f, 0.2f), onThePath = BendAt(0f, 0f);
            // What stays is the push as it passed: beside the path, laid mostly the way it went, a little out from it.
            Assert.That(beside.x, Is.GreaterThan(0.5f), $"Beside the path, the grass should be laid the way it went ({beside})...");
            Assert.That(beside.y, Is.GreaterThan(0.02f), $"...and a little out from the path ({beside}).");
            Assert.That(onThePath.x, Is.GreaterThan(0.5f), $"On the path it's passed, the grass should be laid the way it went ({onThePath}).");
        }

        private void Capsule(string name, Vector3 at, float radius, float height)
        {
            var part = new GameObject(name) { layer = LayerMask.NameToLayer("CharacterHitbox") };
            part.transform.SetParent(bender.transform, false);
            part.transform.localPosition = at;
            var capsule = part.AddComponent<CapsuleCollider>();
            capsule.isTrigger = true;
            capsule.radius = radius;
            capsule.height = height;
            capsule.direction = 1;
        }

        [UnityTest]
        public IEnumerator APushIsKeptForItsHeight()
        {
            // A bender on a floor 4 m up: its push is kept at that height, so the grass on the lawn below (at 0)
            // isn't pushed by it (GrassBlades.compute compares the blade's height with the push's).
            AddBender(new Vector3(0f, 4f, 0f));
            yield return Frames(5);
            Vector4 bend = BendAt(0.3f, 0f);
            Assert.That(bend.z, Is.EqualTo(4f).Within(0.05f), "The push should be kept for the height it was made at.");
        }
    }
}
