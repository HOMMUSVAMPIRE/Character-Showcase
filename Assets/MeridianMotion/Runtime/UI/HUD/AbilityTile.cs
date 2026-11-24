using TMPro;
using UnityEngine;

namespace MeridianMotion.UI
{
    /// <summary>
    /// One tile of an ability bar (ReferenceAssets/OpenTasks.md, H6): a square chamfered tile with an ability's icon, in a
    /// state: Unavailable (greyed), Ready (outlined), Active (the accent's fill) or Cooling Down (dimmed, its ring
    /// draining with the time left). Set from code (SetState); it knows nothing of the abilities.
    /// </summary>
    [AddComponentMenu("Meridian Motion/UI/HUD/Ability Tile")]
    public class AbilityTile : MonoBehaviour
    {
        /// <summary>A tile's state.</summary>
        public enum State { Unavailable, Ready, Active, CoolingDown }

        [Tooltip("The tile's shape")]
        [SerializeField] private ChamferedBox frame;
        [Tooltip("The ability's icon")]
        [SerializeField] private UIIcon icon;
        [Tooltip("The cooldown's ring (shown while it cools down)")]
        [SerializeField] private UIRadialProgress cooldown;
        [Tooltip("Its key or button (optional)")]
        [SerializeField] private TMP_Text keyText;
        [Tooltip("Its state")]
        [SerializeField] private State state = State.Ready;

        /// <summary>Its ID (the ability's, as the bar knows it).</summary>
        public string ID { get; set; }
        /// <summary>Its state.</summary>
        public State CurrentState => state;
        /// <summary>The tile's shape.</summary>
        public ChamferedBox Frame { get => frame; set => frame = value; }
        /// <summary>The ability's icon.</summary>
        public UIIcon Icon { get => icon; set => icon = value; }
        /// <summary>The cooldown's ring.</summary>
        public UIRadialProgress Cooldown { get => cooldown; set => cooldown = value; }

        /// <summary>Its icon's name.</summary>
        public string IconName
        {
            get => icon != null ? icon.IconName : null;
            set { if (icon != null) { icon.IconName = value; } }
        }

        /// <summary>Its key or button, as shown.</summary>
        public string Key
        {
            get => keyText != null ? keyText.text : "";
            set { if (keyText != null) { keyText.text = value; } }
        }

        /// <summary>Puts it in a state; cooling down, cooldownLeft is the fraction of its cooldown left (1 just begun, 0 done).</summary>
        public void SetState(State newState, float cooldownLeft = 0f)
        {
            state = newState;
            if (frame != null)
            {
                frame.Fill = true;
                frame.FillRole = state == State.Active ? UIColorRole.Accent : UIColorRole.Panel;
                frame.Outline = state != State.Active;
                frame.OutlineRole = state == State.Unavailable ? UIColorRole.Disabled : state == State.CoolingDown ? UIColorRole.TextMuted : UIColorRole.Outline;
            }
            if (icon != null)
            {
                icon.FillRole = state switch
                {
                    State.Unavailable => UIColorRole.Disabled,
                    State.Active => UIColorRole.TextOnFill,
                    State.CoolingDown => UIColorRole.TextMuted,
                    _ => UIColorRole.Text,
                };
            }
            if (cooldown != null)
            {
                UIUtility.SetActive(cooldown, state == State.CoolingDown);
                cooldown.Drain = false;
                cooldown.Fraction = state == State.CoolingDown ? Mathf.Clamp01(cooldownLeft) : 0f;
            }
        }

        private void OnValidate() => SetState(state);
    }
}
