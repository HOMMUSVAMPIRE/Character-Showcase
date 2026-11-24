using TMPro;
using UnityEngine;

namespace MeridianMotion.UI
{
    /// <summary>
    /// Text in the theme's look (ReferenceAssets/OpenTasks.md, H5): its font the theme's Display or Body font, its colour its
    /// role's (times its Tint), kept so when the theme changes, in the editor too. Put it beside a TextMeshPro text; it sets
    /// the text's font and colour, so set those here rather than on the text.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(TMP_Text))]
    [AddComponentMenu("Meridian Motion/UI/Themed Text")]
    public class ThemedText : MonoBehaviour
    {
        [Tooltip("The theme it's drawn in (none: the UI Manager's, UITheme.Current)")]
        [SerializeField] private UITheme theme;
        [Tooltip("The display font (headers, numerals, buttons), else the body font")]
        [SerializeField] private bool display;
        [Tooltip("Its colour's role")]
        [SerializeField] private UIColorRole role = UIColorRole.Text;
        [Tooltip("Multiplies its role's colour (its alpha fades it)")]
        [SerializeField] private Color tint = Color.white;

        private TMP_Text text;

        /// <summary>The theme it's drawn in (null: the current one).</summary>
        public UITheme Theme { get => theme; set { theme = value; Apply(); } }
        /// <summary>Whether it's in the display font.</summary>
        public bool Display { get => display; set { display = value; Apply(); } }
        /// <summary>Its colour's role.</summary>
        public UIColorRole Role { get => role; set { role = value; Apply(); } }
        /// <summary>Multiplies its role's colour.</summary>
        public Color Tint { get => tint; set { tint = value; Apply(); } }
        /// <summary>The theme in use: its own, else the current one.</summary>
        public UITheme ThemeInUse => theme != null ? theme : UITheme.Current;

        /// <summary>Sets its text's font and colour from the theme.</summary>
        public void Apply()
        {
            if (text == null) { text = GetComponent<TMP_Text>(); }
            if (text == null) { return; }
            UITheme inUse = ThemeInUse;
            TMP_FontAsset font = display ? inUse.DisplayFont : inUse.BodyFont;
            if (font != null && text.font != font) { text.font = font; }
            text.color = inUse.Color(role) * tint;
        }

        private void OnEnable()
        {
            UITheme.Changed += OnThemeChanged;
            Apply();
        }

        private void OnDisable() => UITheme.Changed -= OnThemeChanged;

        private void OnValidate() => Apply();

        private void OnThemeChanged(UITheme changed)
        {
            if (this != null) { Apply(); }
        }
    }
}
