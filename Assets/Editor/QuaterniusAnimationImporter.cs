using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CharacterShowcase.EditorTools
{
    /// <summary>
    /// Sets up the Quaternius Universal Animation Libraries in Assets/Animation/Quaternius (CC0, from quaternius.com)
    /// as Humanoid animations, so they play on any humanoid model. Each library is one file, a mannequin with every
    /// animation as a take of its own; these are the in-place versions (without root motion), as the character
    /// controller does the moving.
    ///
    /// As Quaternius's own Unity setup has it, the axis conversion is baked in and the file is a Humanoid; its avatar's
    /// reference pose is straightened into a T-pose as every model's is (CharacterModelImporter.EnforceTPose), so the
    /// clips are stored relative to it and play the same on every model. Each take becomes a clip named for it (the
    /// "Armature|Armature|" prefix dropped), looping if its name ends in "_Loop" (as Quaternius names them), with its
    /// root's rotation and position kept in the pose as authored, as the mannequin's own clips' are, so nothing moves
    /// the character but its controller.
    /// The library's T-pose take isn't a clip.
    /// </summary>
    public static class QuaterniusAnimationImporter
    {
        public const string LibraryFolder = "Assets/Animation/Quaternius";
        private const string TPoseTake = "A_TPose";

        [MenuItem("Tools/Character Showcase/Prepare Quaternius Animations", priority = 4)]
        public static void PrepareAllFromMenu()
        {
            List<string> problems = PrepareAll();
            if (problems.Count == 0) { Debug.Log($"[{nameof(QuaterniusAnimationImporter)}] Every library in {LibraryFolder} is set up."); }
        }

        /// <summary>For -executeMethod. Exits with code 1 if any library couldn't be set up.</summary>
        public static void PrepareAllFromCommandLine()
        {
            if (PrepareAll().Count > 0) { EditorApplication.Exit(1); }
        }

        public static IEnumerable<string> FindLibraryFiles() =>
            AssetDatabase.IsValidFolder(LibraryFolder)
                ? AssetDatabase.FindAssets("t:Model", new[] { LibraryFolder }).Select(AssetDatabase.GUIDToAssetPath)
                    .Where(p => p.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase)).OrderBy(p => p)
                : Enumerable.Empty<string>();

        /// <summary>Sets up every library that isn't already, and returns what went wrong, a line per library.</summary>
        public static List<string> PrepareAll()
        {
            var problems = new List<string>();
            foreach (string path in FindLibraryFiles())
            {
                string problem = Prepare(path);
                if (problem == null) { continue; }
                problems.Add($"{path}: {problem}");
                Debug.LogWarning($"[{nameof(QuaterniusAnimationImporter)}] {path}: {problem}");
            }
            return problems;
        }

        /// <summary>A clip in the libraries by name ("Walk_Loop"), or null if there's no such clip, or no libraries.</summary>
        public static AnimationClip FindClip(string name)
        {
            foreach (string path in FindLibraryFiles())
            {
                AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(c => c.name == name);
                if (clip != null) { return clip; }
            }
            return null;
        }

        /// <summary>Sets up one library as Humanoid animations if it isn't already. Returns what went wrong, or null.</summary>
        public static string Prepare(string path)
        {
            if (AssetImporter.GetAtPath(path) is not ModelImporter importer) { return "It isn't a model file."; }
            bool changed = false;
            void Set<T>(T current, T wanted, System.Action<T> apply)
            {
                if (!EqualityComparer<T>.Default.Equals(current, wanted)) { apply(wanted); changed = true; }
            }
            Set(importer.bakeAxisConversion, true, v => importer.bakeAxisConversion = v);
            Set(importer.importAnimation, true, v => importer.importAnimation = v);
            Set(importer.importCameras, false, v => importer.importCameras = v);
            Set(importer.importLights, false, v => importer.importLights = v);
            Set(importer.materialImportMode, ModelImporterMaterialImportMode.None, v => importer.materialImportMode = v);
            Set(importer.animationType, ModelImporterAnimationType.Human, v => importer.animationType = v);
            Set(importer.avatarSetup, ModelImporterAvatarSetup.CreateFromThisModel, v => importer.avatarSetup = v);
            if (changed) { importer.SaveAndReimport(); }

            // Unity maps the skeleton (its bones have the usual names) and makes the avatar on import; then its
            // reference pose is straightened into a T-pose, as every model's is.
            CharacterModelImporter.EnforceTPose(importer, path);

            ModelImporterClipAnimation[] takes = importer.defaultClipAnimations;
            if (takes == null || takes.Length == 0) { return "It has no animation."; }
            // Each clip's height offset is kept: MannequinGenerator sets it, to stand the clip on the mannequin's floor.
            Dictionary<string, float> offsets = (importer.clipAnimations ?? new ModelImporterClipAnimation[0])
                .GroupBy(c => c.name).ToDictionary(g => g.Key, g => g.First().heightOffset);
            ModelImporterClipAnimation[] wanted = takes
                .Where(take => ClipName(take.takeName) != TPoseTake)
                .Select(take => Clip(take, offsets.TryGetValue(ClipName(take.takeName), out float offset) ? offset : 0f))
                .ToArray();
            if (!SameClips(importer.clipAnimations, wanted))
            {
                importer.clipAnimations = wanted;
                importer.SaveAndReimport();
            }

            Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
            if (avatar == null || !avatar.isValid || !avatar.isHuman) { return "Unity couldn't make a Humanoid avatar from its skeleton."; }
            AnimationClip[] clips = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).ToArray();
            if (clips.Length != wanted.Length) { return $"Only {clips.Length} of its {wanted.Length} clips imported."; }
            AnimationClip notHuman = clips.FirstOrDefault(c => !c.humanMotion);
            return notHuman != null ? $"Its clip '{notHuman.name}' didn't import as a Humanoid clip." : null;
        }

        /// <summary>
        /// Raises or lowers clips by the given amounts (metres, as the library's mannequin is sized: the clips' root
        /// height offsets), on top of what they're raised by already, and reimports them. Returns whether any changed.
        /// </summary>
        public static bool RaiseClips(IReadOnlyDictionary<string, float> metres)
        {
            bool any = false;
            foreach (string path in FindLibraryFiles())
            {
                var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                ModelImporterClipAnimation[] clips = importer.clipAnimations;
                bool changed = false;
                for (int i = 0; i < clips.Length; i++)
                {
                    if (!metres.TryGetValue(clips[i].name, out float raise) || Mathf.Abs(raise) < 1e-4f) { continue; }
                    clips[i].heightOffset -= raise; // A positive offset lowers the clip.
                    changed = true;
                }
                if (!changed) { continue; }
                importer.clipAnimations = clips;
                importer.SaveAndReimport();
                any = true;
            }
            return any;
        }

        /// <summary>The clip's name from its take's: "Armature|Armature|Walk_Loop" is "Walk_Loop".</summary>
        public static string ClipName(string takeName) => takeName.Split('|').Last();

        private static ModelImporterClipAnimation Clip(ModelImporterClipAnimation take, float heightOffset)
        {
            string name = ClipName(take.takeName);
            return new ModelImporterClipAnimation
            {
                name = name,
                takeName = take.takeName,
                firstFrame = take.firstFrame,
                lastFrame = take.lastFrame,
                loopTime = name.EndsWith("_Loop"),
                // The root's rotation and position stay in the pose as authored, as the mannequin's own clips' do (so
                // they share its floor: based on the feet instead, they stood 4 cm lower): these are the in-place versions.
                lockRootRotation = true,
                keepOriginalOrientation = true,
                lockRootHeightY = true,
                keepOriginalPositionY = true,
                heightFromFeet = false,
                heightOffset = heightOffset,
                lockRootPositionXZ = true,
                keepOriginalPositionXZ = true,
                mirror = false,
            };
        }

        private static bool SameClips(ModelImporterClipAnimation[] current, ModelImporterClipAnimation[] wanted)
        {
            if (current == null || current.Length != wanted.Length) { return false; }
            for (int i = 0; i < wanted.Length; i++)
            {
                ModelImporterClipAnimation a = current[i], b = wanted[i];
                if (a.name != b.name || a.takeName != b.takeName || !Mathf.Approximately(a.firstFrame, b.firstFrame)
                    || !Mathf.Approximately(a.lastFrame, b.lastFrame) || a.loopTime != b.loopTime || a.lockRootRotation != b.lockRootRotation
                    || a.keepOriginalOrientation != b.keepOriginalOrientation || a.lockRootHeightY != b.lockRootHeightY
                    || a.keepOriginalPositionY != b.keepOriginalPositionY || a.heightFromFeet != b.heightFromFeet
                    || !Mathf.Approximately(a.heightOffset, b.heightOffset)
                    || a.lockRootPositionXZ != b.lockRootPositionXZ
                    || a.keepOriginalPositionXZ != b.keepOriginalPositionXZ || a.mirror != b.mirror) { return false; }
            }
            return true;
        }
    }
}
