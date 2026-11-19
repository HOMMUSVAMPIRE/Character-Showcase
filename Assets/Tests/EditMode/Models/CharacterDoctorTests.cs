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
    /// check fails), and a character with no model source fails the check of it, saying what to do.
    /// </summary>
    [Category(TestArea.Models)]
    [TestTiming]
    public class CharacterDoctorTests
    {
        private const string Folder = "Assets/ZzCharacterDoctorTests";

        [TearDown]
        public void RemoveFolder() => AssetDatabase.DeleteAsset(Folder);

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
