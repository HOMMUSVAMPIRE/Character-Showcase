using System.Collections;
using System.Linq;
using MeridianMotion.Showcase.Grass;
using MeridianMotion.Tests;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MeridianMotion.Showcase.Tests.Grass
{
    /// <summary>
    /// Tests of a grass field placing its blades (GrassField, GrassBlades.compute: ReferenceAssets/OpenTasks.md, O4, O9,
    /// O10), on a small field made in code with a camera of its own: it places blades where the camera looks at it and
    /// none where it looks away; each blade's place is its cell's, so a camera moving a little sees the same blades in
    /// the same places; blades are placed only where the mask has grass; a field without its shaders draws nothing,
    /// without errors; and a camera's blade lists grow to fit what it's asked for, and shrink once they've been roomy a
    /// while.
    /// </summary>
    [Category(TestArea.Levels)]
    [TestTiming]
    public class GrassFieldTests
    {
        private GameObject field, view;
        private Camera camera;
        private GrassField grass;
        private GrassStyle style;

        [SetUp]
        public void SetUp()
        {
            Assume.That(GrassField.Supported, "This platform can't run the grass's compute shaders.");
            view = new GameObject("Grass Test Camera");
            camera = view.AddComponent<Camera>();
            camera.targetTexture = new RenderTexture(320, 180, 24);
            Look(new Vector3(0f, 2f, -6f), 20f);

            // A style with every blade drawn near and none thinned, so what's placed doesn't hang on the distance.
            style = ScriptableObject.CreateInstance<GrassStyle>();
            style.spacing = 0.2f;
            style.drawDistance = 100f;
            style.detailDistance = 100f;
            style.thinFrom = 100f;
            style.spacingByQuality = new[] { 1f, 1f };
        }

        [TearDown]
        public void TearDown()
        {
            if (field != null) { Object.Destroy(field); }
            if (view != null)
            {
                RenderTexture target = camera.targetTexture;
                Object.Destroy(view);
                if (target != null) { target.Release(); }
            }
            if (style != null) { Object.Destroy(style); }
        }

        private void Look(Vector3 from, float pitch) => view.transform.SetPositionAndRotation(from, Quaternion.Euler(pitch, 0f, 0f));

        /// <summary>A field 8 m square round the origin, lawn where lawn says (x, z in metres from its corner), with its shaders or not.</summary>
        private void MakeField(System.Func<float, float, bool> lawn, bool shaders = true)
        {
            field = new GameObject("Grass");
            field.SetActive(false);
            grass = field.AddComponent<GrassField>();
            const int texels = 32;
            var density = new byte[texels * texels];
            for (int z = 0; z < texels; z++)
            {
                for (int x = 0; x < texels; x++) { density[z * texels + x] = lawn((x + 0.5f) * 0.25f, (z + 0.5f) * 0.25f) ? (byte)255 : (byte)0; }
            }
            grass.SetMask(new Vector2(-4f, -4f), 0.25f, new Vector2Int(texels, texels), new[] { new GrassField.Layer { height = 0f, density = density } });
            grass.Style = style;
#if UNITY_EDITOR
            if (shaders)
            {
                grass.SetShaders(UnityEditor.AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/Showcase/Grass/GrassBlades.compute"),
                    UnityEditor.AssetDatabase.LoadAssetAtPath<Shader>("Assets/Showcase/Grass/Grass.shader"));
            }
#endif
            field.SetActive(true);
            // In the editor a field finds its shaders itself as it's enabled; without them is without them.
            if (!shaders) { grass.SetShaders(null, null); }
        }

        private static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++) { yield return null; }
        }

        [UnityTest]
        public IEnumerator BladesArePlaced_WhereTheCameraLooks_AndNoneWhereItLooksAway()
        {
            MakeField((x, z) => true);
            yield return Frames(5);
            int seen = grass.NearRoots(camera).Length;
            Assert.That(seen, Is.GreaterThan(100), "Looking at the field, there should be blades.");
            Look(new Vector3(0f, 2f, -6f), -60f);
            yield return Frames(5);
            Assert.That(grass.NearRoots(camera).Length, Is.LessThan(seen / 20 + 1), "Looking away (up at the sky), there should be (almost) none.");
        }

        [UnityTest]
        public IEnumerator EachBladesPlaceIsItsCells_SoMovingALittleKeepsTheSameBlades()
        {
            MakeField((x, z) => true);
            Look(new Vector3(0f, 9f, -1f), 80f);
            yield return Frames(5);
            Vector3[] before = grass.NearRoots(camera).OrderBy(p => p.x).ThenBy(p => p.z).ToArray();
            Look(new Vector3(0.3f, 9f, -1.2f), 80f);
            yield return Frames(5);
            Vector3[] after = grass.NearRoots(camera).OrderBy(p => p.x).ThenBy(p => p.z).ToArray();
            Assert.That(before.Length, Is.GreaterThan(100));
            // Most of the field's in view both times (looking down from above): the blades both saw are the same ones.
            int same = before.Count(p => after.Any(q => (q - p).sqrMagnitude < 1e-8f));
            Assert.That(same, Is.GreaterThan(before.Length * 8 / 10), "The blades seen both times should be in exactly the same places.");
        }

        [UnityTest]
        public IEnumerator BladesArePlaced_OnlyWhereTheMaskHasGrass()
        {
            // Grass on the field's west half only (x from its corner under 4 m: world x under 0).
            MakeField((x, z) => x < 4f);
            Look(new Vector3(0f, 9f, -1f), 80f);
            yield return Frames(5);
            Vector3[] roots = grass.NearRoots(camera);
            Assert.That(roots.Length, Is.GreaterThan(100));
            Assert.That(roots.Max(p => p.x), Is.LessThan(0.25f), "No blade should be placed east of the lawn (bar the mask's blur over a texel).");
        }

        [UnityTest]
        public IEnumerator AFieldWithoutItsShaders_DrawsNothing_WithoutErrors()
        {
            MakeField((x, z) => true, shaders: false);
            yield return Frames(5);
            Assert.That(grass.Counts(camera), Is.Null, "Without its shaders, it shouldn't have placed anything for the camera.");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator ACamerasBladeLists_GrowToFitWhatItsAskedFor()
        {
            // Dense enough that the near list's starting size can't hold them: it grows, and then holds them all.
            style.spacing = 0.02f;
            MakeField((x, z) => true);
            Look(new Vector3(0f, 9f, -1f), 80f);
            yield return Frames(10);
            (uint near, uint far, int nearCapacity, int farCapacity)? counts = grass.Counts(camera);
            Assert.That(counts, Is.Not.Null);
            Assert.That(counts.Value.near, Is.GreaterThan(65536u), "Test setup: the camera should ask for more blades than the starting list holds.");
            Assert.That(counts.Value.nearCapacity, Is.GreaterThanOrEqualTo((int)counts.Value.near), "The near list should have grown to hold them.");
        }

        [UnityTest]
        public IEnumerator ACamerasBladeLists_ShrinkOnceTheyveBeenRoomyAWhile()
        {
            style.spacing = 0.02f;
            MakeField((x, z) => true);
            Look(new Vector3(0f, 9f, -1f), 80f);
            yield return Frames(10);
            int grown = grass.Counts(camera).Value.nearCapacity;
            // Far fewer blades: the list holds over four times what's asked for, and after a few seconds of that it halves.
            style.spacing = 0.2f;
            float until = Time.realtimeSinceStartup + 7f;
            while (Time.realtimeSinceStartup < until) { yield return null; }
            Assert.That(grass.Counts(camera).Value.nearCapacity, Is.LessThan(grown), "The near list should have shrunk.");
        }
    }
}
