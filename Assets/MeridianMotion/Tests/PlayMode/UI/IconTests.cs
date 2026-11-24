using System.Collections.Generic;
using System.Linq;
using MeridianMotion.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace MeridianMotion.Tests.UI
{
    /// <summary>
    /// Tests of the icons drawn in code (UIIcon, UIIconLibrary): every name draws something inside its rect, at any size;
    /// the names are unique; a theme's sprite stands in for an icon when it has one; an unknown name still draws.
    /// </summary>
    [Category(TestArea.UI)]
    public class IconTests : UITestsBase
    {
        private UIIcon Icon(string name, Vector2 size)
        {
            var obj = Track(new GameObject("Icon", typeof(RectTransform)));
            obj.transform.SetParent(manager.defaultCanvas, false);
            ((RectTransform)obj.transform).sizeDelta = size;
            UIIcon icon = obj.AddComponent<UIIcon>();
            icon.IconName = name;
            return icon;
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

        [Test]
        public void EveryIcon_DrawsInsideItsRect_AtAnySize()
        {
            foreach (Vector2 size in new[] { new Vector2(24f, 24f), new Vector2(96f, 64f) })
            {
                foreach (string name in UIIconLibrary.Names)
                {
                    UIIcon icon = Icon(name, size);
                    List<UIVertex> vertices = Vertices(icon);
                    Assert.That(vertices.Count, Is.GreaterThan(0), $"'{name}' should draw something.");
                    // Fitted to the rect's smaller side, centred: a pixel of feather beyond it, at most.
                    float half = Mathf.Min(size.x, size.y) / 2f + 1f;
                    // The furthest vertex found in a plain loop, then one assert: an assert (and its message) a vertex, for
                    // every vertex of every icon, made this test take many seconds.
                    Vector3 furthest = Vector3.zero;
                    float reach = 0f;
                    foreach (UIVertex vertex in vertices)
                    {
                        float extent = Mathf.Max(Mathf.Abs(vertex.position.x), Mathf.Abs(vertex.position.y));
                        if (extent > reach) { reach = extent; furthest = vertex.position; }
                    }
                    if (reach > half) { Assert.Fail($"'{name}' at {size} reaches {furthest}, beyond {half} from its centre."); }
                    Object.DestroyImmediate(icon.gameObject);
                }
            }
        }

        [Test]
        public void TheIconNames_AreUnique_AndEachIsKnown()
        {
            IReadOnlyList<string> names = UIIconLibrary.Names;
            Assert.That(names.Distinct().Count(), Is.EqualTo(names.Count), "Two icons share a name.");
            foreach (string name in names)
            {
                Assert.That(UIIconLibrary.Has(name), Is.True, name);
                Assert.That(name, Is.EqualTo(name.ToLowerInvariant()), $"Icon names are lower case: '{name}'.");
            }
        }

        [Test]
        public void ATheme_SpriteStandsInForItsIcon_AndIsTintedByRole()
        {
            var theme = Track(ScriptableObject.CreateInstance<UITheme>());
            var texture = Track(new Texture2D(8, 4));
            var sprite = Track(Sprite.Create(texture, new Rect(0f, 0f, 8f, 4f), Vector2.one / 2f));
            UIIcon icon = Icon(UIIconLibrary.Gear, new Vector2(40f, 40f));
            icon.Theme = theme;
            int drawn = Vertices(icon).Count;

            theme.SetIconSprite(UIIconLibrary.Gear, sprite);
            Assert.That(icon.OverrideSprite, Is.SameAs(sprite));
            Assert.That(icon.mainTexture, Is.SameAs(texture), "It should draw with the sprite's texture.");
            Assert.That(icon.NeedsRedraw, Is.True, "Setting the sprite should redraw it.");
            List<UIVertex> quad = Vertices(icon);
            Assert.That(quad.Count, Is.EqualTo(6), "A sprite is one quad.");
            Assert.That(quad.Max(v => v.position.x) - quad.Min(v => v.position.x), Is.EqualTo(40f).Within(0.01f), "Fitted to the rect's width...");
            Assert.That(quad.Max(v => v.position.y) - quad.Min(v => v.position.y), Is.EqualTo(20f).Within(0.01f), "...keeping its aspect.");
            Assert.That((Color)quad[0].color, Is.EqualTo(icon.RoleColor(icon.FillRole)).Using(new ColorComparer()));

            theme.SetIconSprite(UIIconLibrary.Gear, null);
            Assert.That(icon.OverrideSprite, Is.Null);
            Assert.That(Vertices(icon).Count, Is.EqualTo(drawn), "Cleared, the library's icon should be drawn again.");
        }

        [Test]
        public void AnUnknownName_StillDrawsSomething()
        {
            UIIcon icon = Icon("no-such-icon", new Vector2(32f, 32f));
            Assert.That(icon.IsKnown, Is.False);
            Assert.That(Vertices(icon).Count, Is.GreaterThan(0), "An unknown icon should show (a crossed box), not vanish.");
        }

        // Colours equal to a Color32's precision.
        private sealed class ColorComparer : IEqualityComparer<Color>
        {
            public bool Equals(Color a, Color b) => ((Color32)a).Equals((Color32)b);
            public int GetHashCode(Color c) => ((Color32)c).GetHashCode();
        }
    }
}
