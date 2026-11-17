using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Character;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace CharacterShowcase.EditorTools
{
    // Running Tools/Mannequin/generate_mannequin.py in Blender to write the mannequin's FBX, and finding Blender. Other
    // generators (FoxGenerator) run their scripts the same way (RunBlenderScript).
    public static partial class MannequinGenerator
    {
        private const string BlenderPathPreference = "CharacterShowcase.BlenderPath";
        private static readonly string[] CommonBlenderPaths =
        {
            "/Applications/Blender.app/Contents/MacOS/Blender",
            "/usr/bin/blender",
            "/snap/bin/blender",
        };

        /// <summary>Blender's program, where it was last located or in a usual place; null if it isn't found.</summary>
        internal static string FindBlender()
        {
            string saved = EditorPrefs.GetString(BlenderPathPreference, "");
            if (File.Exists(saved)) { return saved; }
            return CommonBlenderPaths.FirstOrDefault(File.Exists);
        }

        /// <summary>Asks for Blender's program and remembers it; null if none was picked.</summary>
        internal static string LocateBlender()
        {
            string picked = EditorUtility.OpenFilePanel("Locate Blender", Application.platform == RuntimePlatform.OSXEditor ? "/Applications" : "", "");
            if (string.IsNullOrEmpty(picked)) { return null; }
            // On macOS the file panel picks the app bundle; the executable is inside it.
            if (picked.EndsWith(".app")) { picked = Path.Combine(picked, "Contents/MacOS/Blender"); }
            if (!File.Exists(picked))
            {
                EditorUtility.DisplayDialog("Generate Mannequin", $"'{picked}' isn't the Blender program.", "OK");
                return null;
            }
            EditorPrefs.SetString(BlenderPathPreference, picked);
            return picked;
        }

        /// <summary>Runs the generator script in Blender, writing the mannequin's FBX. Returns whether it succeeded.</summary>
        private static bool RunBlender(string blenderPath) =>
            RunBlenderScript(blenderPath, GeneratorScript, ModelPath, "mannequin", "Generate Mannequin", nameof(MannequinGenerator));

        /// <summary>
        /// Runs a generator script (a path from the project's root) in Blender, writing the FBX at modelPath (an asset
        /// path). The script prints lines starting "[tag]", and "[tag] exported" once it's written the file. Returns
        /// whether it succeeded, logging Blender's output as from logName.
        /// </summary>
        internal static bool RunBlenderScript(string blenderPath, string script, string modelPath, string tag, string title, string logName)
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            string output = Path.Combine(projectRoot, modelPath);
            var start = new ProcessStartInfo
            {
                FileName = blenderPath,
                Arguments = $"-b --factory-startup -P \"{script}\" -- \"{output}\"",
                WorkingDirectory = projectRoot,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            start.EnvironmentVariables["PYTHONDONTWRITEBYTECODE"] = "1"; // Keep __pycache__ out of Tools.

            var log = new StringBuilder();
            try
            {
                if (!Application.isBatchMode) { EditorUtility.DisplayProgressBar(title, $"Generating the {tag} in Blender…", 0.3f); }
                using Process blender = Process.Start(start);
                blender.OutputDataReceived += (_, e) => { if (e.Data != null) { lock (log) { log.AppendLine(e.Data); } } };
                blender.ErrorDataReceived += (_, e) => { if (e.Data != null) { lock (log) { log.AppendLine(e.Data); } } };
                blender.BeginOutputReadLine();
                blender.BeginErrorReadLine();
                blender.WaitForExit();
                string text;
                lock (log) { text = log.ToString(); }
                if (blender.ExitCode != 0 || !text.Contains($"[{tag}] exported"))
                {
                    Debug.LogError($"[{logName}] Blender couldn't generate the {tag} (exit code {blender.ExitCode}):\n{Tail(text, 40)}");
                    return false;
                }
                Debug.Log($"[{logName}] Blender:\n{string.Join("\n", text.Split('\n').Where(l => l.StartsWith($"[{tag}]")))}");
                return true;
            }
            catch (System.ComponentModel.Win32Exception e)
            {
                Debug.LogError($"[{logName}] Couldn't run Blender at '{blenderPath}': {e.Message}");
                return false;
            }
            finally
            {
                if (!Application.isBatchMode) { EditorUtility.ClearProgressBar(); }
            }
        }

        private static string Tail(string text, int lines) => string.Join("\n", text.Split('\n').TakeLast(lines));
    }
}
