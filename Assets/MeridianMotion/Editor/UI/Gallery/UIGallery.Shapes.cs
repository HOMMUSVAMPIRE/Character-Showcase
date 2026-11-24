using MeridianMotion.UI;
using UnityEngine;

namespace MeridianMotion.UI.EditorTools
{
    public static partial class UIGallery
    {
        // The shapes (H1): boxes, buttons in each colour and state, bars, diamonds, rings and the trim.
        private static void ShapesPage(GalleryPage page)
        {
            UITheme theme = page.Theme;

            page.Row("Panels");
            page.Add<ChamferedBox>("Panel", new Vector2(240f, 140f));
            ChamferedBox tabbed = page.Add<ChamferedBox>("Header tab", new Vector2(240f, 140f));
            tabbed.HeaderTab = true;
            ChamferedBox two = page.Add<ChamferedBox>("Two corners cut", new Vector2(240f, 140f));
            two.Corners = UIShapeMesh.Corners.TopLeft | UIShapeMesh.Corners.BottomRight;
            ChamferedBox raised = page.Add<ChamferedBox>("Raised, no outline", new Vector2(240f, 140f));
            raised.FillRole = UIColorRole.PanelRaised;
            raised.Outline = false;
            ChamferedBox frame = page.Add<ChamferedBox>("Outline only", new Vector2(240f, 140f));
            frame.Fill = false;

            page.Row("Buttons");
            Button(page, "Accent", "CONFIRM", UIColorRole.Accent, UIColorRole.TextOnFill, outline: null);
            Button(page, "Light", "CONTINUE", UIColorRole.Light, UIColorRole.TextOnFill, outline: null);
            Button(page, "Highlight", "SCAN", UIColorRole.Highlight, UIColorRole.TextOnFill, outline: null);
            Button(page, "Panel, accent outline", "OPTIONS", UIColorRole.Panel, UIColorRole.Text, outline: UIColorRole.Accent);
            ChamferedBox disabled = Button(page, "Disabled (hatched)", "LOCKED", UIColorRole.Disabled, UIColorRole.TextMuted, outline: null);
            StripeFill hatch = page.Shape<StripeFill>((RectTransform)disabled.transform);
            hatch.FillRole = UIColorRole.PanelRaised;
            hatch.StripeWidth = 3f;
            hatch.Gap = 6f;
            hatch.Corners = UIShapeMesh.Corners.All;
            hatch.transform.SetSiblingIndex(0);

            page.Row("States");
            (string name, Color tint)[] states =
            {
                ("Normal", Color.white), ("Hovered", theme.HoveredTint), ("Pressed", theme.PressedTint),
                ("Selected", theme.SelectedTint), ("Disabled", theme.DisabledTint),
            };
            foreach ((string name, Color tint) in states)
            {
                ChamferedBox state = Button(page, name, name.ToUpperInvariant(), UIColorRole.Accent, UIColorRole.TextOnFill, outline: null);
                state.color = tint;
            }

            page.Row("Bars");
            foreach (float fraction in new[] { 0f, 0.5f, 1f })
            {
                SkewedBar bar = page.Add<SkewedBar>($"Continuous, {fraction:0.#}", new Vector2(220f, 22f));
                bar.Segments = 1;
                bar.Fraction = fraction;
                bar.Outline = false;
                bar.FillRole = UIColorRole.Highlight;
            }
            SkewedBar segments = page.Add<SkewedBar>("Segmented, 0.6", new Vector2(220f, 22f));
            segments.Fraction = 0.6f;
            segments.Outline = false;
            segments.FillRole = UIColorRole.Accent;
            SkewedBar pips = page.Add<SkewedBar>("Pips, 0.5", new Vector2(200f, 18f));
            pips.Segments = 8;
            pips.Fraction = 0.5f;
            pips.Outline = false;
            pips.FillRole = UIColorRole.Light;

            page.Row("Markers");
            page.Add<DiamondShape>("Diamond", new Vector2(48f, 48f)).FillRole = UIColorRole.Accent;
            DiamondShape hollow = page.Add<DiamondShape>("Outlined", new Vector2(48f, 48f));
            hollow.Fill = false;
            DiamondShape glowing = page.Add<DiamondShape>("Glowing", new Vector2(48f, 48f));
            glowing.FillRole = UIColorRole.Highlight;
            glowing.Outline = false;
            glowing.Glow = true;
            foreach (float fraction in new[] { 0f, 0.25f, 0.5f, 1f })
            {
                RingShape ring = page.Add<RingShape>($"Ring, {fraction:0.##}", new Vector2(72f, 72f));
                ring.Fraction = fraction;
            }

            page.Row("Trim");
            RectTransform callout = page.Add("Hazard stripes", new Vector2(260f, 64f));
            page.Shape<ChamferedBox>(callout).FillRole = UIColorRole.Panel;
            StripeFill hazard = page.Shape<StripeFill>(callout);
            hazard.FillRole = UIColorRole.Accent;
            hazard.Corners = UIShapeMesh.Corners.All;
            CornerBrackets brackets = page.Add<CornerBrackets>("Corner brackets", new Vector2(140f, 90f));
            brackets.FillRole = UIColorRole.Light;
            Divider divider = page.Add<Divider>("Divider", new Vector2(360f, 12f));
            divider.FillRole = UIColorRole.Outline;
        }

        // A long chamfered button in fill, its text in text, outlined in outline (none: no outline), with the "//" gloss.
        private static ChamferedBox Button(GalleryPage page, string caption, string text, UIColorRole fill, UIColorRole textRole, UIColorRole? outline)
        {
            RectTransform slot = page.Add(caption, new Vector2(240f, 56f));
            ChamferedBox box = page.Shape<ChamferedBox>(slot);
            box.FillRole = fill;
            box.Outline = outline.HasValue;
            if (outline.HasValue) { box.OutlineRole = outline.Value; }
            box.Gloss = fill != UIColorRole.Disabled;
            page.Label(slot, text, 22f, textRole);
            return box;
        }
    }
}
