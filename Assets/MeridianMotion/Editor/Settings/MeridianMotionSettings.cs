using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MeridianMotion.EditorTools
{
    /// <summary>
    /// The project's Meridian Motion settings (Project Settings > Meridian Motion), kept in
    /// ProjectSettings/MeridianMotionSettings.asset: where the tools put what they make (prepared models, new characters,
    /// the water effects when they're missing), so nothing is written into the package, which may be read-only.
    /// </summary>
    [FilePath("ProjectSettings/MeridianMotionSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    public sealed class MeridianMotionSettings : ScriptableSingleton<MeridianMotionSettings>
    {
        /// <summary>Where the tools' output goes unless it's set: the project's Assets folder itself.</summary>
        public const string DefaultOutputFolder = "Assets";

        [Tooltip("The folder in Assets the tools put what they make in: prepared models (in Models/Humanoid and Models/Generic), " +
                 "characters made with New Character (in Characters), and the water effects if they're missing.")]
        [SerializeField] private string outputFolder = DefaultOutputFolder;

        /// <summary>The folder the tools' output goes in (Assets, or a folder in it), without a slash at the end.</summary>
        public static string OutputFolder
        {
            get
            {
                string folder = instance.outputFolder?.Trim().Replace('\\', '/').TrimEnd('/');
                return IsValidOutputFolder(folder) ? folder : DefaultOutputFolder;
            }
            set
            {
                string folder = value?.Trim().Replace('\\', '/').TrimEnd('/');
                folder = IsValidOutputFolder(folder) ? folder : DefaultOutputFolder;
                if (folder == instance.outputFolder) { return; }
                instance.outputFolder = folder;
                instance.Save(true);
            }
        }

        /// <summary>Whether folder is Assets or a path in it.</summary>
        public static bool IsValidOutputFolder(string folder) =>
            !string.IsNullOrEmpty(folder) && (folder == "Assets" || folder.StartsWith("Assets/")) && !folder.Contains("..");

        [SettingsProvider]
        private static SettingsProvider Provider() => new("Project/Meridian Motion", SettingsScope.Project)
        {
            label = "Meridian Motion",
            keywords = new HashSet<string> { "Meridian", "Motion", "character", "output", "folder" },
            guiHandler = _ =>
            {
                EditorGUILayout.LabelField("Where the tools put what they make", EditorStyles.boldLabel);
                EditorGUILayout.BeginHorizontal();
                string folder = EditorGUILayout.DelayedTextField(new GUIContent("Output Folder",
                    "Prepared models go in its Models/Humanoid and Models/Generic, new characters in its Characters."), OutputFolder);
                if (folder != OutputFolder) { OutputFolder = folder; }
                if (GUILayout.Button("Choose…", GUILayout.Width(80f)))
                {
                    string chosen = EditorUtility.OpenFolderPanel("Output Folder", OutputFolder, "");
                    string assets = Application.dataPath.Replace('\\', '/');
                    if (!string.IsNullOrEmpty(chosen) && chosen.StartsWith(assets)) { OutputFolder = "Assets" + chosen[assets.Length..]; }
                }
                EditorGUILayout.EndHorizontal();
            },
        };
    }
}
