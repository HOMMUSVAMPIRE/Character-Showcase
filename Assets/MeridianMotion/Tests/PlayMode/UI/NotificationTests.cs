using System.Collections;
using System.Linq;
using MeridianMotion.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MeridianMotion.Tests.UI
{
    /// <summary>
    /// Tests of notifications (Notifications and NotificationEntry: UI_Framework_Blueprint, Notifications, and Building it,
    /// step 7): one shown at its place in its style, through the UIManager too; gone once its time's up, on the UI's clock
    /// (so with the game paused too); at most the cap of a style at once, the oldest closed; the passive ones cleared,
    /// others left; and Read More showing all its message and keeping it.
    /// </summary>
    [Category(TestArea.UI)]
    public class NotificationTests : UITestsBase
    {
        private Notifications notifications;
        private Transform topMiddle, center;

        [SetUp]
        public void SetUpNotifications()
        {
            var obj = new GameObject("Notifications", typeof(RectTransform));
            obj.transform.SetParent(manager.overlayCanvas, false);
            notifications = obj.AddComponent<Notifications>();
            topMiddle = new GameObject("Top Middle", typeof(RectTransform)).transform;
            topMiddle.SetParent(obj.transform, false);
            center = new GameObject("Center", typeof(RectTransform)).transform;
            center.SetParent(obj.transform, false);
            notifications.SetParent(Notifications.Location.Top_Middle, topMiddle);
            notifications.SetParent(Notifications.Location.Center, center);
            notifications.SetPrefab(Notifications.Type.Passive, Entry("Passive"));
            notifications.SetPrefab(Notifications.Type.Center, Entry("Center"));
            notifications.MaxNotifCount = 2;
        }

        private NotificationEntry Entry(string name)
        {
            var obj = Track(new GameObject(name, typeof(RectTransform)));
            obj.SetActive(false);
            return obj.AddComponent<NotificationEntry>();
        }

        [UnityTest]
        public IEnumerator ANotification_ShowsAtItsPlace_UntilItsTimesUp_EvenWithTheGamePaused()
        {
            Time.timeScale = 0f;
            NotificationEntry entry = manager.ShowNotification("Saved", "Progress saved.", 4f);
            Assert.That(entry, Is.Not.Null, "The UIManager should pass it on.");
            Assert.That(entry.transform.parent, Is.SameAs(topMiddle));
            Assert.That((entry.Title, entry.Message, entry.NotificationType), Is.EqualTo(("Saved", "Progress saved.", Notifications.Type.Passive)));
            Now += 3f;
            yield return null;
            Assert.That(entry.IsClosing, Is.False);
            Now += 2f;
            yield return Frames();
            Assert.That(Gone(entry), Is.True, "Its time up, it should close, on the UI's clock.");
            Assert.That(notifications.Shown, Is.Empty);
        }

        [UnityTest]
        public IEnumerator PastTheCapOfAStyle_TheOldestCloses()
        {
            NotificationEntry first = notifications.ShowNotification("1", "", 10f);
            NotificationEntry banner = notifications.ShowNotification("Lap!", "", 10f, Notifications.Location.Center, Notifications.Type.Center);
            notifications.ShowNotification("2", "", 10f);
            Assert.That(first.IsClosing, Is.False);
            notifications.ShowNotification("3", "", 10f);
            Assert.That(first.IsClosing, Is.True, "A third of the style (the cap is two) should close the oldest.");
            Assert.That(banner.IsClosing, Is.False, "Another style shouldn't count.");
            Assert.That(banner.transform.parent, Is.SameAs(center));
            yield return null;
        }

        [UnityTest]
        public IEnumerator ClearAllPassiveNotifications_LeavesTheOthers()
        {
            NotificationEntry toast = notifications.ShowNotification("Toast", "", 10f);
            NotificationEntry banner = notifications.ShowNotification("Banner", "", 10f, Notifications.Location.Center, Notifications.Type.Center);
            notifications.ClearAllPassiveNotifications();
            Assert.That(toast.IsClosing, Is.True);
            Assert.That(banner.IsClosing, Is.False);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ReadMore_ShowsAllItsMessage_AndKeepsIt()
        {
            NotificationEntry entry = notifications.ShowNotification("Lap", "1:02.3\nBest: 0:59.8", 4f);
            Assert.That(entry.Expanded, Is.False, "A message of several lines should show its first.");
            entry.OnReadMoreClicked();
            Assert.That(entry.Expanded, Is.True);
            Now += 60f;
            yield return null;
            Assert.That(entry.IsClosing, Is.False, "Read, it should stay until it's closed.");
            entry.Close();
            yield return Frames();
            Assert.That(notifications.Shown.Count(), Is.EqualTo(0));
        }
    }
}
