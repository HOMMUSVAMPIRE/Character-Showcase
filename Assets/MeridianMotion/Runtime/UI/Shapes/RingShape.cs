using UnityEngine;
using UnityEngine.UI;

namespace MeridianMotion.UI
{
    /// <summary>
    /// A ring (the reference sheet's radial progress: a hold to interact, a cooldown): its track all the way round (Track
    /// Role), and Fraction of it filled (Fill Role) from its start (the top, by default), clockwise unless told otherwise.
    /// Its outline isn't drawn: a ring is its own outline.
    /// </summary>
    [AddComponentMenu("Meridian Motion/UI/Shapes/Ring Shape")]
    public class RingShape : UIShapeGraphic
    {
        /// <summary>Filled in Accent, no outline, by default.</summary>
        public RingShape() : base(UIColorRole.Accent, false)
        {
        }

        [Header("Ring")]
        [Tooltip("How thick (canvas units) the ring is")]
        [Min(0f)] [SerializeField] private float thickness = 6f;
        [Tooltip("How much of it is filled (0 to 1)")]
        [Range(0f, 1f)] [SerializeField] private float fraction = 1f;
        [Tooltip("Where the fill starts, in degrees (90: the top)")]
        [SerializeField] private float startAngle = 90f;
        [Tooltip("Whether it fills clockwise")]
        [SerializeField] private bool clockwise = true;
        [Tooltip("Whether its track (the unfilled ring) shows")]
        [SerializeField] private bool track = true;
        [Tooltip("The track's colour role")]
        [SerializeField] private UIColorRole trackRole = UIColorRole.PanelRaised;

        /// <summary>How much of it is filled (0 to 1).</summary>
        public float Fraction { get => fraction; set { fraction = Mathf.Clamp01(value); SetVerticesDirty(); } }
        /// <summary>How thick the ring is.</summary>
        public float Thickness { get => thickness; set { thickness = Mathf.Max(0f, value); SetVerticesDirty(); } }
        /// <summary>Where the fill starts, in degrees.</summary>
        public float StartAngle { get => startAngle; set { startAngle = value; SetVerticesDirty(); } }
        /// <summary>Whether it fills clockwise.</summary>
        public bool Clockwise { get => clockwise; set { clockwise = value; SetVerticesDirty(); } }
        /// <summary>Whether its track shows.</summary>
        public bool Track { get => track; set { track = value; SetVerticesDirty(); } }

        /// <summary>The angle (degrees) its fill sweeps through: negative clockwise.</summary>
        public float Sweep => 360f * fraction * (clockwise ? -1f : 1f);

        protected override void Build(VertexHelper vh, Rect rect)
        {
            float outer = Mathf.Min(rect.width, rect.height) / 2f, inner = Mathf.Max(0f, outer - thickness);
            if (track) { UIShapeMesh.AddArc(vh, rect.center, inner, outer, 0f, 360f, RoleColor(trackRole), Feather); }
            if (Fill && fraction > 0f) { UIShapeMesh.AddArc(vh, rect.center, inner, outer, startAngle, Sweep, RoleColor(FillRole), Feather); }
        }
    }
}
