using System.Linq;
using UnityEditor;

namespace CharacterShowcase.EditorTools
{
    /// <summary>
    /// Prepares character models as soon as they're added to (or changed in) Assets/Models/CharacterModels, so nobody
    /// has to remember to run Prepare Character Models:
    /// - a model imported for the first time is set up as a Humanoid straight away, so its first import is the right one
    /// - once the editor is idle after models there are imported, moved or deleted, or animation they play (an Animator
    ///   Controller, a clip, or a model source) changes, PrepareChanged prepares whichever are new or changed, so their
    ///   ground offsets are measured again, and reports sources whose models are gone
    /// The mannequin is the exception: its animation clips and the controller built from them come from Generate
    /// Mannequin, which the model pipeline runs again (from the file as it is) when the mannequin's file changes, through
    /// BeforePreparing.
    /// </summary>
    public class CharacterModelPostprocessor : AssetPostprocessor
    {
        private static bool scheduled;

        /// <summary>
        /// Called before changed models are prepared, once the editor's idle: for what has to happen first. The model
        /// pipeline (Assets/Editor/Pipeline) regenerates the mannequin here when its file has changed; without the pipeline,
        /// nothing is.
        /// </summary>
        public static event System.Action BeforePreparing;

        private static bool InModelsFolder(string path) => path.StartsWith(CharacterModelImporter.ModelsFolder + "/");

        private void OnPreprocessModel()
        {
            if (!InModelsFolder(assetPath) || assetImporter is not ModelImporter importer || !importer.importSettingsMissing) { return; }
            // As Prepare Character Models sets a model up the first time (see CharacterModelImporter.ConfigureImporter),
            // which it then leaves alone: set up as a Humanoid here without its axis conversion baked in, a model came out
            // turned round (Quaternius's male mannequin did), its skeleton hung under a node turned to convert axes.
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.bakeAxisConversion = true;
            importer.importCameras = false;
            importer.importLights = false;
        }

        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            // Imports caused by the tools themselves are already being dealt with.
            if (CharacterModelImporter.IsBusy || scheduled) { return; }
            bool relevant = imported.Concat(moved).Any(path => InModelsFolder(path) && AssetImporter.GetAtPath(path) is ModelImporter)
                            || deleted.Concat(movedFrom).Any(InModelsFolder)
                            // What the models' ground offsets are measured with: their controllers, the clips those play,
                            // and their sources (which can name another controller).
                            || imported.Any(AffectsPreparedAnimation);
            if (!relevant) { return; }

            // Not now: assets can't safely be changed while imports are being processed. Once the editor is idle.
            scheduled = true;
            EditorApplication.delayCall += PrepareWhenIdle;
        }

        private static bool AffectsPreparedAnimation(string path) =>
            path.EndsWith(".controller") || path.EndsWith(".overrideController") || path.EndsWith(".anim")
            || (InModelsFolder(path) && path.EndsWith(".asset"));

        private static void PrepareWhenIdle()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += PrepareWhenIdle; // Try again once that's over.
                return;
            }
            scheduled = false;
            if (CharacterModelImporter.IsBusy) { return; }

            // Whatever has to come first (the model pipeline, where it's in the project, regenerating a changed mannequin's
            // clips and controller from its file), then the models.
            BeforePreparing?.Invoke();
            CharacterModelImporter.PrepareChanged();
        }
    }
}
