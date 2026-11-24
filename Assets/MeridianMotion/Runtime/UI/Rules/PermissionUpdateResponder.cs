using UnityEngine;

namespace MeridianMotion.UI
{
    /// <summary>
    /// A control gated on rules (UI_Framework_Blueprint, Rule-gated controls): it registers with the RulesService as it
    /// wakes (and leaves as it's destroyed), and evaluates whenever the rules change and each time it's enabled, so it's
    /// right the first time it shows. It must wake to register: put it on an object that starts active (pointing at what
    /// it shows or hides, for one that starts hidden).
    /// </summary>
    public abstract class PermissionUpdateResponder : MonoBehaviour
    {
        protected virtual void Awake()
        {
            if (!RulesService.responsiveComponents.Contains(this)) { RulesService.responsiveComponents.Add(this); }
        }

        protected virtual void OnEnable() => EvaluateResponse();

        protected virtual void OnDestroy() => RulesService.responsiveComponents.Remove(this);

        /// <summary>Asks the RulesService, and shows, hides, or enables the control accordingly.</summary>
        public abstract void EvaluateResponse();
    }
}
