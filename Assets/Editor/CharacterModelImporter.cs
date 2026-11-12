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
    /// <summary>
    /// Imports and prepares every rigged model in Assets/Models/CharacterModels so the character can use it:
    /// - configures it as a Humanoid, so the mannequin's animations retarget onto it
    /// - puts the avatar's reference pose into a T-pose, so models rigged in an A-pose (arms angled down) don't play
    ///   every animation with their arms raised
    /// - straightens any rotated nodes above the skeleton (Blender's "Armature") in the avatar, and measures how far the
    ///   model's soles stand from its origin as it stands idle: so its feet rest on the ground at any size, even when
    ///   the character is stretched on one axis only
    /// - saves a model prefab next to it ("[name] Model.prefab"): the model with an Animator using the shared Animator
    ///   Controller, CharacterIK and FootPlacementIK
    /// - saves a DirectCharacterModelSource next to it ("[name].asset") naming the prefab and the shared translator,
    ///   which the Character Model window (or a CharacterModelHost) can then use
    /// Each subfolder can hold one model, or several (variants of one model with different materials, say); each model
    /// file becomes its own choice, named after the file. Safe to run repeatedly: IK settings and edits to the sources
    /// are kept, assets made under older names are moved to the current ones, and sources left over from models that
    /// are gone are reported. CharacterModelPostprocessor runs PrepareChanged by itself when models are imported.
    /// </summary>
    public static class CharacterModelImporter
    {
        public const string ModelsFolder = "Assets/Models/CharacterModels";
        // Shared by every model: Humanoid animation plays on any Humanoid model. Built by MannequinGenerator.
        public const string ControllerPath = "Assets/Animation/Mannequin.controller";
        public const string TranslatorPath = "Assets/Animation/Mannequin Animator Translator.asset";

        // Models outside this height range (in meters) get their definition's Scale set to bring them to TypicalHeight,
        // since that usually means the file's units were lost (a 180 m or 1.8 cm character).
        private const float MinHeight = 1f;
        private const float MaxHeight = 2.5f;
        private const float TypicalHeight = 1.8f;

        /// <summary>The outcome of preparing one model file.</summary>
        public sealed class Result
        {
            public string modelPath;
            public string name;
            public DirectCharacterModelSource definition;
            // Why the model couldn't be prepared, or null if it was
            public string problem;
            public float height;
            // Already prepared, and unchanged since, so it was left alone
            public bool upToDate;
            // Things worth knowing that aren't problems, such as assets moved to the model's current names
            public readonly List<string> notes = new();
            public bool Succeeded => problem == null;

            public override string ToString()
            {
                string text = !Succeeded ? $"'{name}' ({modelPath}): not prepared. {problem}"
                    : upToDate ? $"'{name}' ({modelPath}): up to date"
                    : $"'{name}' ({modelPath}): ready, {height:0.00} m tall, source '{AssetDatabase.GetAssetPath(definition)}'";
                return notes.Count == 0 ? text : $"{text} ({string.Join(" ", notes)})";
            }
        }

        [MenuItem("Tools/Character Showcase/Prepare Character Models", priority = 2)]
        public static void PrepareAllFromMenu()
        {
            List<Result> results = PrepareAll();
            if (results == null || Application.isBatchMode) { return; }
            string summary = results.Count == 0
                ? $"No model files were found in {ModelsFolder}. Put each model in a subfolder there."
                : string.Join("\n\n", results.Select(r => r.ToString()));
            EditorUtility.DisplayDialog("Prepare Character Models", summary, "OK");
        }

        /// <summary>
        /// True while a tool is preparing models or generating the mannequin. The imports they cause are their own doing,
        /// so CharacterModelPostprocessor ignores them rather than preparing the same models again.
        /// </summary>
        public static bool IsBusy => busy > 0;
        private static int busy;

        /// <summary>Marks a tool as working on models until the returned object is disposed. See IsBusy.</summary>
        public static System.IDisposable Busy()
        {
            busy++;
            return new BusyScope();
        }

        private sealed class BusyScope : System.IDisposable
        {
            private bool disposed;
            public void Dispose()
            {
                if (disposed) { return; }
                disposed = true;
                busy--;
            }
        }

        /// <summary>
        /// Prepares every model file in the models folder, whether or not it has changed, and reports assets left over
        /// from models that are gone. Returns null if the shared animation assets are missing.
        /// </summary>
        public static List<Result> PrepareAll() => Prepare(onlyChanged: false);

        /// <summary>
        /// Prepares the models in the models folder that are new or have changed since they were last prepared, or whose
        /// prepared assets are missing or broken, and reports assets left over from models that are gone. Run
        /// automatically by CharacterModelPostprocessor when models are imported. Returns null if the shared animation
        /// assets are missing.
        /// </summary>
        public static List<Result> PrepareChanged() => Prepare(onlyChanged: true);

        private static List<Result> Prepare(bool onlyChanged)
        {
            if (!LoadSharedAssets(out AnimatorController controller, out AnimatorParameterTranslator translator)) { return null; }
            var results = new List<Result>();
            Dictionary<string, PreparedRecord> prepared = LoadPreparedRecords();
            int modelCount = 0;
            using (Busy())
            {
                try
                {
                    string[] paths = FindModelFiles().ToArray();
                    var byPath = new Dictionary<string, Result>();
                    // Preparing one model can change what another was prepared against (re-preparing the mannequin
                    // reimports its clips, which every model's ground offset was measured with), so models found up to
                    // date are checked again after any model is prepared, until a pass prepares nothing more.
                    for (int pass = 0; pass < 2; pass++)
                    {
                        bool preparedAny = false;
                        for (int i = 0; i < paths.Length; i++)
                        {
                            if (byPath.TryGetValue(paths[i], out Result done) && !done.upToDate) { continue; }
                            if (onlyChanged && IsUpToDate(paths[i], prepared, controller))
                            {
                                byPath[paths[i]] = new Result { modelPath = paths[i], name = DisplayName(paths[i]), upToDate = true };
                                continue;
                            }
                            if (!Application.isBatchMode)
                            {
                                EditorUtility.DisplayProgressBar("Prepare Character Models", paths[i], (float)i / paths.Length);
                            }
                            byPath[paths[i]] = Prepare(paths[i], controller, translator);
                            preparedAny = true;
                        }
                        if (!onlyChanged || !preparedAny) { break; }
                        AssetDatabase.SaveAssets();
                    }
                    results.AddRange(paths.Select(path => byPath[path]));
                }
                finally
                {
                    if (!Application.isBatchMode) { EditorUtility.ClearProgressBar(); }
                }
                AssetDatabase.SaveAssets();

                // Check every prepared model once all of them are saved, the same way the Character Model window does,
                // in case preparing a later model affected an earlier one.
                // Recorded only now, once everything is saved and imported: what a model was prepared against (its
                // controller and clips) is only settled once every model, the mannequin included, is.
                foreach (Result result in results.Where(r => !r.upToDate))
                {
                    string problem = result.Succeeded ? Validate(result.definition) : result.problem;
                    if (problem == null && ControllerOf(result.definition) != controller) { problem = $"It doesn't use the shared Animator Controller '{ControllerPath}'."; }
                    if (problem != null)
                    {
                        result.problem = problem;
                        prepared.Remove(result.modelPath);
                    }
                    else
                    {
                        prepared[result.modelPath] = RecordFor(result.modelPath, ControllerOf(result.definition));
                    }
                }
                modelCount = results.Count;
                results.AddRange(FindOrphans(results));
            }
            prepared.Keys.Where(path => !File.Exists(path)).ToList().ForEach(path => prepared.Remove(path));
            SavePreparedRecords(prepared);

            bool anythingHappened = results.Any(r => !r.upToDate);
            if (onlyChanged && !anythingHappened) { return results; }
            var log = new StringBuilder($"[CharacterModelImporter] Prepared {results.Count(r => r.Succeeded && !r.upToDate)} " +
                                        $"of {modelCount} models in {ModelsFolder}:");
            foreach (Result result in results.Where(r => !onlyChanged || !r.upToDate)) { log.Append("\n  - ").Append(result); }
            if (results.All(r => r.Succeeded)) { Debug.Log(log); } else { Debug.LogWarning(log); }
            return results;
        }

        /// <summary>
        /// Model sources in the models folder that no model file accounts for: their model was deleted, or renamed
        /// outside Unity (which loses the link). They're reported, not deleted, since scenes may still use them.
        /// </summary>
        private static IEnumerable<Result> FindOrphans(List<Result> results)
        {
            var accountedFor = new HashSet<string>(results.Select(r => DefinitionPathFor(r.modelPath, r.name)));
            foreach (string guid in AssetDatabase.FindAssets($"t:{nameof(DirectCharacterModelSource)}", new[] { ModelsFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (accountedFor.Contains(path)) { continue; }
                var source = AssetDatabase.LoadAssetAtPath<DirectCharacterModelSource>(path);
                yield return new Result
                {
                    modelPath = path,
                    name = source != null ? source.name : Path.GetFileNameWithoutExtension(path),
                    definition = source,
                    problem = "No model file in the folder uses this source any more. Delete it (and its model prefab) once " +
                              "nothing refers to it, or put its model back.",
                };
            }
        }

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

        /// <summary>Records a model as prepared as it is now, for tools that prepare a model themselves (Generate Mannequin).</summary>
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

        public static bool LoadSharedAssets(out AnimatorController controller, out AnimatorParameterTranslator translator)
        {
            controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            translator = AssetDatabase.LoadAssetAtPath<AnimatorParameterTranslator>(TranslatorPath);
            if (controller != null && translator != null && controller.layers.Length > 0) { return true; }
            Debug.LogError($"[CharacterModelImporter] '{ControllerPath}' or '{TranslatorPath}' is missing or empty. Run Tools > Character " +
                           "Showcase > Generate Mannequin first: the mannequin's animations are the ones every model plays.");
            return false;
        }

        /// <summary>The model sources in the models folder that are ready to use (see Validate), sorted by path.</summary>
        public static IEnumerable<DirectCharacterModelSource> FindReadyDefinitions() =>
            AssetDatabase.FindAssets($"t:{nameof(DirectCharacterModelSource)}", new[] { ModelsFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(path => path, System.StringComparer.Ordinal)
                .Select(AssetDatabase.LoadAssetAtPath<DirectCharacterModelSource>)
                .Where(d => d != null && Validate(d) == null);

        /// <summary>Every model file (anything Unity imports as a model) in the models folder, sorted by path.</summary>
        public static IEnumerable<string> FindModelFiles() =>
            AssetDatabase.FindAssets("", new[] { ModelsFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => AssetImporter.GetAtPath(path) is ModelImporter)
                .Distinct()
                .OrderBy(path => path, System.StringComparer.Ordinal);

        /// <summary>
        /// The name a model goes by, which its prefab and source are named after: its file name. It depends on nothing
        /// else (not on what else is in its folder), so adding or removing other models never renames a model's assets
        /// and leaves references to the old ones behind.
        /// </summary>
        public static string DisplayName(string modelPath) => Path.GetFileNameWithoutExtension(modelPath);

        public static string ModelPrefabPathFor(string modelPath, string name) =>
            $"{Path.GetDirectoryName(modelPath)?.Replace('\\', '/')}/{name} Model.prefab";

        public static string DefinitionPathFor(string modelPath, string name) =>
            $"{Path.GetDirectoryName(modelPath)?.Replace('\\', '/')}/{name}.asset";

        /// <summary>Imports one model as a Humanoid and saves its model prefab and definition.</summary>
        public static Result Prepare(string modelPath, AnimatorController controller, AnimatorParameterTranslator translator)
        {
            var result = new Result { modelPath = modelPath, name = DisplayName(modelPath) };
            if (AssetImporter.GetAtPath(modelPath) is not ModelImporter importer)
            {
                result.problem = "It isn't a model file.";
                return result;
            }
            var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (modelAsset == null || modelAsset.GetComponentInChildren<SkinnedMeshRenderer>(true) == null)
            {
                result.problem = "It has no skinned mesh, so it isn't rigged. Rig it (Mixamo can) and export it with its skeleton.";
                return result;
            }

            ConfigureImporter(importer);
            // Only set the reference pose of avatars made here: an avatar copied from another, or configured by hand in
            // the Rig tab, is left as it is.
            if (importer.avatarSetup == ModelImporterAvatarSetup.CreateFromThisModel) { EnforceTPose(importer, modelPath); }

            modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            Avatar avatar = AvatarOf(importer, modelPath);
            if (avatar == null || !avatar.isValid || !avatar.isHuman)
            {
                result.problem = "Unity couldn't map its skeleton to a Humanoid. Select the model, open the Rig tab and press " +
                                 "Configure to see which bones are missing, then run this again.";
                return result;
            }

            string prefabPath = ModelPrefabPathFor(modelPath, result.name);
            string definitionPath = DefinitionPathFor(modelPath, result.name);
            // Assets made for this model under another name (an older naming scheme, or before the file was renamed) are
            // moved to its current names rather than replaced, so whatever refers to them keeps working.
            AdoptExistingAssets(modelPath, modelAsset, prefabPath, definitionPath, result);
            GameObject modelPrefab = SaveModelPrefab(modelAsset, avatar, controller, prefabPath);
            // Read the prefab back as it was saved, and save it once more if its Animator didn't keep the avatar or the
            // controller, so a model is never reported ready without them.
            string saveProblem = SavedAnimatorProblem(prefabPath, avatar, controller);
            if (saveProblem != null)
            {
                modelPrefab = SaveModelPrefab(modelAsset, avatar, controller, prefabPath);
                saveProblem = SavedAnimatorProblem(prefabPath, avatar, controller);
            }
            if (saveProblem != null)
            {
                result.problem = $"Its model prefab '{prefabPath}' didn't keep its setup: {saveProblem}";
                return result;
            }
            result.height = MeasureHeight(modelPrefab);

            var definition = AssetDatabase.LoadAssetAtPath<DirectCharacterModelSource>(definitionPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<DirectCharacterModelSource>();
                AssetDatabase.CreateAsset(definition, definitionPath);
                if (result.height > 0f && (result.height < MinHeight || result.height > MaxHeight))
                {
                    definition.Scale = TypicalHeight / result.height;
                    Debug.LogWarning($"[CharacterModelImporter] '{result.name}' is {result.height:0.00} m tall, so its definition " +
                                     $"scales it by {definition.Scale:0.###} to {TypicalHeight} m. Change Scale on '{definitionPath}' if needed.");
                }
            }
            definition.ModelPrefab = modelPrefab;
            if (definition.Translator == null) { definition.Translator = translator; }
            // Measured on the prefab as saved, loaded afresh: the one in hand can still show the avatar as it was before
            // this run changed it.
            GameObject savedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (TryMeasureStanding(savedPrefab, ControllerOf(definition), definition.Avatar, out float groundOffset, out float animatedGroundOffset,
                    out StandingPose standing))
            {
                definition.GroundOffset = groundOffset;
                definition.AnimatedGroundOffset = animatedGroundOffset;
                definition.FacingCorrection = standing.FacingCorrection;
                definition.MeasureGroundOnLoad = false; // Measured: no need to measure each time it loads.
                if (standing.FacingCorrection != 0f)
                {
                    result.notes.Add($"It was made facing {-standing.FacingCorrection:0} degrees from forward (+Z), so its source turns it to face forward.");
                }
                float leftOver = Mathf.Abs(Mathf.DeltaAngle(standing.Facing + standing.FacingCorrection, 0f));
                if (leftOver > 20f)
                {
                    result.notes.Add($"It stands turned {leftOver:0} degrees from forward, even turned by quarter turns: set its source's " +
                                     "Rotation Offset to face it forward.");
                }
            }
            else
            {
                result.notes.Add("Couldn't measure how high its feet stand, so its Ground Offset was left as it was.");
            }
            string skew = RotatedAboveSkeleton(modelPrefab, definition.Avatar);
            if (skew != null)
            {
                result.notes.Add($"{skew} It's unevenly scaled too, so the rotation couldn't be taken out: stretching the character " +
                                 "on one axis will distort it. Export it without that rotation, or apply its scale.");
            }
            EditorUtility.SetDirty(definition);
            result.definition = definition;
            return result;
        }

        /// <summary>
        /// Why the definition can't be used as the character's model, or null if it can: it must name a model prefab with
        /// a valid Humanoid avatar, an Animator Controller and a translator, as Prepare sets it up.
        /// </summary>
        public static string Validate(DirectCharacterModelSource definition)
        {
            if (definition == null) { return "There is no definition."; }
            if (definition.ModelPrefab == null) { return "It names no model prefab."; }
            var animator = definition.ModelPrefab.GetComponentInChildren<Animator>(true);
            if (animator == null) { return "Its model has no Animator."; }
            Avatar avatar = definition.Avatar != null ? definition.Avatar : animator.avatar;
            if (avatar == null || !avatar.isValid || !avatar.isHuman) { return "Its model has no valid Humanoid avatar."; }
            RuntimeAnimatorController controller = ControllerOf(definition);
            if (controller == null) { return "Its model has no Animator Controller."; }
            if (controller is AnimatorController editable && editable.layers.Length == 0) { return "Its Animator Controller is empty."; }
            if (definition.Translator == null) { return "It names no animation translator."; }
            return null;
        }

        /// <summary>The Animator Controller the definition's model plays: the definition's override, or its prefab's.</summary>
        public static RuntimeAnimatorController ControllerOf(DirectCharacterModelSource definition)
        {
            if (definition == null) { return null; }
            if (definition.AnimatorController != null) { return definition.AnimatorController; }
            Animator animator = definition.ModelPrefab != null ? definition.ModelPrefab.GetComponentInChildren<Animator>(true) : null;
            return animator != null ? animator.runtimeAnimatorController : null;
        }

        /// <summary>
        /// Why the saved model prefab's Animator isn't set up with the given avatar and controller, or null if it is.
        /// Reimports the prefab first, so this checks what's on disk rather than what's in memory.
        /// </summary>
        private static string SavedAnimatorProblem(string prefabPath, Avatar avatar, AnimatorController controller)
        {
            AssetDatabase.ImportAsset(prefabPath, ImportAssetOptions.ForceUpdate);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Animator animator = prefab != null ? prefab.GetComponentInChildren<Animator>(true) : null;
            if (animator == null) { return "it has no Animator."; }
            if (animator.avatar != avatar) { return "its Animator has no avatar, or a different one."; }
            if (animator.runtimeAnimatorController != controller) { return $"its Animator doesn't use '{ControllerPath}'."; }
            return null;
        }

        private static void ConfigureImporter(ModelImporter importer)
        {
            bool changed = false;
            void Set<T>(T current, T wanted, System.Action<T> apply)
            {
                if (!Equals(current, wanted)) { apply(wanted); changed = true; }
            }
            if (importer.animationType != ModelImporterAnimationType.Human)
            {
                // A model being set up for the first time. Its axis conversion is baked in, so its skeleton isn't hung
                // under a node turned to convert axes: straightening such a node (StraightenAboveSkeleton) didn't put
                // Quaternius's female mannequin's avatar in a T-pose, and every animation held her arms overhead.
                // (Models already set up keep their setting.)
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.bakeAxisConversion = true;
                changed = true;
            }
            Set(importer.importCameras, false, v => importer.importCameras = v);
            Set(importer.importLights, false, v => importer.importLights = v);
            if (changed) { importer.SaveAndReimport(); }
        }

        private static Avatar AvatarOf(ModelImporter importer, string modelPath) =>
            importer.avatarSetup == ModelImporterAvatarSetup.CopyFromOther
                ? importer.sourceAvatar
                : AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<Avatar>().FirstOrDefault();

        /// <summary>
        /// Humanoid animation is stored relative to each avatar's T-pose, so an avatar whose reference pose isn't a T-pose
        /// plays every animation offset by the difference: a model rigged with its arms angled down holds them that much
        /// higher in every clip. This straightens the model's spine up, its arms out to the sides and its legs down, from
        /// its rest pose, and stores that as the avatar's reference pose (as the Rig tab's Enforce T-Pose does).
        ///
        /// The rest pose comes from the skin's bind poses rather than the pose the file's skeleton is saved in, which
        /// can be a frame of an animation: Blender's FBX export leaves the skeleton in the last clip's pose. An avatar
        /// made from a posed skeleton stores its own clips relative to that pose, so they look right on that model but
        /// lean or twist on every other one.
        /// Returns whether the reference pose changed.
        /// </summary>
        public static bool EnforceTPose(ModelImporter importer, string modelPath)
        {
            // Unity maps the bones automatically on import without saving the mapping, so read it from the avatar.
            HumanDescription description = importer.humanDescription;
            if (description.human == null || description.human.Length == 0)
            {
                Avatar avatar = AvatarOf(importer, modelPath);
                if (avatar == null || !avatar.isHuman) { return false; }
                description = avatar.humanDescription;
            }
            if (description.human == null || description.human.Length == 0 || description.skeleton == null) { return false; }

            var instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(modelPath));
            try
            {
                instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                Dictionary<string, Transform> transforms = new();
                foreach (Transform t in instance.GetComponentsInChildren<Transform>(true))
                {
                    transforms.TryAdd(t.name, t);
                }
                RestoreBindPose(instance);
                var boneNames = description.human.ToDictionary(h => h.humanName, h => h.boneName);
                Transform Bone(HumanBodyBones bone) =>
                    boneNames.TryGetValue(HumanTrait.BoneName[(int)bone], out string name) && transforms.TryGetValue(name, out Transform t) ? t : null;

                Transform leftLeg = Bone(HumanBodyBones.LeftUpperLeg), rightLeg = Bone(HumanBodyBones.RightUpperLeg);
                if (leftLeg == null || rightLeg == null) { return false; }
                // The character's left, from its hips, so this works whichever way the model faces.
                Vector3 left = Vector3.ProjectOnPlane(leftLeg.position - rightLeg.position, Vector3.up).normalized;
                if (left == Vector3.zero) { return false; }

                // Parents before children, so each bone is aimed after its parent has moved it.
                // The spine straight up, as the mannequin's is: its clips lean the torso relative to a straight spine,
                // so a model resting with a curved or tilted spine would carry that into every pose.
                Transform[] spine =
                {
                    Bone(HumanBodyBones.Hips), Bone(HumanBodyBones.Spine), Bone(HumanBodyBones.Chest),
                    Bone(HumanBodyBones.UpperChest), Bone(HumanBodyBones.Neck), Bone(HumanBodyBones.Head),
                };
                Transform[] chain = spine.Where(t => t != null).ToArray();
                for (int i = 0; i + 1 < chain.Length; i++) { Aim(chain[i], chain[i + 1], Vector3.up); }
                Aim(Bone(HumanBodyBones.LeftUpperArm), Bone(HumanBodyBones.LeftLowerArm), left);
                Aim(Bone(HumanBodyBones.LeftLowerArm), Bone(HumanBodyBones.LeftHand), left);
                Aim(Bone(HumanBodyBones.LeftHand), Bone(HumanBodyBones.LeftMiddleProximal), left);
                Aim(Bone(HumanBodyBones.RightUpperArm), Bone(HumanBodyBones.RightLowerArm), -left);
                Aim(Bone(HumanBodyBones.RightLowerArm), Bone(HumanBodyBones.RightHand), -left);
                Aim(Bone(HumanBodyBones.RightHand), Bone(HumanBodyBones.RightMiddleProximal), -left);
                Aim(leftLeg, Bone(HumanBodyBones.LeftLowerLeg), Vector3.down);
                Aim(Bone(HumanBodyBones.LeftLowerLeg), Bone(HumanBodyBones.LeftFoot), Vector3.down);
                Aim(rightLeg, Bone(HumanBodyBones.RightLowerLeg), Vector3.down);
                Aim(Bone(HumanBodyBones.RightLowerLeg), Bone(HumanBodyBones.RightFoot), Vector3.down);
                StraightenAboveSkeleton(instance.transform, Bone(HumanBodyBones.Hips));

                bool changed = false;
                SkeletonBone[] skeleton = description.skeleton;
                for (int i = 0; i < skeleton.Length; i++)
                {
                    if (!transforms.TryGetValue(skeleton[i].name, out Transform t) || t == instance.transform) { continue; }
                    if (Quaternion.Angle(skeleton[i].rotation, t.localRotation) > 0.01f
                        || Vector3.Distance(skeleton[i].position, t.localPosition) > 1e-4f
                        || Vector3.Distance(skeleton[i].scale, t.localScale) > 1e-4f * Mathf.Max(1f, t.localScale.magnitude)) { changed = true; }
                    skeleton[i].rotation = t.localRotation;
                    skeleton[i].position = t.localPosition;
                    skeleton[i].scale = t.localScale; // Straightening can swap a node's scale axes.
                }
                bool mappingWasSaved = importer.humanDescription.human != null && importer.humanDescription.human.Length > 0;
                if (!changed && mappingWasSaved) { return false; }
                description.skeleton = skeleton;
                importer.humanDescription = description;
                importer.SaveAndReimport();
                return changed;
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        /// <summary>
        /// Puts every bone the meshes are skinned to back where it was when the meshes were bound to it: world pose =
        /// renderer's transform x inverse bind pose. Parents first, so placing a bone doesn't move one already placed.
        /// Bones no mesh uses keep their pose relative to their parent.
        /// </summary>
        private static void RestoreBindPose(GameObject instance)
        {
            var rest = new Dictionary<Transform, Matrix4x4>();
            foreach (SkinnedMeshRenderer skinned in instance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                Matrix4x4[] bindposes = skinned.sharedMesh != null ? skinned.sharedMesh.bindposes : null;
                if (bindposes == null) { continue; }
                Transform[] bones = skinned.bones;
                Matrix4x4 meshToWorld = skinned.transform.localToWorldMatrix;
                for (int i = 0; i < bones.Length && i < bindposes.Length; i++)
                {
                    if (bones[i] != null) { rest.TryAdd(bones[i], meshToWorld * bindposes[i].inverse); }
                }
            }
            foreach (Transform bone in instance.GetComponentsInChildren<Transform>(true)) // Parents before children.
            {
                if (!rest.TryGetValue(bone, out Matrix4x4 world)) { continue; }
                bone.SetPositionAndRotation(world.GetColumn(3), world.rotation);
            }
        }

        /// <summary>
        /// Takes the rotation out of the nodes between the model's root and its hips, keeping everything under them where
        /// it is. Blender's FBX export rotates the armature node -90 degrees about X to convert axes. The Animator poses
        /// those nodes as the avatar stores them and places the body through them, and when the character is stretched
        /// along one axis only (taller, say) the rotation turns the stretch onto another axis: the body is distorted and
        /// lifted off the ground, its hips stretched by the scale twice over. Unrotated, as in models without such a
        /// node, the stretch applies as it should. Every bone's world pose is unchanged, so animation looks the same.
        ///
        /// A node scaled unevenly keeps its scale on the same axes in the world: its rotation is taken out only if it's
        /// a whole number of quarter turns about each axis (as an axis conversion is), which only swaps the scale's
        /// axes. Any other rotation of an unevenly scaled node can't be taken out without shearing what's under it, so
        /// the node is left as it is. (An uneven scale on its own is fine: it stretches the model along axes the
        /// character's own stretches line up with.) Returns whether every node above the hips is now unrotated.
        /// </summary>
        public static bool StraightenAboveSkeleton(Transform root, Transform hips)
        {
            if (hips == null) { return true; }
            var above = new List<Transform>();
            for (Transform node = hips.parent; node != null && node != root; node = node.parent) { above.Add(node); }
            above.Reverse(); // From the top down, so straightening one doesn't move the ones below it.
            bool allStraight = true;
            foreach (Transform node in above)
            {
                Quaternion rotation = node.localRotation;
                if (Quaternion.Angle(rotation, Quaternion.identity) < 1e-3f) { continue; }
                Vector3 scale = node.localScale;
                bool even = Mathf.Abs(scale.x - scale.y) <= 1e-5f * Mathf.Abs(scale.y) && Mathf.Abs(scale.z - scale.y) <= 1e-5f * Mathf.Abs(scale.y);
                Matrix4x4 turn = Matrix4x4.Rotate(rotation);
                if (!even && !IsQuarterTurns(turn))
                {
                    allStraight = false;
                    continue;
                }
                // Before, a child sits at node rotation x node scale x its own pose; after, at the node's new scale x its
                // new pose. With the scale's axes swapped as the rotation swaps them (the rotation applied to the scale,
                // sign dropped), the child's new pose is just the rotation applied to its old one.
                node.localScale = new Vector3(
                    Mathf.Abs(turn.m00) * scale.x + Mathf.Abs(turn.m01) * scale.y + Mathf.Abs(turn.m02) * scale.z,
                    Mathf.Abs(turn.m10) * scale.x + Mathf.Abs(turn.m11) * scale.y + Mathf.Abs(turn.m12) * scale.z,
                    Mathf.Abs(turn.m20) * scale.x + Mathf.Abs(turn.m21) * scale.y + Mathf.Abs(turn.m22) * scale.z);
                if (even) { node.localScale = scale; } // Exactly as it was, without rounding.
                node.localRotation = Quaternion.identity;
                foreach (Transform child in node)
                {
                    child.SetLocalPositionAndRotation(rotation * child.localPosition, rotation * child.localRotation);
                }
            }
            return allStraight;
        }

        /// <summary>Whether a rotation is a whole number of quarter turns: it only swaps axes (and their signs).</summary>
        private static bool IsQuarterTurns(Matrix4x4 turn)
        {
            for (int row = 0; row < 3; row++)
            {
                for (int column = 0; column < 3; column++)
                {
                    float value = Mathf.Abs(turn[row, column]);
                    if (value > 1e-4f && value < 1f - 1e-4f) { return false; }
                }
            }
            return true;
        }

        /// <summary>Turns the bone so its child lies in the given direction from it.</summary>
        private static void Aim(Transform bone, Transform child, Vector3 direction)
        {
            if (bone == null || child == null) { return; }
            Vector3 current = child.position - bone.position;
            if (current.sqrMagnitude < 1e-8f) { return; }
            bone.rotation = Quaternion.FromToRotation(current, direction) * bone.rotation;
        }

        /// <summary>
        /// Saves the model as a prefab (a variant of the model file, so it follows changes to the file) with its Animator,
        /// CharacterIK and FootPlacementIK. IK settings come from an earlier run if there was one, otherwise from the
        /// mannequin's model prefab, so tuning done there carries over to new models.
        /// </summary>
        private static GameObject SaveModelPrefab(GameObject modelAsset, Avatar avatar, AnimatorController controller, string prefabPath)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            var mannequin = AssetDatabase.LoadAssetAtPath<GameObject>(MannequinGenerator.ModelPrefabPath);
            string ikSettings = SettingsOf<CharacterIK>(existing) ?? SettingsOf<CharacterIK>(mannequin);
            string footPlacementSettings = SettingsOf<FootPlacementIK>(existing) ?? SettingsOf<FootPlacementIK>(mannequin);

            var model = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
            try
            {
                model.name = Path.GetFileNameWithoutExtension(prefabPath).Replace(" Model", "");
                model.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity); // Its origin is at its feet.
                model.transform.localScale = Vector3.one;

                Animator animator = model.GetComponent<Animator>();
                if (animator == null) { animator = model.AddComponent<Animator>(); }
                animator.avatar = avatar;
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false; // Movement is driven by MovementController, not animation.
                // Keep animating while hidden in first person, so the shadow the camera keeps still animates.
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                // IK layered over the animations. Lives on the Animator's GameObject, as OnAnimatorIK requires.
                // (Not `??`: it bypasses Unity's null check, so a "missing" component could be returned instead of added.)
                if (!model.TryGetComponent(out CharacterIK ik)) { ik = model.AddComponent<CharacterIK>(); }
                if (ikSettings != null) { EditorJsonUtility.FromJsonOverwrite(ikSettings, ik); }
                // Automatic foot placement on uneven ground, fed through CharacterIK's procedural channel.
                if (!model.TryGetComponent(out FootPlacementIK footPlacement)) { footPlacement = model.AddComponent<FootPlacementIK>(); }
                if (footPlacementSettings != null) { EditorJsonUtility.FromJsonOverwrite(footPlacementSettings, footPlacement); }

                return PrefabUtility.SaveAsPrefabAsset(model, prefabPath);
            }
            finally
            {
                Object.DestroyImmediate(model);
            }
        }

        private static string SettingsOf<T>(GameObject gameObject) where T : Component =>
            gameObject != null && gameObject.TryGetComponent(out T component) ? EditorJsonUtility.ToJson(component) : null;

        /// <summary>
        /// Describes the nodes between the model's root and its hips that its avatar poses rotated, which distort the
        /// model when the character is stretched along one axis, or null if there are none. Preparing straightens them
        /// (see StraightenAboveSkeleton), so this reports what that couldn't: a node both unevenly scaled and turned by
        /// other than quarter turns. (Uneven scale on its own doesn't distort the model, so it isn't reported.)
        /// </summary>
        public static string RotatedAboveSkeleton(GameObject modelPrefab, Avatar avatarOverride)
        {
            Animator animator = modelPrefab != null ? modelPrefab.GetComponentInChildren<Animator>(true) : null;
            Avatar avatar = avatarOverride != null ? avatarOverride : animator != null ? animator.avatar : null;
            if (avatar == null || !avatar.isHuman) { return null; }
            HumanDescription description = avatar.humanDescription;
            string hipsName = description.human.FirstOrDefault(h => h.humanName == HumanTrait.BoneName[(int)HumanBodyBones.Hips]).boneName;
            Transform hips = string.IsNullOrEmpty(hipsName) ? null
                : modelPrefab.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == hipsName);
            if (hips == null) { return null; }
            var problems = new List<string>();
            for (Transform node = hips.parent; node != null && node != modelPrefab.transform; node = node.parent)
            {
                SkeletonBone pose = description.skeleton.FirstOrDefault(bone => bone.name == node.name);
                if (pose.name == null) { continue; }
                if (Quaternion.Angle(pose.rotation, Quaternion.identity) > 0.5f) { problems.Add($"'{node.name}' is rotated"); }
            }
            return problems.Count == 0 ? null : string.Join(", ", problems) + ", above its skeleton.";
        }

        /// <summary>
        /// How far above its origin the model's soles are as it stands idle, in its own units: its lowest point in its
        /// standing clip (the default state's clip, or for a blend tree, its clip for the lowest value, as standing
        /// still is), with its feet where foot placement holds them if it has foot placement, else where the animation
        /// puts them, and the latter on its own. Every mesh counts, skinned or carried by a bone. Used as its source's
        /// Ground Offset and Animated Ground Offset, so models whose feet sit a little above or below their origin (or
        /// whose animation, retargeted onto them, does) still stand on the ground. Measured by StandingPose, as sources
        /// without stored offsets are when they load. False if the model can't be posed or has no meshes.
        /// </summary>
        public static bool TryMeasureGroundOffsets(GameObject modelPrefab, RuntimeAnimatorController controller, Avatar avatarOverride,
            out float groundOffset, out float animatedGroundOffset) =>
            TryMeasureStanding(modelPrefab, controller, avatarOverride, out groundOffset, out animatedGroundOffset, out _);

        /// <summary>The same, and the way the model faces as it stands (see StandingPose.Facing).</summary>
        public static bool TryMeasureStanding(GameObject modelPrefab, RuntimeAnimatorController controller, Avatar avatarOverride,
            out float groundOffset, out float animatedGroundOffset, out StandingPose pose)
        {
            groundOffset = animatedGroundOffset = 0f;
            pose = default;
            AnimationClip standing = StandingClip(controller);
            if (modelPrefab == null || standing == null
                || !StandingPose.TryMeasure(modelPrefab, controller, avatarOverride, out pose, standing)) { return false; }
            bool placesFeet = modelPrefab.GetComponentsInChildren<FootPlacementIK>(true).Any(feet => feet.enabled && feet.weight > 0f);
            groundOffset = placesFeet ? pose.PlacedSoleHeight : pose.SoleHeight;
            animatedGroundOffset = pose.SoleHeight;
            return true;
        }

        /// <summary>The model's Ground Offset, as TryMeasureGroundOffsets measures it.</summary>
        public static bool TryMeasureGroundOffset(GameObject modelPrefab, RuntimeAnimatorController controller, Avatar avatarOverride, out float offset) =>
            TryMeasureGroundOffsets(modelPrefab, controller, avatarOverride, out offset, out _);

        /// <summary>The clip the controller plays standing still (see TryMeasureGroundOffsets). Null if there's none.</summary>
        private static AnimationClip StandingClip(RuntimeAnimatorController controller)
        {
            if (controller is not AnimatorController editable || editable.layers.Length == 0) { return null; }
            AnimatorState state = editable.layers[0].stateMachine.defaultState;
            if (state == null) { return null; }
            Motion motion = state.motion;
            while (motion is BlendTree tree && tree.children.Length > 0)
            {
                motion = tree.children.OrderBy(child => child.threshold).First().motion;
            }
            return motion as AnimationClip;
        }

        /// <summary>The model's height in meters, from its meshes in their bind pose.</summary>
        public static float MeasureHeight(GameObject modelPrefab) => CharacterModelSource.MeasureHeight(modelPrefab);
    }
}
