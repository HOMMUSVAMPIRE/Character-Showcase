using System.Collections.Generic;
using System.IO;
using System.Linq;
using MeridianMotion.Characters;
using UnityEditor.SceneManagement;
using UnityEditor;
using UnityEngine;

namespace MeridianMotion.EditorTools
{
    /// <summary>
    /// What New Character (NewCharacterWindow) makes: a character of one's own, in Assets/Characters/[name]:
    /// - its prefab, a variant of Character.prefab, so improvements to that reach it too, wearing the chosen model (its
    ///   body fitted to the model, as Character.prefab's is: Body Fit, Model's Body);
    /// - its movement mode: the default one shared, or a copy of it or of the fox's, whose tuning is its own;
    /// - with an animation set, a model source of its own, the model's playing the set's controller (the shared model
    ///   source is left as it is, for every other character wearing it).
    /// A model file from outside Assets/Models/Humanoid is copied into a folder of its own there and prepared first
    /// (Prepare Humanoid Models); a generic-rig character's, into one in Assets/Models/Generic (Prepare Generic Rig Models, guessing its setup).
    /// A generic-rig character's prefab has its body as its model source defines it (see SaveVariant). Undone by deleting its folder
    /// (and a copied model's). See docs/adding-a-character.md and docs/generic-rigs.md.
    /// </summary>
    public static class NewCharacter
    {
        public const string CharactersFolder = "Assets/Characters";
        private const string FoxMovementPath = "Assets/ScriptableObjects/Fox Movement.asset";

        public enum Movement { ShareTheDefault, CopyTheDefault, CopyFox }

        /// <summary>The window's answers.</summary>
        public sealed class Answers
        {
            /// <summary>Its name, which its folder and assets are named after.</summary>
            public string name = "";
            /// <summary>A model source Prepare Humanoid Models has made, or null to prepare modelFile.</summary>
            public CharacterModelSource modelSource;
            /// <summary>A rigged model file in the project, prepared on the spot, when there's no model source.</summary>
            public GameObject modelFile;
            /// <summary>Whether modelFile is a generic-rig character's (not a humanoid: prepared by the Generic-rig character Model Importer).</summary>
            public bool genericRig;
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
            if (answers.movement == Movement.CopyFox && AssetDatabase.LoadAssetAtPath<MovementMode>(FoxMovementPath) == null) { return "The fox's movement isn't in this project."; }
            if (AssetDatabase.LoadAssetAtPath<GameObject>(CharacterSceneSetup.CharacterPrefabPath) == null) { return $"There's no Character prefab ({CharacterSceneSetup.CharacterPrefabPath}) to make it from."; }
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

            CharacterModelSource source = answers.modelSource != null ? answers.modelSource
                : answers.genericRig ? PrepareGenericRig(answers.modelFile, name, made) : PrepareModel(answers.modelFile, name, made);
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
                string from = answers.movement == Movement.CopyFox ? FoxMovementPath
                    : AssetDatabase.GetAssetPath(AssetDatabase.LoadAssetAtPath<GameObject>(CharacterSceneSetup.CharacterPrefabPath).GetComponent<MMotionMotor>().movementMode);
                string to = $"{folder}/{name} Movement.asset";
                AssetDatabase.CopyAsset(from, to);
                mode = AssetDatabase.LoadAssetAtPath<MovementMode>(to);
                made.Add($"{to} (its movement, a copy of {Path.GetFileNameWithoutExtension(from)})");
            }

            string prefabPath = $"{folder}/{name}.prefab";
            GameObject prefab = SaveVariant(name, prefabPath, source, mode);
            made.Add($"{prefabPath} (its prefab, a variant of Character.prefab)");
            AssetDatabase.SaveAssets();
            return prefab;
        }

        /// <summary>
        /// Saves a character prefab at the path: a variant of Character.prefab named name, wearing the model source, moving
        /// by the mode (null: Character.prefab's). A generic-rig character's (its source a generic-rig character's: see GenericRigModelImporter) also
        /// has its body as its source defines it (its Body Definition, not fitted to the model, held above its legs over
        /// steps: Hover Over Steps) and, while its model loads, its eye height. Like the Character prefab, it has no
        /// collider of its own: its body is a shape, and its model's hitboxes are what meets it.
        /// </summary>
        public static GameObject SaveVariant(string name, string prefabPath, CharacterModelSource source, MovementMode mode)
        {
            var basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterSceneSetup.CharacterPrefabPath);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
            try
            {
                instance.name = name;
                var movementController = instance.GetComponent<MMotionMotor>();
                if (mode != null) { movementController.movementMode = mode; }
                bool genericRig = GenericRigModelImporter.IsGenericRig(source);
                if (genericRig)
                {
                    var moved = new SerializedObject(movementController);
                    moved.FindProperty("bodyCollider").objectReferenceValue = null;
                    // A long body held above its legs, over steps, so it never rests on a step's edge or its corners.
                    moved.FindProperty("hoverOverSteps").boolValue = true;
                    moved.FindProperty("bodyDefinition").objectReferenceValue = source.BodyDefinition;
                    moved.ApplyModifiedPropertiesWithoutUndo();
                }
                var host = new SerializedObject(instance.GetComponent<CharacterModelSlot>());
                host.FindProperty("modelSource").objectReferenceValue = source;
                if (genericRig)
                {
                    host.FindProperty("bodyFit").enumValueIndex = (int)BodyFit.Off;
                    host.FindProperty("defaultEyeHeight").floatValue = GenericRigModelImporter.EyeHeightOf(source);
                }
                host.ApplyModifiedPropertiesWithoutUndo();
                return PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
            }
            finally { Object.DestroyImmediate(instance); }
        }

        /// <summary>Copies a model file into Assets/Models/Humanoid/[name] (unless it's there already) and prepares it.</summary>
        private static CharacterModelSource PrepareModel(GameObject modelFile, string name, List<string> made)
        {
            string path = AssetDatabase.GetAssetPath(modelFile);
            if (!path.StartsWith(HumanoidModelImporter.ModelsFolder + "/"))
            {
                string folder = $"{HumanoidModelImporter.ModelsFolder}/{name}";
                if (!AssetDatabase.IsValidFolder(folder)) { AssetDatabase.CreateFolder(HumanoidModelImporter.ModelsFolder, name); }
                string copied = $"{folder}/{Path.GetFileName(path)}";
                AssetDatabase.CopyAsset(path, copied);
                made.Add($"{copied} (its model file, copied in to be prepared)");
                path = copied;
            }
            if (!HumanoidModelImporter.LoadSharedAssets(out var controller, out var translator)) { return null; }
            HumanoidModelImporter.Result result = HumanoidModelImporter.Prepare(path, controller, translator);
            if (!result.Succeeded)
            {
                Debug.LogError($"[New Character] Couldn't prepare {path}: {result.problem}");
                return null;
            }
            made.Add($"{AssetDatabase.GetAssetPath(result.definition)} (its model, prepared)");
            return result.definition;
        }

        /// <summary>
        /// Copies a generic-rig character's model file into Assets/Models/Generic/[name] (unless it's in a generic-rig character's folder already) and
        /// prepares it (Prepare Generic Rig Models), making its setup, its bones and clips guessed from their names.
        /// </summary>
        private static CharacterModelSource PrepareGenericRig(GameObject modelFile, string name, List<string> made)
        {
            string path = AssetDatabase.GetAssetPath(modelFile);
            if (!GenericRigModelImporter.InModelsFolder(path))
            {
                string folder = $"{GenericRigModelImporter.ModelsFolder}/{name}";
                if (!AssetDatabase.IsValidFolder(GenericRigModelImporter.ModelsFolder)) { AssetDatabase.CreateFolder("Assets/Models", "Generic-rig characters"); }
                if (!AssetDatabase.IsValidFolder(folder)) { AssetDatabase.CreateFolder(GenericRigModelImporter.ModelsFolder, name); }
                string copied = $"{folder}/{Path.GetFileName(path)}";
                AssetDatabase.CopyAsset(path, copied);
                made.Add($"{copied} (its model file, copied in to be prepared)");
                path = copied;
            }
            GenericRigModelImporter.Result result = GenericRigModelImporter.Prepare(path);
            made.AddRange(result.notes.Select(note => $"  ({note})"));
            if (!result.Succeeded)
            {
                Debug.LogError($"[New Character] Couldn't prepare {path}: {result.problem}");
                return null;
            }
            made.Add($"{AssetDatabase.GetAssetPath(result.source)} (its model, prepared as a generic-rig character: its setup is '{GenericRigModelImporter.SetupPathFor(path)}')");
            return result.source;
        }

        /// <summary>Sets up the open scene to spawn the character (Set Up Scene), with Undo.</summary>
        public static bool SetUpOpenScene(GameObject prefab) =>
            CharacterSceneSetup.SetUp(EditorSceneManager.GetActiveScene(), new CharacterSceneSetup.Settings { characterPrefab = prefab }, withUndo: true);

        /// <summary>
        /// For -executeMethod: makes the character -characterName [name] wearing -model [a prepared model's name or source
        /// path, or a model file's path, with -genericRig for a generic-rig character's], with -movement [ShareTheDefault|CopyTheDefault|
        /// CopyFox] and optionally -animationSet [path], then exits (code 1 if it can't).
        /// </summary>
        public static void MakeFromCommandLine()
        {
            var answers = new Answers { name = CommandLine.Argument("-characterName") ?? "" };
            string model = CommandLine.Argument("-model");
            if (model != null)
            {
                answers.modelSource = AssetDatabase.LoadAssetAtPath<CharacterModelSource>(model);
                if (answers.modelSource == null)
                {
                    answers.modelSource = HumanoidModelImporter.FindPreparedSources().Concat(GenericRigModelImporter.FindPreparedSources()).FirstOrDefault(d => d.name == model);
                }
                if (answers.modelSource == null) { answers.modelFile = AssetDatabase.LoadAssetAtPath<GameObject>(model); }
            }
            answers.genericRig = System.Environment.GetCommandLineArgs().Contains("-genericRig");
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
