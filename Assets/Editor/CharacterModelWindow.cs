using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Character;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CharacterShowcase.EditorTools
{
    /// <summary>
    /// Chooses which character model the sample scene's character uses, from the models Prepare Character Models has set
    /// up in Assets/Models/CharacterModels. The choice is an override on the scene's instance of the Character prefab, so
    /// the prefab itself (and the tests that spawn it) keeps the mannequin.
    /// </summary>
    public sealed class CharacterModelWindow : EditorWindow
    {
        public const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const string CharacterPrefabPath = "Assets/Prefabs/Character.prefab";
        private const float PreviewSize = 64f;

        private sealed class Entry
        {
            public DirectCharacterModelSource definition;
            public string name;
            public string problem;
            public float height;
        }

        private readonly List<Entry> ready = new();
        private readonly List<Entry> notReady = new();
        private readonly List<string> unprepared = new();
        private CharacterModelSource inUse;
        // Heights by model prefab: measuring means instantiating the model, so it isn't redone every time the window refreshes.
        private readonly Dictionary<GameObject, float> heights = new();
        private Vector2 scroll;

        [MenuItem("Tools/Character Showcase/Choose Character Model…", priority = 3)]
        public static void Open() => GetWindow<CharacterModelWindow>("Character Model").Refresh();

        private void OnEnable() => Refresh();
        private void OnFocus() => Refresh();
        private void OnProjectChange()
        {
            heights.Clear(); // A model may have been re-prepared.
            // Wait for the editor to finish what it's doing first: the project changes many times while models are
            // prepared or the mannequin is generated, and a model caught partway through would be listed as broken.
            EditorApplication.delayCall -= RefreshIfOpen;
            EditorApplication.delayCall += RefreshIfOpen;
        }

        private void RefreshIfOpen()
        {
            if (this != null) { Refresh(); }
        }
        private void OnHierarchyChange() => inUse = CurrentModel();

        private void Refresh()
        {
            ready.Clear();
            notReady.Clear();
            unprepared.Clear();
            foreach (DirectCharacterModelSource definition in FindDefinitions())
            {
                var entry = new Entry
                {
                    definition = definition,
                    name = definition.name,
                    problem = CharacterModelImporter.Validate(definition),
                };
                if (entry.problem == null)
                {
                    if (!heights.TryGetValue(definition.ModelPrefab, out float height))
                    {
                        height = heights[definition.ModelPrefab] = CharacterModelImporter.MeasureHeight(definition.ModelPrefab);
                    }
                    entry.height = height * definition.Scale;
                    ready.Add(entry);
                }
                else
                {
                    notReady.Add(entry);
                }
            }
            // Model files with no definition beside them yet.
            foreach (string modelPath in CharacterModelImporter.FindModelFiles())
            {
                string definitionPath = CharacterModelImporter.DefinitionPathFor(modelPath, CharacterModelImporter.DisplayName(modelPath));
                if (AssetDatabase.LoadAssetAtPath<DirectCharacterModelSource>(definitionPath) == null) { unprepared.Add(modelPath); }
            }
            inUse = CurrentModel();
            Repaint();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Character model for the sample scene", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"The character in {Path.GetFileNameWithoutExtension(ScenePath)} loads the chosen model when " +
                                       "the game starts. Models are listed once Prepare Character Models has set them up.",
                                       EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space();

            scroll = EditorGUILayout.BeginScrollView(scroll);
            if (ready.Count == 0)
            {
                EditorGUILayout.HelpBox($"No models are ready. Put rigged models in subfolders of {CharacterModelImporter.ModelsFolder}, " +
                                        "then press Prepare Character Models.", MessageType.Info);
            }
            foreach (Entry entry in ready) { DrawEntry(entry); }

            if (unprepared.Count > 0 || notReady.Count > 0)
            {
                EditorGUILayout.Space();
                var lines = unprepared.Select(path => $"{path}: not prepared yet")
                    .Concat(notReady.Select(e => $"{e.name}: {e.problem} Run Prepare Character Models again."));
                EditorGUILayout.HelpBox("Not available:\n" + string.Join("\n", lines), MessageType.Warning);
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Prepare Character Models"))
                {
                    CharacterModelImporter.PrepareAllFromMenu();
                    Refresh();
                }
                if (GUILayout.Button("Refresh", GUILayout.Width(80f))) { Refresh(); }
            }
        }

        private void DrawEntry(Entry entry)
        {
            bool isInUse = entry.definition == inUse;
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                Texture preview = AssetPreview.GetAssetPreview(entry.definition.ModelPrefab);
                if (preview == null && AssetPreview.IsLoadingAssetPreview(entry.definition.ModelPrefab.GetEntityId())) { Repaint(); }
                Rect previewRect = GUILayoutUtility.GetRect(PreviewSize, PreviewSize, GUILayout.Width(PreviewSize));
                if (preview != null) { GUI.DrawTexture(previewRect, preview, ScaleMode.ScaleToFit); }

                using (new EditorGUILayout.VerticalScope())
                {
                    EditorGUILayout.LabelField(entry.name + (isInUse ? "  (in use)" : ""), EditorStyles.boldLabel);
                    EditorGUILayout.LabelField($"{entry.height:0.00} m tall", EditorStyles.miniLabel);
                    EditorGUILayout.LabelField(AssetDatabase.GetAssetPath(entry.definition), EditorStyles.miniLabel);
                }
                using (new EditorGUI.DisabledScope(isInUse))
                {
                    if (GUILayout.Button(isInUse ? "In Use" : "Use", GUILayout.Width(70f), GUILayout.Height(PreviewSize - 8f)))
                    {
                        if (UseInScene(entry.definition)) { inUse = entry.definition; }
                    }
                }
                if (GUILayout.Button("Select", GUILayout.Width(56f), GUILayout.Height(PreviewSize - 8f)))
                {
                    Selection.activeObject = entry.definition;
                    EditorGUIUtility.PingObject(entry.definition);
                }
            }
        }

        /// <summary>The definitions in the models folder that are ready to use (see CharacterModelImporter.Validate).</summary>
        public static IEnumerable<DirectCharacterModelSource> FindReadyDefinitions() =>
            FindDefinitions().Where(d => CharacterModelImporter.Validate(d) == null);

        private static IEnumerable<DirectCharacterModelSource> FindDefinitions() =>
            AssetDatabase.FindAssets($"t:{nameof(DirectCharacterModelSource)}", new[] { CharacterModelImporter.ModelsFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(path => path, System.StringComparer.Ordinal)
                .Select(AssetDatabase.LoadAssetAtPath<DirectCharacterModelSource>)
                .Where(d => d != null);

        /// <summary>
        /// Makes every character in the sample scene use the given model. If the scene is open, the change is made there
        /// (with undo) for you to save; otherwise the scene is opened alongside, changed, saved and closed again, leaving
        /// whatever is open alone.
        /// </summary>
        public static bool UseInScene(DirectCharacterModelSource definition)
        {
            string problem = CharacterModelImporter.Validate(definition);
            if (problem != null)
            {
                Debug.LogError($"[CharacterModelWindow] '{definition?.name}' can't be used: {problem}");
                return false;
            }

            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            bool wasOpen = scene.isLoaded;
            if (!wasOpen) { scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive); }
            try
            {
                AnimationController[] characters = AnimationControllersIn(scene);
                if (characters.Length == 0)
                {
                    Debug.LogError($"[CharacterModelWindow] No character (AnimationController) found in '{ScenePath}'; it was not changed.");
                    return false;
                }
                foreach (AnimationController character in characters)
                {
                    var serialized = new SerializedObject(character);
                    serialized.FindProperty("modelSource").objectReferenceValue = definition;
                    if (wasOpen) { serialized.ApplyModifiedProperties(); } else { serialized.ApplyModifiedPropertiesWithoutUndo(); }
                }
                // Drop overrides left for fields that no longer exist (such as a renamed one), so they don't pile up.
                GameObject[] instances = characters.Select(c => PrefabUtility.GetOutermostPrefabInstanceRoot(c.gameObject))
                    .Where(root => root != null).Distinct().ToArray();
                if (instances.Length > 0) { PrefabUtility.RemoveUnusedOverrides(instances, wasOpen ? InteractionMode.UserAction : InteractionMode.AutomatedAction); }
                EditorSceneManager.MarkSceneDirty(scene);
                if (!wasOpen) { EditorSceneManager.SaveScene(scene); }
                Debug.Log($"[CharacterModelWindow] The character in '{ScenePath}' now uses '{definition.name}'" +
                          (wasOpen ? ". Save the scene to keep it." : "."));
                return true;
            }
            finally
            {
                if (!wasOpen) { EditorSceneManager.CloseScene(scene, true); }
            }
        }

        /// <summary>For -executeMethod: -model [definition path or name] picks the sample scene's model. Exits with code 1 on failure.</summary>
        public static void UseInSceneFromCommandLine()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            int index = System.Array.IndexOf(args, "-model");
            string wanted = index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
            DirectCharacterModelSource definition = FindReadyDefinitions()
                .FirstOrDefault(d => AssetDatabase.GetAssetPath(d) == wanted || d.name == wanted);
            if (definition == null)
            {
                Debug.LogError($"[CharacterModelWindow] No ready model called '{wanted}'. Ready models: " +
                               string.Join(", ", FindReadyDefinitions().Select(d => d.name)));
                EditorApplication.Exit(1);
                return;
            }
            if (!UseInScene(definition)) { EditorApplication.Exit(1); }
        }

        private static AnimationController[] AnimationControllersIn(Scene scene) =>
            scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<AnimationController>(true)).ToArray();

        /// <summary>
        /// The model the sample scene's character uses. Read from the scene if it's open; otherwise from the scene file
        /// (an override on the Character prefab instance), so checking doesn't open the scene. Falls back to the prefab's own.
        /// </summary>
        private static CharacterModelSource CurrentModel()
        {
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            if (scene.isLoaded)
            {
                AnimationController character = AnimationControllersIn(scene).FirstOrDefault();
                return character != null ? character.ModelSource : null;
            }
            if (File.Exists(ScenePath))
            {
                Match match = Regex.Match(File.ReadAllText(ScenePath),
                    @"propertyPath: (?:modelSource|modelDefinition)\s+value:[^\n]*\n\s+objectReference: \{fileID: \d+, guid: ([0-9a-f]{32})");
                if (match.Success)
                {
                    return AssetDatabase.LoadAssetAtPath<CharacterModelSource>(AssetDatabase.GUIDToAssetPath(match.Groups[1].Value));
                }
            }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterPrefabPath);
            return prefab != null && prefab.TryGetComponent(out AnimationController controller) ? controller.ModelSource : null;
        }
    }
}
