using System;
using System.Collections.Generic;
using UnityEngine;

namespace MeridianMotion.UI
{
    /// <summary>
    /// The toast service (UI_Framework_Blueprint, Notifications): ShowNotification shows a title and a message for a while,
    /// at a place on the screen (Location: a parent each), in a style (Type: a prefab each), at most Max Notif Count of a
    /// style at once (the oldest closed for a new one). A scene singleton (Instance), on the UI Manager's overlay canvas;
    /// the UIManager passes ShowNotification on. Add values to both enums for a game's own styles and places (a chat
    /// banner, say), and give each its prefab and parent.
    /// </summary>
    [AddComponentMenu("Meridian Motion/UI/Notifications")]
    public class Notifications : MonoBehaviour
    {
        /// <summary>Where on the screen a notification shows.</summary>
        public enum Location { Center, Top_Left, Top_Middle, Top_Right, Bottom_Left, Bottom_Middle, Bottom_Right }

        /// <summary>
        /// A notification's style: Passive, a toast that goes by itself; Center, a banner across the middle; and the toasts
        /// of each kind, each its icon and colour (Success, Warning, Info, Item: something gained, Locked: something not yet
        /// open). New values go on the end: a style's saved by its number.
        /// </summary>
        public enum Type { Passive, Center, Success, Warning, Info, Item, Locked }

        /// <summary>A style's prefab.</summary>
        [Serializable]
        public struct StylePrefab
        {
            public Type type;
            public NotificationEntry prefab;
        }

        /// <summary>A place's parent.</summary>
        [Serializable]
        public struct LocationParent
        {
            public Location location;
            public Transform parent;
        }

        /// <summary>The scene's notifications, or null.</summary>
        public static Notifications Instance { get; private set; }

        [Tooltip("The most notifications of one style shown at once: a new one closes the oldest")]
        [SerializeField] private int maxNotifCount = 3;
        [Tooltip("How long (s) a notification shows when none is given")]
        [SerializeField] private float defaultDuration = 4f;
        [Tooltip("Each style's prefab")]
        [SerializeField] private List<StylePrefab> prefabs = new();
        [Tooltip("Each place's parent (a layout group, say, so several stack)")]
        [SerializeField] private List<LocationParent> parents = new();

        private readonly List<NotificationEntry> notifs = new();

        /// <summary>The notifications shown, oldest first.</summary>
        public IReadOnlyList<NotificationEntry> Shown => notifs;
        /// <summary>How long a notification shows when none is given.</summary>
        public float DefaultDuration => defaultDuration;
        /// <summary>The most notifications of one style shown at once.</summary>
        public int MaxNotifCount { get => maxNotifCount; set => maxNotifCount = value; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning($"[Notifications] There are notifications already ('{Instance.name}'): this one ('{name}') is turned off.", this);
                enabled = false;
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) { Instance = null; }
        }

        /// <summary>A style's prefab, or null if it has none.</summary>
        public NotificationEntry PrefabOf(Type type) => prefabs.Find(entry => entry.type == type).prefab;

        /// <summary>Sets a style's prefab (for notifications set up in code: the tests').</summary>
        public void SetPrefab(Type type, NotificationEntry prefab)
        {
            prefabs.RemoveAll(entry => entry.type == type);
            prefabs.Add(new StylePrefab { type = type, prefab = prefab });
        }

        /// <summary>Sets a place's parent (for notifications set up in code: the tests').</summary>
        public void SetParent(Location location, Transform parent)
        {
            parents.RemoveAll(entry => entry.location == location);
            parents.Add(new LocationParent { location = location, parent = parent });
        }

        /// <summary>
        /// Shows a notification: the title and message, for duration seconds (the default, if 0 or less), at the location, in
        /// the style. Past Max Notif Count of its style, the oldest closes. Null, with an error, for a style without a prefab.
        /// </summary>
        public NotificationEntry ShowNotification(string title, string msg, float duration = 0f, Location location = Location.Top_Middle, Type type = Type.Passive)
        {
            NotificationEntry prefab = prefabs.Find(entry => entry.type == type).prefab;
            if (prefab == null)
            {
                Debug.LogError($"[Notifications] No prefab is set for the {type} style.", this);
                return null;
            }
            Transform parent = parents.Find(entry => entry.location == location).parent;
            NotificationEntry entry = Instantiate(prefab, parent != null ? parent : transform, false);
            if (!entry.gameObject.activeSelf) { entry.gameObject.SetActive(true); }
            notifs.Add(entry);
            entry.Initialize(title, msg, duration > 0f ? duration : defaultDuration, type, OnNotificationClosed);
            int ofType = notifs.FindAll(shown => shown != null && shown.NotificationType == type && !shown.IsClosing).Count;
            for (int i = 0; ofType > maxNotifCount && i < notifs.Count; i++)
            {
                NotificationEntry shown = notifs[i];
                if (shown == null || shown == entry || shown.NotificationType != type || shown.IsClosing) { continue; }
                shown.Close();
                ofType--;
            }
            return entry;
        }

        /// <summary>Closes every passive notification (the toasts), leaving the rest.</summary>
        public void ClearAllPassiveNotifications()
        {
            foreach (NotificationEntry entry in notifs.ToArray())
            {
                if (entry != null && entry.NotificationType == Type.Passive) { entry.Close(); }
            }
        }

        private void OnNotificationClosed(NotificationEntry entry) => notifs.Remove(entry);
    }
}
