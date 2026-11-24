using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace MeridianMotion.UI
{
    /// <summary>
    /// A list of the controls (a row each: ControlBindingDisplay), for the control scheme the player's using now
    /// (ControlSchemeTracker): switching from keyboard and mouse to a gamepad lists the gamepad's bindings. Each action of
    /// its Action Map (but its Hidden Actions) is a row, named, with its binding in the scheme; a button's binding can be
    /// rebound (StartRebind: the next button pressed on the scheme's devices; Escape, or the gamepad's Start, cancels).
    /// Rebinding overrides the binding on the actions themselves (so the game, reading the same actions, follows it) and
    /// saves the overrides (SavedBindings). ResetAll puts the asset's own bindings back.
    /// </summary>
    [AddComponentMenu("Meridian Motion/UI/Controls List")]
    public class ControlsList : DataDisplayList<ControlBinding, ControlBindingDisplay>
    {
        [Header("Controls")]
        [Tooltip("The input actions listed (the player's: the asset the game reads)")]
        [SerializeField] private InputActionAsset actions;
        [Tooltip("The action map listed")]
        [SerializeField] private string actionMap = "Player";
        [Tooltip("Actions left off the list (ones nothing uses yet)")]
        [SerializeField] private string[] hiddenActions = new string[0];
        [Tooltip("Shows the control scheme listed (optional)")]
        [SerializeField] private TMP_Text schemeText;
        [Tooltip("The scroll view the rows are in, kept on the row selected with the keyboard or gamepad (optional)")]
        [SerializeField] private ScrollRect scrollRect;

        [Header("Rebinding")]
        [Tooltip("Seconds after a rebinding ends before another can start (the key or click that ended it mustn't start the next)")]
        [Min(0f)] [SerializeField] private float rebindCooldown = 0.3f;
        [Tooltip("Seconds a rebinding waits for a button before giving up")]
        [Min(1f)] [SerializeField] private float rebindTimeout = 10f;

        private InputActionRebindingExtensions.RebindingOperation operation;
        private bool restoreEnabled;
        private float rebindStartedAt, rebindEndedAt = float.NegativeInfinity;
        private string shownScheme;
        private GameObject lastSelected;

        /// <summary>The input actions listed.</summary>
        public InputActionAsset Actions { get => actions; set => actions = value; }
        /// <summary>The control scheme listed, else (null) the one in use (ControlSchemeTracker).</summary>
        public string ForcedScheme { get; set; }
        /// <summary>The control scheme listed now.</summary>
        public string Scheme => ForcedScheme ?? ControlSchemeTracker.CurrentScheme(actions);
        /// <summary>The control being rebound, or null.</summary>
        public ControlBinding Rebinding { get; private set; }
        /// <summary>Raised when a rebinding ends: the control, and whether it was rebound (not cancelled).</summary>
        public event Action<ControlBinding, bool> RebindEnded;

        /// <summary>Sets what's listed: the actions, the action map, and those left off.</summary>
        public void Configure(InputActionAsset asset, string map, string[] hidden)
        {
            actions = asset;
            actionMap = map;
            hiddenActions = hidden ?? new string[0];
        }

        protected virtual void OnEnable()
        {
            ControlSchemeTracker.Listen();
            ControlSchemeTracker.Changed += OnDeviceChanged;
        }

        protected virtual void OnDisable()
        {
            ControlSchemeTracker.Changed -= OnDeviceChanged;
            CancelRebind();
        }

        protected override Awaitable<Dictionary<string, ControlBinding>> GetData()
        {
            var done = new AwaitableCompletionSource<Dictionary<string, ControlBinding>>();
            done.SetResult(Bindings(Scheme));
            return done.Awaitable;
        }

        /// <summary>The rows for a control scheme, in the action map's order: an action each, but the hidden ones.</summary>
        public Dictionary<string, ControlBinding> Bindings(string scheme)
        {
            shownScheme = scheme;
            if (schemeText != null) { schemeText.text = SchemeName(scheme); }
            var rows = new Dictionary<string, ControlBinding>();
            InputActionMap map = actions != null ? actions.FindActionMap(actionMap) : null;
            if (map == null) { return rows; }
            var hidden = new HashSet<string>(hiddenActions);
            InputBinding mask = scheme != null ? InputBinding.MaskByGroup(scheme) : default;
            foreach (InputAction action in map.actions)
            {
                if (hidden.Contains(action.name)) { continue; }
                // Its first binding in the scheme (a composite, as WASD, if one of its parts is the scheme's): what's
                // shown, and, if it's a single button's, what's rebound. (An action bound twice, as to WASD and the
                // arrows, shows its first.)
                int first = FirstBinding(action, scheme, mask);
                bool composite = first >= 0 && action.bindings[first].isComposite;
                int index = composite ? -1 : first;
                string text = first < 0 ? "" : composite ? CompositeText(action, first, scheme, mask) : action.GetBindingDisplayString(first);
                rows[action.id.ToString()] = new ControlBinding
                {
                    ID = action.id.ToString(),
                    Action = action,
                    DisplayName = Spaced(action.name),
                    Scheme = scheme,
                    BindingIndex = index,
                    BindingText = string.IsNullOrEmpty(text) ? "-" : text,
                    Rebindable = action.type == InputActionType.Button && index >= 0 && !composite,
                    Overridden = index >= 0 && !string.IsNullOrEmpty(action.bindings[index].overridePath),
                };
            }
            return rows;
        }

        // A composite's binding as shown: each of its parts' first binding in the scheme, in order ("W/A/S/D", where a part
        // bound to two keys, as W and the up arrow, shows its first).
        private static string CompositeText(InputAction action, int head, string scheme, InputBinding mask)
        {
            var parts = new List<string>();
            var named = new HashSet<string>();
            for (int i = head + 1; i < action.bindings.Count && action.bindings[i].isPartOfComposite; i++)
            {
                InputBinding part = action.bindings[i];
                if ((scheme != null && !mask.Matches(part)) || !named.Add(part.name ?? "")) { continue; }
                parts.Add(action.GetBindingDisplayString(i));
            }
            return string.Join("/", parts);
        }

        // The index of the action's first binding in the scheme (a composite's head, if any of its parts is the scheme's), or -1.
        private static int FirstBinding(InputAction action, string scheme, InputBinding mask)
        {
            for (int i = 0; i < action.bindings.Count; i++)
            {
                InputBinding binding = action.bindings[i];
                if (binding.isPartOfComposite) { continue; }
                if (binding.isComposite)
                {
                    for (int part = i + 1; part < action.bindings.Count && action.bindings[part].isPartOfComposite; part++)
                    {
                        if (scheme == null || mask.Matches(action.bindings[part])) { return i; }
                    }
                    continue;
                }
                if (scheme == null || mask.Matches(binding)) { return i; }
            }
            return -1;
        }

        /// <summary>
        /// Rebinds the control: its binding in the scheme becomes the next button pressed on the scheme's devices (Escape,
        /// or the gamepad's Start, cancels; so does its Rebind Timeout). Nothing if it can't be rebound, one's being
        /// rebound already, or one's only just ended. The UI's Back and Submit keys are held off meanwhile.
        /// </summary>
        public bool StartRebind(ControlBinding binding)
        {
            if (binding == null || !binding.Rebindable || operation != null) { return false; }
            if (UIClock.Now - rebindEndedAt < rebindCooldown) { return false; }
            InputAction action = binding.Action;
            restoreEnabled = action.enabled;
            if (restoreEnabled) { action.Disable(); }
            Rebinding = binding;
            rebindStartedAt = UIClock.Now;
            UIManager.CaptureInput();
            InputActionRebindingExtensions.RebindingOperation rebind = action.PerformInteractiveRebinding(binding.BindingIndex)
                .WithExpectedControlType("Button")
                .WithControlsExcluding("<Pointer>/position")
                .WithControlsExcluding("<Pointer>/delta")
                .WithControlsExcluding("<Gamepad>/start")
                .WithCancelingThrough("<Keyboard>/escape")
                .OnMatchWaitForAnother(0.1f)
                .OnComplete(_ => EndRebind(true))
                .OnCancel(_ => EndRebind(false));
            InputControlScheme? scheme = binding.Scheme != null ? actions.FindControlScheme(binding.Scheme) : null;
            if (scheme.HasValue)
            {
                foreach (InputControlScheme.DeviceRequirement requirement in scheme.Value.deviceRequirements) { rebind.WithControlsHavingToMatchPath(requirement.controlPath); }
            }
            operation = rebind;
            RowOf(binding)?.SetListening(true);
            rebind.Start();
            return true;
        }

        /// <summary>Cancels the rebinding under way, if there is one.</summary>
        public void CancelRebind() => operation?.Cancel();

        /// <summary>Rebinds the control to a control path ("&lt;Keyboard&gt;/k") at once, and saves: as StartRebind's choice does.</summary>
        public void SetBinding(ControlBinding binding, string path)
        {
            if (binding == null || binding.BindingIndex < 0) { return; }
            binding.Action.ApplyBindingOverride(binding.BindingIndex, path);
            Changed();
        }

        /// <summary>Puts back the asset's own bindings for every action listed (overrides removed), and saves.</summary>
        public void ResetAll()
        {
            CancelRebind();
            InputActionMap map = actions != null ? actions.FindActionMap(actionMap) : null;
            if (map == null) { return; }
            foreach (InputAction action in map.actions) { action.RemoveAllBindingOverrides(); }
            Changed();
        }

        /// <summary>
        /// The controls as text, for the scheme: a line per row, its name, a tab, and its binding (a summary: for a log, a
        /// test, a screen reader).
        /// </summary>
        public string Listing(string scheme)
        {
            var text = new StringBuilder();
            foreach (ControlBinding row in Bindings(scheme).Values) { text.Append(row.DisplayName).Append('\t').Append(row.BindingText).Append('\n'); }
            return text.ToString().TrimEnd('\n');
        }

        /// <summary>A control scheme's name as shown ("Keyboard&amp;Mouse" as "KEYBOARD &amp; MOUSE").</summary>
        public static string SchemeName(string scheme) => string.IsNullOrEmpty(scheme) ? "" : scheme.Replace("&", " & ").ToUpperInvariant();

        /// <summary>"ToggleView" as "Toggle View".</summary>
        public static string Spaced(string name)
        {
            var spaced = new StringBuilder();
            for (int i = 0; i < name.Length; i++)
            {
                if (i > 0 && char.IsUpper(name[i]) && !char.IsUpper(name[i - 1])) { spaced.Append(' '); }
                spaced.Append(name[i]);
            }
            return spaced.ToString();
        }

        protected virtual void Update()
        {
            if (operation == null) { return; }
            if (UIClock.Now - rebindStartedAt > rebindTimeout) { CancelRebind(); }
            else if (Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame) { CancelRebind(); }
        }

        protected virtual void LateUpdate() => KeepSelectionInView();

        private void EndRebind(bool rebound)
        {
            ControlBinding binding = Rebinding;
            InputActionRebindingExtensions.RebindingOperation ended = operation;
            operation = null;
            Rebinding = null;
            ended?.Dispose();
            if (binding != null && restoreEnabled) { binding.Action.Enable(); }
            rebindEndedAt = UIClock.Now;
            UIManager.ReleaseInput();
            if (binding != null) { RowOf(binding)?.SetListening(false); }
            if (rebound) { Changed(); }
            RebindEnded?.Invoke(binding, rebound);
        }

        // A binding's changed: saved, and the rows drawn again.
        private void Changed()
        {
            SavedBindings.SaveFor(actions);
            if (isActiveAndEnabled) { RefreshAndForget(); }
        }

        private ControlBindingDisplay RowOf(ControlBinding binding)
        {
            foreach (ControlBindingDisplay row in ShownItems) { if (row.Data != null && row.Data.ID == binding.ID) { return row; } }
            return null;
        }

        private void OnDeviceChanged(InputDevice device)
        {
            if (operation != null || !isActiveAndEnabled || ForcedScheme != null) { return; }
            if (ControlSchemeTracker.CurrentScheme(actions) != shownScheme) { RefreshAndForget(); }
        }

        // Scrolls the row selected (by the keyboard or a gamepad) into view.
        private void KeepSelectionInView()
        {
            GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (selected == lastSelected) { return; }
            lastSelected = selected;
            if (scrollRect == null || selected == null || scrollRect.content == null || !selected.transform.IsChildOf(scrollRect.content)) { return; }
            RectTransform viewport = scrollRect.viewport != null ? scrollRect.viewport : (RectTransform)scrollRect.transform;
            Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, selected.transform);
            Rect view = viewport.rect;
            float shift = bounds.max.y > view.yMax ? view.yMax - bounds.max.y : bounds.min.y < view.yMin ? view.yMin - bounds.min.y : 0f;
            if (shift != 0f) { scrollRect.content.anchoredPosition += new Vector2(0f, shift); }
        }
    }
}
