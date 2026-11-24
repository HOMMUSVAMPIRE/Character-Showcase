using System.Collections;
using MeridianMotion.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MeridianMotion.Tests.UI
{
    /// <summary>
    /// Tests of tabs (TabGroup and Tab: UI_Framework_Blueprint, Tabs, and Building it, step 7): the initial tab selected as
    /// the group starts; a click selecting a tab (its look and contents) and deselecting the rest, raising the group's
    /// change; clicking the selected one deselecting it only with Allow Deselection; a tab tracking a window opening it,
    /// another tab's selection closing it, and the window's closing deselecting its tab (or, without Allow Deselection,
    /// clicking the tab again opening it again).
    /// </summary>
    [Category(TestArea.UI)]
    public class TabTests : UITestsBase
    {
        private TabGroup group;
        private Tab first, second, third;

        [UnitySetUp]
        public IEnumerator SetUpTabs()
        {
            // UnitySetUp runs before the base's SetUp: the manager isn't there yet, so the tabs are made without it.
            var obj = Track(new GameObject("Tabs", typeof(RectTransform)));
            obj.SetActive(false);
            group = obj.AddComponent<TabGroup>();
            first = TabNamed("First");
            second = TabNamed("Second");
            third = TabNamed("Third");
            group.InitialTabEnabled = second;
            yield return null;
        }

        private Tab TabNamed(string name)
        {
            var obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(group.transform, false);
            var tab = obj.AddComponent<Tab>();
            tab.ID = name;
            tab.SelectedState = new GameObject($"{name} Selected");
            tab.SelectedState.transform.SetParent(obj.transform, false);
            tab.UnselectedState = new GameObject($"{name} Unselected");
            tab.UnselectedState.transform.SetParent(obj.transform, false);
            tab.TabContents = new GameObject($"{name} Contents");
            tab.TabContents.transform.SetParent(group.transform, false);
            return tab;
        }

        private IEnumerator Start()
        {
            group.gameObject.SetActive(true);
            yield return null;
        }

        [UnityTest]
        public IEnumerator TheInitialTab_IsSelected_AndAClickSelectsATabAndDeselectsTheRest()
        {
            yield return Start();
            Assert.That(group.SelectedTab, Is.SameAs(second));
            Assert.That(second.TabContents.activeSelf && second.SelectedState.activeSelf && !second.UnselectedState.activeSelf, Is.True);
            Assert.That(first.TabContents.activeSelf || third.TabContents.activeSelf, Is.False);

            int changes = 0;
            group.SelectionChanged.AddListener(() => changes++);
            third.OnClicked();
            Assert.That(group.SelectedTab, Is.SameAs(third));
            Assert.That(third.Selected && !second.Selected, Is.True);
            Assert.That(third.TabContents.activeSelf && !second.TabContents.activeSelf, Is.True);
            Assert.That(changes, Is.EqualTo(1));

            third.OnClicked();
            Assert.That(group.SelectedTab, Is.SameAs(third), "Without Allow Deselection, clicking the selected tab keeps it.");
            Assert.That(changes, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator WithAllowDeselection_ClickingTheSelectedTabDeselectsIt()
        {
            group.AllowDeselection = true;
            yield return Start();
            second.OnClicked();
            Assert.That(group.SelectedTab, Is.Null);
            Assert.That(second.Selected || second.TabContents.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator ATabTrackingAWindow_OpensIt_AndItsClosingDeselectsTheTab()
        {
            group.AllowDeselection = true;
            group.InitialTabEnabled = null;
            RootUIPrefab template = Template("Inventory");
            first.RootUIToTrack = template.gameObject;
            yield return Start();

            first.OnClicked();
            Assert.That(manager.IsVisibleUI(template.gameObject), Is.True, "Selecting the tab should open its window.");
            manager.HandleBack();
            yield return Frames();
            Assert.That(first.Selected, Is.False, "Closed by Back, its window should deselect its tab.");

            first.OnClicked();
            Assert.That(manager.IsVisibleUI(template.gameObject), Is.True);
            second.OnClicked();
            Assert.That(manager.IsVisibleUI(template.gameObject), Is.False, "Selecting another tab should close it.");
        }

        [UnityTest]
        public IEnumerator WithoutAllowDeselection_ClickingTheTabAgain_OpensItsClosedWindowAgain()
        {
            RootUIPrefab template = Template("Map");
            second.RootUIToTrack = template.gameObject;
            yield return Start();
            Assert.That(manager.IsVisibleUI(template.gameObject), Is.True, "The initial tab should open its window.");
            manager.HandleBack();
            yield return Frames();
            Assert.That(second.Selected, Is.True, "Without Allow Deselection, its tab stays selected.");
            second.OnClicked();
            Assert.That(manager.IsVisibleUI(template.gameObject), Is.True, "Clicked again, it should open its window again.");
        }
    }
}
