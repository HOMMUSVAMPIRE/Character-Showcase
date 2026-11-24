using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace MeridianMotion.UI
{
    /// <summary>
    /// The window manager (UI_Framework_Blueprint, Layers and Window classes): a scene singleton (instance) owning the three
    /// canvases windows go on, the window prefabs, and which instance of each is open (prefabInstanceMap). OpenUI&lt;T&gt;
    /// opens a window (bringing its open instance to the front, unless it allows several); the Back key goes to the top
    /// window that takes it, and with none, to the fallback handler (RegisterFallbackHandler: a pause menu, say).
    ///
    /// The UI knows nothing of the game. A project's own windows are listed in its window catalog (Open&lt;T&gt;); the
    /// framework's own (the dialogs, the click blockers) are fields here. What the game needs to follow, it's told by two
    /// events, OnCursorNeededChanged and OnGameInputBlockedChanged, which Set Up Scene connects to the camera and the
    /// player's input.
    /// </summary>
    [AddComponentMenu("Meridian Motion/UI/UI Manager")]
    [DisallowMultipleComponent]
    public partial class UIManager : MonoBehaviour
    {
        /// <summary>The scene's UI manager, or null (set in Awake; a second one disables itself).</summary>
        public static UIManager instance { get; private set; }

        /// <summary>Whether there's a UI manager, and it's running.</summary>
        public static bool Active => instance != null && instance.isActiveAndEnabled;

        /// <summary>How many click blockers are open.</summary>
        public static int ClickBlockerCount
        {
            get
            {
                if (instance == null) { return 0; }
                int count = 0;
                foreach (RootUIPrefab window in instance.openWindows)
                {
                    if (window != null && window.IsBlocker && window.isActiveAndEnabled && !window.IsClosing) { count++; }
                }
                return count;
            }
        }

        [Header("Canvases")]
        [Tooltip("The canvas for labels anchored to the world (behind the windows)")]
        public Transform inWorldCanvas;
        [Tooltip("The canvas windows go on")]
        public Transform defaultCanvas;
        [Tooltip("The canvas for what goes over every window: notifications, a context menu")]
        public Transform overlayCanvas;

        [Header("Windows")]
        [Tooltip("The project's own windows, which Open<T> finds by the component on their root")]
        [SerializeField] private UIWindowCatalog windowCatalog;
        [Tooltip("The dialog with an OK button (UIMessageBox.Open)")]
        [SerializeField] private GameObject messageBoxPrefab;
        [Tooltip("The dialog for an error, with an OK button (UIMessageBox.OpenError)")]
        [SerializeField] private GameObject errorMessageBoxPrefab;
        [Tooltip("The dialog with Yes and No buttons (UIMessageBox.OpenYesNoDialog)")]
        [SerializeField] private GameObject yesNoDialogPrefab;
        [Tooltip("The dialog asking for a line of text (UIMessageBox_PromptForString.Open)")]
        [SerializeField] private GameObject promptForStringPrefab;
        [Tooltip("The context menu (UIRightClickMenu.Open)")]
        [SerializeField] private GameObject rightClickMenuPrefab;

        [Header("Click blockers")]
        [Tooltip("Behind a window whose Click Blocker Style is Dim Background")]
        [SerializeField] private GameObject dimBlockerPrefab;
        [Tooltip("Behind a window whose Click Blocker Style is Invisible")]
        [SerializeField] private GameObject invisibleBlockerPrefab;
        [Tooltip("Behind a window whose Click Blocker Style is Darken Background")]
        [SerializeField] private GameObject darkenBlockerPrefab;
        [Tooltip("Behind a window whose Click Blocker Style is Blackout Background")]
        [SerializeField] private GameObject blackoutBlockerPrefab;

        [Header("Input")]
        [Tooltip("The Back key: the UI action map's Cancel. It's ignored while a text field has focus")]
        [SerializeField] private InputActionReference cancelAction;
        [Tooltip("The Submit key: the UI action map's Submit, which presses a dialog's OK")]
        [SerializeField] private InputActionReference submitAction;

        [Header("The game")]
        [Tooltip("Raised with true when a window that needs the pointer opens (none was open), and false when the last one closes: connect it to what frees the cursor (the camera's Cursor Requested)")]
        [SerializeField] private UnityEvent<bool> onCursorNeededChanged = new();
        [Tooltip("Raised with true when a window that stops the game's input opens (none was open), and false when the last one closes: connect it to what reads the player's input (its Suppressed)")]
        [SerializeField] private UnityEvent<bool> onGameInputBlockedChanged = new();

        /// <summary>The project's own windows (Open&lt;T&gt;).</summary>
        public UIWindowCatalog WindowCatalog { get => windowCatalog; set => windowCatalog = value; }
        /// <summary>The OK dialog's prefab.</summary>
        public GameObject MessageBoxPrefab { get => messageBoxPrefab; set => messageBoxPrefab = value; }
        /// <summary>The error dialog's prefab.</summary>
        public GameObject ErrorMessageBoxPrefab { get => errorMessageBoxPrefab; set => errorMessageBoxPrefab = value; }
        /// <summary>The yes/no dialog's prefab.</summary>
        public GameObject YesNoDialogPrefab { get => yesNoDialogPrefab; set => yesNoDialogPrefab = value; }
        /// <summary>The text prompt's prefab.</summary>
        public GameObject PromptForStringPrefab { get => promptForStringPrefab; set => promptForStringPrefab = value; }
        /// <summary>The context menu's prefab.</summary>
        public GameObject RightClickMenuPrefab { get => rightClickMenuPrefab; set => rightClickMenuPrefab = value; }
        /// <summary>Raised when whether a window needs the pointer changes.</summary>
        public UnityEvent<bool> OnCursorNeededChanged => onCursorNeededChanged;
        /// <summary>Raised when whether a window stops the game's input changes.</summary>
        public UnityEvent<bool> OnGameInputBlockedChanged => onGameInputBlockedChanged;

        /// <summary>The open (or last opened) instance of each window prefab: a closed one reads as null, so OpenUI makes another.</summary>
        public readonly Dictionary<GameObject, RootUIPrefab> prefabInstanceMap = new();

        // The windows that take the Back key, the latest on top (destroyed, hidden, closing and uncancellable ones are
        // taken off as they come to the top). Every window open, blockers included, for closing, hiding and the events.
        private readonly Stack<RootUIPrefab> cancelStack = new();
        private readonly List<RootUIPrefab> openWindows = new();
        private Action fallbackBackHandler;
        private bool cursorNeeded, gameInputBlocked;
        // The canvases' scalers, and how each was set up, for SetCanvasScale.
        private readonly List<(CanvasScaler scaler, float factor, Vector2 resolution)> scalers = new();

        protected virtual void Awake()
        {
            if (instance != null && instance != this)
            {
                Debug.LogWarning($"[UIManager] There's a UI manager already ('{instance.name}'): this one ('{name}') is turned off.", this);
                enabled = false;
                return;
            }
            instance = this;
            foreach (Transform canvas in new[] { inWorldCanvas, defaultCanvas, overlayCanvas })
            {
                CanvasScaler scaler = canvas != null ? canvas.GetComponent<CanvasScaler>() : null;
                if (scaler != null) { scalers.Add((scaler, scaler.scaleFactor, scaler.referenceResolution)); }
            }
        }

        protected virtual void OnEnable()
        {
            if (instance != this) { return; }
            cancelAction?.action?.Enable();
            submitAction?.action?.Enable();
        }

        protected virtual void OnDestroy()
        {
            if (instance == this) { instance = null; }
        }

        protected virtual void Update()
        {
            if (instance != this) { return; }
            if (cancelAction != null && cancelAction.action != null && cancelAction.action.WasPressedThisFrame()) { HandleBack(); }
            if (submitAction != null && submitAction.action != null && submitAction.action.WasPressedThisFrame()) { HandleSubmit(); }
            UpdateGameEvents();
        }

        // ---- Opening and closing ----

        /// <summary>
        /// Opens the window prefab (one with a RootUIPrefab on its root) and returns its component of type T (the window's
        /// own class, beside the RootUIPrefab or the same): the open instance, shown and brought to the front, unless the
        /// window allows several, else a new one on its canvas (canvasOverride's, if not None). A window that takes the
        /// Back key goes on top of the Back key's stack. Null, with an error, for a prefab that isn't a window.
        /// </summary>
        public T OpenUI<T>(GameObject prefab, UICanvas canvasOverride = UICanvas.None) where T : Component
        {
            if (prefab == null || !prefab.TryGetComponent(out RootUIPrefab root))
            {
                Debug.LogError($"[UIManager] Can't open '{(prefab != null ? prefab.name : "nothing")}': a window's prefab needs a RootUIPrefab on its root.", this);
                return null;
            }
            prefabInstanceMap.TryGetValue(prefab, out RootUIPrefab live);
            RootUIPrefab window;
            if (live != null && !live.IsClosing && !root.AllowMultiple)
            {
                window = live;
                if (!window.gameObject.activeSelf) { window.gameObject.SetActive(true); }
                else { window.BringToFront(); }
            }
            else
            {
                window = Instantiate(prefab, GetCanvasTransform(canvasOverride != UICanvas.None ? canvasOverride : root.ParentCanvas), false)
                    .GetComponent<RootUIPrefab>();
                window.name = prefab.name;
                // A prefab kept inactive (a template, say) opens all the same.
                if (!window.gameObject.activeSelf) { window.gameObject.SetActive(true); }
                prefabInstanceMap[prefab] = window;
            }
            if (window.HasCancelAction) { cancelStack.Push(window); }
            return window.GetComponent<T>();
        }

        /// <summary>Opens the window of the catalog whose root has a T (see OpenUI); null, with an error, if it lists none.</summary>
        public T Open<T>(UICanvas canvasOverride = UICanvas.None) where T : Component
        {
            GameObject prefab = windowCatalog != null ? windowCatalog.PrefabFor<T>() : null;
            if (prefab == null)
            {
                Debug.LogError($"[UIManager] No window in the window catalog has a {typeof(T).Name}: add its prefab to the catalog.", this);
                return null;
            }
            return OpenUI<T>(prefab, canvasOverride);
        }

        /// <summary>The window prefab's open instance's T, or null if it isn't open.</summary>
        public T GetUI<T>(GameObject prefab) where T : Component =>
            prefab != null && prefabInstanceMap.TryGetValue(prefab, out RootUIPrefab live) && live != null ? live.GetComponent<T>() : null;

        /// <summary>Whether the window prefab's instance is open and shown (not hidden, not closing).</summary>
        public bool IsVisibleUI(GameObject prefab) =>
            prefab != null && prefabInstanceMap.TryGetValue(prefab, out RootUIPrefab live) && live != null && live.isActiveAndEnabled && !live.IsClosing;

        /// <summary>Closes the window prefab's open instance; false if it wasn't open.</summary>
        public bool CloseUIWithBasePrefab(GameObject prefab)
        {
            if (prefab == null || !prefabInstanceMap.TryGetValue(prefab, out RootUIPrefab live) || live == null || live.IsClosing) { return false; }
            live.Close();
            return true;
        }

        /// <summary>Closes every open window but except, and those exempt from it (ExemptFromAutoClose). Blockers go with their windows.</summary>
        public void CloseAllRootUIs(RootUIPrefab except = null)
        {
            foreach (RootUIPrefab window in openWindows.ToArray())
            {
                if (window != null && window != except && !window.IsBlocker && !window.ExemptFromAutoClose && !window.IsClosing) { window.Close(); }
            }
        }

        /// <summary>Which windows HideAllUI hid, for RestorePreviouslyShownUI to show again.</summary>
        public sealed class HiddenUIState
        {
            internal readonly List<RootUIPrefab> windows = new();
        }

        /// <summary>Hides every shown window (keeping its state: for a cutscene, say), and returns which, to show again.</summary>
        public HiddenUIState HideAllUI()
        {
            var state = new HiddenUIState();
            foreach (RootUIPrefab window in openWindows)
            {
                if (window != null && !window.IsBlocker && !window.IsClosing && window.isActiveAndEnabled) { state.windows.Add(window); }
            }
            // Front ones last, so shown again in this order they're stacked as they were.
            state.windows.Sort((a, b) => a.transform.GetSiblingIndex().CompareTo(b.transform.GetSiblingIndex()));
            foreach (RootUIPrefab window in state.windows) { window.gameObject.SetActive(false); }
            return state;
        }

        /// <summary>Shows again the windows HideAllUI hid (those still there), as they were stacked.</summary>
        public void RestorePreviouslyShownUI(HiddenUIState state)
        {
            if (state == null) { return; }
            foreach (RootUIPrefab window in state.windows)
            {
                if (window == null || window.IsClosing) { continue; }
                window.gameObject.SetActive(true);
                if (window.HasCancelAction) { cancelStack.Push(window); }
            }
        }

        /// <summary>The canvas a window of the given choice goes on (None: the default canvas; this object, without one).</summary>
        public Transform GetCanvasTransform(UICanvas canvas)
        {
            Transform chosen = canvas switch
            {
                UICanvas.InWorld => inWorldCanvas,
                UICanvas.Overlay => overlayCanvas,
                _ => defaultCanvas,
            };
            return chosen != null ? chosen : transform;
        }

        /// <summary>The click blocker prefab for a style, or null for None (or one not set).</summary>
        public GameObject ClickBlockerPrefab(ClickBlockStyle style) => style switch
        {
            ClickBlockStyle.DimBackground => dimBlockerPrefab,
            ClickBlockStyle.Invisible => invisibleBlockerPrefab,
            ClickBlockStyle.DarkenBackground => darkenBlockerPrefab,
            ClickBlockStyle.BlackoutBackground => blackoutBlockerPrefab,
            _ => null,
        };

        /// <summary>Sets the click blocker prefab for a style (for a manager set up in code: the tests').</summary>
        public void SetClickBlockerPrefab(ClickBlockStyle style, GameObject prefab)
        {
            switch (style)
            {
                case ClickBlockStyle.DimBackground: dimBlockerPrefab = prefab; break;
                case ClickBlockStyle.Invisible: invisibleBlockerPrefab = prefab; break;
                case ClickBlockStyle.DarkenBackground: darkenBlockerPrefab = prefab; break;
                case ClickBlockStyle.BlackoutBackground: blackoutBlockerPrefab = prefab; break;
            }
        }

        /// <summary>
        /// Scales every canvas by the given factor of its scale as set up (1 is as set up): its Scale Factor, or for one
        /// scaling with the screen, its Reference Resolution, divided by it.
        /// </summary>
        public void SetCanvasScale(float scale)
        {
            if (scale <= 0f) { return; }
            foreach ((CanvasScaler scaler, float factor, Vector2 resolution) in scalers)
            {
                if (scaler == null) { continue; }
                if (scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize) { scaler.referenceResolution = resolution / scale; }
                else { scaler.scaleFactor = factor * scale; }
            }
        }

        /// <summary>Whether a text field has the keyboard (so the Back and Submit keys are the field's, not the windows').</summary>
        public static bool IsFocusedOnText()
        {
            GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (selected == null) { return false; }
            if (selected.TryGetComponent(out TMP_InputField textMeshField) && textMeshField.isFocused) { return true; }
            return selected.TryGetComponent(out InputField field) && field.isFocused;
        }

        // ---- What the windows tell it ----

        internal void Track(RootUIPrefab window)
        {
            if (!openWindows.Contains(window)) { openWindows.Add(window); }
        }

        internal void Untrack(RootUIPrefab window) => openWindows.Remove(window);
    }
}
