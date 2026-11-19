using System.Linq;
using MeridianMotion.Characters;
using MeridianMotion.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MeridianMotion.Tests.EditMode.Models
{
    /// <summary>
    /// Tests of the Character Doctor's checks (CharacterChecks): every character prefab in the project passes them (no
    /// check fails), and a character with no model source fails the check of it, saying what to do. A character whose
    /// hitbox layer the project hasn't got is warned about, with two fixes: adding the layer (in the first free user layer),
    /// or putting its hitboxes on a layer the project has (not Default, nor one its movement queries).
    /// </summary>
    [Category(TestArea.Models)]
    [TestTiming]
    public class CharacterDoctorTests
    {
        private const string Folder = "Assets/ZzCharacterDoctorTests";

        private const string TestLayer = "ZzDoctorTestLayer";
        private const string TagManagerPath = "ProjectSettings/TagManager.asset";
        // The project's Tags and Layers as the test found them: adding a layer saves them (in the editor's own format),
        // so they're written back as they were.
        private byte[] tagManager;

        [SetUp]
        public void NoteTheLayers() => tagManager = System.IO.File.ReadAllBytes(TagManagerPath);

        [TearDown]
        public void RemoveFolder()
        {
            AssetDatabase.DeleteAsset(Folder);
            if (!RemoveLayer(TestLayer)) { return; }
            // Saved now, so the editor has nothing left to save of them on quitting, then put back as they were.
            AssetDatabase.SaveAssets();
            System.IO.File.WriteAllBytes(TagManagerPath, tagManager);
        }

        /// <summary>A copy of the Character prefab, saved in the test's folder, with its CharacterModelSlot's Hitbox Layer set.</summary>
        private static GameObject CharacterWithHitboxLayer(string layer)
        {
            if (!AssetDatabase.IsValidFolder(Folder)) { AssetDatabase.CreateFolder("Assets", "ZzCharacterDoctorTests"); }
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(CharacterSceneSetup.CharacterPrefabPath));
            try
            {
                var slot = new SerializedObject(instance.GetComponent<CharacterModelSlot>());
                slot.FindProperty("hitboxLayer").stringValue = layer;
                slot.ApplyModifiedPropertiesWithoutUndo();
                return PrefabUtility.SaveAsPrefabAsset(instance, Folder + "/Hitbox Layer.prefab");
            }
            finally { Object.DestroyImmediate(instance); }
        }

        /// <summary>Takes the layer out of Tags and Layers, returning whether it was there.</summary>
        private static bool RemoveLayer(string layer)
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath(TagManagerPath)[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");
            bool removed = false;
            for (int i = 0; i < layers.arraySize; i++)
            {
                if (layers.GetArrayElementAtIndex(i).stringValue != layer) { continue; }
                layers.GetArrayElementAtIndex(i).stringValue = "";
                removed = true;
            }
            if (removed) { tagManager.ApplyModifiedPropertiesWithoutUndo(); }
            return removed;
        }

        [Test]
        public void AHitboxLayerTheProjectHasnt_IsWarnedAbout_AndAddingIt_FixesIt()
        {
            GameObject prefab = CharacterWithHitboxLayer(TestLayer);
            CharacterChecks.Check check = CharacterChecks.Run(prefab).Single(c => c.name == "Its hitboxes' layer");
            Assert.That(check.status, Is.EqualTo(CharacterChecks.CheckStatus.Warn));
            Assert.That(check.message, Does.Contain(TestLayer));
            Assert.That(check.fixLabel, Does.Contain(TestLayer), "The fix adds the layer it names.");
            check.fix();
            Assert.That(LayerMask.NameToLayer(TestLayer), Is.GreaterThanOrEqualTo(8), "It should be added as one of the project's own layers.");
            Assert.That(CharacterChecks.Run(prefab).Single(c => c.name == "Its hitboxes' layer").status, Is.EqualTo(CharacterChecks.CheckStatus.Pass));
        }

        [Test]
        public void AHitboxLayerTheProjectHasnt_CanBeSwappedForOneItHas()
        {
            GameObject prefab = CharacterWithHitboxLayer(TestLayer);
            CharacterChecks.Check check = CharacterChecks.Run(prefab).Single(c => c.name == "Its hitboxes' layer");
            int movementMask = prefab.GetComponent<MMotionMotor>().MovementLayerMask;
            Assert.That(check.choices, Does.Contain(CharacterHitboxes.LayerName), "The project's own hitbox layer is a choice.");
            Assert.That(check.choices, Does.Not.Contain("Default").And.Not.Contain("Ignore Raycast"));
            Assert.That(check.choices.Select(LayerMask.NameToLayer).Where(l => (movementMask & (1 << l)) != 0), Is.Empty,
                "No layer the movement queries is a choice.");
            check.choose("Water");
            Assert.That(prefab.GetComponent<CharacterModelSlot>().HitboxLayer, Is.EqualTo("Water"));
            Assert.That(CharacterChecks.Run(prefab).Single(c => c.name == "Its hitboxes' layer").status, Is.EqualTo(CharacterChecks.CheckStatus.Pass));
        }

        [Test]
        public void AHitboxLayerTheMovementQueries_IsWarnedAbout()
        {
            GameObject prefab = CharacterWithHitboxLayer("Default");
            Assert.That(CharacterChecks.Run(prefab).Single(c => c.name == "Its hitboxes' layer").status, Is.EqualTo(CharacterChecks.CheckStatus.Warn));
        }

        [Test]
        public void EveryCharacterPrefab_FailsNoCheck()
        {
            var prefabs = CharacterChecks.CharacterPrefabs();
            Assert.That(prefabs, Does.Contain(CharacterSceneSetup.CharacterPrefabPath), "Test setup: the Character prefab should be found.");
            foreach (string path in prefabs)
            {
                var failed = CharacterChecks.Run(AssetDatabase.LoadAssetAtPath<GameObject>(path)).Where(c => c.status == CharacterChecks.CheckStatus.Fail).ToList();
                Assert.That(failed, Is.Empty, $"{path}: " + string.Join("; ", failed));
            }
        }

        [Test]
        public void ACharacterWithNoModelSource_FailsThatCheck_SayingWhatToDo()
        {
            AssetDatabase.CreateFolder("Assets", "ZzCharacterDoctorTests");
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(CharacterSceneSetup.CharacterPrefabPath));
            try
            {
                var host = new SerializedObject(instance.GetComponent<CharacterModelSlot>());
                host.FindProperty("modelSource").objectReferenceValue = null;
                host.ApplyModifiedPropertiesWithoutUndo();
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(instance, Folder + "/No Model.prefab");
                CharacterChecks.Check check = CharacterChecks.Run(prefab).Single(c => c.name == "Its model source");
                Assert.That(check.status, Is.EqualTo(CharacterChecks.CheckStatus.Fail));
                Assert.That(check.message, Does.Contain("Model Source"));
            }
            finally { Object.DestroyImmediate(instance); }
        }
    }
}
