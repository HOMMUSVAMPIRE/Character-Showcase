using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace MeridianMotion.UI
{
    /// <summary>
    /// The pause menu: Resume, Restart, Controls and Quit, opened by the Back key when no window takes it (PauseMenuOnBack
    /// registers it as the UIManager's fallback). While it's open the game is paused (Time.timeScale 0, put back as it
    /// was when it closes or is hidden); it asks for the cursor and stops the game's input through the UIManager's events
    /// (its Needs Cursor and Blocks Game Input), so it knows nothing of the camera or the character.
    ///
    /// Controls is a tab of its own (its Page Tabs, a TabGroup, with the main page's tab): its Controls List, a row per
    /// input action with its binding in the control scheme the player's using (following a switch to a gamepad and
    /// back), a button's binding rebound by clicking it (ControlsList), Reset Controls putting the asset's back. (A menu
    /// without a list lists the bindings as text, in Controls Text.) Back on it goes back to the main page. Replays is a
    /// tab shown only while the game's registered replay controls (ReplayControls): Save Replay and Load Replay, each
    /// greyed out when it can't be done, and their status. Quit tells the replay controls first (BeforeQuit). Without tabs,
    /// the pages are shown and hidden themselves. Quit asks first (a yes/no dialog), then quits (or, in the editor,
    /// leaves Play mode). Its buttons call Resume, Restart, ShowControls, ShowMain and Quit. Open it with Open (the
    /// UIManager's window catalog lists it).
    /// </summary>
    [AddComponentMenu("Meridian Motion/UI/Pause Menu")]
    public class PauseMenu : RootUIPrefab
    {
        [Header("Pages")]
        [Tooltip("The main page: Resume, Restart, Controls, Quit")]
        [SerializeField] private GameObject mainPage;
        [Tooltip("The controls page, listing the bindings")]
        [SerializeField] private GameObject controlsPage;
        [Tooltip("The controls page's list of the bindings, each rebindable (or none: Controls Text)")]
        [SerializeField] private ControlsList controlsList;
        [Tooltip("Where the controls page lists the bindings as text, without a Controls List")]
        [SerializeField] private TextMeshProUGUI controlsText;
        [Tooltip("The tabs the pages are the contents of (a tab each), or none: the pages shown and hidden themselves")]
        [SerializeField] private TabGroup pageTabs;
        [Tooltip("The main page's tab")]
        [SerializeField] private Tab mainTab;
        [Tooltip("The controls page's tab")]
        [SerializeField] private Tab controlsTab;

        [Header("Replays")]
        [Tooltip("The Replays tab, shown only while the game's registered replay controls (ReplayControls)")]
        [SerializeField] private Tab replaysTab;
        [Tooltip("The replays page: Save Replay and Load Replay")]
        [SerializeField] private GameObject replaysPage;
        [Tooltip("Save Replay's button (greyed out while there's nothing to save)")]
        [SerializeField] private UnityEngine.UI.Selectable saveReplayButton;
        [Tooltip("Load Replay's button (greyed out while there's no replay)")]
        [SerializeField] private UnityEngine.UI.Selectable loadReplayButton;
        [Tooltip("Where the replays' status is shown")]
        [SerializeField] private TextMeshProUGUI replayStatusText;

        [Header("Controls")]
        [Tooltip("The input actions whose bindings the controls page lists")]
        [SerializeField] private InputActionAsset actions;
        [Tooltip("The action map listed (the player's)")]
        [SerializeField] private string actionMap = "Player";
        [Tooltip("The control schemes listed, a column each, by their names in the input actions")]
        [SerializeField] private string[] controlSchemes = { "Keyboard&Mouse", "Gamepad" };
        [Tooltip("Actions left off the list (ones nothing uses yet)")]
        [SerializeField] private string[] hiddenActions = { "Attack", "Previous", "Next" };

        [Header("Quit")]
        [Tooltip("What the dialog asks before quitting")]
        [SerializeField] private string quitQuestion = "Quit the game?";

        private float timeScaleBefore = 1f;
        private bool paused;

        /// <summary>Whether the controls page is showing.</summary>
        public bool ShowingControls => controlsTab != null ? controlsTab.Selected : controlsPage != null && controlsPage.activeSelf;

        /// <summary>The input actions the controls page lists (set by the prefab; for a menu made in code).</summary>
        public InputActionAsset Actions { get => actions; set => actions = value; }

        /// <summary>The controls page's list, or null.</summary>
        public ControlsList ControlsList => controlsList;

        /// <summary>Opens the pause menu (the window catalog's), or brings the open one to the front.</summary>
        public static PauseMenu Open() => UIManager.instance != null ? UIManager.instance.Open<PauseMenu>() : null;

        protected override void OnEnable()
        {
            base.OnEnable();
            if (IsClosing) { return; }
            if (!paused)
            {
                timeScaleBefore = Time.timeScale;
                Time.timeScale = 0f;
                paused = true;
            }
            // However the controls page comes to be shown (its tab clicked, the Controls button), it's filled.
            if (pageTabs != null) { pageTabs.SelectionChanged.AddListener(FillControlsIfShown); }
            // The Replays tab only while the game has replays to offer.
            UIUtility.SetActive(replaysTab, ReplayControls.Current != null);
            ShowMain();
        }

        protected override void OnDisable()
        {
            if (pageTabs != null) { pageTabs.SelectionChanged.RemoveListener(FillControlsIfShown); }
            Unpause();
            base.OnDisable();
        }

        protected override void OnDestroy()
        {
            Unpause();
            base.OnDestroy();
        }

        // The game's time scale put back as it was (once).
        private void Unpause()
        {
            if (!paused) { return; }
            paused = false;
            Time.timeScale = timeScaleBefore;
        }

        /// <summary>Back: from the controls page, back to the main page; from the main page, resume (unless Cancel Action Overrides say otherwise).</summary>
        public override void OnCancelButton()
        {
            if (ShowingControls || ShowingReplays) { ShowMain(); }
            else { base.OnCancelButton(); }
        }

        /// <summary>Closes the menu, and the game carries on.</summary>
        public void Resume()
        {
            Unpause();
            Close();
        }

        /// <summary>Loads the scene again from its start (the game unpaused first).</summary>
        public void Restart()
        {
            Unpause();
            ReloadScene();
        }

        /// <summary>Loads the active scene again from its start (in the editor, by its path: it needn't be in the build's list).</summary>
        public static void ReloadScene()
        {
            Scene scene = SceneManager.GetActiveScene();
#if UNITY_EDITOR
            // In the editor a scene needn't be in the build's list: load it by its path.
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(scene.path, new LoadSceneParameters(LoadSceneMode.Single));
#else
            SceneManager.LoadScene(scene.buildIndex);
#endif
        }

        /// <summary>Asks whether to quit (a yes/no dialog), and quits if so.</summary>
        public UIMessageBox Quit() => UIMessageBox.OpenYesNoDialog(quitQuestion, QuitNow, KeepPlaying);

        /// <summary>Quit, for a button (a persistent listener calls a method returning nothing).</summary>
        public void QuitFromButton() => Quit();

        // No: back to the menu. (A dialog without a No would take Back as OK, and quit.)
        private static void KeepPlaying()
        {
        }

        private static void QuitNow()
        {
            ReplayControls.Current?.BeforeQuit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        /// <summary>Shows the controls page, listing the bindings.</summary>
        public void ShowControls()
        {
            FillControls();
            if (pageTabs != null && controlsTab != null)
            {
                pageTabs.OnTabClicked(controlsTab);
                return;
            }
            UIUtility.SetActive(mainPage, false);
            UIUtility.SetActive(replaysPage, false);
            UIUtility.SetActive(controlsPage, true);
        }

        /// <summary>The bindings listed on the controls page (as last filled): its list's, for the scheme it shows, else its text.</summary>
        public string ShownControls => controlsList != null ? controlsList.Listing(controlsList.Scheme) : controlsText != null ? controlsText.text : "";

        /// <summary>Puts back the input actions' own bindings (the controls list's ResetAll): for its Reset button.</summary>
        public void ResetControls()
        {
            if (controlsList != null) { controlsList.ResetAll(); }
            else { FillControls(); }
        }

        // The controls page's listing, read from the actions now (so a rebinding shows).
        private void FillControls()
        {
            if (controlsList != null)
            {
                controlsList.Configure(actions, actionMap, hiddenActions);
                controlsList.Populate((System.Collections.Generic.List<string>)null);
            }
            if (controlsText != null) { controlsText.text = ControlsListing(); }
        }

        private void FillControlsIfShown()
        {
            if (ShowingControls) { FillControls(); }
        }

        /// <summary>Whether the replays page is showing.</summary>
        public bool ShowingReplays => replaysTab != null ? replaysTab.Selected : replaysPage != null && replaysPage.activeSelf;

        /// <summary>Shows the replays page (only while the game's registered replay controls).</summary>
        public void ShowReplays()
        {
            if (ReplayControls.Current == null) { return; }
            if (pageTabs != null && replaysTab != null)
            {
                pageTabs.OnTabClicked(replaysTab);
                return;
            }
            UIUtility.SetActive(mainPage, false);
            UIUtility.SetActive(controlsPage, false);
            UIUtility.SetActive(replaysPage, true);
        }

        /// <summary>Save Replay: the game's replay controls save the run under way; the page shows the new status.</summary>
        public void SaveReplay()
        {
            ReplayControls.Current?.SaveReplay();
            RefreshReplays();
        }

        /// <summary>Load Replay: the menu closes (the game carries on), and the game's replay controls play a replay.</summary>
        public void LoadReplay()
        {
            IReplayControls controls = ReplayControls.Current;
            if (controls == null || !controls.CanLoad) { return; }
            Resume();
            controls.LoadReplay();
        }

        /// <summary>Sets the replays page's buttons and status from the game's replay controls.</summary>
        public void RefreshReplays()
        {
            IReplayControls controls = ReplayControls.Current;
            if (saveReplayButton != null) { saveReplayButton.interactable = controls != null && controls.CanSave; }
            if (loadReplayButton != null) { loadReplayButton.interactable = controls != null && controls.CanLoad; }
            if (replayStatusText != null) { replayStatusText.text = controls?.Status ?? ""; }
        }

        // How often (seconds, on the UI's clock) the replays page's status is shown afresh while it's up.
        private const float ReplayStatusInterval = 0.5f;
        private float replaysRefreshedAt = float.NegativeInfinity;

        protected override void Update()
        {
            base.Update();
            if (!ShowingReplays || UIClock.Now - replaysRefreshedAt < ReplayStatusInterval) { return; }
            replaysRefreshedAt = UIClock.Now;
            RefreshReplays();
        }

        /// <summary>Shows the main page.</summary>
        public void ShowMain()
        {
            if (pageTabs != null && mainTab != null)
            {
                pageTabs.OnTabClicked(mainTab);
                return;
            }
            UIUtility.SetActive(controlsPage, false);
            UIUtility.SetActive(replaysPage, false);
            UIUtility.SetActive(mainPage, true);
        }

        /// <summary>
        /// The controls page's listing: a line per action of the action map (but the hidden ones), its name, then its
        /// bindings in each control scheme, in columns.
        /// </summary>
        public string ControlsListing()
        {
            // (The controls list shows one scheme, the one in use; this lists them all, in Control Schemes' columns.)
            InputActionMap map = actions != null ? actions.FindActionMap(actionMap) : null;
            if (map == null) { return ""; }
            var hidden = new HashSet<string>(hiddenActions ?? new string[0]);
            var text = new StringBuilder();
            float column = 100f / (controlSchemes.Length + 1);
            foreach (InputAction action in map.actions)
            {
                if (hidden.Contains(action.name)) { continue; }
                text.Append(ControlsList.Spaced(action.name));
                for (int i = 0; i < controlSchemes.Length; i++)
                {
                    string binding = action.GetBindingDisplayString(InputBinding.MaskByGroup(controlSchemes[i]));
                    text.Append($"<pos={column * (i + 1):0}%>").Append(string.IsNullOrEmpty(binding) ? "-" : binding);
                }
                text.Append('\n');
            }
            return text.ToString().TrimEnd('\n');
        }
    }
}
