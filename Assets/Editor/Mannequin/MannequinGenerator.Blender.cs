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
    // Running Tools/Mannequin/generate_mannequin.py in Blender to write the mannequin's FBX, and finding Blender.
    public static partial class MannequinGenerator
    {
        private const string BlenderPathPreference = "CharacterShowcase.BlenderPath";
        private static readonly string[] CommonBlenderPaths =
        {
            "/Applications/Blender.app/Contents/MacOS/Blender",
            "/usr/bin/blender",
            "/snap/bin/blender",
        };

        private static string FindBlender()
        {
            string saved = EditorPrefs.GetString(BlenderPathPreference, "");
            if (File.Exists(saved)) { return saved; }
            return CommonBlenderPaths.FirstOrDefault(File.Exists);
        }

        private static string LocateBlender()
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
        private static bool RunBlender(string blenderPath)
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            string output = Path.Combine(projectRoot, ModelPath);
            var start = new ProcessStartInfo
            {
                FileName = blenderPath,
                Arguments = $"-b --factory-startup -P \"{GeneratorScript}\" -- \"{output}\"",
                WorkingDirectory = projectRoot,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            start.EnvironmentVariables["PYTHONDONTWRITEBYTECODE"] = "1"; // Keep __pycache__ out of Tools/Mannequin.

            var log = new StringBuilder();
            try
            {
                if (!Application.isBatchMode) { EditorUtility.DisplayProgressBar("Generate Mannequin", "Generating the mannequin in Blender…", 0.3f); }
                using Process blender = Process.Start(start);
                blender.OutputDataReceived += (_, e) => { if (e.Data != null) { lock (log) { log.AppendLine(e.Data); } } };
                blender.ErrorDataReceived += (_, e) => { if (e.Data != null) { lock (log) { log.AppendLine(e.Data); } } };
                blender.BeginOutputReadLine();
                blender.BeginErrorReadLine();
                blender.WaitForExit();
                string text;
                lock (log) { text = log.ToString(); }
                if (blender.ExitCode != 0 || !text.Contains("[mannequin] exported"))
                {
                    Debug.LogError($"[MannequinGenerator] Blender couldn't generate the mannequin (exit code {blender.ExitCode}):\n{Tail(text, 40)}");
                    return false;
                }
                Debug.Log($"[MannequinGenerator] Blender:\n{string.Join("\n", text.Split('\n').Where(l => l.StartsWith("[mannequin]")))}");
                return true;
            }
            catch (System.ComponentModel.Win32Exception e)
            {
                Debug.LogError($"[MannequinGenerator] Couldn't run Blender at '{blenderPath}': {e.Message}");
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
