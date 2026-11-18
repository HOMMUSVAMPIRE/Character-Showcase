using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Character;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace CharacterShowcase.EditorTools
{
    // Remembering which models have been prepared (and from what), and adopting or moving assets an earlier preparation made.
    public static partial class CharacterModelImporter
    {
        /// <summary>
        /// Whether a model needs no preparing: its prepared assets are where they belong and ready to use, and neither its
        /// file nor the animation its ground offset was measured with (its controller and the clips it plays) has
        /// changed since it was last prepared on this machine. A model never prepared here (as after cloning the
        /// project, where the prepared assets come with it) counts as up to date.
        /// </summary>
        private static bool IsUpToDate(string modelPath, Dictionary<string, PreparedRecord> prepared, AnimatorController controller)
        {
            var source = AssetDatabase.LoadAssetAtPath<DirectCharacterModelSource>(DefinitionPathFor(modelPath, DisplayName(modelPath)));
            if (source == null || Validate(source) != null || ControllerOf(source) != controller) { return false; }
            var prefabSource = PrefabUtility.GetCorrespondingObjectFromOriginalSource(source.ModelPrefab);
            if (prefabSource == null || AssetDatabase.GetAssetPath(prefabSource) != modelPath) { return false; }
            return !prepared.TryGetValue(modelPath, out PreparedRecord recorded) || recorded.Equals(RecordFor(modelPath, ControllerOf(source)));
        }

        // Bump to re-prepare every model once, when what preparing does changes.
        private const string PreparationVersion = "6";
        // Per machine, like the rest of Library: which model files were prepared, their contents at the time, and the
        // animation they were prepared against.
        private const string PreparedHashesPath = "Library/CharacterShowcase/PreparedModels.txt";

        /// <summary>What a model was prepared from: its file (and this preparation's version), and its animation.</summary>
        public readonly struct PreparedRecord : System.IEquatable<PreparedRecord>
        {
            public PreparedRecord(string file, string animation)
            {
                File = file;
                Animation = animation;
            }

            /// <summary>A hash of the model file's contents and the version of this preparation.</summary>
            public string File { get; }
            /// <summary>
            /// A hash of the Animator Controller the model plays and everything it depends on (its clips, and the files
            /// they come from), which its ground offset is measured with.
            /// </summary>
            public string Animation { get; }

            public bool Equals(PreparedRecord other) => File == other.File && Animation == other.Animation;
            public override bool Equals(object obj) => obj is PreparedRecord other && Equals(other);
            public override int GetHashCode() => System.HashCode.Combine(File, Animation);
        }

        /// <summary>What preparing the model now would be preparing it from: see PreparedRecord.</summary>
        public static PreparedRecord RecordFor(string modelPath, RuntimeAnimatorController controller)
        {
            var hash = new Hash128();
            hash.Append(File.ReadAllBytes(modelPath));
            hash.Append(PreparationVersion);
            string controllerPath = controller != null ? AssetDatabase.GetAssetPath(controller) : null;
            string animation = string.IsNullOrEmpty(controllerPath) ? "none" : AssetDatabase.GetAssetDependencyHash(controllerPath).ToString();
            return new PreparedRecord(hash.ToString(), animation);
        }

        /// <summary>
        /// Whether the model file has changed since it was last prepared on this machine (false if it never was). Only
        /// the file: the mannequin is regenerated when this says so, which rebuilds its controller, so changes to the
        /// controller made by hand mustn't count.
        /// </summary>
        public static bool HasChangedSincePrepared(string modelPath) =>
            LoadPreparedRecords().TryGetValue(modelPath, out PreparedRecord recorded) && recorded.File != RecordFor(modelPath, null).File;

        /// <summary>Records a model as prepared as it is now, for tools that prepare a model themselves (a model generator).</summary>
        public static void MarkPrepared(string modelPath, RuntimeAnimatorController controller)
        {
            Dictionary<string, PreparedRecord> prepared = LoadPreparedRecords();
            prepared[modelPath] = RecordFor(modelPath, controller);
            SavePreparedRecords(prepared);
        }

        // One line per model: "file hash<tab>animation hash<tab>path". (Lines from before the animation was recorded
        // have no animation hash, so those models are prepared again once.)
        private static Dictionary<string, PreparedRecord> LoadPreparedRecords()
        {
            var records = new Dictionary<string, PreparedRecord>();
            if (!File.Exists(PreparedHashesPath)) { return records; }
            foreach (string line in File.ReadAllLines(PreparedHashesPath))
            {
                string[] parts = line.Split('\t');
                if (parts.Length == 3) { records[parts[2]] = new PreparedRecord(parts[0], parts[1]); }
                else if (parts.Length == 2) { records[parts[1]] = new PreparedRecord(parts[0], ""); }
            }
            return records;
        }

        private static void SavePreparedRecords(Dictionary<string, PreparedRecord> records)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(PreparedHashesPath));
            File.WriteAllLines(PreparedHashesPath, records.OrderBy(r => r.Key, System.StringComparer.Ordinal)
                .Select(r => $"{r.Value.File}\t{r.Value.Animation}\t{r.Key}"));
        }

        /// <summary>
        /// Moves a model prefab and source made for this model under other names to the names it should have, keeping
        /// their GUIDs so everything that refers to them still does. A prefab counts as made for the model if it's a
        /// variant of the model file; a source, if it names such a prefab.
        /// </summary>
        private static void AdoptExistingAssets(string modelPath, GameObject modelAsset, string prefabPath, string sourcePath, Result result)
        {
            string[] searchIn = { ModelsFolder };

            GameObject existingPrefab = AssetDatabase.FindAssets("t:Prefab", searchIn)
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => path != prefabPath)
                .Select(AssetDatabase.LoadAssetAtPath<GameObject>)
                .FirstOrDefault(prefab => prefab != null && PrefabUtility.GetCorrespondingObjectFromOriginalSource(prefab) == modelAsset);
            if (existingPrefab != null && AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
            {
                MoveAsset(AssetDatabase.GetAssetPath(existingPrefab), prefabPath, result);
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null || AssetDatabase.LoadAssetAtPath<DirectCharacterModelSource>(sourcePath) != null) { return; }
            DirectCharacterModelSource existingSource = AssetDatabase.FindAssets($"t:{nameof(DirectCharacterModelSource)}", searchIn)
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<DirectCharacterModelSource>)
                .FirstOrDefault(source => source != null && source.ModelPrefab == prefab);
            if (existingSource != null) { MoveAsset(AssetDatabase.GetAssetPath(existingSource), sourcePath, result); }
        }

        private static void MoveAsset(string from, string to, Result result)
        {
            string error = AssetDatabase.MoveAsset(from, to);
            if (string.IsNullOrEmpty(error)) { result.notes.Add($"Moved '{from}' to '{to}'."); }
            else { Debug.LogWarning($"[CharacterModelImporter] Couldn't move '{from}' to '{to}': {error}"); }
        }
    }
}
