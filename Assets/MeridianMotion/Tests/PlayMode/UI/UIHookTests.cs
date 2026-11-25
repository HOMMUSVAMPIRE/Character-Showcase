using System.Collections;
using System.Collections.Generic;
using MeridianMotion.UI;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;

namespace MeridianMotion.Tests.UI
{
    /// <summary>
    /// Tests of the UI's generic hooks, for anything that reacts to the UI as a whole (sound, say) without knowing its
    /// windows: RootUIPrefab.AnyOpened and AnyClosed are raised with each window, once each, and AnyOpened again as a
    /// hidden window's shown; UIManager.BackPressed as the Back key goes to a window, not while a text field has the
    /// keyboard; and Notifications.AnyShown with each notification shown.
    /// </summary>
    [Category(TestArea.UI)]
    public class UIHookTests : UITestsBase
    {
        private readonly List<RootUIPrefab> opened = new(), closed = new();
        private int backs;

        private void Opened(RootUIPrefab window) => opened.Add(window);
        private void Closed(RootUIPrefab window) => closed.Add(window);
        private void Back() => backs++;

        [SetUp]
        public void Listen()
        {
            opened.Clear();
            closed.Clear();
            backs = 0;
            RootUIPrefab.AnyOpened += Opened;
            RootUIPrefab.AnyClosed += Closed;
            UIManager.BackPressed += Back;
        }

        [TearDown]
        public void StopListening()
        {
            RootUIPrefab.AnyOpened -= Opened;
            RootUIPrefab.AnyClosed -= Closed;
            UIManager.BackPressed -= Back;
        }

        [UnityTest]
        public IEnumerator AnyOpened_AndAnyClosed_AreRaisedWithTheWindow_OnceEach()
        {
            RootUIPrefab window = Open(Template("Window"));
            Assert.That(opened, Is.EqualTo(new[] { window }));
            window.Close();
            yield return Frames();
            Assert.That(closed, Is.EqualTo(new[] { window }));
            Assert.That(opened.Count, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator AnyOpened_IsRaisedAgain_WhenAHiddenWindowIsShown()
        {
            RootUIPrefab window = Open(Template("Window"));
            manager.RestorePreviouslyShownUI(manager.HideAllUI());
            yield return null;
            Assert.That(opened, Is.EqualTo(new[] { window, window }));
        }

        [UnityTest]
        public IEnumerator BackPressed_IsRaisedWhenBackGoesToAWindow_NotWhileATextFieldHasFocus()
        {
            RootUIPrefab window = Open(Template("Window"));
            manager.HandleBack();
            Assert.That(backs, Is.EqualTo(1));
            yield return Frames();

            RootUIPrefab form = Open(Template("Form"));
            var fieldObject = new GameObject("Field", typeof(RectTransform));
            fieldObject.transform.SetParent(form.transform, false);
            var textObject = new GameObject("Text", typeof(RectTransform));
            textObject.transform.SetParent(fieldObject.transform, false);
            var field = fieldObject.AddComponent<TMP_InputField>();
            field.textViewport = (RectTransform)fieldObject.transform;
            field.textComponent = textObject.AddComponent<TextMeshProUGUI>();
            EventSystem.current.SetSelectedGameObject(fieldObject);
            field.ActivateInputField();
            for (int i = 0; i < 5 && !field.isFocused; i++) { yield return null; }
            Assume.That(field.isFocused, Is.True, "Test setup: the field couldn't take the keyboard here.");
            manager.HandleBack();
            Assert.That(backs, Is.EqualTo(1), "the text field kept the key");
        }

        [Test]
        public void AnyShown_IsRaisedWithTheEntry()
        {
            var obj = new GameObject("Notifications", typeof(RectTransform));
            obj.transform.SetParent(manager.overlayCanvas, false);
            Notifications notifications = obj.AddComponent<Notifications>();
            var place = new GameObject("Top Middle", typeof(RectTransform)).transform;
            place.SetParent(obj.transform, false);
            notifications.SetParent(Notifications.Location.Top_Middle, place);
            var entryObj = Track(new GameObject("Passive", typeof(RectTransform)));
            entryObj.SetActive(false);
            notifications.SetPrefab(Notifications.Type.Passive, entryObj.AddComponent<NotificationEntry>());

            var shown = new List<NotificationEntry>();
            void Heard(NotificationEntry entry) => shown.Add(entry);
            Notifications.AnyShown += Heard;
            try
            {
                NotificationEntry entry = notifications.ShowNotification("Saved", "", 4f);
                Assert.That(shown, Is.EqualTo(new[] { entry }));
            }
            finally
            {
                Notifications.AnyShown -= Heard;
            }
        }
    }
}
