using UnityEngine;
using UnityEngine.UI;

namespace MeridianMotion.UI
{
    /// <summary>
    /// Corner brackets (the reference sheet's focus brackets and frame corners): an L at each of the chosen corners of its
    /// rect, Length along each side and Thickness thick, in Fill Role. No outline.
    /// </summary>
    [AddComponentMenu("Meridian Motion/UI/Shapes/Corner Brackets")]
    public class CornerBrackets : UIShapeGraphic
    {
        /// <summary>Filled in Light, no outline, by default.</summary>
        public CornerBrackets() : base(UIColorRole.Light, false)
        {
        }

        [Header("Brackets")]
        [Tooltip("Which corners have a bracket")]
        [SerializeField] private UIShapeMesh.Corners corners = UIShapeMesh.Corners.All;
        [Tooltip("How long (canvas units) each arm of a bracket is")]
        [Min(0f)] [SerializeField] private float length = 16f;
        [Tooltip("How thick (canvas units) a bracket is")]
        [Min(0f)] [SerializeField] private float thickness = 3f;

        /// <summary>Which corners have a bracket.</summary>
        public UIShapeMesh.Corners Corners { get => corners; set { corners = value; SetVerticesDirty(); } }
        /// <summary>How long each arm is.</summary>
        public float Length { get => length; set { length = Mathf.Max(0f, value); SetVerticesDirty(); } }
        /// <summary>How thick a bracket is.</summary>
        public float Thickness { get => thickness; set { thickness = Mathf.Max(0f, value); SetVerticesDirty(); } }

        protected override void Build(VertexHelper vh, Rect rect)
        {
            if (!Fill || thickness <= 0f || length <= 0f) { return; }
            Color colour = RoleColor(FillRole);
            float l = Mathf.Min(length, rect.width / 2f, rect.height / 2f), t = Mathf.Min(thickness, l);
            void Bracket(UIShapeMesh.Corners corner, float x, float y, float dx, float dy)
            {
                if ((corners & corner) == 0) { return; }
                // Its arm along the edge, then its arm down the side (from the first's end, so they don't overlap).
                UIShapeMesh.AddFill(vh, UIShapeMesh.Corners4(RectBetween(x, y, x + dx * l, y + dy * t)), colour, Feather);
                UIShapeMesh.AddFill(vh, UIShapeMesh.Corners4(RectBetween(x, y + dy * t, x + dx * t, y + dy * l)), colour, Feather);
            }
            Bracket(UIShapeMesh.Corners.TopLeft, rect.xMin, rect.yMax, 1f, -1f);
            Bracket(UIShapeMesh.Corners.TopRight, rect.xMax, rect.yMax, -1f, -1f);
            Bracket(UIShapeMesh.Corners.BottomRight, rect.xMax, rect.yMin, -1f, 1f);
            Bracket(UIShapeMesh.Corners.BottomLeft, rect.xMin, rect.yMin, 1f, 1f);
        }

        private static Rect RectBetween(float x0, float y0, float x1, float y1) =>
            Rect.MinMaxRect(Mathf.Min(x0, x1), Mathf.Min(y0, y1), Mathf.Max(x0, x1), Mathf.Max(y0, y1));
    }
}
