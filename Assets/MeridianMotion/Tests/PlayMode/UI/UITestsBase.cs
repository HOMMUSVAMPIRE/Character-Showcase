using System.Collections;
using System.Collections.Generic;
using MeridianMotion.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace MeridianMotion.Tests.UI
{
    /// <summary>
    /// What the UI's fixtures share: a UIManager made in code, with its three canvases and an EventSystem, no character
    /// and no prefab assets (the UI knows nothing of the game, as the camera's tests follow a plain Transform), and
    /// windows opened from templates made in code (Template: an inactive GameObject with a RootUIPrefab, which OpenUI
    /// copies as it would a prefab). The UI's clock (UIClock) is the test's own (Now), set back after.
    /// </summary>
    [TestTiming]
    public abstract class UITestsBase
    {
        protected readonly List<Object> created = new();
        protected UIManager manager;
        /// <summary>The UI's time, which a test moves on itself (UIClock.TimeSource reads it).</summary>
        protected float Now;

        [SetUp]
        public void SetUpManager()
        {
            Now = 0f;
            UIClock.TimeSource = () => Now;
            var root = Track(new GameObject("UI Manager"));
            root.SetActive(false); // Its fields first: it finds its canvases' scalers in Awake.
            manager = root.AddComponent<UIManager>();
            manager.inWorldCanvas = Canvas(root.transform, "In World Canvas", 0);
            manager.defaultCanvas = Canvas(root.transform, "Default Canvas", 10);
            manager.overlayCanvas = Canvas(root.transform, "Overlay Canvas", 20);
            var events = new GameObject("EventSystem", typeof(EventSystem));
            events.transform.SetParent(root.transform, false);
            root.SetActive(true);
        }

        [UnityTearDown]
        public IEnumerator TearDownManager()
        {
            foreach (Object obj in created) { if (obj != null) { Object.Destroy(obj); } }
            created.Clear();
            UIClock.TimeSource = null;
            Time.timeScale = 1f;
            yield return null;
        }

        protected T Track<T>(T obj) where T : Object
        {
            created.Add(obj);
            return obj;
        }

        private static Transform Canvas(Transform parent, string name, int order)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            obj.transform.SetParent(parent, false);
            var canvas = obj.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = order;
            return obj.transform;
        }

        /// <summary>
        /// A window template of the given type (RootUIPrefab or a subclass), inactive so it's never a window itself, with a
        /// RectTransform of the given size (full screen, by default) and an Image clicks land on.
        /// </summary>
        protected T Template<T>(string name, Vector2? size = null) where T : RootUIPrefab
        {
            var obj = Track(new GameObject(name, typeof(RectTransform), typeof(Image)));
            obj.SetActive(false);
            var rect = (RectTransform)obj.transform;
            if (size is { } fixedSize) { rect.sizeDelta = fixedSize; }
            else { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
            return obj.AddComponent<T>();
        }

        protected RootUIPrefab Template(string name, Vector2? size = null) => Template<RootUIPrefab>(name, size);

        /// <summary>A click blocker template (full screen, several allowed, not taking Back), set as the manager's for the style.</summary>
        protected RootUIPrefab Blocker(ClickBlockStyle style = ClickBlockStyle.DimBackground)
        {
            RootUIPrefab blocker = Template($"Click Blocker ({style})");
            blocker.AllowMultiple = true;
            blocker.CloseOnCancelButton = false;
            blocker.NeedsCursor = false;
            manager.SetClickBlockerPrefab(style, blocker.gameObject);
            return blocker;
        }

        /// <summary>Opens a window from its template, as the window's own class would (OpenUI).</summary>
        protected T Open<T>(Component template, UICanvas canvas = UICanvas.None) where T : Component =>
            manager.OpenUI<T>(template.gameObject, canvas);

        protected RootUIPrefab Open(Component template, UICanvas canvas = UICanvas.None) => Open<RootUIPrefab>(template, canvas);

        /// <summary>Waits a frame (for Destroy, and the manager's Update), then another.</summary>
        protected static IEnumerator Frames(int count = 2)
        {
            for (int i = 0; i < count; i++) { yield return null; }
        }

        /// <summary>Whether a window was destroyed (Unity's null).</summary>
        protected static bool Gone(Object obj) => obj == null;

#if UNITY_EDITOR
        // An Animator Controller whose "Close" trigger plays a state tagged "Close" of the given length (s), fading the
        // window's CanvasGroup out; the state's clip calls Animator_OnCloseComplete at its end if asked.
        protected RuntimeAnimatorController CloseController(float seconds, bool closeState = true)
        {
            var controller = Track(new UnityEditor.Animations.AnimatorController { name = "Window Animation" });
            controller.AddLayer("Base");
            controller.AddParameter(RootUIPrefab.CloseTrigger, AnimatorControllerParameterType.Trigger);
            UnityEditor.Animations.AnimatorStateMachine machine = controller.layers[0].stateMachine;
            UnityEditor.Animations.AnimatorState open = machine.AddState("Open");
            if (!closeState) { return controller; }
            var clip = Track(new AnimationClip { name = "Close" });
            clip.SetCurve("", typeof(CanvasGroup), "m_Alpha", AnimationCurve.Linear(0f, 1f, seconds, 0f));
            UnityEditor.Animations.AnimatorState close = machine.AddState("Close");
            close.tag = RootUIPrefab.CloseStateTag;
            close.motion = clip;
            UnityEditor.Animations.AnimatorStateTransition toClose = open.AddTransition(close);
            toClose.hasExitTime = false;
            toClose.duration = 0f;
            toClose.AddCondition(UnityEditor.Animations.AnimatorConditionMode.If, 0f, RootUIPrefab.CloseTrigger);
            return controller;
        }

        protected RootUIPrefab AnimatedTemplate(RuntimeAnimatorController controller)
        {
            RootUIPrefab template = Template("Animated Window");
            template.gameObject.AddComponent<CanvasGroup>();
            var animator = template.gameObject.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            template.Animator = animator;
            return template;
        }
#endif
    }
}
