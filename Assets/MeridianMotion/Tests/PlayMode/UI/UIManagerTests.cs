using System.Collections;
using MeridianMotion.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace MeridianMotion.Tests.UI
{
    /// <summary>
    /// Tests of the window manager (UIManager and RootUIPrefab: UI_Framework_Blueprint, Window classes, and Building it,
    /// step 2): a window opens on its canvas (or the one asked for); opened again, its open instance comes to the front
    /// rather than a copy (unless it allows several); closed, it's destroyed, and the next open makes another; GetUI,
    /// IsVisibleUI and CloseUIWithBasePrefab find it by its prefab; CloseAllRootUIs leaves the one excepted and those
    /// exempt; the window catalog's windows open by their component; a window's own component is what OpenUI returns;
    /// and the canvases scale together.
    /// </summary>
    [Category(TestArea.UI)]
    public class UIManagerTests : UITestsBase
    {
        /// <summary>A window's own class beside its RootUIPrefab (a plain MonoBehaviour, as the blueprint allows).</summary>
        private class SettingsWindow : MonoBehaviour
        {
            public int page;
        }

        [UnityTest]
        public IEnumerator AWindow_OpensOnItsCanvas_OrTheOneAskedFor()
        {
            RootUIPrefab template = Template("Overlay Window");
            template.ParentCanvas = UICanvas.Overlay;
            RootUIPrefab window = Open(template);
            Assert.That(window.transform.parent, Is.SameAs(manager.overlayCanvas));
            Assert.That(manager.prefabInstanceMap[template.gameObject], Is.SameAs(window));
            Assert.That(window.gameObject.activeInHierarchy, Is.True, "A template kept inactive should open shown.");

            RootUIPrefab other = Template("Default Window");
            other.ParentCanvas = UICanvas.None;
            Assert.That(Open(other).transform.parent, Is.SameAs(manager.defaultCanvas), "None should be the default canvas.");
            RootUIPrefab moved = Template("Moved Window");
            Assert.That(Open(moved, UICanvas.InWorld).transform.parent, Is.SameAs(manager.inWorldCanvas), "The override should win.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator OpeningItAgain_BringsTheOpenOneToTheFront_UnlessItAllowsSeveral()
        {
            RootUIPrefab first = Template("First");
            RootUIPrefab second = Template("Second");
            RootUIPrefab window = Open(first);
            Open(second);
            Assert.That(Open(first), Is.SameAs(window), "Opened again, the open one should come back, not a copy.");
            Assert.That(window.transform.GetSiblingIndex(), Is.EqualTo(window.transform.parent.childCount - 1), "It should be in front.");

            RootUIPrefab several = Template("Several");
            several.AllowMultiple = true;
            Assert.That(Open(several), Is.Not.SameAs(Open(several)), "One that allows several should open a copy each time.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Closing_DestroysIt_AndTheNextOpenMakesANewOne()
        {
            RootUIPrefab template = Template("Window");
            RootUIPrefab window = Open(template);
            window.Close();
            yield return Frames();
            Assert.That(Gone(window), Is.True, "Closed, it should be destroyed, not kept.");
            Assert.That(manager.GetUI<RootUIPrefab>(template.gameObject), Is.Null);
            RootUIPrefab again = Open(template);
            Assert.That(again, Is.Not.Null);
            Assert.That(Gone(again), Is.False, "Opened again, it should be a new one.");
        }

        [UnityTest]
        public IEnumerator GetUI_IsVisibleUI_AndCloseUIWithBasePrefab_FindItByItsPrefab()
        {
            RootUIPrefab template = Template("Window");
            Assert.That(manager.IsVisibleUI(template.gameObject), Is.False);
            Assert.That(manager.CloseUIWithBasePrefab(template.gameObject), Is.False, "Nothing to close yet.");
            RootUIPrefab window = Open(template);
            Assert.That(manager.GetUI<RootUIPrefab>(template.gameObject), Is.SameAs(window));
            Assert.That(manager.IsVisibleUI(template.gameObject), Is.True);
            Assert.That(manager.CloseUIWithBasePrefab(template.gameObject), Is.True);
            Assert.That(manager.IsVisibleUI(template.gameObject), Is.False, "Closing, it's no longer visible.");
            yield return Frames();
            Assert.That(Gone(window), Is.True);
        }

        [UnityTest]
        public IEnumerator CloseAllRootUIs_LeavesTheOneExcepted_AndThoseExempt()
        {
            RootUIPrefab a = Open(Template("A")), b = Open(Template("B"));
            RootUIPrefab hudTemplate = Template("HUD");
            hudTemplate.ExemptFromAutoClose = true;
            RootUIPrefab hud = Open(hudTemplate);
            manager.CloseAllRootUIs(except: b);
            yield return Frames();
            Assert.That(Gone(a), Is.True);
            Assert.That(Gone(b), Is.False, "The one excepted should stay.");
            Assert.That(Gone(hud), Is.False, "One exempt from it should stay.");
        }

        [UnityTest]
        public IEnumerator AWindowsOwnComponent_IsWhatOpenUIReturns_AndTheCatalogFindsItsPrefabByIt()
        {
            RootUIPrefab template = Template("Settings");
            template.gameObject.AddComponent<SettingsWindow>();
            SettingsWindow settings = manager.OpenUI<SettingsWindow>(template.gameObject);
            Assert.That(settings, Is.Not.Null, "OpenUI should return the window's own component beside its RootUIPrefab.");
            settings.page = 3;

            var catalog = Track(ScriptableObject.CreateInstance<UIWindowCatalog>());
            catalog.Windows.Add(Template("Unrelated").gameObject);
            catalog.Windows.Add(template.gameObject);
            manager.WindowCatalog = catalog;
            Assert.That(manager.Open<SettingsWindow>(), Is.SameAs(settings), "The catalog's window should be found by its component, and the open one returned.");
            Assert.That(settings.page, Is.EqualTo(3));
            yield return null;
        }

        [Test]
        public void APrefabThatIsntAWindow_IsntOpened_AndSaysSo()
        {
            var notAWindow = Track(new GameObject("Not A Window"));
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("needs a RootUIPrefab"));
            Assert.That(manager.OpenUI<RootUIPrefab>(notAWindow), Is.Null);
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("window catalog"));
            Assert.That(manager.Open<RootUIPrefab>(), Is.Null);
        }

        [UnityTest]
        public IEnumerator SetCanvasScale_ScalesEveryCanvas_FromHowItWasSetUp()
        {
            var scaler = manager.defaultCanvas.GetComponent<CanvasScaler>();
            float factor = scaler.scaleFactor;
            manager.SetCanvasScale(2f);
            Assert.That(scaler.scaleFactor, Is.EqualTo(2f * factor).Within(1e-5f));
            manager.SetCanvasScale(1f);
            Assert.That(scaler.scaleFactor, Is.EqualTo(factor).Within(1e-5f), "1 should put it back as it was set up.");
            yield return null;
        }
    }
}
