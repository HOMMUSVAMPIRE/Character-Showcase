using System.Collections;
using MeridianMotion.UI;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace MeridianMotion.Tests.UI
{
    /// <summary>
    /// Tests of the pause menu (PauseMenu, opened by PauseMenuOnBack: ReferenceAssets/OpenTasks.md, G11): the Back key with
    /// no window to take it opens it and pauses the game, and again resumes it, the time scale put back as it was; while
    /// it's open the game is told it needs the pointer and its input stops; its controls page lists the bindings read from
    /// the input actions (but the hidden ones), and Back goes from it back to the main page; and Quit asks first, Back on
    /// the question keeping the game going.
    /// </summary>
    [Category(TestArea.UI)]
    public class PauseMenuTests : UITestsBase
    {
        private PauseMenu menu;

        // A plain SetUp, so it runs after the base's (a UnitySetUp would run before it, with no manager yet).
        [SetUp]
        public void SetUpMenu()
        {
            menu = Template<PauseMenu>("Pause Menu");
            menu.BlocksGameInput = true;
            menu.Actions = Track(Actions());
            GameObject Page(string name)
            {
                var page = new GameObject(name, typeof(RectTransform));
                page.transform.SetParent(menu.transform, false);
                return page;
            }
            GameObject controls = Page("Controls Page");
            var text = new GameObject("Bindings", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            text.transform.SetParent(controls.transform, false);
            SetField(menu, "mainPage", Page("Main Page"));
            SetField(menu, "controlsPage", controls);
            SetField(menu, "controlsText", text);

            var catalog = Track(ScriptableObject.CreateInstance<UIWindowCatalog>());
            catalog.Windows.Add(menu.gameObject);
            manager.WindowCatalog = catalog;
            UIMessageBox yesNo = Template<UIMessageBox>("Yes No Dialog", new Vector2(400f, 200f));
            manager.YesNoDialogPrefab = yesNo.gameObject;
            manager.gameObject.AddComponent<PauseMenuOnBack>(); // Registers the fallback as it's enabled.
        }

        // The menu's serialized fields, as its prefab sets them.
        private static void SetField(PauseMenu menu, string name, Object value) =>
            typeof(PauseMenu).GetField(name, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(menu, value);

        // Input actions as the project's are laid out: a Player map with a used action and a hidden one.
        private static InputActionAsset Actions()
        {
            var actions = ScriptableObject.CreateInstance<InputActionAsset>();
            InputActionMap player = actions.AddActionMap("Player");
            player.AddAction("Jump").AddBinding("<Keyboard>/space", groups: "Keyboard&Mouse");
            player.FindAction("Jump").AddBinding("<Gamepad>/buttonSouth", groups: "Gamepad");
            player.AddAction("ToggleView").AddBinding("<Keyboard>/v", groups: "Keyboard&Mouse");
            player.AddAction("Attack").AddBinding("<Mouse>/leftButton", groups: "Keyboard&Mouse");
            return actions;
        }

        private PauseMenu Open => manager.GetUI<PauseMenu>(menu.gameObject);

        [UnityTest]
        public IEnumerator Back_OpensItAndPausesTheGame_AndAgainResumesIt()
        {
            Time.timeScale = 0.5f;
            manager.HandleBack();
            Assert.That(Open, Is.Not.Null, "Back with no window to take it should open the pause menu.");
            Assert.That(Time.timeScale, Is.EqualTo(0f), "Open, it should pause the game.");
            yield return Frames();

            manager.HandleBack();
            Assert.That(Time.timeScale, Is.EqualTo(0.5f), "Closed, the time scale should be as it was.");
            yield return Frames();
            Assert.That(Open, Is.Null);
        }

        [UnityTest]
        public IEnumerator WhileItsOpen_TheGameIsToldItNeedsThePointer_AndItsInputStops()
        {
            manager.HandleBack();
            yield return Frames();
            Assert.That(manager.CursorNeeded, Is.True);
            Assert.That(manager.GameInputBlocked, Is.True);
            Open.Resume();
            yield return Frames();
            Assert.That(manager.CursorNeeded || manager.GameInputBlocked, Is.False, "Resumed, the game should have its pointer and input back.");
            Assert.That(Time.timeScale, Is.EqualTo(1f));
        }

        [UnityTest]
        public IEnumerator TheControlsPage_ListsTheBindings_AndBackGoesBackToTheMainPage()
        {
            manager.HandleBack();
            PauseMenu open = Open;
            open.ShowControls();
            Assert.That(open.ShowingControls, Is.True);
            string listing = open.ControlsListing();
            StringAssert.Contains("Jump", listing);
            StringAssert.Contains("Space", listing, "The keyboard's binding should be read from the actions.");
            StringAssert.Contains("Toggle View", listing, "Action names should be spaced out.");
            StringAssert.DoesNotContain("Attack", listing, "A hidden action shouldn't be listed.");

            manager.HandleBack();
            Assert.That(open.ShowingControls, Is.False, "Back on the controls page should go back to the main page.");
            Assert.That(open.IsClosing, Is.False, "Not close the menu.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Quit_AsksFirst_AndBackOnTheQuestionKeepsPlaying()
        {
            manager.HandleBack();
            PauseMenu open = Open;
            UIMessageBox question = open.Quit();
            Assert.That(question, Is.Not.Null, "Quit should ask first.");
            manager.HandleBack();
            Assert.That(question.IsClosing, Is.True, "Back should answer the question.");
            Assert.That(open.IsClosing, Is.False, "With No: the menu stays.");
            yield return Frames();
            Assert.That(Application.isPlaying, Is.True);
        }
    }
}
