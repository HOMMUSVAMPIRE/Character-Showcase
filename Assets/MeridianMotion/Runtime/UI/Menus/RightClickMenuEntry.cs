using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MeridianMotion.UI
{
    /// <summary>
    /// One entry of a context menu (UI_Framework_Blueprint, Context menu): its item's text and icon, on the background for
    /// where it is in its group (Position: alone, first, in the middle or last, so groups get rounded ends). Clicked (its
    /// button calls OnClicked), it runs its item's callback and closes the menu.
    /// </summary>
    [AddComponentMenu("Meridian Motion/UI/Right Click Menu Entry")]
    public class RightClickMenuEntry : MonoBehaviour
    {
        /// <summary>Where an entry is in its group, for its background.</summary>
        public enum Position { Solo, First, Middle, Last }

        [Tooltip("Where the item's text is shown")]
        [SerializeField] private TextMeshProUGUI label;
        [Tooltip("Where the item's icon is shown (hidden without one)")]
        [SerializeField] private Image icon;
        [Tooltip("The background of an entry alone in its group")]
        [SerializeField] private GameObject soloBackground;
        [Tooltip("The background of the first entry of a group")]
        [SerializeField] private GameObject firstBackground;
        [Tooltip("The background of an entry in the middle of a group")]
        [SerializeField] private GameObject middleBackground;
        [Tooltip("The background of the last entry of a group")]
        [SerializeField] private GameObject lastBackground;

        private UIRightClickMenu menu;

        /// <summary>The item it shows.</summary>
        public MenuItemData Item { get; private set; }
        /// <summary>Where it is in its group.</summary>
        public Position Place { get; private set; }

        /// <summary>Shows the item, on the background for its place in its group.</summary>
        public void Init(UIRightClickMenu menu, MenuItemData item, Position position)
        {
            this.menu = menu;
            Item = item;
            Place = position;
            if (label != null) { label.text = item.Text; }
            if (icon != null)
            {
                icon.sprite = item.Icon;
                UIUtility.SetActive(icon, item.Icon != null);
            }
            UIUtility.SetActive(soloBackground, position == Position.Solo);
            UIUtility.SetActive(firstBackground, position == Position.First);
            UIUtility.SetActive(middleBackground, position == Position.Middle);
            UIUtility.SetActive(lastBackground, position == Position.Last);
        }

        /// <summary>Clicked: its item's callback, then the menu closes.</summary>
        public void OnClicked()
        {
            if (menu == null || menu.IsClosing) { return; }
            Item?.OnSelectedCallback.Invoke();
            menu.Close();
        }
    }
}
