using System;
using System.Collections.Generic;
using CharacterShowcase.EditorTools.Level;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CharacterShowcase.Tests.EditMode
{
    /// <summary>
    /// Checks of level specs (LevelSpec, LevelSpecTools): every level recorded from its builder, written as JSON, read
    /// back and built again builds the same level, object for object; a hand-written spec builds what it lists (State
    /// entries included); mistakes in a spec are reported with where they are; numbers round-trip exactly; and the
    /// schema covers every piece. Each builds in a new scene, replacing what's open.
    /// </summary>
    public class LevelSpecTests
    {
        [TearDown]
        public void TearDown() => EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        private static string Spec(string pieces) =>
            "{\"format\": \"character-showcase-level\", \"version\": 1, \"name\": \"Test Level\", \"scene\": \"Assets/Scenes/Test/Test.unity\", " +
            "\"parts\": [{\"name\": \"Part\", \"pieces\": [" + pieces + "]}]}";

        [TestCase("Proving Ground")]
        [TestCase("Level Playground")]
        [TestCase("Meridian Complex")]
        public void EachLevel_BuiltFromItsOwnSpec_IsTheSameLevel(string level)
        {
            List<string> differences = LevelSpecTools.CheckRoundTrip(level);
            Assert.IsEmpty(differences, $"'{level}' built from its spec differs from its builder's:\n{string.Join("\n", differences)}");
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
    }
}
