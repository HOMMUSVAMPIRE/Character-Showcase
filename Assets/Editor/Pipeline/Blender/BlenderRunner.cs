using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using MeridianMotion.EditorTools;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace MeridianMotion.Showcase.Pipeline
{
    /// <summary>
    /// Finding Blender and running the generators' scripts in it (Generate Mannequin's Tools/Mannequin/generate_mannequin.py,
    /// Generate Fox's Tools/Fox/generate_fox.py), for any generator: each gives its own title and names.
    /// </summary>
    internal static class BlenderRunner
    {
        private const string BlenderPathPreference = "CharacterShowcase.BlenderPath";
        private static readonly string[] CommonBlenderPaths =
        {
            "/Applications/Blender.app/Contents/MacOS/Blender",
            "/usr/bin/blender",
            "/snap/bin/blender",
        };

        /// <summary>Blender's program, where it was last located or in a usual place; null if it isn't found.</summary>
        public static string Find()
        {
            string saved = EditorPrefs.GetString(BlenderPathPreference, "");
            if (File.Exists(saved)) { return saved; }
            return CommonBlenderPaths.FirstOrDefault(File.Exists);
        }

        /// <summary>Asks for Blender's program and remembers it; null if none was picked. title: the generator's, for the dialog.</summary>
        public static string Locate(string title)
        {
            string picked = EditorUtility.OpenFilePanel("Locate Blender", Application.platform == RuntimePlatform.OSXEditor ? "/Applications" : "", "");
            if (string.IsNullOrEmpty(picked)) { return null; }
            // On macOS the file panel picks the app bundle; the executable is inside it.
            if (picked.EndsWith(".app")) { picked = Path.Combine(picked, "Contents/MacOS/Blender"); }
            if (!File.Exists(picked))
            {
                EditorUtility.DisplayDialog(title, $"'{picked}' isn't the Blender program.", "OK");
                return null;
            }
            EditorPrefs.SetString(BlenderPathPreference, picked);
            return picked;
        }

        /// <summary>
        /// For a generator run from its menu: Blender, found, or else asked for (located, or the generator told to rebuild
        /// from the file it made before, existingFile, with blender null). False if the generator shouldn't run (cancelled).
        /// what: what it generates, for the dialog ("the mannequin and its animations").
        /// </summary>
        public static bool FindOrAsk(string title, string what, string existingFile, out string blender)
        {
            blender = Find();
            if (blender != null) { return true; }
            int choice = EditorUtility.DisplayDialogComplex(title,
                $"Blender is needed to generate {what}, but wasn't found. Locate it, or rebuild from the {existingFile} that's " +
                "already in the project.", "Locate Blender…", "Cancel", $"Use Existing {existingFile}");
            if (choice == 1) { return false; }
            if (choice == 2) { return true; }
            blender = Locate(title);
            return blender != null;
        }

        /// <summary>
        /// For a generator run with -executeMethod: Blender from -blender &lt;path&gt;, or found; null (blender) with
        /// -skipBlender, to rebuild from existingFile. False, logged as from logName, if it's needed and not found.
        /// </summary>
        public static bool FromCommandLine(string logName, string existingFile, out string blender)
        {
            bool skip = CommandLine.Has("-skipBlender");
            blender = skip ? null : CommandLine.Argument("-blender") ?? Find();
            if (blender != null || skip) { return true; }
            Debug.LogError($"[{logName}] Blender wasn't found. Pass -blender <path>, or -skipBlender to use the existing {existingFile}.");
            return false;
        }

        /// <summary>
        /// Runs a generator script (a path from the project's root) in Blender, writing the FBX at modelPath (an asset
        /// path). The script prints lines starting "[tag]", and "[tag] exported" once it's written the file. Returns
        /// whether it succeeded, logging Blender's output as from logName, its progress shown under title.
        /// </summary>
        public static bool RunScript(string blenderPath, string script, string modelPath, string tag, string title, string logName)
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
