using System.Collections.Generic;
using MeridianMotion.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace MeridianMotion.UI.EditorTools
{
    public static partial class UIGallery
    {
        // The UI's own windows (H5): the dialogs and the pause menu, as their prefabs are.
        private static void WindowsPage(GalleryPage page)
        {
            page.Row("Dialogs");
            Body(Instance(page, "Message Box", UIAssets.MessageBoxPrefabPath, 0.7f), "The course record has been saved.");
            Body(Instance(page, "Yes No Dialog", UIAssets.YesNoDialogPrefabPath, 0.7f), "Quit to the desktop? Your lap times are kept.");
            GameObject prompt = Instance(page, "Prompt For String", UIAssets.PromptForStringPrefabPath, 0.7f);
            Body(prompt, "Name this route:");

            page.Row("Pause menu");
            Instance(page, "Pause Menu", UIAssets.PauseMenuPrefabPath, 0.62f);
            GameObject controls = Instance(page, "Pause Menu: Controls", UIAssets.PauseMenuPrefabPath, 0.62f);
            Show(controls, "Main Page", false);
            Show(controls, "Controls Page", true);
            Show(controls, "Menu Tab/Selected", false);
            Show(controls, "Controls Tab/Selected", true);
            Text(controls, "Controls Page/Bindings", "Move            WASD, Left Stick\nJump            Space, South Button\nCrouch          C, East Button\nSprint          Left Shift, Left Stick Press\nInteract        E, West Button");
        }

        // The widgets (H5): the buttons in each kind and state, list rows, a scrollbar, toggles, the context menu's entries
        // and every notification style.
        private static void WidgetsPage(GalleryPage page)
        {
            page.Row("Buttons");
            foreach (ThemedButton.Kind kind in System.Enum.GetValues(typeof(ThemedButton.Kind)))
            {
                ThemedButton button = Instance(page, kind.ToString(), UIAssets.WidgetPath("Button"), 1f).GetComponent<ThemedButton>();
                button.ButtonKind = kind;
                Text(button.gameObject, "Label", kind.ToString().ToUpperInvariant());
            }
            ThemedButton disabled = Instance(page, "Disabled", UIAssets.WidgetPath("Button"), 1f).GetComponent<ThemedButton>();
            disabled.interactable = false;
            disabled.ShowState(ThemedButton.PreviewState.Disabled);
            Text(disabled.gameObject, "Label", "LOCKED");

            page.Row("States");
            foreach (ThemedButton.PreviewState state in System.Enum.GetValues(typeof(ThemedButton.PreviewState)))
            {
                ThemedButton button = Instance(page, state.ToString(), UIAssets.WidgetPath("Button"), 1f).GetComponent<ThemedButton>();
                if (state == ThemedButton.PreviewState.Disabled) { button.interactable = false; }
                button.ShowState(state);
                Text(button.gameObject, "Label", state.ToString().ToUpperInvariant());
            }

            page.Row("Lists, menus");
            RectTransform list = page.Add("List rows, scrollbar", new Vector2(520f, 160f));
            for (int i = 0; i < 3; i++)
            {
                GameObject row = Child(list, UIAssets.WidgetPath("List Row"), new Vector2(0f, -i * 54f), 1f);
                Text(row, "Label", new[] { "Quarry Run", "Relay Tower", "Basin Descent" }[i]);
                row.GetComponent<Toggle>().isOn = i == 0;
            }
            GameObject scrollbar = Child(list, UIAssets.WidgetPath("Scrollbar"), new Vector2(496f, 0f), 0.5f);
            scrollbar.GetComponent<Scrollbar>().size = 0.45f;
            RectTransform menu = page.Add("Entries: a group of three, then featured", new Vector2(280f, 190f));
            string[] places = { "First", "Middle", "Last" };
            for (int i = 0; i < places.Length; i++) { MenuEntry(menu, UIAssets.MenuEntryPrefabPath, places[i], new[] { "Inspect", "Copy route", "Share" }[i], -i * 46f); }
            MenuEntry(menu, UIAssets.FeaturedMenuEntryPrefabPath, "Solo", "Start course", -3f * 46f - 8f);


            page.Row("Toggles");
            foreach ((string path, bool on, string label) in new[]
            {
                (UIAssets.WidgetPath("Radio Toggle"), true, "Radio, on"), (UIAssets.WidgetPath("Radio Toggle"), false, "Radio, off"),
                (UIAssets.WidgetPath("Checkbox"), true, "Checkbox, on"), (UIAssets.WidgetPath("Checkbox"), false, "Checkbox, off"),
            })
            {
                GameObject toggle = Instance(page, label, path, 1f);
                toggle.GetComponent<Toggle>().isOn = on;
                Text(toggle, "Label", label);
            }

            page.Row("Notifications");
            int count = 0;
            foreach (Notifications.Type type in System.Enum.GetValues(typeof(Notifications.Type)))
            {
                if (type == Notifications.Type.Center) { continue; }
                if (count++ == 4) { page.Row(""); }
                GameObject note = Instance(page, type.ToString(), UIAssets.NotificationPrefabPath(type), 0.8f);
                Text(note, "Title", type.ToString().ToUpperInvariant());
                Text(note, "Message", "Checkpoint reached at 01:12.40.");
            }
        }

        // A prefab's instance in a slot of the row, scaled.
        private static GameObject Instance(GalleryPage page, string caption, string path, float scale)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Vector2 size = prefab != null ? ((RectTransform)prefab.transform).sizeDelta * scale : new Vector2(120f, 40f);
            RectTransform slot = page.Add(caption, size);
            if (prefab == null) { return slot.gameObject; }
            return Child(slot, path, Vector2.zero, scale);
        }

        // A prefab's instance in parent, its top left at position, scaled.
        private static GameObject Child(RectTransform parent, string path, Vector2 position, float scale)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            SetLayer(instance.transform, parent.gameObject.layer);
            var rect = (RectTransform)instance.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.localScale = Vector3.one * scale;
            return instance;
        }

        private static void MenuEntry(RectTransform parent, string path, string place, string label, float y)
        {
            GameObject entry = Child(parent, path, new Vector2(0f, y), 1f);
            ((RectTransform)entry.transform).sizeDelta = new Vector2(280f, 44f);
            foreach (string background in new[] { "Solo", "First", "Middle", "Last" }) { Show(entry, $"{background} Background", background == place); }
            Text(entry, "Label", label);
            Show(entry, "Icon", false);
        }

        private static void Body(GameObject dialog, string text) => Text(dialog, "Body", text);

        private static void Text(GameObject root, string path, string text)
        {
            Transform found = root.transform.Find(path);
            if (found != null && found.TryGetComponent(out TMP_Text label)) { label.text = text; }
        }

        private static void Show(GameObject root, string path, bool shown)
        {
            Transform found = root.transform.Find(path);
            if (found != null) { found.gameObject.SetActive(shown); }
        }

        private static void SetLayer(Transform transform, int layer)
        {
            transform.gameObject.layer = layer;
            foreach (Transform child in transform) { SetLayer(child, layer); }
        }
    }
}
