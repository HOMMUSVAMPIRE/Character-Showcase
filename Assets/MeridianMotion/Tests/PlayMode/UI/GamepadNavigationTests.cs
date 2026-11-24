using System.Collections;
using System.Collections.Generic;
using MeridianMotion.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace MeridianMotion.Tests.UI
{
    /// <summary>
    /// Tests of navigating windows with a gamepad (UIManager's selection, RootUIPrefab.FirstSelectable): nothing selected
    /// for the pointer, but the frontmost window's first control once the gamepad's used, then the gamepad moving between
    /// controls and pressing them; a window opened while the gamepad's in use selected at once, its First Selected if it
    /// has one; a HUD (taking neither the pointer nor the game's input) never selected.
    /// </summary>
    [Category(TestArea.UI)]
    public class GamepadNavigationTests : UITestsBase
    {
        private VirtualGamepad gamepad;

        [SetUp]
        public void AddTheModuleAndAGamepad()
        {
            var module = EventSystem.current.gameObject.AddComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions();
            gamepad = new VirtualGamepad();
        }

        [TearDown]
        public void RemoveTheGamepad()
        {
            gamepad?.Dispose();
            gamepad = null;
        }

        // A window with buttons stacked top to bottom, named for their places.
        private RootUIPrefab Menu(string name, int buttons, out List<Button> made)
        {
            RootUIPrefab template = Template(name);
            made = new List<Button>();
            for (int i = 0; i < buttons; i++)
            {
                var obj = new GameObject($"Button {i}", typeof(RectTransform), typeof(Image), typeof(Button));
                obj.transform.SetParent(template.transform, false);
                var rect = (RectTransform)obj.transform;
                rect.sizeDelta = new Vector2(200f, 40f);
                rect.anchoredPosition = new Vector2(0f, 100f - 60f * i);
                made.Add(obj.GetComponent<Button>());
            }
            return template;
        }

        private IEnumerator Press(GamepadButton button) => gamepad.Press(button);

        private static GameObject Selected => EventSystem.current.currentSelectedGameObject;

        // The open window's copy of a template's button (OpenUI copies the template).
        private static Button InWindow(RootUIPrefab window, int index) => window.transform.Find($"Button {index}").GetComponent<Button>();

        [UnityTest]
        public IEnumerator NothingIsSelectedForThePointer_ButTheFirstButtonOnceTheGamepadMoves_ThenItMovesAndPresses()
        {
            RootUIPrefab window = Open(Menu("Menu", 3, out _));
            yield return Frames(3);
            Assert.That(Selected, Is.Null, "Until a gamepad (or the keyboard) is used, nothing should be selected: the pointer needs nothing.");

            yield return Press(GamepadButton.DpadDown);
            Assert.That(Selected, Is.EqualTo(InWindow(window, 0).gameObject), "The gamepad used, the window's first button should be selected.");
            Assert.That(manager.NavigatingByButtons, Is.True);

            yield return Press(GamepadButton.DpadDown);
            Assert.That(Selected, Is.EqualTo(InWindow(window, 1).gameObject), "The gamepad should move to the next button.");

            bool clicked = false;
            InWindow(window, 1).onClick.AddListener(() => clicked = true);
            yield return Press(GamepadButton.South);
            Assert.That(clicked, Is.True, "The gamepad's A should press the selected button.");
        }

        [UnityTest]
        public IEnumerator AWindowOpenedWhileTheGamepadsInUse_IsSelectedAtOnce_ItsFirstSelectedFirst()
        {
            Open(Menu("First", 2, out _));
            yield return Frames(2);
            yield return Press(GamepadButton.DpadDown);
            Assert.That(manager.NavigatingByButtons, Is.True, "Test setup: the gamepad should be in use.");

            RootUIPrefab template = Menu("Second", 3, out List<Button> buttons);
            template.FirstSelected = buttons[2];
            RootUIPrefab second = Open(template);
            yield return Frames(2);
            Assert.That(Selected, Is.EqualTo(InWindow(second, 2).gameObject),
                "A window opened over another while the gamepad's in use should have its First Selected selected at once.");
        }

        [UnityTest]
        public IEnumerator AHudIsNeverSelected()
        {
            RootUIPrefab hud = Menu("HUD", 2, out _);
            hud.NeedsCursor = false;
            hud.BlocksGameInput = false;
            Open(hud);
            yield return Frames(2);
            yield return Press(GamepadButton.DpadDown);
            Assert.That(Selected, Is.Null, "A window taking neither the pointer nor the game's input (a HUD) shouldn't take the gamepad.");
            Assert.That(manager.FocusedWindow(), Is.Null);
        }
    }
}
