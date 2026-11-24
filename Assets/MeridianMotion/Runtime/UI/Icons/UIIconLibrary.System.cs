using UnityEngine;

namespace MeridianMotion.UI
{
    public static partial class UIIconLibrary
    {
        // The system set: on the 24-unit grid, y up.

        private static void DrawPin(UIIconPainter p)
        {
            p.Circle(V(12f, 15f), 5.5f, Stroke);
            p.Dot(V(12f, 15f), 1.9f);
            p.Line(Stroke, V(7.4f, 12f), V(12f, 3f), V(16.6f, 12f));
        }

        private static void DrawWaypoint(UIIconPainter p)
        {
            p.Loop(Stroke, V(12f, 2.5f), V(21.5f, 12f), V(12f, 21.5f), V(2.5f, 12f));
            p.Poly(V(12f, 7.5f), V(16.5f, 12f), V(12f, 16.5f), V(7.5f, 12f));
        }

        private static void DrawAlert(UIIconPainter p)
        {
            p.Line(Bold + 0.5f, V(12f, 19.5f), V(12f, 10f));
            p.Dot(V(12f, 5f), 1.9f);
        }

        private static void DrawHelp(UIIconPainter p)
        {
            p.Arc(V(12f, 15f), 4.5f, Bold, 165f, -255f);
            p.Line(Bold, V(12f, 10.5f), V(12f, 8.75f));
            p.Dot(V(12f, 4.5f), 1.75f);
        }

        private static void DrawInfo(UIIconPainter p)
        {
            p.Dot(V(12f, 18.5f), 1.9f);
            p.Line(Bold + 0.5f, V(12f, 13.5f), V(12f, 5f));
            p.Line(Stroke, V(9.5f, 13.5f), V(12f, 13.5f));
            p.Line(Stroke, V(9f, 5f), V(15f, 5f));
        }

        private static void DrawPlus(UIIconPainter p)
        {
            p.Line(Bold + 0.5f, V(12f, 4f), V(12f, 20f));
            p.Line(Bold + 0.5f, V(4f, 12f), V(20f, 12f));
        }

        private static void DrawGear(UIIconPainter p)
        {
            p.Circle(V(12f, 12f), 5.75f, 3f);
            p.Dot(V(12f, 12f), 1.5f);
            for (int i = 0; i < 8; i++)
            {
                float angle = i * 45f;
                p.Poly(UIIconPainter.Turned(angle, V(19f, 10.4f), V(21.5f, 10.9f), V(21.5f, 13.1f), V(19f, 13.6f), V(16.5f, 13.6f), V(16.5f, 10.4f)));
            }
        }

        private static void DrawWrench(UIIconPainter p)
        {
            p.Line(Bold + 0.75f, V(5f, 5f), V(13.5f, 13.5f));
            p.Arc(V(16.25f, 16.25f), 3.75f, Bold, 85f, 280f);
        }

        private static void DrawClipboard(UIIconPainter p)
        {
            p.Frame(5f, 3f, 19f, 19.5f, Stroke);
            p.Box(8.5f, 18f, 15.5f, 21.5f);
            p.Line(Stroke, V(8.5f, 14f), V(15.5f, 14f));
            p.Line(Stroke, V(8.5f, 10f), V(15.5f, 10f));
            p.Line(Stroke, V(8.5f, 6f), V(12.5f, 6f));
        }

        private static void DrawDatabase(UIIconPainter p)
        {
            p.Loop(Stroke, UIIconPainter.Ellipse(V(12f, 18f), 7f, 2.75f, 0f, 345f));
            p.Line(Stroke, V(5f, 18f), V(5f, 6f));
            p.Line(Stroke, V(19f, 18f), V(19f, 6f));
            p.Line(Stroke, UIIconPainter.Ellipse(V(12f, 12f), 7f, 2.75f, 180f, 180f));
            p.Line(Stroke, UIIconPainter.Ellipse(V(12f, 6f), 7f, 2.75f, 180f, 180f));
        }

        private static void DrawDownload(UIIconPainter p)
        {
            p.Line(Bold, V(12f, 20.5f), V(12f, 8f));
            p.Line(Bold, V(7f, 12.5f), V(12f, 7.5f), V(17f, 12.5f));
            p.Line(Bold, V(4.5f, 3.5f), V(19.5f, 3.5f));
        }

        private static void DrawLock(UIIconPainter p)
        {
            p.Frame(5f, 3f, 19f, 12.5f, Stroke + 0.25f);
            p.Arc(V(12f, 14.5f), 4.5f, Stroke + 0.25f, 0f, 180f);
            p.Line(Stroke + 0.25f, V(7.5f, 14.5f), V(7.5f, 12.5f));
            p.Line(Stroke + 0.25f, V(16.5f, 14.5f), V(16.5f, 12.5f));
            p.Dot(V(12f, 8.25f), 1.6f);
        }

        private static void DrawUnlock(UIIconPainter p)
        {
            p.Frame(5f, 3f, 19f, 12.5f, Stroke + 0.25f);
            p.Arc(V(12f, 16.5f), 4.5f, Stroke + 0.25f, 0f, 180f);
            p.Line(Stroke + 0.25f, V(7.5f, 16.5f), V(7.5f, 12.5f));
            p.Dot(V(12f, 8.25f), 1.6f);
        }

        private static void DrawElevator(UIIconPainter p)
        {
            p.Frame(4f, 3f, 20f, 21f, Stroke);
            p.Line(Stroke, V(12f, 3f), V(12f, 21f));
            p.Poly(V(5.75f, 12.5f), V(10.25f, 12.5f), V(8f, 16.5f));
            p.Poly(V(13.75f, 11.5f), V(18.25f, 11.5f), V(16f, 7.5f));
        }

        private static void DrawStairs(UIIconPainter p) =>
            p.Line(Limb, V(3.5f, 5f), V(8.5f, 5f), V(8.5f, 10f), V(13.5f, 10f), V(13.5f, 15f), V(18.5f, 15f), V(18.5f, 20f), V(20.5f, 20f));

        private static void DrawDoor(UIIconPainter p)
        {
            p.Line(Stroke, V(6.5f, 3f), V(6.5f, 21f), V(17.5f, 21f), V(17.5f, 3f));
            p.Dot(V(14.75f, 12f), 1.4f);
            p.Line(Stroke, V(3f, 3f), V(21f, 3f));
        }

        private static void DrawCheck(UIIconPainter p) => p.Line(Bold + 0.5f, V(4.5f, 12.5f), V(9.5f, 7f), V(19.5f, 17.5f));

        private static void DrawCross(UIIconPainter p)
        {
            p.Line(Bold + 0.5f, V(6f, 6f), V(18f, 18f));
            p.Line(Bold + 0.5f, V(6f, 18f), V(18f, 6f));
        }

        private static void DrawWarning(UIIconPainter p)
        {
            p.Loop(Stroke + 0.25f, V(12f, 20.75f), V(21.5f, 4f), V(2.5f, 4f));
            p.Line(Bold, V(12f, 14.75f), V(12f, 10f));
            p.Dot(V(12f, 7f), 1.5f);
        }

        private static void DrawCrate(UIIconPainter p)
        {
            p.Loop(Stroke, V(12f, 21.5f), V(20.5f, 17f), V(20.5f, 7f), V(12f, 2.5f), V(3.5f, 7f), V(3.5f, 17f));
            p.Line(Stroke, V(3.5f, 17f), V(12f, 12.5f), V(20.5f, 17f));
            p.Line(Stroke, V(12f, 12.5f), V(12f, 2.5f));
        }

        private static void DrawChevrons(UIIconPainter p)
        {
            p.Line(Bold, V(5f, 5.5f), V(11.5f, 12f), V(5f, 18.5f));
            p.Line(Bold, V(12.5f, 5.5f), V(19f, 12f), V(12.5f, 18.5f));
        }

        // An arrow, turned to point at degrees (0: right).
        private static void DrawArrow(UIIconPainter p, float degrees)
        {
            p.Line(Bold, UIIconPainter.Turned(degrees, V(4f, 12f), V(19.5f, 12f)));
            p.Line(Bold, UIIconPainter.Turned(degrees, V(13f, 5.5f), V(19.5f, 12f), V(13f, 18.5f)));
        }

        // The Meridian emblem: a crystal of three petals on a base.
        private static void DrawEmblem(UIIconPainter p)
        {
            p.Poly(V(12f, 4.5f), V(15f, 12f), V(12f, 21.5f), V(9f, 12f));
            p.Line(Stroke, V(10f, 4.5f), V(5f, 8.5f), V(3.5f, 16.5f), V(7.5f, 12.5f));
            p.Line(Stroke, V(14f, 4.5f), V(19f, 8.5f), V(20.5f, 16.5f), V(16.5f, 12.5f));
            p.Line(Stroke, V(6f, 2.25f), V(18f, 2.25f));
        }
    }
}
