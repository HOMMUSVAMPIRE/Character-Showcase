using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MeridianMotion.Characters;
using UnityEditor;
using UnityEngine;

namespace MeridianMotion.EditorTools
{
    /// <summary>
    /// New Ability (Tools > Meridian Motion > New Ability…): asks where the ability goes (the project, or the package
    /// itself), its name, what it does with the character, what starts it and where it goes in the order, shows every
    /// file it will make or change (AbilityScaffold), and makes them: a small working ability, with its state, events and
    /// tests (and, in the package, its docs), that a person then turns into theirs. Once Unity has compiled it, the
    /// movement mode assets are given it (MovementModeSync): in the project, the project's own that were chosen, turned on;
    /// in the package, every one, turned on for the default character and off for the others. See docs/adding-an-ability.md.
    /// </summary>
    public class NewAbilityWindow : EditorWindow
    {
        private const string TemplatesFolderName = "NewAbilityTemplates";
        // Set while a new ability waits for Unity to compile it, to give it to the movement modes then (see AfterCompiling).
        private const string PendingKey = "MeridianMotion.NewAbility.Pending";

        private AbilityScaffold.Answers answers = new();
        private Vector2 scroll;
        // The project's own movement assets, and which are chosen to be given the ability (Your Project).
        private List<string> projectModes;
        private readonly HashSet<string> unchosenModes = new();

        /// <summary>What a made ability waits for, to be given to the movement modes once it's compiled (see AfterCompiling).</summary>
        [Serializable]
        private sealed class Pending
        {
            public string name;
            public AbilityScaffold.Target target;
            public string after;
            public List<string> modes;
        }

        /// <summary>Whether the package can't be changed (installed read-only): New Ability makes only a project's abilities then.</summary>
        private static bool PackageIsReadOnly => MeridianMotionAssets.IsReadOnly(MeridianMotionAssets.CharacterPrefab);

        [MenuItem("Tools/Meridian Motion/New Ability…", priority = 12)]
        public static void Open() => GetWindow<NewAbilityWindow>(true, "New Ability").minSize = new Vector2(480f, 520f);

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.HelpBox("Makes a new movement ability that works as it is (it hovers, or boosts the walk), with its " +
                                    "tests and docs, for you to turn into yours. Nothing is changed until you press Make It.", MessageType.Info);

            if (PackageIsReadOnly) { answers.target = AbilityScaffold.Target.Project; }
            else
            {
                answers.target = (AbilityScaffold.Target)EditorGUILayout.EnumPopup(new GUIContent("Where",
                    "Project: in your project's own folder and assembly (Abilities, in the output folder: Project Settings > Meridian " +
                    "Motion), so a package upgrade can't overwrite it. Package: in Meridian Motion itself, for developing the package " +
                    "(its flags, its default components, its docs)."), answers.target);
            }
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
                "which every humanoid model gets once Prepare Humanoid Models runs."), answers.needsIK);
            answers.area = (AbilityScaffold.Area)EditorGUILayout.EnumPopup(new GUIContent("Kind",
                "Abilities: what the character does by itself. Traversal: getting about the level (climbing, ropes, water). Picks its " +
                "docs page and its tests' folder."), answers.area);
            DrawOrder();
            if (answers.target == AbilityScaffold.Target.Project) { DrawProjectModes(); }

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
                EditorGUILayout.LabelField(answers.target == AbilityScaffold.Target.Project
                        ? $"Then, once Unity has compiled it, the movement assets chosen above ({answers.modes?.Count ?? 0}), turned on."
                        : "Then, once Unity has compiled it, the movement modes: turned on for the default character's, off for the others'.",
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

        // Your Project: the project's own movement assets, each to be given the ability or not (all are, unless unticked).
        private void DrawProjectModes()
        {
            projectModes ??= MovementModeSync.ProjectModes();
            EditorGUILayout.LabelField(new GUIContent("Give It To", "The project's own movement assets to add it to, turned on, once it's " +
                "compiled (the package's are left alone). Add it to another later with its inspector's Add Component."), EditorStyles.boldLabel);
            if (projectModes.Count == 0)
            {
                EditorGUILayout.HelpBox("The project has no movement assets of its own yet, so none will be given it: make a character with " +
                                        "New Character (its movement is a copy of its own), then add it there with the movement's inspector.", MessageType.None);
            }
            foreach (string path in projectModes)
            {
                bool chosen = EditorGUILayout.ToggleLeft(path, !unchosenModes.Contains(path));
                if (chosen) { unchosenModes.Remove(path); }
                else { unchosenModes.Add(path); }
            }
            answers.modes = projectModes.Where(p => !unchosenModes.Contains(p)).ToList();
        }

        private void Make(List<AbilityScaffold.Change> changes)
        {
            List<string> written = AbilityScaffold.Write(changes, ProjectRoot);
            Debug.Log($"[New Ability] Made {answers.name}:\n" + string.Join("\n", written));
            Wait(answers);
            AssetDatabase.Refresh();
            string component = AbilityScaffold.ComponentPath(answers, new AbilityScaffold.Names(answers.name));
            AssetDatabase.OpenAsset(AssetDatabase.LoadAssetAtPath<MonoScript>(component));
            Close();
        }

        // Keeps what the movement modes are to be given, for AfterCompiling (the domain reloads in between).
        private static void Wait(AbilityScaffold.Answers made) =>
            SessionState.SetString(PendingKey, JsonUtility.ToJson(new Pending { name = made.name.Trim(), target = made.target, after = made.after, modes = made.modes }));

        /// <summary>
        /// Once Unity has compiled a new ability, gives it to the movement mode assets: a project's to the project's own that
        /// were chosen (MovementModeSync.AddToProjectModes), turned on; the package's to every one (AddToAllModes). And says
        /// what's next.
        /// </summary>
        [InitializeOnLoadMethod]
        private static void AfterCompiling()
        {
            string json = SessionState.GetString(PendingKey, "");
            if (json.Length == 0) { return; }
            EditorApplication.delayCall += () =>
            {
                SessionState.EraseString(PendingKey);
                Pending pending;
                try { pending = JsonUtility.FromJson<Pending>(json); }
                catch (ArgumentException) { pending = new Pending { name = json, target = AbilityScaffold.Target.Package }; }
                if (pending.target == AbilityScaffold.Target.Package)
                {
                    int added = MovementModeSync.AddToAllModes();
                    Debug.Log($"[New Ability] {pending.name} is in the movement modes ({added} added): turned on for the default character's, " +
                              "off for the others. Next: write what it does in its component, run its tests (python3 Tools/run_tests.py " +
                              "--changed), and describe it in its docs section. See docs/adding-an-ability.md.");
                    return;
                }
                Type type = ProjectAbilityType(pending.name);
                if (type == null)
                {
                    Debug.LogError($"[New Ability] {pending.name} isn't compiled (see the Console's errors), so no movement asset was given it: " +
                                   "once it compiles, add it with a movement asset's inspector (Add Component).");
                    return;
                }
                List<string> modes = MovementModeSync.AddToProjectModes(type, pending.after, pending.modes ?? new List<string>());
                Debug.Log($"[New Ability] {pending.name} is made{(modes.Count > 0 ? $", and turned on in {string.Join(", ", modes)}" : ", and in no movement asset yet")}. " +
                          "Next: write what it does in its component, run its tests (Test Runner, PlayMode: " + AbilityScaffold.ProjectTestsAssembly +
                          "), and add it to a character's movement with its inspector. See docs/adding-an-ability.md.");
            };
        }

        /// <summary>A project's ability's component type, by its class name (in the project's abilities' assembly), or null.</summary>
        public static Type ProjectAbilityType(string name) =>
            TypeCache.GetTypesDerivedFrom<MovementComponent>()
                .FirstOrDefault(t => t.Name == name && t.Assembly.GetName().Name == AbilityScaffold.ProjectAssembly);

        /// <summary>
        /// For -executeMethod: makes the ability given by -abilityName [Name], with -target [Project|Package] (Project unless
        /// given), -kind [TakesOver|OnTopOfWalking], -button [None|Jump|Dash|Crouch|Roll|Interact], -after [a default
        /// component, or first], -area [Abilities|Traversal] and -levelElement and -needsIK, then exits (code 1 if it can't).
        /// Once it's compiled, give it to the movement modes: a project's with AddToProjectModesFromCommandLine (the same
        /// -abilityName and -after, and -modes), a package's with MovementModeSync.AddToAllModesFromCommandLine.
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
                | !TryParse(CommandLine.Argument("-area"), ref made.area) | !TryParse(CommandLine.Argument("-target"), ref made.target))
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
            Debug.Log($"[New Ability] Made {made.name}:\n" + string.Join("\n", written) + "\nNext, once it's compiled: -executeMethod " +
                      (made.target == AbilityScaffold.Target.Project
                          ? $"MeridianMotion.EditorTools.NewAbilityWindow.AddToProjectModesFromCommandLine -abilityName {made.name}, to give it to the project's movement assets."
                          : "MeridianMotion.EditorTools.MovementModeSync.AddToAllModesFromCommandLine, to give it to the movement modes."));
            CommandLine.Finish(true);
        }

        /// <summary>
        /// For -executeMethod, once a project's ability made with MakeFromCommandLine is compiled: gives the ability
        /// -abilityName [Name] to the project's own movement assets (-modes: all, unless given none or paths split by ';'),
        /// turned on, just after -after [a component, or first] (last if a mode hasn't it), then exits (code 1 if it can't).
        /// </summary>
        public static void AddToProjectModesFromCommandLine()
        {
            string name = CommandLine.Argument("-abilityName") ?? "";
            Type type = ProjectAbilityType(name);
            if (type == null)
            {
                Debug.LogError($"[New Ability] There's no ability called '{name}' in the {AbilityScaffold.ProjectAssembly} assembly (is it compiled?).");
                CommandLine.Finish(false);
                return;
            }
            string after = CommandLine.Argument("-after") ?? nameof(Rolling);
            string modes = CommandLine.Argument("-modes") ?? "all";
            List<string> paths = modes == "all" ? MovementModeSync.ProjectModes()
                : modes == "none" ? new List<string>() : modes.Split(';').Select(p => p.Trim()).Where(p => p.Length > 0).ToList();
            List<string> added = MovementModeSync.AddToProjectModes(type, after == "first" ? null : after, paths);
            Debug.Log($"[New Ability] {name} added to {added.Count} movement asset(s).");
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
            return "Assets/MeridianMotion/Editor/Movement/" + TemplatesFolderName;
        }
    }
}
