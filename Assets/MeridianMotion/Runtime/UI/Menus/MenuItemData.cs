using System;
using UnityEngine;
using UnityEngine.Events;

namespace MeridianMotion.UI
{
    /// <summary>
    /// One item of a context menu (UIRightClickMenu, UI_Framework_Blueprint, Context menu): its text and icon, whether it's
    /// featured (drawn as the main choice), what it does (OnSelectedCallback), when it's shown (ShowIf: always, if null), or
    /// a spacer splitting the items into groups.
    /// </summary>
    public class MenuItemData
    {
        public string Text;
        public Sprite Icon;
        public bool Featured;
        public UnityEvent OnSelectedCallback = new();
        public Func<bool> ShowIf;
        public bool IsSpacer;

        public MenuItemData()
        {
        }

        /// <summary>An item with its text, calling onSelected when chosen.</summary>
        public MenuItemData(string text, Action onSelected, Sprite icon = null, bool featured = false, Func<bool> showIf = null)
        {
            Text = text;
            Icon = icon;
            Featured = featured;
            ShowIf = showIf;
            if (onSelected != null) { OnSelectedCallback.AddListener(() => onSelected()); }
        }

        /// <summary>A spacer: the items before it and after it are separate groups.</summary>
        public static MenuItemData Spacer => new() { IsSpacer = true };
    }
}
