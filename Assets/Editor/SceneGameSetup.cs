using System.Collections.Generic;
using System.Linq;
using CameraSystem;
using Character;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CharacterShowcase.EditorTools
{
    /// <summary>
    /// Makes a scene spawn its character rather than have it placed in it: adds a "Game Setup" object with a
    /// CharacterSpawner that spawns a character prefab on Start, at one of its spawn points chosen at random, optionally
    /// with another model and movement mode, and hands it to a camera to follow (with its eye point and size). The
    /// camera and input manager are the scene's own if it has them (a CameraRig, an ACInputManager), or else copies of
    /// the demo scene's, so they're set up and tuned the same. Characters placed in the scene can be replaced by spawn
    /// points where they stood. The level's fixtures (moving platforms, pushable crates, timing gates) are set up from
    /// the markers it builds (see LevelFixtureSetup). Used by the Scene Setup window (the open scene) and from the
    /// command line.
    /// </summary>
    public static class SceneGameSetup
    {
        public const string DemoScenePath = "Assets/Scenes/SampleScene.unity";
        public const string CharacterPrefabPath = "Assets/Prefabs/Character.prefab";
        public const string SetupName = "Game Setup";
        /// <summary>
        /// Objects whose names start with this are spawn markers: places a level marks for a character to start, facing
        /// their forward. The level builders make them (Graybox.SpawnMarkerPrefix, which this must match).
        /// </summary>
        public const string SpawnMarkerPrefix = "Spawn Point";
        // The objects copied from the demo scene, by name.
        private const string InputManagerName = "InputManager";
        private const string CameraName = "Main Camera";

        /// <summary>What to set a scene up with.</summary>
        public sealed class Settings
        {
            /// <summary>The character prefab to spawn. Required.</summary>
            public GameObject characterPrefab;
            /// <summary>The model the spawned character uses instead of the prefab's own; null keeps the prefab's.</summary>
            public CharacterModelSource modelSource;
            /// <summary>The movement mode the spawned character uses instead of the prefab's own; null keeps the prefab's.</summary>
            public MovementMode movementMode;
            /// <summary>The spawn points. Null finds them (see FindSpawnPoints), using defaultSpawnPoint if there are none.</summary>
            public IReadOnlyList<SpawnPoint> spawnPoints;
            /// <summary>Where the spawn point goes when none are given or found.</summary>
            public SpawnPoint defaultSpawnPoint = new(Vector3.zero, Quaternion.identity);
            /// <summary>Remove the characters placed in the scene, spawning the character where they stood instead.</summary>
            public bool replacePlacedCharacters = true;
            /// <summary>
            /// Turn off the scene's other cameras (all but the one following the spawned character), which would
            /// compete with it for Camera.main. Their objects are deactivated rather than deleted, since they may hold
            /// more than a camera.
            /// </summary>
            public bool turnOffOtherCameras = true;
        }

        /// <summary>The scene's game setup object, or null if it doesn't have one.</summary>
        public static GameObject FindSetup(Scene scene) =>
            scene.IsValid() && scene.isLoaded ? scene.GetRootGameObjects().FirstOrDefault(root => root.name == SetupName) : null;

        /// <summary>The spawner in the scene's game setup, or null.</summary>
        public static CharacterSpawner FindSpawner(Scene scene)
        {
            GameObject setup = FindSetup(scene);
            return setup != null ? setup.GetComponentInChildren<CharacterSpawner>(true) : null;
        }

        /// <summary>The scene's own camera rig (outside its game setup), which the setup uses rather than copying the demo's, or null.</summary>
        public static CameraRig FindOwnCameraRig(Scene scene) =>
            OutsideSetup<CameraRig>(scene).FirstOrDefault(rig => rig.gameObject.activeInHierarchy);

        /// <summary>The scene's active cameras that the setup would turn off: all but its own camera rig's.</summary>
        public static List<Camera> OtherCameras(Scene scene)
        {
            CameraRig own = FindOwnCameraRig(scene);
            return OutsideSetup<Camera>(scene)
                .Where(camera => camera.gameObject.activeInHierarchy && (own == null || camera.gameObject != own.gameObject))
                .ToList();
        }

        /// <summary>The scene's input managers outside its game setup: the setup adds none while there's one already.</summary>
        public static List<ACInputManager> InputManagersOutsideSetup(Scene scene) => OutsideSetup<ACInputManager>(scene);

        /// <summary>Characters placed in the scene (outside its game setup).</summary>
        public static List<ACCharacterController> PlacedCharacters(Scene scene) => OutsideSetup<ACCharacterController>(scene);

        /// <summary>
        /// The scene's spawn markers, outside its game setup: objects named SpawnMarkerPrefix, or that followed by a
        /// space and more ("Spawn Point (Plaza North)"). Not other names that merely start with it, like the "Spawn
        /// Points" group they're kept in.
        /// </summary>
        public static List<Transform> SpawnMarkers(Scene scene) =>
            OutsideSetup<Transform>(scene).Where(t => IsSpawnMarkerName(t.name)).ToList();

        public static bool IsSpawnMarkerName(string name) =>
            name == SpawnMarkerPrefix || name.StartsWith(SpawnMarkerPrefix + " ", System.StringComparison.Ordinal);

        /// <summary>
        /// The spawn points a setup finds for itself, and where they came from (for showing): the scene's existing
        /// setup's, if it has some; otherwise its spawn markers, and where its placed characters stand if they're being
        /// replaced. Empty if there are none of these.
        /// </summary>
        public static List<SpawnPoint> FindSpawnPoints(Scene scene, bool replacePlacedCharacters, out string source)
        {
            CharacterSpawner existing = FindSpawner(scene);
            if (existing != null && existing.SpawnPoints.Count > 0)
            {
                source = "kept from the scene's existing setup";
                return existing.SpawnPoints.ToList();
            }
            List<Transform> markers = SpawnMarkers(scene);
            List<ACCharacterController> placed = replacePlacedCharacters ? PlacedCharacters(scene) : new List<ACCharacterController>();
            var parts = new List<string>();
            if (markers.Count > 0) { parts.Add($"the scene's {Count(markers.Count, "spawn marker")}"); }
            if (placed.Count > 0) { parts.Add($"where its placed {(placed.Count == 1 ? "character stands" : "characters stand")}"); }
            source = string.Join(" and ", parts);
            return markers.Select(m => new SpawnPoint(m.position, m.rotation))
                .Concat(placed.Select(c => new SpawnPoint(c.transform.position, c.transform.rotation)))
                .ToList();
        }

        /// <summary>Why the scene can't be set up, or null if it can.</summary>
        public static string Problem(Scene scene) =>
            !scene.IsValid() || !scene.isLoaded ? "There's no open scene to set up." : null;

        /// <summary>
        /// Sets the scene up: replaces its game setup (if it has one) with a new one. With undo, the changes can be
        /// undone and the scene is left for you to save; without, they can't (for scenes set up by a script, which saves
        /// them). Returns false, changing nothing, if something it needs is missing (logged as an error).
        /// </summary>
        public static bool SetUp(Scene scene, Settings settings, bool withUndo)
        {
            string problem = Problem(scene);
            if (problem == null && (settings == null || settings.characterPrefab == null)) { problem = "No character prefab was chosen."; }
            if (problem == null && settings.characterPrefab.GetComponent<ACCharacterController>() == null)
            {
                problem = $"'{settings.characterPrefab.name}' isn't a character (it has no {nameof(ACCharacterController)}).";
            }
            if (problem != null)
            {
                Debug.LogError($"[{nameof(SceneGameSetup)}] {problem} Nothing was changed.");
                return false;
            }

            // Found before the old setup and the placed characters go.
            List<SpawnPoint> spawnPoints = settings.spawnPoints?.ToList()
                                           ?? FindSpawnPoints(scene, settings.replacePlacedCharacters, out _);
            if (spawnPoints.Count == 0) { spawnPoints.Add(settings.defaultSpawnPoint); }
            List<GameObject> placed = settings.replacePlacedCharacters
                ? PlacedCharacters(scene).Select(c => c.gameObject).Where(CanRemove).ToList()
                : new List<GameObject>();

            // Copied first, so a demo scene without what's needed changes nothing.
            var setup = new GameObject(SetupName).transform;
            SceneManager.MoveGameObjectToScene(setup.gameObject, scene);
            bool keepInputManager = InputManagersOutsideSetup(scene).Count > 0;
            CameraRig rig = FindOwnCameraRig(scene);
            bool keepCamera = rig != null;
            if (!keepCamera || !keepInputManager)
            {
                if (!CopyFromDemo(setup, !keepInputManager, !keepCamera, out GameObject copiedCamera))
                {
                    Object.DestroyImmediate(setup.gameObject);
                    return false;
                }
                if (!keepCamera) { rig = copiedCamera.GetComponent<CameraRig>(); }
            }

            int undoGroup = Undo.GetCurrentGroup();
            if (withUndo) { Undo.SetCurrentGroupName("Set Up Scene"); }
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == SetupName && root != setup.gameObject) { Remove(root, withUndo); }
            }
            // Named now: they're gone by the time the log below is written.
            string placedNames = string.Join(", ", placed.Select(c => c.name));
            if (keepCamera) { ForgetPlacedCharacters(rig, placed, withUndo); }
            foreach (GameObject character in placed) { Remove(character, withUndo); }
            List<Camera> otherCameras = settings.turnOffOtherCameras ? OtherCameras(scene) : new List<Camera>();
            foreach (Camera other in otherCameras)
            {
                if (withUndo) { Undo.RecordObject(other.gameObject, "Set Up Scene"); }
                other.gameObject.SetActive(false);
            }

            var spawnerObject = new GameObject("Character Spawner");
            spawnerObject.transform.SetParent(setup, false);
            var spawner = spawnerObject.AddComponent<CharacterSpawner>();
            spawner.CharacterPrefab = settings.characterPrefab;
            spawner.ModelSource = settings.modelSource;
            spawner.MovementMode = settings.movementMode;
            spawner.SpawnOnStart = true;
            spawner.SpawnPoints.Clear();
            spawner.SpawnPoints.AddRange(spawnPoints);
            UnityEventTools.AddPersistentListener(spawner.OnCameraTargetSpawned, rig.Follow);
            // The level's fixtures: moving platforms, pushable crates and timing gates, from the markers the level builds.
            string fixtures = LevelFixtureSetup.SetUp(scene, setup, spawner, withUndo);
            if (withUndo)
            {
                Undo.RegisterCreatedObjectUndo(setup.gameObject, "Set Up Scene");
                Undo.CollapseUndoOperations(undoGroup);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[{nameof(SceneGameSetup)}] Set up '{SceneName(scene)}' to spawn '{settings.characterPrefab.name}'" +
                      (settings.modelSource != null ? $" with the model '{settings.modelSource.name}'" : "") +
                      (settings.movementMode != null ? $" and the movement mode '{settings.movementMode.name}'" : "") +
                      $", at {Count(spawnPoints.Count, "spawn point")}, followed by '{rig.name}'." +
                      (placed.Count > 0 ? $" Replaced the placed {(placed.Count == 1 ? "character" : "characters")}: {placedNames}." : "") +
                      (keepInputManager ? " The scene's own input manager is kept." : "") +
                      (otherCameras.Count > 0 ? $" Turned off the scene's other cameras: {string.Join(", ", otherCameras.Select(c => c.name))}." : "") +
                      (fixtures.Length > 0 ? $" Set up the level's {fixtures}." : ""));
            return true;
        }

        /// <summary>
        /// For -executeMethod: sets up and saves the scene given by -scene [path], spawning -prefab [path] (the
        /// Character prefab without it), optionally with -model [a prepared model's name or source path] and
        /// -movementMode [asset path]. Exits with code 1 on failure.
        /// </summary>
        public static void SetUpFromCommandLine()
        {
            string scenePath = Argument("-scene");
            string prefabPath = Argument("-prefab") ?? CharacterPrefabPath;
            string modelName = Argument("-model");
            string modePath = Argument("-movementMode");

            if (scenePath == null || AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
            {
                Debug.LogError($"[{nameof(SceneGameSetup)}] " + (scenePath == null ? "Give the scene to set up with -scene [path]." : $"There's no scene at '{scenePath}'."));
                EditorApplication.Exit(1);
                return;
            }
            // Opened before loading the assets: opening a scene unloads assets nothing loaded uses, which would leave
            // ones loaded earlier reading as null here (though still saved correctly).
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            var settings = new Settings { characterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) };
            string problem = settings.characterPrefab == null ? $"There's no character prefab at '{prefabPath}'." : null;
            if (problem == null && modelName != null)
            {
                settings.modelSource = CharacterModelImporter.FindReadyDefinitions()
                    .FirstOrDefault(d => AssetDatabase.GetAssetPath(d) == modelName || d.name == modelName);
                if (settings.modelSource == null)
                {
                    problem = $"No ready model called '{modelName}'. Ready models: " +
                              string.Join(", ", CharacterModelImporter.FindReadyDefinitions().Select(d => d.name));
                }
            }
            if (problem == null && modePath != null)
            {
                settings.movementMode = AssetDatabase.LoadAssetAtPath<MovementMode>(modePath);
                if (settings.movementMode == null) { problem = $"There's no movement mode asset at '{modePath}'."; }
            }
            if (problem != null)
            {
                Debug.LogError($"[{nameof(SceneGameSetup)}] {problem}");
                EditorApplication.Exit(1);
                return;
            }

            if (!SetUp(scene, settings, withUndo: false) || !EditorSceneManager.SaveScene(scene)) { EditorApplication.Exit(1); }
        }

        private static string Argument(string name)
        {
            string[] args = System.Environment.GetCommandLineArgs();
            int index = System.Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }

        private static string Count(int count, string noun) => $"{count} {noun}{(count == 1 ? "" : "s")}";

        /// <summary>
        /// Whether a placed character can be removed: not if it's part of another prefab instance, which only that
        /// prefab can change.
        /// </summary>
        private static bool CanRemove(GameObject character)
        {
            if (!PrefabUtility.IsPartOfPrefabInstance(character) || PrefabUtility.IsOutermostPrefabInstanceRoot(character)) { return true; }
            Debug.LogWarning($"[{nameof(SceneGameSetup)}] '{character.name}' is part of the prefab instance " +
                             $"'{PrefabUtility.GetOutermostPrefabInstanceRoot(character).name}', so it's left in place.", character);
            return false;
        }

        /// <summary>
        /// Clears the camera's references to characters about to be removed (its target, eye anchor and size
        /// reference), so it isn't left with missing ones; the spawned character fills them in when it spawns.
        /// </summary>
        private static void ForgetPlacedCharacters(CameraRig rig, List<GameObject> placed, bool withUndo)
        {
            if (placed.Count == 0) { return; }
            var serialized = new SerializedObject(rig);
            foreach (string path in new[] { "target", "targetSizeReference", "firstPerson.eyeAnchor" })
            {
                SerializedProperty property = serialized.FindProperty(path);
                if (property?.objectReferenceValue is Transform transform && placed.Any(c => transform.IsChildOf(c.transform)))
                {
                    property.objectReferenceValue = null;
                }
            }
            if (withUndo) { serialized.ApplyModifiedProperties(); } else { serialized.ApplyModifiedPropertiesWithoutUndo(); }
        }

        public static string SceneName(Scene scene) => string.IsNullOrEmpty(scene.name) ? "Untitled" : scene.name;

        /// <summary>
        /// The ground below a point, for a spawn point: the first surface a ray finds straight down from a little above
        /// it, or the point itself if there's nothing below.
        /// </summary>
        public static Vector3 GroundBelow(Vector3 point, Scene scene)
        {
            const float above = 2f;
            PhysicsScene physics = scene.IsValid() ? scene.GetPhysicsScene() : Physics.defaultPhysicsScene;
            return physics.Raycast(point + Vector3.up * above, Vector3.down, out RaycastHit hit, 1000f, ~0, QueryTriggerInteraction.Ignore)
                ? hit.point
                : point;
        }

        private static void Remove(GameObject gameObject, bool withUndo)
        {
            if (withUndo) { Undo.DestroyObjectImmediate(gameObject); } else { Object.DestroyImmediate(gameObject); }
        }

        private static List<T> OutsideSetup<T>(Scene scene) where T : Component
        {
            var found = new List<T>();
            if (!scene.IsValid() || !scene.isLoaded) { return found; }
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name != SetupName) { found.AddRange(root.GetComponentsInChildren<T>(true)); }
            }
            return found;
        }

        /// <summary>
        /// Copies the demo scene's input manager and camera, as asked, into the setup. The demo scene is opened
        /// alongside and closed again unless it was open already, and is never saved.
        /// </summary>
        private static bool CopyFromDemo(Transform setup, bool copyInputManager, bool copyCamera, out GameObject camera)
        {
            camera = null;
            Scene demo = SceneManager.GetSceneByPath(DemoScenePath);
            bool wasOpen = demo.isLoaded;
            if (!wasOpen)
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(DemoScenePath) == null)
                {
                    Debug.LogError($"[{nameof(SceneGameSetup)}] The demo scene '{DemoScenePath}' wasn't found. Nothing was changed.");
                    return false;
                }
                demo = EditorSceneManager.OpenScene(DemoScenePath, OpenSceneMode.Additive);
            }
            try
            {
                if (copyInputManager && CopyFrom(demo, InputManagerName, setup) == null) { return false; }
                if (!copyCamera) { return true; }
                camera = CopyFrom(demo, CameraName, setup);
                if (camera != null && camera.GetComponent<CameraRig>() == null)
                {
                    Debug.LogError($"[{nameof(SceneGameSetup)}] The demo scene's '{CameraName}' has no {nameof(CameraRig)}. Nothing was changed.");
                    camera = null;
                }
                return camera != null;
            }
            finally
            {
                if (!wasOpen) { EditorSceneManager.CloseScene(demo, true); }
            }
        }

        /// <summary>
        /// Copies a root object of the demo scene into the setup, keeping its name. References from it into the demo
        /// scene (the camera's target is the demo's character, say) can't cross into another scene, so they're
        /// cleared; references within the copy, and to assets, are kept.
        /// </summary>
        private static GameObject CopyFrom(Scene demo, string objectName, Transform setup)
        {
            GameObject original = demo.GetRootGameObjects().FirstOrDefault(root => root.name == objectName);
            if (original == null)
            {
                Debug.LogError($"[{nameof(SceneGameSetup)}] Couldn't find '{objectName}' in '{DemoScenePath}' to copy. Nothing was changed.");
                return null;
            }

            GameObject copy = Object.Instantiate(original, setup);
            copy.name = original.name;
            foreach (Component component in copy.GetComponentsInChildren<Component>(true))
            {
                if (component == null) { continue; } // A missing script.
                var serialized = new SerializedObject(component);
                SerializedProperty property = serialized.GetIterator();
                bool changed = false;
                while (property.Next(true))
                {
                    if (property.propertyType != SerializedPropertyType.ObjectReference || property.objectReferenceValue == null) { continue; }
                    Scene referenced = property.objectReferenceValue switch
                    {
                        GameObject gameObject => gameObject.scene,
                        Component other => other.gameObject.scene,
                        _ => default,
                    };
                    // A demo scene being set up itself keeps them: references within one scene are fine.
                    if (referenced == demo && referenced != setup.gameObject.scene)
                    {
                        property.objectReferenceValue = null;
                        changed = true;
                    }
                }
                if (changed) { serialized.ApplyModifiedPropertiesWithoutUndo(); }
            }
            return copy;
        }
    }
}
