using System.Collections.Generic;
using System.Linq;
using MeridianMotion.Cameras;
using MeridianMotion.Characters;
using MeridianMotion.Levels.EditorTools;
using MeridianMotion.Levels;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEditor;
using UnityEngine.SceneManagement;
using UnityEngine;

namespace MeridianMotion.EditorTools
{
    /// <summary>
    /// Makes a scene spawn its character rather than have it placed in it: adds a "Meridian Motion Setup" object with a
    /// CharacterSpawner that spawns a character prefab on Start, at one of its spawn points chosen at random, optionally
    /// with another model and movement mode, and hands it to a camera to follow (with its eye point and size). The
    /// camera and input manager are the scene's own if it has them (a MMotionCameraRig, an PlayerInputReader), or else instances
    /// of the Camera Rig and Input Manager prefabs, so they're set up and tuned the same (retuning a prefab retunes every
    /// scene set up with it). Characters placed in the scene can be replaced by spawn
    /// points where they stood. The level's fixtures (moving platforms, pushable crates, timing gates) are set up from
    /// the markers it builds (see LevelFixtureSetup). Used by the Set Up Scene window (the open scene) and from the
    /// command line.
    /// </summary>
    public static class CharacterSceneSetup
    {
        /// <summary>The camera rig the setup adds to scenes that haven't their own.</summary>
        public const string CameraRigPrefabPath = "Assets/Prefabs/Camera Rig.prefab";
        /// <summary>The input manager the setup adds to scenes that haven't their own.</summary>
        public const string InputManagerPrefabPath = "Assets/Prefabs/Input Manager.prefab";
        /// <summary>The character prefab the command line spawns unless it's given another (-prefab).</summary>
        public const string CharacterPrefabPath = "Assets/Prefabs/Character.prefab";
        /// <summary>The name of the object the setup makes (and finds again) at the scene's root.</summary>
        public const string SetupName = "Meridian Motion Setup";
        // What the setup object was called before (scenes set up then still have it): found, and replaced, as it is.
        private static readonly string[] EarlierSetupNames = { "Game Setup" };

        /// <summary>Whether a scene's root object is its setup object (by its name, or a name it had before).</summary>
        public static bool IsSetup(GameObject root) => root.name == SetupName || System.Array.IndexOf(EarlierSetupNames, root.name) >= 0;
        /// <summary>
        /// Objects whose names start with this are spawn markers: places a level marks for a character to start, facing
        /// their forward. The level builders make them, by the level elements' name for them (LevelFixtureSetup).
        /// </summary>
        public const string SpawnMarkerPrefix = LevelFixtureSetup.SpawnMarkerPrefix;
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
            scene.IsValid() && scene.isLoaded ? scene.GetRootGameObjects().FirstOrDefault(IsSetup) : null;

        /// <summary>The spawner in the scene's game setup, or null.</summary>
        public static CharacterSpawner FindSpawner(Scene scene)
        {
            GameObject setup = FindSetup(scene);
            return setup != null ? setup.GetComponentInChildren<CharacterSpawner>(true) : null;
        }

        /// <summary>The scene's own camera rig (outside its game setup), which the setup uses rather than adding its own, or null.</summary>
        public static MMotionCameraRig FindOwnCameraRig(Scene scene) =>
            OutsideSetup<MMotionCameraRig>(scene).FirstOrDefault(rig => rig.gameObject.activeInHierarchy);

        /// <summary>The scene's active cameras that the setup would turn off: all but its own camera rig's.</summary>
        public static List<Camera> OtherCameras(Scene scene)
        {
            MMotionCameraRig own = FindOwnCameraRig(scene);
            return OutsideSetup<Camera>(scene)
                .Where(camera => camera.gameObject.activeInHierarchy && (own == null || camera.gameObject != own.gameObject))
                .ToList();
        }

        /// <summary>The scene's input managers outside its game setup: the setup adds none while there's one already.</summary>
        public static List<PlayerInputReader> InputManagersOutsideSetup(Scene scene) => OutsideSetup<PlayerInputReader>(scene);

        /// <summary>Characters placed in the scene (outside its game setup).</summary>
        public static List<MMotionCharacter> PlacedCharacters(Scene scene) => OutsideSetup<MMotionCharacter>(scene);

        /// <summary>
        /// The scene's spawn markers, outside its game setup: objects named SpawnMarkerPrefix, or that followed by a
        /// space and more ("Spawn Point (Plaza North)"). Not other names that merely start with it, like the "Spawn
        /// Points" group they're kept in.
        /// </summary>
        public static List<Transform> SpawnMarkers(Scene scene) =>
            OutsideSetup<Transform>(scene).Where(t => IsSpawnMarkerName(t.name)).ToList();

        /// <summary>
        /// Whether name is a spawn marker's: SpawnMarkerPrefix alone, or followed by a space and more.
        /// </summary>
        public static bool IsSpawnMarkerName(string name) =>
            name == SpawnMarkerPrefix || name.StartsWith(SpawnMarkerPrefix + " ", System.StringComparison.Ordinal);

        private static NpcRoute CopyOf(NpcRoute route) => route == null ? null : new NpcRoute
        {
            spawnPoint = route.spawnPoint,
            waypoints = new List<Vector3>(route.waypoints),
            sprint = route.sprint,
            loop = route.loop,
        };

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
            List<MMotionCharacter> placed = replacePlacedCharacters ? PlacedCharacters(scene) : new List<MMotionCharacter>();
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
            if (problem == null && settings.characterPrefab.GetComponent<MMotionCharacter>() == null)
            {
                problem = $"'{settings.characterPrefab.name}' isn't a character (it has no {nameof(MMotionCharacter)}).";
            }
            if (problem != null)
            {
                Debug.LogError($"[{nameof(CharacterSceneSetup)}] {problem} Nothing was changed.");
                return false;
            }

            // Found before the old setup and the placed characters go.
            List<SpawnPoint> spawnPoints = settings.spawnPoints?.ToList()
                                           ?? FindSpawnPoints(scene, settings.replacePlacedCharacters, out _);
            if (spawnPoints.Count == 0) { spawnPoints.Add(settings.defaultSpawnPoint); }
            // The old spawner's NPC routes, kept as its spawn points are (copied: the old spawner goes).
            List<NpcRoute> npcRoutes = (FindSpawner(scene)?.Npcs ?? new List<NpcRoute>()).Select(CopyOf).ToList();
            List<GameObject> placed = settings.replacePlacedCharacters
                ? PlacedCharacters(scene).Select(c => c.gameObject).Where(CanRemove).ToList()
                : new List<GameObject>();

            // Added first, so a missing prefab changes nothing.
            var setup = new GameObject(SetupName).transform;
            SceneManager.MoveGameObjectToScene(setup.gameObject, scene);
            bool keepInputManager = InputManagersOutsideSetup(scene).Count > 0;
            MMotionCameraRig rig = FindOwnCameraRig(scene);
            bool keepCamera = rig != null;
            if (!keepCamera || !keepInputManager)
            {
                if (!AddFromPrefabs(setup, !keepInputManager, !keepCamera, out GameObject addedCamera))
                {
                    Object.DestroyImmediate(setup.gameObject);
                    return false;
                }
                if (!keepCamera) { rig = addedCamera.GetComponent<MMotionCameraRig>(); }
            }

            int undoGroup = Undo.GetCurrentGroup();
            if (withUndo) { Undo.SetCurrentGroupName("Set Up Scene"); }
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (IsSetup(root) && root != setup.gameObject) { Remove(root, withUndo); }
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
            spawner.Npcs.AddRange(npcRoutes);
            UnityEventTools.AddPersistentListener(spawner.OnCameraTargetSpawned, rig.Follow);
            // The level's fixtures (moving platforms, ropes, water, timing gates…), from the markers the level builds: a
            // built level has them already (each build sets them up), so this finds them set up and leaves them, and does
            // it for a scene built some other way. Then the courses' timers are told who's racing: the spawned character.
            string fixtures = LevelFixtureSetup.SetUp(scene, withUndo);
            foreach (CourseTimer timer in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<CourseTimer>(true)))
            {
                UnityEventTools.AddPersistentListener(spawner.OnCharacterSpawned, timer.SetRacer);
            }
            // With water in the level, the camera's view goes underwater with it (see UnderwaterView).
            bool underwaterView = AddUnderwaterView(scene, rig, withUndo);
            if (withUndo)
            {
                Undo.RegisterCreatedObjectUndo(setup.gameObject, "Set Up Scene");
                Undo.CollapseUndoOperations(undoGroup);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[{nameof(CharacterSceneSetup)}] Set up '{SceneName(scene)}' to spawn '{settings.characterPrefab.name}'" +
                      (settings.modelSource != null ? $" with the model '{settings.modelSource.name}'" : "") +
                      (settings.movementMode != null ? $" and the movement mode '{settings.movementMode.name}'" : "") +
                      $", at {Count(spawnPoints.Count, "spawn point")}, followed by '{rig.name}'." +
                      (placed.Count > 0 ? $" Replaced the placed {(placed.Count == 1 ? "character" : "characters")}: {placedNames}." : "") +
                      (keepInputManager ? " The scene's own input manager is kept." : "") +
                      (otherCameras.Count > 0 ? $" Turned off the scene's other cameras: {string.Join(", ", otherCameras.Select(c => c.name))}." : "") +
                      (fixtures.Length > 0 ? $" Set up the level's {fixtures}." : "") +
                      (underwaterView ? " The camera has an underwater view." : ""));
            return true;
        }

        /// <summary>
        /// Gives the camera the rig drives an UnderwaterView, if the scene has any water (outside the game setup) and the
        /// camera hasn't one already. Returns whether the camera has one.
        /// </summary>
        public static bool AddUnderwaterView(Scene scene, MMotionCameraRig rig, bool withUndo)
        {
            Camera camera = rig != null ? rig.GetComponentInChildren<Camera>(true) : null;
            if (camera == null || !OutsideSetup<SwimmableWater>(scene).Any()) { return false; }
            if (camera.GetComponent<UnderwaterView>() != null) { return true; }
            if (withUndo) { Undo.AddComponent<UnderwaterView>(camera.gameObject); } else { camera.gameObject.AddComponent<UnderwaterView>(); }
            return true;
        }

        /// <summary>
        /// For -executeMethod: sets up and saves the scene given by -scene [path], spawning -prefab [path] (the
        /// Character prefab without it), optionally with -model [a prepared model's name or source path] and
        /// -movementMode [asset path]. Exits when it's done, with code 1 on failure.
        /// </summary>
        public static void SetUpFromCommandLine()
        {
            string scenePath = CommandLine.Argument("-scene");
            string prefabPath = CommandLine.Argument("-prefab") ?? CharacterPrefabPath;
            string modelName = CommandLine.Argument("-model");
            string modePath = CommandLine.Argument("-movementMode");

            if (scenePath == null || AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
            {
                Debug.LogError($"[{nameof(CharacterSceneSetup)}] " + (scenePath == null ? "Give the scene to set up with -scene [path]." : $"There's no scene at '{scenePath}'."));
                CommandLine.Finish(false);
                return;
            }
            // Opened before loading the assets: opening a scene unloads assets nothing loaded uses, which would leave
            // ones loaded earlier reading as null here (though still saved correctly).
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            var settings = new Settings { characterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) };
            string problem = settings.characterPrefab == null ? $"There's no character prefab at '{prefabPath}'." : null;
            if (problem == null && modelName != null)
            {
                settings.modelSource = HumanoidModelImporter.FindPreparedSources()
                    .FirstOrDefault(d => AssetDatabase.GetAssetPath(d) == modelName || d.name == modelName);
                if (settings.modelSource == null)
                {
                    problem = $"No ready model called '{modelName}'. Ready models: " +
                              string.Join(", ", HumanoidModelImporter.FindPreparedSources().Select(d => d.name));
                }
            }
            if (problem == null && modePath != null)
            {
                settings.movementMode = AssetDatabase.LoadAssetAtPath<MovementMode>(modePath);
                if (settings.movementMode == null) { problem = $"There's no movement mode asset at '{modePath}'."; }
            }
            if (problem != null)
            {
                Debug.LogError($"[{nameof(CharacterSceneSetup)}] {problem}");
                CommandLine.Finish(false);
                return;
            }

            bool ok = SetUp(scene, settings, withUndo: false) && EditorSceneManager.SaveScene(scene);
            CommandLine.Finish(ok);
        }


        private static string Count(int count, string noun) => $"{count} {noun}{(count == 1 ? "" : "s")}";

        /// <summary>
        /// Whether a placed character can be removed: not if it's part of another prefab instance, which only that
        /// prefab can change.
        /// </summary>
        private static bool CanRemove(GameObject character)
        {
            if (!PrefabUtility.IsPartOfPrefabInstance(character) || PrefabUtility.IsOutermostPrefabInstanceRoot(character)) { return true; }
            Debug.LogWarning($"[{nameof(CharacterSceneSetup)}] '{character.name}' is part of the prefab instance " +
                             $"'{PrefabUtility.GetOutermostPrefabInstanceRoot(character).name}', so it's left in place.", character);
            return false;
        }

        /// <summary>
        /// Clears the camera's references to characters about to be removed (its target, eye anchor and size
        /// reference), so it isn't left with missing ones; the spawned character fills them in when it spawns.
        /// </summary>
        private static void ForgetPlacedCharacters(MMotionCameraRig rig, List<GameObject> placed, bool withUndo)
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

        /// <summary>The scene's name for showing, or "Untitled" if it hasn't been saved.</summary>
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
                if (!IsSetup(root)) { found.AddRange(root.GetComponentsInChildren<T>(true)); }
            }
            return found;
        }

        /// <summary>
        /// Adds the Input Manager and Camera Rig prefabs, as asked, to the setup, as prefab instances (so they follow the
        /// prefabs). Returns false, adding nothing, if a prefab it needs is missing (logged as an error).
        /// </summary>
        private static bool AddFromPrefabs(Transform setup, bool addInputManager, bool addCamera, out GameObject camera)
        {
            camera = null;
            GameObject inputManagerPrefab = addInputManager ? LoadPrefab(InputManagerPrefabPath) : null;
            GameObject cameraPrefab = addCamera ? LoadPrefab(CameraRigPrefabPath) : null;
            if ((addInputManager && inputManagerPrefab == null) || (addCamera && cameraPrefab == null)) { return false; }
            if (addCamera && cameraPrefab.GetComponent<MMotionCameraRig>() == null)
            {
                Debug.LogError($"[{nameof(CharacterSceneSetup)}] '{CameraRigPrefabPath}' has no {nameof(MMotionCameraRig)}. Nothing was changed.");
                return false;
            }
            if (addInputManager) { PrefabUtility.InstantiatePrefab(inputManagerPrefab, setup); }
            if (addCamera) { camera = (GameObject)PrefabUtility.InstantiatePrefab(cameraPrefab, setup); }
            return true;
        }

        private static GameObject LoadPrefab(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) { Debug.LogError($"[{nameof(CharacterSceneSetup)}] There's no prefab at '{path}'. Nothing was changed."); }
            return prefab;
        }
    }
}
