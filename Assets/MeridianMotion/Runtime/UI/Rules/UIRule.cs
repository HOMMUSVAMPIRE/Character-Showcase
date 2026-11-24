using UnityEngine;

namespace MeridianMotion.UI
{
    /// <summary>
    /// A rule a control can be gated on (UI_Framework_Blueprint, Rule-gated controls): an unlocked feature, a tutorial step,
    /// a role. An asset, picked in a gated control's inspector, so the framework names no game's rules; the game says
    /// whether it holds (RulesService.Set).
    /// </summary>
    [CreateAssetMenu(fileName = "UI Rule", menuName = "Meridian Motion/UI/Rule")]
    public class UIRule : ScriptableObject
    {
        [Tooltip("Whether it's permitted until the game says otherwise")]
        [SerializeField] private bool permittedByDefault = true;

        /// <summary>Whether it's permitted until the game says otherwise.</summary>
        public bool PermittedByDefault { get => permittedByDefault; set => permittedByDefault = value; }
    }
}
