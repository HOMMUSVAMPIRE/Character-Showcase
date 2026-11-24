using System.Collections;
using System.Collections.Generic;
using MeridianMotion.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MeridianMotion.Tests.UI
{
    /// <summary>
    /// Tests of the helpers the UI is built on (UIUtility: UI_Framework_Blueprint, Building it, step 1), each safe on a
    /// null or destroyed object, and of its clock (UIClock).
    /// </summary>
    [Category(TestArea.UI)]
    public class UIUtilityTests : UITestsBase
    {
        [Test]
        public void SetActive_ShowsAndHides_AndDoesNothingToANullOrDestroyedObject()
        {
            var obj = Track(new GameObject("Shown"));
            Transform shown = obj.transform;
            UIUtility.SetActive(shown, false);
            Assert.That(obj.activeSelf, Is.False);
            UIUtility.SetActive(shown, true);
            Assert.That(obj.activeSelf, Is.True);

            Object.DestroyImmediate(obj);
            Assert.DoesNotThrow(() => UIUtility.SetActive(shown, true), "A destroyed component's object should be left alone.");
            Assert.DoesNotThrow(() => UIUtility.SetActive((Component)null, true));
            Assert.DoesNotThrow(() => UIUtility.SetActive((GameObject)null, false));
        }

        [Test]
        public void CountOrZero_CountsAList_AndANullOneIsEmpty()
        {
            Assert.That(new List<int> { 1, 2, 3 }.CountOrZero(), Is.EqualTo(3));
            Assert.That(((List<int>)null).CountOrZero(), Is.EqualTo(0));
            Assert.That(((int[])null).CountOrZero(), Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator WorldToCanvas_PutsWhatTheCameraLooksAtInTheCanvassMiddle()
        {
            var cameraObject = Track(new GameObject("UI Test Camera"));
            var camera = cameraObject.AddComponent<Camera>();
            yield return Frames();
            var canvas = (RectTransform)manager.inWorldCanvas;
            Vector2 middle = UIUtility.WorldToCanvas(camera, cameraObject.transform.position + Vector3.forward * 10f, canvas);
            Assert.That(middle.magnitude, Is.LessThan(1f), "What the camera looks at should be the canvas's middle (its pivot).");
            Vector2 right = UIUtility.WorldToCanvas(camera, cameraObject.transform.position + new Vector3(2f, 0f, 10f), canvas);
            Assert.That(right.x, Is.GreaterThan(1f), "Something to the camera's right should be to the canvas's right.");
            Assert.That(UIUtility.IsInFrontOf(camera, cameraObject.transform.position - Vector3.forward), Is.False);
            Assert.That(UIUtility.WorldToCanvas(null, Vector3.zero, canvas), Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void TheUIClock_GoesByItsTimeSource_ElseUnscaledTime()
        {
            Now = 12.5f;
            Assert.That(UIClock.Now, Is.EqualTo(12.5f));
            UIClock.TimeSource = null;
            Assert.That(UIClock.Now, Is.EqualTo(Time.unscaledTime));
        }
    }
}
