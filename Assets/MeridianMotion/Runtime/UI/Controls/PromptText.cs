using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MeridianMotion.UI
{
    /// <summary>
    /// A line of guidance that names the controls it means by their actions ("Press {Jump} to jump"), written out with each
    /// action's binding in a control scheme ("Press Space to jump", or a gamepad's button), so it says what the player's
    /// device has (pass ControlSchemeTracker.CurrentScheme for the device in use, and write it again on its Changed). The
    /// bindings are the actions' as they are now, rebindings included.
    /// </summary>
    public static class PromptText
    {
        private static readonly StringBuilder written = new();

        /// <summary>
        /// The template with each {Action} replaced by that action's binding in the scheme (null: every binding). An
        /// action the asset doesn't have is left as its name (without the braces), with a warning; so is every one, with
        /// no asset.
        /// </summary>
        public static string Format(string template, InputActionAsset actions, string controlScheme)
        {
            if (string.IsNullOrEmpty(template)) { return ""; }
            written.Clear();
            int at = 0;
            while (at < template.Length)
            {
                int open = template.IndexOf('{', at);
                int close = open < 0 ? -1 : template.IndexOf('}', open + 1);
                if (open < 0 || close < 0)
                {
                    written.Append(template, at, template.Length - at);
                    break;
                }
                written.Append(template, at, open - at);
                string name = template.Substring(open + 1, close - open - 1);
                written.Append(BindingOf(name, actions, controlScheme));
                at = close + 1;
            }
            return written.ToString();
        }

        /// <summary>An action's binding as shown, in the scheme (or the action's name, with a warning, if there's no such action).</summary>
        public static string BindingOf(string actionName, InputActionAsset actions, string controlScheme)
        {
            InputAction action = actions != null ? actions.FindAction(actionName) : null;
            if (action == null)
            {
                if (actions != null) { Debug.LogWarning($"[PromptText] '{actions.name}' has no action '{actionName}': shown by its name."); }
                return actionName;
            }
            string shown = controlScheme != null ? action.GetBindingDisplayString(InputBinding.MaskByGroup(controlScheme)) : action.GetBindingDisplayString();
            return string.IsNullOrEmpty(shown) ? actionName : shown;
        }
    }
}
