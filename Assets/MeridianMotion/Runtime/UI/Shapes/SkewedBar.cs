using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MeridianMotion.UI
{
    /// <summary>
    /// A slanted bar (the reference sheet's progress bars and pips): parallelogram segments across its rect, the first
    /// Fraction of them filled (Fill Role) and the rest empty (Empty Role). With one segment it's a continuous bar, filled
    /// part way; with several, whole segments fill (a part-filled one shows as filled once it's past half).
    /// </summary>
    [AddComponentMenu("Meridian Motion/UI/Shapes/Skewed Bar")]
    public class SkewedBar : UIShapeGraphic
    {
        /// <summary>Filled in Highlight, no outline, by default.</summary>
        public SkewedBar() : base(UIColorRole.Highlight, false)
        {
        }

        [Header("Bar")]
        [Tooltip("How far (canvas units) each segment's top is shifted right of its bottom; below 0, the bar's height (45 degrees)")]
        [SerializeField] private float slant = -1f;
        [Tooltip("How many segments (1: a continuous bar)")]
        [Min(1)] [SerializeField] private int segments = 5;
        [Tooltip("The gap (canvas units) between segments")]
        [Min(0f)] [SerializeField] private float gap = 4f;
        [Tooltip("How much of it is filled (0 to 1)")]
        [Range(0f, 1f)] [SerializeField] private float fraction = 1f;
        [Tooltip("The empty part's colour role")]
        [SerializeField] private UIColorRole emptyRole = UIColorRole.PanelRaised;

        /// <summary>How much of it is filled (0 to 1).</summary>
        public float Fraction { get => fraction; set { fraction = Mathf.Clamp01(value); SetVerticesDirty(); } }
        /// <summary>How many segments.</summary>
        public int Segments { get => segments; set { segments = Mathf.Max(1, value); SetVerticesDirty(); } }
        /// <summary>The empty part's colour role.</summary>
        public UIColorRole EmptyRole { get => emptyRole; set { emptyRole = value; SetVerticesDirty(); } }

        /// <summary>How many segments are filled, as the bar draws them.</summary>
        public int FilledSegments => segments == 1 ? (fraction > 0f ? 1 : 0) : Mathf.RoundToInt(fraction * segments);

        protected override void Build(VertexHelper vh, Rect rect)
        {
            float s = slant >= 0f ? slant : rect.height;
            float span = rect.width - s;
            if (span <= 0f) { return; }
            float width = (span - gap * (segments - 1)) / segments;
            for (int i = 0; i < segments; i++)
            {
                float x0 = rect.xMin + i * (width + gap), x1 = x0 + width;
                List<Vector2> segment = UIShapeMesh.Parallelogram(x0, x1, rect.yMin, rect.yMax, s);
                if (segments == 1)
                {
                    UIShapeMesh.AddFill(vh, segment, RoleColor(emptyRole), Feather);
                    if (fraction > 0f && Fill)
                    {
                        List<Vector2> filled = UIShapeMesh.Parallelogram(x0, x0 + width * fraction, rect.yMin, rect.yMax, s);
                        UIShapeMesh.AddFill(vh, filled, RoleColor(FillRole), Feather);
                    }
                }
                else
                {
                    bool filled = Fill && i < FilledSegments;
                    UIShapeMesh.AddFill(vh, segment, RoleColor(filled ? FillRole : emptyRole), Feather);
                }
                if (Outline) { UIShapeMesh.AddOutline(vh, segment, OutlineWidthInUse, RoleColor(OutlineRole), Feather); }
            }
        }
    }
}
