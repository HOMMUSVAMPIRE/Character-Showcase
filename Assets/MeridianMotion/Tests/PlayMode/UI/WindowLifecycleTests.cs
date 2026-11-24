using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MeridianMotion.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;

namespace MeridianMotion.Tests.UI
{
    /// <summary>
    /// Tests of a window's life (UI_Framework_Blueprint, Window lifecycle, and Building it, step 3): its events in order,
    /// once each; Close() a second time doing nothing; a window with an Animator playing its close (in unscaled time, so
    /// while the game is paused too) before it's destroyed, its animation event ending it at once, and a close that never
    /// plays through ending all the same; its click blocker opened behind it on its canvas, stopping clicks reaching what's
    /// behind, and closed with it; and hiding and showing it again keeping its state, its stacking and its blocker.
    /// </summary>
    [Category(TestArea.UI)]
    public class WindowLifecycleTests : UITestsBase
    {
        /// <summary>A window with something of its own to keep (its state, across hiding).</summary>
        private class CountingWindow : RootUIPrefab
        {
            public int value;
        }

        [UnityTest]
        public IEnumerator ItsEvents_ComeInOrder_OnceEach_AndASecondCloseDoesNothing()
        {
            var raised = new List<string>();
            RootUIPrefab template = Template("Window");
            // Subscribed on the instance: OnOpened is raised as it opens, so the open is counted through the manager's map.
            RootUIPrefab window = Open(template);
            Assert.That(window.isActiveAndEnabled, Is.True);
            window.OnOpened += _ => raised.Add("opened");
            window.OnClosing += _ => raised.Add("closing");
            window.OnClosed += _ => raised.Add("closed");
            window.Close();
            window.Close();
            Assert.That(window.IsClosing, Is.True);
            yield return Frames();
            Assert.That(raised, Is.EqualTo(new[] { "closing", "closed" }), "Closing should raise OnClosing then OnClosed, once each.");
            Assert.That(Gone(window), Is.True);
        }

        [UnityTest]
        public IEnumerator OnOpened_IsRaisedAsItOpens_AndAgainWhenShownAfterHiding()
        {
            RootUIPrefab template = Template("Window");
            int opened = 0;
            RootUIPrefab window = Open(template);
            window.OnOpened += _ => opened++;
            UIManager.HiddenUIState hidden = manager.HideAllUI();
            manager.RestorePreviouslyShownUI(hidden);
            yield return null;
            Assert.That(opened, Is.EqualTo(1), "Shown again after hiding, it should raise OnOpened again.");
        }

#if UNITY_EDITOR
        [UnityTest]
        public IEnumerator AWindowWithAnAnimator_PlaysItsCloseBeforeItsDestroyed_EvenWithTheGamePaused()
        {
            const float closeSeconds = 0.3f;
            RootUIPrefab window = Open(AnimatedTemplate(CloseController(closeSeconds)));
            bool closedRaised = false;
            window.OnClosed += _ => closedRaised = true;
            yield return Frames();
            Time.timeScale = 0f;
            float started = Time.realtimeSinceStartup;
            window.Close();
            yield return Frames();
            Assert.That(Gone(window), Is.False, "It should still be there while its close plays.");
            Assert.That(closedRaised, Is.False);
            while (!Gone(window) && Time.realtimeSinceStartup - started < 3f) { yield return null; }
            Assert.That(Gone(window), Is.True, "Once its close has played (in unscaled time: the game is paused), it should be destroyed.");
            Assert.That(closedRaised, Is.True);
            Assert.That(Time.realtimeSinceStartup - started, Is.GreaterThanOrEqualTo(closeSeconds * 0.9f), "It should have waited for the close to play.");
        }

        [UnityTest]
        public IEnumerator ItsCloseAnimationsEvent_EndsTheCloseAtOnce()
        {
            RootUIPrefab window = Open(AnimatedTemplate(CloseController(10f)));
            yield return Frames();
            window.Close();
            yield return null;
            Assert.That(Gone(window), Is.False);
            window.Animator_OnCloseComplete();
            yield return Frames();
            Assert.That(Gone(window), Is.True);
        }

        [UnityTest]
        public IEnumerator ACloseThatNeverPlaysThrough_EndsAllTheSame()
        {
            RootUIPrefab window = Open(AnimatedTemplate(CloseController(1f, closeState: false)));
            yield return Frames();
            window.Close();
            yield return Frames();
            Assert.That(Gone(window), Is.False, "Test setup: the trigger should be waited on.");
            Now += 60f; // Long past any close (the UI's own clock).
            yield return Frames();
            Assert.That(Gone(window), Is.True, "A close with no state tagged Close should still end.");
        }
#endif

        [UnityTest]
        public IEnumerator AClickBlocker_OpensBehindItOnItsCanvas_AndClosesWithIt()
        {
            Blocker(ClickBlockStyle.DimBackground);
            RootUIPrefab template = Template("Window", new Vector2(200f, 100f));
            template.ClickBlockerStyle = ClickBlockStyle.DimBackground;
            template.ParentCanvas = UICanvas.Overlay;
            RootUIPrefab window = Open(template);
            RootUIPrefab blocker = window.BlockerPrefab;
            Assert.That(blocker, Is.Not.Null, "Its blocker should open with it.");
            Assert.That(blocker.IsBlocker, Is.True);
            Assert.That(blocker.transform.parent, Is.SameAs(window.transform.parent), "On its canvas.");
            Assert.That(blocker.transform.GetSiblingIndex(), Is.EqualTo(window.transform.GetSiblingIndex() - 1), "Just behind it.");
            Assert.That(UIManager.ClickBlockerCount, Is.EqualTo(1));

            Open(template);
            Assert.That(window.BlockerPrefab, Is.SameAs(blocker), "Brought to the front, it should keep its one blocker, just behind it.");
            Assert.That(blocker.transform.GetSiblingIndex(), Is.EqualTo(window.transform.GetSiblingIndex() - 1));

            window.Close();
            yield return Frames();
            Assert.That(Gone(blocker), Is.True, "Its blocker should close with it.");
            Assert.That(UIManager.ClickBlockerCount, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator TheBlocker_StopsClicksReachingWhatsBehindIt()
        {
            Blocker(ClickBlockStyle.Invisible);
            RootUIPrefab behind = Open(Template("Behind"));
            RootUIPrefab template = Template("Small Window", new Vector2(100f, 100f));
            template.ClickBlockerStyle = ClickBlockStyle.Invisible;
            RootUIPrefab window = Open(template);
            window.GetComponent<RectTransform>().anchoredPosition = new Vector2(-Screen.width, -Screen.height); // Off the middle.
            yield return Frames();

            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = new Vector2(Screen.width, Screen.height) / 2f }, hits);
            Assert.That(hits, Is.Not.Empty, "Test setup: a click in the middle should land on something.");
            Assert.That(hits[0].gameObject, Is.SameAs(window.BlockerPrefab.gameObject), "The click should land on the blocker, not what's behind.");
            Assert.That(hits.Any(h => h.gameObject == behind.gameObject), Is.True, "Test setup: what's behind should be under it.");
        }

        [UnityTest]
        public IEnumerator HidingAndShowingAgain_KeepsItsStateItsStackingAndItsBlocker()
        {
            Blocker(ClickBlockStyle.DimBackground);
            CountingWindow back = Open<CountingWindow>(Template<CountingWindow>("Back"));
            CountingWindow frontTemplate = Template<CountingWindow>("Front");
            frontTemplate.ClickBlockerStyle = ClickBlockStyle.DimBackground;
            CountingWindow front = Open<CountingWindow>(frontTemplate);
            front.value = 7;

            UIManager.HiddenUIState hidden = manager.HideAllUI();
            yield return null;
            Assert.That(front.gameObject.activeSelf, Is.False);
            Assert.That(back.gameObject.activeSelf, Is.False);
            Assert.That(UIManager.ClickBlockerCount, Is.EqualTo(0), "Hidden, its blocker should close.");

            manager.RestorePreviouslyShownUI(hidden);
            yield return null;
            Assert.That(front.value, Is.EqualTo(7), "Shown again, it should keep its state.");
            Assert.That(front.transform.GetSiblingIndex(), Is.GreaterThan(back.transform.GetSiblingIndex()), "And be stacked as it was.");
            Assert.That(front.BlockerPrefab, Is.Not.Null, "And have its blocker again.");
            Assert.That(UIManager.ClickBlockerCount, Is.EqualTo(1));
        }
    }
}
