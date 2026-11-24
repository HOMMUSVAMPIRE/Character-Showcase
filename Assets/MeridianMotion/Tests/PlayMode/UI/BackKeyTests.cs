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
    /// Tests of the Back key (UI_Framework_Blueprint, Opening a window, and the Back key, and Building it, step 4): it goes
    /// to the top window that takes it, closing the windows from the top down, then runs the fallback handler; a window's
    /// Cancel Action Overrides run instead of closing it; a window that doesn't take Back, a hidden one and a closing one
    /// are passed over (a hidden one taking it again once shown); a text field with the keyboard keeps it; and the
    /// manager tells the game whether the shown windows need the pointer or stop its input, as they open and close.
    /// </summary>
    [Category(TestArea.UI)]
    public class BackKeyTests : UITestsBase
    {
        /// <summary>Counts what a window's Cancel Action Overrides run (a persistent listener needs an object to call).</summary>
        private class BackCounter : MonoBehaviour
        {
            public int count;
            public void Count() => count++;
        }

        [UnityTest]
        public IEnumerator Back_ClosesTheWindowsFromTheTopDown_ThenRunsTheFallback()
        {
            int fallbacks = 0;
            manager.RegisterFallbackHandler(() => fallbacks++);
            RootUIPrefab bottom = Open(Template("Bottom"));
#if UNITY_EDITOR
            // The top one plays a long close, so it's still there, closing, when Back is pressed again.
            RootUIPrefab top = Open(AnimatedTemplate(CloseController(10f)));
            yield return Frames();
#else
            RootUIPrefab top = Open(Template("Top"));
#endif

            manager.HandleBack();
            Assert.That(top.IsClosing, Is.True, "Back should close the top window first.");
            Assert.That(bottom.IsClosing, Is.False);
            Assert.That(Gone(top), Is.False, "Test setup: the top window should still be closing.");
            manager.HandleBack();
            Assert.That(bottom.IsClosing, Is.True, "Then the next, without waiting for the first to be gone.");
            Assert.That(fallbacks, Is.EqualTo(0));
            top.Animator_OnCloseComplete();
            yield return Frames();
            manager.HandleBack();
            Assert.That(fallbacks, Is.EqualTo(1), "With no window to take it, Back should run the fallback.");
        }

#if UNITY_EDITOR
        [UnityTest]
        public IEnumerator AWindowsCancelActionOverrides_RunInsteadOfClosingIt()
        {
            RootUIPrefab template = Template("Paged Window");
            var counter = template.gameObject.AddComponent<BackCounter>();
            UnityEditor.Events.UnityEventTools.AddVoidPersistentListener(template.CancelActionOverrides, counter.Count);
            RootUIPrefab window = Open(template);
            Assert.That(window.HasCancelAction, Is.True);

            manager.HandleBack();
            Assert.That(window.IsClosing, Is.False, "With overrides, Back shouldn't close it.");
            Assert.That(window.GetComponent<BackCounter>().count, Is.EqualTo(1), "Back should run its overrides.");
            yield return null;
        }
#endif

        [UnityTest]
        public IEnumerator AWindowThatDoesntTakeBack_IsPassedOver()
        {
            RootUIPrefab below = Open(Template("Below"));
            RootUIPrefab hudTemplate = Template("HUD");
            hudTemplate.CloseOnCancelButton = false;
            RootUIPrefab hud = Open(hudTemplate);
            Assert.That(hud.HasCancelAction, Is.False);
            Assert.That(manager.TopWindow, Is.SameAs(below), "The window that doesn't take Back shouldn't be the top for it.");
            manager.HandleBack();
            Assert.That(below.IsClosing, Is.True);
            Assert.That(hud.IsClosing, Is.False);
            yield return null;
        }

        [UnityTest]
        public IEnumerator AHiddenWindow_IsPassedOver_UntilItsShownAgain()
        {
            int fallbacks = 0;
            manager.RegisterFallbackHandler(() => fallbacks++);
            RootUIPrefab window = Open(Template("Window"));
            UIManager.HiddenUIState hidden = manager.HideAllUI();
            manager.HandleBack();
            Assert.That(window.IsClosing, Is.False, "Hidden, it shouldn't take Back.");
            Assert.That(fallbacks, Is.EqualTo(1));

            manager.RestorePreviouslyShownUI(hidden);
            manager.HandleBack();
            Assert.That(window.IsClosing, Is.True, "Shown again, it should take Back again.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator ATextFieldWithTheKeyboard_KeepsTheBackKey()
        {
            RootUIPrefab window = Open(Template("Form"));
            var fieldObject = new GameObject("Field", typeof(RectTransform));
            fieldObject.transform.SetParent(window.transform, false);
            var textObject = new GameObject("Text", typeof(RectTransform));
            textObject.transform.SetParent(fieldObject.transform, false);
            var text = textObject.AddComponent<TextMeshProUGUI>();
            var field = fieldObject.AddComponent<TMP_InputField>();
            field.textViewport = (RectTransform)fieldObject.transform;
            field.textComponent = text;
            EventSystem.current.SetSelectedGameObject(fieldObject);
            field.ActivateInputField();
            for (int i = 0; i < 5 && !field.isFocused; i++) { yield return null; }
            Assume.That(field.isFocused, Is.True, "Test setup: the field couldn't take the keyboard here.");
            Assert.That(UIManager.IsFocusedOnText(), Is.True);

            manager.HandleBack();
            Assert.That(window.IsClosing, Is.False, "While a text field has the keyboard, Back should be the field's.");
        }

        [UnityTest]
        public IEnumerator TheGameIsTold_WhetherTheShownWindowsNeedThePointerOrStopItsInput()
        {
            var cursor = new List<bool>();
            var input = new List<bool>();
            manager.OnCursorNeededChanged.AddListener(cursor.Add);
            manager.OnGameInputBlockedChanged.AddListener(input.Add);

            RootUIPrefab labelTemplate = Template("Label");
            labelTemplate.NeedsCursor = false;
            Open(labelTemplate);
            yield return Frames();
            Assert.That(cursor, Is.Empty, "A window that doesn't need the pointer shouldn't ask for it.");

            RootUIPrefab modalTemplate = Template("Modal");
            modalTemplate.BlocksGameInput = true;
            RootUIPrefab modal = Open(modalTemplate);
            yield return Frames();
            Assert.That(cursor, Is.EqualTo(new[] { true }));
            Assert.That(input, Is.EqualTo(new[] { true }));
            Assert.That(manager.CursorNeeded && manager.GameInputBlocked, Is.True);

            modal.Close();
            yield return Frames();
            Assert.That(cursor, Is.EqualTo(new[] { true, false }), "Closed, the pointer should be let go.");
            Assert.That(input, Is.EqualTo(new[] { true, false }), "And the game's input given back.");
        }
    }
}
