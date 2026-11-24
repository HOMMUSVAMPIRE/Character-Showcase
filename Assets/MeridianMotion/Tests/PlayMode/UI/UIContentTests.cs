using System.Collections;
using System.Linq;
using MeridianMotion.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem.UI;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace MeridianMotion.Tests.UI
{
    /// <summary>
    /// Tests of the UI's own prefabs (Assets/MeridianMotion/Content/UI, made by Create UI Assets): the UI Manager prefab has
    /// its canvases, an EventSystem reading the UI action map, and every dialog and blocker; and each dialog, opened through
    /// its helper, comes with its blocker and runs its callback from its own buttons, so a field or a button left unwired
    /// shows here. Skipped where the prefab isn't (a project without the package's content).
    /// </summary>
    [Category(TestArea.UI)]
    [TestTiming]
    public class UIContentTests
    {
        private const string ManagerGuid = "c7a565a23b671483b87cbdcbf55b7a3b";
        private GameObject managerObject;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            GameObject prefab = null;
#if UNITY_EDITOR
            prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(UnityEditor.AssetDatabase.GUIDToAssetPath(ManagerGuid));
#endif
            if (prefab == null) { Assert.Ignore("There's no UI Manager prefab (the package's UI content) here."); }
            managerObject = Object.Instantiate(prefab);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (managerObject != null) { Object.Destroy(managerObject); }
            Time.timeScale = 1f;
            yield return null;
        }

        [Test]
        public void TheUIManager_HasItsCanvasesItsInputAndEveryDialog()
        {
            UIManager manager = UIManager.instance;
            Assert.That(manager, Is.Not.Null);
            Assert.That(new[] { manager.inWorldCanvas, manager.defaultCanvas, manager.overlayCanvas }.All(c => c != null && c.GetComponent<Canvas>() != null), Is.True);
            Assert.That(new[] { manager.MessageBoxPrefab, manager.ErrorMessageBoxPrefab, manager.YesNoDialogPrefab, manager.PromptForStringPrefab }.All(p => p != null), Is.True);
            Assert.That(new[] { ClickBlockStyle.DimBackground, ClickBlockStyle.Invisible, ClickBlockStyle.DarkenBackground, ClickBlockStyle.BlackoutBackground }
                .All(style => manager.ClickBlockerPrefab(style) != null), Is.True);
            Assert.That(manager.WindowCatalog, Is.Not.Null);
            Assert.That(manager.Theme, Is.Not.Null, "The UI Manager should have the package's theme.");
            Assert.That(manager.Theme.DisplayFont, Is.Not.Null, "The theme should have its display font (UIFonts).");
            Assert.That(manager.Theme.BodyFont, Is.Not.Null, "The theme should have its body font (UIFonts).");
            Assert.That(manager.Theme.DisplayFont.HasCharacters("0123456789:.-/ ABCZabcz", out _), Is.True, "The display font should have its numerals and letters.");
            var module = managerObject.GetComponentInChildren<InputSystemUIInputModule>();
            Assert.That(module, Is.Not.Null);
            Assert.That(module.cancel?.action?.name, Is.EqualTo("Cancel"), "The EventSystem should read the UI action map.");
            Assert.That(module.point?.action?.name, Is.EqualTo("Point"));
        }

        private static Button ButtonLabelled(Component dialog, string label) =>
            dialog.GetComponentsInChildren<Button>().First(b => b.name.StartsWith(label));

        [UnityTest]
        public IEnumerator EachDialog_OpensWithItsBlocker_AndItsButtonsRunItsCallbacks()
        {
            int okayed = 0, yes = 0, no = 0;
            string typed = null;

            UIMessageBox box = UIMessageBox.Open("Saved.", () => okayed++);
            Assert.That(UIManager.ClickBlockerCount, Is.EqualTo(1), "The dialog should bring its blocker.");
            ButtonLabelled(box, "OK").onClick.Invoke();
            Assert.That(okayed, Is.EqualTo(1));
            yield return null;

            ButtonLabelled(UIMessageBox.OpenError("Couldn't save.", () => okayed++), "OK").onClick.Invoke();
            Assert.That(okayed, Is.EqualTo(2));
            yield return null;

            UIMessageBox yesNo = UIMessageBox.OpenYesNoDialog("Discard?", () => yes++, () => no++);
            ButtonLabelled(yesNo, "No").onClick.Invoke();
            yield return null;
            ButtonLabelled(UIMessageBox.OpenYesNoDialog("Discard?", () => yes++, () => no++), "Yes").onClick.Invoke();
            Assert.That((yes, no), Is.EqualTo((1, 1)));
            yield return null;

            UIMessageBox_PromptForString prompt = UIMessageBox_PromptForString.Open("Name it", s => typed = s);
            Assert.That(prompt.InputField, Is.Not.Null, "The prompt should have its field.");
            prompt.Value = "Fast";
            ButtonLabelled(prompt, "OK").onClick.Invoke();
            Assert.That(typed, Is.EqualTo("Fast"));
            yield return null;
            yield return null;
            Assert.That(UIManager.ClickBlockerCount, Is.EqualTo(0), "Every dialog's blocker should have gone with it.");
        }

        [UnityTest]
        public IEnumerator Back_OpensThePackagesPauseMenu_ListingTheProjectsControls()
        {
            UIManager.instance.HandleBack();
            PauseMenu menu = Object.FindAnyObjectByType<PauseMenu>();
            Assert.That(menu, Is.Not.Null, "Back with no window should open the pause menu (the UI Manager's Pause Menu On Back, and the catalog's menu).");
            Assert.That(Time.timeScale, Is.EqualTo(0f));
            Assert.That(menu.Actions, Is.Not.Null, "It should read the project's input actions.");
            StringAssert.Contains("Jump", menu.ControlsListing());
            Assert.That(menu.ControlsList, Is.Not.Null, "Its controls page should be a list of the controls.");
            menu.ShowControls();
            yield return Waits.WaitUntil(() => !menu.ControlsList.IsUpdating && menu.ControlsList.ShownItems.Any(row => row.Data.DisplayName == "Jump"));
            ControlBindingDisplay jump = menu.ControlsList.ShownItems.First(row => row.Data.DisplayName == "Jump");
            Assert.That(jump.Data.Rebindable, Is.True, "Jump's binding should be rebindable from its row.");
            Assert.That(jump.ShownBinding, Is.Not.Empty.And.Not.EqualTo("-"));
            menu.Resume();
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            yield return null;
        }

        // Replay controls as a game registers them, counting what's pressed.
        private sealed class FakeReplays : IReplayControls
        {
            public int saved, loaded, quit;
            public bool CanSave => true;
            public bool CanLoad => true;
            public string Status => "Recording: 0:42";
            public void SaveReplay() => saved++;
            public void LoadReplay() => loaded++;
            public void BeforeQuit() => quit++;
        }

        [UnityTest]
        public IEnumerator ThePauseMenusReplaysTab_ShowsOnlyWithReplayControls_AndDrivesThem()
        {
            UIManager.instance.HandleBack();
            PauseMenu menu = Object.FindAnyObjectByType<PauseMenu>();
            Assert.That(menu.transform.Find("Replays Tab").gameObject.activeSelf, Is.False, "Without replay controls, there's no Replays tab.");
            menu.Resume();
            yield return null;

            var replays = new FakeReplays();
            ReplayControls.Register(replays);
            try
            {
                UIManager.instance.HandleBack();
                menu = Object.FindAnyObjectByType<PauseMenu>();
                Assert.That(menu.transform.Find("Replays Tab").gameObject.activeSelf, Is.True, "With them, the tab shows.");
                menu.ShowReplays();
                Assert.That(menu.ShowingReplays, Is.True);
                menu.RefreshReplays();
                StringAssert.Contains("0:42", menu.transform.Find("Replays Page/Status").GetComponent<TMPro.TMP_Text>().text, "Its page shows their status.");
                menu.transform.Find("Replays Page/Save Replay Button").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                Assert.That(replays.saved, Is.EqualTo(1), "Save Replay saves.");
                UIManager.instance.HandleBack();
                Assert.That(menu.ShowingReplays, Is.False, "Back from the replays page goes to the main page.");
                menu.ShowReplays();
                menu.transform.Find("Replays Page/Load Replay Button").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                Assert.That((replays.loaded, menu.IsClosing || menu == null, Time.timeScale), Is.EqualTo((1, true, 1f)), "Load Replay closes the menu, then plays.");
            }
            finally { ReplayControls.Unregister(replays); }
            yield return null;
        }

        [Test]
        public void ThePackagesChoiceDialog_OffersItsOptions_AndRunsTheOneChosen()
        {
            int chosen = -1;
            UIChoiceDialog dialog = UIChoiceDialog.Open("Which?", -1,
                new UIChoiceDialog.Option("One", () => chosen = 1, ThemedButton.Kind.Primary),
                new UIChoiceDialog.Option("Two", () => chosen = 2),
                new UIChoiceDialog.Option("Three", () => chosen = 3));
            Assert.That(dialog, Is.Not.Null, "The UI Manager should have its Choice Dialog.");
            Assert.That(dialog.Buttons.Select(b => b.GetComponentInChildren<TMPro.TMP_Text>().text), Is.EqualTo(new[] { "ONE", "TWO", "THREE" }));
            Assert.That(dialog.Buttons[0].ButtonKind, Is.EqualTo(ThemedButton.Kind.Primary));
            UIManager.instance.HandleBack();
            Assert.That((chosen, dialog.IsClosing), Is.EqualTo((-1, false)), "With no option for Back, Back leaves it open.");
            dialog.Buttons[1].onClick.Invoke();
            dialog.Buttons[2].onClick.Invoke();
            Assert.That(chosen, Is.EqualTo(2), "The one pressed runs, once: the dialog's closing.");
            Assert.That(dialog.IsClosing || dialog == null, Is.True);
        }

        [UnityTest]
        public IEnumerator TheContextMenuAndTheNotifications_WorkFromThePackagesPrefabs()
        {
            int opened = 0;
            UIRightClickMenu menu = UIRightClickMenu.Open(new System.Collections.Generic.List<MenuItemData>
            {
                new("Open", () => opened++, featured: true), new("Rename", () => { }), MenuItemData.Spacer, new("Delete", () => { }),
            }, new Vector2(20f, Screen.height - 20f));
            Assert.That(menu, Is.Not.Null, "The UI Manager should have a context menu.");
            Assert.That(menu.Entries.Count, Is.EqualTo(3));
            menu.Entries[0].GetComponent<Button>().onClick.Invoke();
            Assert.That(opened, Is.EqualTo(1), "An entry's own button should run its item.");

            NotificationEntry toast = UIManager.instance.ShowNotification("Saved", "Progress saved.");
            Assert.That(toast, Is.Not.Null, "The UI Manager should have notifications.");
            Assert.That(toast.transform.parent.name, Is.EqualTo("Top Middle"));
            yield return null;
        }

        [UnityTest]
        public IEnumerator ThePauseMenusControls_AreATab()
        {
            UIManager.instance.HandleBack();
            PauseMenu menu = Object.FindAnyObjectByType<PauseMenu>();
            yield return null; // Its tabs start.
            Assert.That(menu.ShowingControls, Is.False);

            // Its own tab button (not the Controls button) must fill the page too: on a menu just opened, never filled.
            Tab controlsTab = menu.GetComponent<TabGroup>().Tabs.First(tab => tab.ID == "Controls");
            controlsTab.GetComponent<Button>().onClick.Invoke();
            Assert.That(menu.ShowingControls, Is.True, "Controls should be a tab of its own.");
            StringAssert.Contains("Jump", menu.ShownControls, "Opened by its tab, the controls page should list the controls.");

            UIManager.instance.HandleBack();
            Assert.That(menu.ShowingControls, Is.False, "Back should go back to the menu's tab.");
            menu.ShowControls();
            Assert.That(menu.GetComponent<TabGroup>().SelectedTab.ID, Is.EqualTo("Controls"), "The Controls button should select its tab.");
            menu.Resume();
        }
    }
}
