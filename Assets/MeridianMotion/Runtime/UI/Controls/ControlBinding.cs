using UnityEngine.InputSystem;

namespace MeridianMotion.UI
{
    /// <summary>
    /// One row of a controls list (ControlsList): an input action, its binding in a control scheme (the index of the
    /// binding it reads, and how it's shown), and whether it can be rebound (a button's single binding can; a stick's or
    /// a composite's, as WASD, is shown but not rebound). Its ID is the action's.
    /// </summary>
    public class ControlBinding : Identifiable
    {
        /// <summary>The action.</summary>
        public InputAction Action;
        /// <summary>Its name as shown ("Toggle View").</summary>
        public string DisplayName;
        /// <summary>The control scheme it's for.</summary>
        public string Scheme;
        /// <summary>The index (in Action.bindings) of the binding rebound, or -1 if it has none in the scheme.</summary>
        public int BindingIndex = -1;
        /// <summary>How its bindings in the scheme are shown ("Space", "WASD", "-" for none).</summary>
        public string BindingText;
        /// <summary>Whether it can be rebound.</summary>
        public bool Rebindable;
        /// <summary>Whether its binding's been changed from the asset's (an override).</summary>
        public bool Overridden;
    }
}
