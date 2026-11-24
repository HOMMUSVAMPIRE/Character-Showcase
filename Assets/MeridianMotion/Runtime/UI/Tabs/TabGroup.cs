using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace MeridianMotion.UI
{
    /// <summary>
    /// A set of tabs, one selected at a time (UI_Framework_Blueprint, Tabs): clicking a tab (Tab.OnClicked) selects it and
    /// deselects the rest, or, with Allow Deselection, deselects it if it's selected already. Each tab shows its own content,
    /// or opens a window it tracks (a toolbar of toggle buttons kept in step with the windows they open). Tabs register
    /// themselves; the Initial Tab Enabled is selected as it starts (else, without Allow Deselection, the first).
    /// </summary>
    [AddComponentMenu("Meridian Motion/UI/Tab Group")]
    public class TabGroup : MonoBehaviour
    {
        [Tooltip("Whether clicking the selected tab deselects it, leaving none selected (and closing a tracked window deselects its tab)")]
        [SerializeField] private bool allowDeselection;
        [Tooltip("The tabs, in order (each registers itself too)")]
        [SerializeField] private List<Tab> tabs = new();
        [Tooltip("The tab selected as it starts (none: the first, unless Allow Deselection)")]
        [SerializeField] private Tab initialTabEnabled;
        [Tooltip("Raised when which tab is selected changes")]
        [SerializeField] private UnityEvent OnSelectionChanged = new();

        /// <summary>The selected tab, or null.</summary>
        public Tab SelectedTab { get; private set; }

        /// <summary>The tabs, in order.</summary>
        public IReadOnlyList<Tab> Tabs => tabs;

        /// <summary>Whether clicking the selected tab deselects it.</summary>
        public bool AllowDeselection { get => allowDeselection; set => allowDeselection = value; }

        /// <summary>The tab selected as it starts.</summary>
        public Tab InitialTabEnabled { get => initialTabEnabled; set => initialTabEnabled = value; }

        /// <summary>Raised when which tab is selected changes.</summary>
        public UnityEvent SelectionChanged => OnSelectionChanged;

        private bool started;

        private void Start()
        {
            started = true;
            Tab initial = initialTabEnabled != null ? initialTabEnabled : allowDeselection || tabs.Count == 0 ? null : tabs[0];
            foreach (Tab tab in tabs) { if (tab != null) { tab.SelectTab(tab == initial, isDeselection: false); } }
            SelectedTab = initial;
        }

        /// <summary>Adds a tab to the group (once).</summary>
        public void RegisterTab(Tab tab)
        {
            if (tab == null || tabs.Contains(tab)) { return; }
            tabs.Add(tab);
            if (started) { tab.SelectTab(false, isDeselection: false); }
        }

        /// <summary>
        /// A tab was clicked: it's selected and the rest deselected, or, with Allow Deselection and it selected already, it's
        /// deselected.
        /// </summary>
        public void OnTabClicked(Tab clicked)
        {
            if (clicked == null) { return; }
            bool deselect = allowDeselection && clicked == SelectedTab;
            Tab selected = deselect ? null : clicked;
            if (selected == SelectedTab)
            {
                // Clicked again, staying selected: selected again, so a tracked window closed meanwhile opens again.
                if (selected != null) { selected.SelectTab(true, isDeselection: false); }
                return;
            }
            SelectedTab = selected;
            foreach (Tab tab in tabs)
            {
                if (tab != null) { tab.SelectTab(tab == selected, isDeselection: deselect && tab == clicked); }
            }
            OnSelectionChanged.Invoke();
        }

        /// <summary>A tracked window closed by itself (the Back key, say): its tab is deselected, if the group allows none selected.</summary>
        internal void OnTrackedWindowClosed(Tab tab)
        {
            if (allowDeselection && tab == SelectedTab) { OnTabClicked(tab); }
        }
    }
}
