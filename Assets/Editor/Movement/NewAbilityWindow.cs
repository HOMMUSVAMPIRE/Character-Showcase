using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CharacterShowcase.EditorTools
{
    /// <summary>
    /// New Ability (Tools > Character Showcase > New Ability…): asks for an ability's name, what it does with the
    /// character, what starts it and where it goes in the order, shows every file it will make or change
    /// (AbilityScaffold), and makes them: a small working ability, with its state, events, tests and docs, that a person
    /// then turns into theirs. Once Unity has compiled it, the movement mode assets are given it (MovementModeSync), turned
    /// on for the default character and off for the others. See docs/adding-an-ability.md.
    /// </summary>
    public class NewAbilityWindow : EditorWindow
    {
        private const string TemplatesFolderName = "NewAbilityTemplates";
        // Set while a new ability waits for Unity to compile it, to give it to the movement modes then (see AfterCompiling).
        private const string PendingKey = "CharacterShowcase.NewAbility.Pending";

        private AbilityScaffold.Answers answers = new();
        private Vector2 scroll;

        [MenuItem("Tools/Character Showcase/New Ability…", priority = 10)]
        public static void Open() => GetWindow<NewAbilityWindow>(true, "New Ability").minSize = new Vector2(480f, 520f);

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.HelpBox("Makes a new movement ability that works as it is (it hovers, or boosts the walk), with its " +
                                    "tests and docs, for you to turn into yours. Nothing is changed until you press Make It.", MessageType.Info);

            answers.name = EditorGUILayout.TextField(new GUIContent("Name", "Its class name: a capital letter first, no spaces (Gliding, say). " +
                "Its inspector name is this spaced out."), answers.name);
            answers.kind = (AbilityScaffold.Kind)EditorGUILayout.EnumPopup(new GUIContent("What It Does",
                "Takes Over: it has the character while it's under way (as a zipline or a wall run does), and the abilities below it " +
                "in the order don't run. On Top Of Walking: it adds to the character's own movement (as a dash does)."), answers.kind);
            answers.button = (AbilityScaffold.StartButton)EditorGUILayout.EnumPopup(new GUIContent("Started By",
                "The button a press of which starts it, or None. A button of its own is a step of its own (see AGENTS.md, Input)."), answers.button);
            answers.levelElement = EditorGUILayout.Toggle(new GUIContent("At A Level Element",
                "It starts near a level element of its own, made with it (as a zipline is caught near a cable)."), answers.levelElement);
            answers.needsIK = EditorGUILayout.Toggle(new GUIContent("Needs IK",
                "It gets an IK component of its own, placing the limbs while it's under way (hands on what it holds, say), " +
                "which every humanoid model gets once Prepare Character Models runs."), answers.needsIK);
            answers.area = (AbilityScaffold.Area)EditorGUILayout.EnumPopup(new GUIContent("Kind",
                "Abilities: what the character does by itself. Traversal: getting about the level (climbing, ropes, water). Picks its " +
                "docs page and its tests' folder."), answers.area);
            DrawOrder();

            string problem = AbilityScaffold.Problem(answers, ReadProjectFile);
            EditorGUILayout.Space();
            if (problem != null)
            {
                EditorGUILayout.HelpBox(problem, MessageType.Warning);
            }
            else
            {
                List<AbilityScaffold.Change> changes = AbilityScaffold.Plan(answers, ReadProjectFile, ReadTemplate);
                EditorGUILayout.LabelField("It will make or change", EditorStyles.boldLabel);
                foreach (AbilityScaffold.Change change in changes)
                {
                    EditorGUILayout.LabelField($"{(change.isNew ? "New" : "Changed")}: {change.path}", EditorStyles.miniBoldLabel);
                    EditorGUILayout.LabelField("    " + change.summary, EditorStyles.wordWrappedMiniLabel);
                }
                EditorGUILayout.LabelField("Then, once Unity has compiled it, the movement modes: turned on for the default character's, off for the others'.",
                                           EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.Space();
                if (GUILayout.Button("Make It", GUILayout.Height(28f))) { Make(changes); }
            }
            EditorGUILayout.EndScrollView();
        }

        // Where it goes: just after one of the default components, chosen from their list.
        private void DrawOrder()
        {
            List<string> order = AbilityScaffold.DefaultComponentNames();
            var choices = new List<string> { "(first)" };
            choices.AddRange(order.Select(n => "After " + n));
            int index = answers.after == null ? 0 : Math.Max(0, order.IndexOf(answers.after) + 1);
            index = EditorGUILayout.Popup(new GUIContent("Place In The Order", "Where several abilities could take the character over in a " +
                "step, the one higher in the list does; among the others acting at the same point of a step, list order decides. " +
                "See docs/movement.md, Movement Components."), index, choices.ToArray());
            answers.after = index == 0 ? null : order[index - 1];
        }

        private void Make(List<AbilityScaffold.Change> changes)
        {
            List<string> written = AbilityScaffold.Write(changes, ProjectRoot);
            Debug.Log($"[New Ability] Made {answers.name}:\n" + string.Join("\n", written));
            SessionState.SetString(PendingKey, answers.name);
            AssetDatabase.Refresh();
            string component = AbilityScaffold.ComponentPath(new AbilityScaffold.Names(answers.name));
            AssetDatabase.OpenAsset(AssetDatabase.LoadAssetAtPath<MonoScript>(component));
            Close();
        }

        /// <summary>
        /// Once Unity has compiled a new ability, gives it to every movement mode asset (MovementModeSync.AddToAllModes),
        /// and says what's next.
        /// </summary>
        [InitializeOnLoadMethod]
        private static void AfterCompiling()
        {
            string pending = SessionState.GetString(PendingKey, "");
            if (pending.Length == 0) { return; }
            EditorApplication.delayCall += () =>
            {
                SessionState.EraseString(PendingKey);
                int added = MovementModeSync.AddToAllModes();
                Debug.Log($"[New Ability] {pending} is in the movement modes ({added} added): turned on for the default character's, " +
                          "off for the others. Next: write what it does in its component, run its tests (python3 Tools/run_tests.py " +
                          "--changed), and describe it in its docs section. See docs/adding-an-ability.md.");
            };
        }

        /// <summary>
        /// For -executeMethod: makes the ability given by -abilityName [Name], with -kind [TakesOver|OnTopOfWalking],
        /// -button [None|Jump|Dash|Crouch|Roll|Grab], -after [a default component, or first], -area [Abilities|Traversal] and
        /// -levelElement and -needsIK, then exits (code 1 if it can't). Run MovementModeSync.AddToAllModesFromCommandLine after it, once
        /// it's compiled, to give it to the movement modes.
        /// </summary>
        public static void MakeFromCommandLine()
        {
            var made = new AbilityScaffold.Answers
            {
                name = CommandLine.Argument("-abilityName") ?? "",
                levelElement = CommandLine.Has("-levelElement"),
                needsIK = CommandLine.Has("-needsIK"),
            };
            string after = CommandLine.Argument("-after");
            if (after != null) { made.after = after == "first" ? null : after; }
            if (!TryParse(CommandLine.Argument("-kind"), ref made.kind) | !TryParse(CommandLine.Argument("-button"), ref made.button)
                | !TryParse(CommandLine.Argument("-area"), ref made.area))
            {
                CommandLine.Finish(false);
                return;
            }
            string problem = AbilityScaffold.Problem(made, ReadProjectFile);
            if (problem != null)
            {
                Debug.LogError("[New Ability] " + problem);
                CommandLine.Finish(false);
                return;
            }
            List<string> written = AbilityScaffold.Write(AbilityScaffold.Plan(made, ReadProjectFile, ReadTemplate), ProjectRoot);
            Debug.Log($"[New Ability] Made {made.name}:\n" + string.Join("\n", written) +
                      "\nNext: -executeMethod CharacterShowcase.EditorTools.MovementModeSync.AddToAllModesFromCommandLine, to give it to the movement modes.");
            CommandLine.Finish(true);
        }

        private static bool TryParse<T>(string value, ref T result) where T : struct, Enum
        {
            if (value == null) { return true; }
            if (Enum.TryParse(value, true, out T parsed)) { result = parsed; return true; }
            Debug.LogError($"[New Ability] '{value}' isn't one of {string.Join(", ", Enum.GetNames(typeof(T)))}.");
            return false;
        }

        private static string ProjectRoot => Path.GetDirectoryName(Application.dataPath);

        /// <summary>A project file's text by its path from the project's folder, or null if it isn't there.</summary>
        public static string ReadProjectFile(string path)
        {
            string full = Path.Combine(ProjectRoot, path);
            return File.Exists(full) ? File.ReadAllText(full) : null;
        }

        /// <summary>A template's text, from the NewAbilityTemplates folder beside this window's script (wherever it's installed).</summary>
        public static string ReadTemplate(string name)
        {
            string folder = TemplatesFolder();
            string path = Path.Combine(ProjectRoot, folder, name + ".txt");
            if (!File.Exists(path)) { throw new FileNotFoundException($"New Ability's template {name}.txt isn't in {folder}."); }
            return File.ReadAllText(path);
        }

        private static string TemplatesFolder()
        {
            foreach (string guid in AssetDatabase.FindAssets(TemplatesFolderName))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetDatabase.IsValidFolder(path) && Path.GetFileName(path) == TemplatesFolderName) { return path; }
            }
            return "Assets/Editor/Movement/" + TemplatesFolderName;
        }
    }
}
