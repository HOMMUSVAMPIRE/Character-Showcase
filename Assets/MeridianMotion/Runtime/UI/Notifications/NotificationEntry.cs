using System;
using TMPro;
using UnityEngine;

namespace MeridianMotion.UI
{
    /// <summary>
    /// One notification (UI_Framework_Blueprint, Notifications): its title and message, shown until its duration's up (on
    /// the UIClock, so it runs while the game is paused) or it's closed. A long message shows its first line until Read
    /// More is clicked (its button calls OnReadMoreClicked), which shows it all and keeps it until it's closed. Its close
    /// button calls Close.
    /// </summary>
    [AddComponentMenu("Meridian Motion/UI/Notification Entry")]
    public class NotificationEntry : MonoBehaviour
    {
        [Tooltip("Where the title is shown")]
        [SerializeField] private TextMeshProUGUI titleText;
        [Tooltip("Where the message is shown")]
        [SerializeField] private TextMeshProUGUI messageText;
        [Tooltip("Shown when the message is longer than its first line, to show it all")]
        [SerializeField] private GameObject readMoreButton;

        private Action<NotificationEntry> onClosed;
        private float shownAt, duration;
        private bool kept;

        /// <summary>Its style.</summary>
        public Notifications.Type NotificationType { get; private set; }
        /// <summary>Its title.</summary>
        public string Title { get; private set; }
        /// <summary>Its whole message.</summary>
        public string Message { get; private set; }
        /// <summary>Whether it's closing (it's destroyed at the end of the frame).</summary>
        public bool IsClosing { get; private set; }
        /// <summary>Whether all its message shows (its first line only, else).</summary>
        public bool Expanded { get; private set; }

        /// <summary>Shows the title and message for duration seconds in its style; onClosed is told when it closes.</summary>
        public void Initialize(string title, string msg, float duration, Notifications.Type type, Action<NotificationEntry> onClosed)
        {
            Title = title ?? "";
            Message = msg ?? "";
            this.duration = duration;
            NotificationType = type;
            this.onClosed = onClosed;
            shownAt = UIClock.Now;
            if (titleText != null) { titleText.text = Title; }
            bool longer = Message.Contains('\n');
            UIUtility.SetActive(readMoreButton, longer);
            ShowMessage(!longer);
        }

        private void Update()
        {
            if (!IsClosing && !kept && UIClock.Now - shownAt >= duration) { Close(); }
        }

        /// <summary>Closes it.</summary>
        public void Close()
        {
            if (IsClosing) { return; }
            IsClosing = true;
            onClosed?.Invoke(this);
            Destroy(gameObject);
        }

        /// <summary>Read More: shows all its message, and keeps it until it's closed.</summary>
        public void OnReadMoreClicked()
        {
            kept = true;
            UIUtility.SetActive(readMoreButton, false);
            ShowMessage(true);
        }

        private void ShowMessage(bool all)
        {
            Expanded = all;
            if (messageText != null) { messageText.text = all ? Message : Message.Split('\n')[0]; }
        }

        private void OnDestroy()
        {
            if (!IsClosing) { onClosed?.Invoke(this); }
        }
    }
}
