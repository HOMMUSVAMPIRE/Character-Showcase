using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace MeridianMotion.UI
{
    /// <summary>
    /// A context menu (UI_Framework_Blueprint, Context menu): a window built from a list of items (MenuItemData), leaving out
    /// those whose ShowIf says no, its spacers splitting the rest into groups. It opens at the pointer, flipped to stay on
    /// the screen, and closes once the pointer leaves it, or if the pointer never comes onto it (within Never Entered Close
    /// Seconds), or when an entry is clicked. Open it with Open (the UIManager's Right Click Menu Prefab).
    /// </summary>
    [AddComponentMenu("Meridian Motion/UI/Right Click Menu")]
    public class UIRightClickMenu : RootUIPrefab, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("Entries")]
        [Tooltip("Where the entries go (a vertical layout group)")]
        [SerializeField] private RectTransform entryParent;
        [Tooltip("An entry (with a RightClickMenuEntry on its root)")]
        [SerializeField] private GameObject entryPrefab;
        [Tooltip("A featured entry (the main choice); an ordinary one if empty")]
        [SerializeField] private GameObject featuredEntryPrefab;
        [Tooltip("What's put between groups")]
        [SerializeField] private GameObject spacerEntryPrefab;
        [Tooltip("How long (s) it stays open if the pointer never comes onto it")]
        [SerializeField] private float neverEnteredCloseSeconds = 1.5f;

        private readonly List<GameObject> built = new();
        private bool entered;
        private float openedAt;

        /// <summary>The entries shown, in order.</summary>
        public List<RightClickMenuEntry> Entries { get; } = new();

        /// <summary>Opens the menu at the pointer (or at screenPosition, in pixels) with the items. Null without a UIManager or its prefab.</summary>
        public static UIRightClickMenu Open(List<MenuItemData> items, Vector2? screenPosition = null)
        {
            if (UIManager.instance == null || UIManager.instance.RightClickMenuPrefab == null)
            {
                Debug.LogError("[UIRightClickMenu] There's no UIManager, or its Right Click Menu Prefab isn't set.");
                return null;
            }
            UIRightClickMenu menu = UIManager.instance.OpenUI<UIRightClickMenu>(UIManager.instance.RightClickMenuPrefab);
            if (menu == null) { return null; }
            menu.Init(items);
            Vector2 at = screenPosition ?? (Pointer.current != null ? Pointer.current.position.ReadValue() : new Vector2(Screen.width, Screen.height) / 2f);
            menu.PlaceAt(at);
            return menu;
        }

        /// <summary>Builds its entries from the items (see the class), and returns the rect they take, in its own space.</summary>
        public Rect Init(List<MenuItemData> items)
        {
            foreach (GameObject obj in built) { if (obj != null) { Destroy(obj); } }
            built.Clear();
            Entries.Clear();
            entered = false;
            openedAt = UIClock.Now;
            Transform parent = entryParent != null ? entryParent : transform;

            // The groups: the items shown, split at the spacers (an empty group left out).
            var groups = new List<List<MenuItemData>> { new() };
            foreach (MenuItemData item in items ?? new List<MenuItemData>())
            {
                if (item == null) { continue; }
                if (item.IsSpacer) { groups.Add(new List<MenuItemData>()); }
                else if (item.ShowIf == null || item.ShowIf()) { groups[^1].Add(item); }
            }
            groups.RemoveAll(group => group.Count == 0);
            for (int g = 0; g < groups.Count; g++)
            {
                if (g > 0 && spacerEntryPrefab != null) { built.Add(Instantiate(spacerEntryPrefab, parent, false)); }
                for (int i = 0; i < groups[g].Count; i++)
                {
                    MenuItemData item = groups[g][i];
                    GameObject prefab = item.Featured && featuredEntryPrefab != null ? featuredEntryPrefab : entryPrefab;
                    if (prefab == null) { continue; }
                    GameObject obj = Instantiate(prefab, parent, false);
                    if (!obj.activeSelf) { obj.SetActive(true); } // A prefab kept inactive (a template) shows all the same.
                    built.Add(obj);
                    var entry = obj.GetComponent<RightClickMenuEntry>();
                    if (entry == null) { continue; }
                    int count = groups[g].Count;
                    RightClickMenuEntry.Position position = count == 1 ? RightClickMenuEntry.Position.Solo
                        : i == 0 ? RightClickMenuEntry.Position.First
                        : i == count - 1 ? RightClickMenuEntry.Position.Last
                        : RightClickMenuEntry.Position.Middle;
                    entry.Init(this, item, position);
                    Entries.Add(entry);
                }
            }
            var rect = (RectTransform)transform;
            LayoutRebuildHelper.RebuildLayout(rect);
            return rect.rect;
        }

        /// <summary>
        /// Puts its corner at the screen position (in pixels), opening down and to the right, or flipped to open up or left
        /// where it would run off the screen.
        /// </summary>
        public void PlaceAt(Vector2 screenPosition)
        {
            var rect = (RectTransform)transform;
            var canvas = transform.parent as RectTransform;
            if (canvas == null) { return; }
            Canvas root = canvas.GetComponentInParent<Canvas>();
            float scale = root != null ? root.scaleFactor : 1f;
            Vector2 size = rect.rect.size * scale;
            bool flipX = screenPosition.x + size.x > Screen.width;
            bool flipY = screenPosition.y - size.y < 0f;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(flipX ? 1f : 0f, flipY ? 0f : 1f);
            Camera camera = root != null && root.renderMode != RenderMode.ScreenSpaceOverlay ? root.worldCamera : null;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas, screenPosition, camera, out Vector2 local);
            rect.localPosition = local;
        }

        protected override void Update()
        {
            base.Update();
            if (!entered && !IsClosing && UIClock.Now - openedAt > neverEnteredCloseSeconds) { Close(); }
        }

        /// <summary>The pointer came onto it: it stays open until the pointer leaves.</summary>
        public void OnPointerEnter(PointerEventData eventData) => OnPointerEnter();

        /// <summary>The pointer left it: it closes.</summary>
        public void OnPointerExit(PointerEventData eventData) => OnPointerExit();

        /// <summary>The pointer came onto it.</summary>
        public void OnPointerEnter() => entered = true;

        /// <summary>The pointer left it: it closes.</summary>
        public void OnPointerExit()
        {
            if (entered) { Close(); }
        }
    }
}
