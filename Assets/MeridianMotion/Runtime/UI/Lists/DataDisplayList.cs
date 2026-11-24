using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MeridianMotion.UI
{
    /// <summary>
    /// A list of records (UI_Framework_Blueprint, Generic data lists, and How a list refreshes): it fetches them (GetData,
    /// a game's data source: the only thing a list must override), keeps those of the IDs it's given (Populate) that match
    /// every word of its search terms (HasSearchTerm), and shows each in a row (U, a DataDisplay&lt;T&gt;) from its pool:
    /// rows are made as more are needed (from its item prefabs in turn, for alternating styles), reused, and hidden when
    /// unused, never destroyed. Its selection (SelectedItemID) is kept across refreshes (else the first row's), passed up
    /// from the row by a callback, then to its panel by OnItemSelected, wired in the inspector. With no rows its "shown if no
    /// items" objects show (an empty state), else its "shown if items".
    /// </summary>
    public abstract class DataDisplayList<T, U> : MonoBehaviour where T : Identifiable where U : DataDisplay<T>
    {
        [Header("Rows")]
        [Tooltip("The row prefabs (each with a U on its root), used in turn: two give alternating rows")]
        [SerializeField] protected List<GameObject> itemPrefabs = new();
        [Tooltip("Where the rows go (a layout group, say); the list's own transform if empty")]
        [SerializeField] protected Transform itemsParent;
        [Tooltip("The group the rows' toggles are in, so one is selected at a time")]
        [SerializeField] protected ToggleGroup toggleGroup;

        [Header("States")]
        [Tooltip("Shown while there are rows")]
        [SerializeField] protected List<GameObject> shownIfItems = new();
        [Tooltip("Shown while there are none (an empty state)")]
        [SerializeField] protected List<GameObject> shownIfNoItems = new();
        [Tooltip("Shown while it's fetching its records (a spinner)")]
        [SerializeField] protected GameObject loadingIndicator;
        [Tooltip("Hidden while it's fetching its records (a refresh button)")]
        [SerializeField] protected GameObject refreshButton;

        [Header("Selection")]
        [Tooltip("Raised with the record selected: wire the list's panel here")]
        [SerializeField] protected IdentifiableEvent<T> OnItemSelected = new();

        /// <summary>The rows, shown and pooled, in order.</summary>
        protected readonly List<U> displayItems = new();

        /// <summary>The search terms: a record is shown if it matches every word (HasSearchTerm). Set with SetSearchTerms.</summary>
        public string searchTerms = "";

        /// <summary>The selected record's ID (kept across refreshes), or null.</summary>
        public string SelectedItemID { get; set; }

        /// <summary>Whether it's refreshing (a Refresh asked for meanwhile is ignored).</summary>
        public bool IsUpdating { get; private set; }

        /// <summary>Raised with the record selected (as OnItemSelected).</summary>
        public IdentifiableEvent<T> ItemSelected => OnItemSelected;

        /// <summary>The rows shown, in order.</summary>
        public IEnumerable<U> ShownItems
        {
            get
            {
                foreach (U row in displayItems) { if (row != null && row.gameObject.activeSelf) { yield return row; } }
            }
        }

        // The records to show in place of GetData's (Populate(data)), and the IDs to keep (Populate(ids)), or null.
        private List<T> overrideData;
        private HashSet<string> dataIDs;

        /// <summary>
        /// The records, by ID: a game's data source (fetched, loaded or simply returned). The order they come in is the
        /// order they're shown in.
        /// </summary>
        protected abstract Awaitable<Dictionary<string, T>> GetData();

        /// <summary>Whether a record matches one word of the search (by default, its ID contains it, ignoring case).</summary>
        protected virtual bool HasSearchTerm(T data, string term) =>
            data.ID != null && data.ID.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0;

        /// <summary>Shows only the records of these IDs (all of them, for null), and refreshes.</summary>
        public void Populate(List<string> ids)
        {
            overrideData = null;
            dataIDs = ids != null ? new HashSet<string>(ids) : null;
            RefreshAndForget();
        }

        /// <summary>Shows these records rather than GetData's, and refreshes.</summary>
        public void Populate(List<T> data)
        {
            overrideData = data != null ? new List<T>(data) : null;
            dataIDs = null;
            RefreshAndForget();
        }

        /// <summary>Sets the search terms (a search box's text) and refreshes.</summary>
        public void SetSearchTerms(string terms)
        {
            searchTerms = terms ?? "";
            RefreshAndForget();
        }

        /// <summary>Deselects every row (and forgets the selection).</summary>
        public void DeselectAll()
        {
            SelectedItemID = null;
            if (toggleGroup != null) { toggleGroup.SetAllTogglesOff(); }
            foreach (U row in displayItems) { if (row != null) { row.Select(false); } }
        }

        /// <summary>
        /// Fetches the records again and shows them (see the class), the selection kept. Nothing if it's refreshing already.
        /// </summary>
        public async Awaitable Refresh()
        {
            if (IsUpdating) { return; }
            IsUpdating = true;
            UIUtility.SetActive(loadingIndicator, true);
            UIUtility.SetActive(refreshButton, false);
            try
            {
                List<T> shown = Filtered(overrideData ?? Values(await GetData()));
                if (this == null) { return; } // Destroyed while it fetched.
                ShowRows(shown);
                await LayoutRebuildHelper.RebuildLayoutAsync(itemsParent != null ? itemsParent as RectTransform : transform as RectTransform);
                if (this == null) { return; }
                SelectAfterRefresh();
            }
            finally
            {
                if (this != null)
                {
                    IsUpdating = false;
                    UIUtility.SetActive(loadingIndicator, false);
                    UIUtility.SetActive(refreshButton, true);
                }
            }
        }

        /// <summary>Called with the rows shown, before the layout's rebuilt: for a list that adds to them (category headers).</summary>
        protected virtual void OnRowsShown(IReadOnlyList<U> rows)
        {
        }

        /// <summary>Refresh, from a method that can't wait for it (a search box's change): its exceptions are logged.</summary>
        protected async void RefreshAndForget()
        {
            try { await Refresh(); }
            catch (Exception exception) { Debug.LogException(exception, this); }
        }

        private static List<T> Values(Dictionary<string, T> data) => data != null ? new List<T>(data.Values) : new List<T>();

        // The records kept: of the IDs given (if any), matching every word of the search.
        private List<T> Filtered(List<T> records)
        {
            string[] words = searchTerms.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            var kept = new List<T>();
            foreach (T record in records)
            {
                if (record == null || (dataIDs != null && !dataIDs.Contains(record.ID))) { continue; }
                bool matches = true;
                foreach (string word in words) { matches &= HasSearchTerm(record, word); }
                if (matches) { kept.Add(record); }
            }
            return kept;
        }

        // Grows the pool to the records, shows a row for each and hides the rest; then the empty state.
        private void ShowRows(List<T> records)
        {
            Transform parent = itemsParent != null ? itemsParent : transform;
            while (displayItems.Count < records.Count && itemPrefabs.Count > 0)
            {
                GameObject prefab = itemPrefabs[displayItems.Count % itemPrefabs.Count];
                U row = Instantiate(prefab, parent, false).GetComponent<U>();
                if (row == null)
                {
                    Debug.LogError($"[{GetType().Name}] The row prefab '{prefab.name}' has no {typeof(U).Name} on its root.", this);
                    break;
                }
                row.SetToggleGroup(toggleGroup);
                displayItems.Add(row);
            }
            var shown = new List<U>();
            for (int i = 0; i < displayItems.Count; i++)
            {
                bool used = i < records.Count;
                UIUtility.SetActive(displayItems[i], used);
                if (!used) { continue; }
                displayItems[i].transform.SetSiblingIndex(i);
                displayItems[i].Initialize(records[i], OnItemSelectedCallback);
                shown.Add(displayItems[i]);
            }
            foreach (GameObject obj in shownIfItems) { UIUtility.SetActive(obj, shown.Count > 0); }
            foreach (GameObject obj in shownIfNoItems) { UIUtility.SetActive(obj, shown.Count == 0); }
            OnRowsShown(shown);
        }

        // The selected record's row selected again, else the first.
        private void SelectAfterRefresh()
        {
            U chosen = null;
            foreach (U row in ShownItems)
            {
                if (chosen == null) { chosen = row; }
                if (row.Data.ID == SelectedItemID) { chosen = row; break; }
            }
            if (chosen == null) { return; }
            // Selected afresh, so the panel hears even when it's the row that was selected before.
            chosen.Select(false);
            chosen.Select(true);
        }

        private void OnItemSelectedCallback(T data)
        {
            SelectedItemID = data.ID;
            OnItemSelected.Invoke(data);
        }
    }
}
