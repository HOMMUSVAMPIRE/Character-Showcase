using System.Linq;
using MeridianMotion.Cameras;
using MeridianMotion.Characters;
using MeridianMotion.EditorTools;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace MeridianMotion.UI.EditorTools
{
    /// <summary>
    /// Adds the UI to every scene Set Up Scene sets up (registered with CharacterSceneSetup.AfterSettingUp, so the core
    /// never names the UI, and a package cut without it sets scenes up without it): the scene's own UI Manager if it has
    /// one, else an instance of the UI Manager prefab in the setup. Then it connects the UI to the game, in the scene, not
    /// in code (the UI knows nothing of the game):
    /// - the UI Manager's On Cursor Needed Changed to the camera rig's SetCursorRequested, and its On Game Input Blocked
    ///   Changed to the player's input reader's SetSuppressed;
    /// - the Back key is the UI's (the pause menu opens on it, PauseMenuOnBack), so the camera's Unlock Key is set to None;
    /// - one EventSystem: the scene's own, if it has one, and the UI Manager's turned off.
    /// </summary>
    [InitializeOnLoad]
    public static class UISceneSetup
    {
        static UISceneSetup() => CharacterSceneSetup.AfterSettingUp += AddUI;

        /// <summary>Adds the UI to a scene Set Up Scene has just set up (see the class).</summary>
        public static void AddUI(CharacterSceneSetup.SetupContext context)
        {
            UIManager manager = OwnManager(context.scene, context.setup);
            if (manager == null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(UIAssets.ManagerPath);
                if (prefab == null || prefab.GetComponent<UIManager>() == null)
                {
                    context.notes.Add($"No UI was added: there's no UI Manager prefab ('{UIAssets.ManagerPath}': Tools > Meridian Motion > Utilities > Create UI Assets makes it).");
                    return;
                }
                manager = ((GameObject)PrefabUtility.InstantiatePrefab(prefab, context.setup)).GetComponent<UIManager>();
                context.notes.Add("Added the UI Manager.");
            }
            else
            {
                context.notes.Add($"The scene's own UI Manager ('{manager.name}') is kept.");
            }

            OneEventSystem(context, manager);
            if (context.withUndo) { Undo.RecordObject(manager, "Set Up Scene"); }
            if (context.camera != null)
            {
                Connect(manager.OnCursorNeededChanged, context.camera, context.camera.SetCursorRequested);
                HandOverEscape(context.camera, context.withUndo);
            }
            if (context.inputReader != null) { Connect(manager.OnGameInputBlockedChanged, context.inputReader, context.inputReader.SetSuppressed); }
            EditorUtility.SetDirty(manager);
            context.notes.Add("The UI frees the cursor and stops the player's input while its windows need it, and Back is the UI's.");
        }

        // The scene's own UI Manager (outside the setup), or null.
        private static UIManager OwnManager(Scene scene, Transform setup) =>
            scene.GetRootGameObjects().Where(root => root.transform != setup)
                .SelectMany(root => root.GetComponentsInChildren<UIManager>(true)).FirstOrDefault();

        // Adds target's method as a persistent listener of the event, unless it's one already (set up again).
        private static void Connect(UnityEvent<bool> uiEvent, Object target, UnityAction<bool> method)
        {
            for (int i = 0; i < uiEvent.GetPersistentEventCount(); i++)
            {
                if (uiEvent.GetPersistentTarget(i) == target && uiEvent.GetPersistentMethodName(i) == method.Method.Name) { return; }
            }
            UnityEventTools.AddPersistentListener(uiEvent, method);
        }

        // The Back key (Escape) opens the UI's pause menu, which frees the cursor itself: the camera's own unlock key goes.
        private static void HandOverEscape(MMotionCameraRig rig, bool withUndo)
        {
            var fields = new SerializedObject(rig);
            SerializedProperty unlockKey = fields.FindProperty("cursor.unlockKey");
            if (unlockKey == null) { return; }
            if (withUndo) { Undo.RecordObject(rig, "Set Up Scene"); }
            unlockKey.intValue = (int)Key.None;
            fields.ApplyModifiedProperties();
        }

        // One EventSystem in the scene: with one of the scene's own, the UI Manager's is turned off.
        private static void OneEventSystem(CharacterSceneSetup.SetupContext context, UIManager manager)
        {
            EventSystem own = context.scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<EventSystem>(true))
                .FirstOrDefault(system => !system.transform.IsChildOf(manager.transform) && system.gameObject.activeInHierarchy);
            if (own == null) { return; }
            foreach (EventSystem managers in manager.GetComponentsInChildren<EventSystem>(true))
            {
                if (context.withUndo) { Undo.RecordObject(managers.gameObject, "Set Up Scene"); }
                managers.gameObject.SetActive(false);
            }
            context.notes.Add($"The scene's own EventSystem ('{own.name}') is kept.");
        }

        // ---- Replays ----

        /// <summary>
        /// Adds the Replay Recorder prefab (a scene's replays, and the pause menu's Replays tab) to the scene, under parent
        /// (the scene's root, if none), unless the scene has a ReplayRecorder. Returns the one it has, or null if there's no
        /// prefab (Create UI Assets makes it).
        /// </summary>
        public static ReplayRecorder AddReplayRecorder(Scene scene, Transform parent, bool withUndo)
        {
            ReplayRecorder own = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<ReplayRecorder>(true)).FirstOrDefault();
            if (own != null) { return own; }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(UIAssets.ReplayRecorderPath);
            if (prefab == null) { return null; }
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            if (parent != null) { instance.transform.SetParent(parent, false); }
            if (withUndo) { Undo.RegisterCreatedObjectUndo(instance, "Add Replay Recorder"); }
            EditorSceneManager.MarkSceneDirty(scene);
            return instance.GetComponent<ReplayRecorder>();
        }

        [MenuItem("Tools/Meridian Motion/Add Replay Recorder", priority = 22)]
        public static void AddReplayRecorderFromMenu()
        {
            Scene scene = SceneManager.GetActiveScene();
            ReplayRecorder recorder = AddReplayRecorder(scene, null, withUndo: true);
            if (recorder == null) { Debug.LogError($"[UISceneSetup] There's no Replay Recorder prefab at '{UIAssets.ReplayRecorderPath}': Tools > Meridian Motion > Utilities > Create UI Assets makes it."); }
            else
            {
                Selection.activeGameObject = recorder.gameObject;
                Debug.Log($"[UISceneSetup] '{scene.name}' has a Replay Recorder: it records each play session, and the pause menu has a Replays tab.", recorder);
            }
        }

        /// <summary>For -executeMethod: adds the Replay Recorder to each scene given by -scene [path] (several allowed), saves them, and exits (code 1 on failure).</summary>
        public static void AddReplayRecorderFromCommandLine()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            bool ok = true, any = false;
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] != "-scene") { continue; }
                any = true;
                Scene scene = EditorSceneManager.OpenScene(args[i + 1], OpenSceneMode.Single);
                ReplayRecorder recorder = AddReplayRecorder(scene, null, withUndo: false);
                ok &= recorder != null && EditorSceneManager.SaveScene(scene);
                Debug.Log($"[UISceneSetup] {(recorder != null ? "Added a Replay Recorder to" : "Couldn't add a Replay Recorder to")} '{args[i + 1]}'.");
            }
            if (!any) { Debug.LogError("[UISceneSetup] Give the scenes with -scene [path]."); }
            if (Application.isBatchMode) { EditorApplication.Exit(ok && any ? 0 : 1); }
        }
    }
}
