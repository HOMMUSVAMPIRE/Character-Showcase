using UnityEngine;
using UnityEngine.Sprites;
using UnityEngine.UI;

namespace MeridianMotion.UI
{
    /// <summary>
    /// An icon (ReferenceAssets/OpenTasks.md, H4): the library's icon of its Icon Name, drawn as a mesh in its Fill Role
    /// (sharp at any size), fitted to its rect and centred. If its theme has a sprite for that name (the theme's Icon
    /// Overrides), the sprite's drawn instead, fitted the same way and tinted the same colour. An unknown name draws a
    /// crossed box, so it's seen.
    /// </summary>
    [AddComponentMenu("Meridian Motion/UI/Icon")]
    public class UIIcon : UIShapeGraphic
    {
        [Header("Icon")]
        [Tooltip("The icon's name (UIIconLibrary's: pin, warning, ladder...)")]
        [SerializeField] private string iconName = UIIconLibrary.Info;

        /// <summary>Drawn in Text, no outline, by default.</summary>
        public UIIcon() : base(UIColorRole.Text, false)
        {
        }

        /// <summary>The icon's name.</summary>
        public string IconName
        {
            get => iconName;
            set
            {
                if (iconName == value) { return; }
                iconName = value;
                SetVerticesDirty();
                SetMaterialDirty();
            }
        }

        /// <summary>The sprite drawn in the library's icon's place (its theme's), or null.</summary>
        public Sprite OverrideSprite => ThemeInUse.IconSprite(iconName);

        /// <summary>Whether the library has an icon of its name (or its theme has a sprite for it).</summary>
        public bool IsKnown => OverrideSprite != null || UIIconLibrary.Has(iconName);

        public override Texture mainTexture
        {
            get
            {
                Sprite sprite = OverrideSprite;
                return sprite != null ? sprite.texture : s_WhiteTexture;
            }
        }

        protected override void ThemeChanged()
        {
            SetVerticesDirty();
            SetMaterialDirty();
        }

        protected override void Build(VertexHelper vh, Rect rect)
        {
            if (!Fill) { return; }
            Color colour = RoleColor(FillRole);
            Sprite sprite = OverrideSprite;
            if (sprite != null)
            {
                AddSprite(vh, rect, sprite, colour);
                return;
            }
            var painter = new UIIconPainter(vh, rect, colour, Feather);
            if (!UIIconLibrary.Draw(iconName, painter))
            {
                painter.Frame(4f, 4f, 20f, 20f, 2f);
                painter.Line(2f, new Vector2(4f, 4f), new Vector2(20f, 20f));
            }
        }

        // The sprite, its aspect kept, fitted to the rect and centred.
        private static void AddSprite(VertexHelper vh, Rect rect, Sprite sprite, Color colour)
        {
            Vector2 size = sprite.rect.size;
            float scale = Mathf.Min(rect.width / size.x, rect.height / size.y);
            Rect fitted = new(rect.center - size * scale / 2f, size * scale);
            Vector4 uv = DataUtility.GetOuterUV(sprite);
            int start = vh.currentVertCount;
            vh.AddVert(new Vector3(fitted.xMin, fitted.yMin), colour, new Vector2(uv.x, uv.y));
            vh.AddVert(new Vector3(fitted.xMax, fitted.yMin), colour, new Vector2(uv.z, uv.y));
            vh.AddVert(new Vector3(fitted.xMax, fitted.yMax), colour, new Vector2(uv.z, uv.w));
            vh.AddVert(new Vector3(fitted.xMin, fitted.yMax), colour, new Vector2(uv.x, uv.w));
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
