using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace MeridianMotion.UI
{
    /// <summary>
    /// The UI's look in one asset: a colour for each role (UIColorRole), the size of a panel's cut corners, an outline's
    /// width, a glow's size and strength, the fonts, and how a control's states tint it. Every shape, text and icon asks
    /// it by role, so retuning it restyles them all; the UIManager names the theme in use (a shape can name its own).
    /// Changing it (in the inspector, or SetColor) tells every shape to redraw (Changed). Without one, BuiltIn: the
    /// levels' palette.
    /// </summary>
    [CreateAssetMenu(fileName = "UI Theme", menuName = "Meridian Motion/UI/Theme")]
    public class UITheme : ScriptableObject
    {
        /// <summary>A role's colour.</summary>
        [Serializable]
        public struct RoleColor
        {
            public UIColorRole role;
            public Color color;
        }

        /// <summary>A sprite standing in for an icon of the library's, by its name.</summary>
        [Serializable]
        public struct IconOverride
        {
            [Tooltip("The icon's name (UIIconLibrary's)")]
            public string name;
            [Tooltip("The sprite drawn in its place (white, so it takes the icon's colour)")]
            public Sprite sprite;
        }

        [Header("Colours")]
        [Tooltip("The colour of each role (Panel, Outline, Accent...): a role not listed takes the levels' palette's")]
        [SerializeField] private List<RoleColor> colors = DefaultColors();

        [Header("Shapes")]
        [Tooltip("How far (canvas units) a panel's or button's corners are cut back, at 45 degrees")]
        [Min(0f)] [SerializeField] private float chamferSize = 12f;
        [Tooltip("How wide (canvas units) an outline is")]
        [Min(0f)] [SerializeField] private float outlineWidth = 2f;
        [Tooltip("How far (canvas units) a glow reaches out from its shape")]
        [Min(0f)] [SerializeField] private float glowSize = 14f;
        [Tooltip("How strong a glow is at its shape's edge (0 to 1), fading to nothing at its reach")]
        [Range(0f, 1f)] [SerializeField] private float glowStrength = 0.55f;

        [Header("Fonts")]
        [Tooltip("Headers, numerals and buttons (none: TextMeshPro's default)")]
        [SerializeField] private TMP_FontAsset displayFont;
        [Tooltip("Body text (none: TextMeshPro's default)")]
        [SerializeField] private TMP_FontAsset bodyFont;

        [Header("States")]
        [Tooltip("Multiplies a control's colours while it's hovered")]
        [SerializeField] private Color hoveredTint = new(1.15f, 1.15f, 1.15f, 1f);
        [Tooltip("Multiplies a control's colours while it's selected (by navigation, or chosen)")]
        [SerializeField] private Color selectedTint = new(1.1f, 1.1f, 1.1f, 1f);
        [Tooltip("Multiplies a control's colours while it's pressed")]
        [SerializeField] private Color pressedTint = new(0.8f, 0.8f, 0.8f, 1f);
        [Tooltip("Multiplies a control's colours while it can't be used (it's hatched too)")]
        [SerializeField] private Color disabledTint = new(0.6f, 0.6f, 0.6f, 0.6f);

        [Header("Icons")]
        [Tooltip("Sprites drawn in place of the library's icons, by name: an artist's set can replace them one at a time")]
        [SerializeField] private List<IconOverride> iconOverrides = new();

        /// <summary>Raised when a theme changes (in the inspector, or SetColor): every shape redraws.</summary>
        public static event Action<UITheme> Changed;

        private static UITheme builtIn;

        /// <summary>The levels' palette, for a shape with no theme to ask (made in memory, never saved).</summary>
        public static UITheme BuiltIn
        {
            get
            {
                if (builtIn == null)
                {
                    builtIn = CreateInstance<UITheme>();
                    builtIn.name = "Built-in UI Theme";
                    builtIn.hideFlags = HideFlags.DontSave;
                }
                return builtIn;
            }
        }

        /// <summary>The theme in use: the UIManager's, else Fallback, else BuiltIn.</summary>
        public static UITheme Current =>
            UIManager.instance != null && UIManager.instance.Theme != null ? UIManager.instance.Theme : Fallback != null ? Fallback : BuiltIn;

        /// <summary>
        /// The theme in use where there's no UI Manager with one (the UI's editor tools set it to the package's, so
        /// prefabs and the gallery are drawn in it); else, without it, BuiltIn.
        /// </summary>
        public static UITheme Fallback { get; set; }

        /// <summary>How far a panel's corners are cut back.</summary>
        public float ChamferSize { get => chamferSize; set { chamferSize = Mathf.Max(0f, value); RaiseChanged(); } }
        /// <summary>How wide an outline is.</summary>
        public float OutlineWidth { get => outlineWidth; set { outlineWidth = Mathf.Max(0f, value); RaiseChanged(); } }
        /// <summary>How far a glow reaches.</summary>
        public float GlowSize { get => glowSize; set { glowSize = Mathf.Max(0f, value); RaiseChanged(); } }
        /// <summary>How strong a glow is at its shape's edge.</summary>
        public float GlowStrength { get => glowStrength; set { glowStrength = Mathf.Clamp01(value); RaiseChanged(); } }
        /// <summary>The font for headers, numerals and buttons, or null.</summary>
        public TMP_FontAsset DisplayFont { get => displayFont; set { displayFont = value; RaiseChanged(); } }
        /// <summary>The font for body text, or null.</summary>
        public TMP_FontAsset BodyFont { get => bodyFont; set { bodyFont = value; RaiseChanged(); } }
        /// <summary>Multiplies a control's colours while it's hovered.</summary>
        public Color HoveredTint => hoveredTint;
        /// <summary>Multiplies a control's colours while it's selected.</summary>
        public Color SelectedTint => selectedTint;
        /// <summary>Multiplies a control's colours while it's pressed.</summary>
        public Color PressedTint => pressedTint;
        /// <summary>Multiplies a control's colours while it can't be used.</summary>
        public Color DisabledTint => disabledTint;

        /// <summary>The states' tints as a Selectable's colour tint (normal: none).</summary>
        public UnityEngine.UI.ColorBlock StateTints => new()
        {
            normalColor = UnityEngine.Color.white,
            highlightedColor = hoveredTint,
            pressedColor = pressedTint,
            selectedColor = selectedTint,
            disabledColor = disabledTint,
            colorMultiplier = 1f,
            fadeDuration = 0.1f,
        };

        /// <summary>The sprite standing in for the named icon, or null (the library draws it).</summary>
        public Sprite IconSprite(string iconName)
        {
            foreach (IconOverride entry in iconOverrides) { if (entry.name == iconName && entry.sprite != null) { return entry.sprite; } }
            return null;
        }

        /// <summary>Sets (or, with null, clears) the sprite standing in for the named icon.</summary>
        public void SetIconSprite(string iconName, Sprite sprite)
        {
            iconOverrides.RemoveAll(entry => entry.name == iconName);
            if (sprite != null) { iconOverrides.Add(new IconOverride { name = iconName, sprite = sprite }); }
            RaiseChanged();
        }

        /// <summary>The role's colour (the levels' palette's, for a role this theme doesn't list).</summary>
        public Color Color(UIColorRole role)
        {
            foreach (RoleColor entry in colors) { if (entry.role == role) { return entry.color; } }
            return DefaultColor(role);
        }

        /// <summary>Sets the role's colour, and tells every shape.</summary>
        public void SetColor(UIColorRole role, Color color)
        {
            int index = colors.FindIndex(entry => entry.role == role);
            if (index >= 0) { colors[index] = new RoleColor { role = role, color = color }; }
            else { colors.Add(new RoleColor { role = role, color = color }); }
            RaiseChanged();
        }

        private void OnValidate() => RaiseChanged();

        /// <summary>Tells every shape this theme has changed (as its setters do): for one made the current theme.</summary>
        public void RaiseChanged() => Changed?.Invoke(this);

        /// <summary>The levels' palette (ReferenceAssets' style sheets): graphite panels, steel lines, the orange and cyan accents.</summary>
        public static Color DefaultColor(UIColorRole role) => role switch
        {
            UIColorRole.Backdrop => Hex(0x1E2126),
            UIColorRole.Panel => Hex(0x2D3138),
            UIColorRole.PanelRaised => Hex(0x3B4049),
            UIColorRole.Outline => Hex(0xA9AFB8),
            UIColorRole.Text => Hex(0xF2F4F6),
            UIColorRole.TextMuted => Hex(0x9AA1AA),
            UIColorRole.TextOnFill => Hex(0x2D3138),
            UIColorRole.Accent => Hex(0xE07A2F),
            UIColorRole.Highlight => Hex(0x54C6D6),
            UIColorRole.Light => Hex(0xF2F4F6),
            UIColorRole.Disabled => Hex(0x5A6069),
            UIColorRole.Positive => Hex(0x54C6D6),
            UIColorRole.Warning => Hex(0xE07A2F),
            UIColorRole.Info => Hex(0xD6D9DD),
            UIColorRole.Danger => Hex(0xE04A2F),
            _ => UnityEngine.Color.magenta,
        };

        private static List<RoleColor> DefaultColors()
        {
            var list = new List<RoleColor>();
            foreach (UIColorRole role in Enum.GetValues(typeof(UIColorRole))) { list.Add(new RoleColor { role = role, color = DefaultColor(role) }); }
            return list;
        }

        private static Color Hex(int rgb) => new((rgb >> 16 & 0xFF) / 255f, (rgb >> 8 & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
    }
}
