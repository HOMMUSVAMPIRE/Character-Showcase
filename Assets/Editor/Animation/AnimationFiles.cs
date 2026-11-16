using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CharacterShowcase.EditorTools
{
    /// <summary>
    /// What the animation importers (QuaterniusAnimationImporter, MotionCaptureClipImporter) share: finding their files,
    /// and preparing each in turn, collecting what went wrong.
    /// </summary>
    internal static class AnimationFiles
    {
        /// <summary>Every model file (.fbx) in folder and its subfolders, in path order; none if there's no such folder.</summary>
        public static IEnumerable<string> FbxFilesIn(string folder) =>
            AssetDatabase.IsValidFolder(folder)
                ? AssetDatabase.FindAssets("t:Model", new[] { folder })
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Where(path => path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                    .Distinct()
                    .OrderBy(path => path, StringComparer.Ordinal)
                : Enumerable.Empty<string>();

        /// <summary>
        /// Prepares each file with prepare (which returns what went wrong, or null), logging and returning the problems,
        /// a line per file. With a progress title, shows a progress bar that can cancel the rest.
        /// </summary>
        public static List<string> PrepareEach(IEnumerable<string> files, Func<string, string> prepare, string logTag, string progressTitle = null)
        {
            var problems = new List<string>();
            List<string> paths = files.ToList();
            try
            {
                for (int i = 0; i < paths.Count; i++)
                {
                    if (progressTitle != null && EditorUtility.DisplayCancelableProgressBar(progressTitle, Path.GetFileName(paths[i]), (float)i / paths.Count)) { break; }
                    string problem = prepare(paths[i]);
                    if (problem == null) { continue; }
                    problems.Add($"{paths[i]}: {problem}");
                    Debug.LogWarning($"[{logTag}] {paths[i]}: {problem}");
                }
            }
            finally
            {
                if (progressTitle != null) { EditorUtility.ClearProgressBar(); }
            }
            return problems;
        }
    }
}
