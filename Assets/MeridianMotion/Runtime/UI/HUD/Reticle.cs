using UnityEngine;
using UnityEngine.UI;

namespace MeridianMotion.UI
{
    /// <summary>
    /// The aiming reticle (ReferenceAssets/OpenTasks.md, H6), drawn as a mesh on the icons' 24-unit grid, fitted to its
    /// rect: a Crosshair, Dotted ring, corner Brackets, or a Lock On (brackets round a ring, glowing). LockOn plays the
    /// lock-on: it closes in from Lock On Scale to its size over Lock On Seconds, on the UI's clock; Unlock lets go.
    /// Drawn in its Fill Role.
    /// </summary>
    [AddComponentMenu("Meridian Motion/UI/HUD/Reticle")]
    public class Reticle : UIShapeGraphic
    {
        /// <summary>A reticle's look.</summary>
        public enum Style { Crosshair, Dotted, Brackets, LockOn }

        [Header("Reticle")]
        [Tooltip("Its look")]
        [SerializeField] private Style style = Style.Crosshair;
        [Tooltip("How much bigger it starts as it locks on")]
        [Min(1f)] [SerializeField] private float lockOnScale = 1.8f;
        [Tooltip("How long (seconds, on the UI's clock) it takes to close in as it locks on")]
        [Min(0.01f)] [SerializeField] private float lockOnSeconds = 0.25f;

        private float lockedAt = float.NegativeInfinity;
        private bool locked;

        /// <summary>Drawn in Light, no outline, by default.</summary>
        public Reticle() : base(UIColorRole.Light, false)
        {
        }

        /// <summary>Its look.</summary>
        public Style CurrentStyle { get => style; set { style = value; SetVerticesDirty(); } }
        /// <summary>Whether it's locked on.</summary>
        public bool Locked => locked;

        /// <summary>How far the lock-on's closed in (0 just begun, 1 done; 1 when not locked on).</summary>
        public float LockOnProgress => locked ? Mathf.Clamp01((UIClock.Now - lockedAt) / lockOnSeconds) : 1f;

        /// <summary>How big it's drawn, for its lock-on (1: its size).</summary>
        public float Scale => Mathf.Lerp(lockOnScale, 1f, Smooth(LockOnProgress));

        /// <summary>Locks on: shows the Lock On look, closing in.</summary>
        public void LockOn()
        {
            locked = true;
            lockedAt = UIClock.Now;
            style = Style.LockOn;
            SetVerticesDirty();
        }

        /// <summary>Lets go of a lock-on, back to the look given.</summary>
        public void Unlock(Style back = Style.Crosshair)
        {
            locked = false;
            style = back;
            SetVerticesDirty();
        }

        private void Update()
        {
            if (locked && LockOnProgress < 1f) { SetVerticesDirty(); }
        }

        protected override void Build(VertexHelper vh, Rect rect)
        {
            if (!Fill) { return; }
            float scale = Scale;
            var drawn = new Rect(rect.center - rect.size * scale / 2f, rect.size * scale);
            var p = new UIIconPainter(vh, drawn, RoleColor(FillRole), Feather);
            switch (style)
            {
                case Style.Crosshair:
                    p.Line(1.5f, new Vector2(12f, 3f), new Vector2(12f, 9f));
                    p.Line(1.5f, new Vector2(12f, 15f), new Vector2(12f, 21f));
                    p.Line(1.5f, new Vector2(3f, 12f), new Vector2(9f, 12f));
                    p.Line(1.5f, new Vector2(15f, 12f), new Vector2(21f, 12f));
                    p.Dot(new Vector2(12f, 12f), 1f);
                    break;
                case Style.Dotted:
                    for (int i = 0; i < 12; i++)
                    {
                        float angle = i * 30f * Mathf.Deg2Rad;
                        p.Dot(new Vector2(12f + Mathf.Cos(angle) * 8f, 12f + Mathf.Sin(angle) * 8f), 0.9f);
                    }
                    p.Dot(new Vector2(12f, 12f), 1f);
                    break;
                case Style.Brackets:
                    Brackets(p, 4f, 5f);
                    p.Dot(new Vector2(12f, 12f), 1f);
                    break;
                default:
                    Color glow = RoleColor(UIColorRole.Highlight);
                    glow.a *= ThemeInUse.GlowStrength * LockOnProgress;
                    UIShapeMesh.AddGlow(vh, UIShapeMesh.Diamond(new Rect(rect.center - rect.size * 0.2f, rect.size * 0.4f)), ThemeInUse.GlowSize * 0.5f, glow);
                    Brackets(p, 3f, 5f);
                    p.Circle(new Vector2(12f, 12f), 4.5f, 1.5f);
                    p.Dot(new Vector2(12f, 12f), 1.2f);
                    break;
            }
        }

        // An L at each corner, inset from the grid's edge, each arm length long.
        private static void Brackets(UIIconPainter p, float inset, float length)
        {
            float a = inset, b = 24f - inset;
            p.Line(1.75f, new Vector2(a, a + length), new Vector2(a, a), new Vector2(a + length, a));
            p.Line(1.75f, new Vector2(b - length, a), new Vector2(b, a), new Vector2(b, a + length));
            p.Line(1.75f, new Vector2(b, b - length), new Vector2(b, b), new Vector2(b - length, b));
            p.Line(1.75f, new Vector2(a + length, b), new Vector2(a, b), new Vector2(a, b - length));
        }

        private static float Smooth(float t) => t * t * (3f - 2f * t);
    }
}
