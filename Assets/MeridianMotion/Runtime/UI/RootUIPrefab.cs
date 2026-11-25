using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace MeridianMotion.UI
{
    /// <summary>
    /// The base of every top-level window (UI_Framework_Blueprint, Window classes), set up in the inspector: the canvas it
    /// goes on, whether several copies may be open, the click blocker behind it and what the Back key does. Open one with
    /// UIManager.OpenUI (by convention through a static Open(...) on the window's own class, which then calls its own
    /// Initialize), never by instantiating it.
    ///
    /// Its life (the blueprint's Window lifecycle): opening it raises OnOpened, with its blocker opened behind it and it in
    /// front; HideAllUI hides it, keeping its state, and RestorePreviouslyShownUI shows it again; Close() raises OnClosing,
    /// closes its blocker, plays the Animator's close (its "Close" trigger, into a state tagged "Close") if it has one,
    /// then raises OnClosed and destroys it. Closed windows aren't kept: the next OpenUI makes a new one. A window needn't
    /// derive this: a component of its own beside it on the prefab is what OpenUI&lt;T&gt; returns.
    /// </summary>
    [AddComponentMenu("Meridian Motion/UI/Root UI Prefab")]
    [DisallowMultipleComponent]
    public class RootUIPrefab : MonoBehaviour
    {
        /// <summary>The Animator trigger that plays a window's close.</summary>
        public const string CloseTrigger = "Close";
        /// <summary>The tag of the Animator state a window's close plays (it's closed once that state has played through).</summary>
        public const string CloseStateTag = "Close";
        // A close animation that hasn't finished in this long (s, UIClock) is taken as finished: a controller with the
        // trigger but no state tagged Close would otherwise leave the window closing for ever.
        private const float MaxCloseSeconds = 5f;

        [Header("Window")]
        [Tooltip("The canvas the window goes on: Default for windows, Overlay for what's over every window, InWorld for labels in the world.")]
        [SerializeField] private UICanvas parentCanvas = UICanvas.Default;
        [Tooltip("Whether opening it while it's open makes another copy, rather than bringing the open one to the front")]
        [SerializeField] private bool allowMultiple;
        [Tooltip("Whether it stays open when the UIManager closes every window (CloseAllRootUIs): a HUD, say")]
        [SerializeField] private bool exemptFromAutoClose;

        [Header("Back key")]
        [Tooltip("Whether the Back key closes it when it's the top window that takes Back")]
        [SerializeField] private bool closeOnCancelButton = true;
        [Tooltip("What the Back key does instead of closing it (go back a page, say): when anything is set here, Back runs it rather than closing")]
        [SerializeField] private UnityEvent cancelActionOverrides = new();

        [Header("Click blocker")]
        [Tooltip("What's put behind it to stop clicks reaching the windows (and the game) under it")]
        [SerializeField] private ClickBlockStyle clickBlockerStyle = ClickBlockStyle.None;

        [Header("Animation")]
        [Tooltip("Plays its opening and closing, in unscaled time (so it runs while the game is paused): its \"Close\" trigger into a state tagged \"Close\". Found on the window if left empty; none closes at once")]
        [SerializeField] private Animator animator;

        [Header("Gamepad and keyboard")]
        [Tooltip("What's selected first when it's navigated by a gamepad or the keyboard and nothing of it is (empty: its first control that can be used)")]
        [SerializeField] private Selectable firstSelected;

        [Header("The game")]
        [Tooltip("Whether the pointer is needed while it's open: the UIManager tells the game (OnCursorNeededChanged), which frees the cursor")]
        [SerializeField] private bool needsCursor = true;
        [Tooltip("Whether the game's own input stops while it's open (a modal dialog, a pause menu): the UIManager tells the game (OnGameInputBlockedChanged)")]
        [SerializeField] private bool blocksGameInput;

        /// <summary>The canvas it goes on (see UICanvas; None is the default canvas).</summary>
        public UICanvas ParentCanvas { get => parentCanvas; set => parentCanvas = value; }
        /// <summary>The Animator playing its open and close, or null.</summary>
        public Animator Animator { get => animator; set => animator = value; }
        /// <summary>Whether opening it while it's open makes another copy.</summary>
        public bool AllowMultiple { get => allowMultiple; set => allowMultiple = value; }
        /// <summary>Whether the Back key closes it (unless CancelActionOverrides does something else).</summary>
        public bool CloseOnCancelButton { get => closeOnCancelButton; set => closeOnCancelButton = value; }
        /// <summary>What Back does instead of closing it, when anything's set in it in the inspector.</summary>
        public UnityEvent CancelActionOverrides => cancelActionOverrides;
        /// <summary>The click blocker behind it.</summary>
        public ClickBlockStyle ClickBlockerStyle { get => clickBlockerStyle; set => clickBlockerStyle = value; }
        /// <summary>Whether CloseAllRootUIs leaves it open.</summary>
        public bool ExemptFromAutoClose { get => exemptFromAutoClose; set => exemptFromAutoClose = value; }
        /// <summary>Whether the pointer is needed while it's open.</summary>
        public bool NeedsCursor { get => needsCursor; set => needsCursor = value; }
        /// <summary>Whether the game's own input stops while it's open.</summary>
        public bool BlocksGameInput { get => blocksGameInput; set => blocksGameInput = value; }
        /// <summary>What's selected first when it's navigated by a gamepad or the keyboard (null: its first control that can be used).</summary>
        public Selectable FirstSelected { get => firstSelected; set => firstSelected = value; }

        /// <summary>
        /// What a gamepad or the keyboard starts from in it: its First Selected, if that can be used now, else its first
        /// control (in the hierarchy's order) that's shown and can be used; null if it has none.
        /// </summary>
        public Selectable FirstSelectable()
        {
            if (firstSelected != null && firstSelected.isActiveAndEnabled && firstSelected.IsInteractable()) { return firstSelected; }
            foreach (Selectable selectable in GetComponentsInChildren<Selectable>())
            {
                if (selectable.IsInteractable() && selectable.navigation.mode != Navigation.Mode.None) { return selectable; }
            }
            return null;
        }

        /// <summary>The click blocker open behind it, or null.</summary>
        public RootUIPrefab BlockerPrefab { get; private set; }
        /// <summary>Whether it's closing (Close was called): it's destroyed once its close has played.</summary>
        public bool IsClosing { get; private set; }
        /// <summary>Whether the Back key does anything to it: closes it, or runs CancelActionOverrides.</summary>
        public bool HasCancelAction => closeOnCancelButton || HasCancelOverrides;
        /// <summary>Whether it's a click blocker, opened behind another window.</summary>
        public bool IsBlocker { get; private set; }

        /// <summary>Raised as it opens, or is shown again after HideAllUI.</summary>
        public event Action<RootUIPrefab> OnOpened;
        /// <summary>Raised as its closing starts (Close).</summary>
        public event Action<RootUIPrefab> OnClosing;
        /// <summary>Raised as its closing ends, just before it's destroyed.</summary>
        public event Action<RootUIPrefab> OnClosed;

        /// <summary>
        /// Raised with any window as it opens, or is shown again after HideAllUI (with its own OnOpened): for what reacts
        /// to the UI as a whole without knowing its windows (sound, say). Static, so a listener stops listening as it goes.
        /// </summary>
        public static event Action<RootUIPrefab> AnyOpened;
        /// <summary>Raised with any window as its closing ends (with its own OnClosed). See AnyOpened.</summary>
        public static event Action<RootUIPrefab> AnyClosed;

        private bool HasCancelOverrides => cancelActionOverrides != null && cancelActionOverrides.GetPersistentEventCount() > 0;
        private bool animatingClose, closed;
        private float closeStartedAt;

        protected virtual void Awake()
        {
            if (animator == null) { animator = GetComponent<Animator>(); }
            if (animator != null) { animator.updateMode = AnimatorUpdateMode.UnscaledTime; }
        }

        protected virtual void OnEnable()
        {
            if (IsClosing) { return; }
            OpenBlocker();
            BringToFront();
            if (UIManager.instance != null) { UIManager.instance.Track(this); }
            OnOpened?.Invoke(this);
            AnyOpened?.Invoke(this);
        }

        protected virtual void OnDisable()
        {
            CloseBlocker();
        }

        protected virtual void OnDestroy()
        {
            CloseBlocker();
            if (UIManager.instance != null) { UIManager.instance.Untrack(this); }
        }

        protected virtual void Update()
        {
            if (!animatingClose) { return; }
            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
            bool played = !animator.IsInTransition(0) && state.IsTag(CloseStateTag) && state.normalizedTime >= 1f;
            if (played || UIClock.Now - closeStartedAt > MaxCloseSeconds) { FinishClose(); }
        }

        /// <summary>
        /// Closes it: raises OnClosing, closes its blocker, plays its close if its Animator has one, then raises OnClosed
        /// and destroys it. Nothing if it's already closing, so clicks during the close do nothing more.
        /// </summary>
        public void Close()
        {
            if (IsClosing) { return; }
            IsClosing = true;
            OnClosing?.Invoke(this);
            CloseBlocker();
            if (animator != null && animator.isActiveAndEnabled && HasTrigger(animator, CloseTrigger))
            {
                animatingClose = true;
                closeStartedAt = UIClock.Now;
                animator.SetTrigger(CloseTrigger);
            }
            else
            {
                FinishClose();
            }
        }

        /// <summary>
        /// What the Back key does to it as the top window that takes Back: runs CancelActionOverrides when anything's set
        /// in it, else closes it.
        /// </summary>
        public virtual void OnCancelButton()
        {
            if (HasCancelOverrides) { cancelActionOverrides.Invoke(); }
            else { Close(); }
        }

        /// <summary>What the Submit key (Enter) does to it as the top window: nothing, unless a window says (a dialog presses OK).</summary>
        public virtual void OnSubmitButton()
        {
        }

        /// <summary>Opens its click blocker (ClickBlockerStyle) behind it, on its canvas, if it has none open.</summary>
        public void OpenBlocker()
        {
            if (clickBlockerStyle == ClickBlockStyle.None || BlockerPrefab != null || IsBlocker || UIManager.instance == null) { return; }
            GameObject prefab = UIManager.instance.ClickBlockerPrefab(clickBlockerStyle);
            if (prefab == null) { return; }
            RootUIPrefab blocker = UIManager.instance.OpenUI<RootUIPrefab>(prefab);
            if (blocker == null) { return; }
            blocker.IsBlocker = true;
            blocker.transform.SetParent(transform.parent, false);
            BlockerPrefab = blocker;
            BringToFront();
        }

        /// <summary>For an Animator's open animation, as an animation event: its open has played (nothing more to do yet).</summary>
        public virtual void Animator_OnOpenComplete()
        {
        }

        /// <summary>For an Animator's close animation, as an animation event: its close has played, so it's destroyed now.</summary>
        public void Animator_OnCloseComplete()
        {
            if (IsClosing) { FinishClose(); }
        }

        /// <summary>Puts it in front of the other windows on its canvas, its blocker just behind it.</summary>
        internal void BringToFront()
        {
            if (BlockerPrefab != null && BlockerPrefab.transform.parent == transform.parent) { BlockerPrefab.transform.SetAsLastSibling(); }
            transform.SetAsLastSibling();
            if (UIManager.instance != null && isActiveAndEnabled) { UIManager.instance.Track(this); }
        }

        private void CloseBlocker()
        {
            if (BlockerPrefab == null) { return; }
            RootUIPrefab blocker = BlockerPrefab;
            BlockerPrefab = null;
            blocker.Close();
        }

        private void FinishClose()
        {
            if (closed) { return; }
            closed = true;
            animatingClose = false;
            OnClosed?.Invoke(this);
            AnyClosed?.Invoke(this);
            Destroy(gameObject);
        }

        private static bool HasTrigger(Animator animator, string trigger)
        {
            if (animator.runtimeAnimatorController == null) { return false; }
            foreach (AnimatorControllerParameter parameter in animator.parameters)
            {
                if (parameter.type == AnimatorControllerParameterType.Trigger && parameter.name == trigger) { return true; }
            }
            return false;
        }
    }
}
