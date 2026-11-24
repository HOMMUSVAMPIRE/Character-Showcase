using System.Collections;
using MeridianMotion.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace MeridianMotion.Tests.UI
{
    /// <summary>
    /// Tests of rule-gated controls (RulesService, PermissionVisibility, PermissionInteractability: UI_Framework_Blueprint,
    /// Rule-gated controls, and Building it, step 8): a rule as its default until the game sets it; a gated control right
    /// the first time it shows; changing a rule at runtime showing, hiding or greying out controls with no window opened
    /// again, and telling listeners; both the feature and the permission needed; and Reset putting the rules back.
    /// </summary>
    [Category(TestArea.UI)]
    public class RulesTests : UITestsBase
    {
        private UIRule feature, permission;

        [SetUp]
        public void SetUpRules()
        {
            RulesService.Reset();
            feature = Track(ScriptableObject.CreateInstance<UIRule>());
            permission = Track(ScriptableObject.CreateInstance<UIRule>());
            permission.PermittedByDefault = false;
        }

        [TearDown]
        public void ResetRules() => RulesService.Reset();

        [UnityTest]
        public IEnumerator AGatedControl_IsRightAtOnce_AndFollowsTheRulesAsTheyChange()
        {
            var gate = Track(new GameObject("Gate"));
            var shown = new GameObject("Craft Button");
            shown.transform.SetParent(gate.transform, false);
            var visibility = gate.AddComponent<PermissionVisibility>();
            visibility.Target = shown;
            visibility.Feature = feature;
            visibility.Permission = permission;
            visibility.EvaluateResponse();
            Assert.That(shown.activeSelf, Is.False, "Its permission isn't granted by default: hidden at once.");

            int told = 0;
            void Count() => told++;
            RulesService.OnSelfPermissionsUpdated += Count;
            RulesService.Set(permission, true);
            RulesService.OnSelfPermissionsUpdated -= Count;
            Assert.That(shown.activeSelf, Is.True, "Granted, it should show, without anything opened again.");
            Assert.That(told, Is.EqualTo(1), "Listeners should hear.");

            RulesService.Set(feature, false);
            Assert.That(shown.activeSelf, Is.False, "Both the feature and the permission are needed.");
            RulesService.Reset();
            Assert.That(shown.activeSelf, Is.False, "Reset, the permission is back to its default: not granted.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator AnInteractabilityGate_GreysOutItsControl()
        {
            var obj = Track(new GameObject("Buy Button", typeof(RectTransform), typeof(Image), typeof(Button)));
            var gate = obj.AddComponent<PermissionInteractability>();
            gate.Feature = feature;
            yield return null;
            Assert.That(obj.GetComponent<Button>().interactable, Is.True);
            RulesService.Set(feature, false);
            Assert.That(obj.GetComponent<Button>().interactable, Is.False);
            Assert.That(obj.activeSelf, Is.True, "Greyed out, not hidden.");
            Object.DestroyImmediate(obj);
            Assert.DoesNotThrow(() => RulesService.Set(feature, true), "A destroyed control should have left the service.");
        }
    }
}
