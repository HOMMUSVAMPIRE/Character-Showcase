using System;
using System.Collections.Generic;

namespace MeridianMotion.UI
{
    /// <summary>
    /// What the game permits (UI_Framework_Blueprint, Rule-gated controls): each rule (a UIRule asset) is permitted or not,
    /// as the game sets it (Set), else as the rule says by default. When the rules change, every gated control registered
    /// with it (its responsiveComponents) evaluates again, and OnSelfPermissionsUpdated tells code that draws its controls
    /// itself, so no screen rechecks its buttons by hand.
    /// </summary>
    public static class RulesService
    {
        /// <summary>The gated controls, which add themselves in Awake and take themselves off in OnDestroy.</summary>
        public static readonly List<PermissionUpdateResponder> responsiveComponents = new();

        /// <summary>Raised when the rules change.</summary>
        public static event Action OnSelfPermissionsUpdated;

        private static readonly Dictionary<UIRule, bool> permitted = new();

        /// <summary>Whether the rule is permitted (true for no rule: a control gated on nothing is never hidden).</summary>
        public static bool IsPermitted(UIRule rule)
        {
            if (rule == null) { return true; }
            return permitted.TryGetValue(rule, out bool value) ? value : rule.PermittedByDefault;
        }

        /// <summary>Permits the rule or not, and tells the gated controls if that changed it.</summary>
        public static void Set(UIRule rule, bool isPermitted)
        {
            if (rule == null) { return; }
            bool before = IsPermitted(rule);
            permitted[rule] = isPermitted;
            if (before != isPermitted) { RulesChanged(); }
        }

        /// <summary>Puts every rule back to its default (a new game, or a test's start), telling the gated controls.</summary>
        public static void Reset()
        {
            permitted.Clear();
            RulesChanged();
        }

        /// <summary>The rules changed (all at once: set them, then call this): every gated control evaluates again.</summary>
        public static void RulesChanged()
        {
            OnSelfPermissionsUpdated?.Invoke();
            foreach (PermissionUpdateResponder responder in responsiveComponents.ToArray())
            {
                if (responder != null) { responder.EvaluateResponse(); }
            }
        }
    }
}
