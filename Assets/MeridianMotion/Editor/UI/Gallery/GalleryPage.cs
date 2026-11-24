using MeridianMotion.UI;
using TMPro;
using UnityEngine;

namespace MeridianMotion.UI.EditorTools
{
    /// <summary>
    /// One page of the UI gallery, laid out in rows from the top left: Row starts a row with its heading, and Add puts a
    /// sample in it (left to right, its caption under it), every shape drawn in the gallery's theme.
    /// </summary>
    internal sealed class GalleryPage
    {
        // How the page is laid out, in reference pixels.
        private const float Margin = 48f, HeadingWidth = 220f, Gap = 32f, RowGap = 40f, CaptionHeight = 26f;
        private const float TitleSize = 40f, HeadingSize = 22f, CaptionSize = 16f;

        public Canvas Canvas { get; }
        public UITheme Theme { get; }

        private readonly RectTransform root;
        private float x, y, rowHeight;

        public GalleryPage(string name, Canvas canvas, UITheme theme)
        {
            Canvas = canvas;
            Theme = theme;
            root = (RectTransform)canvas.transform;
            Text(root, name.ToUpperInvariant(), new Vector2(Margin, -Margin), new Vector2(1400f, 50f), TitleSize, UIColorRole.Text, display: true);
            y = Margin + 50f + RowGap;
        }

        /// <summary>Starts a row, headed by its title.</summary>
        public void Row(string title)
        {
            if (rowHeight > 0f) { y += rowHeight + CaptionHeight + RowGap; }
            rowHeight = 0f;
            x = Margin + HeadingWidth;
            if (string.IsNullOrEmpty(title)) { return; }
            Text(root, title.ToUpperInvariant(), new Vector2(Margin, -y), new Vector2(HeadingWidth - Gap, 30f), HeadingSize, UIColorRole.Accent, display: true);
        }

        /// <summary>Adds a sample of size to the row, with its caption under it: an empty object to build in.</summary>
        public RectTransform Add(string caption, Vector2 size)
        {
            var obj = new GameObject(caption, typeof(RectTransform));
            obj.layer = root.gameObject.layer;
            var rect = (RectTransform)obj.transform;
            rect.SetParent(root, false);
            Place(rect, new Vector2(x, -y), size);
            Text(root, caption, new Vector2(x, -y - size.y - 4f), new Vector2(Mathf.Max(size.x, 120f), CaptionHeight), CaptionSize, UIColorRole.TextMuted, display: false);
            x += Mathf.Max(size.x, 120f) + Gap;
            rowHeight = Mathf.Max(rowHeight, size.y);
            return rect;
        }

        /// <summary>Adds a shape of size to the row, captioned, in the gallery's theme.</summary>
        public T Add<T>(string caption, Vector2 size) where T : UIShapeGraphic => Shape<T>(Add(caption, size));

        /// <summary>A shape filling parent, in the gallery's theme.</summary>
        public T Shape<T>(RectTransform parent) where T : UIShapeGraphic
        {
            T shape = parent.GetComponent<UIShapeGraphic>() == null ? parent.gameObject.AddComponent<T>() : Child<T>(parent);
            shape.Theme = Theme;
            return shape;
        }

        /// <summary>A label filling parent, centred, in role, in the display font.</summary>
        public TextMeshProUGUI Label(RectTransform parent, string text, float size, UIColorRole role) =>
            Words(parent, text, size, role, display: true, TextAlignmentOptions.Center);

        /// <summary>
        /// Text filling parent, in role, in the display font (headers, numerals, buttons) or the body font. Without a
        /// display font, TextMeshPro's default, made bold.
        /// </summary>
        public TextMeshProUGUI Words(RectTransform parent, string text, float size, UIColorRole role, bool display, TextAlignmentOptions alignment)
        {
            var obj = new GameObject("Label", typeof(RectTransform));
            obj.layer = parent.gameObject.layer;
            var rect = (RectTransform)obj.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            TextMeshProUGUI label = obj.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = size;
            label.alignment = alignment;
            label.color = Theme.Color(role);
            Style(label, display);
            return label;
        }

        // A shape on a child filling parent (a parent can have only one graphic).
        private T Child<T>(RectTransform parent) where T : UIShapeGraphic
        {
            var obj = new GameObject(typeof(T).Name, typeof(RectTransform));
            obj.layer = parent.gameObject.layer;
            var rect = (RectTransform)obj.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return obj.AddComponent<T>();
        }

        private void Text(RectTransform parent, string text, Vector2 position, Vector2 size, float fontSize, UIColorRole role, bool display)
        {
            var obj = new GameObject(text, typeof(RectTransform));
            obj.layer = parent.gameObject.layer;
            var rect = (RectTransform)obj.transform;
            rect.SetParent(parent, false);
            Place(rect, position, size);
            TextMeshProUGUI label = obj.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = fontSize;
            label.color = Theme.Color(role);
            label.alignment = TextAlignmentOptions.TopLeft;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            Style(label, display);
        }

        // The display or body font; TextMeshPro's default (bold, for display) where the theme has none.
        private void Style(TMP_Text label, bool display)
        {
            TMP_FontAsset font = display ? Theme.DisplayFont : Theme.BodyFont;
            if (font != null) { label.font = font; }
            label.fontStyle = display && font == null ? FontStyles.Bold : FontStyles.Normal;
        }

        // Anchored to the top left, at position (y down: negative), of size.
        private static void Place(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }
    }
}
