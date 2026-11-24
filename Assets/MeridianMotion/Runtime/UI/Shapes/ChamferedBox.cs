using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MeridianMotion.UI
{
    /// <summary>
    /// A panel or button's body (the reference sheet's): a rect with its corners cut back at 45 degrees (each corner on or
    /// off), filled and outlined by role; with an optional header tab (a bar along its top edge, from its left, its end
    /// slanted, in the Accent role by default) and an optional gloss (two slanted "//" stripes near its top left).
    /// </summary>
    [AddComponentMenu("Meridian Motion/UI/Shapes/Chamfered Box")]
    public class ChamferedBox : UIShapeGraphic
    {
        [Header("Corners")]
        [Tooltip("Which corners are cut back")]
        [SerializeField] private UIShapeMesh.Corners corners = UIShapeMesh.Corners.All;
        [Tooltip("How far (canvas units) they're cut back; below 0, the theme's Chamfer Size")]
        [SerializeField] private float chamfer = -1f;

        [Header("Header tab")]
        [Tooltip("Whether it has a tab along its top edge")]
        [SerializeField] private bool headerTab;
        [Tooltip("The tab's colour role")]
        [SerializeField] private UIColorRole headerTabRole = UIColorRole.Accent;
        [Tooltip("How much of its width the tab runs along (0 to 1)")]
        [Range(0f, 1f)] [SerializeField] private float headerTabWidth = 0.4f;
        [Tooltip("How tall (canvas units) the tab is")]
        [Min(0f)] [SerializeField] private float headerTabHeight = 8f;

        [Header("Gloss")]
        [Tooltip("Whether it has the two slanted gloss stripes near its top left")]
        [SerializeField] private bool gloss;
        [Tooltip("The gloss's colour role")]
        [SerializeField] private UIColorRole glossRole = UIColorRole.Light;
        [Tooltip("How opaque the gloss is (0 to 1)")]
        [Range(0f, 1f)] [SerializeField] private float glossOpacity = 0.35f;

        /// <summary>Which corners are cut back.</summary>
        public UIShapeMesh.Corners Corners { get => corners; set { corners = value; SetVerticesDirty(); } }
        /// <summary>How far they're cut back; below 0, the theme's.</summary>
        public float Chamfer { get => chamfer; set { chamfer = value; SetVerticesDirty(); } }
        /// <summary>Whether it has a header tab.</summary>
        public bool HeaderTab { get => headerTab; set { headerTab = value; SetVerticesDirty(); } }
        /// <summary>The header tab's colour role.</summary>
        public UIColorRole HeaderTabRole { get => headerTabRole; set { headerTabRole = value; SetVerticesDirty(); } }
        /// <summary>How much of its width the tab runs along.</summary>
        public float HeaderTabWidth { get => headerTabWidth; set { headerTabWidth = Mathf.Clamp01(value); SetVerticesDirty(); } }
        /// <summary>How tall (canvas units) its header tab is.</summary>
        public float HeaderTabHeight { get => headerTabHeight; set { headerTabHeight = Mathf.Max(0f, value); SetVerticesDirty(); } }
        /// <summary>Whether it has the gloss.</summary>
        public bool Gloss { get => gloss; set { gloss = value; SetVerticesDirty(); } }

        /// <summary>How far its corners are cut back, in use.</summary>
        public float ChamferInUse => chamfer >= 0f ? chamfer : ThemeInUse.ChamferSize;

        /// <summary>Its outline (the cut corners in), counter-clockwise: for clipping what's drawn inside it.</summary>
        public List<Vector2> OutlinePolygon(Rect rect) => UIShapeMesh.ChamferedRect(rect, ChamferInUse, corners);

        protected override void Build(VertexHelper vh, Rect rect)
        {
            List<Vector2> shape = OutlinePolygon(rect);
            if (Fill) { UIShapeMesh.AddFill(vh, shape, RoleColor(FillRole), Feather); }
            List<Vector2> inside = UIShapeMesh.Offset(shape, -OutlineWidthInUse);
            if (headerTab && headerTabHeight > 0f)
            {
                float end = rect.xMin + rect.width * headerTabWidth;
                List<Vector2> tab = UIShapeMesh.Parallelogram(rect.xMin, end, rect.yMax - headerTabHeight, rect.yMax, headerTabHeight);
                tab[1] = new Vector2(end - headerTabHeight, tab[1].y); // Its far end slants back, as the sheet's tabs do.
                tab[2] = new Vector2(end, tab[2].y);
                tab[3] = new Vector2(rect.xMin, tab[3].y);
                UIShapeMesh.AddFill(vh, UIShapeMesh.Clip(tab, shape), RoleColor(headerTabRole), Feather);
            }
            if (gloss)
            {
                Color glossColor = RoleColor(glossRole);
                glossColor.a *= glossOpacity;
                float h = rect.height * 0.55f, w = Mathf.Max(2f, rect.height * 0.08f), x = rect.xMin + ChamferInUse + rect.height * 0.25f;
                for (int i = 0; i < 2; i++, x += w * 2.2f)
                {
                    List<Vector2> stripe = UIShapeMesh.Parallelogram(x, x + w, rect.yMax - h, rect.yMax, h * 0.6f);
                    UIShapeMesh.AddFill(vh, UIShapeMesh.Clip(stripe, inside), glossColor, Feather);
                }
            }
            if (Outline) { UIShapeMesh.AddOutline(vh, shape, OutlineWidthInUse, RoleColor(OutlineRole), Feather); }
        }
    }
}
