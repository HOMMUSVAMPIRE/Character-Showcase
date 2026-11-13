using System.Collections.Generic;
using System.IO;
using System.Linq;
using CharacterShowcase.EditorTools.Level;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CharacterShowcase.Tests.EditMode
{
    /// <summary>
    /// Checks of the level editor's workings (LevelSpecEdit, LevelSpecPreview), without its window: moving a piece
    /// moves its positions and nothing else; a new piece of every kind is valid and builds; the preview knows which
    /// piece made each object; and rebuilding one edited piece leaves the level as building the edited spec afresh
    /// would, object for object (pieces with deferred touches, like spawn markers, included). Each builds in a new
    /// scene, replacing what's open.
    /// </summary>
    public class LevelEditorTests
    {
        private const string ProvingGround = "Assets/Levels/ProvingGround.level.json";

        [TearDown]
        public void TearDown() => EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        private static JsonObject Piece(string json) => (JsonObject)LevelSpecJson.Parse(json);

        private static LevelSpec LoadProvingGround()
        {
            if (!File.Exists(ProvingGround)) { Assert.Ignore("No Proving Ground spec exported."); }
            return LevelSpec.FromJson(File.ReadAllText(ProvingGround));
        }

        [Test]
        public void Moving_MovesPositionsNotDirectionsOrSizes()
        {
            JsonObject platform = Piece("{\"piece\": \"Platform\", \"parent\": \"\", \"name\": \"P\", \"x0\": 0, \"x1\": 4, \"z0\": 0, \"z1\": 2, \"y0\": 0, \"top\": 3}");
            Assert.IsTrue(LevelSpecEdit.Translate(platform, 10f, 1f, -5f));
            Assert.AreEqual("{\"piece\": \"Platform\", \"parent\": \"\", \"name\": \"P\", \"x0\": 10, \"x1\": 14, \"z0\": -5, \"z1\": -3, \"y0\": 1, \"top\": 4}", LevelSpecJson.Write(platform));

            JsonObject shuttle = Piece("{\"piece\": \"MovingPlatform\", \"parent\": \"\", \"label\": \"S\", \"top\": [0, 0, 0], \"size\": [4, 0.5, 4], \"travel\": [0, 0, 10], \"period\": 4}");
            LevelSpecEdit.Translate(shuttle, 1f, 2f, 3f);
            StringAssert.Contains("\"top\": [1, 2, 3], \"size\": [4, 0.5, 4], \"travel\": [0, 0, 10]", LevelSpecJson.Write(shuttle));

            JsonObject rail = Piece("{\"piece\": \"RailAround\", \"rails\": \"\", \"shape\": [[0, 0], [4, 0], [4, 4]], \"height\": 2, \"gaps\": [[[2, 0], 1.5]]}");
            LevelSpecEdit.Translate(rail, 1f, 0f, 1f);
            Assert.AreEqual("{\"piece\": \"RailAround\", \"rails\": \"\", \"shape\": [[1, 1], [5, 1], [5, 5]], \"height\": 2, \"gaps\": [[[3, 1], 1.5]]}",
                LevelSpecJson.Write(rail), "Outlines and gaps' centres move; gaps' widths don't.");

            JsonObject ramp = Piece("{\"piece\": \"Ramp\", \"parent\": \"\", \"name\": \"R\", \"start\": [0, 0], \"direction\": [0, 1], \"width\": 4, \"length\": 8, \"y0\": 0, \"y1\": 2}");
            LevelSpecEdit.Translate(ramp, 3f, 0f, 3f);
            StringAssert.Contains("\"start\": [3, 3], \"direction\": [0, 1], \"width\": 4, \"length\": 8", LevelSpecJson.Write(ramp));
        }

        [Test]
        public void ANewPieceOfEveryKind_IsValidAndBuilds()
        {
            var pieces = new List<string> { "{\"piece\": \"Group\", \"parent\": \"\", \"name\": \"Zone\"}" };
            foreach (string kind in LevelSpec.Pieces.Keys.Concat(new[] { LevelSpec.StatePiece }))
            {
                JsonObject piece = LevelSpecEdit.NewPiece(kind, "Zone", new Vector3(10f, 0f, 20f));
                Assert.IsNull(LevelSpec.Problem(piece), $"A new {kind}: {LevelSpecJson.Write(piece)}");
                pieces.Add(LevelSpecJson.Write(piece));
            }
            string json = "{\"format\": \"character-showcase-level\", \"name\": \"Every Piece\", \"scene\": \"Assets/Scenes/T/T.unity\", " +
                          "\"parts\": [{\"name\": \"All\", \"pieces\": [" + string.Join(",", pieces) + "]}]}";
            Transform root = LevelSpecTools.BuildForTest(json);
            Assert.Greater(root.Find("Zone").childCount, LevelSpec.Pieces.Count / 2, "Most pieces make something in their group.");
        }

        [Test]
        public void ThePreview_KnowsWhichPieceMadeWhat()
        {
            LevelSpec spec = LoadProvingGround();
            LevelSpecPreview preview = LevelSpecPreview.Build(spec);
            Assert.IsEmpty(preview.Problems, string.Join("\n", preview.Problems.Values));
            JsonObject platformA = spec.Parts.SelectMany(p => p.Pieces).First(p => p.TryGetValue("name", out object n) && (string)n == "Platform A (+4)");
            List<GameObject> made = preview.ObjectsOf(platformA);
            Assert.IsNotEmpty(made);
            Assert.AreSame(platformA, preview.PieceOf(made[0]));
            Assert.IsTrue(preview.BoundsOf(platformA, out Bounds b));
            Assert.That(b.max.y, Is.EqualTo(4f).Within(0.2f));
            Assert.IsTrue(preview.GroupPaths().Contains("P2 Gap Run/Stairs"));
            Assert.IsNotEmpty(preview.Footprints);
        }

        [Test]
        public void TheEditor_MovesAPieceAndUndoesIt()
        {
            LoadProvingGround();
            var window = ScriptableObject.CreateInstance<LevelEditorWindow>();
            try
            {
                window.OpenSpec(ProvingGround);
                string before = window.Json;
                window.Preview();
                Assert.IsNotNull(window.CurrentPreview);
                List<string> built = LevelSpecTools.Snapshot(window.CurrentPreview.Root);

                Assert.IsTrue(window.SelectPiece("Platform A (+4)"));
                window.MovePicked(new Vector3(2f, 0f, 0f));
                StringAssert.Contains("\"name\": \"Platform A (+4)\", \"x0\": 138, \"x1\": 150", window.Json);
                Assert.AreNotEqual(built, LevelSpecTools.Snapshot(window.CurrentPreview.Root));

                Undo.PerformUndo();
                Assert.AreEqual(before, window.Json, "Undo should put the spec back.");
                List<string> differences = LevelSpecTools.Compare(built, LevelSpecTools.Snapshot(window.CurrentPreview.Root));
                Assert.IsEmpty(differences, "Undo should put the preview back:\n" + string.Join("\n", differences));
            }
            finally
            {
                Object.DestroyImmediate(window);
            }
        }

        private static int Count(LevelEditorWindow window) => LevelSpec.FromJson(window.Json).Parts.Sum(p => p.Pieces.Count);

        private static LevelEditorWindow OpenWithPreview()
        {
            LoadProvingGround();
            var window = ScriptableObject.CreateInstance<LevelEditorWindow>();
            window.OpenSpec(ProvingGround);
            window.Preview();
            Assert.IsNotNull(window.CurrentPreview);
            return window;
        }

        [Test]
        public void TheEditor_AddsAPieceWhereItsPlaced_AndDeletesIt()
        {
            LevelEditorWindow window = OpenWithPreview();
            try
            {
                int before = Count(window);
                List<string> built = LevelSpecTools.Snapshot(window.CurrentPreview.Root);
                Assert.IsTrue(window.SelectPiece("Platform A (+4)"));

                // Placed on top of platform A (+4), in its group, after it.
                JsonObject block = window.AddPieceAt("Block", new Vector3(140f, 4f, 30f));
                Assert.AreEqual(before + 1, Count(window));
                Assert.AreSame(block, window.Picked, "The new piece should be picked.");
                StringAssert.Contains("{\"piece\": \"Block\", \"parent\": \"P2 Gap Run\", \"name\": \"New Block\", \"x0\": 140, \"x1\": 144, \"z0\": 30, \"z1\": 34, \"y0\": 4, \"y1\": 5}", window.Json);
                Assert.IsTrue(window.CurrentPreview.BoundsOf(block, out Bounds b), "The new piece should be built in the preview.");
                Assert.That(Vector3.Distance(b.center, new Vector3(142f, 4.5f, 32f)), Is.LessThan(0.01f));

                // Delete, as the Delete key does.
                Assert.IsTrue(window.RunCommand("SoftDelete"));
                Assert.AreEqual(before, Count(window));
                Assert.IsEmpty(window.CurrentPreview.ObjectsOf(block), "Its objects should be gone from the scene.");
                List<string> differences = LevelSpecTools.Compare(built, LevelSpecTools.Snapshot(window.CurrentPreview.Root));
                Assert.IsEmpty(differences, string.Join("\n", differences));

                // Undo brings it back.
                Undo.PerformUndo();
                Assert.AreEqual(before + 1, Count(window));
            }
            finally { Object.DestroyImmediate(window); }
        }

        [Test]
        public void TheEditor_DuplicatesThePickedPiece()
        {
            LevelEditorWindow window = OpenWithPreview();
            try
            {
                int before = Count(window);
                Assert.IsTrue(window.SelectPiece("Platform B (+4)"));
                Assert.IsTrue(window.RunCommand("Duplicate"));
                Assert.AreEqual(before + 1, Count(window));
                StringAssert.Contains("\"name\": \"Platform B (+4) (Copy)\", \"x0\": 138, \"x1\": 150, \"z0\": 45, \"z1\": 57", window.Json);
                Assert.IsNotEmpty(window.CurrentPreview.ObjectsOf(window.Picked));
            }
            finally { Object.DestroyImmediate(window); }
        }

        [Test]
        public void DeletingAPiecesObjectsFromTheScene_TakesItOutOfTheSpec()
        {
            LevelEditorWindow window = OpenWithPreview();
            try
            {
                int before = Count(window);
                Assert.IsTrue(window.SelectPiece("Platform C (+4)"));
                foreach (GameObject o in window.CurrentPreview.ObjectsOf(window.Picked)) { Object.DestroyImmediate(o); }
                window.SyncWithScene();
                Assert.AreEqual(before - 1, Count(window));
                StringAssert.DoesNotContain("Platform C (+4)\"", window.Json);
            }
            finally { Object.DestroyImmediate(window); }
        }

        [TestCase("Platform A (+4)")]
        [TestCase("Start South West")]
        [TestCase("Turntable")]
        public void RebuildingOneEditedPiece_IsTheSameAsBuildingTheEditedSpec(string name)
        {
            LevelSpec spec = LoadProvingGround();
            LevelSpecPreview preview = LevelSpecPreview.Build(spec);
            JsonObject piece = spec.Parts.SelectMany(p => p.Pieces)
                .First(p => (p.TryGetValue("name", out object n) && (string)n == name) || (p.TryGetValue("label", out object l) && (string)l == name));
            Assert.IsTrue(LevelSpecEdit.Translate(piece, 3f, 0.5f, -2f));
            Assert.IsTrue(preview.Rebuild(piece), "Only the piece should have been rebuilt.");
            List<string> incremental = LevelSpecTools.Snapshot(preview.Root);

            Transform fresh = LevelSpecTools.BuildForTest(spec.ToJson());
            List<string> differences = LevelSpecTools.Compare(LevelSpecTools.Snapshot(fresh), incremental);
            Assert.IsEmpty(differences, string.Join("\n", differences));
        }
    }
}
