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
    /// Controls is a page of its own listing the input actions' bindings, read from the actions (not written into the
    /// menu); Back on it goes back to the main page. Quit asks first (a yes/no dialog), then quits (or, in the editor,
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
        [Tooltip("Where the controls page lists the bindings")]
        [SerializeField] private TextMeshProUGUI controlsText;

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
        public bool ShowingControls => controlsPage != null && controlsPage.activeSelf;

        /// <summary>The input actions the controls page lists (set by the prefab; for a menu made in code).</summary>
        public InputActionAsset Actions { get => actions; set => actions = value; }

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
            ShowMain();
        }

        protected override void OnDisable()
        {
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
            if (ShowingControls) { ShowMain(); }
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
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        /// <summary>Shows the controls page, listing the bindings.</summary>
        public void ShowControls()
        {
            if (controlsText != null) { controlsText.text = ControlsListing(); }
            UIUtility.SetActive(mainPage, false);
            UIUtility.SetActive(controlsPage, true);
        }

        /// <summary>Shows the main page.</summary>
        public void ShowMain()
        {
            UIUtility.SetActive(controlsPage, false);
            UIUtility.SetActive(mainPage, true);
        }

        /// <summary>
        /// The controls page's listing: a line per action of the action map (but the hidden ones), its name, then its
        /// bindings in each control scheme, in columns.
        /// </summary>
        public string ControlsListing()
        {
            InputActionMap map = actions != null ? actions.FindActionMap(actionMap) : null;
            if (map == null) { return ""; }
            var hidden = new HashSet<string>(hiddenActions ?? new string[0]);
            var text = new StringBuilder();
            float column = 100f / (controlSchemes.Length + 1);
            foreach (InputAction action in map.actions)
            {
                if (hidden.Contains(action.name)) { continue; }
                text.Append(Spaced(action.name));
                for (int i = 0; i < controlSchemes.Length; i++)
                {
                    string binding = action.GetBindingDisplayString(InputBinding.MaskByGroup(controlSchemes[i]));
                    text.Append($"<pos={column * (i + 1):0}%>").Append(string.IsNullOrEmpty(binding) ? "-" : binding);
                }
                text.Append('\n');
            }
            return text.ToString().TrimEnd('\n');
        }

        // "ToggleView" as "Toggle View".
        private static string Spaced(string name)
        {
            var spaced = new StringBuilder();
            for (int i = 0; i < name.Length; i++)
            {
                if (i > 0 && char.IsUpper(name[i]) && !char.IsUpper(name[i - 1])) { spaced.Append(' '); }
                spaced.Append(name[i]);
            }
            return spaced.ToString();
        }
    }
}
