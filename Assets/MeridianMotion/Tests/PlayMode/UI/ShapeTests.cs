using System.Collections.Generic;
using MeridianMotion.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace MeridianMotion.Tests.UI
{
    /// <summary>
    /// Tests of the UI's shapes (UIShapeGraphic and its kinds) and its theme (UITheme): each mesh inside its rect (with its
    /// feather), a chamfered box's corners cut, a ring's arc following its fraction, a bar's segments filling, colours
    /// taken from the theme's roles, and a change to the theme reaching every shape.
    /// </summary>
    [Category(TestArea.UI)]
    public class ShapeTests : UITestsBase
    {
        private static readonly Vector2 Size = new(200f, 80f);

        private T Shape<T>(Vector2? size = null) where T : UIShapeGraphic
        {
            var obj = Track(new GameObject(typeof(T).Name, typeof(RectTransform)));
            obj.transform.SetParent(manager.defaultCanvas, false);
            ((RectTransform)obj.transform).sizeDelta = size ?? Size;
            return obj.AddComponent<T>();
        }

        private static List<UIVertex> Vertices(UIShapeGraphic shape)
        {
            var vh = new VertexHelper();
            shape.BuildMesh(vh);
            var vertices = new List<UIVertex>();
            vh.GetUIVertexStream(vertices);
            vh.Dispose();
            return vertices;
        }

        // The rect a shape draws in, grown by its feather's reach: half a pixel, further along a sharp corner's mitre.
        private static Rect Bounds(UIShapeGraphic shape, float grow = 4f)
        {
            Rect rect = shape.GetPixelAdjustedRect();
            return Rect.MinMaxRect(rect.xMin - grow, rect.yMin - grow, rect.xMax + grow, rect.yMax + grow);
        }

        private static bool Same(Color32 a, Color32 b) => a.r == b.r && a.g == b.g && a.b == b.b;

        private UITheme NewTheme()
        {
            var theme = Track(ScriptableObject.CreateInstance<UITheme>());
            return theme;
        }

        [Test]
        public void EveryShape_DrawsInsideItsRect()
        {
            var shapes = new UIShapeGraphic[]
            {
                Shape<ChamferedBox>(), Shape<SkewedBar>(), Shape<DiamondShape>(), Shape<RingShape>(), Shape<StripeFill>(),
                Shape<CornerBrackets>(), Shape<Divider>(),
            };
            ((ChamferedBox)shapes[0]).HeaderTab = true;
            ((ChamferedBox)shapes[0]).Gloss = true;
            foreach (UIShapeGraphic shape in shapes)
            {
                List<UIVertex> vertices = Vertices(shape);
                Assert.That(vertices.Count, Is.GreaterThan(0), $"{shape.GetType().Name} should draw something.");
                Rect bounds = Bounds(shape);
                foreach (UIVertex vertex in vertices)
                {
                    Assert.That(bounds.Contains((Vector2)vertex.position), Is.True,
                        $"{shape.GetType().Name}'s vertex {vertex.position} is outside its rect {bounds}.");
                }
            }
        }

        [Test]
        public void ADiamondsGlow_ReachesTheThemesGlowSize_AndNoFurther()
        {
            var diamond = Shape<DiamondShape>(new Vector2(60f, 60f));
            diamond.Glow = true;
            float glow = diamond.ThemeInUse.GlowSize;
            float reach = 0f;
            foreach (UIVertex vertex in Vertices(diamond))
            {
                reach = Mathf.Max(reach, Mathf.Abs(vertex.position.x), Mathf.Abs(vertex.position.y));
            }
            // The diamond's tips are 30 from its centre; at its square corners the glow's edges meet a mitre of glow / cos 45 out.
            Assert.That(reach, Is.GreaterThan(30f + glow), "The glow should reach out the theme's glow size from the edges.");
            Assert.That(reach, Is.LessThan(30f + glow / Mathf.Cos(45f * Mathf.Deg2Rad) + 2f), "The glow should reach no further than its mitre.");
        }

        [Test]
        public void AChamferedBox_CutsOnlyTheCornersItsTold()
        {
            var box = Shape<ChamferedBox>();
            box.Outline = false;
            box.Chamfer = 20f;
            box.Corners = UIShapeMesh.Corners.TopLeft | UIShapeMesh.Corners.BottomRight;
            Rect rect = box.GetPixelAdjustedRect();
            List<Vector2> outline = box.OutlinePolygon(rect);
            bool Has(Vector2 point) => outline.Exists(p => (p - point).sqrMagnitude < 0.01f);

            Assert.That(Has(new Vector2(rect.xMax, rect.yMax)), Is.True, "The top right corner should be square.");
            Assert.That(Has(new Vector2(rect.xMin, rect.yMin)), Is.True, "The bottom left corner should be square.");
            Assert.That(Has(new Vector2(rect.xMin, rect.yMax)), Is.False, "The top left corner should be cut.");
            Assert.That(Has(new Vector2(rect.xMax, rect.yMin)), Is.False, "The bottom right corner should be cut.");
            Assert.That(Has(new Vector2(rect.xMin + 20f, rect.yMax)) && Has(new Vector2(rect.xMin, rect.yMax - 20f)), Is.True,
                "The top left should be cut back by the chamfer along both sides.");
        }

        [Test]
        public void AChamferedBoxWithoutAChamfer_UsesTheThemes()
        {
            UITheme theme = NewTheme();
            theme.ChamferSize = 17f;
            var box = Shape<ChamferedBox>();
            box.Theme = theme;
            Assert.That(box.ChamferInUse, Is.EqualTo(17f));
            box.Chamfer = 5f;
            Assert.That(box.ChamferInUse, Is.EqualTo(5f), "Its own chamfer should win over the theme's.");
        }

        [Test]
        public void ARingsArc_FollowsItsFraction()
        {
            var ring = Shape<RingShape>(new Vector2(100f, 100f));
            ring.Track = false;
            ring.Fraction = 0f;
            Assert.That(Vertices(ring).Count, Is.Zero, "An empty ring without its track draws nothing.");

            // Clockwise from the top: a quarter covers the top-right quadrant only.
            ring.Fraction = 0.25f;
            foreach (UIVertex vertex in Vertices(ring))
            {
                Assert.That(vertex.position.x, Is.GreaterThan(-0.5f), "A quarter ring shouldn't reach left of the top.");
                Assert.That(vertex.position.y, Is.GreaterThan(-0.5f), "A quarter ring shouldn't reach below its right.");
            }
            Assert.That(ring.Sweep, Is.EqualTo(-90f).Within(0.001f));
            int quarter = Vertices(ring).Count;
            ring.Fraction = 1f;
            Assert.That(Vertices(ring).Count, Is.GreaterThan(quarter * 3), "A full ring should have about four times a quarter's steps.");

            ring.Clockwise = false;
            ring.Fraction = 0.25f;
            foreach (UIVertex vertex in Vertices(ring)) { Assert.That(vertex.position.x, Is.LessThan(0.5f), "Anticlockwise, a quarter goes left."); }
        }

        [Test]
        public void ASegmentedBar_FillsWholeSegmentsByItsFraction()
        {
            UITheme theme = NewTheme();
            var bar = Shape<SkewedBar>();
            bar.Theme = theme;
            bar.Outline = false;
            bar.Segments = 5;
            bar.Fraction = 0.6f;
            Assert.That(bar.FilledSegments, Is.EqualTo(3));
            Color filled = theme.Color(bar.FillRole), empty = theme.Color(bar.EmptyRole);
            int filledVerts = 0, emptyVerts = 0;
            foreach (UIVertex vertex in Vertices(bar))
            {
                if (vertex.color.a < 250) { continue; } // The feather.
                if (Same(vertex.color, filled)) { filledVerts++; }
                else if (Same(vertex.color, empty)) { emptyVerts++; }
            }
            Assert.That(filledVerts, Is.GreaterThan(0));
            Assert.That(filledVerts * 2, Is.EqualTo(emptyVerts * 3), "Three segments should be filled to two empty.");
        }

        [Test]
        public void AShapesColours_ComeFromItsThemesRoles_TintedByItsColour()
        {
            UITheme theme = NewTheme();
            theme.SetColor(UIColorRole.Accent, new Color(1f, 0f, 0f, 1f));
            var box = Shape<ChamferedBox>();
            box.Theme = theme;
            box.FillRole = UIColorRole.Accent;
            box.color = new Color(1f, 1f, 1f, 0.5f);
            Assert.That(box.RoleColor(UIColorRole.Accent), Is.EqualTo(new Color(1f, 0f, 0f, 0.5f)));
        }

        [Test]
        public void WithoutATheme_AShapeUsesTheManagers_ElseTheFallback_ElseTheBuiltIn()
        {
            var box = Shape<ChamferedBox>();
            UITheme fallback = UITheme.Fallback;
            try
            {
                UITheme.Fallback = null;
                Assert.That(box.ThemeInUse, Is.SameAs(UITheme.BuiltIn), "With no theme anywhere, the built-in one.");
                UITheme other = NewTheme();
                UITheme.Fallback = other;
                Assert.That(box.ThemeInUse, Is.SameAs(other), "Without the UI Manager's, the fallback.");
                UITheme theme = NewTheme();
                manager.Theme = theme;
                Assert.That(box.ThemeInUse, Is.SameAs(theme), "The UI Manager's theme should be the current one.");
            }
            finally
            {
                manager.Theme = null;
                UITheme.Fallback = fallback;
            }
        }

        [Test]
        public void ChangingTheTheme_RedrawsTheShapesDrawnInIt()
        {
            UITheme theme = NewTheme();
            var box = Shape<ChamferedBox>();
            box.Theme = theme;
            Canvas.ForceUpdateCanvases();
            Assert.That(box.NeedsRedraw, Is.False, "Drawn, it shouldn't need drawing again.");
            theme.SetColor(UIColorRole.Panel, Color.magenta);
            Assert.That(box.NeedsRedraw, Is.True, "A change to its theme should make it draw again.");
        }

        [Test]
        public void ATheme_HasAColourForEveryRole()
        {
            UITheme theme = NewTheme();
            foreach (UIColorRole role in System.Enum.GetValues(typeof(UIColorRole)))
            {
                Assert.That(theme.Color(role), Is.EqualTo(UITheme.DefaultColor(role)), $"{role} should start as its default.");
            }
        }
    }
}
