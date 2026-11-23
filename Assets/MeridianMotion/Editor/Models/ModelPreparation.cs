using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MeridianMotion.EditorTools
{
    /// <summary>
    /// What the two model importers (HumanoidModelImporter, GenericRigModelImporter) and their postprocessors share:
    /// - whether a tool is working on models (Busy), so the postprocessors leave the imports it causes alone;
    /// - running something once the editor is idle (WhenIdle), since assets can't safely be changed while imports are
    ///   being processed;
    /// - where a model's files are (NameOf, BesideModel, InFolders, ModelFilesIn);
    /// - the entry points' running and reporting (FromCommandLine, ShowSummary).
    /// What each model was prepared from is kept in a PreparedRecords file of each importer's.
    /// </summary>
    public static class ModelPreparation
    {
        // ---- Busy ----

        /// <summary>
        /// True while a tool is preparing models or generating one. The imports they cause are their own doing, so the
        /// postprocessors ignore them rather than preparing the same models again.
        /// </summary>
        public static bool IsBusy => busy > 0;
        private static int busy;

        /// <summary>Marks a tool as working on models until the returned object is disposed. See IsBusy.</summary>
        public static IDisposable Busy()
        {
            busy++;
            return new BusyScope();
        }

        private sealed class BusyScope : IDisposable
        {
            private bool disposed;
            public void Dispose()
            {
                if (disposed) { return; }
                disposed = true;
                busy--;
            }
        }

        // ---- Once the editor is idle ----

        // In the order they were asked for: the humanoids' before the generic rigs', say, if both were.
        private static readonly List<Action> pending = new();

        /// <summary>Whether action is waiting for the editor to be idle (see WhenIdle).</summary>
        public static bool IsPending(Action action) => pending.Contains(action);

        /// <summary>
        /// Runs action once the editor is idle: not playing or about to, compiling or importing. Asked for again before
        /// then, it still runs once. It doesn't run while a tool is busy with models (IsBusy): what it would have
        /// prepared is that tool's doing.
        /// </summary>
        public static void WhenIdle(Action action)
        {
            if (action == null || pending.Contains(action)) { return; }
            pending.Add(action);
            if (pending.Count == 1) { EditorApplication.delayCall += RunWhenIdle; }
        }

        private static void RunWhenIdle()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += RunWhenIdle; // Try again once that's over.
                return;
            }
            Action[] actions = pending.ToArray();
            pending.Clear();
            foreach (Action action in actions)
            {
                if (IsBusy) { continue; }
                action();
            }
        }

        // ---- Where things are ----

        /// <summary>
        /// The name a model goes by, which what's made for it is named after: its file's. It depends on nothing else (not
        /// on what else is in its folder), so adding or removing other models never renames a model's assets.
        /// </summary>
        public static string NameOf(string modelPath) => Path.GetFileNameWithoutExtension(modelPath);

        /// <summary>The folder a model file is in, without a slash at the end.</summary>
        public static string FolderOf(string modelPath) => Path.GetDirectoryName(modelPath)?.Replace('\\', '/');

        /// <summary>The path of a file named fileName in the model's folder.</summary>
        public static string BesideModel(string modelPath, string fileName) => $"{FolderOf(modelPath)}/{fileName}";

        /// <summary>Whether path is inside one of folders (not the folder itself).</summary>
        public static bool InFolders(string path, params string[] folders) =>
            !string.IsNullOrEmpty(path) && folders.Any(folder => !string.IsNullOrEmpty(folder) && path.StartsWith(folder + "/", StringComparison.Ordinal));

        /// <summary>
        /// AssetDatabase.FindAssets(filter) in folders: none, if there are none (given no folders, it would search the
        /// whole project).
        /// </summary>
        public static string[] FindIn(string filter, string[] folders)
        {
            string[] valid = folders.Where(folder => !string.IsNullOrEmpty(folder) && AssetDatabase.IsValidFolder(folder)).ToArray();
            return valid.Length == 0 ? Array.Empty<string>() : AssetDatabase.FindAssets(filter, valid);
        }

        /// <summary>Every model file (anything Unity imports as a model) in folders, sorted by path.</summary>
        public static IEnumerable<string> ModelFilesIn(params string[] folders) =>
            FindIn("t:Model", folders)
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => AssetImporter.GetAtPath(path) is ModelImporter)
                .Distinct()
                .OrderBy(path => path, StringComparer.Ordinal);

        // ---- Running and reporting ----

        /// <summary>
        /// For an importer's -executeMethod entry point: runs prepare, then exits (code 0, or 1 if it returned null, a
        /// result that didn't succeed, or threw).
        /// </summary>
        public static void FromCommandLine<T>(Func<IReadOnlyCollection<T>> prepare, Func<T, bool> succeeded)
        {
            bool ok;
            try
            {
                IReadOnlyCollection<T> results = prepare();
                ok = results != null && results.All(succeeded);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                ok = false;
            }
            CommandLine.Finish(ok);
        }

        /// <summary>Shows how preparing went, for an importer's menu item (in the editor only: not in batch mode).</summary>
        public static void ShowSummary(string title, string summary)
        {
            if (Application.isBatchMode) { return; }
            EditorUtility.DisplayDialog(title, summary, "OK");
        }
    }

    /// <summary>
    /// What each model an importer prepared was prepared from, by its model file's path: a record (a hash of what went
    /// into preparing it, which the importer makes), so a model is prepared again only when that changes. Kept per
    /// machine, like the rest of Library, one line per model: the record, a tab, the path. A record can hold tabs of its
    /// own (fields); the path is after the last one.
    /// </summary>
    public sealed class PreparedRecords
    {
        private readonly string file;
        private readonly Dictionary<string, string> records = new();

        private PreparedRecords(string file) => this.file = file;

        /// <summary>The records in file (none, if it isn't there).</summary>
        public static PreparedRecords Load(string file)
        {
            var loaded = new PreparedRecords(file);
            if (!File.Exists(file)) { return loaded; }
            foreach (string line in File.ReadAllLines(file))
            {
                int tab = line.LastIndexOf('\t');
                if (tab > 0 && tab < line.Length - 1) { loaded.records[line.Substring(tab + 1)] = line.Substring(0, tab); }
            }
            return loaded;
        }

        /// <summary>The record kept for the model at modelPath, if there is one.</summary>
        public bool TryGet(string modelPath, out string record) => records.TryGetValue(modelPath, out record);

        /// <summary>Keeps record for the model at modelPath (save to write it).</summary>
        public void Set(string modelPath, string record) => records[modelPath] = record;

        /// <summary>Forgets the model at modelPath (save to write it).</summary>
        public void Remove(string modelPath) => records.Remove(modelPath);

        /// <summary>Forgets the models whose files are gone.</summary>
        public void ForgetMissing()
        {
            foreach (string path in records.Keys.Where(path => !File.Exists(path)).ToList()) { records.Remove(path); }
        }

        /// <summary>Writes the records, sorted by path.</summary>
        public void Save()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file));
            File.WriteAllLines(file, records.OrderBy(r => r.Key, StringComparer.Ordinal).Select(r => $"{r.Value}\t{r.Key}"));
        }

        /// <summary>
        /// Keeps record for the model at modelPath in file at once (loading and saving the rest, less those whose models
        /// are gone: a test's, say).
        /// </summary>
        public static void Mark(string file, string modelPath, string record)
        {
            PreparedRecords loaded = Load(file);
            loaded.ForgetMissing();
            loaded.Set(modelPath, record);
            loaded.Save();
        }
    }
}
