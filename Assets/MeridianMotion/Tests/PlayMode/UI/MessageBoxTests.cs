using System.Collections;
using MeridianMotion.UI;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

namespace MeridianMotion.Tests.UI
{
    /// <summary>
    /// Tests of the modal dialogs (UIMessageBox and UIMessageBox_PromptForString: UI_Framework_Blueprint, Modal dialogs,
    /// and Building it, step 5): each helper opens its prefab (the manager's) with the text and returns at once; a button
    /// runs its callback and the dialog closes itself, its blocker with it; a second press while it closes counts once;
    /// Enter presses OK; Back declines a yes/no dialog and dismisses an OK one; the prompt passes on what's typed (Enter in
    /// its field included); and without a manager, opening one says so.
    /// </summary>
    [Category(TestArea.UI)]
    public class MessageBoxTests : UITestsBase
    {
        private UIMessageBox messageBox, yesNo;
        private UIMessageBox_PromptForString prompt;

        [SetUp]
        public void SetUpDialogs()
        {
            Blocker(ClickBlockStyle.DimBackground);
            messageBox = Dialog<UIMessageBox>("Message Box");
            manager.MessageBoxPrefab = messageBox.gameObject;
            manager.ErrorMessageBoxPrefab = Dialog<UIMessageBox>("Error Message Box").gameObject;
            yesNo = Dialog<UIMessageBox>("Yes No Dialog");
            manager.YesNoDialogPrefab = yesNo.gameObject;
            prompt = Dialog<UIMessageBox_PromptForString>("Prompt For String");
            var textObject = new GameObject("Text", typeof(RectTransform));
            textObject.transform.SetParent(prompt.transform, false);
            var field = prompt.gameObject.AddComponent<TMP_InputField>();
            field.textViewport = (RectTransform)prompt.transform;
            field.textComponent = textObject.AddComponent<TextMeshProUGUI>();
            prompt.InputField = field;
            manager.PromptForStringPrefab = prompt.gameObject;
        }

        private T Dialog<T>(string name) where T : UIMessageBox
        {
            T dialog = Template<T>(name, new Vector2(400f, 200f));
            dialog.ClickBlockerStyle = ClickBlockStyle.DimBackground;
            dialog.BlocksGameInput = true;
            return dialog;
        }

        [UnityTest]
        public IEnumerator Open_ShowsTheText_ReturnsAtOnce_AndOKRunsTheCallbackAndClosesIt()
        {
            int okayed = 0;
            UIMessageBox box = UIMessageBox.Open("Progress saved.", () => okayed++);
            Assert.That(box, Is.Not.Null);
            Assert.That(box.Text, Is.EqualTo("Progress saved."));
            Assert.That(okayed, Is.EqualTo(0), "Opening shouldn't wait for an answer.");
            Assert.That(UIManager.ClickBlockerCount, Is.EqualTo(1), "Its blocker should keep the rest of the screen from being clicked.");

            box.OnOkButtonClicked();
            box.OnOkButtonClicked();
            Assert.That(okayed, Is.EqualTo(1), "A second press while it closes should count once.");
            yield return Frames();
            Assert.That(Gone(box), Is.True, "It should close itself.");
            Assert.That(UIManager.ClickBlockerCount, Is.EqualTo(0), "With its blocker.");
        }

        [UnityTest]
        public IEnumerator AYesNoDialog_GivesTheAnswer_AndBackDeclines()
        {
            int yes = 0, no = 0;
            UIMessageBox box = UIMessageBox.OpenYesNoDialog("Discard changes?", () => yes++, () => no++);
            box.OnDeclineButtonClicked();
            Assert.That((yes, no), Is.EqualTo((0, 1)));
            yield return Frames();

            UIMessageBox.OpenYesNoDialog("Discard changes?", () => yes++, () => no++).OnOkButtonClicked();
            Assert.That((yes, no), Is.EqualTo((1, 1)));
            yield return Frames();

            UIMessageBox.OpenYesNoDialog("Discard changes?", () => yes++, () => no++);
            manager.HandleBack();
            Assert.That((yes, no), Is.EqualTo((1, 2)), "Back should answer No.");
        }

        [UnityTest]
        public IEnumerator Enter_PressesOK_AndBackDismissesAnOKDialog()
        {
            int okayed = 0;
            UIMessageBox.Open("Saved.", () => okayed++);
            manager.HandleSubmit();
            Assert.That(okayed, Is.EqualTo(1), "Enter should press OK.");
            yield return Frames();

            UIMessageBox error = UIMessageBox.OpenError("Couldn't save.", () => okayed++);
            Assert.That(error.name, Is.EqualTo("Error Message Box"), "OpenError should open the error dialog's prefab.");
            manager.HandleBack();
            Assert.That(okayed, Is.EqualTo(2), "Back should dismiss a dialog with only OK, so its caller still hears.");
        }

        [UnityTest]
        public IEnumerator ThePrompt_PassesOnWhatsTyped_FromOKOrEnterInItsField()
        {
            string submitted = null;
            bool cancelled = false;
            UIMessageBox_PromptForString box = UIMessageBox_PromptForString.Open("Name this tuning", s => submitted = s, () => cancelled = true, "Fast");
            Assert.That(box.Value, Is.EqualTo("Fast"), "It should start with the text given.");
            box.Value = "Floaty";
            box.OnOkButtonClicked();
            Assert.That(submitted, Is.EqualTo("Floaty"));
            yield return Frames();

            box = UIMessageBox_PromptForString.Open("Name this tuning", s => submitted = s, () => cancelled = true);
            box.Value = "Heavy";
            box.InputField.onSubmit.Invoke(box.Value);
            Assert.That(submitted, Is.EqualTo("Heavy"), "Enter in the field should press OK.");
            yield return Frames();

            box = UIMessageBox_PromptForString.Open("Name this tuning", s => submitted = s, () => cancelled = true);
            box.OnDeclineButtonClicked();
            Assert.That(cancelled, Is.True);
            Assert.That(submitted, Is.EqualTo("Heavy"), "Cancelled, nothing should be passed on.");
        }

        [UnityTest]
        public IEnumerator WithoutAManager_OpeningOneSaysSo()
        {
            Object.Destroy(manager.gameObject);
            yield return null;
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("no UIManager"));
            Assert.That(UIMessageBox.Open("Hello"), Is.Null);
        }
    }
}
