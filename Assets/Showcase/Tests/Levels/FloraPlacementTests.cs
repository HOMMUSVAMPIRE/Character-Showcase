using System.Collections.Generic;
using System.Linq;
using MeridianMotion.Levels.EditorTools;
using MeridianMotion.Showcase.Levels;
using MeridianMotion.Tests;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MeridianMotion.Showcase.Tests.Levels
{
    /// <summary>
    /// Tests of the flora the level pieces place (Graybox.Flora.cs, Graybox.WallFlora.cs: ReferenceAssets/OpenTasks.md,
    /// category P): a tree is a flora prefab, its LODs cross-fading and casting shadows, its trunk's collider on it, not
    /// static-batched; the same seeds place the same plants the same way; the level's flora seed reshuffles the variants
    /// and leaves every plant where and as big as it was; widths vary within their range, and some plants are mirrored;
    /// without prefabs a tree is built faceted; and ivy goes only on plain walls, its leaves out from them. Skipped
    /// without the flora's prefabs (Generate Flora makes them).
    /// </summary>
    [Category(TestArea.Levels)]
    [TestTiming]
    public class FloraPlacementTests
    {
        private Transform root;
        private int savedSeed;

        [SetUp]
        public void SetUp()
        {
            Assume.That(AssetDatabase.IsValidFolder(Graybox.FloraFolder.TrimEnd('/')), "No flora prefabs: Generate Flora makes them.");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Graybox.Begin();
            Graybox.ClearDeferred();
            Graybox.ForgetFlora();
            savedSeed = Graybox.FloraSeed;
            Graybox.FloraSeed = 0;
            root = new GameObject("Flora Test").transform;
        }

        [TearDown]
        public void TearDown()
        {
            Graybox.FloraSeed = savedSeed;
            Graybox.ClearDeferred();
            Graybox.ForgetFlora();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        /// <summary>Count trees in a row under a fresh group, from one random seeded seed: the group.</summary>
        private Transform Trees(int count, int seed, string name = "Trees")
        {
            var group = new GameObject(name).transform;
            group.SetParent(root, false);
            var random = new System.Random(seed);
            for (int i = 0; i < count; i++) { Graybox.Tree(group, new Vector2(i * 6f, 0f), 0f, random); }
            return group;
        }

        /// <summary>The flora prefab instance in a tree (or a shrub's own object).</summary>
        private static GameObject PlantIn(Transform tree) =>
            tree.GetComponentsInChildren<Transform>(true).Select(t => t.gameObject).FirstOrDefault(PrefabUtility.IsOutermostPrefabInstanceRoot);

        private static string Source(GameObject plant) => PrefabUtility.GetCorrespondingObjectFromSource(plant).name;

        [Test]
        public void ATree_IsAFloraPrefab_CrossFadingAndCastingShadows_WithItsTrunksCollider_NotBatched()
        {
            Transform tree = Trees(1, 3).GetChild(0);
            Assert.That(tree.name, Is.EqualTo("Tree"));
            Assert.That(tree.GetComponent<BoxCollider>(), Is.Not.Null, "The trunk's collider should be on the tree.");
            GameObject plant = PlantIn(tree);
            Assert.That(plant, Is.Not.Null, "The tree should be a flora prefab's instance.");
            Assert.That(AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(plant)), Does.StartWith(Graybox.FloraFolder));
            var lods = plant.GetComponentInChildren<LODGroup>();
            Assert.That(lods, Is.Not.Null, "A flora prefab should have a LOD Group.");
            Assert.That(lods.fadeMode, Is.EqualTo(LODFadeMode.CrossFade), "Its LODs should cross-fade, not pop.");
            foreach (LOD lod in lods.GetLODs())
            {
                foreach (Renderer r in lod.renderers.Where(r => r != null))
                {
                    // Every LOD casts (P7): far trees without shadows looked like they floated.
                    Assert.That(r.shadowCastingMode, Is.Not.EqualTo(UnityEngine.Rendering.ShadowCastingMode.Off), $"{r.name} should cast a shadow.");
                }
            }
            foreach (Transform part in plant.GetComponentsInChildren<Transform>(true))
            {
                Assert.That(GameObjectUtility.GetStaticEditorFlags(part.gameObject) & StaticEditorFlags.BatchingStatic, Is.EqualTo((StaticEditorFlags)0),
                    $"{part.name} shouldn't be static-batched: copies of a prefab are drawn instanced.");
            }
        }

        [Test]
        public void TheSameSeeds_PlaceTheSamePlants_TheSameWay()
        {
            List<string> Describe(Transform group) => Enumerable.Range(0, group.childCount).Select(i => PlantIn(group.GetChild(i))).Select(p =>
                $"{Source(p)} {p.transform.position} {p.transform.rotation.eulerAngles} {p.transform.lossyScale}").ToList();
            List<string> first = Describe(Trees(20, 11, "First"));
            Graybox.ForgetFlora();
            List<string> second = Describe(Trees(20, 11, "Second"));
            Assert.That(second, Is.EqualTo(first));
        }

        [Test]
        public void TheFloraSeed_ReshufflesTheVariants_LeavingEveryPlantWhereAndAsBigAsItWas()
        {
            Transform before = Trees(20, 5, "Before");
            Graybox.FloraSeed = 12345;
            Transform after = Trees(20, 5, "After");
            int changed = 0;
            for (int i = 0; i < 20; i++)
            {
                GameObject a = PlantIn(before.GetChild(i)), b = PlantIn(after.GetChild(i));
                Assert.That(b.transform.localPosition, Is.EqualTo(a.transform.localPosition), "A plant should stay where it was.");
                Assert.That(b.transform.localRotation.eulerAngles.y, Is.EqualTo(a.transform.localRotation.eulerAngles.y).Within(1e-3f), "...turned as it was.");
                // As tall: the height its first LOD's scaled to (each variant's own size, so compared by the world bounds).
                Assert.That(Height(b), Is.EqualTo(Height(a)).Within(0.02f), "...and as tall.");
                if (Source(a) != Source(b) || Mathf.Sign(a.transform.localScale.x) != Mathf.Sign(b.transform.localScale.x)) { changed++; }
            }
            Assert.That(changed, Is.GreaterThan(0), "Another flora seed should give some plants another variant, or mirror them.");
        }

        /// <summary>How tall a plant's first LOD is, as placed.</summary>
        private static float Height(GameObject plant)
        {
            Renderer[] first = plant.GetComponentInChildren<LODGroup>().GetLODs()[0].renderers.Where(r => r != null).ToArray();
            Bounds b = first[0].bounds;
            foreach (Renderer r in first) { b.Encapsulate(r.bounds); }
            return b.size.y;
        }

        [Test]
        public void Widths_VaryWithinTheirRange_AndSomePlantsAreMirrored()
        {
            Transform group = Trees(30, 9);
            int mirrored = 0;
            for (int i = 0; i < group.childCount; i++)
            {
                Vector3 scale = PlantIn(group.GetChild(i)).transform.localScale;
                Assert.That(Mathf.Abs(scale.x) / scale.y, Is.InRange(1f - Graybox.WidthVariation - 1e-4f, 1f + Graybox.WidthVariation + 1e-4f));
                Assert.That(Mathf.Abs(scale.x), Is.EqualTo(scale.z).Within(1e-5f), "A plant's width should be the same both ways.");
                if (scale.x < 0f) { mirrored++; }
            }
            Assert.That(mirrored, Is.InRange(1, group.childCount - 1), "Some plants should be mirrored, and some not.");
        }

        [Test]
        public void WithoutItsPrefabs_ATreeIsBuiltFaceted()
        {
            string[] trees = Graybox.Settings.foliage.trees;
            try
            {
                Graybox.Settings.foliage.trees = new[] { "No Such Plant" };
                Graybox.ForgetFlora();
                Transform tree = Trees(1, 3).GetChild(0);
                Assert.That(PlantIn(tree), Is.Null, "There's no prefab to place.");
                Assert.That(tree.GetComponent<MeshFilter>(), Is.Not.Null, "The tree should be built faceted instead.");
                Assert.That(tree.GetComponent<BoxCollider>(), Is.Not.Null, "...with its trunk's collider.");
            }
            finally { Graybox.Settings.foliage.trees = trees; }
        }

        [Test]
        public void Ivy_GoesOnlyOnPlainWalls_ItsLeavesOutFromThem()
        {
            // Two walls along x, their faces at z = 0 looking -z: a plain one (0 to 10), and a climbable panel (12 to 22).
            Wall("Wall", 0f, 10f);
            Wall(LevelFixtureSetup.ClimbablePrefix + " (Test)", 12f, 22f);
            Graybox.Ivy(root, new Vector2(0f, 0f), new Vector2(22f, 0f), new Vector2(0f, -1f), 0f, 4f, 1, cover: 1f);
            Graybox.RunDeferred();
            Transform ivy = root.Find("Ivy");
            Assert.That(ivy, Is.Not.Null, "The plain wall should have ivy.");
            // Each plant's first LOD (its billboard's crossed cards stand both sides of its middle).
            Renderer[] leaves = ivy.GetComponentsInChildren<LODGroup>().SelectMany(g => g.GetLODs()[0].renderers).Where(r => r != null).ToArray();
            Assert.That(leaves, Is.Not.Empty);
            foreach (Renderer r in leaves)
            {
                Assert.That(r.bounds.center.x, Is.LessThan(11f), $"{r.name} is on the climbable panel, or where there's no wall.");
                Assert.That(r.bounds.max.z, Is.LessThan(0.05f), $"{r.name} should be out in front of the wall, not in it.");
            }
        }

        /// <summary>A wall from x0 to x1, its face at z = 0 (it's 1 m thick behind it), 6 m tall, with a box collider.</summary>
        private void Wall(string name, float x0, float x1)
        {
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = name;
            wall.transform.SetParent(root, false);
            wall.transform.localPosition = new Vector3((x0 + x1) / 2f, 3f, 0.5f);
            wall.transform.localScale = new Vector3(x1 - x0, 6f, 1f);
        }
    }
}
