using System.Collections.Generic;
using System.Linq;
using Character;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CharacterShowcase.EditorTools
{
    /// <summary>
    /// Sets up the open scene to spawn a character (see SceneGameSetup): choose the character prefab, the model it
    /// uses (any prepared by Prepare Character Models, or the prefab's own) and its movement mode asset, then confirm.
    /// The scene gets a Game Setup object with a CharacterSpawner, using the scene's own camera rig and input manager
    /// or copies of the demo scene's. Its changes can be undone, and are left for you to save.
    /// </summary>
    public sealed class SceneSetupWindow : EditorWindow
    {
        private const float PreviewSize = 48f;

        private GameObject characterPrefab;
        // Null: the prefab's own model.
        private CharacterModelSource modelSource;
        private MovementMode movementMode;
        private bool replacePlacedCharacters = true;
        private bool turnOffOtherCameras = true;
        // The scene the choices were started from, so opening another scene starts from its own.
        private Scene choicesScene;

        private readonly List<DirectCharacterModelSource> readyModels = new();
        private readonly List<string> unavailableModels = new();
        // Heights by model prefab: measuring means instantiating the model, so it isn't redone on every refresh.
        private readonly Dictionary<GameObject, float> heights = new();
        private Vector2 scroll;

        [MenuItem("Tools/Character Showcase/Set Up Scene…", priority = 3)]
        public static void Open() => GetWindow<SceneSetupWindow>("Scene Setup").Refresh();

        private void OnEnable()
        {
            minSize = new Vector2(360f, 420f);
            Refresh();
        }

        private void OnFocus() => Refresh();
        private void OnHierarchyChange() => Repaint();

        private void OnProjectChange()
        {
            heights.Clear(); // A model may have been re-prepared.
            // Wait for the editor to finish what it's doing first: the project changes many times while models are
            // prepared, and a model caught partway through would be listed as broken.
            EditorApplication.delayCall -= RefreshIfOpen;
            EditorApplication.delayCall += RefreshIfOpen;
        }

        private void RefreshIfOpen()
        {
            if (this != null) { Refresh(); }
        }

        /// <summary>Lists the models that are ready to use, and starts the choices from the open scene (see StartChoices).</summary>
        private void Refresh()
        {
            readyModels.Clear();
            readyModels.AddRange(CharacterModelImporter.FindReadyDefinitions());
            unavailableModels.Clear();
            foreach (string modelPath in CharacterModelImporter.FindModelFiles())
            {
                string name = CharacterModelImporter.DisplayName(modelPath);
                var definition = AssetDatabase.LoadAssetAtPath<DirectCharacterModelSource>(CharacterModelImporter.DefinitionPathFor(modelPath, name));
                string problem = definition == null ? "not prepared yet" : CharacterModelImporter.Validate(definition);
                if (problem != null) { unavailableModels.Add($"{name}: {problem}"); }
            }

            Scene scene = SceneManager.GetActiveScene();
            if (characterPrefab == null || scene != choicesScene) { StartChoices(scene); }
            if (modelSource is DirectCharacterModelSource direct && !readyModels.Contains(direct)) { modelSource = null; }
            if (movementMode == null) { movementMode = PrefabMovementMode(); }
            Repaint();
        }

        /// <summary>
        /// Starts the choices from what the scene has: its existing setup's, or else its placed character's (the prefab
        /// it's an instance of, and its model and movement mode), or else the package's Character prefab as it is.
        /// </summary>
        private void StartChoices(Scene scene)
        {
            choicesScene = scene;
            characterPrefab = null;
            modelSource = null;
            movementMode = null;

            CharacterSpawner existing = SceneGameSetup.FindSpawner(scene);
            ACCharacterController placed = SceneGameSetup.PlacedCharacters(scene).FirstOrDefault();
            if (existing != null && existing.CharacterPrefab != null)
            {
                characterPrefab = existing.CharacterPrefab;
                modelSource = existing.ModelSource;
                movementMode = existing.MovementMode;
            }
            else if (placed != null && PrefabUtility.GetCorrespondingObjectFromSource(placed.gameObject) is GameObject source &&
                     source.GetComponent<ACCharacterController>() != null)
            {
                characterPrefab = source;
                if (placed.TryGetComponent(out CharacterModelHost host)) { modelSource = host.ModelSource; }
                if (placed.TryGetComponent(out MovementController movement)) { movementMode = movement.movementHandler; }
            }
            if (characterPrefab == null) { characterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SceneGameSetup.CharacterPrefabPath); }
            if (modelSource == PrefabModelSource()) { modelSource = null; }
        }

        private void OnGUI()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene != choicesScene) { StartChoices(scene); } // Another scene was opened.
            string sceneProblem = SceneGameSetup.Problem(scene);

            EditorGUILayout.LabelField($"Set up '{SceneGameSetup.SceneName(scene)}' to spawn a character", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Adds a Game Setup object to the open scene, with a Character Spawner that spawns the " +
                                       "character below when the scene starts, and a camera to follow it.",
                                       EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space();

            scroll = EditorGUILayout.BeginScrollView(scroll);
            DrawCharacter();
            EditorGUILayout.Space();
            DrawModels();
            EditorGUILayout.Space();
            DrawMovementMode();
            EditorGUILayout.Space();
            if (sceneProblem == null) { DrawScene(scene); }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("Prepare Character Models", "Sets up the model files in " +
                                                    CharacterModelImporter.ModelsFolder + " (Tools > Character Showcase > Prepare Character Models).")))
                {
                    CharacterModelImporter.PrepareAllFromMenu();
                    Refresh();
                }
                if (GUILayout.Button(new GUIContent("Prepare Animations", "Sets up the animation files: the Quaternius libraries in " +
                                                    QuaterniusAnimationImporter.LibraryFolder + " and the motion-capture clips in " +
                                                    MotionCaptureClipImporter.ClipsFolder + ".")))
                {
                    PrepareAnimations();
                    Refresh();
                }
                if (GUILayout.Button("Refresh", GUILayout.Width(70f))) { Refresh(); }
            }

            string problem = sceneProblem ?? CharacterProblem();
            if (problem != null) { EditorGUILayout.HelpBox(problem, MessageType.Warning); }
            using (new EditorGUI.DisabledScope(problem != null))
            {
                if (GUILayout.Button($"Set Up '{SceneGameSetup.SceneName(scene)}'", GUILayout.Height(30f))) { Confirm(scene); }
            }
            EditorGUILayout.Space();
        }

        private void DrawCharacter()
        {
            EditorGUILayout.LabelField("Character", EditorStyles.boldLabel);
            using (var check = new EditorGUI.ChangeCheckScope())
            {
                var chosen = (GameObject)EditorGUILayout.ObjectField(new GUIContent("Character Prefab", "The character the scene spawns."),
                                                                     characterPrefab, typeof(GameObject), false);
                if (check.changed)
                {
                    // The movement mode follows the prefab, unless one other than the old prefab's was chosen.
                    bool modeWasPrefabs = movementMode == PrefabMovementMode();
                    characterPrefab = chosen;
                    if (modeWasPrefabs || movementMode == null) { movementMode = PrefabMovementMode(); }
                }
            }
        }

        private void DrawModels()
        {
            EditorGUILayout.LabelField("Model", EditorStyles.boldLabel);
            CharacterModelSource prefabModel = PrefabModelSource();
            var prefabDirect = prefabModel as DirectCharacterModelSource;
            DrawModelEntry(null, prefabModel != null ? $"The prefab's own ({prefabModel.name})" : "The prefab's own",
                           prefabDirect, "Keeps the model the prefab has.");
            foreach (DirectCharacterModelSource definition in readyModels)
            {
                if (definition == prefabModel) { continue; } // Listed as the prefab's own.
                DrawModelEntry(definition, definition.name, definition, AssetDatabase.GetAssetPath(definition));
            }
            if (readyModels.Count == 0)
            {
                EditorGUILayout.HelpBox($"No other models are ready. Put rigged models in subfolders of {CharacterModelImporter.ModelsFolder}, " +
                                        "then press Prepare Character Models.", MessageType.Info);
            }
            if (unavailableModels.Count > 0)
            {
                EditorGUILayout.HelpBox("Not available (press Prepare Character Models):\n" + string.Join("\n", unavailableModels), MessageType.Warning);
            }
        }

        private void DrawModelEntry(CharacterModelSource source, string label, DirectCharacterModelSource shown, string detail)
        {
            bool selected = source == modelSource;
            GameObject model = shown != null ? shown.ModelPrefab : null;
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                Rect previewRect = GUILayoutUtility.GetRect(PreviewSize, PreviewSize, GUILayout.Width(PreviewSize));
                if (model != null)
                {
                    Texture preview = AssetPreview.GetAssetPreview(model);
                    if (preview == null && AssetPreview.IsLoadingAssetPreview(model.GetEntityId())) { Repaint(); }
                    if (preview != null) { GUI.DrawTexture(previewRect, preview, ScaleMode.ScaleToFit); }
                }
                using (new EditorGUILayout.VerticalScope())
                {
                    EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
                    float height = Height(shown);
                    if (height > 0f) { EditorGUILayout.LabelField($"{height:0.00} m tall", EditorStyles.miniLabel); }
                    EditorGUILayout.LabelField(detail, EditorStyles.miniLabel);
                }
                bool choose = GUILayout.Toggle(selected, selected ? "Chosen" : "Choose", EditorStyles.miniButton,
                                               GUILayout.Width(64f), GUILayout.Height(PreviewSize - 8f));
                if (choose && !selected) { modelSource = source; }
            }
        }

        /// <summary>
        /// How tall a ready model stands at the character's authored size, sized as its source sizes it (Scale or Fit
        /// To Height), or 0 if it isn't ready.
        /// </summary>
        private float Height(DirectCharacterModelSource definition)
        {
            if (definition == null || CharacterModelImporter.Validate(definition) != null) { return 0f; }
            if (!heights.TryGetValue(definition.ModelPrefab, out float height))
            {
                height = heights[definition.ModelPrefab] = CharacterModelSource.MeasureHeight(definition.ModelPrefab, definition.ModelPrefab.transform);
            }
            return definition.StandingHeightFor(height);
        }

        private void DrawMovementMode()
        {
            EditorGUILayout.LabelField("Movement", EditorStyles.boldLabel);
            movementMode = (MovementMode)EditorGUILayout.ObjectField(
                new GUIContent("Movement Mode", "The movement mode asset the character moves with: its speeds, jumps and dashes."),
                movementMode, typeof(MovementMode), false);
            MovementMode prefabMode = PrefabMovementMode();
            if (movementMode != null && movementMode == prefabMode)
            {
                EditorGUILayout.LabelField("The prefab's own.", EditorStyles.miniLabel);
            }
            else if (movementMode != null && prefabMode != null && GUILayout.Button("Use the Prefab's Own", GUILayout.Width(150f)))
            {
                movementMode = prefabMode;
            }
        }

        private void DrawScene(Scene scene)
        {
            EditorGUILayout.LabelField("Scene", EditorStyles.boldLabel);

            List<ACCharacterController> placed = SceneGameSetup.PlacedCharacters(scene);
            if (placed.Count > 0)
            {
                replacePlacedCharacters = EditorGUILayout.ToggleLeft(
                    new GUIContent($"Replace the placed {(placed.Count == 1 ? "character" : "characters")} ({string.Join(", ", placed.Select(c => c.name))})",
                                   "Removes them, and spawns the character where they stood instead. Left off, the spawned " +
                                   "character is added alongside them."),
                    replacePlacedCharacters);
            }

            List<SpawnPoint> found = SceneGameSetup.FindSpawnPoints(scene, replacePlacedCharacters, out string source);
            if (found.Count > 0)
            {
                EditorGUILayout.HelpBox($"The character spawns at one of {found.Count} spawn point{(found.Count == 1 ? "" : "s")}, " +
                                        $"chosen at random: {source}.", MessageType.Info);
            }
            else
            {
                Vector3 point = DefaultSpawnPoint(scene).position;
                EditorGUILayout.HelpBox($"The character spawns at {point:F1}: the ground below the middle of the Scene view. Move it, " +
                                        "or add more spawn points, on the Character Spawner afterwards.", MessageType.Info);
            }

            var rig = SceneGameSetup.FindOwnCameraRig(scene);
            EditorGUILayout.LabelField(rig != null
                    ? $"The scene's own camera rig ('{rig.name}') follows the character."
                    : "A copy of the demo scene's camera follows the character.",
                EditorStyles.wordWrappedMiniLabel);
            List<Camera> cameras = SceneGameSetup.OtherCameras(scene);
            if (cameras.Count > 0)
            {
                turnOffOtherCameras = EditorGUILayout.ToggleLeft(
                    new GUIContent($"Turn off the scene's other cameras ({string.Join(", ", cameras.Select(c => c.name))})",
                                   "Their objects are deactivated, not deleted. Left on, they compete with the setup's camera."),
                    turnOffOtherCameras);
            }
            EditorGUILayout.LabelField(SceneGameSetup.InputManagersOutsideSetup(scene).Count > 0
                    ? "The scene's own input manager is kept."
                    : "A copy of the demo scene's input manager reads the player's input.",
                EditorStyles.wordWrappedMiniLabel);
        }

        private string CharacterProblem()
        {
            if (characterPrefab == null) { return "Choose a character prefab."; }
            if (characterPrefab.GetComponent<ACCharacterController>() == null)
            {
                return $"'{characterPrefab.name}' isn't a character: it has no {nameof(ACCharacterController)}.";
            }
            if (movementMode == null) { return "Choose a movement mode asset."; }
            return null;
        }

        private void Confirm(Scene scene)
        {
            var settings = new SceneGameSetup.Settings
            {
                characterPrefab = characterPrefab,
                // Only what differs from the prefab is stored as an override, so later changes to the prefab carry through.
                modelSource = modelSource != null && modelSource != PrefabModelSource() ? modelSource : null,
                movementMode = movementMode != PrefabMovementMode() ? movementMode : null,
                defaultSpawnPoint = DefaultSpawnPoint(scene),
                replacePlacedCharacters = replacePlacedCharacters,
                turnOffOtherCameras = turnOffOtherCameras,
            };
            if (SceneGameSetup.SetUp(scene, settings, withUndo: true))
            {
                CharacterSpawner spawner = SceneGameSetup.FindSpawner(scene);
                if (spawner != null) { Selection.activeObject = spawner.gameObject; }
                ShowNotification(new GUIContent("Scene set up. Save it to keep it."));
            }
        }

        /// <summary>
        /// A spawn point on the ground below the middle of the Scene view, facing the way the view looks, or at the
        /// world's origin without a Scene view. Just above the ground, so the character settles onto it.
        /// </summary>
        private static SpawnPoint DefaultSpawnPoint(Scene scene)
        {
            SceneView view = SceneView.lastActiveSceneView;
            if (view == null) { return new SpawnPoint(Vector3.zero, Quaternion.identity); }
            Vector3 position = SceneGameSetup.GroundBelow(view.pivot, scene) + Vector3.up * 0.05f;
            Vector3 forward = Vector3.ProjectOnPlane(view.rotation * Vector3.forward, Vector3.up);
            Quaternion facing = forward.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(forward, Vector3.up) : Quaternion.identity;
            return new SpawnPoint(position, facing);
        }

        private static void PrepareAnimations()
        {
            List<string> problems = QuaterniusAnimationImporter.PrepareAll();
            problems.AddRange(MotionCaptureClipImporter.PrepareAll());
            EditorUtility.DisplayDialog("Prepare Animations", problems.Count == 0
                ? "The animation files are set up. To use changed clips in the mannequin's Animator Controller, run " +
                  "Tools > Character Showcase > Generate Mannequin."
                : "Some animation files couldn't be set up:\n\n" + string.Join("\n", problems), "OK");
        }

        private MovementMode PrefabMovementMode() =>
            characterPrefab != null && characterPrefab.TryGetComponent(out MovementController movement) ? movement.movementHandler : null;

        private CharacterModelSource PrefabModelSource() =>
            characterPrefab != null && characterPrefab.TryGetComponent(out CharacterModelHost host) ? host.ModelSource : null;
    }
}
