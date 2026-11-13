using System.Linq;
using CharacterShowcase.EditorTools;
using Level;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CharacterShowcase.Tests.EditMode
{
    /// <summary>
    /// Checks of LevelFixtureSetup (part of a scene's game setup), on a new, unsaved scene: a fixture's settings read
    /// from its name, moving platforms and pushable crates given what they need from them, and timing gates given a
    /// course timer each (one per course, under the game setup) with their displays wired to it. Fixtures are named
    /// as the level builders name them (Graybox.Mechanics).
    /// </summary>
    public class LevelFixtureSetupTests
    {
        private Scene scene;

        [SetUp]
        public void SetUp() => scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // An empty scene in place of the test's, so nothing from it carries into the next test.
        [TearDown]
        public void TearDown() => EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        private GameObject Add(string name, Transform parent = null)
        {
            var obj = new GameObject(name);
            if (parent != null) { obj.transform.SetParent(parent, false); }
            else { SceneManager.MoveGameObjectToScene(obj, scene); }
            return obj;
        }

        [Test]
        public void Parameters_AreReadFromTheName()
        {
            var p = LevelFixtureSetup.Parameters("Moving Platform (Lift) | travel=0,6,0 | period=5 | smooth=0");
            Assert.AreEqual("0,6,0", p["travel"]);
            Assert.AreEqual("5", p["period"]);
            Assert.AreEqual("0", p["smooth"]);
            Assert.IsEmpty(LevelFixtureSetup.Parameters("Moving Platform"));
            Assert.IsTrue(LevelFixtureSetup.IsNamed("Moving Platform (Lift) | period=5", LevelFixtureSetup.MovingPlatformPrefix));
            Assert.IsFalse(LevelFixtureSetup.IsNamed("Moving Platforms", LevelFixtureSetup.MovingPlatformPrefix), "A group named for platforms isn't one.");
        }

        [Test]
        public void SetUp_MakesMovingPlatformsAndCrates_FromTheirNames()
        {
            GameObject platform = Add("Moving Platform (Lift) | travel=0,6,0 | period=5 | pause=1.5 | spin=0,30,0 | phase=0.25 | smooth=0");
            platform.AddComponent<BoxCollider>();
            GameObject crate = Add("Pushable Crate | pull=0 | speed=0.8");
            crate.AddComponent<BoxCollider>();
            Transform setup = Add("Game Setup").transform;

            string summary = LevelFixtureSetup.SetUp(scene, setup, null, withUndo: false);

            var moving = platform.GetComponent<MovingPlatform>();
            Assert.IsNotNull(moving);
            Assert.IsNotNull(platform.GetComponent<Rigidbody>());
            Assert.AreEqual(new Vector3(0f, 6f, 0f), moving.travel);
            Assert.AreEqual(5f, moving.period);
            Assert.AreEqual(1.5f, moving.pauseAtEnds);
            Assert.AreEqual(new Vector3(0f, 30f, 0f), moving.spinDegreesPerSecond);
            Assert.AreEqual(0.25f, moving.phaseOffset);
            Assert.IsFalse(moving.smooth);
            var pushable = crate.GetComponent<PushableObject>();
            Assert.IsNotNull(pushable);
            Assert.IsFalse(pushable.CanBePulled);
            Assert.AreEqual(0.8f, pushable.SpeedMultiplier, 1e-5f);
            StringAssert.Contains("1 moving platform", summary);
            StringAssert.Contains("1 pushable crate", summary);
        }

        [Test]
        public void SetUp_MakesLaddersFromTheirNames_KeepingTheLevelsMesh_ButNotFutureLadderMarkers()
        {
            GameObject ladder = Add("Ladder | height=5.5 | width=0.7 | spacing=0.25 | rail=0.05 | rung=0.03");
            var levelMesh = new Mesh { name = "The Level's Ladder" };
            ladder.AddComponent<MeshFilter>().sharedMesh = levelMesh;
            ladder.AddComponent<BoxCollider>();
            GameObject marker = Add("Ladder (future): up the shaft");
            Transform setup = Add("Game Setup").transform;

            string summary = LevelFixtureSetup.SetUp(scene, setup, null, withUndo: false);

            var climbable = ladder.GetComponent<Ladder>();
            Assert.IsNotNull(climbable);
            Assert.IsFalse(climbable.BuildsMesh, "A level's ladder should keep the level's mesh.");
            Assert.AreSame(levelMesh, ladder.GetComponent<MeshFilter>().sharedMesh);
            Assert.AreEqual(5.5f, climbable.Height, 1e-5f);
            Assert.AreEqual(0.7f, climbable.Width, 1e-5f);
            Assert.AreEqual(0.25f, climbable.RungSpacing, 1e-5f);
            Assert.AreEqual(0.05f, climbable.Depth, 1e-5f);
            Assert.AreEqual(0.03f, climbable.RungSize, 1e-5f);
            Assert.IsNull(marker.GetComponent<Ladder>(), "A future ladder's marker isn't a ladder.");
            StringAssert.Contains("1 ladder", summary);
            Object.DestroyImmediate(levelMesh);
        }

        [Test]
        public void SetUp_GivesEachCourseATimer_AndWiresItsGatesAndDisplays()
        {
            Transform gates = Add("Gates").transform;
            GameObject start = Add("Timing Gate | course=Speed Trap | role=start | distance=100", gates);
            start.AddComponent<BoxCollider>();
            GameObject finish = Add("Timing Gate | course=Speed Trap | role=finish | distance=100", gates);
            finish.AddComponent<BoxCollider>();
            GameObject lap = Add("Timing Gate | course=Lap | role=start-finish", gates);
            lap.AddComponent<BoxCollider>();
            // Any component with a string text property will do as a display; the level's are TextMeshPro.
            GameObject display = Add("Timing Display | course=Speed Trap", gates);
            TextMesh text = display.AddComponent<TextMesh>();
            Transform setup = Add("Game Setup").transform;

            LevelFixtureSetup.SetUp(scene, setup, null, withUndo: false);

            CourseTimer[] timers = setup.GetComponentsInChildren<CourseTimer>();
            Assert.AreEqual(2, timers.Length, "One timer per course.");
            CourseTimer trap = timers.Single(t => t.CourseName == "Speed Trap");
            Assert.AreEqual(100f, trap.Distance);
            Assert.AreSame(trap, start.GetComponent<TimingGate>().Timer);
            Assert.AreEqual(TimingGateRole.Start, start.GetComponent<TimingGate>().Role);
            Assert.AreEqual(TimingGateRole.Finish, finish.GetComponent<TimingGate>().Role);
            Assert.AreEqual(TimingGateRole.StartAndFinish, lap.GetComponent<TimingGate>().Role);
            Assert.IsTrue(start.GetComponent<Collider>().isTrigger);

            Assert.AreEqual(1, trap.OnDisplay.GetPersistentEventCount(), "The display should be wired to its course's timer.");
            // Persistent listeners run only in play mode, so check what it calls rather than calling it.
            Assert.AreSame(text, trap.OnDisplay.GetPersistentTarget(0));
            Assert.AreEqual("set_text", trap.OnDisplay.GetPersistentMethodName(0));
        }
    }
}
