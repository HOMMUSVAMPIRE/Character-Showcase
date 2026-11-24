using System.Collections;
using MeridianMotion.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MeridianMotion.Tests.UI
{
    /// <summary>
    /// Tests of windows anchored to the world (FollowWorldTransformUIPrefab and its safe-area variant: UI_Framework_Blueprint,
    /// Window classes, and Building it, step 9): a label following a moving object across its canvas, hidden while the
    /// object's behind the camera, closed once the object's destroyed; and the safe-area variant kept on the screen when
    /// its object's off it.
    /// </summary>
    [Category(TestArea.UI)]
    public class WorldLabelTests : UITestsBase
    {
        private class Label : FollowWorldTransformUIPrefab
        {
            public void Follow(Transform target, Camera camera) => Initialize(target, camera);
        }

        private class EdgeLabel : FollowWorldTransformWithSafeAreaUIPrefab
        {
            public void Follow(Transform target, Camera camera) => Initialize(target, camera);
        }

        private Camera view;
        private Transform target;

        [SetUp]
        public void SetUpWorld()
        {
            view = Track(new GameObject("Label Test Camera")).AddComponent<Camera>();
            target = Track(new GameObject("Target")).transform;
            target.position = new Vector3(0f, 0f, 10f);
        }

        private T Open<T>(string name) where T : RootUIPrefab
        {
            T template = Template<T>(name, new Vector2(80f, 30f));
            template.ParentCanvas = UICanvas.InWorld;
            template.CloseOnCancelButton = false;
            template.NeedsCursor = false;
            return manager.OpenUI<T>(template.gameObject);
        }

        [UnityTest]
        public IEnumerator ALabel_FollowsItsObject_HidesBehindTheCamera_AndClosesWithIt()
        {
            yield return null;
            Label label = Open<Label>("Name Tag");
            label.Follow(target, view);
            Vector3 middle = label.transform.localPosition;
            target.position = new Vector3(2f, 0f, 10f);
            yield return null;
            Assert.That(label.transform.localPosition.x, Is.GreaterThan(middle.x + 1f), "It should follow its object across the canvas.");
            Assert.That(label.OnScreen, Is.True);

            target.position = new Vector3(0f, 0f, -10f);
            yield return null;
            Assert.That(label.OnScreen, Is.False, "Behind the camera, it should hide.");
            Assert.That(label.GetComponent<CanvasGroup>().alpha, Is.EqualTo(0f));

            Object.Destroy(target.gameObject);
            yield return Frames(3);
            Assert.That(Gone(label), Is.True, "Its object gone, it should close.");
        }

        [UnityTest]
        public IEnumerator TheSafeAreaVariant_StaysOnTheScreen()
        {
            yield return null;
            EdgeLabel label = Open<EdgeLabel>("Edge Tag");
            target.position = new Vector3(1000f, 0f, 10f); // Far off to the right, still in front.
            label.Follow(target, view);
            yield return null;
            var canvas = (RectTransform)manager.inWorldCanvas;
            float right = canvas.rect.width / 2f;
            Assert.That(label.transform.localPosition.x + 40f, Is.LessThanOrEqualTo(right + 0.01f), "It should be kept inside the screen.");
        }
    }
}
