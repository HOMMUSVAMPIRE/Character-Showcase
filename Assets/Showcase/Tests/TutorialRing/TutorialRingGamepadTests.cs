using System.Collections;
using System.Linq;
using MeridianMotion.Characters;
using MeridianMotion.Tests;
using MeridianMotion.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace MeridianMotion.Showcase.Tests.TutorialRing
{
    /// <summary>
    /// The Tutorial Ring's built scene, set up (Set Up Scene), with its UI, played from a gamepad: Start opens the pause
    /// menu with a control selected, the d-pad moves between its controls, B closes it but never opens it, and Start
    /// opens and closes it. The gamepad's the test's own (VirtualGamepad: the machine's devices off meanwhile), and the
    /// player's input is switched to it and kept there (no auto-switching): the UI's Menu action shares the player's
    /// actions, and listens only to the devices of its control scheme.
    /// </summary>
    [Category(TestArea.Levels)]
    [TestTiming]
    public class TutorialRingGamepadTests
    {
        private const string ScenePath = "Assets/Showcase/Scenes/TutorialRing/TutorialRing.unity";
        private Scene scene;
        private VirtualGamepad gamepad;
        // The player's input, and whether it auto-switched schemes before the test.
        private PlayerInput player;
        private bool playerAutoSwitched;

        [UnitySetUp]
        public IEnumerator LoadTheSceneWithAGamepad()
        {
            gamepad = new VirtualGamepad();
#if UNITY_EDITOR
            AsyncOperation loading = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
#else
            AsyncOperation loading = SceneManager.LoadSceneAsync("TutorialRing", LoadSceneMode.Additive);
#endif
            while (!loading.isDone) { yield return null; }
            scene = SceneManager.GetSceneByPath(ScenePath);
            SceneManager.SetActiveScene(scene);
            CharacterSpawner spawner = Object.FindAnyObjectByType<CharacterSpawner>();
            Assert.That(spawner, Is.Not.Null, "The scene should be set up (Set Up Scene) with a spawner.");
            float giveUp = Time.realtimeSinceStartup + 20f;
            while (spawner.PlayerCharacter == null && Time.realtimeSinceStartup < giveUp) { yield return null; }
            player = PlayerInputReader.instance.playerInput;
            playerAutoSwitched = !player.neverAutoSwitchControlSchemes;
            player.neverAutoSwitchControlSchemes = true;
            Assert.That(player.SwitchCurrentControlScheme(gamepad.Device), Is.True, "The player's input should switch to the test's gamepad.");
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator UnloadTheScene()
        {
            if (player != null) { player.neverAutoSwitchControlSchemes = !playerAutoSwitched; }
            player = null;
            if (scene.IsValid() && scene.isLoaded)
            {
                AsyncOperation unloading = SceneManager.UnloadSceneAsync(scene);
                while (unloading != null && !unloading.isDone) { yield return null; }
            }
            Time.timeScale = 1f;
            gamepad?.Dispose();
            gamepad = null;
        }

        private IEnumerator Press(GamepadButton button) => gamepad.Press(button);

        // What the player's input was on, for a failure's message.
        private string InputState()
        {
            InputAction menu = player != null ? player.actions.FindAction("UI/Menu") : null;
            string paired = player != null && player.user.valid ? string.Join(", ", player.user.pairedDevices.Select(d => d.name)) : "(none)";
            return $"Scheme {player?.currentControlScheme}; paired [{paired}]; "
                + $"Menu {(menu == null ? "missing" : $"enabled {menu.enabled}, on [{string.Join(", ", menu.controls.Select(c => c.path))}]")}; "
                + $"captured {UIManager.IsInputCaptured}; top window {UIManager.instance?.TopWindow?.name ?? "none"}.";
        }

        private static GameObject Selected() => EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;

        [UnityTest]
        public IEnumerator Start_OpensThePauseMenu_TheDpadMovesThroughIt_AndStartClosesIt()
        {
            UIManager manager = UIManager.instance;
            Assert.That(manager, Is.Not.Null, "The scene should have its UI (Set Up Scene).");
            yield return Press(GamepadButton.Start);
            RootUIPrefab menu = manager.FocusedWindow();
            Assert.That(menu, Is.Not.Null, "The gamepad's Start should open the pause menu. " + InputState());
            Assert.That(menu.GetComponent<PauseMenu>(), Is.Not.Null, "The window opened should be the pause menu.");
            Assert.That(Selected(), Is.Not.Null, "Opened by the gamepad, the pause menu should have a control selected.");
            Assert.That(Selected().transform.IsChildOf(menu.transform), Is.True);

            GameObject first = Selected();
            yield return Press(GamepadButton.DpadDown);
            Assert.That(Selected(), Is.Not.EqualTo(first), "The gamepad's d-pad should move between the menu's controls.");
            Assert.That(Selected().transform.IsChildOf(menu.transform), Is.True);

            yield return Press(GamepadButton.East);
            Assert.That(manager.FocusedWindow(), Is.Null, "B should close it.");
            yield return Press(GamepadButton.East);
            Assert.That(manager.FocusedWindow(), Is.Null, "With nothing open, B shouldn't open it (Start does).");
            yield return Press(GamepadButton.Start);
            Assert.That(manager.FocusedWindow(), Is.Not.Null, "Start should open it again...");
            yield return Press(GamepadButton.Start);
            Assert.That(manager.FocusedWindow(), Is.Null, "...and close it.");
        }
    }
}
