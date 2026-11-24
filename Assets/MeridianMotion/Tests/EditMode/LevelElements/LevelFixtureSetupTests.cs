using System.Collections.Generic;
using System.Linq;
using MeridianMotion.Levels.EditorTools;
using MeridianMotion.Levels;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine;

namespace MeridianMotion.Tests.EditMode.LevelElements
{
    /// <summary>
    /// Checks of LevelFixtureSetup (the level elements' editor code that the level builders and Set Up Scene both run), on
    /// a new, unsaved scene: a fixture's settings read from its name, moving platforms and pushable crates given what
    /// they need from them, and timing gates given a course timer each (one per course, in the scene's Course Timers
    /// group, found again rather than made twice) with their displays wired to it; checkpoints, hazards, lesson zones and
    /// shortcut gates and triggers from their names, a timing gate's split, and a crate that resets.
    /// </summary>
    [Category(TestArea.Levels)]
    [TestTiming]
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

            string summary = LevelFixtureSetup.SetUp(scene, withUndo: false);

            var moving = platform.GetComponent<TickedPlatform>();
            Assert.IsNotNull(moving);
            Assert.IsNotNull(platform.GetComponent<Rigidbody>());
            Assert.AreEqual(new Vector3(0f, 6f, 0f), moving.travel);
            Assert.AreEqual(5f, moving.roundTripTime);
            Assert.AreEqual(1.5f, moving.pauseAtEnds);
            Assert.AreEqual(new Vector3(0f, 30f, 0f), moving.spinDegreesPerSecond);
            Assert.AreEqual(0.25f, moving.phaseOffset);
            Assert.IsFalse(moving.easeAtEnds);
            var pushable = crate.GetComponent<PushableBlock>();
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

            string summary = LevelFixtureSetup.SetUp(scene, withUndo: false);

            var climbable = ladder.GetComponent<ClimbableLadder>();
            Assert.IsNotNull(climbable);
            Assert.IsFalse(climbable.BuildsMesh, "A level's ladder should keep the level's mesh.");
            Assert.AreSame(levelMesh, ladder.GetComponent<MeshFilter>().sharedMesh);
            Assert.AreEqual(5.5f, climbable.Height, 1e-5f);
            Assert.AreEqual(0.7f, climbable.Width, 1e-5f);
            Assert.AreEqual(0.25f, climbable.RungSpacing, 1e-5f);
            Assert.AreEqual(0.05f, climbable.RailDepth, 1e-5f);
            Assert.AreEqual(0.03f, climbable.RungSize, 1e-5f);
            Assert.IsNull(marker.GetComponent<ClimbableLadder>(), "A future ladder's marker isn't a ladder.");
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

            LevelFixtureSetup.SetUp(scene, withUndo: false);

            CourseTimer[] Timers() => scene.GetRootGameObjects().Single(root => root.name == LevelFixtureSetup.CoursesName)
                .GetComponentsInChildren<CourseTimer>();
            CourseTimer[] timers = Timers();
            Assert.AreEqual(2, timers.Length, "One timer per course, in the scene's Course Timers group.");
            // Set up again (as Set Up Scene does a built level), each course's timer is found, not made again.
            LevelFixtureSetup.SetUp(scene, withUndo: false);
            Assert.AreEqual(2, Timers().Length, "Set up again, the courses' timers should be found again, not made twice.");
            CourseTimer trap = timers.Single(t => t.CourseName == "Speed Trap");
            Assert.AreEqual(100f, trap.Distance);
            Assert.AreSame(trap, start.GetComponent<TimingGate>().Timer);
            Assert.AreEqual(TimingGateRole.Start, start.GetComponent<TimingGate>().Role);
            Assert.AreEqual(TimingGateRole.Finish, finish.GetComponent<TimingGate>().Role);
            Assert.AreEqual(TimingGateRole.StartAndFinish, lap.GetComponent<TimingGate>().Role);
            Assert.IsTrue(start.GetComponent<Collider>().isTrigger);

            Assert.AreEqual(1, trap.OnTextChanged.GetPersistentEventCount(), "The display should be wired to its course's timer.");
            // Persistent listeners run only in play mode, so check what it calls rather than calling it.
            Assert.AreSame(text, trap.OnTextChanged.GetPersistentTarget(0));
            Assert.AreEqual("set_text", trap.OnTextChanged.GetPersistentMethodName(0));
        }

        [Test]
        public void SetUp_MakesRopesZiplinesAndBouncePads_FromTheirNames_ButNotPlaceholdersNamedForThem()
        {
            GameObject rope = Add("Rope | length=9 | thickness=0.08");
            GameObject anchor = Add("Rope Anchor (+24)");
            GameObject marker = Add("Rope Swing (future): across the gap");
            GameObject line = Add("Zipline | length=30.5 | speed=12");
            line.transform.rotation = Quaternion.Euler(10f, 90f, 0f);
            var levelCable = new Mesh { name = "The Level's Cable" };
            line.AddComponent<MeshFilter>().sharedMesh = levelCable;
            GameObject pad = Add("Bounce Pad | height=7.5");
            pad.AddComponent<BoxCollider>();
            GameObject spring = Add("Spring", pad.transform);

            string summary = LevelFixtureSetup.SetUp(scene, withUndo: false);

            var swing = rope.GetComponent<SwingRope>();
            Assert.IsNotNull(swing);
            Assert.IsTrue(swing.BuildsMesh, "A rope draws itself as it swings.");
            Assert.AreEqual(9f, swing.Length, 1e-5f);
            Assert.AreEqual(0.08f, swing.Thickness, 1e-5f);
            Assert.IsNull(anchor.GetComponent<SwingRope>(), "A rope's anchor placeholder isn't a rope.");
            Assert.IsNull(marker.GetComponent<SwingRope>(), "A future rope swing's marker isn't a rope.");

            var zipline = line.GetComponent<ZiplineCable>();
            Assert.IsNotNull(zipline);
            Assert.IsFalse(zipline.BuildsMesh, "A level's zipline should keep the level's cable.");
            Assert.AreSame(levelCable, line.GetComponent<MeshFilter>().sharedMesh);
            Assert.AreEqual(30.5f, zipline.Length, 1e-3f);
            Assert.AreEqual(12f, zipline.MaxSpeed, 1e-5f);
            Assert.That(Vector3.Angle(zipline.Direction, line.transform.forward), Is.LessThan(0.01f), "The cable runs along the zipline's +Z.");

            var bounce = pad.GetComponent<LaunchPad>();
            Assert.IsNotNull(bounce);
            Assert.AreEqual(7.5f, bounce.LaunchHeight, 1e-5f);
            Assert.AreSame(spring.transform, bounce.Spring);
            StringAssert.Contains("1 rope", summary);
            StringAssert.Contains("1 zipline", summary);
            StringAssert.Contains("1 bounce pad", summary);
            Object.DestroyImmediate(levelCable);
        }

        [Test]
        public void SetUp_MakesClimbablePanelsClimbable_ButNotFutureClimbableWallMarkers()
        {
            GameObject panel = Add("Climbable Panel | speed=0.8");
            panel.AddComponent<BoxCollider>();
            GameObject marker = Add("Climbable Wall (future): the tower's west face");

            string summary = LevelFixtureSetup.SetUp(scene, withUndo: false);

            var climbable = panel.GetComponent<ClimbableSurface>();
            Assert.IsNotNull(climbable);
            Assert.AreEqual(0.8f, climbable.SpeedMultiplier, 1e-5f);
            Assert.IsNull(marker.GetComponent<ClimbableSurface>(), "A future climbable wall's marker isn't climbable.");
            StringAssert.Contains("1 climbable panel", summary);
        }

        [Test]
        public void SetUp_MarksWallRunAndWallSlideWalls_ForWhatTheirNamesSay()
        {
            GameObject run = Add("Wall-Run Wall (+8)");
            GameObject slide = Add("Wall-Slide Wall");
            GameObject plain = Add("Wall-Run Walls Ahead (a sign, not a wall)");

            string summary = LevelFixtureSetup.SetUp(scene, withUndo: false);

            Assert.That(run.GetComponent<TraversableWall>() is { Move: TraversableWall.WallMove.WallRun }, "A wall-run wall is for wall runs.");
            Assert.That(slide.GetComponent<TraversableWall>() is { Move: TraversableWall.WallMove.WallSlide }, "A wall-slide wall is for wall slides.");
            Assert.IsNull(plain.GetComponent<TraversableWall>(), "Only the names themselves (alone, or followed by a space) mark a wall.");
            StringAssert.Contains("2 walls for wall runs or slides", summary);
        }

        [Test]
        public void SetUp_MakesWaterSwimmable_KeepingTheLevelsSurface_ButNotFutureWaterMarkers()
        {
            GameObject pool = Add("Water | size=22,2.6,12 | label=Pool");
            pool.transform.SetPositionAndRotation(new Vector3(23f, -0.4f, 31f), Quaternion.Euler(0f, 30f, 0f));
            var levelSurface = new Mesh { name = "The Level's Water" };
            pool.AddComponent<MeshFilter>().sharedMesh = levelSurface;
            GameObject marker = Add("Water (future): a canal along the wall");

            string summary = LevelFixtureSetup.SetUp(scene, withUndo: false);

            var water = pool.GetComponent<SwimmableWater>();
            Assert.IsNotNull(water);
            Assert.IsFalse(water.BuildsMesh, "A level's water should keep the level's surface.");
            Assert.AreSame(levelSurface, pool.GetComponent<MeshFilter>().sharedMesh);
            Assert.That(Vector3.Distance(water.Size, new Vector3(22f, 2.6f, 12f)), Is.LessThan(1e-4f));
            Assert.AreEqual(-0.4f, water.SurfaceHeight, 1e-5f);
            Assert.IsTrue(water.Contains(new Vector3(23f, -2f, 31f)), "The water's middle, under its surface, should be in it.");
            Assert.IsFalse(water.Contains(new Vector3(23f, 0f, 31f)), "Above its surface isn't in the water.");
            Assert.IsNull(marker.GetComponent<SwimmableWater>(), "A future water marker isn't water.");
            StringAssert.Contains("1 body of water", summary);
            Object.DestroyImmediate(levelSurface);
        }
    
        [Test]
        public void SetUp_MakesCheckpointsHazardsAndLessonZones_FromTheirNames()
        {
            GameObject checkpoint = Add("Checkpoint | id=L02-jump | start=1 | size=6,3,2");
            GameObject hazard = Add("Hazard Volume (Pit) | size=10,2,8");
            GameObject zone = Add("Lesson Zone | lesson=L02-jump | size=8,4,12");
            GameObject marker = Add("Checkpoints");

            string summary = LevelFixtureSetup.SetUp(scene, withUndo: false);

            var point = checkpoint.GetComponent<Checkpoint>();
            Assert.IsNotNull(point);
            Assert.AreEqual("L02-jump", point.Id);
            Assert.IsTrue(point.IsStart);
            Assert.AreEqual(new Vector3(6f, 3f, 2f), point.Size);
            Assert.AreEqual(new Vector3(0f, 1.5f, 0f), point.Center, "A checkpoint's box should stand on its respawn point.");
            var pit = hazard.GetComponent<HazardVolume>();
            Assert.IsNotNull(pit, "A hazard named with a label should still be one.");
            Assert.AreEqual(Vector3.zero, pit.Center);
            Assert.AreEqual(new Vector3(10f, 2f, 8f), pit.Size);
            var lesson = zone.GetComponent<LessonZone>();
            Assert.IsNotNull(lesson);
            Assert.AreEqual("L02-jump", lesson.LessonId);
            Assert.IsNull(marker.GetComponent<LevelVolume>(), "A group named for checkpoints isn't one.");
            StringAssert.Contains("1 checkpoint", summary);
            StringAssert.Contains("1 hazard volume", summary);
            StringAssert.Contains("1 lesson zone", summary);
        }

        [Test]
        public void SetUp_MakesShortcutGatesAndTheirTriggers_FromTheirNames()
        {
            GameObject gate = Add("Shortcut Gate | id=lessons | travel=0,-4,0 | time=2");
            gate.AddComponent<BoxCollider>();
            GameObject trigger = Add("Shortcut Trigger | gate=lessons | size=6,3,2");

            string summary = LevelFixtureSetup.SetUp(scene, withUndo: false);

            var shortcut = gate.GetComponent<ShortcutGate>();
            Assert.IsNotNull(shortcut);
            Assert.IsNotNull(gate.GetComponent<Rigidbody>(), "A gate moves, so it needs a Rigidbody.");
            Assert.AreEqual("lessons", shortcut.Id);
            Assert.AreEqual(new Vector3(0f, -4f, 0f), shortcut.Travel);
            Assert.AreEqual(2f, shortcut.OpenTime);
            var opener = trigger.GetComponent<ShortcutTrigger>();
            Assert.IsNotNull(opener);
            Assert.AreSame(shortcut, opener.Gate);
            Assert.AreEqual(new Vector3(6f, 3f, 2f), opener.Size);
            StringAssert.Contains("1 shortcut gate with 1 trigger", summary);
        }

        [Test]
        public void AShortcutTrigger_NamingNoGate_IsReported_NotThrown()
        {
            GameObject trigger = Add("Shortcut Trigger | gate=nowhere | size=6,3,2");
            UnityEngine.TestTools.LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("names no shortcut gate"));

            Assert.DoesNotThrow(() => LevelFixtureSetup.SetUp(scene, withUndo: false));
            Assert.IsNull(trigger.GetComponent<ShortcutTrigger>());
        }

        [Test]
        public void SetUp_MakesSplitGates_AndCratesThatReset()
        {
            GameObject split = Add("Timing Gate | course=Agility | role=split | index=2");
            split.AddComponent<BoxCollider>();
            GameObject crate = Add("Pushable Crate | pull=1 | speed=1 | reset=L13-crate,L13-crate-b");
            crate.AddComponent<BoxCollider>();

            LevelFixtureSetup.SetUp(scene, withUndo: false);

            var gate = split.GetComponent<TimingGate>();
            Assert.AreEqual(TimingGateRole.Split, gate.Role);
            Assert.AreEqual(2, gate.SplitIndex);
            var reset = crate.GetComponent<ResetOnRespawn>();
            Assert.IsNotNull(reset, "A crate with reset= should reset on a respawn.");
            CollectionAssert.AreEqual(new[] { "L13-crate", "L13-crate-b" }, reset.CheckpointIds);
        }
    }
}
