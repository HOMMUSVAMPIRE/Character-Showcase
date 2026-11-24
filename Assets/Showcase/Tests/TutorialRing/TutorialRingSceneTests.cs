using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MeridianMotion.Characters;
using MeridianMotion.Levels;
using MeridianMotion.Showcase.TutorialRing;
using MeridianMotion.Tests;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace MeridianMotion.Showcase.Tests.TutorialRing
{
    /// <summary>
    /// Tests of the Tutorial Ring's scene, as it's built and set up (Assets/Showcase/Scenes/TutorialRing), played in the
    /// game's own loop with the player's character driven from the test: it spawns at the hub with one jump (its own
    /// tuned copy of the movement), and what it can do is the envelope without its air jumps (what the level was sized
    /// for); every lesson zone is a lesson of the catalog's; the roll lesson stops crouch-walking and lets a roll
    /// through; the lessons shortcut opens from the lessons' end and not from the hub; and falling off the ring
    /// respawns the character at the checkpoint it reached last.
    /// </summary>
    [Category(TestArea.Levels)]
    [TestTiming]
    public class TutorialRingSceneTests
    {
        private const string ScenePath = "Assets/Showcase/Scenes/TutorialRing/TutorialRing.unity";
        private Scene scene;
        private FrameTimeLimit fasterThanRealTime;
        private MMotionCharacter player;
        private MMotionMotor motor;
        private Driver driver;
        private bool promptsWereHidden;

        /// <summary>Drives the character from the test: each step's inputs are Inputs' (idle by default).</summary>
        private sealed class Driver : CharacterInputSource
        {
            public Func<StepInputs> Inputs = CharacterTestRig.Idle;

            public override CharacterInputFrame FrameFor(uint tick, MMotionMotor character) => CharacterInputFrame.Holding(Inputs(), tick);
        }

        [UnitySetUp]
        public IEnumerator LoadTheScene()
        {
            promptsWereHidden = TutorialPromptPresenter.Hidden;
#if UNITY_EDITOR
            AsyncOperation loading = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
#else
            AsyncOperation loading = SceneManager.LoadSceneAsync("TutorialRing", LoadSceneMode.Additive);
#endif
            while (!loading.isDone) { yield return null; }
            scene = SceneManager.GetSceneByPath(ScenePath);
            SceneManager.SetActiveScene(scene);
            fasterThanRealTime = FrameTimeLimit.Start(CharacterTestRig.TimeScale, CharacterTestRig.MaxFrameTime);
            CharacterSpawner spawner = Object.FindAnyObjectByType<CharacterSpawner>();
            Assert.That(spawner, Is.Not.Null, "The scene should be set up (Set Up Scene) with a spawner.");
            float giveUp = Time.realtimeSinceStartup + 20f;
            while (spawner.PlayerCharacter == null && Time.realtimeSinceStartup < giveUp) { yield return null; }
            Assert.That(spawner.PlayerCharacter, Is.Not.Null, "The player's character should spawn.");
            player = spawner.PlayerCharacter.GetComponent<MMotionCharacter>();
            motor = player.GetComponent<MMotionMotor>();
            driver = player.gameObject.AddComponent<Driver>();
            player.InputSource = driver;
        }

        [UnityTearDown]
        public IEnumerator UnloadTheScene()
        {
            if (fasterThanRealTime != null) { Object.Destroy(fasterThanRealTime.gameObject); }
            if (scene.IsValid() && scene.isLoaded)
            {
                AsyncOperation unloading = SceneManager.UnloadSceneAsync(scene);
                while (unloading != null && !unloading.isDone) { yield return null; }
            }
            TutorialPromptPresenter.Hidden = promptsWereHidden;
        }

        private static IEnumerator Steps(int steps)
        {
            for (int i = 0; i < steps; i++) { yield return new WaitForFixedUpdate(); }
        }

        /// <summary>Steps until done, or seconds of game time; passes whether done came true to result, if given.</summary>
        private static IEnumerator Until(Func<bool> done, float seconds, Action<bool> result = null)
        {
            int limit = CharacterTestRig.StepsFor(seconds);
            for (int i = 0; i < limit && !done(); i++) { yield return new WaitForFixedUpdate(); }
            result?.Invoke(done());
        }

        private IEnumerator RespawnAt(string checkpointId)
        {
            Checkpoint point = Checkpoint.Find(checkpointId);
            Assert.That(point, Is.Not.Null, $"The level should have the checkpoint '{checkpointId}'.");
            driver.Inputs = CharacterTestRig.Idle;
            motor.Respawn(point.Feet, point.Facing);
            yield return Steps(15);
            Assert.That(motor.levelPresence.active, Is.SameAs(point), $"Put at '{checkpointId}', the character should reach it.");
        }

        [UnityTest]
        public IEnumerator ThePlayer_SpawnsAtTheHub_WithOneJump()
        {
            yield return Steps(2);
            Checkpoint hub = Checkpoint.Start;
            Assert.That(hub, Is.Not.Null);
            Assert.That(Vector3.Distance(motor.Feet, hub.Feet), Is.LessThan(1.5f), $"The player should spawn at the hub (it's at {motor.Feet}).");
            var mode = (AbilityMovementMode)motor.movementMode;
            Assert.That(mode.Get<Jumping>().MaxJumps, Is.EqualTo(1), "The tutorial's character has one jump.");
            Assert.That(player.GetComponent<OwnedMovementMode>(), Is.Not.Null, "It should move with its own tuned copy of the movement.");
        }

        [Serializable] private class EnvelopeMove { public float distance, height; public string move; }
        [Serializable] private class Envelope { public List<EnvelopeMove> jumps, climbs; public float slideLength, rollLength, wallRunLength; }

        [UnityTest]
        public IEnumerator TheTutorialCharactersCapabilities_MatchTheEnvelopeWithoutAirJumps()
        {
            var models = player.GetComponent<CharacterModelSlot>();
            float giveUp = Time.realtimeSinceStartup + 20f;
            while (models != null && !models.IsModelReady && Time.realtimeSinceStartup < giveUp) { yield return null; }
            string path = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? "", "Tools", "LevelSpec", "envelope.json");
            var envelope = JsonUtility.FromJson<Envelope>(File.ReadAllText(path));
            MovementCapabilities measured = motor.Capabilities;

            Assert.That(measured.Jumps.Any(j => j.Name.Contains("double jump")) || measured.Climbs.Any(c => c.Name.Contains("double jump")), Is.False,
                "With one jump, the character should have no double jumps.");
            foreach (EnvelopeMove jump in envelope.jumps.Where(j => !j.move.Contains("double jump")))
            {
                MovementCapabilities.Move found = measured.Jumps.FirstOrDefault(j => j.Name == jump.move);
                Assert.That(found.Name, Is.EqualTo(jump.move), $"The character should be able to {jump.move}.");
                Assert.That(found.Reach, Is.EqualTo(jump.distance).Within(0.01f), $"{jump.move}: the level was sized for {jump.distance} m.");
            }
            foreach (EnvelopeMove climb in envelope.climbs.Where(c => !c.move.Contains("double jump")))
            {
                MovementCapabilities.Move found = measured.Climbs.FirstOrDefault(c => c.Name == climb.move);
                Assert.That(found.Name, Is.EqualTo(climb.move), $"The character should be able to {climb.move}.");
                Assert.That(found.Reach, Is.EqualTo(climb.height).Within(0.01f), $"{climb.move}: the level was sized for {climb.height} m.");
            }
            Assert.That(measured.SlideLength, Is.EqualTo(envelope.slideLength).Within(0.01f));
            Assert.That(measured.RollLength, Is.EqualTo(envelope.rollLength).Within(0.01f));
            Assert.That(measured.WallRunLength, Is.EqualTo(envelope.wallRunLength).Within(0.01f));
        }

        [UnityTest]
        public IEnumerator EveryLessonZone_IsALessonOfTheCatalogs()
        {
            yield return null;
            TutorialRingSetup setup = Object.FindAnyObjectByType<TutorialRingSetup>();
            Assert.That(setup, Is.Not.Null, "Set Up Scene should add the tutorial's game.");
            Assert.That(setup.Lessons, Is.Not.Null, "The player's character should have its lessons tracked.");
            LessonZone[] zones = Object.FindObjectsByType<LessonZone>();
            Assert.That(zones.Length, Is.EqualTo(setup.Catalog.Lessons.Count));
            foreach (LessonZone zone in zones) { Assert.That(setup.Catalog.Find(zone.LessonId), Is.Not.Null, $"'{zone.LessonId}' should be in the catalog."); }
        }

        /// <summary>
        /// From the roll lesson's start, crouches and walks up to its blocker, waits for it to open, then tries the way
        /// given through: whether the character got past the blocked stretch without being respawned.
        /// </summary>
        private IEnumerator TryTheRollLesson(Func<StepInputs> through, Action<bool> passed)
        {
            yield return RespawnAt("L06-roll");
            TickedPlatform blocker = Object.FindObjectsByType<TickedPlatform>().First(p => p.name.Contains("Roll Blocker"));
            blocker.PoseAt(0, out Vector3 start, out _);
            Vector3 forward = Checkpoint.Find("L06-roll").Facing * Vector3.forward;
            float windowStart = Vector3.Dot(start, forward) - 1.5f, windowEnd = windowStart + 3f;
            float along() => Vector3.Dot(motor.Feet, forward);
            Rigidbody body = blocker.GetComponent<Rigidbody>();
            // Open: at its trip's start, beside the lane (where it is at tick 0); closed: across it.
            blocker.PoseAt(0, out Vector3 openPosition, out _);
            bool IsOpen() => Vector3.Distance(body.position, openPosition) < 0.05f;

            driver.Inputs = () => CharacterTestRig.Crouch(forward);
            yield return Until(() => along() >= windowStart - 0.9f, 12f);
            Assume.That(along(), Is.GreaterThanOrEqualTo(windowStart - 1.2f), "Test setup: the character should crouch-walk up to the blockers.");
            driver.Inputs = () => CharacterTestRig.Crouch();
            yield return Until(() => !IsOpen(), 3f);
            yield return Until(IsOpen, 3f);
            Assume.That(IsOpen(), Is.True, "Test setup: the blocker should open.");
            int respawns = motor.levelPresence.respawns;
            int step = 0;
            driver.Inputs = () => step++ < 2 ? through() : CharacterTestRig.Crouch(forward);
            yield return Until(() => along() >= windowEnd + 1f || motor.levelPresence.respawns != respawns, 3f);
            passed(along() >= windowEnd + 1f && motor.levelPresence.respawns == respawns);
        }

        /// <summary>
        /// The roll lesson's lock: crouch-walking is too slow (the blocker comes back while it's in the way, and respawns
        /// it), and a roll gets through while it's open. A dash, taught last, gets through too on a revisit: it's quicker
        /// than a roll, so no timing that lets a roll through stops it; the design accepts that (ReferenceAssets/OpenTasks.md, K).
        /// </summary>
        [UnityTest]
        public IEnumerator TheRollLesson_CrouchWalkingFails_RollingPasses()
        {
            yield return Steps(2);
            Vector3 forward = Checkpoint.Find("L06-roll").Facing * Vector3.forward;
            bool crouched = true, rolled = false;
            yield return TryTheRollLesson(() => CharacterTestRig.Crouch(forward), p => crouched = p);
            yield return TryTheRollLesson(() => CharacterTestRig.Roll(forward), p => rolled = p);
            Assert.That(rolled, Is.True, "A roll should get through the blockers while they're open.");
            Assert.That(crouched, Is.False, "Crouch-walking should be too slow to get through.");
        }

        [UnityTest]
        public IEnumerator TheLessonsShortcut_OpensFromTheLessonsEnd_NotFromTheHub()
        {
            yield return Steps(2);
            ShortcutGate gate = ShortcutGate.Find("lessons");
            Assert.That(gate, Is.Not.Null);
            // Standing at the gate on the hub's side doesn't open it.
            Vector3 gateFeet = gate.transform.position;
            Vector3 hub = Checkpoint.Start.Feet;
            motor.Respawn(new Vector3(gateFeet.x, hub.y, gateFeet.z) + (hub - gateFeet).WithY(0f).normalized * 2f, Quaternion.identity);
            yield return Steps(20);
            Assert.That(gate.IsOpen, Is.False, "From the hub's side, the gate should stay shut.");
            ShortcutTrigger trigger = Object.FindObjectsByType<ShortcutTrigger>().First(t => t.Gate == gate);
            motor.Respawn(new Vector3(trigger.transform.position.x, hub.y, trigger.transform.position.z), Quaternion.identity);
            yield return Steps(4);
            Assert.That(gate.IsOpen, Is.True, "Reaching its trigger at the lessons' end should open it.");
        }

        [UnityTest]
        public IEnumerator FallingOffTheRing_RespawnsAtTheLastCheckpoint()
        {
            yield return RespawnAt("L03-sprint");
            Checkpoint reached = motor.levelPresence.active;
            int respawns = motor.levelPresence.respawns;
            // Over the ground, well away from the shortcut's bridge across the middle.
            motor.Respawn(new Vector3(60f, 12f, 60f), Quaternion.identity);
            yield return Until(() => motor.levelPresence.respawns != respawns, 5f);
            Assert.That(motor.levelPresence.respawns, Is.EqualTo(respawns + 1), "Falling into the ground's hazard should respawn the character.");
            yield return Steps(2);
            Assert.That(Vector3.Distance(motor.Feet, reached.Feet), Is.LessThan(1f), $"It should be back at '{reached.Id}' (it's at {motor.Feet}).");
        }
    }

    internal static class VectorExtensions
    {
        public static Vector3 WithY(this Vector3 v, float y) => new(v.x, y, v.z);
    }
}
