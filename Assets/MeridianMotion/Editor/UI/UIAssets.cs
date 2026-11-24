using System;
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
    /// prompt for text) and an empty window catalog. The package's own are found by their GUIDs; without them (in a
    /// project of its own), they're made in the output folder's UI folder (Project Settings > Meridian Motion). Made
    /// once, they're hand-edited: nothing remakes one that's there. Their look is the showcase levels' palette.
    /// </summary>
    public static partial class UIAssets
    {
        // The package's own, found by GUID (with the path it has in this repository: MeridianMotionAssets.PathOf), else
        // where it'd be made in the output folder.
        private static string Folder => MeridianMotionSettings.OutputFolder + "/UI";
        public static string ManagerPath => Existing(MeridianMotionAssets.PathOf("c7a565a23b671483b87cbdcbf55b7a3b", "Assets/MeridianMotion/Content/UI/UI Manager.prefab"), "UI Manager.prefab");
        public static string CatalogPath => Existing(MeridianMotionAssets.PathOf("56c742955394949a1be9fbe4a1abb503", "Assets/MeridianMotion/Content/UI/UI Window Catalog.asset"), "UI Window Catalog.asset");
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
        private static string PauseMenuPath => Existing(MeridianMotionAssets.PathOf("079bb9af2a59246dd8fb6525db8da406", "Assets/MeridianMotion/Content/UI/Windows/Pause Menu.prefab"), "Windows/Pause Menu.prefab");
        private static string PromptForStringPath => Existing(MeridianMotionAssets.PathOf("f8a5a548bed7048da94af523f7c99945", "Assets/MeridianMotion/Content/UI/Dialogs/Prompt For String.prefab"), "Dialogs/Prompt For String.prefab");

        // The package's path if its asset is there, else the output folder's (inOutputFolder, within its UI folder).
        private static string Existing(string path, string inOutputFolder) =>
            !string.IsNullOrEmpty(path) && AssetDatabase.GetMainAssetTypeAtPath(path) != null ? path : Folder + "/" + inOutputFolder;

        // The showcase levels' palette (ReferenceAssets/MeridianComplexStyleReference.png): graphite panels, off-white
        // text, the orange accent, steel grey for the lesser button.
        private static readonly Color Graphite = Hex(0x2D3138), OffWhite = Hex(0xF2F4F6), Orange = Hex(0xE07A2F), SteelGrey = Hex(0x7E8793), Cyan = Hex(0x54C6D6);

        // How big the dialogs and their parts are, in reference pixels (the canvases scale with a 1920x1080 screen).
        private static readonly Vector2 ReferenceResolution = new(1920f, 1080f);
        private static readonly Vector2 DialogSize = new(560f, 260f), PromptSize = new(560f, 320f), ButtonSize = new(160f, 48f);
        private static readonly Vector2 PauseMenuSize = new(760f, 640f), MenuButtonSize = new(320f, 56f);
        private const float Padding = 28f, AccentHeight = 6f, BodyFontSize = 28f, ButtonFontSize = 24f, FieldHeight = 52f;
        private const float TitleFontSize = 44f, TitleHeight = 64f, ControlsFontSize = 22f, MenuButtonGap = 16f;

        [MenuItem("Tools/Meridian Motion/Utilities/Create UI Assets", priority = 61)]
        public static void CreateFromMenu() => Create();

        /// <summary>For -executeMethod: Create, then exits (code 1 if it couldn't).</summary>
        public static void CreateFromCommandLine()
        {
            bool made;
            try { made = Create(); }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                made = false;
            }
            if (Application.isBatchMode) { EditorApplication.Exit(made ? 0 : 1); }
        }

        /// <summary>Makes whichever of the UI's prefabs are missing (see the class), and sets the UI Manager's fields to them. False if it couldn't.</summary>
        public static bool Create()
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

            GameObject dim = PrefabAt(DimBlockerPath, () => Blocker("Dim", new Color(0f, 0f, 0f, 0.5f)));
            GameObject invisible = PrefabAt(InvisibleBlockerPath, () => Blocker("Invisible", new Color(0f, 0f, 0f, 0f)));
            GameObject darken = PrefabAt(DarkenBlockerPath, () => Blocker("Darken", new Color(0f, 0f, 0f, 0.75f)));
            GameObject blackout = PrefabAt(BlackoutBlockerPath, () => Blocker("Blackout", Color.black));

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
            GameObject passive = PrefabAt(PassiveNotificationPath, () => NotificationPrefab("Notification (Passive)", center: false));
            GameObject center = PrefabAt(CenterNotificationPath, () => NotificationPrefab("Notification (Center)", center: true));

            GameObject manager = PrefabAt(ManagerPath, () => Manager(actions));
            // Every field, so the manager made before a dialog or blocker was (or one whose fields were cleared) has them.
            using (var editing = new PrefabUtility.EditPrefabContentsScope(AssetDatabase.GetAssetPath(manager)))
            {
                // The Back key opens the pause menu when no window takes it.
                if (editing.prefabContentsRoot.GetComponent<PauseMenuOnBack>() == null) { editing.prefabContentsRoot.AddComponent<PauseMenuOnBack>(); }
                var fields = new SerializedObject(editing.prefabContentsRoot.GetComponent<UIManager>());
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
                AddNotifications(editing.prefabContentsRoot.GetComponent<UIManager>(), passive, center);
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
            if (property == null) { throw new InvalidOperationException($"UIManager has no field '{name}' (renamed?): update UIAssets."); }
            if (property.objectReferenceValue == null) { property.objectReferenceValue = value; }
        }

        // ---- The pieces ----

        // The prefab at path, made with make (then saved and the scene's copy destroyed) if there's none.
        private static GameObject PrefabAt(string path, Func<GameObject> make)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null) { return prefab; }
            AssetFolders.Ensure(System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/'));
            GameObject made = make();
            try { prefab = PrefabUtility.SaveAsPrefabAsset(made, path); }
            finally { UnityEngine.Object.DestroyImmediate(made); }
            return prefab;
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
            events.AddComponent<InputSystemUIInputModule>(); // Its actions: see Create.
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

        private static GameObject Blocker(string name, Color color)
        {
            var obj = new GameObject($"Click Blocker ({name})", typeof(RectTransform), typeof(Image));
            Stretch((RectTransform)obj.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            obj.GetComponent<Image>().color = color;
            var window = obj.AddComponent<RootUIPrefab>();
            window.AllowMultiple = true;
            window.CloseOnCancelButton = false;
            window.NeedsCursor = false;
            return obj;
        }

        // A dialog of type T: a graphite panel with an orange accent along its top, its message, (for a prompt) a text field,
        // and its buttons along the bottom, right to left as given last to first; each (label, isOk) wired to OK or decline.
        private static GameObject Dialog<T>(string name, Vector2 size, params (string label, bool ok)[] buttons) where T : UIMessageBox
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)root.transform;
            rect.sizeDelta = size;
            root.GetComponent<Image>().color = Graphite;
            var box = root.AddComponent<T>();
            box.ClickBlockerStyle = ClickBlockStyle.DimBackground;
            box.BlocksGameInput = true;

            var accent = new GameObject("Accent", typeof(RectTransform), typeof(Image));
            accent.transform.SetParent(rect, false);
            Stretch((RectTransform)accent.transform, new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -AccentHeight), Vector2.zero);
            accent.GetComponent<Image>().color = Orange;

            bool prompt = typeof(UIMessageBox_PromptForString).IsAssignableFrom(typeof(T));
            float bottom = Padding + ButtonSize.y + Padding + (prompt ? FieldHeight + Padding * 0.5f : 0f);
            var body = new GameObject("Body", typeof(RectTransform), typeof(TextMeshProUGUI));
            body.transform.SetParent(rect, false);
            Stretch((RectTransform)body.transform, Vector2.zero, Vector2.one, new Vector2(Padding, bottom), new Vector2(-Padding, -Padding - AccentHeight));
            var text = body.GetComponent<TextMeshProUGUI>();
            text.fontSize = BodyFontSize;
            text.color = OffWhite;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.text = "";
            var fields = new SerializedObject(box);
            fields.FindProperty("bodyText").objectReferenceValue = text;

            if (prompt)
            {
                GameObject field = TMP_DefaultControls.CreateInputField(new TMP_DefaultControls.Resources());
                field.name = "Input Field";
                field.transform.SetParent(rect, false);
                float fieldBottom = Padding + ButtonSize.y + Padding;
                Stretch((RectTransform)field.transform, Vector2.zero, new Vector2(1f, 0f), new Vector2(Padding, fieldBottom), new Vector2(-Padding, fieldBottom + FieldHeight));
                fields.FindProperty("inputField").objectReferenceValue = field.GetComponent<TMP_InputField>();
            }
            fields.ApplyModifiedPropertiesWithoutUndo();

            for (int i = 0; i < buttons.Length; i++)
            {
                (string label, bool ok) = buttons[buttons.Length - 1 - i];
                MakeButton(rect, label, ok ? Orange : SteelGrey, ok ? box.OnOkButtonClicked : box.OnDeclineButtonClicked, ButtonSize,
                    new Vector2(1f, 0f), new Vector2(-Padding - i * (ButtonSize.x + Padding * 0.5f), Padding));
            }
            return root;
        }

        // A button labelled label, of the colour and size, anchored (and pivoted) at anchor and placed at position from it,
        // calling pressed when clicked (a persistent listener, saved with the prefab).
        private static GameObject MakeButton(Transform parent, string label, Color color, UnityAction pressed, Vector2 size, Vector2 anchor, Vector2 position)
        {
            GameObject button = TMP_DefaultControls.CreateButton(new TMP_DefaultControls.Resources());
            button.name = $"{label} Button";
            button.transform.SetParent(parent, false);
            var rect = (RectTransform)button.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            button.GetComponent<Image>().color = color;
            var text = button.GetComponentInChildren<TextMeshProUGUI>();
            text.text = label;
            text.fontSize = ButtonFontSize;
            text.color = OffWhite;
            UnityEventTools.AddPersistentListener(button.GetComponent<Button>().onClick, pressed);
            return button;
        }

        // A text of the size and alignment, stretched over its parent less the offsets.
        private static TextMeshProUGUI MakeText(Transform parent, string name, string value, float size, TextAlignmentOptions alignment,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            obj.transform.SetParent(parent, false);
            Stretch((RectTransform)obj.transform, anchorMin, anchorMax, offsetMin, offsetMax);
            var text = obj.GetComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = size;
            text.color = OffWhite;
            text.alignment = alignment;
            return text;
        }

        // The pause menu: a graphite panel with the orange accent; its main page (a title and Resume, Restart, Controls and
        // Quit down the middle) and its controls page (a title, the bindings listing, and Back), each button wired.
        private static GameObject PauseMenuWindow(InputActionAsset actions)
        {
            var root = new GameObject("Pause Menu", typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)root.transform;
            rect.sizeDelta = PauseMenuSize;
            root.GetComponent<Image>().color = Graphite;
            var menu = root.AddComponent<PauseMenu>();
            menu.ClickBlockerStyle = ClickBlockStyle.DarkenBackground;
            menu.BlocksGameInput = true;
            menu.Actions = actions;

            var accent = new GameObject("Accent", typeof(RectTransform), typeof(Image));
            accent.transform.SetParent(rect, false);
            Stretch((RectTransform)accent.transform, new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -AccentHeight), Vector2.zero);
            accent.GetComponent<Image>().color = Orange;

            Transform Page(string name)
            {
                var page = new GameObject(name, typeof(RectTransform));
                page.transform.SetParent(rect, false);
                Stretch((RectTransform)page.transform, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0f, -AccentHeight - TabRowHeight));
                MakeText(page.transform, "Title", name == "Main Page" ? "PAUSED" : "CONTROLS", TitleFontSize, TextAlignmentOptions.Center,
                    new Vector2(0f, 1f), Vector2.one, new Vector2(Padding, -Padding - TitleHeight), new Vector2(-Padding, -Padding));
                return page.transform;
            }

            Transform main = Page("Main Page");
            var buttons = new (string label, Color color, UnityAction pressed)[]
            {
                ("Resume", Orange, menu.Resume), ("Restart", SteelGrey, menu.Restart),
                ("Controls", SteelGrey, menu.ShowControls), ("Quit", SteelGrey, menu.QuitFromButton),
            };
            float top = -Padding * 2f - TitleHeight;
            for (int i = 0; i < buttons.Length; i++)
            {
                (string label, Color color, UnityAction pressed) = buttons[i];
                MakeButton(main, label, color, pressed, MenuButtonSize, new Vector2(0.5f, 1f), new Vector2(0f, top - i * (MenuButtonSize.y + MenuButtonGap)));
            }

            Transform controls = Page("Controls Page");
            TextMeshProUGUI listing = MakeText(controls, "Bindings", "", ControlsFontSize, TextAlignmentOptions.TopLeft, Vector2.zero, Vector2.one,
                new Vector2(Padding, Padding * 2f + ButtonSize.y), new Vector2(-Padding, -Padding * 2f - TitleHeight));
            MakeButton(controls, "Back", SteelGrey, menu.ShowMain, ButtonSize, new Vector2(1f, 0f), new Vector2(-Padding, Padding));

            // Its pages as the contents of two tabs along its top.
            var tabs = root.AddComponent<TabGroup>();
            Tab mainTab = TabButton(rect, tabs, "Menu", 0, main.gameObject);
            Tab controlsTab = TabButton(rect, tabs, "Controls", 1, controls.gameObject);
            tabs.InitialTabEnabled = mainTab;

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
