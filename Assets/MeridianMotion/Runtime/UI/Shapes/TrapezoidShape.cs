using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MeridianMotion.UI
{
    /// <summary>
    /// A trapezoid (the reference sheet's tab bar): its bottom edge across its rect, its top narrower by Slant at each
    /// side, filled and outlined by role. Upside Down puts the wide edge at the top.
    /// </summary>
    [AddComponentMenu("Meridian Motion/UI/Shapes/Trapezoid Shape")]
    public class TrapezoidShape : UIShapeGraphic
    {
        [Header("Trapezoid")]
        [Tooltip("How far (canvas units) each side slants in, from its wide edge to its narrow one; below 0, half its height")]
        [SerializeField] private float slant = -1f;
        [Tooltip("Whether the wide edge is at the top")]
        [SerializeField] private bool upsideDown;

        /// <summary>How far each side slants in (below 0: half its height).</summary>
        public float Slant { get => slant; set { slant = value; SetVerticesDirty(); } }
        /// <summary>Whether the wide edge is at the top.</summary>
        public bool UpsideDown { get => upsideDown; set { upsideDown = value; SetVerticesDirty(); } }

        /// <summary>Its outline, for the rect.</summary>
        public List<Vector2> OutlinePolygon(Rect rect)
        {
            float s = Mathf.Min(slant >= 0f ? slant : rect.height / 2f, rect.width / 2f);
            float bottomInset = upsideDown ? s : 0f, topInset = upsideDown ? 0f : s;
            return new List<Vector2>
            {
                new(rect.xMin + bottomInset, rect.yMin), new(rect.xMax - bottomInset, rect.yMin),
                new(rect.xMax - topInset, rect.yMax), new(rect.xMin + topInset, rect.yMax),
            };
        }

        protected override void Build(VertexHelper vh, Rect rect) => AddFilledAndOutlined(vh, OutlinePolygon(rect));
    }
}
