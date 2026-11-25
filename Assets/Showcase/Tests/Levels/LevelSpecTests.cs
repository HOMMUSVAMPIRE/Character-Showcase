using System;
using System.Collections.Generic;
using MeridianMotion.Showcase.Levels;
using MeridianMotion.Tests;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MeridianMotion.Showcase.Tests.Levels
{
    /// <summary>
    /// Checks of level specs (LevelSpec, LevelSpecTools): every level recorded from its builder, written as JSON, read
    /// back and built again builds the same level, object for object; the Tutorial Ring's spec (written by its script) builds,
    /// with its lessons and shortcuts; a hand-written spec builds what it lists (State
    /// entries included); mistakes in a spec are reported with where they are; numbers round-trip exactly; and the
    /// schema covers every piece. Each builds in a new scene, replacing what's open.
    /// </summary>
    [Category(TestArea.Levels)]
    [TestTiming]
    public class LevelSpecTests
    {
        [TearDown]
        public void TearDown() => EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        private static string Spec(string pieces) =>
            "{\"format\": \"character-showcase-level\", \"version\": 1, \"name\": \"Test Level\", \"scene\": \"Assets/Showcase/Scenes/Test/Test.unity\", " +
            "\"parts\": [{\"name\": \"Part\", \"pieces\": [" + pieces + "]}]}";

        [TestCase("Level Playground")]
        [TestCase("Meridian Complex")]
        public void EachLevel_BuiltFromItsOwnSpec_IsTheSameLevel(string level)
        {
            List<string> differences = LevelSpecTools.CheckRoundTrip(level);
            Assert.IsEmpty(differences, $"'{level}' built from its spec differs from its builder's:\n{string.Join("\n", differences)}");
        }

        [Test]
        public void TheTutorialRingSpec_Builds_WithItsLessonsAndShortcuts()
        {
            string json = System.IO.File.ReadAllText("Assets/Showcase/Levels/TutorialRing.level.json");
            Transform root = LevelSpecTools.BuildForTest(json);
            var names = new List<string>();
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true)) { names.Add(t.name); }
            Assert.That(names.FindAll(n => n.StartsWith("Lesson Zone |")).Count, Is.EqualTo(18), "Each of the eighteen lessons should have its zone.");
            Assert.That(names.FindAll(n => n.StartsWith("Checkpoint |") && n.Contains("start=1")).Count, Is.EqualTo(1), "One start checkpoint.");
            Assert.That(names.FindAll(n => n.StartsWith("Shortcut Gate |")).Count, Is.EqualTo(2));
            Assert.That(names.FindAll(n => n.StartsWith("Shortcut Trigger |")).Count, Is.EqualTo(2));
            Assert.That(names.Exists(n => n.StartsWith("Hazard Volume")), Is.True);
        }

        [Test]
        public void AHandWrittenSpec_BuildsWhatItLists()
        {
            Transform root = LevelSpecTools.BuildForTest(Spec(
                "{\"piece\": \"Group\", \"parent\": \"\", \"name\": \"Zone\"}," +
                "{\"piece\": \"Block\", \"parent\": \"Zone\", \"name\": \"Deck\", \"x0\": 0, \"x1\": 10, \"z0\": 0, \"z1\": 4, \"y0\": 0, \"y1\": 4, \"surface\": \"Steel\"}," +
                "{\"piece\": \"Flight\", \"parent\": \"Zone\", \"name\": \"Stair (Default)\", \"basePoint\": [12, 0], \"direction\": [0, 1], \"width\": 2, \"y0\": 0, \"y1\": 4}," +
                "{\"piece\": \"State\", \"stepRise\": 0.25, \"stepRun\": 0.5}," +
                "{\"piece\": \"Flight\", \"parent\": \"Zone\", \"name\": \"Stair (State)\", \"basePoint\": [16, 0], \"direction\": [0, 1], \"width\": 2, \"y0\": 0, \"y1\": 4}"));

            Transform deck = root.Find("Zone/Deck");
            Assert.IsNotNull(deck, "The block should be built in its group.");
            Bounds bounds = deck.GetComponent<Collider>().bounds;
            Assert.That(bounds.min.x, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(bounds.max.x, Is.EqualTo(10f).Within(1e-4f));
            Assert.That(bounds.max.y, Is.EqualTo(4f).Within(1e-4f));
            StringAssert.StartsWith("Level_Steel", deck.GetComponent<MeshRenderer>().sharedMaterial.name);

            int steps = root.Find("Zone/Stair (State)").GetComponents<BoxCollider>().Length;
            Assert.AreEqual(16, steps, "After the State entry, a 4 m flight should have 0.25 m steps.");
            Assert.AreNotEqual(steps, root.Find("Zone/Stair (Default)").GetComponents<BoxCollider>().Length,
                "Before it, the flight should have the generation settings' steps.");
        }

        [Test]
        public void Groups_WithTheSameName_AreToldApartByNumber()
        {
            Transform root = LevelSpecTools.BuildForTest(Spec(
                "{\"piece\": \"Group\", \"parent\": \"\", \"name\": \"G\"}," +
                "{\"piece\": \"Placed\", \"parent\": \"\", \"name\": \"G\", \"position\": [5, 0, 0]}," +
                "{\"piece\": \"Block\", \"parent\": \"G#2\", \"name\": \"B\", \"x0\": 0, \"x1\": 1, \"z0\": 0, \"z1\": 1, \"y0\": 0, \"y1\": 1}"));
            Assert.AreEqual(0, root.GetChild(0).childCount);
            Assert.AreEqual("B", root.GetChild(1).GetChild(0).name, "'G#2' should be the second group named G.");
            Assert.That(root.GetChild(1).GetChild(0).GetComponent<Collider>().bounds.center.x, Is.EqualTo(5.5f).Within(1e-4f));
        }

        [TestCase("{\"piece\": \"Teleporter\"}", "no piece 'Teleporter'")]
        [TestCase("{\"piece\": \"Block\", \"parent\": \"\", \"name\": \"B\", \"x0\": 0, \"x1\": 1, \"z0\": 0, \"z1\": 1, \"y0\": 0, \"y1\": 1, \"colour\": \"red\"}", "no setting 'colour'")]
        [TestCase("{\"piece\": \"Block\", \"parent\": \"\", \"name\": \"B\", \"x0\": 0, \"x1\": 1, \"z0\": 0, \"z1\": 1, \"y0\": 0}", "needs a 'y1'")]
        [TestCase("{\"piece\": \"Ramp\", \"parent\": \"\", \"name\": \"R\", \"start\": [0, 0, 0], \"direction\": [0, 1], \"width\": 2, \"length\": 8, \"y0\": 0, \"y1\": 2}", "should be 2 numbers")]
        [TestCase("{\"piece\": \"Block\", \"parent\": \"\", \"name\": \"B\", \"x0\": 0, \"x1\": 1, \"z0\": 0, \"z1\": 1, \"y0\": 0, \"y1\": 1, \"surface\": \"Marble\"}", "isn't one of")]
        [TestCase("{\"piece\": \"State\", \"stepHeight\": 0.3}", "no setting 'stepHeight'")]
        public void Mistakes_AreReportedWithWhereTheyAre(string piece, string message)
        {
            var e = Assert.Throws<FormatException>(() => LevelSpec.FromJson(Spec(piece)));
            StringAssert.Contains(message, e.Message);
            StringAssert.Contains("Part 'Part', piece 1", e.Message);
        }

        [Test]
        public void AParentThatIsntThere_IsReportedWhenBuilding()
        {
            var e = Assert.Throws<InvalidOperationException>(() => LevelSpecTools.BuildForTest(Spec(
                "{\"piece\": \"Block\", \"parent\": \"Nowhere\", \"name\": \"B\", \"x0\": 0, \"x1\": 1, \"z0\": 0, \"z1\": 1, \"y0\": 0, \"y1\": 1}")));
            StringAssert.Contains("nothing at 'Nowhere'", e.Message);
        }

        [Test]
        public void BadJson_IsReportedWithItsLine()
        {
            var e = Assert.Throws<FormatException>(() => LevelSpec.FromJson("{\n  \"format\": \"character-showcase-level\",\n  \"name\" \"x\"\n}"));
            StringAssert.Contains("line 3", e.Message);
        }

        [TestCase(0.1f)]
        [TestCase(7.4558444f)]
        [TestCase(-123456.7f)]
        [TestCase(1e-5f)]
        [TestCase(3e9f)]
        [TestCase(0f)]
        public void Numbers_ReadBackAsTheSameFloat(float value)
        {
            string text = LevelSpecJson.Write(new List<object> { value });
            var read = (List<object>)LevelSpecJson.Parse(text);
            Assert.AreEqual(value, (float)(double)read[0], $"'{text}' should read back as {value:R}.");
        }

        [Test]
        public void TheSchema_CoversEveryPiece()
        {
            var defs = (JsonObject)LevelSpecTools.Schema()["$defs"];
            foreach (string piece in LevelSpec.Pieces.Keys)
            {
                Assert.IsTrue(defs.ContainsKey("piece." + piece), $"The schema should describe '{piece}'.");
            }
            var platform = (JsonObject)defs["piece.Platform"];
            var required = (List<object>)platform["required"];
            CollectionAssert.Contains(required, "x0");
            CollectionAssert.DoesNotContain(required, "coping", "Settings with defaults aren't required.");
            Assert.AreEqual("Graphite", ((JsonObject)((JsonObject)platform["properties"])["coping"])["default"]);
        }
    
        private const string SkySpecHead = "{\"format\": \"character-showcase-level\", \"version\": 1, \"name\": \"Sky Test\", " +
                                           "\"scene\": \"Assets/Sky Test.unity\", ";

        [Test]
        public void ASpecsSky_AndKeepingItsPostProcessing_RoundTrip()
        {
            var spec = LevelSpec.FromJson(SkySpecHead + "\"sky\": \"Assets/Sky/Sky.mat\", \"keepPostProcessing\": true, \"parts\": []}");
            Assert.That(spec.Sky, Is.EqualTo("Assets/Sky/Sky.mat"));
            Assert.That(spec.KeepPostProcessing, Is.True);
            LevelSpec again = LevelSpec.FromJson(spec.ToJson());
            Assert.That(again.Sky, Is.EqualTo("Assets/Sky/Sky.mat"));
            Assert.That(again.KeepPostProcessing, Is.True);
        }

        [Test]
        public void ASpecWithoutThem_HasNeither_AndWritesNeither()
        {
            var spec = LevelSpec.FromJson(SkySpecHead + "\"parts\": []}");
            Assert.That(spec.Sky, Is.Null.Or.Empty);
            Assert.That(spec.KeepPostProcessing, Is.False);
            Assert.That(spec.ToJson(), Does.Not.Contain("\"sky\"").And.Not.Contain("keepPostProcessing"));
        }

        [Test]
        public void ASkyThatIsntAMaterial_IsRefused()
        {
            Assert.Throws<FormatException>(() => LevelSpec.FromJson(SkySpecHead + "\"sky\": \"Sky.png\", \"parts\": []}"));
        }

        private const string ProfileFolder = "Assets/__PostProcessingTest";
        private const string ProfilePath = ProfileFolder + "/Test_PostProcessing.asset";

        [TearDown]
        public void RemoveTestProfile() => UnityEditor.AssetDatabase.DeleteAsset(ProfileFolder);

        private static UnityEngine.Rendering.VolumeProfile MadeProfile()
        {
            if (!UnityEditor.AssetDatabase.IsValidFolder(ProfileFolder)) { UnityEditor.AssetDatabase.CreateFolder("Assets", "__PostProcessingTest"); }
            return LevelPostProcessing.Profile(ProfilePath, keepExisting: false);
        }

        [Test]
        public void KeptPostProcessing_IsLeftAsTuned()
        {
            UnityEngine.Rendering.VolumeProfile made = MadeProfile();
            Assert.That(made.TryGet(out UnityEngine.Rendering.Universal.Bloom bloom), "it's made with the level's grade");
            bloom.intensity.Override(3.5f);   // As a person tuning it would.
            UnityEditor.EditorUtility.SetDirty(made);
            UnityEditor.AssetDatabase.SaveAssets();

            UnityEngine.Rendering.VolumeProfile kept = LevelPostProcessing.Profile(ProfilePath, keepExisting: true);
            Assert.That(kept, Is.SameAs(made));
            Assert.That(kept.TryGet(out UnityEngine.Rendering.Universal.Bloom keptBloom) && keptBloom.intensity.value == 3.5f,
                        "the tuning is still there");
        }

        [Test]
        public void KeptPostProcessing_IsMadeWhereThereIsNone()
        {
            if (!UnityEditor.AssetDatabase.IsValidFolder(ProfileFolder)) { UnityEditor.AssetDatabase.CreateFolder("Assets", "__PostProcessingTest"); }
            UnityEngine.Rendering.VolumeProfile made = LevelPostProcessing.Profile(ProfilePath, keepExisting: true);
            Assert.That(made, Is.Not.Null);
            Assert.That(UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.VolumeProfile>(ProfilePath), Is.SameAs(made));
            Assert.That(made.TryGet(out UnityEngine.Rendering.Universal.Tonemapping _));
        }

        [Test]
        public void PostProcessingNotKept_IsMadeAgain()
        {
            UnityEngine.Rendering.VolumeProfile made = MadeProfile();
            made.TryGet(out UnityEngine.Rendering.Universal.Bloom bloom);
            bloom.intensity.Override(3.5f);
            UnityEngine.Rendering.VolumeProfile again = LevelPostProcessing.Profile(ProfilePath, keepExisting: false);
            Assert.That(again.TryGet(out UnityEngine.Rendering.Universal.Bloom newBloom) && newBloom.intensity.value != 3.5f,
                        "the builders' levels are graded afresh, as they were");
        }
}
}
