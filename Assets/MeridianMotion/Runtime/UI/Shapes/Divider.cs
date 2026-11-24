using UnityEngine;
using UnityEngine.UI;

namespace MeridianMotion.UI
{
    /// <summary>
    /// A divider (the reference sheet's rules between sections): a line across the middle of its rect, Thickness thick in
    /// Fill Role, with an optional accent segment (Accent Length long from Accent Start, Accent Thickness thick, in Accent
    /// Role). No outline.
    /// </summary>
    [AddComponentMenu("Meridian Motion/UI/Shapes/Divider")]
    public class Divider : UIShapeGraphic
    {
        /// <summary>Filled in Outline, no outline, by default.</summary>
        public Divider() : base(UIColorRole.Outline, false)
        {
        }

        [Header("Line")]
        [Tooltip("How thick (canvas units) the line is")]
        [Min(0f)] [SerializeField] private float thickness = 2f;
        [Header("Accent")]
        [Tooltip("How long (canvas units) the accent segment is (0: none)")]
        [Min(0f)] [SerializeField] private float accentLength = 48f;
        [Tooltip("Where along the line the accent starts (0 to 1)")]
        [Range(0f, 1f)] [SerializeField] private float accentStart;
        [Tooltip("How thick (canvas units) the accent is")]
        [Min(0f)] [SerializeField] private float accentThickness = 4f;
        [Tooltip("The accent's colour role")]
        [SerializeField] private UIColorRole accentRole = UIColorRole.Accent;

        /// <summary>How long the accent segment is (0: none).</summary>
        public float AccentLength { get => accentLength; set { accentLength = Mathf.Max(0f, value); SetVerticesDirty(); } }

        protected override void Build(VertexHelper vh, Rect rect)
        {
            float y = rect.center.y;
            if (Fill && thickness > 0f)
            {
                UIShapeMesh.AddFill(vh, UIShapeMesh.Corners4(Rect.MinMaxRect(rect.xMin, y - thickness / 2f, rect.xMax, y + thickness / 2f)), RoleColor(FillRole), Feather);
            }
            if (accentLength > 0f && accentThickness > 0f)
            {
                float x0 = rect.xMin + rect.width * accentStart, x1 = Mathf.Min(rect.xMax, x0 + accentLength);
                UIShapeMesh.AddFill(vh, UIShapeMesh.Corners4(Rect.MinMaxRect(x0, y - accentThickness / 2f, x1, y + accentThickness / 2f)), RoleColor(accentRole), Feather);
            }
        }
    }
}
