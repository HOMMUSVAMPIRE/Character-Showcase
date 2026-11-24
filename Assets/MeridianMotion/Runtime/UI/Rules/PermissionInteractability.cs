using UnityEngine;
using UnityEngine.UI;

namespace MeridianMotion.UI
{
    /// <summary>
    /// Lets its control be used only while its rules are permitted (UI_Framework_Blueprint, Rule-gated controls): a button,
    /// a toggle (any Selectable) shown but greyed out otherwise. The feature and the permission are each optional.
    /// </summary>
    [AddComponentMenu("Meridian Motion/UI/Permission Interactability")]
    public class PermissionInteractability : PermissionUpdateResponder
    {
        [Tooltip("The feature that must be permitted, or none")]
        [SerializeField] private UIRule feature;
        [Tooltip("The permission that must be granted too, or none")]
        [SerializeField] private UIRule permission;
        [Tooltip("The control: the Selectable on this object if empty")]
        [SerializeField] private Selectable selectable;

        /// <summary>The feature that must be permitted, or null.</summary>
        public UIRule Feature { get => feature; set => feature = value; }
        /// <summary>The permission that must be granted too, or null.</summary>
        public UIRule Permission { get => permission; set => permission = value; }
        /// <summary>The control (the Selectable on this object, if null).</summary>
        public Selectable Selectable { get => selectable; set => selectable = value; }

        /// <summary>Lets the control be used if both rules are permitted, else greys it out.</summary>
        public override void EvaluateResponse()
        {
            if (this == null) { return; }
            Selectable control = selectable != null ? selectable : GetComponent<Selectable>();
            if (control != null) { control.interactable = RulesService.IsPermitted(feature) && RulesService.IsPermitted(permission); }
        }
    }
}
