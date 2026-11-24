using TMPro;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;

namespace MeridianMotion.UI.EditorTools
{
    // The widgets' prefabs: the context menu and its entries, the notifications and the tab buttons.
    public static partial class UIAssets
    {
        private static readonly Vector2 MenuEntrySize = new(280f, 44f), TabButtonSize = new(200f, 44f);
        private static readonly Vector2 PassiveNotificationSize = new(440f, 110f), CenterNotificationSize = new(960f, 150f);
        private const float MenuSpacerHeight = 8f, TabRowHeight = 56f, TabUnderline = 4f, NotificationGap = 12f, IconSize = 24f;

        // Unity's own rounded sprite, for the ends of a menu's groups.
        private static Sprite Rounded => UnityEditor.AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

        // A context menu's entry: its label and icon on a background for each place in a group (rounded at a group's
        // ends), featured ones in the accent colour; its button calls OnClicked.
        private static GameObject MenuEntry(string name, bool featured)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(LayoutElement), typeof(Button));
            var layout = root.GetComponent<LayoutElement>();
            layout.preferredWidth = MenuEntrySize.x;
            layout.preferredHeight = MenuEntrySize.y;
            var entry = root.AddComponent<RightClickMenuEntry>();
            Color colour = featured ? Orange : Graphite;
            GameObject Background(string place, bool rounded)
            {
                var obj = new GameObject($"{place} Background", typeof(RectTransform), typeof(Image));
                obj.transform.SetParent(root.transform, false);
                Stretch((RectTransform)obj.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                var image = obj.GetComponent<Image>();
                image.color = colour;
                if (rounded)
                {
                    image.sprite = Rounded;
                    image.type = Image.Type.Sliced;
                }
                return obj;
            }
            GameObject solo = Background("Solo", true), first = Background("First", true), middle = Background("Middle", false), last = Background("Last", true);
            root.GetComponent<Button>().targetGraphic = middle.GetComponent<Image>();

            var icon = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            icon.transform.SetParent(root.transform, false);
            var iconRect = (RectTransform)icon.transform;
            iconRect.anchorMin = iconRect.anchorMax = iconRect.pivot = new Vector2(0f, 0.5f);
            iconRect.sizeDelta = new Vector2(IconSize, IconSize);
            iconRect.anchoredPosition = new Vector2(Padding * 0.5f, 0f);
            TextMeshProUGUI label = MakeText(root.transform, "Label", "", ButtonFontSize, TextAlignmentOptions.MidlineLeft, Vector2.zero, Vector2.one,
                new Vector2(Padding * 0.5f + IconSize + Padding * 0.5f, 0f), new Vector2(-Padding * 0.5f, 0f));

            var fields = new UnityEditor.SerializedObject(entry);
            fields.FindProperty("label").objectReferenceValue = label;
            fields.FindProperty("icon").objectReferenceValue = icon.GetComponent<Image>();
            fields.FindProperty("soloBackground").objectReferenceValue = solo;
            fields.FindProperty("firstBackground").objectReferenceValue = first;
            fields.FindProperty("middleBackground").objectReferenceValue = middle;
            fields.FindProperty("lastBackground").objectReferenceValue = last;
            fields.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(root.GetComponent<Button>().onClick, entry.OnClicked);
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
            var fields = new UnityEditor.SerializedObject(menu);
            fields.FindProperty("entryParent").objectReferenceValue = root.transform;
            fields.FindProperty("entryPrefab").objectReferenceValue = entry;
            fields.FindProperty("featuredEntryPrefab").objectReferenceValue = featuredEntry;
            fields.FindProperty("spacerEntryPrefab").objectReferenceValue = spacer;
            fields.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        // A notification: a passive toast (a graphite card: title, message, Read More and close), or a banner across the
        // middle (wider, its title larger, the orange accent).
        private static GameObject NotificationPrefab(string name, bool center)
        {
            Vector2 size = center ? CenterNotificationSize : PassiveNotificationSize;
            var root = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            ((RectTransform)root.transform).sizeDelta = size;
            root.GetComponent<LayoutElement>().preferredWidth = size.x;
            root.GetComponent<LayoutElement>().preferredHeight = size.y;
            root.GetComponent<Image>().color = Graphite;
            var entry = root.AddComponent<NotificationEntry>();

            var accent = new GameObject("Accent", typeof(RectTransform), typeof(Image));
            accent.transform.SetParent(root.transform, false);
            Stretch((RectTransform)accent.transform, Vector2.zero, new Vector2(0f, 1f), Vector2.zero, new Vector2(AccentHeight, 0f));
            accent.GetComponent<Image>().color = center ? Orange : Cyan;

            float inset = Padding * 0.5f + AccentHeight;
            TextMeshProUGUI title = MakeText(root.transform, "Title", "", center ? TitleFontSize : ButtonFontSize, TextAlignmentOptions.TopLeft,
                new Vector2(0f, 0.5f), Vector2.one, new Vector2(inset, 0f), new Vector2(-Padding * 2f, -Padding * 0.5f));
            title.fontStyle = FontStyles.Bold;
            TextMeshProUGUI message = MakeText(root.transform, "Message", "", ButtonFontSize * 0.8f, TextAlignmentOptions.TopLeft,
                Vector2.zero, new Vector2(1f, 0.5f), new Vector2(inset, Padding * 0.5f), new Vector2(-Padding * 2f, 0f));
            GameObject readMore = MakeButton(root.transform, "Read More", SteelGrey, entry.OnReadMoreClicked, new Vector2(140f, 32f),
                new Vector2(1f, 0f), new Vector2(-Padding * 0.5f, Padding * 0.25f));
            MakeButton(root.transform, "X", SteelGrey, entry.Close, new Vector2(32f, 32f), Vector2.one, new Vector2(-Padding * 0.25f, -Padding * 0.25f));

            var fields = new UnityEditor.SerializedObject(entry);
            fields.FindProperty("titleText").objectReferenceValue = title;
            fields.FindProperty("messageText").objectReferenceValue = message;
            fields.FindProperty("readMoreButton").objectReferenceValue = readMore;
            fields.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        // The UI Manager's notifications, if it has none: on its overlay canvas, a parent for each place, stacking its
        // notifications, and the two styles' prefabs.
        private static void AddNotifications(UIManager manager, GameObject passive, GameObject center)
        {
            if (manager.GetComponentInChildren<Notifications>(true) != null || manager.overlayCanvas == null) { return; }
            var root = new GameObject("Notifications", typeof(RectTransform));
            root.transform.SetParent(manager.overlayCanvas, false);
            Stretch((RectTransform)root.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var notifications = root.AddComponent<Notifications>();
            notifications.SetPrefab(Notifications.Type.Passive, passive.GetComponent<NotificationEntry>());
            notifications.SetPrefab(Notifications.Type.Center, center.GetComponent<NotificationEntry>());
            foreach (Notifications.Location location in System.Enum.GetValues(typeof(Notifications.Location)))
            {
                string place = location.ToString();
                Vector2 anchor = new(place.EndsWith("Left") ? 0f : place.EndsWith("Right") ? 1f : 0.5f,
                    place.StartsWith("Top") ? 1f : place.StartsWith("Bottom") ? 0f : 0.5f);
                var parent = new GameObject(place.Replace('_', ' '), typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
                parent.transform.SetParent(root.transform, false);
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
                notifications.SetParent(location, rect);
            }
        }

        // A tab button along the top of a window: its label, an orange underline while selected; its content the page.
        private static Tab TabButton(RectTransform window, TabGroup group, string label, int index, GameObject contents)
        {
            GameObject button = TMP_DefaultControls.CreateButton(new TMP_DefaultControls.Resources());
            button.name = $"{label} Tab";
            button.transform.SetParent(window, false);
            var rect = (RectTransform)button.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = TabButtonSize;
            rect.anchoredPosition = new Vector2(Padding + index * (TabButtonSize.x + Padding * 0.5f), -AccentHeight - (TabRowHeight - TabButtonSize.y) / 2f);
            button.GetComponent<Image>().color = Graphite;
            var text = button.GetComponentInChildren<TextMeshProUGUI>();
            text.text = label;
            text.fontSize = ButtonFontSize;
            text.color = OffWhite;
            var underline = new GameObject("Selected", typeof(RectTransform), typeof(Image));
            underline.transform.SetParent(rect, false);
            Stretch((RectTransform)underline.transform, Vector2.zero, new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, TabUnderline));
            underline.GetComponent<Image>().color = Orange;

            var tab = button.AddComponent<Tab>();
            tab.ID = label;
            tab.Group = group;
            tab.SelectedState = underline;
            tab.TabContents = contents;
            group.RegisterTab(tab);
            UnityEventTools.AddPersistentListener(button.GetComponent<Button>().onClick, tab.OnClicked);
            return tab;
        }
    }
}
