using System.Collections;
using System.Collections.Generic;
using MeridianMotion.Showcase.Audio;
using MeridianMotion.Showcase.UIAudio;
using MeridianMotion.Tests;
using MeridianMotion.Tests.UI;
using MeridianMotion.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace MeridianMotion.Showcase.Tests.Audio
{
    /// <summary>
    /// Tests of the UI's sounds (UISounds, UISoundRelay), on the UI framework's own test set-up: a window opening plays
    /// its sound and gives its buttons theirs; a click plays, a greyed-out button's plays the denied sound; hovering plays
    /// once per entering; toggles, tab changes, Back and notifications play theirs; they play while the game is paused;
    /// reopening a window doesn't double its buttons' sounds; and with UI Sounds gone, or no audio service, nothing plays.
    /// </summary>
    [Category(TestArea.Audio)]
    public class UISoundTests : UITestsBase
    {
        private AudioTestKit kit;
        private AudioService service;
        private UISounds sounds;
        private UISoundSet set;
        private readonly List<SoundRequest> played = new();
        private int nextId = 500;

        private SoundEvent Sound(AudioBus bus)
        {
            SoundEvent sound = kit.Sound(nextId++, bus: bus);
            sound.MaxInstances = 16;
            return sound;
        }

        [SetUp]
        public void MakeSounds()
        {
            kit = new AudioTestKit();
            AudioBus ui = kit.Bus("UI", pausesWithGame: false);
            var obj = kit.Track(new GameObject("Test Audio"));
            obj.SetActive(false);
            service = obj.AddComponent<AudioService>();
            service.Library = kit.Library;
            obj.SetActive(true);
            played.Clear();
            service.Played += played.Add;

            set = kit.Track(ScriptableObject.CreateInstance<UISoundSet>());
            set.Hover = Sound(ui);
            set.Click = Sound(ui);
            set.Denied = Sound(ui);
            set.ToggleOn = Sound(ui);
            set.ToggleOff = Sound(ui);
            set.Slider = Sound(ui);
            set.Tab = Sound(ui);
            set.WindowOpen = Sound(ui);
            set.WindowClose = Sound(ui);
            set.Back = Sound(ui);
            set.Notification = Sound(ui);
            var soundsObj = kit.Track(new GameObject("UI Sounds"));
            soundsObj.SetActive(false);
            sounds = soundsObj.AddComponent<UISounds>();
            sounds.Sounds = set;
            soundsObj.SetActive(true);
        }

        [TearDown]
        public void RemoveSounds()
        {
            Time.timeScale = 1f;
            kit.Dispose();
        }

        private int Count(SoundEvent sound)
        {
            int count = 0;
            foreach (SoundRequest request in played) { if (request.sound == sound.Id) { count++; } }
            return count;
        }

        /// <summary>A window template with a button (and a toggle, if asked).</summary>
        private RootUIPrefab WindowWithButton(bool withToggle = false)
        {
            RootUIPrefab template = Template("Window");
            var button = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button));
            button.transform.SetParent(template.transform, false);
            if (withToggle)
            {
                var toggle = new GameObject("Toggle", typeof(RectTransform), typeof(Image), typeof(Toggle));
                toggle.transform.SetParent(template.transform, false);
            }
            return template;
        }

        private static void Click(GameObject target) =>
            ExecuteEvents.Execute(target, new PointerEventData(EventSystem.current), ExecuteEvents.pointerClickHandler);

        [Test]
        public void OpeningAWindow_PlaysOpen_AndAddsRelaysToItsButtons()
        {
            RootUIPrefab window = Open(WindowWithButton());
            Assert.That(Count(set.WindowOpen), Is.EqualTo(1));
            Assert.That(window.GetComponentInChildren<Button>().GetComponent<UISoundRelay>(), Is.Not.Null);
        }

        [Test]
        public void ClickingAButton_PlaysClick_AGreyedOutOne_PlaysDenied()
        {
            RootUIPrefab window = Open(WindowWithButton());
            Button button = window.GetComponentInChildren<Button>();
            Click(button.gameObject);
            Assert.That(Count(set.Click), Is.EqualTo(1));
            button.interactable = false;
            Click(button.gameObject);
            Assert.That(Count(set.Denied), Is.EqualTo(1));
            Assert.That(Count(set.Click), Is.EqualTo(1));
        }

        [Test]
        public void HoveringPlaysHover_OncePerEnter()
        {
            RootUIPrefab window = Open(WindowWithButton());
            GameObject button = window.GetComponentInChildren<Button>().gameObject;
            var pointer = new PointerEventData(EventSystem.current);
            ExecuteEvents.Execute(button, pointer, ExecuteEvents.pointerEnterHandler);
            Assert.That(Count(set.Hover), Is.EqualTo(1));
            // Clicking selects it: that isn't navigation, so no second hover.
            ExecuteEvents.Execute(button, pointer, ExecuteEvents.selectHandler);
            Assert.That(Count(set.Hover), Is.EqualTo(1));
            // Navigating to it is.
            ExecuteEvents.Execute(button, new BaseEventData(EventSystem.current), ExecuteEvents.selectHandler);
            Assert.That(Count(set.Hover), Is.EqualTo(2));
        }

        [Test]
        public void TogglingPlaysOnThenOff()
        {
            RootUIPrefab window = Open(WindowWithButton(withToggle: true));
            Toggle toggle = window.GetComponentInChildren<Toggle>();
            toggle.isOn = false;
            played.Clear();
            Click(toggle.gameObject);
            Assert.That(toggle.isOn, Is.True);
            Click(toggle.gameObject);
            Assert.That(Count(set.ToggleOn), Is.EqualTo(1));
            Assert.That(Count(set.ToggleOff), Is.EqualTo(1));
        }

        [Test]
        public void ATabChange_PlaysTab()
        {
            RootUIPrefab template = Template("Tabbed");
            var tabs = new GameObject("Tabs", typeof(RectTransform), typeof(TabGroup));
            tabs.transform.SetParent(template.transform, false);
            RootUIPrefab window = Open(template);
            window.GetComponentInChildren<TabGroup>().SelectionChanged.Invoke();
            Assert.That(Count(set.Tab), Is.EqualTo(1));
        }

        [Test]
        public void Back_PlaysBack()
        {
            Open(WindowWithButton());
            manager.HandleBack();
            Assert.That(Count(set.Back), Is.EqualTo(1));
        }

        [Test]
        public void ANotification_PlaysNotification()
        {
            var obj = new GameObject("Notifications", typeof(RectTransform));
            obj.transform.SetParent(manager.overlayCanvas, false);
            Notifications notifications = obj.AddComponent<Notifications>();
            var entry = Track(new GameObject("Passive", typeof(RectTransform)));
            entry.SetActive(false);
            notifications.SetPrefab(Notifications.Type.Passive, entry.AddComponent<NotificationEntry>());
            notifications.ShowNotification("Saved", "", 4f);
            Assert.That(Count(set.Notification), Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator UISounds_PlayWhileTheGameIsPaused()
        {
            Time.timeScale = 0f;
            RootUIPrefab window = Open(WindowWithButton());
            Click(window.GetComponentInChildren<Button>().gameObject);
            yield return null;
            Assert.That(Count(set.WindowOpen), Is.EqualTo(1));
            Assert.That(Count(set.Click), Is.EqualTo(1), "the UI's sounds don't wait for the game (their bus plays on: AudioServiceTests)");
        }

        [UnityTest]
        public IEnumerator ReopeningAWindow_DoesNotDoubleTheRelays()
        {
            RootUIPrefab window = Open(WindowWithButton());
            manager.RestorePreviouslyShownUI(manager.HideAllUI());
            yield return null;
            Button button = window.GetComponentInChildren<Button>();
            Assert.That(button.GetComponents<UISoundRelay>().Length, Is.EqualTo(1));
            played.Clear();
            Click(button.gameObject);
            Assert.That(Count(set.Click), Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator AfterUISoundsIsDestroyed_NothingIsListening()
        {
            RootUIPrefab template = WindowWithButton();
            Object.Destroy(sounds.gameObject);
            yield return null;
            RootUIPrefab window = Open(template);
            Click(window.GetComponentInChildren<Button>().gameObject);
            manager.HandleBack();
            Assert.That(played, Is.Empty);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator WithNoService_ItDoesNothing()
        {
            Object.Destroy(service.gameObject);
            yield return null;
            RootUIPrefab window = Open(WindowWithButton());
            Click(window.GetComponentInChildren<Button>().gameObject);
            manager.HandleBack();
            LogAssert.NoUnexpectedReceived();
        }
    
        [Test]
        public void AWindowThatDoesntNeedTheCursor_PlaysNoWindowSound()
        {
            // A name tag, a HUD prompt: windows to the UI framework, not to the player.
            RootUIPrefab template = Template("Label");
            template.NeedsCursor = false;
            RootUIPrefab label = Open(template);
            label.Close();
            Assert.That(Count(set.WindowOpen) + Count(set.WindowClose), Is.Zero);
        }

        [Test]
        public void AToggleSetByCode_PlaysNothing()
        {
            RootUIPrefab window = Open(WindowWithButton(withToggle: true));
            Toggle toggle = window.GetComponentInChildren<Toggle>();
            played.Clear();
            toggle.isOn = !toggle.isOn;
            toggle.isOn = !toggle.isOn;
            Assert.That(Count(set.ToggleOn) + Count(set.ToggleOff), Is.Zero);
        }

        [UnityTest]
        public IEnumerator AControlMadeAfterTheWindowOpened_StillHasItsSounds()
        {
            RootUIPrefab window = Open(Template("Window"));
            // As a list fills its rows in Start, after the window's opened.
            var button = new GameObject("Late Button", typeof(RectTransform), typeof(Image), typeof(Button));
            button.transform.SetParent(window.transform, false);
            yield return null;
            yield return null;
            Click(button);
            Assert.That(Count(set.Click), Is.EqualTo(1));
        }
}
}
