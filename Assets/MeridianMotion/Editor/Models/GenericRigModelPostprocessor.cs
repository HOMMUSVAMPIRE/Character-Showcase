using System.Linq;
using MeridianMotion.Characters;
using UnityEditor;

namespace MeridianMotion.EditorTools
{
    /// <summary>
    /// Prepares generic-rig characters as soon as they're added to (or changed in) Models/Generic in the output folder, as the character models'
    /// postprocessor does humanoids, so nobody has to remember to run Prepare Generic Rig Models:
    /// - a model imported there for the first time is set up as a Generic rig straight away (a generic-rig character's skeleton isn't
    ///   a humanoid's), so its first import is the right one;
    /// - once the editor is idle after a model there is imported or moved, or a generic-rig character's setup or animation set changes,
    ///   GenericRigModelImporter.PrepareChanged prepares whichever are new or changed (making a new one's setup, guessed).
    /// </summary>
    public class GenericRigModelPostprocessor : AssetPostprocessor
    {
        private void OnPreprocessModel()
        {
            if (!GenericRigModelImporter.InModelsFolder(assetPath) || assetImporter is not ModelImporter importer || !importer.importSettingsMissing) { return; }
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importCameras = false;
            importer.importLights = false;
        }

        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            // Imports caused by the tools themselves are already being dealt with.
            if (ModelPreparation.IsBusy || ModelPreparation.IsPending(PrepareWhenIdle)) { return; }
            bool relevant = imported.Concat(moved).Any(path => GenericRigModelImporter.InModelsFolder(path)
                && (AssetImporter.GetAtPath(path) is ModelImporter || IsSetupOrSet(path)));
            if (!relevant) { return; }
            // Not now: assets can't safely be changed while imports are being processed. Once the editor is idle.
            ModelPreparation.WhenIdle(PrepareWhenIdle);
        }

        private static bool IsSetupOrSet(string path)
        {
            System.Type type = AssetDatabase.GetMainAssetTypeAtPath(path);
            return type == typeof(GenericRigSetup) || type == typeof(CharacterAnimationSet);
        }

        private static void PrepareWhenIdle()
        {
            GenericRigModelImporter.PrepareChanged();
        }
    }
}
