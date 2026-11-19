using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using MeridianMotion.Characters;
using NUnit.Framework;
using UnityEngine.TestTools;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MeridianMotion.Tests.Spawning
{
    /// <summary>
    /// Tests of CharacterSpawner: it spawns its prefab at one of its spawn points, facing the way that point does, on
    /// Start or when asked, chooses among all its points, tells listeners about the character, and refuses (with an
    /// error) when it has no prefab or no spawn points. The prefab is a plain object, so these don't depend on the
    /// character's setup (its input manager and camera).
    /// </summary>
    [Category(TestArea.Spawning)]
    [TestTiming]
    public class SpawnerTests
    {
        private readonly List<Object> created = new();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (Object obj in created) { if (obj != null) { Object.Destroy(obj); } }
            created.Clear();
            yield return null;
        }

        private T Track<T>(T obj) where T : Object
        {
            created.Add(obj);
            return obj;
        }

        /// <summary>A stand-in for a character prefab: an inactive template, so it doesn't count as spawned itself.</summary>
        private GameObject Template()
        {
            var template = Track(new GameObject("Spawn Test Character"));
            template.SetActive(false);
            return template;
        }

        /// <summary>A spawner that doesn't spawn on Start, so tests can set it up first.</summary>
        private CharacterSpawner Spawner(GameObject prefab, params SpawnPoint[] points)
        {
            var holder = Track(new GameObject("Spawner"));
            holder.SetActive(false);
            var spawner = holder.AddComponent<CharacterSpawner>();
            spawner.SpawnOnStart = false;
            spawner.CharacterPrefab = prefab;
            spawner.SpawnPoints.Clear();
            spawner.SpawnPoints.AddRange(points);
            holder.SetActive(true);
            return spawner;
        }

        [Test]
        public void Spawn_PlacesTheCharacterAtThePoint_FacingItsWay()
        {
            var point = new SpawnPoint(new Vector3(3f, 2f, -5f), Quaternion.Euler(0f, 135f, 0f));
            CharacterSpawner spawner = Spawner(Template(), point);

            GameObject character = Track(spawner.Spawn());

            Assert.IsNotNull(character);
            Assert.AreSame(character, spawner.Character);
            Assert.AreEqual(0, spawner.LastSpawnIndex);
            Assert.Less(Vector3.Distance(point.position, character.transform.position), 1e-4f);
            Assert.Less(Quaternion.Angle(point.rotation, character.transform.rotation), 0.01f);
        }

        [Test]
        public void SpawnAt_UsesTheGivenPoint()
        {
            var points = new[]
            {
                new SpawnPoint(new Vector3(0f, 0f, 0f), Quaternion.identity),
                new SpawnPoint(new Vector3(10f, 0f, 0f), Quaternion.Euler(0f, 90f, 0f)),
                new SpawnPoint(new Vector3(20f, 0f, 0f), Quaternion.Euler(0f, 180f, 0f)),
            };
            CharacterSpawner spawner = Spawner(Template(), points);

            GameObject character = Track(spawner.SpawnAt(2));

            Assert.AreEqual(2, spawner.LastSpawnIndex);
            Assert.Less(Vector3.Distance(points[2].position, character.transform.position), 1e-4f);
            Assert.Less(Quaternion.Angle(points[2].rotation, character.transform.rotation), 0.01f);
        }

        [Test]
        public void Spawn_ChoosesAmongAllThePoints()
        {
            var points = new[]
            {
                new SpawnPoint(new Vector3(0f, 0f, 0f), Quaternion.identity),
                new SpawnPoint(new Vector3(10f, 0f, 0f), Quaternion.identity),
                new SpawnPoint(new Vector3(20f, 0f, 0f), Quaternion.identity),
            };
            CharacterSpawner spawner = Spawner(Template(), points);

            // A fixed seed, so the test can't fail by bad luck: 200 draws from 3 points miss one with chance ~1e-35
            // anyway, but the seed keeps the result the same on every run.
            Random.State previous = Random.state;
            Random.InitState(12345);
            var used = new HashSet<int>();
            try
            {
                for (int i = 0; i < 200; i++)
                {
                    GameObject character = Track(spawner.Spawn());
                    used.Add(spawner.LastSpawnIndex);
                    Assert.AreEqual(points[spawner.LastSpawnIndex].position, character.transform.position);
                }
            }
            finally { Random.state = previous; }

            CollectionAssert.AreEquivalent(new[] { 0, 1, 2 }, used);
        }

        [UnityTest]
        public IEnumerator SpawnOnStart_SpawnsWithoutBeingAsked()
        {
            var holder = Track(new GameObject("Spawner"));
            holder.SetActive(false);
            var spawner = holder.AddComponent<CharacterSpawner>();
            spawner.SpawnOnStart = true;
            spawner.CharacterPrefab = Template();
            spawner.SpawnPoints.Clear();
            spawner.SpawnPoints.Add(new SpawnPoint(new Vector3(1f, 0f, 1f), Quaternion.identity));
            holder.SetActive(true);

            yield return null; // Start runs before the first frame's Update.

            Assert.IsNotNull(spawner.Character, "The spawner should have spawned a character on Start.");
            Track(spawner.Character);
            Assert.AreEqual(new Vector3(1f, 0f, 1f), spawner.Character.transform.position);
        }

        [UnityTest]
        public IEnumerator SpawnOnStart_Off_WaitsToBeAsked()
        {
            CharacterSpawner spawner = Spawner(Template(), new SpawnPoint(Vector3.zero, Quaternion.identity));

            yield return null;

            Assert.IsNull(spawner.Character);
            Assert.AreEqual(-1, spawner.LastSpawnIndex);
        }

        [Test]
        public void Spawn_TellsListeners()
        {
            CharacterSpawner spawner = Spawner(Template(), new SpawnPoint(Vector3.zero, Quaternion.identity));
            GameObject heard = null;
            Transform heardByEvent = null;
            spawner.CharacterSpawned += character => heard = character;
            spawner.OnCharacterSpawned.AddListener(character => heardByEvent = character);

            GameObject spawned = Track(spawner.Spawn());

            Assert.AreSame(spawned, heard);
            Assert.AreSame(spawned.transform, heardByEvent);
        }

        [Test]
        public void Spawn_WithAnEmptyRotation_FacesTheDefaultWay()
        {
            // A spawn point added in the inspector to an empty list starts with an all-zero rotation.
            CharacterSpawner spawner = Spawner(Template(), new SpawnPoint(Vector3.zero, new Quaternion(0f, 0f, 0f, 0f)));

            GameObject character = Track(spawner.Spawn());

            Assert.Less(Quaternion.Angle(Quaternion.identity, character.transform.rotation), 0.01f);
        }

        /// <summary>
        /// Spawns the real character prefab, with its controller off as the test rig has it (it would read the absent
        /// input manager). The rig is only there for its camera, which the character's movement expects.
        /// </summary>
        private GameObject SpawnCharacter(CharacterSpawner spawner)
        {
            GameObject character = Track(spawner.Spawn());
            character.GetComponent<MMotionCharacter>().enabled = false;
            return character;
        }

        [UnityTest]
        public IEnumerator Spawn_UsesTheChosenModelAndMovementMode_InsteadOfThePrefabs()
        {
            using var rig = new CharacterTestRig();
            GameObject template = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            template.name = "Cube Model";
            Object.Destroy(template.GetComponent<Collider>());
            template.SetActive(false);
            var model = Track(ScriptableObject.CreateInstance<PrefabModelSource>());
            model.ModelPrefab = template;
            var mode = Track(ScriptableObject.CreateInstance<StandardMovementMode>());
            CharacterSpawner spawner = Spawner(CharacterTestRig.CharacterPrefab(), new SpawnPoint(Vector3.zero, Quaternion.identity));
            spawner.ModelSource = model;
            spawner.MovementMode = mode;

            GameObject character = SpawnCharacter(spawner);
            var host = character.GetComponent<CharacterModelSlot>();

            Assert.AreSame(mode, character.GetComponent<MMotionMotor>().movementMode);
            Assert.AreSame(model, host.ModelSource, "The chosen model should be the one loaded on Start.");
            float giveUp = Time.realtimeSinceStartup + 10f;
            while (!host.IsModelReady && Time.realtimeSinceStartup < giveUp) { yield return null; }
            Assert.IsTrue(host.IsModelReady, "The chosen model didn't load.");
            Assert.AreSame(model, host.ModelSource);
            StringAssert.StartsWith("Cube Model", host.ModelRoot.name);
        }

        [Test]
        public void Spawn_GivesTheCameraEventTheCharactersEyePointAndSize()
        {
            using var rig = new CharacterTestRig();
            CharacterSpawner spawner = Spawner(CharacterTestRig.CharacterPrefab(), new SpawnPoint(Vector3.zero, Quaternion.identity));
            Transform heardTarget = null, heardEyes = null, heardSize = null;
            spawner.OnCameraTargetSpawned.AddListener((target, eyes, size) => (heardTarget, heardEyes, heardSize) = (target, eyes, size));

            GameObject character = SpawnCharacter(spawner);
            var host = character.GetComponent<CharacterModelSlot>();

            Assert.AreSame(character.transform, heardTarget);
            Assert.AreSame(host.EyePoint, heardEyes);
            Transform expectedSize = host.ModelParent != character.transform ? host.ModelParent : null;
            Assert.AreSame(expectedSize, heardSize, "The size reference should be the model parent, whose scale is the character's size.");
        }

        /// <summary>
        /// Spawning the local player's character and an AI's: each is told who it's for (MMotionCharacter.Owner), and
        /// only the local player's is handed to the camera, so the camera follows the player whatever else is spawned.
        /// </summary>
        [Test]
        public void OnlyTheLocalPlayersCharacter_IsHandedToTheCamera()
        {
            using var rig = new CharacterTestRig();
            CharacterSpawner spawner = Spawner(CharacterTestRig.CharacterPrefab(), new SpawnPoint(Vector3.zero, Quaternion.identity),
                new SpawnPoint(new Vector3(4f, 0f, 0f), Quaternion.identity));
            var followed = new List<Transform>();
            spawner.OnCameraTargetSpawned.AddListener((target, _, _) => followed.Add(target));

            GameObject player = Track(spawner.SpawnAt(0));
            GameObject npc = Track(spawner.SpawnAt(1, ControlledBy.Npc));
            // Not stepped here (as SpawnCharacter's aren't).
            player.GetComponent<MMotionCharacter>().enabled = false;
            npc.GetComponent<MMotionCharacter>().enabled = false;

            Assert.That(player.GetComponent<MMotionCharacter>().ControlledBy, Is.EqualTo(ControlledBy.LocalPlayer));
            Assert.That(npc.GetComponent<MMotionCharacter>().ControlledBy, Is.EqualTo(ControlledBy.Npc));
            Assert.That(followed, Is.EqualTo(new[] { player.transform }), "Only the local player's character should be handed to the camera.");
            Assert.AreSame(npc, spawner.Character, "The spawner's Character is still the latest spawned.");
        }

        /// <summary>
        /// An NPC spawned from a route is an AI's character (ControlledBy.Npc) walking it (a WaypointInputSource, its
        /// input source), not handed to the camera; the spawner's PlayerCharacter stays the player's.
        /// </summary>
        [Test]
        public void AnNpc_IsAnAIsCharacter_WalkingItsRoute_NotFollowedByTheCamera()
        {
            using var rig = new CharacterTestRig();
            CharacterSpawner spawner = Spawner(CharacterTestRig.CharacterPrefab(), new SpawnPoint(Vector3.zero, Quaternion.identity),
                new SpawnPoint(new Vector3(4f, 0f, 0f), Quaternion.identity));
            var followed = new List<Transform>();
            spawner.OnCameraTargetSpawned.AddListener((target, _, _) => followed.Add(target));
            var route = new NpcRoute { spawnPoint = 1, waypoints = { new Vector3(4f, 0f, 5f), new Vector3(0f, 0f, 5f) }, loop = true };

            GameObject player = Track(spawner.SpawnAt(0));
            GameObject npc = Track(spawner.SpawnNpc(route));
            player.GetComponent<MMotionCharacter>().enabled = false;
            var controller = npc.GetComponent<MMotionCharacter>();
            controller.enabled = false;

            Assert.That(controller.ControlledBy, Is.EqualTo(ControlledBy.Npc));
            var walker = npc.GetComponent<WaypointInputSource>();
            Assert.That(walker, Is.Not.Null, "The NPC should have an AI walking its route.");
            Assert.AreSame(walker, controller.InputSource, "The AI should drive the NPC's input.");
            Assert.That(walker.Waypoints, Is.EqualTo(route.waypoints));
            Assert.That(walker.Loop, Is.True);
            Assert.That(followed, Is.EqualTo(new[] { player.transform }), "Only the player's character should be handed to the camera.");
            Assert.AreSame(player, spawner.PlayerCharacter, "PlayerCharacter should stay the player's.");
            Assert.AreSame(npc, spawner.Character, "Character is the latest spawned.");
        }

        [Test]
        public void Spawn_WithoutOverrides_KeepsThePrefabsModelAndMovementMode()
        {
            using var rig = new CharacterTestRig();
            GameObject prefab = CharacterTestRig.CharacterPrefab();
            CharacterSpawner spawner = Spawner(prefab, new SpawnPoint(Vector3.zero, Quaternion.identity));

            GameObject character = SpawnCharacter(spawner);

            Assert.AreSame(prefab.GetComponent<MMotionMotor>().movementMode, character.GetComponent<MMotionMotor>().movementMode);
            Assert.AreSame(prefab.GetComponent<CharacterModelSlot>().ModelSource, character.GetComponent<CharacterModelSlot>().ModelSource);
        }

        [Test]
        public void Spawn_WithoutSpawnPoints_RefusesWithAnError()
        {
            CharacterSpawner spawner = Spawner(Template());

            LogAssert.Expect(LogType.Error, new Regex("no spawn points"));
            Assert.IsNull(spawner.Spawn());
            Assert.IsNull(spawner.Character);
        }

        [Test]
        public void Spawn_WithoutAPrefab_RefusesWithAnError()
        {
            CharacterSpawner spawner = Spawner(null, new SpawnPoint(Vector3.zero, Quaternion.identity));

            LogAssert.Expect(LogType.Error, new Regex("no character prefab"));
            Assert.IsNull(spawner.Spawn());
        }

        [Test]
        public void SpawnAt_OutOfRange_RefusesWithAnError()
        {
            CharacterSpawner spawner = Spawner(Template(), new SpawnPoint(Vector3.zero, Quaternion.identity));

            LogAssert.Expect(LogType.Error, new Regex("no spawn point 3"));
            Assert.IsNull(spawner.SpawnAt(3));
        }
    }
}
