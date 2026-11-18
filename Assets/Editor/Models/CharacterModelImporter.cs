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
    ///   Controller, CharacterIK, FootPlacementIK, LedgeHandIK, WallSlideIK, LadderIK and LineGripIK
    /// - saves a DirectCharacterModelSource next to it ("[name].asset") naming the prefab and the shared translator,
    ///   which the Character Model window (or a CharacterModelHost) can then use
    /// Each subfolder can hold one model, or several (variants of one model with different materials, say); each model
    /// file becomes its own choice, named after the file. Safe to run repeatedly: IK settings and edits to the sources
    /// are kept, assets made under older names are moved to the current ones, and sources left over from models that
    /// are gone are reported. CharacterModelPostprocessor runs PrepareChanged by itself when models are imported.
    /// </summary>
    public static partial class CharacterModelImporter
    {
        /// <summary>The folder the models are in, a subfolder per model (or per model and its variants).</summary>
        public const string ModelsFolder = "Assets/Models/CharacterModels";
        /// <summary>The mannequin's model file: the model every model's animations are made for.</summary>
        public const string MannequinModelPath = ModelsFolder + "/Mannequin/Mannequin.fbx";
        /// <summary>The mannequin's model prefab, whose IK settings new models start from.</summary>
        public const string MannequinPrefabPath = ModelsFolder + "/Mannequin/Mannequin Model.prefab";
        /// <summary>
        /// The Animator Controller, shared by every model: Humanoid animation plays on any Humanoid model. Built from its
        /// layout (LayoutPath) by the Animator Controller Builder.
        /// </summary>
        public const string ControllerPath = "Assets/Animation/Mannequin.controller";
        /// <summary>The shared Animator Controller's layout (an AnimatorLayout): its states, clips and transitions, edited there.</summary>
        public const string LayoutPath = "Assets/Animation/Mannequin Layout.asset";
        /// <summary>
        /// The translator every model's source names, shared like the controller: an asset of its own, set in its inspector.
        /// </summary>
        public const string TranslatorPath = "Assets/Animation/Mannequin Animator Translator.asset";

        // Models outside this height range (in meters) get their definition's Scale set to bring them to TypicalHeight,
        // since that usually means the file's units were lost (a 180 m or 1.8 cm character).
        private const float MinHeight = 1f;
        private const float MaxHeight = 2.5f;
        private const float TypicalHeight = 1.8f;

        /// <summary>The outcome of preparing one model file.</summary>
        public sealed class Result
        {
            /// <summary>The model file.</summary>
            public string modelPath;
            /// <summary>The name the model goes by (see DisplayName).</summary>
            public string name;
            /// <summary>The model's source, saved next to it.</summary>
            public DirectCharacterModelSource definition;
            /// <summary>Why the model couldn't be prepared, or null if it was.</summary>
            public string problem;
            /// <summary>The model's measured height (m).</summary>
            public float height;
            /// <summary>Already prepared, and unchanged since, so it was left alone.</summary>
            public bool upToDate;
            /// <summary>
            /// Things worth knowing that aren't problems, such as assets moved to the model's current names.
            /// </summary>
            public readonly List<string> notes = new();
            /// <summary>Whether the model was prepared (or already was).</summary>
            public bool Succeeded => problem == null;

            public override string ToString()
            {
                string text = !Succeeded ? $"'{name}' ({modelPath}): not prepared. {problem}"
                    : upToDate ? $"'{name}' ({modelPath}): up to date"
                    : $"'{name}' ({modelPath}): ready, {height:0.00} m tall, source '{AssetDatabase.GetAssetPath(definition)}'";
                return notes.Count == 0 ? text : $"{text} ({string.Join(" ", notes)})";
            }
        }

        /// <summary>
        /// For -executeMethod: prepares every model (see PrepareAll), logs how each went, and exits when it's done, with
        /// code 1 if any couldn't be prepared.
        /// </summary>
        public static void PrepareAllFromCommandLine()
        {
            List<Result> results = PrepareAll();
            bool ok = results != null && results.All(r => r.Succeeded);
            if (results != null) { foreach (Result result in results) { Debug.Log($"[CharacterModelImporter] {result}"); } }
            CommandLine.Finish(ok);
        }

        /// <summary>Prepares every model (see PrepareAll) and shows how each went.</summary>
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
        /// Loads the shared Animator Controller and translator every model uses. False, with an error logged, if either
        /// is missing or the controller is empty: the mannequin hasn't been generated.
        /// </summary>
        public static bool LoadSharedAssets(out AnimatorController controller, out AnimatorParameterTranslator translator)
        {
            controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            translator = AssetDatabase.LoadAssetAtPath<AnimatorParameterTranslator>(TranslatorPath);
            if (controller != null && translator != null && controller.layers.Length > 0) { return true; }
            Debug.LogError($"[CharacterModelImporter] '{ControllerPath}' or '{TranslatorPath}' is missing or empty. Run Tools > Character " +
                           "Showcase > Prepare Animations first, which builds the controller from its layout: the mannequin's animations are the ones every model plays.");
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

        /// <summary>Where a model's prefab is saved: "[name] Model.prefab" next to the model file.</summary>
        public static string ModelPrefabPathFor(string modelPath, string name) =>
            $"{Path.GetDirectoryName(modelPath)?.Replace('\\', '/')}/{name} Model.prefab";

        /// <summary>Where a model's source is saved: "[name].asset" next to the model file.</summary>
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
            // Its height, for working out what a character with it can do before one's spawned (a character fitting its
            // body to its model's height takes it).
            definition.RecordMeasurements(CharacterModelSource.MeasureHeight(savedPrefab, savedPrefab.transform));
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
            // Its own body, which a character fitting its body to its model takes (after the facing's measured: it's fitted
            // as the model faces the character's way).
            FitBody(definition, definitionPath, savedPrefab, result);
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
    }
}
