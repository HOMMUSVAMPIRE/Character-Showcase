using System;
using System.Collections.Generic;
using System.Linq;
using MeridianMotion.EditorTools;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace MeridianMotion.UI.EditorTools
{
    /// <summary>
    /// Makes the UI's own prefabs (Tools > Meridian Motion > Utilities > Create UI Assets, or CreateFromCommandLine) where
    /// they're missing, as Create Water Effects makes the water's: the UI Manager (its three canvases, an EventSystem
    /// reading the project's UI actions, and every field set), the four click blockers, the dialogs (OK, error, yes/no,
    /// prompt for text), the pause menu, the context menu, the notifications' styles, the widgets (a button, a list row,
    /// a scrollbar, a radio toggle, a checkbox), the theme and an empty window catalog. The package's own are found by
    /// their GUIDs; without them (in a project of its own), they're made in the output folder's UI folder (Project
    /// Settings > Meridian Motion). Made once, they're hand-edited: Create UI Assets remakes none that's there, but Rebuild
    /// UI Assets (-rebuild) remakes every one but the UI Manager in place (ReplaceContents), losing hand edits but keeping
    /// what names them. They're drawn in the theme (its shapes, ThemedText, ThemedButton), never in colours of their own.
    /// </summary>
    public static partial class UIAssets
    {
        // The package's own, found by GUID (with the path it has in this repository: MeridianMotionAssets.PathOf), else
        // where it'd be made in the output folder.
        private static string Folder => MeridianMotionSettings.OutputFolder + "/UI";
        public static string ManagerPath => Existing(MeridianMotionAssets.PathOf("c7a565a23b671483b87cbdcbf55b7a3b", "Assets/MeridianMotion/Content/UI/UI Manager.prefab"), "UI Manager.prefab");
        public static string CatalogPath => Existing(MeridianMotionAssets.PathOf("56c742955394949a1be9fbe4a1abb503", "Assets/MeridianMotion/Content/UI/UI Window Catalog.asset"), "UI Window Catalog.asset");
        public static string ThemePath => Existing(MeridianMotionAssets.PathOf("8c75a2db3bbdf4bcfa488da0c15cc9cb", "Assets/MeridianMotion/Content/UI/UI Theme.asset"), "UI Theme.asset");
        private static string DimBlockerPath => Existing(MeridianMotionAssets.PathOf("3ac7922116bca4b57a56a5ef1e439f24", "Assets/MeridianMotion/Content/UI/Click Blockers/Click Blocker (Dim).prefab"), "Click Blockers/Click Blocker (Dim).prefab");
        private static string InvisibleBlockerPath => Existing(MeridianMotionAssets.PathOf("469189874af4d47348075150c5470f7b", "Assets/MeridianMotion/Content/UI/Click Blockers/Click Blocker (Invisible).prefab"), "Click Blockers/Click Blocker (Invisible).prefab");
        private static string DarkenBlockerPath => Existing(MeridianMotionAssets.PathOf("0725892fccc6147278dd844b3c36352f", "Assets/MeridianMotion/Content/UI/Click Blockers/Click Blocker (Darken).prefab"), "Click Blockers/Click Blocker (Darken).prefab");
        private static string BlackoutBlockerPath => Existing(MeridianMotionAssets.PathOf("5670621cbe3664f3f8dae403ab7c9e3f", "Assets/MeridianMotion/Content/UI/Click Blockers/Click Blocker (Blackout).prefab"), "Click Blockers/Click Blocker (Blackout).prefab");
        private static string MessageBoxPath => Existing(MeridianMotionAssets.PathOf("a8e30522f4c89487697736d36ddbc8c6", "Assets/MeridianMotion/Content/UI/Dialogs/Message Box.prefab"), "Dialogs/Message Box.prefab");
        private static string ErrorMessageBoxPath => Existing(MeridianMotionAssets.PathOf("69dcadec54e00483594fe209a3d6a4a7", "Assets/MeridianMotion/Content/UI/Dialogs/Error Message Box.prefab"), "Dialogs/Error Message Box.prefab");
        private static string YesNoDialogPath => Existing(MeridianMotionAssets.PathOf("72e7f93d7f32c45fcafed8af60bd2966", "Assets/MeridianMotion/Content/UI/Dialogs/Yes No Dialog.prefab"), "Dialogs/Yes No Dialog.prefab");
        private static string RightClickMenuPath => Existing(MeridianMotionAssets.PathOf("07523b1ba69934069afa6625ecefa05b", "Assets/MeridianMotion/Content/UI/Menus/Right Click Menu.prefab"), "Menus/Right Click Menu.prefab");
        private static string MenuEntryPath => Existing(MeridianMotionAssets.PathOf("f94881717f89f457f9a84bca174fe386", "Assets/MeridianMotion/Content/UI/Menus/Menu Entry.prefab"), "Menus/Menu Entry.prefab");
        private static string FeaturedMenuEntryPath => Existing(MeridianMotionAssets.PathOf("e7fbfbdcc634645cf8c1ef3821ec66dc", "Assets/MeridianMotion/Content/UI/Menus/Featured Menu Entry.prefab"), "Menus/Featured Menu Entry.prefab");
        private static string MenuSpacerPath => Existing(MeridianMotionAssets.PathOf("6096276928547439b9047c0a05337aae", "Assets/MeridianMotion/Content/UI/Menus/Menu Spacer.prefab"), "Menus/Menu Spacer.prefab");
        private static string PassiveNotificationPath => Existing(MeridianMotionAssets.PathOf("450fb0205c0cb4649af17feee75fa87b", "Assets/MeridianMotion/Content/UI/Notifications/Notification (Passive).prefab"), "Notifications/Notification (Passive).prefab");
        private static string CenterNotificationPath => Existing(MeridianMotionAssets.PathOf("d4c22fb8459334e53a701efa71acb260", "Assets/MeridianMotion/Content/UI/Notifications/Notification (Center).prefab"), "Notifications/Notification (Center).prefab");
        // A notification style's prefab.
        private static string NotificationPath(Notifications.Type type) => type switch
        {
            Notifications.Type.Passive => PassiveNotificationPath,
            Notifications.Type.Center => CenterNotificationPath,
            Notifications.Type.Success => Existing(MeridianMotionAssets.PathOf("69d29736b6228485f8c56271e55a964c", "Assets/MeridianMotion/Content/UI/Notifications/Notification (Success).prefab"), "Notifications/Notification (Success).prefab"),
            Notifications.Type.Warning => Existing(MeridianMotionAssets.PathOf("c9176c9141b3d490b9a1696a6c682b70", "Assets/MeridianMotion/Content/UI/Notifications/Notification (Warning).prefab"), "Notifications/Notification (Warning).prefab"),
            Notifications.Type.Info => Existing(MeridianMotionAssets.PathOf("ea8c4eb7cc6f441748d44ad1f7fe0d39", "Assets/MeridianMotion/Content/UI/Notifications/Notification (Info).prefab"), "Notifications/Notification (Info).prefab"),
            Notifications.Type.Item => Existing(MeridianMotionAssets.PathOf("0003d3fe4a5944e198cd39e1750851e6", "Assets/MeridianMotion/Content/UI/Notifications/Notification (Item).prefab"), "Notifications/Notification (Item).prefab"),
            Notifications.Type.Locked => Existing(MeridianMotionAssets.PathOf("a008a3564ba904d2f8f69bfc3640812f", "Assets/MeridianMotion/Content/UI/Notifications/Notification (Locked).prefab"), "Notifications/Notification (Locked).prefab"),
            // A style of a project's own (a value it's added): in the output folder.
            _ => Existing(null, $"Notifications/Notification ({type}).prefab"),
        };

        // The widgets' prefabs, by name.
        private static readonly Dictionary<string, string> WidgetGuids = new()
        {
            ["Button"] = "7c8c198e923ec40148f7bfe4f9639eb0", ["List Row"] = "911f2c44c9ef3492e8bc3acc86f1b9ba",
            ["Scrollbar"] = "fd5b2df8844444eb9a12d8cc2c86e64f", ["Radio Toggle"] = "59ac0069d31a44297a5d2dd267220167",
            ["Checkbox"] = "238fe4ec49d094c89ab2085916a79411",
        };

        /// <summary>A widget's prefab (Button, List Row, Scrollbar, Radio Toggle, Checkbox).</summary>
        public static string WidgetPath(string name) =>
            Existing(WidgetGuids.TryGetValue(name, out string guid) ? MeridianMotionAssets.PathOf(guid, $"Assets/MeridianMotion/Content/UI/Widgets/{name}.prefab") : null, $"Widgets/{name}.prefab");
        // For the gallery.
        internal static string MessageBoxPrefabPath => MessageBoxPath;
        internal static string YesNoDialogPrefabPath => YesNoDialogPath;
        internal static string PromptForStringPrefabPath => PromptForStringPath;
        internal static string PauseMenuPrefabPath => PauseMenuPath;
        internal static string MenuEntryPrefabPath => MenuEntryPath;
        internal static string FeaturedMenuEntryPrefabPath => FeaturedMenuEntryPath;
        internal static string NotificationPrefabPath(Notifications.Type type) => NotificationPath(type);
        private static string PauseMenuPath => Existing(MeridianMotionAssets.PathOf("079bb9af2a59246dd8fb6525db8da406", "Assets/MeridianMotion/Content/UI/Windows/Pause Menu.prefab"), "Windows/Pause Menu.prefab");
        private static string PromptForStringPath => Existing(MeridianMotionAssets.PathOf("f8a5a548bed7048da94af523f7c99945", "Assets/MeridianMotion/Content/UI/Dialogs/Prompt For String.prefab"), "Dialogs/Prompt For String.prefab");

        /// <summary>The package's path if its asset is there, else the output folder's (inOutputFolder, within its UI folder).</summary>
        internal static string ExistingOrOutput(string path, string inOutputFolder) => Existing(path, inOutputFolder);

        // The package's path if its asset is there, else the output folder's (inOutputFolder, within its UI folder).
        private static string Existing(string path, string inOutputFolder) =>
            !string.IsNullOrEmpty(path) && AssetDatabase.GetMainAssetTypeAtPath(path) != null ? path : Folder + "/" + inOutputFolder;

        // How big the dialogs and their parts are, in reference pixels (the canvases scale with a 1920x1080 screen).
        private static readonly Vector2 ReferenceResolution = new(1920f, 1080f);
        private static readonly Vector2 DialogSize = new(560f, 260f), PromptSize = new(560f, 320f), ButtonSize = new(160f, 48f);
        private static readonly Vector2 PauseMenuSize = new(760f, 640f), MenuButtonSize = new(320f, 56f);
        private const float Padding = 28f, AccentHeight = 6f, BodyFontSize = 28f, ButtonFontSize = 24f, FieldHeight = 52f;
        // The panels' trim: the header tab's height, the corner brackets (how far outside the panel, how long, how thick),
        // and the chamfer of small pieces (fields, small buttons, menu entries).
        private const float HeaderTabHeight = 10f, BracketGap = 8f, BracketLength = 22f, BracketThickness = 3f, SmallChamfer = 7f;
        // The click blockers' opacities (of the theme's backdrop).
        private const float DimOpacity = 0.5f, DarkenOpacity = 0.75f;
        private const float TitleFontSize = 44f, TitleHeight = 64f, ControlsFontSize = 22f, MenuButtonGap = 16f;

        [MenuItem("Tools/Meridian Motion/Utilities/Create UI Assets", priority = 61)]
        public static void CreateFromMenu() => Create();

        /// <summary>For -executeMethod: Create (Rebuild UI Assets, given -rebuild), then exits (code 1 if it couldn't).</summary>
        public static void CreateFromCommandLine()
        {
            bool made;
            try { made = Create(Array.IndexOf(Environment.GetCommandLineArgs(), "-rebuild") >= 0); }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                made = false;
            }
            if (Application.isBatchMode) { EditorApplication.Exit(made ? 0 : 1); }
        }

        /// <summary>
        /// Makes whichever of the UI's prefabs are missing (see the class), or, rebuilding, remakes every one but the UI
        /// Manager in place; and sets the UI Manager's empty fields to them. False if it couldn't.
        /// </summary>
        public static bool Create(bool rebuild = false)
        {
            rebuilding = rebuild;
            try { return CreateAll(); }
            finally { rebuilding = false; }
        }

        private static bool CreateAll()
        {
            InputActionAsset actions = FindActions();
            if (actions == null)
            {
                Debug.LogError("[UIAssets] Couldn't find the package's input actions (an Input Action Asset in the package): the UI reads its UI action map.");
                return false;
            }
            var catalog = AssetDatabase.LoadAssetAtPath<UIWindowCatalog>(CatalogPath);
            if (catalog == null)
            {
                AssetFolders.Ensure(System.IO.Path.GetDirectoryName(CatalogPath)?.Replace('\\', '/'));
                catalog = ScriptableObject.CreateInstance<UIWindowCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            var theme = AssetDatabase.LoadAssetAtPath<UITheme>(ThemePath);
            if (theme == null)
            {
                AssetFolders.Ensure(System.IO.Path.GetDirectoryName(ThemePath)?.Replace('\\', '/'));
                theme = ScriptableObject.CreateInstance<UITheme>();
                AssetDatabase.CreateAsset(theme, ThemePath);
            }
            // The fonts (UIFonts), where the theme has none.
            var themeFields = new SerializedObject(theme);
            SetIfEmpty(themeFields, "displayFont", UIFonts.Display());
            SetIfEmpty(themeFields, "bodyFont", UIFonts.Body());
            themeFields.ApplyModifiedPropertiesWithoutUndo();
            // What's made is drawn in it (its fonts saved on the texts).
            UITheme.Fallback = theme;

            GameObject dim = PrefabAt(DimBlockerPath, () => Blocker("Dim", DimOpacity));
            GameObject invisible = PrefabAt(InvisibleBlockerPath, () => Blocker("Invisible", 0f));
            GameObject darken = PrefabAt(DarkenBlockerPath, () => Blocker("Darken", DarkenOpacity));
            GameObject blackout = PrefabAt(BlackoutBlockerPath, () => Blocker("Blackout", 1f));

            GameObject messageBox = PrefabAt(MessageBoxPath, () => Dialog<UIMessageBox>("Message Box", DialogSize, ("OK", true)));
            GameObject errorBox = PrefabAt(ErrorMessageBoxPath, () => Dialog<UIMessageBox>("Error Message Box", DialogSize, ("OK", true)));
            GameObject yesNo = PrefabAt(YesNoDialogPath, () => Dialog<UIMessageBox>("Yes No Dialog", DialogSize, ("No", false), ("Yes", true)));
            GameObject prompt = PrefabAt(PromptForStringPath,
                () => Dialog<UIMessageBox_PromptForString>("Prompt For String", PromptSize, ("Cancel", false), ("OK", true)));

            GameObject pauseMenu = PrefabAt(PauseMenuPath, () => PauseMenuWindow(actions));
            if (!catalog.Windows.Contains(pauseMenu))
            {
                catalog.Windows.Add(pauseMenu);
                EditorUtility.SetDirty(catalog);
            }

            GameObject entry = PrefabAt(MenuEntryPath, () => MenuEntry("Menu Entry", featured: false));
            GameObject featuredEntry = PrefabAt(FeaturedMenuEntryPath, () => MenuEntry("Featured Menu Entry", featured: true));
            GameObject spacer = PrefabAt(MenuSpacerPath, MenuSpacer);
            GameObject rightClickMenu = PrefabAt(RightClickMenuPath, () => RightClickMenu(entry, featuredEntry, spacer));
            var notificationPrefabs = new Dictionary<Notifications.Type, GameObject>();
            foreach (Notifications.Type type in Enum.GetValues(typeof(Notifications.Type)))
            {
                notificationPrefabs[type] = PrefabAt(NotificationPath(type), () => NotificationPrefab(type));
            }
            MakeWidgets();

            // Never rebuilt: scenes hold it with their own connections (Set Up Scene's), on objects inside it.
            GameObject manager = PrefabAt(ManagerPath, () => Manager(actions), rebuildable: false);
            // Every field, so the manager made before a dialog or blocker was (or one whose fields were cleared) has them.
            using (var editing = new PrefabUtility.EditPrefabContentsScope(AssetDatabase.GetAssetPath(manager)))
            {
                // The Back key opens the pause menu when no window takes it.
                if (editing.prefabContentsRoot.GetComponent<PauseMenuOnBack>() == null) { editing.prefabContentsRoot.AddComponent<PauseMenuOnBack>(); }
                var fields = new SerializedObject(editing.prefabContentsRoot.GetComponent<UIManager>());
                SetIfEmpty(fields, "theme", theme);
                SetIfEmpty(fields, "windowCatalog", catalog);
                SetIfEmpty(fields, "messageBoxPrefab", messageBox);
                SetIfEmpty(fields, "errorMessageBoxPrefab", errorBox);
                SetIfEmpty(fields, "yesNoDialogPrefab", yesNo);
                SetIfEmpty(fields, "promptForStringPrefab", prompt);
                SetIfEmpty(fields, "rightClickMenuPrefab", rightClickMenu);
                SetIfEmpty(fields, "dimBlockerPrefab", dim);
                SetIfEmpty(fields, "invisibleBlockerPrefab", invisible);
                SetIfEmpty(fields, "darkenBlockerPrefab", darken);
                SetIfEmpty(fields, "blackoutBlockerPrefab", blackout);
                SetIfEmpty(fields, "cancelAction", ActionReference(actions, "UI", "Cancel"));
                SetIfEmpty(fields, "submitAction", ActionReference(actions, "UI", "Submit"));
                fields.ApplyModifiedPropertiesWithoutUndo();
                AddNotifications(editing.prefabContentsRoot.GetComponent<UIManager>(), notificationPrefabs);
                // The EventSystem's module, through its serialized fields: its property setters don't keep the
                // references in the editor (the module isn't running there).
                var module = editing.prefabContentsRoot.GetComponentInChildren<InputSystemUIInputModule>(true);
                if (module != null)
                {
                    var moduleFields = new SerializedObject(module);
                    SetIfEmpty(moduleFields, "m_ActionsAsset", actions);
                    foreach ((string field, string action) in ModuleActions) { SetIfEmpty(moduleFields, field, ActionReference(actions, "UI", action)); }
                    moduleFields.ApplyModifiedPropertiesWithoutUndo();
                }
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[UIAssets] The UI's prefabs are ready: the UI Manager is '{AssetDatabase.GetAssetPath(manager)}'.", manager);
            return true;
        }

        private static void SetIfEmpty(SerializedObject fields, string name, UnityEngine.Object value)
        {
            SerializedProperty property = fields.FindProperty(name);
            if (property == null) { throw new InvalidOperationException($"{fields.targetObject.GetType().Name} has no field '{name}' (renamed?): update UIAssets."); }
            if (property.objectReferenceValue == null) { property.objectReferenceValue = value; }
        }

        // ---- The pieces ----

        // Rebuild UI Assets: every prefab (but the UI Manager) remade in place.
        private static bool rebuilding;

        // The prefab at path, made with make (then saved and the scene's copy destroyed) if there's none; rebuilding, the
        // one there remade in place (ReplaceContents), so its GUID and root's file ID stay.
        private static GameObject PrefabAt(string path, Func<GameObject> make, bool rebuildable = true)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null && !(rebuilding && rebuildable)) { return prefab; }
            AssetFolders.Ensure(System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/'));
            GameObject made = make();
            try
            {
                if (prefab == null) { PrefabUtility.SaveAsPrefabAsset(made, path); }
                else
                {
                    using var editing = new PrefabUtility.EditPrefabContentsScope(path);
                    ReplaceContents(editing.prefabContentsRoot, made);
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(made); }
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        private static GameObject Manager(InputActionAsset actions)
        {
            var root = new GameObject("UI Manager");
            var manager = root.AddComponent<UIManager>();
            manager.inWorldCanvas = Canvas(root.transform, "In World Canvas", 0);
            manager.defaultCanvas = Canvas(root.transform, "Default Canvas", 10);
            manager.overlayCanvas = Canvas(root.transform, "Overlay Canvas", 20);

            var events = new GameObject("EventSystem", typeof(EventSystem));
            events.transform.SetParent(root.transform, false);
            // The package's UI actions (a new module takes the Input System's defaults by itself, so they're set here,
            // not only where empty, as Create does for a manager that's there).
            var moduleFields = new SerializedObject(events.AddComponent<InputSystemUIInputModule>());
            moduleFields.FindProperty("m_ActionsAsset").objectReferenceValue = actions;
            foreach ((string field, string action) in ModuleActions) { moduleFields.FindProperty(field).objectReferenceValue = ActionReference(actions, "UI", action); }
            moduleFields.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        private static Transform Canvas(Transform parent, string name, int order)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            obj.transform.SetParent(parent, false);
            var canvas = obj.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = order;
            var scaler = obj.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.matchWidthOrHeight = 0.5f;
            return obj.transform;
        }

        // A click blocker: the theme's backdrop across the screen, at the opacity given (a raycast target, so it takes clicks).
        private static GameObject Blocker(string name, float opacity)
        {
            var obj = new GameObject($"Click Blocker ({name})", typeof(RectTransform));
            Stretch((RectTransform)obj.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var backdrop = obj.AddComponent<ChamferedBox>();
            backdrop.Corners = UIShapeMesh.Corners.None;
            backdrop.Outline = false;
            backdrop.FillRole = UIColorRole.Backdrop;
            backdrop.color = new Color(1f, 1f, 1f, opacity);
            var window = obj.AddComponent<RootUIPrefab>();
            window.AllowMultiple = true;
            window.CloseOnCancelButton = false;
            window.NeedsCursor = false;
            return obj;
        }

        // A window's panel on root: chamfered, outlined, with the accent's tab along its top (headerTab) and corner brackets
        // just outside it.
        private static ChamferedBox Panel(GameObject root, bool headerTab)
        {
            var panel = root.AddComponent<ChamferedBox>();
            panel.FillRole = UIColorRole.Panel;
            panel.HeaderTab = headerTab;
            panel.HeaderTabHeight = HeaderTabHeight;
            var brackets = new GameObject("Corner Brackets", typeof(RectTransform));
            brackets.transform.SetParent(root.transform, false);
            Stretch((RectTransform)brackets.transform, Vector2.zero, Vector2.one, -Vector2.one * BracketGap, Vector2.one * BracketGap);
            var shape = brackets.AddComponent<CornerBrackets>();
            shape.FillRole = UIColorRole.Accent;
            shape.Length = BracketLength;
            shape.Thickness = BracketThickness;
            shape.raycastTarget = false;
            return panel;
        }

        // A dialog of type T: a panel with the accent's tab, its message, (for a prompt) a text field, and its buttons along
        // the bottom, right to left as given last to first; each (label, isOk) wired to OK (the primary) or decline.
        private static GameObject Dialog<T>(string name, Vector2 size, params (string label, bool ok)[] buttons) where T : UIMessageBox
        {
            var root = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)root.transform;
            rect.sizeDelta = size;
            Panel(root, headerTab: true);
            var box = root.AddComponent<T>();
            box.ClickBlockerStyle = ClickBlockStyle.DimBackground;
            box.BlocksGameInput = true;

            bool prompt = typeof(UIMessageBox_PromptForString).IsAssignableFrom(typeof(T));
            float bottom = Padding + ButtonSize.y + Padding + (prompt ? FieldHeight + Padding * 0.5f : 0f);
            TextMeshProUGUI text = MakeText(rect, "Body", "", BodyFontSize, TextAlignmentOptions.TopLeft, Vector2.zero, Vector2.one,
                new Vector2(Padding, bottom), new Vector2(-Padding, -Padding - HeaderTabHeight), display: false);
            var fields = new SerializedObject(box);
            fields.FindProperty("bodyText").objectReferenceValue = text;

            if (prompt)
            {
                GameObject field = InputField();
                field.transform.SetParent(rect, false);
                float fieldBottom = Padding + ButtonSize.y + Padding;
                Stretch((RectTransform)field.transform, Vector2.zero, new Vector2(1f, 0f), new Vector2(Padding, fieldBottom), new Vector2(-Padding, fieldBottom + FieldHeight));
                fields.FindProperty("inputField").objectReferenceValue = field.GetComponent<TMP_InputField>();
            }
            fields.ApplyModifiedPropertiesWithoutUndo();

            for (int i = 0; i < buttons.Length; i++)
            {
                (string label, bool ok) = buttons[buttons.Length - 1 - i];
                MakeButton(rect, label, ok ? ThemedButton.Kind.Primary : ThemedButton.Kind.Outline, ok ? box.OnOkButtonClicked : box.OnDeclineButtonClicked,
                    ButtonSize, new Vector2(1f, 0f), new Vector2(-Padding - i * (ButtonSize.x + Padding * 0.5f), Padding));
            }
            return root;
        }

        // A text field: TextMeshPro's, on a raised chamfered box (its target graphic), its text and placeholder themed.
        private static GameObject InputField()
        {
            GameObject field = TMP_DefaultControls.CreateInputField(new TMP_DefaultControls.Resources());
            field.name = "Input Field";
            UnityEngine.Object.DestroyImmediate(field.GetComponent<Image>());
            var box = field.AddComponent<ChamferedBox>();
            box.FillRole = UIColorRole.PanelRaised;
            box.Chamfer = SmallChamfer;
            var input = field.GetComponent<TMP_InputField>();
            input.targetGraphic = box;
            input.transition = Selectable.Transition.ColorTint;
            input.colors = UITheme.Current.StateTints;
            Theme(input.textComponent, display: false, UIColorRole.Text);
            input.textComponent.fontSize = BodyFontSize;
            if (input.placeholder is TMP_Text placeholder)
            {
                Theme(placeholder, display: false, UIColorRole.TextMuted);
                placeholder.fontSize = BodyFontSize;
            }
            return field;
        }

        // A themed button labelled label, of the kind and size, anchored (and pivoted) at anchor and placed at position from
        // it, calling pressed when clicked (a persistent listener, saved with the prefab): a chamfered shape, its hatching
        // (shown while it can't be used) and its label.
        private static GameObject MakeButton(Transform parent, string label, ThemedButton.Kind kind, UnityAction pressed, Vector2 size, Vector2 anchor, Vector2 position)
        {
            GameObject button = ThemedButtonObject(label, kind, size);
            button.transform.SetParent(parent, false);
            var rect = (RectTransform)button.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = position;
            UnityEventTools.AddPersistentListener(button.GetComponent<Button>().onClick, pressed);
            return button;
        }

        // A themed button's object, unparented: "[label] Button", of the kind and size.
        private static GameObject ThemedButtonObject(string label, ThemedButton.Kind kind, Vector2 size)
        {
            var button = new GameObject($"{label} Button", typeof(RectTransform));
            ((RectTransform)button.transform).sizeDelta = size;
            var shape = button.AddComponent<ChamferedBox>();
            if (size.y < ButtonSize.y) { shape.Chamfer = SmallChamfer; }

            var hatchObject = new GameObject("Hatch", typeof(RectTransform));
            hatchObject.transform.SetParent(button.transform, false);
            Stretch((RectTransform)hatchObject.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var hatch = hatchObject.AddComponent<StripeFill>();
            hatch.Corners = UIShapeMesh.Corners.All;
            hatch.StripeWidth = 3f;
            hatch.Gap = 6f;
            hatch.raycastTarget = false;

            TextMeshProUGUI text = MakeText(button.transform, "Label", label.ToUpperInvariant(), size.y < ButtonSize.y ? ButtonFontSize * 0.75f : ButtonFontSize,
                TextAlignmentOptions.Center, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, display: true);
            text.raycastTarget = false;

            var themed = button.AddComponent<ThemedButton>();
            themed.Shape = shape;
            themed.Hatch = hatch;
            themed.Label = text.GetComponent<ThemedText>();
            themed.ButtonKind = kind;
            hatchObject.SetActive(false);
            return button;
        }

        // A button that's an icon (a window's close button): the icon of the name, in role, tinted by state.
        private static GameObject IconButton(Transform parent, string name, string iconName, UnityAction pressed, Vector2 size, Vector2 anchor, Vector2 position)
        {
            var button = new GameObject(name, typeof(RectTransform));
            button.transform.SetParent(parent, false);
            var rect = (RectTransform)button.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            var icon = button.AddComponent<UIIcon>();
            icon.IconName = iconName;
            icon.FillRole = UIColorRole.TextMuted;
            var clickable = button.AddComponent<Button>();
            clickable.targetGraphic = icon;
            clickable.colors = UITheme.Current.StateTints;
            UnityEventTools.AddPersistentListener(clickable.onClick, pressed);
            return button;
        }

        // A text of the size and alignment, stretched over its parent less the offsets, in the theme's display or body font
        // and the role's colour (a ThemedText).
        private static TextMeshProUGUI MakeText(Transform parent, string name, string value, float size, TextAlignmentOptions alignment,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, bool display = false, UIColorRole role = UIColorRole.Text)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            obj.transform.SetParent(parent, false);
            Stretch((RectTransform)obj.transform, anchorMin, anchorMax, offsetMin, offsetMax);
            var text = obj.GetComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = size;
            text.alignment = alignment;
            Theme(text, display, role);
            return text;
        }

        // Puts a text in the theme's look (a ThemedText beside it).
        private static ThemedText Theme(TMP_Text text, bool display, UIColorRole role)
        {
            var themed = text.GetComponent<ThemedText>();
            if (themed == null) { themed = text.gameObject.AddComponent<ThemedText>(); }
            themed.Display = display;
            themed.Role = role;
            return themed;
        }

        // The pause menu: a panel with the tab bar along its top (Menu and Controls, trapezoids), its main page (a title and
        // Resume, Restart, Controls and Quit down the middle) and its controls page (a title, the bindings listing, and Back),
        // each button wired.
        private static GameObject PauseMenuWindow(InputActionAsset actions)
        {
            var root = new GameObject("Pause Menu", typeof(RectTransform));
            var rect = (RectTransform)root.transform;
            rect.sizeDelta = PauseMenuSize;
            Panel(root, headerTab: false);
            var menu = root.AddComponent<PauseMenu>();
            menu.ClickBlockerStyle = ClickBlockStyle.DarkenBackground;
            menu.BlocksGameInput = true;
            menu.Actions = actions;

            Transform Page(string name)
            {
                var page = new GameObject(name, typeof(RectTransform));
                page.transform.SetParent(rect, false);
                Stretch((RectTransform)page.transform, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0f, -TabRowHeight));
                MakeText(page.transform, "Title", name == "Main Page" ? "PAUSED" : "CONTROLS", TitleFontSize, TextAlignmentOptions.Center,
                    new Vector2(0f, 1f), Vector2.one, new Vector2(Padding, -Padding - TitleHeight), new Vector2(-Padding, -Padding), display: true);
                return page.transform;
            }

            Transform main = Page("Main Page");
            var buttons = new (string label, ThemedButton.Kind kind, UnityAction pressed)[]
            {
                ("Resume", ThemedButton.Kind.Primary, menu.Resume), ("Restart", ThemedButton.Kind.Outline, menu.Restart),
                ("Controls", ThemedButton.Kind.Outline, menu.ShowControls), ("Quit", ThemedButton.Kind.Outline, menu.QuitFromButton),
            };
            float top = -Padding * 2f - TitleHeight;
            for (int i = 0; i < buttons.Length; i++)
            {
                (string label, ThemedButton.Kind kind, UnityAction pressed) = buttons[i];
                GameObject button = MakeButton(main, label, kind, pressed, MenuButtonSize, new Vector2(0.5f, 1f), new Vector2(0f, top - i * (MenuButtonSize.y + MenuButtonGap)));
                button.GetComponent<ThemedButton>().Gloss = true;
            }

            Transform controls = Page("Controls Page");
            TextMeshProUGUI listing = MakeText(controls, "Bindings", "", ControlsFontSize, TextAlignmentOptions.TopLeft, Vector2.zero, Vector2.one,
                new Vector2(Padding, Padding * 2f + ButtonSize.y), new Vector2(-Padding, -Padding * 2f - TitleHeight));
            MakeButton(controls, "Back", ThemedButton.Kind.Outline, menu.ShowMain, ButtonSize, new Vector2(1f, 0f), new Vector2(-Padding, Padding));

            // Its pages as the contents of two tabs along its top.
            var tabs = root.AddComponent<TabGroup>();
            Tab mainTab = TabButton(rect, tabs, "Menu", 0, main.gameObject);
            Tab controlsTab = TabButton(rect, tabs, "Controls", 1, controls.gameObject);
            tabs.InitialTabEnabled = mainTab;
            // Shown as it opens: the first tab selected (the group does it again when it starts).
            controlsTab.SelectedState.SetActive(false);

            var fields = new SerializedObject(menu);
            fields.FindProperty("pageTabs").objectReferenceValue = tabs;
            fields.FindProperty("mainTab").objectReferenceValue = mainTab;
            fields.FindProperty("controlsTab").objectReferenceValue = controlsTab;
            fields.FindProperty("mainPage").objectReferenceValue = main.gameObject;
            fields.FindProperty("controlsPage").objectReferenceValue = controls.gameObject;
            fields.FindProperty("controlsText").objectReferenceValue = listing;
            fields.ApplyModifiedPropertiesWithoutUndo();
            controls.gameObject.SetActive(false);
            return root;
        }

        private static void Stretch(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        // ---- The project's input ----

        // The EventSystem module's action fields, and the UI action map's actions they read.
        private static readonly (string field, string action)[] ModuleActions =
        {
            ("m_PointAction", "Point"), ("m_MoveAction", "Navigate"), ("m_SubmitAction", "Submit"), ("m_CancelAction", "Cancel"),
            ("m_LeftClickAction", "Click"), ("m_MiddleClickAction", "MiddleClick"), ("m_RightClickAction", "RightClick"),
            ("m_ScrollWheelAction", "ScrollWheel"), ("m_TrackedDevicePositionAction", "TrackedDevicePosition"),
            ("m_TrackedDeviceOrientationAction", "TrackedDeviceOrientation"),
        };

        // The package's input actions: the Input Action Asset in the package's folder.
        private static InputActionAsset FindActions()
        {
            string root = MeridianMotionAssets.PackageRoot;
            if (string.IsNullOrEmpty(root) || !AssetDatabase.IsValidFolder(root)) { return null; }
            string guid = AssetDatabase.FindAssets("t:" + nameof(InputActionAsset), new[] { root }).FirstOrDefault();
            return guid != null ? AssetDatabase.LoadAssetAtPath<InputActionAsset>(AssetDatabase.GUIDToAssetPath(guid)) : null;
        }

        // The asset's reference to its action map/action (the references Unity keeps in the .inputactions file), or null.
        private static InputActionReference ActionReference(InputActionAsset actions, string map, string action) =>
            AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(actions)).OfType<InputActionReference>()
                .FirstOrDefault(reference => reference.action != null && reference.action.actionMap?.name == map && reference.action.name == action);

        private static Color Hex(int rgb) => new((rgb >> 16 & 0xFF) / 255f, (rgb >> 8 & 0xFF) / 255f, (rgb & 0xFF) / 255f);
    }
}
