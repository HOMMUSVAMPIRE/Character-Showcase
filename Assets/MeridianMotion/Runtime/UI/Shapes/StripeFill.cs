using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MeridianMotion.UI
{
    /// <summary>
    /// Stripes across its rect (the reference sheet's hazard stripes, and the hatching on what can't be used): stripes of
    /// Stripe Width with Gap between, at Angle, in Fill Role, clipped to its rect with its corners cut as a ChamferedBox's
    /// (so it lies exactly over a chamfered panel of the same corners). No outline.
    /// </summary>
    [AddComponentMenu("Meridian Motion/UI/Shapes/Stripe Fill")]
    public class StripeFill : UIShapeGraphic
    {
        /// <summary>Filled in Accent, no outline, by default.</summary>
        public StripeFill() : base(UIColorRole.Accent, false)
        {
        }

        [Header("Stripes")]
        [Tooltip("How wide (canvas units) each stripe is")]
        [Min(0.5f)] [SerializeField] private float stripeWidth = 10f;
        [Tooltip("The gap (canvas units) between stripes")]
        [Min(0f)] [SerializeField] private float gap = 10f;
        [Tooltip("The stripes' angle, in degrees from the horizontal")]
        [SerializeField] private float angle = 60f;
        [Tooltip("Which corners of the area they're clipped to are cut back")]
        [SerializeField] private UIShapeMesh.Corners corners = UIShapeMesh.Corners.None;
        [Tooltip("How far (canvas units) those corners are cut back; below 0, the theme's Chamfer Size")]
        [SerializeField] private float chamfer = -1f;

        /// <summary>How wide each stripe is.</summary>
        public float StripeWidth { get => stripeWidth; set { stripeWidth = Mathf.Max(0.5f, value); SetVerticesDirty(); } }
        /// <summary>The gap between stripes.</summary>
        public float Gap { get => gap; set { gap = Mathf.Max(0f, value); SetVerticesDirty(); } }
        /// <summary>The stripes' angle from the horizontal.</summary>
        public float Angle { get => angle; set { angle = value; SetVerticesDirty(); } }
        /// <summary>Which corners of the area are cut back.</summary>
        public UIShapeMesh.Corners Corners { get => corners; set { corners = value; SetVerticesDirty(); } }

        protected override void Build(VertexHelper vh, Rect rect)
        {
            if (!Fill) { return; }
            List<Vector2> area = UIShapeMesh.ChamferedRect(rect, chamfer >= 0f ? chamfer : ThemeInUse.ChamferSize, corners);
            float radians = angle * Mathf.Deg2Rad;
            var along = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
            var across = new Vector2(-along.y, along.x);
            float reach = rect.size.magnitude;
            // The stripes' range across them: the rect's corners projected onto their normal.
            float min = float.MaxValue, max = float.MinValue;
            foreach (Vector2 corner in UIShapeMesh.Corners4(rect))
            {
                float d = Vector2.Dot(corner - rect.center, across);
                min = Mathf.Min(min, d);
                max = Mathf.Max(max, d);
            }
            Color stripeColor = RoleColor(FillRole);
            for (float d = min; d < max; d += stripeWidth + gap)
            {
                Vector2 a = rect.center + across * d, b = rect.center + across * (d + stripeWidth);
                // Counter-clockwise: along it, then across (left of along).
                var stripe = new List<Vector2> { a - along * reach, a + along * reach, b + along * reach, b - along * reach };
                UIShapeMesh.AddFill(vh, UIShapeMesh.Clip(stripe, area), stripeColor, Feather);
            }
        }
    }
}
