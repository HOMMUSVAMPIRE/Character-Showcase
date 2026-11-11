using System.Linq;
using UnityEditor;

namespace CharacterShowcase.EditorTools
{
    /// <summary>
    /// Prepares character models as soon as they're added to (or changed in) Assets/Models/CharacterModels, so nobody
    /// has to remember to run Prepare Character Models:
    /// - a model imported for the first time is set up as a Humanoid straight away, so its first import is the right one
    /// - once the editor is idle after models there are imported, moved or deleted, PrepareChanged prepares whichever
    ///   are new or changed, and reports sources whose models are gone
    /// The mannequin is the exception: its animation clips and the controller built from them come from Generate
    /// Mannequin, which is run again (from the file as it is) when the mannequin's file changes.
    /// </summary>
    public class CharacterModelPostprocessor : AssetPostprocessor
    {
        private static bool scheduled;

        private static bool InModelsFolder(string path) => path.StartsWith(CharacterModelImporter.ModelsFolder + "/");

        private void OnPreprocessModel()
        {
            if (!InModelsFolder(assetPath) || assetImporter is not ModelImporter importer || !importer.importSettingsMissing) { return; }
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importCameras = false;
            importer.importLights = false;
        }

        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            // Imports caused by the tools themselves are already being dealt with.
            if (CharacterModelImporter.IsBusy || scheduled) { return; }
            bool relevant = imported.Concat(moved).Any(path => InModelsFolder(path) && AssetImporter.GetAtPath(path) is ModelImporter)
                            || deleted.Concat(movedFrom).Any(InModelsFolder);
            if (!relevant) { return; }

            // Not now: assets can't safely be changed while imports are being processed. Once the editor is idle.
            scheduled = true;
            EditorApplication.delayCall += PrepareWhenIdle;
        }

        private static void PrepareWhenIdle()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += PrepareWhenIdle; // Try again once that's over.
                return;
            }
            scheduled = false;
            if (CharacterModelImporter.IsBusy) { return; }

            // The mannequin's clips and controller depend on its file, so a changed mannequin is regenerated from it.
            if (MannequinGenerator.NeedsRebuild()) { MannequinGenerator.Generate(blenderPath: null); }
            CharacterModelImporter.PrepareChanged();
        }
    }
}
