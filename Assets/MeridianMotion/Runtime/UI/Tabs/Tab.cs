using UnityEngine;
using UnityEngine.Events;

namespace MeridianMotion.UI
{
    /// <summary>
    /// One tab of a TabGroup (UI_Framework_Blueprint, Tabs): selected, it shows its selected look and its contents, runs its
    /// On Selected Actions, and opens the window it tracks (Root UI To Track, a window prefab), whose closing (by the Back
    /// key, say) deselects it again; deselected, it shows its unselected look, hides its contents and closes its window.
    /// Its button calls OnClicked. It finds its group by itself in the editor.
    /// </summary>
    [AddComponentMenu("Meridian Motion/UI/Tab")]
    public class Tab : MonoBehaviour
    {
        [Tooltip("The tab's ID, for code finding it")]
        public string ID;
        [Tooltip("The group it's one of (found among its parents by itself)")]
        [SerializeField] private TabGroup group;
        [Tooltip("Shown while it's selected")]
        [SerializeField] private GameObject selectedState;
        [Tooltip("Shown while it isn't")]
        [SerializeField] private GameObject unselectedState;
        [Tooltip("Its content, shown while it's selected")]
        [SerializeField] private GameObject tabContents;
        [Tooltip("A window prefab it opens while it's selected (and is deselected when it closes), if any")]
        [SerializeField] private GameObject rootUIToTrack;
        [Tooltip("Run each time it's selected")]
        [SerializeField] private UnityEvent OnSelectedActions = new();

        /// <summary>Whether it's selected.</summary>
        public bool Selected { get; private set; }

        /// <summary>Its group.</summary>
        public TabGroup Group { get => group; set => group = value; }
        /// <summary>Shown while it's selected.</summary>
        public GameObject SelectedState { get => selectedState; set => selectedState = value; }
        /// <summary>Shown while it isn't.</summary>
        public GameObject UnselectedState { get => unselectedState; set => unselectedState = value; }
        /// <summary>Its content.</summary>
        public GameObject TabContents { get => tabContents; set => tabContents = value; }
        /// <summary>The window prefab it opens while selected, or null.</summary>
        public GameObject RootUIToTrack { get => rootUIToTrack; set => rootUIToTrack = value; }
        /// <summary>Run each time it's selected.</summary>
        public UnityEvent SelectedActions => OnSelectedActions;

        private RootUIPrefab tracked;

        private void Awake()
        {
            if (group == null) { group = GetComponentInParent<TabGroup>(true); }
            if (group != null) { group.RegisterTab(this); }
        }

        private void OnValidate()
        {
            if (group == null) { group = GetComponentInParent<TabGroup>(true); }
        }

        /// <summary>Clicked: its group selects it (or deselects it).</summary>
        public void OnClicked()
        {
            if (group != null) { group.OnTabClicked(this); }
            else { SelectTab(!Selected, isDeselection: Selected); }
        }

        /// <summary>
        /// Selects or deselects it (see the class). isDeselection: deselected by itself (clicked again, or its window
        /// closed), rather than because another was selected.
        /// </summary>
        public void SelectTab(bool selected, bool isDeselection)
        {
            bool changed = selected != Selected;
            Selected = selected;
            UIUtility.SetActive(selectedState, selected);
            UIUtility.SetActive(unselectedState, !selected);
            UIUtility.SetActive(tabContents, selected);
            if (selected)
            {
                OpenTracked();
                if (changed) { OnSelectedActions.Invoke(); }
            }
            else
            {
                CloseTracked();
            }
        }

        private void OpenTracked()
        {
            if (rootUIToTrack == null || UIManager.instance == null || (tracked != null && !tracked.IsClosing)) { return; }
            tracked = UIManager.instance.OpenUI<RootUIPrefab>(rootUIToTrack);
            if (tracked != null) { tracked.OnClosed += OnTrackedClosed; }
        }

        private void CloseTracked()
        {
            if (tracked == null) { return; }
            RootUIPrefab window = tracked;
            tracked = null;
            window.OnClosed -= OnTrackedClosed;
            window.Close();
        }

        // Its window closed by itself (not by its being deselected, which lets go of it first).
        private void OnTrackedClosed(RootUIPrefab window)
        {
            if (window != tracked) { return; }
            tracked = null;
            if (group != null) { group.OnTrackedWindowClosed(this); }
            else if (Selected) { SelectTab(false, isDeselection: true); }
        }
    }
}
