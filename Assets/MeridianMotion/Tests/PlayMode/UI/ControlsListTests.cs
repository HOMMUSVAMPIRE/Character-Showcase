using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MeridianMotion.UI;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace MeridianMotion.Tests.UI
{
    /// <summary>
    /// Tests of the controls list (ControlsList, ControlBindingDisplay, ControlSchemeTracker, SavedBindings): a row per
    /// action (but the hidden ones) with its binding in the scheme in use, a button's rebindable and a stick's or a
    /// composite's not; switching to a gamepad listing the gamepad's bindings; a button rebound by pressing the new one,
    /// the UI's Back held off meanwhile (with a gamepad, the button that started it, still held, not taken for the new
    /// one); a rebinding saved, and reset.
    /// </summary>
    [Category(TestArea.UI)]
    public class ControlsListTests : UITestsBase
    {
        private const string SavedKey = "MeridianMotion.Tests.ControlsListTests";
        private InputActionAsset actions;
        private ControlsList list;
        private SavedBindings saved;
        private readonly List<InputDevice> devices = new();

        [SetUp]
        public void MakeTheList()
        {
            ControlSchemeTracker.Forget();
            actions = Track(ScriptableObject.CreateInstance<InputActionAsset>());
            actions.AddControlScheme("Keyboard&Mouse").WithRequiredDevice("<Keyboard>").WithOptionalDevice("<Mouse>");
            actions.AddControlScheme("Gamepad").WithRequiredDevice("<Gamepad>");
            InputActionMap player = actions.AddActionMap("Player");
            InputAction move = player.AddAction("Move", InputActionType.Value);
            move.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/w", "Keyboard&Mouse").With("Down", "<Keyboard>/s", "Keyboard&Mouse")
                .With("Left", "<Keyboard>/a", "Keyboard&Mouse").With("Right", "<Keyboard>/d", "Keyboard&Mouse")
                .With("Up", "<Keyboard>/upArrow", "Keyboard&Mouse"); // A part with two keys, as the project's WASD and arrows.
            move.AddBinding("<Gamepad>/leftStick", groups: "Gamepad");
            InputAction jump = player.AddAction("Jump", InputActionType.Button);
            jump.AddBinding("<Keyboard>/space", groups: "Keyboard&Mouse");
            jump.AddBinding("<Gamepad>/buttonSouth", groups: "Gamepad");
            player.AddAction("ToggleView", InputActionType.Button).AddBinding("<Keyboard>/v", groups: "Keyboard&Mouse");
            player.AddAction("Attack", InputActionType.Button).AddBinding("<Mouse>/leftButton", groups: "Keyboard&Mouse");

            var savedObject = Track(new GameObject("Saved Bindings"));
            savedObject.SetActive(false);
            saved = savedObject.AddComponent<SavedBindings>();
            saved.Actions = actions;
            saved.Key = SavedKey;
            savedObject.SetActive(true);

            var root = Track(new GameObject("Controls", typeof(RectTransform)));
            root.transform.SetParent(manager.defaultCanvas, false);
            root.SetActive(false);
            list = root.AddComponent<ControlsList>();
            list.Configure(actions, "Player", new[] { "Attack" });
            SetBase("itemPrefabs", new List<GameObject> { RowTemplate() });
            SetBase("itemsParent", root.transform);
            root.SetActive(true);
        }

        [TearDown]
        public void RemoveDevicesAndSaved()
        {
            list.CancelRebind();
            foreach (InputDevice device in devices) { if (device.added) { InputSystem.RemoveDevice(device); } }
            ControlSchemeTracker.Forget();
            PlayerPrefs.DeleteKey(SavedKey);
        }

        private void SetBase(string name, object value) =>
            typeof(DataDisplayList<ControlBinding, ControlBindingDisplay>).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(list, value);

        private static void Set(object target, string name, object value) =>
            target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);

        // A row: its name, its binding button (and the button's label) and its plain binding text.
        private GameObject RowTemplate()
        {
            var row = Track(new GameObject("Control Row", typeof(RectTransform)));
            TMP_Text Text(Transform parent, string name) => new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)) { transform = { parent = parent } }.GetComponent<TMP_Text>();
            var buttonObject = new GameObject("Binding Button", typeof(RectTransform));
            buttonObject.transform.SetParent(row.transform, false);
            var shape = buttonObject.AddComponent<ChamferedBox>();
            var button = buttonObject.AddComponent<ThemedButton>();
            button.Shape = shape;
            var display = row.AddComponent<ControlBindingDisplay>();
            Set(display, "nameText", Text(row.transform, "Name"));
            Set(display, "bindingButton", button);
            Set(display, "bindingButtonText", Text(buttonObject.transform, "Label"));
            Set(display, "bindingText", Text(row.transform, "Binding Text"));
            return row;
        }

        private T Add<T>() where T : InputDevice
        {
            T device = InputSystem.AddDevice<T>();
            devices.Add(device);
            return device;
        }

        private IEnumerator Shown(int rows)
        {
            list.Populate((List<string>)null);
            // Its rows for the scheme it shows now: the list filled itself as it was turned on, with whatever device was
            // used last (a gamepad, after a test with one), and those rows count as many.
            yield return Waits.WaitUntil(() => !list.IsUpdating && list.ShownItems.Count() == rows && list.ShownItems.All(row => row.Data.Scheme == list.Scheme));
        }

        private ControlBindingDisplay Row(string name) => list.ShownItems.First(row => row.Data.DisplayName == name);

        [UnityTest]
        public IEnumerator ItListsAnActionPerRow_WithItsBindingInTheSchemeInUse()
        {
            list.ForcedScheme = "Keyboard&Mouse";
            yield return Shown(3);
            Assert.That(list.ShownItems.Select(row => row.Data.DisplayName), Is.EqualTo(new[] { "Move", "Jump", "Toggle View" }),
                "A row per action in order, named for people, the hidden one left out.");
            ControlBindingDisplay jump = Row("Jump"), move = Row("Move");
            Assert.That(jump.Data.Rebindable, Is.True, "A button's binding can be rebound.");
            Assert.That(jump.ShownBinding, Is.EqualTo(actions.FindAction("Jump").GetBindingDisplayString(InputBinding.MaskByGroup("Keyboard&Mouse"))));
            Assert.That(move.Data.Rebindable, Is.False, "A composite's (WASD) is shown, not rebound.");
            Assert.That(move.ShownBinding, Is.EqualTo("W/S/A/D"), "A composite shows each part's first key, in order.");
        }

        [UnityTest]
        public IEnumerator UsingAGamepad_ListsTheGamepadsBindings_AndTheKeyboardsAgainAfter()
        {
            Keyboard keyboard = Add<Keyboard>();
            Gamepad gamepad = Add<Gamepad>();
            // The devices settled first: a device just added sends its state over the next updates, which an action
            // still bound to gamepads (another test's) can take as the gamepad being used.
            yield return null;
            yield return null;
            ControlSchemeTracker.NoteDeviceUsed(keyboard);
            yield return Shown(3);
            Assert.That(list.Scheme, Is.EqualTo("Keyboard&Mouse"));
            string keyboardJump = Row("Jump").ShownBinding;

            ControlSchemeTracker.NoteDeviceUsed(gamepad);
            yield return Waits.WaitUntil(() => !list.IsUpdating && list.ShownItems.Any() && list.ShownItems.First().Data.Scheme == "Gamepad");
            Assert.That(Row("Jump").ShownBinding, Is.EqualTo(actions.FindAction("Jump").GetBindingDisplayString(InputBinding.MaskByGroup("Gamepad"))));
            Assert.That(Row("Jump").ShownBinding, Is.Not.EqualTo(keyboardJump), "Switching to the gamepad should list its bindings.");
            Assert.That(Row("Toggle View").ShownBinding, Is.EqualTo("-"), "An action with no gamepad binding is listed without one.");
            Assert.That(Row("Toggle View").Data.Rebindable, Is.False, "With no binding in the scheme, there's nothing to rebind.");

            ControlSchemeTracker.NoteDeviceUsed(keyboard);
            yield return Waits.WaitUntil(() => !list.IsUpdating && list.ShownItems.First().Data.Scheme == "Keyboard&Mouse");
            Assert.That(Row("Jump").ShownBinding, Is.EqualTo(keyboardJump), "Back on the keyboard, its bindings again.");
        }

        [UnityTest]
        public IEnumerator PressingAButton_RebindsIt_WithBackHeldOffMeanwhile_AndItsSaved()
        {
            Keyboard keyboard = Add<Keyboard>();
            list.ForcedScheme = "Keyboard&Mouse";
            yield return Shown(3);
            RootUIPrefab window = Open(Template("Window"));
            ControlBinding jump = Row("Jump").Data;

            Assert.That(list.StartRebind(jump), Is.True);
            Assert.That(Row("Jump").Listening, Is.True, "Its row should show it's waiting for the button.");
            Assert.That(UIManager.IsInputCaptured, Is.True);
            manager.HandleBack();
            Assert.That(window.IsClosing, Is.False, "Back is held off while a rebinding waits (Escape cancels it instead).");

            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.K));
            InputSystem.Update();
            yield return Waits.WaitUntil(() => list.Rebinding == null);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.Update();

            InputAction action = actions.FindAction("Jump");
            Assert.That(action.bindings[jump.BindingIndex].effectivePath, Is.EqualTo("<Keyboard>/k"));
            Assert.That(PlayerPrefs.GetString(SavedKey, ""), Does.Contain("<Keyboard>/k"), "The rebinding should be saved.");
            yield return Waits.WaitUntil(() => !list.IsUpdating && Row("Jump").Data.Overridden);
            yield return null;
            Assert.That(UIManager.IsInputCaptured, Is.False, "Back should be the windows' again.");
        }

        [UnityTest]
        public IEnumerator RebindingWithAGamepad_DoesntTakeTheHeldButtonThatStartedIt()
        {
            Gamepad gamepad = Add<Gamepad>();
            list.ForcedScheme = "Gamepad";
            yield return Shown(3);
            ControlBinding jump = Row("Jump").Data;

            // A pressed (as it is when it starts the rebinding) and still held, the gamepad reporting its state again.
            InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.South));
            InputSystem.Update();
            yield return null;
            Assert.That(gamepad.buttonSouth.isPressed, Is.True, "Test setup: A should be held.");
            Assert.That(list.StartRebind(jump), Is.True);
            for (int i = 0; i < 5; i++)
            {
                InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.South));
                InputSystem.Update();
                yield return null;
            }
            Assert.That(list.Rebinding, Is.Not.Null, "The held A shouldn't be taken as the new binding.");

            InputSystem.QueueStateEvent(gamepad, new GamepadState());
            InputSystem.Update();
            yield return null;
            yield return null;
            InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.North));
            InputSystem.Update();
            yield return Waits.WaitUntil(() => list.Rebinding == null);
            InputSystem.QueueStateEvent(gamepad, new GamepadState());
            InputSystem.Update();
            Assert.That(actions.FindAction("Jump").bindings[jump.BindingIndex].effectivePath, Is.EqualTo("<Gamepad>/buttonNorth"),
                "Once A's let go, the next button pressed should be the new binding.");
        }

        [UnityTest]
        public IEnumerator ARebindingCancelled_LeavesTheBinding_AndResetPutsTheAssetsBack()
        {
            list.ForcedScheme = "Keyboard&Mouse";
            yield return Shown(3);
            ControlBinding jump = Row("Jump").Data;
            InputAction action = actions.FindAction("Jump");

            Assert.That(list.StartRebind(jump), Is.True);
            list.CancelRebind();
            Assert.That(list.Rebinding, Is.Null);
            Assert.That(action.bindings[jump.BindingIndex].effectivePath, Is.EqualTo("<Keyboard>/space"), "Cancelled, the binding stays.");

            list.SetBinding(jump, "<Keyboard>/j");
            Assert.That(action.bindings[jump.BindingIndex].effectivePath, Is.EqualTo("<Keyboard>/j"));
            list.ResetAll();
            Assert.That(action.bindings[jump.BindingIndex].effectivePath, Is.EqualTo("<Keyboard>/space"), "Reset should put the asset's binding back.");
            Assert.That(PlayerPrefs.GetString(SavedKey, ""), Does.Not.Contain("<Keyboard>/j"), "...and save that.");
        }

        [Test]
        public void SavedBindings_PutTheSavedRebindingBack()
        {
            InputAction jump = actions.FindAction("Jump");
            jump.ApplyBindingOverride(0, "<Keyboard>/q");
            saved.Save();
            jump.RemoveAllBindingOverrides();
            saved.Load();
            Assert.That(jump.bindings[0].effectivePath, Is.EqualTo("<Keyboard>/q"));
        }

        [Test]
        public void TheTracker_NamesTheSchemeForTheDeviceUsed()
        {
            Gamepad gamepad = Add<Gamepad>();
            Assert.That(ControlSchemeTracker.SchemeFor(actions, gamepad), Is.EqualTo("Gamepad"));
            ControlSchemeTracker.NoteDeviceUsed(gamepad);
            Assert.That(ControlSchemeTracker.CurrentScheme(actions), Is.EqualTo("Gamepad"));
            ControlSchemeTracker.NoteDeviceUsed(Add<Keyboard>());
            Assert.That(ControlSchemeTracker.CurrentScheme(actions), Is.EqualTo("Keyboard&Mouse"));
        }
    }
}
