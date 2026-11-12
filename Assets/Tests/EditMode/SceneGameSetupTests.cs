using System.Linq;
using Character;
using CharacterShowcase.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CharacterShowcase.Tests.EditMode
{
    /// <summary>
    /// Checks of SceneGameSetup (what the Scene Setup window and the command line do), each on a new, unsaved scene
    /// replacing whatever is open (Unity can't add a new scene beside an untitled one, which the test runner may have):
    /// placed characters replaced by spawn points where they stood, spawn markers used as spawn points, a set-up scene
    /// keeping its spawn points when set up again, the camera wired to the spawner (copied from the demo scene, or the
    /// scene's own), the input manager copied once, and other cameras turned off rather than deleted. They need the
    /// demo scene, which the camera and input manager are copied from, and the Character prefab.
    /// </summary>
    [Category(PreparedModelTests.SampleContent)]
    public class SceneGameSetupTests
    {
        private Scene scene;
        private GameObject prefab;

        [SetUp]
        public void SetUp()
        {
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SceneGameSetup.CharacterPrefabPath);
            if (prefab == null) { Assert.Ignore($"There's no character prefab at '{SceneGameSetup.CharacterPrefabPath}'."); }
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(SceneGameSetup.DemoScenePath) == null) { Assert.Ignore($"There's no demo scene at '{SceneGameSetup.DemoScenePath}'."); }
            scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        // An empty scene in place of the test's, so nothing from it carries into the next test.
        [TearDown]
        public void TearDown() => EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        private GameObject Add(string name)
        {
            var gameObject = new GameObject(name);
            SceneManager.MoveGameObjectToScene(gameObject, scene);
            return gameObject;
        }

        private SceneGameSetup.Settings Settings() => new() { characterPrefab = prefab };

        [Test]
        public void SetUp_ReplacesThePlacedCharacter_WithASpawnPointWhereItStood()
        {
            var placed = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            placed.transform.SetPositionAndRotation(new Vector3(4f, 1f, -3f), Quaternion.Euler(0f, 60f, 0f));

            Assert.IsTrue(SceneGameSetup.SetUp(scene, Settings(), withUndo: false));

            Assert.IsTrue(placed == null, "The placed character should have been removed.");
            Assert.IsEmpty(SceneGameSetup.PlacedCharacters(scene));
            CharacterSpawner spawner = SceneGameSetup.FindSpawner(scene);
            Assert.IsNotNull(spawner);
            Assert.AreSame(prefab, spawner.CharacterPrefab);
            Assert.IsTrue(spawner.SpawnOnStart);
            Assert.AreEqual(1, spawner.SpawnPoints.Count);
            Assert.Less(Vector3.Distance(new Vector3(4f, 1f, -3f), spawner.SpawnPoints[0].position), 1e-4f);
            Assert.Less(Quaternion.Angle(Quaternion.Euler(0f, 60f, 0f), spawner.SpawnPoints[0].rotation), 0.01f);
        }

        [Test]
        public void SetUp_KeepsThePlacedCharacter_WhenAsked()
        {
            var placed = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            SceneGameSetup.Settings settings = Settings();
            settings.replacePlacedCharacters = false;
            settings.defaultSpawnPoint = new SpawnPoint(new Vector3(0f, 0f, 9f), Quaternion.identity);

            Assert.IsTrue(SceneGameSetup.SetUp(scene, settings, withUndo: false));

            Assert.IsTrue(placed != null);
            Assert.AreEqual(new Vector3(0f, 0f, 9f), SceneGameSetup.FindSpawner(scene).SpawnPoints.Single().position);
        }

        [Test]
        public void SetUp_SpawnsAtTheScenesSpawnMarkers()
        {
            // Kept in a group named like them, as the level builders do, which isn't a spawn point itself.
            GameObject level = Add("Level");
            var group = new GameObject($"{SceneGameSetup.SpawnMarkerPrefix}s").transform;
            group.SetParent(level.transform, false);
            group.position = new Vector3(0f, 50f, 0f);
            for (int i = 0; i < 3; i++)
            {
                var marker = new GameObject($"{SceneGameSetup.SpawnMarkerPrefix} ({i})").transform;
                marker.SetParent(group, false);
                marker.SetPositionAndRotation(new Vector3(i * 10f, 2f, 0f), Quaternion.Euler(0f, i * 90f, 0f));
            }

            Assert.IsTrue(SceneGameSetup.SetUp(scene, Settings(), withUndo: false));

            var points = SceneGameSetup.FindSpawner(scene).SpawnPoints;
            Assert.AreEqual(3, points.Count);
            for (int i = 0; i < 3; i++)
            {
                Assert.Less(Vector3.Distance(new Vector3(i * 10f, 2f, 0f), points[i].position), 1e-4f);
                Assert.Less(Quaternion.Angle(Quaternion.Euler(0f, i * 90f, 0f), points[i].rotation), 0.01f);
            }
        }

        [Test]
        public void SetUpAgain_KeepsItsSpawnPoints_AndReplacesTheOldSetup()
        {
            var points = new[]
            {
                new SpawnPoint(new Vector3(1f, 0f, 2f), Quaternion.identity),
                new SpawnPoint(new Vector3(-5f, 3f, 7f), Quaternion.Euler(0f, 180f, 0f)),
            };
            SceneGameSetup.Settings first = Settings();
            first.spawnPoints = points;
            Assert.IsTrue(SceneGameSetup.SetUp(scene, first, withUndo: false));

            Assert.IsTrue(SceneGameSetup.SetUp(scene, Settings(), withUndo: false));

            Assert.AreEqual(1, scene.GetRootGameObjects().Count(root => root.name == SceneGameSetup.SetupName), "The old setup should have been replaced.");
            CollectionAssert.AreEqual(points.Select(p => p.position), SceneGameSetup.FindSpawner(scene).SpawnPoints.Select(p => p.position));
            Assert.AreEqual(1, scene.GetRootGameObjects().Sum(root => root.GetComponentsInChildren<ACInputManager>(true).Length));
        }

        [Test]
        public void SetUp_WiresTheDemosCameraToTheSpawner_AndTurnsOffOtherCameras()
        {
            GameObject otherCamera = Add("Old Camera");
            otherCamera.AddComponent<Camera>();

            Assert.IsTrue(SceneGameSetup.SetUp(scene, Settings(), withUndo: false));

            Assert.IsTrue(otherCamera != null, "Other cameras should be turned off, not deleted.");
            Assert.IsFalse(otherCamera.activeSelf);
            GameObject setup = SceneGameSetup.FindSetup(scene);
            Assert.AreEqual(1, setup.GetComponentsInChildren<ACInputManager>(true).Length, "The demo's input manager should have been copied.");
            CharacterSpawner spawner = SceneGameSetup.FindSpawner(scene);
            AssertFollowedBy(spawner, setup.GetComponentsInChildren<Camera>(true).Single().gameObject);
        }

        [Test]
        public void SetUp_UsesTheScenesOwnCameraRig()
        {
            GameObject own = Add("Own Camera");
            own.AddComponent<Camera>();
            System.Type rigType = System.Type.GetType("CameraSystem.CameraRig, CameraSystem");
            Assert.IsNotNull(rigType);
            own.AddComponent(rigType);

            Assert.IsTrue(SceneGameSetup.SetUp(scene, Settings(), withUndo: false));

            Assert.IsTrue(own.activeSelf, "The scene's own camera rig shouldn't be turned off.");
            Assert.IsEmpty(SceneGameSetup.FindSetup(scene).GetComponentsInChildren<Camera>(true), "No camera should be copied.");
            AssertFollowedBy(SceneGameSetup.FindSpawner(scene), own);
        }

        /// <summary>The spawner's camera event calls Follow on the camera rig on the given object.</summary>
        private static void AssertFollowedBy(CharacterSpawner spawner, GameObject camera)
        {
            var cameraEvent = spawner.OnCameraTargetSpawned;
            Assert.AreEqual(1, cameraEvent.GetPersistentEventCount());
            var target = cameraEvent.GetPersistentTarget(0) as Component;
            Assert.IsNotNull(target);
            Assert.AreEqual("CameraRig", target.GetType().Name);
            Assert.AreSame(camera, target.gameObject);
            Assert.AreEqual("Follow", cameraEvent.GetPersistentMethodName(0));
        }
    }
}
