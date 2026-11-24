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
    }
}
