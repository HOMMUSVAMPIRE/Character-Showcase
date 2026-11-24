namespace MeridianMotion.UI
{
    /// <summary>
    /// What a colour is for, which a UITheme gives a colour (so retuning the theme restyles every widget). Shapes, text and
    /// icons ask for a role, never a colour of their own.
    /// </summary>
    public enum UIColorRole
    {
        /// <summary>Behind everything: a screen's backdrop, a blocker.</summary>
        Backdrop,
        /// <summary>A panel's or a button's body.</summary>
        Panel,
        /// <summary>A panel inside a panel, an empty bar or track.</summary>
        PanelRaised,
        /// <summary>The thin steel line round a panel.</summary>
        Outline,
        /// <summary>Text on a panel.</summary>
        Text,
        /// <summary>Lesser text: hints, captions.</summary>
        TextMuted,
        /// <summary>Text on a light or accent fill (a primary button's label).</summary>
        TextOnFill,
        /// <summary>The main accent: headers' tabs, the primary button, what's selected (the levels' orange).</summary>
        Accent,
        /// <summary>The second accent: progress, what's active (the levels' cyan).</summary>
        Highlight,
        /// <summary>A light fill: a secondary button, a gloss.</summary>
        Light,
        /// <summary>What can't be used.</summary>
        Disabled,
        /// <summary>Success, done, a check.</summary>
        Positive,
        /// <summary>A warning.</summary>
        Warning,
        /// <summary>Information.</summary>
        Info,
        /// <summary>Danger, an error.</summary>
        Danger,
    }
}
