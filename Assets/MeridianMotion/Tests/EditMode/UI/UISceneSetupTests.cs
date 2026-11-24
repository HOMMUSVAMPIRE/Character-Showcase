using System.Linq;
using MeridianMotion.Cameras;
using MeridianMotion.Characters;
using MeridianMotion.EditorTools;
using MeridianMotion.UI;
using MeridianMotion.UI.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace MeridianMotion.Tests.EditMode.UI
{
    /// <summary>
    /// Checks of what Set Up Scene does for the UI (UISceneSetup, through CharacterSceneSetup.AfterSettingUp), each on a new
    /// scene: the UI Manager added to the setup, as an instance of its prefab; its On Cursor Needed Changed connected to the
    /// camera rig and its On Game Input Blocked Changed to the player's input reader, once however often it's set up; the
    /// camera's unlock key handed over to the UI's Back key; the scene's own UI Manager and EventSystem kept. They need the
    /// Character, Camera Rig, Input Manager and UI Manager prefabs.
    /// </summary>
    [Category(TestArea.UI)]
    [Category(Models.PreparedModelTests.SampleContent)]
    [TestTiming]
    public class UISceneSetupTests
    {
        private Scene scene;
        private GameObject characterPrefab;

        [SetUp]
        public void SetUp()
        {
            characterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterSceneSetup.CharacterPrefabPath);
            if (characterPrefab == null) { Assert.Ignore($"There's no character prefab at '{CharacterSceneSetup.CharacterPrefabPath}'."); }
            if (AssetDatabase.LoadAssetAtPath<GameObject>(UIAssets.ManagerPath) == null) { Assert.Ignore($"There's no UI Manager prefab at '{UIAssets.ManagerPath}'."); }
            scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        [TearDown]
        public void TearDown() => EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        private void SetUpScene() =>
            Assert.That(CharacterSceneSetup.SetUp(scene, new CharacterSceneSetup.Settings { characterPrefab = characterPrefab }, withUndo: false), Is.True);

        private T Only<T>() where T : Component =>
            scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).Single();

        // How many of the event's persistent listeners call target's method.
        private static int Calls(UnityEventBase uiEvent, Object target, string method) =>
            Enumerable.Range(0, uiEvent.GetPersistentEventCount())
                .Count(i => uiEvent.GetPersistentTarget(i) == target && uiEvent.GetPersistentMethodName(i) == method);

        [Test]
        public void SetUp_AddsTheUIManager_AndConnectsItToTheCameraAndThePlayersInput()
        {
            SetUpScene();
            UIManager manager = Only<UIManager>();
            Assert.That(manager.transform.IsChildOf(CharacterSceneSetup.FindSetup(scene).transform), Is.True, "It should be in the setup.");
            Assert.That(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(manager.gameObject), Is.EqualTo(UIAssets.ManagerPath), "As an instance of its prefab.");

            MMotionCameraRig rig = Only<MMotionCameraRig>();
            PlayerInputReader reader = Only<PlayerInputReader>();
            Assert.That(Calls(manager.OnCursorNeededChanged, rig, nameof(MMotionCameraRig.SetCursorRequested)), Is.EqualTo(1));
            Assert.That(Calls(manager.OnGameInputBlockedChanged, reader, nameof(PlayerInputReader.SetSuppressed)), Is.EqualTo(1));
            Assert.That(rig.CursorSettings.unlockKey, Is.EqualTo(Key.None), "Back (Escape) should be the UI's: the camera's unlock key goes.");
            Assert.That(manager.GetComponent<PauseMenuOnBack>(), Is.Not.Null, "Back with no window should open the pause menu.");
            // By its name: the bridge is CharacterUI's, which these tests don't reference.
            Assert.That(reader.GetComponents<MonoBehaviour>().Count(c => c != null && c.GetType().Name == "SprintModeOption"), Is.EqualTo(1),
                "The controls page's Sprint option should be beside the player's input reader.");
            Assert.That(scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<EventSystem>()).Count(), Is.EqualTo(1));
        }

        [Test]
        public void TheScenesOwnUIManagerAndEventSystem_AreKept_AndConnectedOnce()
        {
            var own = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(UIAssets.ManagerPath), scene);
            own.name = "Own UI";
            var eventSystem = new GameObject("Own EventSystem", typeof(EventSystem));
            SceneManager.MoveGameObjectToScene(eventSystem, scene);

            SetUpScene();
            SetUpScene();
            UIManager manager = Only<UIManager>();
            Assert.That(manager.gameObject, Is.SameAs(own), "The scene's own UI Manager should be kept.");
            Assert.That(Calls(manager.OnCursorNeededChanged, Only<MMotionCameraRig>(), nameof(MMotionCameraRig.SetCursorRequested)), Is.EqualTo(1),
                "Set up twice, it should be connected once.");
            Assert.That(scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<EventSystem>()).Single().gameObject,
                Is.SameAs(eventSystem), "The scene's own EventSystem should be the one left on.");
        }
    }
}
