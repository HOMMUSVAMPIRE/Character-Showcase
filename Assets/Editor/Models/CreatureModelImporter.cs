using System.Collections.Generic;
using System.IO;
using System.Linq;
using Character;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CharacterShowcase.EditorTools
{
    /// <summary>
    /// Prepares creatures (models that aren't humanoids: on four legs, two, or more) as Prepare Character Models prepares
    /// humanoids: drop a rigged model into a folder of its own in Assets/Models/Creatures and it's made ready by itself
    /// (CreatureModelPostprocessor), or Tools > Character Showcase > Prepare Creatures (PrepareAllFromCommandLine). Each
    /// model is described by its CreatureSetup ("[name] Setup.asset" beside it): made the first time, its bones and clips
    /// guessed from their names (Guess), then the creature's to correct. Preparing makes, beside the model:
    /// - its model prefab ("[name] Model.prefab"): its Animator playing its controller, an Eyes point on its eyes bone,
    ///   paw placement (PawPlacementIK, its legs from the setup) and hitboxes (HitboxFitter, on the setup's hitbox bones);
    /// - its Animator Controller, built from its animation set (or its layout: see AnimatorControllerBuilder);
    /// - an Animator parameter translator, made only if missing (then the creature's to change);
    /// - its body ("[name] Body.asset": capsules along its torso and a sphere for its head, fitted from its mesh, see
    ///   BodyFitting.Creature), made only if missing (then the creature's to tune);
    /// - its model source ("[name].asset"), which a character wears (New Character makes one).
    /// See docs/creatures.md.
    /// </summary>
    public static partial class CreatureModelImporter
    {
        /// <summary>Where creatures live: each in a folder of its own, its model and setup in it.</summary>
        public const string CreaturesFolder = "Assets/Models/Creatures";
        /// <summary>The layout a creature's animation set starts from: the fox's states, with empty slots (Only For Sets).</summary>
        public const string StarterLayoutPath = "Assets/Animation/Creature Layout.asset";
        private const string LogName = "Prepare Creatures";
        // The take some exporters add holding the rig's rest pose, which isn't a clip to play.
        private const string BindPoseTake = "!BindPose";

        /// <summary>What preparing a creature came to.</summary>
        public sealed class Result
        {
            public string name;
            public CreatureSetup setup;
            public DirectCharacterModelSource source;
            /// <summary>Why it couldn't be prepared, or null if it was.</summary>
            public string problem;
            /// <summary>What it made, and what's worth knowing (bones it couldn't find, say).</summary>
            public readonly List<string> notes = new();
            public bool Succeeded => problem == null;

            public override string ToString() => $"{name}: " + (Succeeded ? "prepared" : "not prepared: " + problem) +
                                                 (notes.Count > 0 ? "\n  " + string.Join("\n  ", notes) : "");
        }

        // ---- Where things are ----

        /// <summary>A creature's name: its model file's.</summary>
        public static string NameOf(string modelPath) => Path.GetFileNameWithoutExtension(modelPath);
        private static string FolderOf(string modelPath) => Path.GetDirectoryName(modelPath).Replace('\\', '/');
        public static string SetupPathFor(string modelPath) => $"{FolderOf(modelPath)}/{NameOf(modelPath)} Setup.asset";
        public static string ModelPrefabPathFor(string modelPath) => $"{FolderOf(modelPath)}/{NameOf(modelPath)} Model.prefab";
        public static string SourcePathFor(string modelPath) => $"{FolderOf(modelPath)}/{NameOf(modelPath)}.asset";
        public static string BodyPathFor(string modelPath) => $"{FolderOf(modelPath)}/{NameOf(modelPath)} Body.asset";
        public static string TranslatorPathFor(string modelPath) => $"{FolderOf(modelPath)}/{NameOf(modelPath)} Animator Translator.asset";

        /// <summary>Whether a path is in a creature's folder (Assets/Models/Creatures/[name]/...).</summary>
        public static bool InCreaturesFolder(string path) => path.StartsWith(CreaturesFolder + "/");

        /// <summary>Whether a model source is a creature's: it, or the model prefab it wears, is in a creature's folder.</summary>
        public static bool IsCreature(CharacterModelSource source) =>
            source != null && (InCreaturesFolder(AssetDatabase.GetAssetPath(source)) || InCreaturesFolder(ModelPrefabPathOf(source)));

        private static string ModelPrefabPathOf(CharacterModelSource source) =>
            source is DirectCharacterModelSource direct && direct.ModelPrefab != null ? AssetDatabase.GetAssetPath(direct.ModelPrefab) : "";

        /// <summary>The model files in the creatures' folders that have a skinned mesh (a model to prepare, not only clips).</summary>
        public static IEnumerable<string> FindModelFiles() =>
            AssetDatabase.IsValidFolder(CreaturesFolder)
                ? AssetDatabase.FindAssets("t:Model", new[] { CreaturesFolder }).Select(AssetDatabase.GUIDToAssetPath)
                    .Where(path => AssetImporter.GetAtPath(path) is ModelImporter && HasSkinnedMesh(path))
                    .Distinct().OrderBy(p => p, System.StringComparer.Ordinal)
                : Enumerable.Empty<string>();

        private static bool HasSkinnedMesh(string modelPath)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            return model != null && model.GetComponentInChildren<SkinnedMeshRenderer>(true) != null;
        }

        /// <summary>Every creature's model source that's ready to wear (its model prefab is there).</summary>
        public static IEnumerable<DirectCharacterModelSource> FindPrepared() =>
            FindModelFiles().Select(path => AssetDatabase.LoadAssetAtPath<DirectCharacterModelSource>(SourcePathFor(path)))
                .Where(source => source != null && source.ModelPrefab != null);

        // ---- Preparing ----

        [MenuItem("Tools/Character Showcase/Prepare Creatures", priority = 4)]
        public static void PrepareAllFromMenu()
        {
            List<Result> results = PrepareAll();
            int failed = results.Count(r => !r.Succeeded);
            EditorUtility.DisplayDialog(LogName, results.Count == 0 ? $"There are no creatures to prepare: put a rigged model in a folder of its own in {CreaturesFolder}."
                : $"Prepared {results.Count - failed} creature(s)" + (failed > 0 ? $"; {failed} couldn't be (see the Console)." : "."), "OK");
        }

        /// <summary>For -executeMethod: prepares every creature (or only -model [path]), then exits (code 1 if one failed).</summary>
        public static void PrepareAllFromCommandLine()
        {
            string path = CommandLine.Argument("-model");
            List<Result> results = path != null ? new List<Result> { Prepare(path) } : PrepareAll();
            CommandLine.Finish(results.All(r => r.Succeeded));
        }

        /// <summary>Prepares every creature in the creatures' folders.</summary>
        public static List<Result> PrepareAll() => PrepareMany(onlyChanged: false);

        /// <summary>
        /// Prepares the creatures that are new, or whose model, setup or animation changed since they were last prepared
        /// on this machine (see IsUpToDate): what the postprocessor runs.
        /// </summary>
        public static List<Result> PrepareChanged() => PrepareMany(onlyChanged: true);

        private static List<Result> PrepareMany(bool onlyChanged)
        {
            var results = new List<Result>();
            using (CharacterModelImporter.Busy())
            {
                foreach (string modelPath in FindModelFiles())
                {
                    if (onlyChanged && IsUpToDate(modelPath)) { continue; }
                    results.Add(Prepare(modelPath));
                }
                AssetDatabase.SaveAssets();
            }
            foreach (Result result in results)
            {
                if (result.Succeeded) { Debug.Log($"[{LogName}] {result}", result.source); }
                else { Debug.LogError($"[{LogName}] {result}", result.setup); }
            }
            return results;
        }

        /// <summary>Prepares the creature whose model is at the path, making its setup first (guessed) if it has none.</summary>
        public static Result Prepare(string modelPath)
        {
            var result = new Result { name = NameOf(modelPath) };
            if (AssetImporter.GetAtPath(modelPath) is not ModelImporter)
            {
                result.problem = $"'{modelPath}' isn't a model file.";
                return result;
            }
            using (CharacterModelImporter.Busy())
            {
                ConfigureImport(modelPath, takesAnew: false);
                CreatureSetup setup = LoadOrMakeSetup(modelPath, result);
                return Prepare(setup, result);
            }
        }

        /// <summary>Prepares the creature a setup describes (see the class).</summary>
        public static Result Prepare(CreatureSetup setup) => Prepare(setup, new Result { name = setup.model != null ? setup.model.name : setup.name });

        private static Result Prepare(CreatureSetup setup, Result result)
        {
            result.setup = setup;
            string modelPath = setup.model != null ? AssetDatabase.GetAssetPath(setup.model) : null;
            if (string.IsNullOrEmpty(modelPath))
            {
                result.problem = $"its setup ('{AssetDatabase.GetAssetPath(setup)}') names no Model.";
                return result;
            }
            result.name = NameOf(modelPath);
            using (CharacterModelImporter.Busy())
            {
                AnimatorController controller = BuildController(setup, result);
                if (controller == null) { return result; }
                GameObject modelPrefab = SaveModelPrefab(setup, modelPath, controller, result);
                if (modelPrefab == null) { return result; }
                AnimatorParameterTranslator translator = LoadOrMakeTranslator(modelPath, result);
                BodyDefinition body = LoadOrFitBody(setup, modelPath, modelPrefab, result);
                result.source = SaveSource(modelPath, modelPrefab, translator, body);
                string[] unfound = Unfound(setup, modelPrefab);
                if (!unfound.SequenceEqual(setup.unfound))
                {
                    setup.unfound = unfound;
                    EditorUtility.SetDirty(setup);
                }
                if (unfound.Length > 0)
                {
                    result.notes.Add($"It couldn't find: {string.Join("; ", unfound)}. Set them in its setup, " +
                                     $"'{AssetDatabase.GetAssetPath(setup)}' (it's prepared again by itself).");
                }
                AssetDatabase.SaveAssets();
                MarkPrepared(modelPath, setup);
            }
            return result;
        }

        /// <summary>
        /// Imports a creature's model as a Generic rig (its own avatar), each of its takes a clip named as the take is (less
        /// any "Armature|" before it) and looping, without the rest pose take some exporters add. Only the first time
        /// (while it has no clips set up), so changes made to its clips in its import settings are kept, unless takesAnew
        /// (for a model whose takes have changed: the showcase's own creatures, regenerated).
        /// </summary>
        public static bool ConfigureImport(string modelPath, bool takesAnew)
        {
            if (AssetImporter.GetAtPath(modelPath) is not ModelImporter importer) { return false; }
            bool configured = importer.animationType == ModelImporterAnimationType.Generic
                              && importer.avatarSetup == ModelImporterAvatarSetup.CreateFromThisModel
                              && importer.clipAnimations.Length > 0;
            if (configured && !takesAnew) { return true; }
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;
            importer.clipAnimations = importer.defaultClipAnimations
                .Where(take => take.takeName.Split('|').Last() != BindPoseTake)
                .Select(take =>
                {
                    take.name = take.takeName.Split('|').Last();
                    take.loopTime = true;
                    take.loopPose = false;
                    return take;
                })
                .ToArray();
            importer.SaveAndReimport();
            return true;
        }

        /// <summary>The creature's setup, made beside its model (its bones and clips guessed) if it has none.</summary>
        public static CreatureSetup LoadOrMakeSetup(string modelPath, Result result = null)
        {
            string path = SetupPathFor(modelPath);
            var setup = AssetDatabase.LoadAssetAtPath<CreatureSetup>(path);
            if (setup != null) { return setup; }
            setup = ScriptableObject.CreateInstance<CreatureSetup>();
            setup.model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            Guess(setup);
            // Its animation set first, so the setup is saved naming it.
            if (setup.animationSet != null && !AssetDatabase.Contains(setup.animationSet))
            {
                AssetDatabase.CreateAsset(setup.animationSet, AssetDatabase.GenerateUniqueAssetPath($"{FolderOf(modelPath)}/{NameOf(modelPath)} Animation Set.asset"));
                result?.notes.Add($"made its animation set, '{AssetDatabase.GetAssetPath(setup.animationSet)}', from its clips' names");
            }
            AssetDatabase.CreateAsset(setup, path);
            result?.notes.Add($"made its setup, '{path}', its bones and clips guessed from their names: check it");
            return setup;
        }

        /// <summary>The creature's controller: its animation set's, built (made beside the set), else its layout's.</summary>
        private static AnimatorController BuildController(CreatureSetup setup, Result result)
        {
            if (setup.animationSet != null)
            {
                AnimatorController built = AnimatorControllerBuilder.Build(setup.animationSet);
                if (built == null) { result.problem = $"its animation set '{setup.animationSet.name}' couldn't be built (see the Console)."; }
                return built;
            }
            if (setup.layout != null)
            {
                AnimatorController built = AnimatorControllerBuilder.Build(setup.layout);
                if (built == null) { result.problem = $"its layout '{setup.layout.name}' couldn't be built (see the Console)."; }
                return built;
            }
            result.problem = "its setup has no Animation Set (or Layout) to play: give it a set starting from " +
                             $"'{StarterLayoutPath}', with its clips in the roles it has.";
            return null;
        }

        // ---- Whether it needs preparing ----

        // Bump to prepare every creature again once, when what preparing does changes.
        private const string PreparationVersion = "1";
        // Per machine, like the rest of Library: what each creature was prepared from (its model file, setup and animation).
        private const string RecordsPath = "Library/CharacterShowcase/PreparedCreatures.txt";

        /// <summary>
        /// Whether a creature needs no preparing: its setup, model prefab and model source are there, and neither its model
        /// file, its setup nor its animation has changed since it was last prepared on this machine. One never prepared
        /// here (as after cloning the project, where its prepared assets come with it) counts as up to date.
        /// </summary>
        private static bool IsUpToDate(string modelPath)
        {
            var setup = AssetDatabase.LoadAssetAtPath<CreatureSetup>(SetupPathFor(modelPath));
            var source = AssetDatabase.LoadAssetAtPath<DirectCharacterModelSource>(SourcePathFor(modelPath));
            if (setup == null || source == null || source.ModelPrefab == null) { return false; }
            return !LoadRecords().TryGetValue(modelPath, out string recorded) || recorded == RecordFor(modelPath, setup);
        }

        /// <summary>What preparing the creature now would be preparing it from: a hash of its model file, its setup, and its animation set and layout.</summary>
        private static string RecordFor(string modelPath, CreatureSetup setup)
        {
            var hash = new Hash128();
            hash.Append(File.ReadAllBytes(modelPath));
            hash.Append(PreparationVersion);
            // The setup, and what its animation is built from: its animation set (whose clips it picks) and its layout.
            foreach (Object asset in new Object[] { setup, setup.animationSet, setup.animationSet != null ? setup.animationSet.layout : setup.layout })
            {
                string path = asset != null ? AssetDatabase.GetAssetPath(asset) : null;
                hash.Append(string.IsNullOrEmpty(path) ? "none" : AssetDatabase.GetAssetDependencyHash(path).ToString());
            }
            return hash.ToString();
        }

        /// <summary>Records a creature as prepared as it is now (so it isn't prepared again until something changes).</summary>
        public static void MarkPrepared(string modelPath, CreatureSetup setup)
        {
            Dictionary<string, string> records = LoadRecords();
            records[modelPath] = RecordFor(modelPath, setup);
            Directory.CreateDirectory(Path.GetDirectoryName(RecordsPath));
            File.WriteAllLines(RecordsPath, records.OrderBy(r => r.Key, System.StringComparer.Ordinal).Select(r => $"{r.Value}\t{r.Key}"));
        }

        // One line per creature: "hash<tab>model path".
        private static Dictionary<string, string> LoadRecords()
        {
            var records = new Dictionary<string, string>();
            if (!File.Exists(RecordsPath)) { return records; }
            foreach (string line in File.ReadAllLines(RecordsPath))
            {
                string[] parts = line.Split('\t');
                if (parts.Length == 2) { records[parts[1]] = parts[0]; }
            }
            return records;
        }
    }
}
