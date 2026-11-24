using System.Text.RegularExpressions;
using MeridianMotion.Showcase.TutorialRing;
using MeridianMotion.Tests;
using MeridianMotion.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace MeridianMotion.Showcase.Tests.TutorialRing
{
    /// <summary>
    /// Tests of the lesson prompts (PromptText, TutorialPromptPresenter): an action in braces is written as its binding
    /// in the scheme given (the keyboard's, the gamepad's, read from the actions rather than written here); an action the
    /// actions don't have is left as its name; switching to a gamepad while a prompt is up writes it again for the
    /// gamepad; doing a lesson shows its follow-up, then nothing once the zone's left; and hidden, nothing's shown though
    /// the lessons are still followed.
    /// </summary>
    [Category(TestArea.UI)]
    [TestTiming]
    public class PromptTests
    {
        private InputActionAsset actions;
        private TutorialLessonCatalog catalog;
        private TutorialLessonTracker tracker;
        private TutorialPromptPresenter presenter;
        private Gamepad gamepad;
        private bool wasHidden;

        [SetUp]
        public void SetUp()
        {
            ControlSchemeTracker.Forget();
            wasHidden = TutorialPromptPresenter.Hidden;
            TutorialPromptPresenter.Hidden = false;
            actions = TutorialTestLessons.Actions();
            catalog = TutorialTestLessons.Catalog();
            var holder = new GameObject("Prompts");
            tracker = holder.AddComponent<TutorialLessonTracker>();
            tracker.Catalog = catalog;
            presenter = holder.AddComponent<TutorialPromptPresenter>();
            presenter.Actions = actions;
            presenter.Show(tracker);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(tracker.gameObject);
            Object.DestroyImmediate(catalog);
            Object.DestroyImmediate(actions);
            if (gamepad != null) { InputSystem.RemoveDevice(gamepad); }
            ControlSchemeTracker.Forget();
            TutorialPromptPresenter.Hidden = wasHidden;
        }

        [Test]
        public void Format_WithTheKeyboard_ShowsTheKeyboardsBinding()
        {
            string shown = PromptText.Format("Press {Crouch} to crouch.", actions, TutorialTestLessons.KeyboardScheme);
            Assert.That(shown, Is.EqualTo($"Press {TutorialTestLessons.Shown(actions, "Crouch", TutorialTestLessons.KeyboardScheme)} to crouch."));
            Assert.That(shown, Is.EqualTo("Press C to crouch."));
        }

        [Test]
        public void Format_WithAGamepad_ShowsTheGamepadsBinding()
        {
            string shown = PromptText.Format("Press {Crouch}.", actions, TutorialTestLessons.GamepadScheme);
            string expected = TutorialTestLessons.Shown(actions, "Crouch", TutorialTestLessons.GamepadScheme);
            Assert.That(shown, Is.EqualTo($"Press {expected}."));
            Assert.That(expected, Is.Not.EqualTo(TutorialTestLessons.Shown(actions, "Crouch", TutorialTestLessons.KeyboardScheme)));
        }

        [Test]
        public void Format_WithAnUnknownAction_LeavesItsName()
        {
            LogAssert.Expect(LogType.Warning, new Regex("no action 'Fly'"));
            Assert.That(PromptText.Format("Press {Fly}!", actions, TutorialTestLessons.KeyboardScheme), Is.EqualTo("Press Fly!"));
        }

        [Test]
        public void SwitchingToAGamepad_WhileAPromptIsUp_WritesItForTheGamepad()
        {
            tracker.Enter("crouch");
            string keyboard = TutorialTestLessons.Shown(actions, "Crouch", TutorialTestLessons.KeyboardScheme);
            string pad = TutorialTestLessons.Shown(actions, "Crouch", TutorialTestLessons.GamepadScheme);
            Assert.That(presenter.Text, Is.EqualTo($"Press {keyboard} to crouch."));
            gamepad = InputSystem.AddDevice<Gamepad>();
            ControlSchemeTracker.NoteDeviceUsed(gamepad);
            Assert.That(presenter.Text, Is.EqualTo($"Press {pad} to crouch."), "The prompt should follow the switch to the gamepad at once.");
        }

        [Test]
        public void DoingTheLesson_ShowsItsFollowUp_ThenNothingOnLeaving()
        {
            tracker.Enter("crouch");
            tracker.Heard(MeridianMotion.Characters.MovementEvents.CrouchStarted, default);
            Assert.That(presenter.Text, Is.EqualTo("Now slide."));
            tracker.Leave("crouch");
            Assert.That(presenter.Text, Is.Empty);
        }

        [Test]
        public void Hidden_ShowsNothing_ButTheLessonsAreStillFollowed()
        {
            TutorialPromptPresenter.Hidden = true;
            tracker.Enter("jump");
            Assert.That(presenter.ShownText, Is.Empty, "Hidden, nothing should be shown.");
            Assert.That(presenter.Text, Is.Not.Empty, "Hidden, the prompt should still be worked out.");
            tracker.Heard(MeridianMotion.Characters.MovementEvents.Jumped, default);
            Assert.That(tracker.IsCompleted("jump"), Is.True, "Hidden, the lessons should still be followed.");
        }
    }
}
