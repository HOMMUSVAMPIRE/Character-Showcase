using System;
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
    /// Start Here (Tools > Meridian Motion > Start Here): one window for every everyday task, in sections (play it,
    /// characters, abilities, checking your work, learning), each button with a line saying what it does; the test and
    /// hygiene scripts (in Tools/), run in a terminal, are named with their commands. What isn't in the project (those
    /// scripts, the docs, the levels, in a package without them) is left out, not greyed. It opens by itself the first time a project is opened on a machine, unless its Open On First
    /// Load is turned off (a setting per machine).
    /// </summary>
    public class StartHereWindow : EditorWindow
    {
        private const string Title = "Start Here";
        // Per machine: whether it opens by itself on a project's first load, and the projects it has opened in.
        private const string OpenOnFirstLoadKey = "CharacterShowcase.StartHere.OpenOnFirstLoad";
        private const string OpenedKeyPrefix = "CharacterShowcase.StartHere.Opened:";
        private const string ScenesFolder = "Assets/Scenes";
        private const string TestsScript = "Tools/run_tests.py";
        private const string HygieneScript = "Tools/CI/check_project_hygiene.py";
        private const string DocsFolder = "docs";

        private Vector2 scroll;
        private GUIStyle descriptionStyle;

        [MenuItem("Tools/Meridian Motion/Start Here", priority = 0)]
        public static void Open() => GetWindow<StartHereWindow>(false, Title).minSize = new Vector2(420f, 480f);

        [InitializeOnLoadMethod]
        private static void OpenOnFirstLoad()
        {
            if (Application.isBatchMode || !EditorPrefs.GetBool(OpenOnFirstLoadKey, true)) { return; }
            string opened = OpenedKeyPrefix + ProjectRoot;
            if (EditorPrefs.GetBool(opened, false)) { return; }
            EditorPrefs.SetBool(opened, true);
            // Once the editor's up: windows can't open while it's still loading.
            EditorApplication.delayCall += Open;
        }

        private static string ProjectRoot => Path.GetDirectoryName(Application.dataPath);
        private static bool InProject(string relative) => File.Exists(Path.Combine(ProjectRoot, relative)) || Directory.Exists(Path.Combine(ProjectRoot, relative));

        private void OnGUI()
        {
            descriptionStyle ??= new GUIStyle(EditorStyles.wordWrappedMiniLabel);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField("Meridian Motion", EditorStyles.largeLabel);
            EditorGUILayout.LabelField("Everything you'll use most, in one place. Every tool here is also under Tools > Meridian Motion.", EditorStyles.wordWrappedLabel);

            Section("Play");
            foreach (string scene in Levels())
            {
                Button($"Open {Path.GetFileNameWithoutExtension(scene)}", "Open this level, then press Play (the ▶ at the top of the editor).", () => OpenScene(scene));
            }
            Button("Set Up Scene…", "Puts a character, a camera and spawn points in the open scene, wearing the model you choose.", SceneSetupWindow.Open);

            Section("Characters");
            Button("New Character…", "Makes a character of your own from a model: its prefab, movement and body, ready to play.", NewCharacterWindow.Open);
            Button("Character Doctor", "Checks a character prefab and says what's wrong, with a fix where a tool can make one.", CharacterDoctorWindow.Open);
            Button("Prepare Humanoid Models", "Makes every humanoid model in Assets/Models/Humanoid ready to wear (it runs by itself when one's added).", HumanoidModelImporter.PrepareAllFromMenu);
            Button("Prepare Generic Rig Models", "Makes every generic-rig character in Assets/Models/Generic ready (it runs by itself when one's added).", GenericRigModelImporter.PrepareAllFromMenu);

            Section("Abilities");
            Button("New Ability…", "Makes a small working ability to build on, with its tests and docs.", NewAbilityWindow.Open);
            MovementMode mode = DefaultMovementMode();
            if (mode != null)
            {
                Button("The Movement Mode", $"Selects {mode.name}, the default character's movement: every ability and its settings, to tune.", () => Select(mode));
            }
            Button("Tuning Presets", "Selects the saved tunings (to apply one, select it); save one from the movement mode's inspector.", SelectPresets);

            Section("Check my work");
            Button("Test Runner", "Unity's window for running the tests (its EditMode and PlayMode tabs).", () => EditorApplication.ExecuteMenuItem("Window/General/Test Runner"));
            if (InProject(TestsScript))
            {
                EditorGUILayout.LabelField($"Or close Unity and run, in a terminal at the project's folder: python3 {TestsScript} --changed (the tests your changes can affect).", descriptionStyle);
            }
            if (InProject(HygieneScript))
            {
                EditorGUILayout.LabelField($"The hygiene check (the project's conventions: missing .meta files, an ability without its tests or docs, ...) " +
                                           $"takes a few seconds, with Unity open or not: python3 {HygieneScript}", descriptionStyle);
            }

            var docs = new List<(string label, string page, string what)>
            {
                ("Unity Basics", "unity-basics.md", "The Unity words the docs use, for anyone new to Unity."),
                ("Adding a Character", "adding-a-character.md", "A new look, a character of your own, or a generic-rig character: step by step."),
                ("Your First Ability", "tutorial-first-ability.md", "Builds one small ability (Ground Pound) from start to finish."),
                ("Adding a Movement Ability", "adding-an-ability.md", "The full reference for every part of an ability."),
                ("Troubleshooting", "troubleshooting.md", "When something's wrong."),
            }.Where(d => InProject(Path.Combine(DocsFolder, d.page))).ToList();
            if (docs.Count > 0)
            {
                Section("Learn");
                foreach (var (label, page, what) in docs)
                {
                    Button(label, what, () => Application.OpenURL(new Uri(Path.Combine(ProjectRoot, DocsFolder, page)).AbsoluteUri));
                }
            }

            EditorGUILayout.Space(12f);
            EditorGUI.BeginChangeCheck();
            bool openOnFirstLoad = EditorGUILayout.ToggleLeft(new GUIContent("Open On First Load",
                "Open this window by itself the first time a project is opened on this machine"), EditorPrefs.GetBool(OpenOnFirstLoadKey, true));
            if (EditorGUI.EndChangeCheck()) { EditorPrefs.SetBool(OpenOnFirstLoadKey, openOnFirstLoad); }
            EditorGUILayout.EndScrollView();
        }

        private static void Section(string title)
        {
            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        }

        private void Button(string label, string what, Action action)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(label, GUILayout.Width(190f)))
                {
                    // Not while the window is being drawn: a tool may open another window, or a dialog.
                    EditorApplication.delayCall += () => action();
                }
                EditorGUILayout.LabelField(what, descriptionStyle);
            }
        }

        /// <summary>The level scenes in Assets/Scenes, by name.</summary>
        private static IEnumerable<string> Levels() =>
            AssetDatabase.IsValidFolder(ScenesFolder)
                ? AssetDatabase.FindAssets("t:Scene", new[] { ScenesFolder }).Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => Path.GetFileName(p), StringComparer.Ordinal)
                : Enumerable.Empty<string>();

        private static void OpenScene(string path)
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) { EditorSceneManager.OpenScene(path); }
        }

        private static MovementMode DefaultMovementMode()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterSceneSetup.CharacterPrefabPath);
            return prefab != null && prefab.TryGetComponent(out MMotionMotor movement) ? movement.movementMode : null;
        }

        private static void Select(UnityEngine.Object asset)
        {
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }

        private static void SelectPresets()
        {
            UnityEngine.Object[] presets = AssetDatabase.FindAssets("t:" + nameof(MovementTuningPreset))
                .Select(guid => AssetDatabase.LoadAssetAtPath<MovementTuningPreset>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(p => p != null).Cast<UnityEngine.Object>().ToArray();
            if (presets.Length == 0)
            {
                EditorUtility.DisplayDialog("Tuning Presets", "There are no tuning presets yet. To save one, select the movement mode and press " +
                                                              "Save Tuning as Preset at the bottom of its inspector.", "OK");
                MovementMode mode = DefaultMovementMode();
                if (mode != null) { Select(mode); }
                return;
            }
            Selection.objects = presets;
            EditorGUIUtility.PingObject(presets[0]);
        }
    }
}
