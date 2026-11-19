using System.Collections.Generic;
using System.IO;
using System.Linq;
using Character;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CharacterShowcase.EditorTools
{
    /// <summary>
    /// What New Character (NewCharacterWindow) makes: a character of one's own, in Assets/Characters/[name]:
    /// - its prefab, a variant of Character.prefab, so improvements to that reach it too, wearing the chosen model (its
    ///   body fitted to the model, as Character.prefab's is: Body Fit, Model's Body);
    /// - its movement mode: the default one shared, or a copy of it or of the fox's, whose tuning is its own;
    /// - with an animation set, a model source of its own, the model's playing the set's controller (the shared model
    ///   source is left as it is, for every other character wearing it).
    /// A model file from outside Assets/Models/CharacterModels is copied into a folder of its own there and prepared first
    /// (Prepare Character Models). Undone by deleting its folder. See docs/adding-a-character.md.
    /// </summary>
    public static class NewCharacter
    {
        public const string CharactersFolder = "Assets/Characters";
        private const string FoxMovementPath = "Assets/ScriptableObjects/Fox Movement.asset";

        public enum Movement { ShareTheDefault, CopyTheDefault, CopyTheFoxs }

        /// <summary>The window's answers.</summary>
        public sealed class Answers
        {
            /// <summary>Its name, which its folder and assets are named after.</summary>
            public string name = "";
            /// <summary>A model source Prepare Character Models has made, or null to prepare modelFile.</summary>
            public CharacterModelSource modelSource;
            /// <summary>A rigged humanoid model file in the project, prepared on the spot, when there's no model source.</summary>
            public GameObject modelFile;
            public Movement movement = Movement.CopyTheDefault;
            /// <summary>An animation set its model plays (optional).</summary>
            public CharacterAnimationSet animationSet;
        }

        public static string FolderOf(string name) => $"{CharactersFolder}/{name}";

        /// <summary>Why the answers can't be made, or null if they can.</summary>
        public static string Problem(Answers answers)
        {
            string name = answers.name?.Trim() ?? "";
            if (name.Length == 0 || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name.Contains("/")) { return "Give it a name (letters, numbers and spaces)."; }
            if (AssetDatabase.IsValidFolder(FolderOf(name))) { return $"There's already a character called {name} ({FolderOf(name)})."; }
            if (answers.modelSource == null && answers.modelFile == null) { return "Choose its model: one already prepared, or a model file (an FBX) to prepare."; }
            if (answers.modelSource == null && !AssetDatabase.GetAssetPath(answers.modelFile).EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
            {
                return "The model file should be a rigged model (an .fbx) in the project: drag it into the Project window first.";
            }
            if (answers.movement == Movement.CopyTheFoxs && AssetDatabase.LoadAssetAtPath<MovementMode>(FoxMovementPath) == null) { return "The fox's movement isn't in this project."; }
            if (AssetDatabase.LoadAssetAtPath<GameObject>(SceneGameSetup.CharacterPrefabPath) == null) { return $"There's no Character prefab ({SceneGameSetup.CharacterPrefabPath}) to make it from."; }
            return null;
        }

        /// <summary>Makes the character (call Problem first), returning its prefab, and listing what it made in made.</summary>
        public static GameObject Make(Answers answers, List<string> made)
        {
            string name = answers.name.Trim();
            string folder = FolderOf(name);
            if (!AssetDatabase.IsValidFolder(CharactersFolder)) { AssetDatabase.CreateFolder("Assets", "Characters"); }
            AssetDatabase.CreateFolder(CharactersFolder, name);
            made.Add(folder);

            CharacterModelSource source = answers.modelSource != null ? answers.modelSource : PrepareModel(answers.modelFile, name, made);
            if (source == null) { return null; }
            if (answers.animationSet != null)
            {
                if (answers.animationSet.controller == null) { AnimatorControllerBuilder.Build(answers.animationSet); }
                string own = $"{folder}/{name} Model.asset";
                AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(source), own);
                var copy = AssetDatabase.LoadAssetAtPath<CharacterModelSource>(own);
                copy.AnimatorController = answers.animationSet.controller;
                EditorUtility.SetDirty(copy);
                source = copy;
                made.Add($"{own} (its model, playing {answers.animationSet.name})");
            }

            MovementMode mode = null;
            if (answers.movement != Movement.ShareTheDefault)
            {
                string from = answers.movement == Movement.CopyTheFoxs ? FoxMovementPath
                    : AssetDatabase.GetAssetPath(AssetDatabase.LoadAssetAtPath<GameObject>(SceneGameSetup.CharacterPrefabPath).GetComponent<MovementController>().movementHandler);
                string to = $"{folder}/{name} Movement.asset";
                AssetDatabase.CopyAsset(from, to);
                mode = AssetDatabase.LoadAssetAtPath<MovementMode>(to);
                made.Add($"{to} (its movement, a copy of {Path.GetFileNameWithoutExtension(from)})");
            }

            // The prefab: a variant of Character.prefab, wearing its model, moving by its mode.
            var basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SceneGameSetup.CharacterPrefabPath);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
            try
            {
                instance.name = name;
                var host = new SerializedObject(instance.GetComponent<CharacterModelHost>());
                host.FindProperty("modelSource").objectReferenceValue = source;
                host.ApplyModifiedPropertiesWithoutUndo();
                if (mode != null) { instance.GetComponent<MovementController>().movementHandler = mode; }
                string prefabPath = $"{folder}/{name}.prefab";
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
                made.Add($"{prefabPath} (its prefab, a variant of Character.prefab)");
                AssetDatabase.SaveAssets();
                return prefab;
            }
            finally { Object.DestroyImmediate(instance); }
        }

        /// <summary>Copies a model file into Assets/Models/CharacterModels/[name] (unless it's there already) and prepares it.</summary>
        private static CharacterModelSource PrepareModel(GameObject modelFile, string name, List<string> made)
        {
            string path = AssetDatabase.GetAssetPath(modelFile);
            if (!path.StartsWith(CharacterModelImporter.ModelsFolder + "/"))
            {
                string folder = $"{CharacterModelImporter.ModelsFolder}/{name}";
                if (!AssetDatabase.IsValidFolder(folder)) { AssetDatabase.CreateFolder(CharacterModelImporter.ModelsFolder, name); }
                string copied = $"{folder}/{Path.GetFileName(path)}";
                AssetDatabase.CopyAsset(path, copied);
                made.Add($"{copied} (its model file, copied in to be prepared)");
                path = copied;
            }
            if (!CharacterModelImporter.LoadSharedAssets(out var controller, out var translator)) { return null; }
            CharacterModelImporter.Result result = CharacterModelImporter.Prepare(path, controller, translator);
            if (!result.Succeeded)
            {
                Debug.LogError($"[New Character] Couldn't prepare {path}: {result.problem}");
                return null;
            }
            made.Add($"{AssetDatabase.GetAssetPath(result.definition)} (its model, prepared)");
            return result.definition;
        }

        /// <summary>Sets up the open scene to spawn the character (Set Up Scene), with Undo.</summary>
        public static bool SetUpOpenScene(GameObject prefab) =>
            SceneGameSetup.SetUp(EditorSceneManager.GetActiveScene(), new SceneGameSetup.Settings { characterPrefab = prefab }, withUndo: true);

        /// <summary>
        /// For -executeMethod: makes the character -characterName [name] wearing -model [a prepared model's name or source
        /// path, or a model file's path], with -movement [ShareTheDefault|CopyTheDefault|CopyTheFoxs] and optionally
        /// -animationSet [path], then exits (code 1 if it can't).
        /// </summary>
        public static void MakeFromCommandLine()
        {
            var answers = new Answers { name = CommandLine.Argument("-characterName") ?? "" };
            string model = CommandLine.Argument("-model");
            if (model != null)
            {
                answers.modelSource = AssetDatabase.LoadAssetAtPath<CharacterModelSource>(model);
                if (answers.modelSource == null) { answers.modelSource = CharacterModelImporter.FindReadyDefinitions().FirstOrDefault(d => d.name == model); }
                if (answers.modelSource == null) { answers.modelFile = AssetDatabase.LoadAssetAtPath<GameObject>(model); }
            }
            string movement = CommandLine.Argument("-movement");
            if (movement != null && System.Enum.TryParse(movement, true, out Movement parsed)) { answers.movement = parsed; }
            string set = CommandLine.Argument("-animationSet");
            if (set != null) { answers.animationSet = AssetDatabase.LoadAssetAtPath<CharacterAnimationSet>(set); }
            string problem = Problem(answers);
            if (problem != null) { Debug.LogError("[New Character] " + problem); CommandLine.Finish(false); return; }
            var made = new List<string>();
            GameObject prefab = Make(answers, made);
            Debug.Log($"[New Character] Made {answers.name}:\n" + string.Join("\n", made));
            CommandLine.Finish(prefab != null);
        }
    }
}
