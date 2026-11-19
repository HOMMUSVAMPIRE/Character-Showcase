using System.Linq;
using System.Reflection;
using MeridianMotion.EditorTools;
using NUnit.Framework;
using UnityEditor;

namespace MeridianMotion.Tests.EditMode.Spawning
{
    /// <summary>
    /// Tests of how the tools find the package's assets and where they put what they make (MeridianMotionAssets,
    /// MeridianMotionSettings): every asset the locator names is found, by its GUID, wherever it is; the tools' output
    /// follows the output folder; and nothing in Assets counts as read-only.
    /// </summary>
    [Category(TestArea.Spawning)]
    [TestTiming]
    public class PackageAssetsTests
    {
        private string outputFolder;

        [SetUp]
        public void NoteTheOutputFolder() => outputFolder = MeridianMotionSettings.OutputFolder;

        [TearDown]
        public void PutTheOutputFolderBack() => MeridianMotionSettings.OutputFolder = outputFolder;

        [Test]
        public void EveryAssetTheLocatorNames_IsFound()
        {
            PropertyInfo[] assets = typeof(MeridianMotionAssets).GetProperties(BindingFlags.Public | BindingFlags.Static)
                .Where(p => p.PropertyType == typeof(string)).ToArray();
            Assert.That(assets, Is.Not.Empty);
            foreach (PropertyInfo asset in assets)
            {
                string path = (string)asset.GetValue(null);
                Assert.That(AssetDatabase.GetMainAssetTypeAtPath(path), Is.Not.Null, $"{asset.Name} ('{path}') should be an asset in the project.");
            }
        }

        [Test]
        public void AnAssetsPath_IsWhereItsGuidIs_NotWhereItWasExpected()
        {
            string guid = AssetDatabase.AssetPathToGUID(MeridianMotionAssets.CharacterPrefab);
            Assert.That(MeridianMotionAssets.PathOf(guid, "Assets/Somewhere Else/Character.prefab"), Is.EqualTo(MeridianMotionAssets.CharacterPrefab));
            Assert.That(MeridianMotionAssets.PathOf("00000000000000000000000000000000", "Assets/Expected.prefab"), Is.EqualTo("Assets/Expected.prefab"),
                "An asset that isn't there is named by the path it was expected at.");
        }

        [Test]
        public void TheToolsOutput_FollowsTheOutputFolder()
        {
            MeridianMotionSettings.OutputFolder = "Assets/Zz My Game";
            Assert.That(HumanoidModelImporter.ModelsFolder, Is.EqualTo("Assets/Zz My Game/Models/Humanoid"));
            Assert.That(GenericRigModelImporter.ModelsFolder, Is.EqualTo("Assets/Zz My Game/Models/Generic"));
            Assert.That(NewCharacter.CharactersFolder, Is.EqualTo("Assets/Zz My Game/Characters"));
            Assert.That(WaterEffectAssets.PrefabFolder, Is.EqualTo("Assets/Zz My Game/Prefabs/Effects"));
            Assert.That(WaterEffectAssets.SplashPath, Is.EqualTo(MeridianMotionAssets.WaterSplashPrefab), "The package's own effects are used where they are.");
            Assert.That(HumanoidModelImporter.MannequinModelPath, Is.EqualTo(MeridianMotionAssets.MannequinModel), "The package's mannequin stays where it is.");
        }

        [Test]
        public void AnOutputFolderOutsideAssets_IsRefused()
        {
            MeridianMotionSettings.OutputFolder = "Packages/com.someone.thing";
            Assert.That(MeridianMotionSettings.OutputFolder, Is.EqualTo(MeridianMotionSettings.DefaultOutputFolder));
            MeridianMotionSettings.OutputFolder = "Assets/../Elsewhere";
            Assert.That(MeridianMotionSettings.OutputFolder, Is.EqualTo(MeridianMotionSettings.DefaultOutputFolder));
        }

        [Test]
        public void NothingInAssets_IsReadOnly()
        {
            Assert.That(MeridianMotionAssets.IsReadOnly(MeridianMotionAssets.CharacterPrefab), Is.False);
            Assert.That(MeridianMotionAssets.IsReadOnly("Packages/com.unity.inputsystem/package.json"), Is.True, "A registry package is read-only.");
        }
    }
}
