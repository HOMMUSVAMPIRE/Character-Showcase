using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MeridianMotion.UI
{
    /// <summary>
    /// A diamond (the reference sheet's markers, radio toggles and bullets): the diamond inscribed in its rect, filled and
    /// outlined by role, with an optional glow round it (the theme's Glow Size and Glow Strength, in Glow Role).
    /// </summary>
    [AddComponentMenu("Meridian Motion/UI/Shapes/Diamond Shape")]
    public class DiamondShape : UIShapeGraphic
    {
        /// <summary>Filled in Accent, outlined, by default.</summary>
        public DiamondShape() : base(UIColorRole.Accent, true)
        {
        }

        [Header("Glow")]
        [Tooltip("Whether it glows")]
        [SerializeField] private bool glow;
        [Tooltip("The glow's colour role")]
        [SerializeField] private UIColorRole glowRole = UIColorRole.Highlight;

        /// <summary>Whether it glows.</summary>
        public bool Glow { get => glow; set { glow = value; SetVerticesDirty(); } }
        /// <summary>The glow's colour role.</summary>
        public UIColorRole GlowRole { get => glowRole; set { glowRole = value; SetVerticesDirty(); } }

        protected override void Build(VertexHelper vh, Rect rect)
        {
            List<Vector2> diamond = UIShapeMesh.Diamond(rect);
            if (glow)
            {
                Color glowColor = RoleColor(glowRole);
                glowColor.a *= ThemeInUse.GlowStrength;
                UIShapeMesh.AddGlow(vh, diamond, ThemeInUse.GlowSize, glowColor);
            }
            AddFilledAndOutlined(vh, diamond);
        }
    }
}
