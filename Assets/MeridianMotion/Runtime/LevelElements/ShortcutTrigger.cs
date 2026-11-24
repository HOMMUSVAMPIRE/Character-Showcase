using UnityEngine;

namespace MeridianMotion.Levels
{
    /// <summary>
    /// What opens a shortcut gate: a volume on the gate's far side (the side a player reaches it from the long way round).
    /// A character's feet coming into it opens its gate, which then stays open both ways.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Meridian Motion/Levels/Shortcut Trigger")]
    public class ShortcutTrigger : LevelVolume
    {
        [Tooltip("The gate this opens.")]
        [SerializeField] private ShortcutGate gate;

        /// <summary>The gate this opens.</summary>
        public ShortcutGate Gate { get => gate; set => gate = value; }

        public override void Entered(IRespawnable who, LevelPresence presence)
        {
            if (gate != null) { gate.Open(); }
        }

        protected override Color GizmoColor => new(1f, 0.8f, 0.2f);
    }
}
