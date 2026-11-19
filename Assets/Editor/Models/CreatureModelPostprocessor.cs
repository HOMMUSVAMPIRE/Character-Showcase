using System.Linq;
using Character;
using UnityEditor;

namespace CharacterShowcase.EditorTools
{
    /// <summary>
    /// Prepares creatures as soon as they're added to (or changed in) Assets/Models/Creatures, as the character models'
    /// postprocessor does humanoids, so nobody has to remember to run Prepare Creatures:
    /// - a model imported there for the first time is set up as a Generic rig straight away (a creature's skeleton isn't
    ///   a humanoid's), so its first import is the right one;
    /// - once the editor is idle after a model there is imported or moved, or a creature's setup or animation set changes,
    ///   CreatureModelImporter.PrepareChanged prepares whichever are new or changed (making a new one's setup, guessed).
    /// </summary>
    public class CreatureModelPostprocessor : AssetPostprocessor
    {
        private static bool scheduled;

        private void OnPreprocessModel()
        {
            if (!CreatureModelImporter.InCreaturesFolder(assetPath) || assetImporter is not ModelImporter importer || !importer.importSettingsMissing) { return; }
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importCameras = false;
            importer.importLights = false;
        }

        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            // Imports caused by the tools themselves are already being dealt with.
            if (CharacterModelImporter.IsBusy || scheduled) { return; }
            bool relevant = imported.Concat(moved).Any(path => CreatureModelImporter.InCreaturesFolder(path)
                && (AssetImporter.GetAtPath(path) is ModelImporter || IsSetupOrSet(path)));
            if (!relevant) { return; }
            // Not now: assets can't safely be changed while imports are being processed. Once the editor is idle.
            scheduled = true;
            EditorApplication.delayCall += PrepareWhenIdle;
        }

        private static bool IsSetupOrSet(string path)
        {
            System.Type type = AssetDatabase.GetMainAssetTypeAtPath(path);
            return type == typeof(CreatureSetup) || type == typeof(CharacterAnimationSet);
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
            CreatureModelImporter.PrepareChanged();
        }
    }
}
