using UnityEngine;
using UnityEngine.UI;

namespace MeridianMotion.UI
{
    /// <summary>
    /// A button in the theme's look (ReferenceAssets/OpenTasks.md, H5): a chamfered shape (its Shape, its target graphic)
    /// coloured by its Kind, its label's colour to suit, tinted in each state by the theme's state tints, and hatched (its
    /// Hatch shown) while it can't be used. Kept so when the theme changes. Its click is a Button's (onClick).
    /// </summary>
    [AddComponentMenu("Meridian Motion/UI/Themed Button")]
    public class ThemedButton : Button
    {
        /// <summary>
        /// A button's kind: Primary (the accent: the main choice), Secondary (light), Highlight (the highlight colour) or
        /// Outline (a panel outlined in the accent: the lesser choices).
        /// </summary>
        public enum Kind { Primary, Secondary, Highlight, Outline }

        /// <summary>A state to show it in (the gallery's previews).</summary>
        public enum PreviewState { Normal, Hovered, Pressed, Selected, Disabled }

        [Tooltip("Its colours: Primary (accent), Secondary (light), Highlight, or Outline (a panel outlined in the accent)")]
        [SerializeField] private Kind kind = Kind.Primary;
        [Tooltip("Its shape, coloured by its kind and tinted by its state")]
        [SerializeField] private ChamferedBox shape;
        [Tooltip("The hatching shown over it while it can't be used (optional)")]
        [SerializeField] private StripeFill hatch;
        [Tooltip("Its label, coloured to suit its kind (optional)")]
        [SerializeField] private ThemedText label;
        [Tooltip("Whether its shape has the \"//\" gloss at its left (for long buttons: it'd run into a short one's label)")]
        [SerializeField] private bool gloss;

        /// <summary>Its kind.</summary>
        public Kind ButtonKind { get => kind; set { kind = value; ApplyTheme(); } }
        /// <summary>Its shape.</summary>
        public ChamferedBox Shape { get => shape; set { shape = value; ApplyTheme(); } }
        /// <summary>The hatching shown while it can't be used.</summary>
        public StripeFill Hatch { get => hatch; set { hatch = value; ApplyTheme(); } }
        /// <summary>Whether its shape has the gloss.</summary>
        public bool Gloss { get => gloss; set { gloss = value; ApplyTheme(); } }
        /// <summary>Its label.</summary>
        public ThemedText Label { get => label; set { label = value; ApplyTheme(); } }

        /// <summary>The fill role its kind has.</summary>
        public static UIColorRole FillRoleOf(Kind kind) => kind switch
        {
            Kind.Primary => UIColorRole.Accent,
            Kind.Secondary => UIColorRole.Light,
            Kind.Highlight => UIColorRole.Highlight,
            _ => UIColorRole.Panel,
        };

        /// <summary>Sets its shape's and label's roles, and its state tints, from its kind and the theme.</summary>
        public void ApplyTheme()
        {
            UITheme theme = shape != null ? shape.ThemeInUse : UITheme.Current;
            if (shape != null)
            {
                shape.FillRole = FillRoleOf(kind);
                shape.Outline = kind == Kind.Outline;
                shape.OutlineRole = UIColorRole.Accent;
                shape.Gloss = gloss;
                targetGraphic = shape;
            }
            if (label != null) { label.Role = kind == Kind.Outline ? UIColorRole.Text : UIColorRole.TextOnFill; }
            if (hatch != null) { hatch.FillRole = UIColorRole.PanelRaised; }
            transition = Transition.ColorTint;
            colors = theme.StateTints;
            UIUtility.SetActive(hatch, !IsInteractable());
        }

        /// <summary>Shows it in a state, at once (for a preview: the gallery's).</summary>
        public void ShowState(PreviewState state)
        {
            SelectionState shown = state switch
            {
                PreviewState.Hovered => SelectionState.Highlighted,
                PreviewState.Pressed => SelectionState.Pressed,
                PreviewState.Selected => SelectionState.Selected,
                PreviewState.Disabled => SelectionState.Disabled,
                _ => SelectionState.Normal,
            };
            DoStateTransition(shown, true);
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            UITheme.Changed += OnThemeChanged;
            ApplyTheme();
        }

        protected override void OnDisable()
        {
            UITheme.Changed -= OnThemeChanged;
            base.OnDisable();
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            ApplyTheme();
        }
#endif

        protected override void DoStateTransition(SelectionState state, bool instant)
        {
            base.DoStateTransition(state, instant);
            UIUtility.SetActive(hatch, state == SelectionState.Disabled);
        }

        private void OnThemeChanged(UITheme changed)
        {
            if (this != null) { ApplyTheme(); }
        }
    }
}
