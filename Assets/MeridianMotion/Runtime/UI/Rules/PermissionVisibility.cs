using UnityEngine;

namespace MeridianMotion.UI
{
    /// <summary>
    /// Shows its target only while its rules are permitted (UI_Framework_Blueprint, Rule-gated controls): the feature and the
    /// permission, each optional (none: always shown). Its target is its own object unless set: point it at a child to keep
    /// the gate itself active (it must wake to hear the rules change).
    /// </summary>
    [AddComponentMenu("Meridian Motion/UI/Permission Visibility")]
    public class PermissionVisibility : PermissionUpdateResponder
    {
        [Tooltip("The feature that must be permitted (unlocked, say), or none")]
        [SerializeField] private UIRule feature;
        [Tooltip("The permission that must be granted too (a role, a setting), or none")]
        [SerializeField] private UIRule permission;
        [Tooltip("What's shown or hidden: this object if empty")]
        [SerializeField] private GameObject target;

        /// <summary>The feature that must be permitted, or null.</summary>
        public UIRule Feature { get => feature; set => feature = value; }
        /// <summary>The permission that must be granted too, or null.</summary>
        public UIRule Permission { get => permission; set => permission = value; }
        /// <summary>What's shown or hidden (this object, if null).</summary>
        public GameObject Target { get => target; set => target = value; }

        /// <summary>Shows the target if both rules are permitted, else hides it.</summary>
        public override void EvaluateResponse()
        {
            if (this == null) { return; }
            bool shown = RulesService.IsPermitted(feature) && RulesService.IsPermitted(permission);
            UIUtility.SetActive(target != null ? target : gameObject, shown);
        }
    }
}
