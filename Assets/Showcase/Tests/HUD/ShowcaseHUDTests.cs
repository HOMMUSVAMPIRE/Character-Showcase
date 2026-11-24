using System.Collections;
using System.Linq;
using MeridianMotion.Levels;
using MeridianMotion.Showcase.HUD;
using MeridianMotion.Tests;
using MeridianMotion.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace MeridianMotion.Showcase.Tests.HUD
{
    /// <summary>
    /// Tests of the showcase's HUD (ShowcaseHUD, NpcLabel: ReferenceAssets/OpenTasks.md, G12), on a UI manager and
    /// notifications made in code: a course timer's finished lap shown as a notification (a new best as a banner across
    /// the middle, with its text), and a split with how far ahead of or behind the best run it is; the controls hint
    /// shown once however often the character spawns; and an NPC's name tag
    /// opened over it on the in-world canvas, showing its name, and closed when it goes.
    /// </summary>
    [Category(TestArea.UI)]
    [TestTiming]
    public class ShowcaseHUDTests
    {
        private GameObject root;
        private UIManager manager;
        private Notifications notifications;
        private ShowcaseHUD hud;
        private Transform topMiddle, center;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("UI Manager");
            root.SetActive(false);
            manager = root.AddComponent<UIManager>();
            manager.inWorldCanvas = Canvas("In World Canvas", 0);
            manager.defaultCanvas = Canvas("Default Canvas", 10);
            manager.overlayCanvas = Canvas("Overlay Canvas", 20);
            new GameObject("EventSystem", typeof(EventSystem)).transform.SetParent(root.transform, false);
            root.SetActive(true);

            var notificationsObject = new GameObject("Notifications", typeof(RectTransform));
            notificationsObject.transform.SetParent(manager.overlayCanvas, false);
            notifications = notificationsObject.AddComponent<Notifications>();
            topMiddle = Child(notificationsObject.transform, "Top Middle");
            center = Child(notificationsObject.transform, "Center");
            notifications.SetParent(Notifications.Location.Top_Middle, topMiddle);
            notifications.SetParent(Notifications.Location.Center, center);
            notifications.SetPrefab(Notifications.Type.Passive, Template<NotificationEntry>("Toast"));
            notifications.SetPrefab(Notifications.Type.Center, Template<NotificationEntry>("Banner"));

            hud = new GameObject("Showcase HUD").AddComponent<ShowcaseHUD>();
            hud.transform.SetParent(root.transform, false);
            NpcLabel label = Template<NpcLabel>("NPC Label");
            label.ParentCanvas = UICanvas.InWorld;
            label.AllowMultiple = true;
            label.CloseOnCancelButton = false;
            label.NeedsCursor = false;
            hud.NpcLabelPrefab = label.gameObject;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.Destroy(root);
            yield return null;
        }

        private Transform Canvas(string name, int order)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            obj.transform.SetParent(root.transform, false);
            obj.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            obj.GetComponent<Canvas>().sortingOrder = order;
            return obj.transform;
        }

        private static Transform Child(Transform parent, string name)
        {
            var obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            return obj.transform;
        }

        // An inactive template under the test's root (so it goes with it), copied as a prefab would be.
        private T Template<T>(string name) where T : Component
        {
            var obj = new GameObject(name, typeof(RectTransform));
            obj.SetActive(false);
            obj.transform.SetParent(root.transform, false);
            return obj.AddComponent<T>();
        }

        [UnityTest]
        public IEnumerator ASplit_IsShown_WithHowFarFromTheBestRunItIs()
        {
            var timerObject = new GameObject("Course Timer");
            timerObject.transform.SetParent(root.transform, false);
            var timer = timerObject.AddComponent<CourseTimer>();
            timer.CourseName = "Agility";
            hud.Listen(timer);
            yield return null;

            hud.SplitPassed(timer, 0, 12.5f, 0f);
            NotificationEntry first = notifications.Shown.Last();
            StringAssert.StartsWith("Agility", first.Title);
            StringAssert.Contains("1", first.Title, "Split 0 is shown as the first.");
            Assert.That(first.Message, Is.EqualTo("0:12.50"), "With no best run, a split shows its time alone.");

            hud.SplitPassed(timer, 1, 20f, 21.25f);
            StringAssert.Contains("-1.25", notifications.Shown.Last().Message, "Ahead of the best run, the split shows by how much.");
        }

        [UnityTest]
        public IEnumerator AFinishedLap_IsShown_AndANewBestAsABanner()
        {
            var timerObject = new GameObject("Course Timer");
            timerObject.transform.SetParent(root.transform, false);
            var timer = timerObject.AddComponent<CourseTimer>();
            timer.CourseName = "Sprint Lane";
            // A timer made after the HUD started (as here) is handed to it; one in the scene as it starts, it finds itself.
            hud.Listen(timer);
            hud.Listen(timer);
            yield return null;

            timer.OnLapFinished.Invoke(62.5f, false);
            Assert.That(notifications.Shown.Count, Is.EqualTo(1), "Listened to twice, a lap should still show once.");
            NotificationEntry toast = notifications.Shown.Last();
            Assert.That(toast.transform.parent, Is.SameAs(topMiddle));
            Assert.That(toast.Title, Is.EqualTo("Sprint Lane"));
            Assert.That(toast.Message, Is.EqualTo("1:02.50"));

            timer.OnLapFinished.Invoke(59.8f, true);
            NotificationEntry banner = notifications.Shown.Last();
            Assert.That(banner.NotificationType, Is.EqualTo(Notifications.Type.Center), "A new best should be a banner across the middle.");
            Assert.That(banner.transform.parent, Is.SameAs(center));
            StringAssert.Contains(hud.NewBestText, banner.Message);
        }

        [UnityTest]
        public IEnumerator TheControlsHint_IsShownOnce()
        {
            hud.ShowControlsHint(null);
            hud.ShowControlsHint(null);
            Assert.That(notifications.Shown.Count(n => n.Title == hud.ControlsHintTitle), Is.EqualTo(1), "Spawned again, the hint shouldn't show again.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator AnNpc_GetsANameTagOverIt_ClosedWhenItGoes()
        {
            var cameraObject = new GameObject("Camera");
            cameraObject.transform.SetParent(root.transform, false);
            cameraObject.AddComponent<Camera>().tag = "MainCamera";
            var npc = new GameObject("NPC").transform;
            npc.position = new Vector3(0f, 0f, 10f);
            hud.AddNpcLabel(npc, "Courier");
            NpcLabel label = manager.inWorldCanvas.GetComponentInChildren<NpcLabel>();
            Assert.That(label, Is.Not.Null, "The NPC should get a name tag, on the in-world canvas.");
            Assert.That(label.Name, Is.EqualTo("Courier"));
            Assert.That(label.FollowTransform, Is.SameAs(npc));

            Object.Destroy(npc.gameObject);
            for (int i = 0; i < 3; i++) { yield return null; }
            Assert.That(label == null, Is.True, "Its NPC gone, the tag should close.");
        }
    }
}
