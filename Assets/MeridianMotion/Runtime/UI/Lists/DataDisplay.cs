using System;
using UnityEngine;
using UnityEngine.UI;

namespace MeridianMotion.UI
{
    /// <summary>
    /// One row of a data list, drawing one record (UI_Framework_Blueprint, Generic data lists): a game's row derives it and
    /// overrides Refresh (draw Data) and InitializeFromID (draw the record of an ID, found its own way). Rows are pooled and
    /// reused by their list (DataDisplayList), never destroyed; selecting one (its Toggle, in the list's ToggleGroup, or
    /// Select) tells the list.
    /// </summary>
    public abstract class DataDisplay<T> : MonoBehaviour where T : Identifiable
    {
        [Tooltip("The toggle that selects this row (in its list's toggle group), if it can be selected by clicking it")]
        [SerializeField] private Toggle toggle;

        /// <summary>What the row tells when it's selected: its list's callback.</summary>
        protected Action<T> OnSelectedAction;

        /// <summary>The record drawn.</summary>
        public T Data { get; private set; }

        /// <summary>Whether the row is selected.</summary>
        public bool Selected { get; private set; }

        /// <summary>The row's toggle, or null.</summary>
        public Toggle Toggle { get => toggle; set => toggle = value; }

        protected virtual void Awake()
        {
            if (toggle != null) { toggle.onValueChanged.AddListener(OnToggleChanged); }
        }

        /// <summary>Draws the record, and tells onSelected when the row is selected.</summary>
        public void Initialize(T data, Action<T> onSelected)
        {
            Data = data;
            OnSelectedAction = onSelected;
            Refresh();
        }

        /// <summary>Draws the record of the ID (found as the row's own kind knows how: a game's lookup).</summary>
        public abstract void InitializeFromID(string id);

        /// <summary>Draws Data.</summary>
        protected abstract void Refresh();

        /// <summary>Selects the row (telling its list, so the list's panel hears) or deselects it.</summary>
        public void Select(bool selected)
        {
            if (toggle != null && toggle.isOn != selected)
            {
                toggle.isOn = selected; // Its change tells the list (OnToggleChanged).
                return;
            }
            SetSelected(selected);
        }

        /// <summary>Puts the row's toggle in the list's group (one selected at a time).</summary>
        public void SetToggleGroup(ToggleGroup group)
        {
            if (toggle != null) { toggle.group = group; }
        }

        private void OnToggleChanged(bool on) => SetSelected(on);

        private void SetSelected(bool selected)
        {
            Selected = selected;
            if (selected && Data != null) { OnSelectedAction?.Invoke(Data); }
        }
    }
}
