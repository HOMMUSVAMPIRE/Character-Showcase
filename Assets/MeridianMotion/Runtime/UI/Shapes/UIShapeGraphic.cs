using UnityEngine;
using UnityEngine.UI;

namespace MeridianMotion.UI
{
    /// <summary>
    /// The base of the UI's shapes: a Graphic that builds its own mesh (UIShapeMesh), so it's sharp at any size and needs
    /// no sprite, coloured by role from its theme (its own, else UITheme.Current) and tinted by its Color. Its fill and
    /// outline can each be on or off; their edges are feathered by about a pixel. It redraws when its theme changes.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public abstract class UIShapeGraphic : MaskableGraphic
    {
        [Header("Look")]
        [Tooltip("The theme its colours come from (none: the UI Manager's, else the built-in palette)")]
        [SerializeField] private UITheme theme;
        [Tooltip("Whether it's filled")]
        [SerializeField] private bool fill = true;
        [Tooltip("The fill's colour role")]
        [SerializeField] private UIColorRole fillRole = UIColorRole.Panel;
        [Tooltip("Whether it has an outline")]
        [SerializeField] private bool outline = true;
        [Tooltip("The outline's colour role")]
        [SerializeField] private UIColorRole outlineRole = UIColorRole.Outline;
        [Tooltip("The outline's width (canvas units); below 0, the theme's")]
        [SerializeField] private float outlineWidth = -1f;

        /// <summary>A shape filled in Panel and outlined in Outline.</summary>
        protected UIShapeGraphic() : this(UIColorRole.Panel, true)
        {
        }

        /// <summary>A shape of another kind's defaults: its fill role, and whether it's outlined (a new one's; saved ones keep theirs).</summary>
        protected UIShapeGraphic(UIColorRole defaultFillRole, bool outlinedByDefault)
        {
            fillRole = defaultFillRole;
            outline = outlinedByDefault;
        }

        /// <summary>Its own theme, or null (the UI Manager's, else the built-in palette).</summary>
        public UITheme Theme { get => theme; set { theme = value; SetVerticesDirty(); } }
        /// <summary>Whether it's filled.</summary>
        public bool Fill { get => fill; set { fill = value; SetVerticesDirty(); } }
        /// <summary>The fill's colour role.</summary>
        public UIColorRole FillRole { get => fillRole; set { fillRole = value; SetVerticesDirty(); } }
        /// <summary>Whether it has an outline.</summary>
        public bool Outline { get => outline; set { outline = value; SetVerticesDirty(); } }
        /// <summary>The outline's colour role.</summary>
        public UIColorRole OutlineRole { get => outlineRole; set { outlineRole = value; SetVerticesDirty(); } }
        /// <summary>The outline's width; below 0, the theme's.</summary>
        public float OutlineWidth { get => outlineWidth; set { outlineWidth = value; SetVerticesDirty(); } }

        /// <summary>The theme it draws with: its own, else the UI Manager's, else the built-in palette.</summary>
        public UITheme ThemeInUse => theme != null ? theme : UITheme.Current;

        /// <summary>The outline's width it draws with.</summary>
        protected float OutlineWidthInUse => outlineWidth >= 0f ? outlineWidth : ThemeInUse.OutlineWidth;

        /// <summary>About a pixel, in canvas units: how wide an edge's feather is.</summary>
        protected float Feather => canvas != null && canvas.scaleFactor > 0f ? 1f / canvas.scaleFactor : 1f;

        /// <summary>The role's colour from its theme, tinted by its Color.</summary>
        public Color RoleColor(UIColorRole role) => ThemeInUse.Color(role) * color;

        protected override void OnEnable()
        {
            base.OnEnable();
            UITheme.Changed += OnThemeChanged;
        }

        protected override void OnDisable()
        {
            UITheme.Changed -= OnThemeChanged;
            base.OnDisable();
        }

        private void OnThemeChanged(UITheme changed)
        {
            if (this != null) { ThemeChanged(); }
        }

        /// <summary>Called when a theme changes: it redraws (a shape that takes more from its theme does more).</summary>
        protected virtual void ThemeChanged() => SetVerticesDirty();

        /// <summary>Whether its mesh is to be built again (its vertices made dirty since it was last built).</summary>
        public bool NeedsRedraw { get; private set; } = true;

        public override void SetVerticesDirty()
        {
            NeedsRedraw = true;
            base.SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            NeedsRedraw = false;
            BuildMesh(vh);
        }

        /// <summary>Builds its mesh into vh (cleared first), for its rect as it is now.</summary>
        public void BuildMesh(VertexHelper vh)
        {
            vh.Clear();
            Build(vh, GetPixelAdjustedRect());
        }

        /// <summary>Adds the shape's mesh for the rect.</summary>
        protected abstract void Build(VertexHelper vh, Rect rect);

        /// <summary>Fills and outlines a convex polygon as its Fill and Outline say.</summary>
        protected void AddFilledAndOutlined(VertexHelper vh, System.Collections.Generic.IReadOnlyList<Vector2> polygon)
        {
            if (fill) { UIShapeMesh.AddFill(vh, polygon, RoleColor(fillRole), Feather); }
            if (outline) { UIShapeMesh.AddOutline(vh, polygon, OutlineWidthInUse, RoleColor(outlineRole), Feather); }
        }
    }
}
