using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;

namespace MeridianMotion.UI.EditorTools
{
    // The widgets' prefabs: the context menu and its entries, the notifications, the tab buttons, and the widgets a project
    // uses in its own windows (a button, a list row, a scrollbar, a radio toggle, a checkbox).
    public static partial class UIAssets
    {
        private static readonly Vector2 MenuEntrySize = new(280f, 44f), TabButtonSize = new(200f, 44f);
        private static readonly Vector2 PassiveNotificationSize = new(440f, 110f), CenterNotificationSize = new(960f, 150f);
        private static readonly Vector2 ReadMoreSize = new(140f, 32f);
        private static readonly Vector2 ListRowSize = new(480f, 48f), ToggleSize = new(240f, 36f), ScrollbarSize = new(14f, 320f);
        private const float MenuSpacerHeight = 8f, TabRowHeight = 56f, NotificationGap = 12f, IconSize = 24f, NotificationIconSize = 36f;
        private const float TabSlant = 14f, TabGap = 4f, BulletSize = 16f, ToggleBoxSize = 24f;

        // Each notification style's colour role and icon (none: no icon).
        private static (UIColorRole role, string icon) NotificationLook(Notifications.Type type) => type switch
        {
            Notifications.Type.Center => (UIColorRole.Accent, null),
            Notifications.Type.Success => (UIColorRole.Positive, UIIconLibrary.Check),
            Notifications.Type.Warning => (UIColorRole.Warning, UIIconLibrary.Warning),
            Notifications.Type.Info => (UIColorRole.Info, UIIconLibrary.Info),
            Notifications.Type.Item => (UIColorRole.Highlight, UIIconLibrary.Crate),
            Notifications.Type.Locked => (UIColorRole.TextMuted, UIIconLibrary.Lock),
            _ => (UIColorRole.Highlight, null),
        };

        // A context menu's entry: its label and icon on a chamfered background for each place in a group (its outer corners
        // cut at a group's ends), featured ones in the accent; its button calls OnClicked.
        private static GameObject MenuEntry(string name, bool featured)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(LayoutElement), typeof(Button));
            var layout = root.GetComponent<LayoutElement>();
            layout.preferredWidth = MenuEntrySize.x;
            layout.preferredHeight = MenuEntrySize.y;
            var entry = root.AddComponent<RightClickMenuEntry>();
            GameObject Background(string place, UIShapeMesh.Corners corners)
            {
                var obj = new GameObject($"{place} Background", typeof(RectTransform));
                obj.transform.SetParent(root.transform, false);
                Stretch((RectTransform)obj.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                var box = obj.AddComponent<ChamferedBox>();
                box.FillRole = featured ? UIColorRole.Accent : UIColorRole.Panel;
                box.Outline = !featured;
                box.Corners = corners;
                box.Chamfer = SmallChamfer;
                return obj;
            }
            const UIShapeMesh.Corners top = UIShapeMesh.Corners.TopLeft | UIShapeMesh.Corners.TopRight;
            const UIShapeMesh.Corners bottom = UIShapeMesh.Corners.BottomLeft | UIShapeMesh.Corners.BottomRight;
            GameObject solo = Background("Solo", UIShapeMesh.Corners.All), first = Background("First", top);
            GameObject middle = Background("Middle", UIShapeMesh.Corners.None), last = Background("Last", bottom);
            var button = root.GetComponent<Button>();
            button.targetGraphic = middle.GetComponent<ChamferedBox>();
            button.colors = UITheme.Current.StateTints;

            var icon = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            icon.transform.SetParent(root.transform, false);
            var iconRect = (RectTransform)icon.transform;
            iconRect.anchorMin = iconRect.anchorMax = iconRect.pivot = new Vector2(0f, 0.5f);
            iconRect.sizeDelta = new Vector2(IconSize, IconSize);
            iconRect.anchoredPosition = new Vector2(Padding * 0.5f, 0f);
            TextMeshProUGUI label = MakeText(root.transform, "Label", "", ButtonFontSize * 0.85f, TextAlignmentOptions.MidlineLeft, Vector2.zero, Vector2.one,
                new Vector2(Padding * 0.5f + IconSize + Padding * 0.5f, 0f), new Vector2(-Padding * 0.5f, 0f), display: true,
                featured ? UIColorRole.TextOnFill : UIColorRole.Text);

            var fields = new SerializedObject(entry);
            fields.FindProperty("label").objectReferenceValue = label;
            fields.FindProperty("icon").objectReferenceValue = icon.GetComponent<Image>();
            fields.FindProperty("soloBackground").objectReferenceValue = solo;
            fields.FindProperty("firstBackground").objectReferenceValue = first;
            fields.FindProperty("middleBackground").objectReferenceValue = middle;
            fields.FindProperty("lastBackground").objectReferenceValue = last;
            fields.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(button.onClick, entry.OnClicked);
            return root;
        }

        // The gap between a context menu's groups.
        private static GameObject MenuSpacer()
        {
            var root = new GameObject("Menu Spacer", typeof(RectTransform), typeof(LayoutElement));
            root.GetComponent<LayoutElement>().preferredHeight = MenuSpacerHeight;
            return root;
        }

        // The context menu: its entries stacked, sized to them, on the overlay canvas.
        private static GameObject RightClickMenu(GameObject entry, GameObject featuredEntry, GameObject spacer)
        {
            var root = new GameObject("Right Click Menu", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter), typeof(Image));
            root.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f); // Catches the pointer between entries.
            var layout = root.GetComponent<VerticalLayoutGroup>();
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var fitter = root.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var menu = root.AddComponent<UIRightClickMenu>();
            menu.ParentCanvas = UICanvas.Overlay;
            var fields = new SerializedObject(menu);
            fields.FindProperty("entryParent").objectReferenceValue = root.transform;
            fields.FindProperty("entryPrefab").objectReferenceValue = entry;
            fields.FindProperty("featuredEntryPrefab").objectReferenceValue = featuredEntry;
            fields.FindProperty("spacerEntryPrefab").objectReferenceValue = spacer;
            fields.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        // A notification of the style: a panel outlined in its colour, a strip of it down its left, its icon (if it has
        // one), title, message, Read More and close. Center's is a banner: wider, its title larger, the accent's tab.
        private static GameObject NotificationPrefab(Notifications.Type type)
        {
            bool center = type == Notifications.Type.Center;
            (UIColorRole role, string iconName) = NotificationLook(type);
            Vector2 size = center ? CenterNotificationSize : PassiveNotificationSize;
            var root = new GameObject($"Notification ({type})", typeof(RectTransform), typeof(LayoutElement));
            ((RectTransform)root.transform).sizeDelta = size;
            root.GetComponent<LayoutElement>().preferredWidth = size.x;
            root.GetComponent<LayoutElement>().preferredHeight = size.y;
            var panel = root.AddComponent<ChamferedBox>();
            panel.FillRole = UIColorRole.Panel;
            panel.OutlineRole = role;
            panel.HeaderTab = center;
            panel.HeaderTabHeight = HeaderTabHeight;
            var entry = root.AddComponent<NotificationEntry>();

            var strip = new GameObject("Accent", typeof(RectTransform));
            strip.transform.SetParent(root.transform, false);
            Stretch((RectTransform)strip.transform, Vector2.zero, new Vector2(0f, 1f), new Vector2(0f, SmallChamfer * 2f), new Vector2(AccentHeight, -SmallChamfer * 2f));
            var stripShape = strip.AddComponent<ChamferedBox>();
            stripShape.Corners = UIShapeMesh.Corners.None;
            stripShape.Outline = false;
            stripShape.FillRole = role;
            stripShape.raycastTarget = false;

            float inset = Padding * 0.5f + AccentHeight;
            if (iconName != null)
            {
                var icon = new GameObject("Icon", typeof(RectTransform));
                icon.transform.SetParent(root.transform, false);
                var iconRect = (RectTransform)icon.transform;
                iconRect.anchorMin = iconRect.anchorMax = iconRect.pivot = new Vector2(0f, 1f);
                iconRect.sizeDelta = new Vector2(NotificationIconSize, NotificationIconSize);
                iconRect.anchoredPosition = new Vector2(inset, -Padding * 0.5f);
                var shape = icon.AddComponent<UIIcon>();
                shape.IconName = iconName;
                shape.FillRole = role;
                shape.raycastTarget = false;
                inset += NotificationIconSize + Padding * 0.5f;
            }
            TextMeshProUGUI title = MakeText(root.transform, "Title", "", center ? TitleFontSize : ButtonFontSize, TextAlignmentOptions.TopLeft,
                new Vector2(0f, 0.5f), Vector2.one, new Vector2(inset, 0f), new Vector2(-Padding * 2f, -Padding * 0.5f - (center ? HeaderTabHeight : 0f)), display: true);
            TextMeshProUGUI message = MakeText(root.transform, "Message", "", ButtonFontSize * 0.8f, TextAlignmentOptions.TopLeft,
                Vector2.zero, new Vector2(1f, 0.5f), new Vector2(inset, Padding * 0.5f), new Vector2(-Padding - ReadMoreSize.x, 0f), display: false, UIColorRole.TextMuted);
            GameObject readMore = MakeButton(root.transform, "Read More", ThemedButton.Kind.Outline, entry.OnReadMoreClicked, ReadMoreSize,
                new Vector2(1f, 0f), new Vector2(-Padding * 0.5f, Padding * 0.25f));
            IconButton(root.transform, "Close Button", UIIconLibrary.Cross, entry.Close, new Vector2(24f, 24f), Vector2.one,
                new Vector2(-Padding * 0.4f, -Padding * 0.4f - (center ? HeaderTabHeight : 0f)));

            var fields = new SerializedObject(entry);
            fields.FindProperty("titleText").objectReferenceValue = title;
            fields.FindProperty("messageText").objectReferenceValue = message;
            fields.FindProperty("readMoreButton").objectReferenceValue = readMore;
            fields.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        // The UI Manager's notifications: if it has none, on its overlay canvas, a parent for each place, stacking its
        // notifications; and each style's prefab, where it has none.
        private static void AddNotifications(UIManager manager, Dictionary<Notifications.Type, GameObject> prefabs)
        {
            var notifications = manager.GetComponentInChildren<Notifications>(true);
            if (notifications == null && manager.overlayCanvas != null)
            {
                var root = new GameObject("Notifications", typeof(RectTransform));
                root.transform.SetParent(manager.overlayCanvas, false);
                Stretch((RectTransform)root.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                notifications = root.AddComponent<Notifications>();
                foreach (Notifications.Location location in System.Enum.GetValues(typeof(Notifications.Location)))
                {
                    notifications.SetParent(location, LocationParent(root.transform, location));
                }
            }
            if (notifications == null) { return; }
            foreach ((Notifications.Type type, GameObject prefab) in prefabs)
            {
                if (notifications.PrefabOf(type) == null) { notifications.SetPrefab(type, prefab.GetComponent<NotificationEntry>()); }
            }
            EditorUtility.SetDirty(notifications);
        }

        // A place's parent, stacking its notifications from its corner (or edge, or the middle).
        private static RectTransform LocationParent(Transform root, Notifications.Location location)
        {
            string place = location.ToString();
            Vector2 anchor = new(place.EndsWith("Left") ? 0f : place.EndsWith("Right") ? 1f : 0.5f,
                place.StartsWith("Top") ? 1f : place.StartsWith("Bottom") ? 0f : 0.5f);
            var parent = new GameObject(place.Replace('_', ' '), typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            parent.transform.SetParent(root, false);
            var rect = (RectTransform)parent.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = new Vector2((0.5f - anchor.x) * 2f * Padding, (0.5f - anchor.y) * 2f * Padding);
            var layout = parent.GetComponent<VerticalLayoutGroup>();
            layout.spacing = NotificationGap;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            layout.childAlignment = anchor.y > 0.5f ? (anchor.x < 0.5f ? TextAnchor.UpperLeft : anchor.x > 0.5f ? TextAnchor.UpperRight : TextAnchor.UpperCenter)
                : anchor.y < 0.5f ? (anchor.x < 0.5f ? TextAnchor.LowerLeft : anchor.x > 0.5f ? TextAnchor.LowerRight : TextAnchor.LowerCenter)
                : TextAnchor.MiddleCenter;
            var fitter = parent.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return rect;
        }

        // A tab of the tab bar along the top of a window: a raised trapezoid, the accent's while selected (its Selected
        // State), its label; its content the page.
        private static Tab TabButton(RectTransform window, TabGroup group, string label, int index, GameObject contents)
        {
            var button = new GameObject($"{label} Tab", typeof(RectTransform));
            button.transform.SetParent(window, false);
            var rect = (RectTransform)button.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = TabButtonSize;
            rect.anchoredPosition = new Vector2(Padding + index * (TabButtonSize.x - TabSlant + TabGap), -(TabRowHeight - TabButtonSize.y) / 2f);
            var shape = button.AddComponent<TrapezoidShape>();
            shape.FillRole = UIColorRole.PanelRaised;
            shape.Slant = TabSlant;
            shape.UpsideDown = true;
            var clickable = button.AddComponent<Button>();
            clickable.targetGraphic = shape;
            clickable.colors = UITheme.Current.StateTints;

            var selected = new GameObject("Selected", typeof(RectTransform));
            selected.transform.SetParent(rect, false);
            Stretch((RectTransform)selected.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var selectedShape = selected.AddComponent<TrapezoidShape>();
            selectedShape.FillRole = UIColorRole.Accent;
            selectedShape.Outline = false;
            selectedShape.Slant = TabSlant;
            selectedShape.UpsideDown = true;
            selectedShape.raycastTarget = false;
            TextMeshProUGUI text = MakeText(rect, "Label", label.ToUpperInvariant(), ButtonFontSize * 0.85f, TextAlignmentOptions.Center,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, display: true);
            text.raycastTarget = false;

            var tab = button.AddComponent<Tab>();
            tab.ID = label;
            tab.Group = group;
            tab.SelectedState = selected;
            tab.TabContents = contents;
            group.RegisterTab(tab);
            UnityEventTools.AddPersistentListener(clickable.onClick, tab.OnClicked);
            return tab;
        }

        // ---- The widgets a project uses in its own windows ----

        private static void MakeWidgets()
        {
            PrefabAt(WidgetPath("Button"), () => ThemedButtonObject("Button", ThemedButton.Kind.Primary, ButtonSize));
            PrefabAt(WidgetPath("List Row"), ListRow);
            PrefabAt(WidgetPath("Scrollbar"), ScrollbarWidget);
            PrefabAt(WidgetPath("Radio Toggle"), () => ToggleWidget("Radio Toggle", radio: true));
            PrefabAt(WidgetPath("Checkbox"), () => ToggleWidget("Checkbox", radio: false));
        }

        // A list's row (for a DataDisplay of a project's): a toggle on a panel row, its selection the accent's row with a
        // filled diamond bullet, else an outlined diamond, and its label.
        private static GameObject ListRow()
        {
            var root = new GameObject("List Row", typeof(RectTransform), typeof(LayoutElement));
            ((RectTransform)root.transform).sizeDelta = ListRowSize;
            root.GetComponent<LayoutElement>().preferredHeight = ListRowSize.y;
            var background = root.AddComponent<ChamferedBox>();
            background.Chamfer = SmallChamfer;
            background.Corners = UIShapeMesh.Corners.TopLeft | UIShapeMesh.Corners.BottomRight;

            var selected = new GameObject("Selected", typeof(RectTransform));
            selected.transform.SetParent(root.transform, false);
            Stretch((RectTransform)selected.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var selectedShape = selected.AddComponent<ChamferedBox>();
            selectedShape.FillRole = UIColorRole.Accent;
            selectedShape.Outline = false;
            selectedShape.Chamfer = SmallChamfer;
            selectedShape.Corners = background.Corners;
            selectedShape.raycastTarget = false;

            DiamondShape Bullet(Transform parent, string name, bool filled, UIColorRole role)
            {
                var obj = new GameObject(name, typeof(RectTransform));
                obj.transform.SetParent(parent, false);
                var rect = (RectTransform)obj.transform;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 0.5f);
                rect.sizeDelta = new Vector2(BulletSize, BulletSize);
                rect.anchoredPosition = new Vector2(Padding * 0.5f, 0f);
                var bullet = obj.AddComponent<DiamondShape>();
                bullet.Fill = filled;
                bullet.FillRole = role;
                bullet.Outline = !filled;
                bullet.raycastTarget = false;
                return bullet;
            }
            Bullet(root.transform, "Bullet", filled: false, UIColorRole.Outline);
            Bullet(selected.transform, "Selected Bullet", filled: true, UIColorRole.TextOnFill);
            TextMeshProUGUI label = MakeText(root.transform, "Label", "List Row", ButtonFontSize * 0.85f, TextAlignmentOptions.MidlineLeft, Vector2.zero, Vector2.one,
                new Vector2(Padding + BulletSize, 0f), new Vector2(-Padding * 0.5f, 0f), display: true);
            label.raycastTarget = false;

            var toggle = root.AddComponent<Toggle>();
            toggle.targetGraphic = background;
            toggle.graphic = selectedShape;
            toggle.colors = UITheme.Current.StateTints;
            toggle.isOn = false;
            return root;
        }

        // A vertical scrollbar: a raised track and the accent's handle.
        private static GameObject ScrollbarWidget()
        {
            var root = new GameObject("Scrollbar", typeof(RectTransform));
            ((RectTransform)root.transform).sizeDelta = ScrollbarSize;
            var track = root.AddComponent<ChamferedBox>();
            track.FillRole = UIColorRole.PanelRaised;
            track.Outline = false;
            track.Corners = UIShapeMesh.Corners.None;

            var area = new GameObject("Sliding Area", typeof(RectTransform));
            area.transform.SetParent(root.transform, false);
            Stretch((RectTransform)area.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var handle = new GameObject("Handle", typeof(RectTransform));
            handle.transform.SetParent(area.transform, false);
            Stretch((RectTransform)handle.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var handleShape = handle.AddComponent<ChamferedBox>();
            handleShape.FillRole = UIColorRole.Accent;
            handleShape.Outline = false;
            handleShape.Chamfer = ScrollbarSize.x / 2f;
            handleShape.Corners = UIShapeMesh.Corners.TopLeft | UIShapeMesh.Corners.BottomRight;

            var scrollbar = root.AddComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scrollbar.handleRect = (RectTransform)handle.transform;
            scrollbar.targetGraphic = handleShape;
            scrollbar.colors = UITheme.Current.StateTints;
            scrollbar.size = 0.3f;
            return root;
        }

        // A toggle: a radio toggle (an outlined diamond, filled with the accent while on) or a checkbox (an outlined square,
        // the accent's check while on), and its label.
        private static GameObject ToggleWidget(string name, bool radio)
        {
            var root = new GameObject(name, typeof(RectTransform));
            ((RectTransform)root.transform).sizeDelta = ToggleSize;

            var box = new GameObject("Box", typeof(RectTransform));
            box.transform.SetParent(root.transform, false);
            var boxRect = (RectTransform)box.transform;
            boxRect.anchorMin = boxRect.anchorMax = boxRect.pivot = new Vector2(0f, 0.5f);
            boxRect.sizeDelta = new Vector2(ToggleBoxSize, ToggleBoxSize);
            UIShapeGraphic frame;
            if (radio)
            {
                var diamond = box.AddComponent<DiamondShape>();
                diamond.Fill = false;
                frame = diamond;
            }
            else
            {
                var square = box.AddComponent<ChamferedBox>();
                square.Corners = UIShapeMesh.Corners.None;
                square.FillRole = UIColorRole.PanelRaised;
                frame = square;
            }

            var mark = new GameObject(radio ? "Dot" : "Check", typeof(RectTransform));
            mark.transform.SetParent(box.transform, false);
            float inset = radio ? ToggleBoxSize * 0.25f : ToggleBoxSize * 0.1f;
            Stretch((RectTransform)mark.transform, Vector2.zero, Vector2.one, Vector2.one * inset, -Vector2.one * inset);
            Graphic markShape;
            if (radio)
            {
                var dot = mark.AddComponent<DiamondShape>();
                dot.Outline = false;
                dot.FillRole = UIColorRole.Accent;
                markShape = dot;
            }
            else
            {
                var check = mark.AddComponent<UIIcon>();
                check.IconName = UIIconLibrary.Check;
                check.FillRole = UIColorRole.Accent;
                markShape = check;
            }
            markShape.raycastTarget = false;

            TextMeshProUGUI label = MakeText(root.transform, "Label", name, ButtonFontSize * 0.8f, TextAlignmentOptions.MidlineLeft, Vector2.zero, Vector2.one,
                new Vector2(ToggleBoxSize + Padding * 0.4f, 0f), Vector2.zero, display: false);
            label.raycastTarget = false;

            var toggle = root.AddComponent<Toggle>();
            toggle.targetGraphic = frame;
            toggle.graphic = markShape;
            toggle.colors = UITheme.Current.StateTints;
            toggle.isOn = true;
            return root;
        }
    }
}
