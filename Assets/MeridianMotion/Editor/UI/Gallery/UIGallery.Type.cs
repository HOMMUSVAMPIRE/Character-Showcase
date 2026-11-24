using MeridianMotion.UI;
using TMPro;
using UnityEngine;

namespace MeridianMotion.UI.EditorTools
{
    public static partial class UIGallery
    {
        // The theme's type (H3): headers, numerals and buttons in its display font, body text in its body font.
        private static void TypePage(GalleryPage page)
        {
            page.Row("Headers");
            page.Words(page.Add("Window title", new Vector2(560f, 64f)), "MISSION BRIEFING", 56f, UIColorRole.Text, true, TextAlignmentOptions.Left);
            page.Words(page.Add("Section", new Vector2(360f, 64f)), "COURSE RECORDS", 32f, UIColorRole.Accent, true, TextAlignmentOptions.Left);
            page.Words(page.Add("Small caps", new Vector2(360f, 64f)), "OBJECTIVE  //  ZONE B-04", 22f, UIColorRole.TextMuted, true, TextAlignmentOptions.Left);

            page.Row("Numerals");
            page.Words(page.Add("Digits", new Vector2(620f, 96f)), "0123456789", 88f, UIColorRole.Text, true, TextAlignmentOptions.Left);
            page.Words(page.Add("Lap time", new Vector2(320f, 96f)), "01:23.45", 72f, UIColorRole.Accent, true, TextAlignmentOptions.Left);
            page.Words(page.Add("Counter", new Vector2(200f, 96f)), "x12", 56f, UIColorRole.Highlight, true, TextAlignmentOptions.Left);

            page.Row("Buttons");
            foreach ((string text, UIColorRole fill, UIColorRole textRole) in new[]
            {
                ("RESUME", UIColorRole.Accent, UIColorRole.TextOnFill), ("CONTROLS", UIColorRole.Light, UIColorRole.TextOnFill),
                ("RESTART", UIColorRole.Highlight, UIColorRole.TextOnFill), ("QUIT", UIColorRole.Panel, UIColorRole.Text),
            })
            {
                RectTransform slot = page.Add(text.ToLowerInvariant(), new Vector2(260f, 56f));
                ChamferedBox box = page.Shape<ChamferedBox>(slot);
                box.FillRole = fill;
                box.Outline = fill == UIColorRole.Panel;
                box.OutlineRole = UIColorRole.Accent;
                box.Gloss = true;
                page.Label(slot, text, 26f, textRole);
            }

            page.Row("Body");
            page.Words(page.Add("Body text", new Vector2(900f, 120f)),
                "Reach the relay tower before the timer runs out. Climb the painted walls, swing across the gap on the rope, and " +
                "ride the zipline down to the yard. Press Back at any time to pause.",
                24f, UIColorRole.Text, false, TextAlignmentOptions.TopLeft);
            page.Words(page.Add("Muted", new Vector2(520f, 120f)), "Best lap 01:18.92 · Attempts 7 · Last played 2 days ago", 20f,
                UIColorRole.TextMuted, false, TextAlignmentOptions.TopLeft);
        }
    }
}
