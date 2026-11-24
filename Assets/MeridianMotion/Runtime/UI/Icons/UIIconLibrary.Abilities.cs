using UnityEngine;

namespace MeridianMotion.UI
{
    public static partial class UIIconLibrary
    {
        // The ability set: simple figures (a dot for the head, strokes for the limbs) on the 24-unit grid, y up, facing right.

        private static void DrawGrab(UIIconPainter p)
        {
            p.Box(6.5f, 3f, 17.5f, 11.5f);
            p.Line(Limb, V(8f, 11f), V(8f, 18.5f));
            p.Line(Limb, V(11f, 11f), V(11f, 20.5f));
            p.Line(Limb, V(14f, 11f), V(14f, 20f));
            p.Line(Limb, V(17f, 11f), V(17f, 17.5f));
            p.Line(Limb, V(7f, 8.5f), V(3.5f, 13f));
        }

        private static void DrawPush(UIIconPainter p)
        {
            p.Frame(15f, 3f, 21.5f, 12f, Stroke);
            p.Dot(V(7.5f, 18.5f), HeadRadius);
            p.Line(Limb, V(8f, 15.5f), V(10.5f, 10f));
            p.Line(Stroke, V(8.75f, 14f), V(13.75f, 9.5f));
            p.Line(Limb, V(10.5f, 10f), V(12.5f, 3f));
            p.Line(Limb, V(10.5f, 10f), V(6.5f, 6.5f), V(5f, 3f));
        }

        private static void DrawLadder(UIIconPainter p)
        {
            p.Line(Stroke, V(7f, 2f), V(7f, 22f));
            p.Line(Stroke, V(17f, 2f), V(17f, 22f));
            for (float y = 6f; y <= 18f; y += 4f) { p.Line(Stroke, V(7f, y), V(17f, y)); }
        }

        private static void DrawRun(UIIconPainter p)
        {
            p.Dot(V(14.5f, 19.5f), HeadRadius);
            p.Line(Limb, V(13.5f, 16.5f), V(11f, 10.5f));
            p.Line(Stroke, V(13f, 15f), V(16.5f, 12.5f), V(19f, 14f));
            p.Line(Stroke, V(13f, 15f), V(9.5f, 14f), V(7.5f, 11f));
            p.Line(Limb, V(11f, 10.5f), V(14.5f, 7.5f), V(13.5f, 3f));
            p.Line(Limb, V(11f, 10.5f), V(8f, 6.5f), V(4f, 5.5f));
        }

        private static void DrawSprint(UIIconPainter p)
        {
            p.Dot(V(17f, 18.5f), HeadRadius);
            p.Line(Limb, V(15.5f, 15.75f), V(11f, 11f));
            p.Line(Stroke, V(14.5f, 14.5f), V(18.5f, 12f), V(20.5f, 14f));
            p.Line(Stroke, V(14.5f, 14.5f), V(10.5f, 15.5f), V(8.5f, 13f));
            p.Line(Limb, V(11f, 11f), V(15.5f, 8f), V(15.5f, 3f));
            p.Line(Limb, V(11f, 11f), V(7.5f, 7.5f), V(3f, 7.5f));
            p.Line(Fine, V(2f, 18f), V(7f, 18f));
            p.Line(Fine, V(3f, 14f), V(6f, 14f));
        }

        private static void DrawZipline(UIIconPainter p)
        {
            p.Line(Fine, V(2f, 21.5f), V(22f, 17f));
            p.Line(Stroke, V(12f, 19.25f), V(11f, 13.5f));
            p.Dot(V(14f, 13.5f), HeadRadius);
            p.Line(Limb, V(11.5f, 11f), V(11f, 6f));
            p.Line(Limb, V(11f, 6f), V(14f, 3f));
            p.Line(Limb, V(11f, 6f), V(8f, 3f));
        }

        private static void DrawRope(UIIconPainter p)
        {
            p.Line(Fine, V(16f, 22f), V(16f, 2f));
            p.Dot(V(10.5f, 17.5f), HeadRadius);
            p.Line(Stroke, V(11.5f, 14.5f), V(14.25f, 19f));
            p.Line(Stroke, V(11.5f, 13.75f), V(14.25f, 12.5f));
            p.Line(Limb, V(11.25f, 14.5f), V(10.5f, 8.5f));
            p.Line(Limb, V(10.5f, 8.5f), V(14.25f, 6.25f));
            p.Line(Limb, V(10.5f, 8.5f), V(9f, 3f));
        }

        private static void DrawWallRun(UIIconPainter p)
        {
            p.Line(Limb, V(21f, 2f), V(21f, 22f));
            p.Dot(V(12f, 19f), HeadRadius);
            p.Line(Limb, V(11.5f, 16f), V(9.5f, 10.5f));
            p.Line(Stroke, V(11f, 14.75f), V(7f, 15.75f));
            p.Line(Stroke, V(11f, 14.75f), V(14.75f, 12.75f));
            p.Line(Limb, V(9.5f, 10.5f), V(14.5f, 10f), V(19.25f, 13f));
            p.Line(Limb, V(9.5f, 10.5f), V(8f, 6f), V(3.5f, 5f));
        }

        private static void DrawDash(UIIconPainter p)
        {
            p.Line(Bold, V(4f, 7f), V(9.5f, 12.5f), V(4f, 18f));
            p.Line(Bold, V(11f, 7f), V(16.5f, 12.5f), V(11f, 18f));
            p.Line(Stroke, V(4f, 3f), V(20f, 3f));
        }

        private static void DrawCrouch(UIIconPainter p)
        {
            p.Line(Fine, V(3f, 17.5f), V(21f, 17.5f));
            p.Dot(V(14f, 12.5f), HeadRadius);
            p.Line(Limb, V(12.75f, 10f), V(8.5f, 8f));
            p.Line(Stroke, V(12f, 9.75f), V(16f, 8f));
            p.Line(Limb, V(8.5f, 8f), V(13.5f, 6.5f), V(13f, 3f));
            p.Line(Limb, V(8.5f, 8f), V(6f, 5f), V(7.5f, 3f));
            p.Line(Fine, V(3f, 2f), V(21f, 2f));
        }

        private static void DrawSlide(UIIconPainter p)
        {
            p.Dot(V(17.5f, 10.5f), HeadRadius);
            p.Line(Limb, V(15f, 9f), V(9f, 6.5f));
            p.Line(Stroke, V(14f, 8.5f), V(18.5f, 5.5f));
            p.Line(Limb, V(9f, 6.5f), V(3f, 4.5f));
            p.Line(Limb, V(9f, 6.5f), V(5.5f, 8.5f));
            p.Line(Fine, V(2f, 2.5f), V(22f, 2.5f));
        }

        private static void DrawRoll(UIIconPainter p)
        {
            p.Dot(V(12f, 12f), 2.5f);
            p.Arc(V(12f, 12f), 7.5f, Limb, 120f, -300f);
            p.Poly(V(1.25f, 11f), V(7.75f, 11f), V(4.5f, 15.5f));
        }
    }
}
