using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MeridianMotion.UI
{
    /// <summary>
    /// The geometry the UI's shapes are built from, into a VertexHelper: filled convex polygons and outline bands with a
    /// feathered (anti-aliased) edge, arcs for rings, and clipping a polygon to a convex one (stripes inside a panel).
    /// Polygons are counter-clockwise, in the rect's space (y up). The feather is a band, about a pixel wide, whose outer
    /// edge is transparent: the shapes need no texture to be smooth.
    /// </summary>
    public static class UIShapeMesh
    {
        /// <summary>Which corners of a rect are cut back.</summary>
        [System.Flags]
        public enum Corners
        {
            None = 0,
            TopLeft = 1,
            TopRight = 2,
            BottomRight = 4,
            BottomLeft = 8,
            All = TopLeft | TopRight | BottomRight | BottomLeft,
        }

        /// <summary>A rect's outline with the given corners cut back by chamfer at 45 degrees (counter-clockwise from the bottom left).</summary>
        public static List<Vector2> ChamferedRect(Rect rect, float chamfer, Corners corners)
        {
            float c = Mathf.Max(0f, Mathf.Min(chamfer, rect.width / 2f, rect.height / 2f));
            var points = new List<Vector2>(8);
            void Corner(Vector2 corner, Vector2 towardsPrevious, Vector2 towardsNext, bool cut)
            {
                if (cut && c > 0f)
                {
                    points.Add(corner + towardsPrevious * c);
                    points.Add(corner + towardsNext * c);
                }
                else { points.Add(corner); }
            }
            Corner(new Vector2(rect.xMin, rect.yMin), Vector2.up, Vector2.right, (corners & Corners.BottomLeft) != 0);
            Corner(new Vector2(rect.xMax, rect.yMin), Vector2.left, Vector2.up, (corners & Corners.BottomRight) != 0);
            Corner(new Vector2(rect.xMax, rect.yMax), Vector2.down, Vector2.left, (corners & Corners.TopRight) != 0);
            Corner(new Vector2(rect.xMin, rect.yMax), Vector2.right, Vector2.down, (corners & Corners.TopLeft) != 0);
            return points;
        }

        /// <summary>
        /// A convex polygon offset outward by distance (inward, if negative): each corner moved along its mitre, so the
        /// edges move by the distance (a very sharp corner's mitre is limited).
        /// </summary>
        public static List<Vector2> Offset(IReadOnlyList<Vector2> points, float distance)
        {
            int n = points.Count;
            var offset = new List<Vector2>(n);
            for (int i = 0; i < n; i++)
            {
                Vector2 previous = points[(i - 1 + n) % n], current = points[i], next = points[(i + 1) % n];
                Vector2 into = (current - previous).normalized, outOf = (next - current).normalized;
                Vector2 normalIn = new(into.y, -into.x), normalOut = new(outOf.y, -outOf.x);
                Vector2 mitre = (normalIn + normalOut).normalized;
                if (mitre == Vector2.zero) { mitre = normalIn; }
                float along = Mathf.Max(Vector2.Dot(mitre, normalIn), 0.25f);
                offset.Add(current + mitre * (distance / along));
            }
            return offset;
        }

        /// <summary>Fills a convex polygon, its edge feathered over feather (canvas units; none: hard).</summary>
        public static void AddFill(VertexHelper vh, IReadOnlyList<Vector2> polygon, Color color, float feather)
        {
            if (polygon == null || polygon.Count < 3 || color.a <= 0f) { return; }
            IReadOnlyList<Vector2> inner = feather > 0f ? Offset(polygon, -feather / 2f) : polygon;
            int start = vh.currentVertCount;
            foreach (Vector2 point in inner) { vh.AddVert(point, color, Vector4.zero); }
            for (int i = 1; i < inner.Count - 1; i++) { vh.AddTriangle(start, start + i, start + i + 1); }
            if (feather > 0f) { AddBand(vh, inner, color, Offset(polygon, feather / 2f), Clear(color)); }
        }

        /// <summary>An outline of width just inside a convex polygon's edge, both its edges feathered.</summary>
        public static void AddOutline(VertexHelper vh, IReadOnlyList<Vector2> polygon, float width, Color color, float feather)
        {
            if (polygon == null || polygon.Count < 3 || width <= 0f || color.a <= 0f) { return; }
            float f = Mathf.Min(feather, width) / 2f;
            List<Vector2> outer = Offset(polygon, f), outerSolid = Offset(polygon, -f);
            List<Vector2> innerSolid = Offset(polygon, -width + f), inner = Offset(polygon, -width - f);
            if (feather > 0f) { AddBand(vh, outerSolid, color, outer, Clear(color)); }
            AddBand(vh, innerSolid, color, outerSolid, color);
            if (feather > 0f) { AddBand(vh, inner, Clear(color), innerSolid, color); }
        }

        /// <summary>A glow round a convex polygon: from strength at its edge to nothing at reach.</summary>
        public static void AddGlow(VertexHelper vh, IReadOnlyList<Vector2> polygon, float reach, Color color)
        {
            if (polygon == null || polygon.Count < 3 || reach <= 0f || color.a <= 0f) { return; }
            AddBand(vh, polygon, color, Offset(polygon, reach), Clear(color));
        }

        /// <summary>The band between two polygons of as many corners (inner and outer), each edge its own colour.</summary>
        public static void AddBand(VertexHelper vh, IReadOnlyList<Vector2> inner, Color innerColor, IReadOnlyList<Vector2> outer, Color outerColor)
        {
            int n = Mathf.Min(inner.Count, outer.Count);
            if (n < 2) { return; }
            int start = vh.currentVertCount;
            for (int i = 0; i < n; i++)
            {
                vh.AddVert(inner[i], innerColor, Vector4.zero);
                vh.AddVert(outer[i], outerColor, Vector4.zero);
            }
            for (int i = 0; i < n; i++)
            {
                int a = start + i * 2, b = start + (i + 1) % n * 2;
                vh.AddTriangle(a, a + 1, b + 1);
                vh.AddTriangle(a, b + 1, b);
            }
        }

        /// <summary>
        /// An arc of a ring, between radii inner and outer, from startDegrees (0: right, counter-clockwise) through
        /// sweepDegrees (negative: clockwise), its round edges feathered. A full sweep (360 or more) closes it.
        /// </summary>
        public static void AddArc(VertexHelper vh, Vector2 center, float inner, float outer, float startDegrees, float sweepDegrees, Color color, float feather)
        {
            if (outer <= inner || Mathf.Abs(sweepDegrees) < 0.01f || color.a <= 0f) { return; }
            float sweep = Mathf.Clamp(sweepDegrees, -360f, 360f);
            int steps = Mathf.Max(2, Mathf.CeilToInt(Mathf.Abs(sweep) / 4f));
            float f = Mathf.Min(feather, (outer - inner) / 2f) / 2f;
            float[] radii = { outer + f, outer - f, inner + f, Mathf.Max(0f, inner - f) };
            Color[] colors = { Clear(color), color, color, inner > 0f ? Clear(color) : color };
            int start = vh.currentVertCount;
            for (int s = 0; s <= steps; s++)
            {
                float angle = (startDegrees + sweep * s / steps) * Mathf.Deg2Rad;
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                for (int r = 0; r < radii.Length; r++) { vh.AddVert(center + direction * radii[r], colors[r], Vector4.zero); }
            }
            for (int s = 0; s < steps; s++)
            {
                int a = start + s * radii.Length, b = a + radii.Length;
                for (int r = 0; r < radii.Length - 1; r++)
                {
                    vh.AddTriangle(a + r, b + r, b + r + 1);
                    vh.AddTriangle(a + r, b + r + 1, a + r + 1);
                }
            }
        }

        /// <summary>A polygon clipped to a convex one (both counter-clockwise): what of it is inside (Sutherland-Hodgman).</summary>
        public static List<Vector2> Clip(IReadOnlyList<Vector2> subject, IReadOnlyList<Vector2> convex)
        {
            var output = new List<Vector2>(subject);
            for (int e = 0; e < convex.Count && output.Count > 0; e++)
            {
                Vector2 a = convex[e], b = convex[(e + 1) % convex.Count];
                var input = output;
                output = new List<Vector2>(input.Count + 2);
                for (int i = 0; i < input.Count; i++)
                {
                    Vector2 current = input[i], previous = input[(i - 1 + input.Count) % input.Count];
                    bool currentIn = Side(a, b, current) >= 0f, previousIn = Side(a, b, previous) >= 0f;
                    if (currentIn)
                    {
                        if (!previousIn) { output.Add(Intersect(previous, current, a, b)); }
                        output.Add(current);
                    }
                    else if (previousIn) { output.Add(Intersect(previous, current, a, b)); }
                }
            }
            return output;
        }

        /// <summary>A parallelogram: its bottom edge from x0 to x1 at y0, its top shifted right by slant at y1.</summary>
        public static List<Vector2> Parallelogram(float x0, float x1, float y0, float y1, float slant) =>
            new() { new Vector2(x0, y0), new Vector2(x1, y0), new Vector2(x1 + slant, y1), new Vector2(x0 + slant, y1) };

        /// <summary>The diamond inscribed in a rect (counter-clockwise from its bottom).</summary>
        public static List<Vector2> Diamond(Rect rect) =>
            new() { new Vector2(rect.center.x, rect.yMin), new Vector2(rect.xMax, rect.center.y), new Vector2(rect.center.x, rect.yMax), new Vector2(rect.xMin, rect.center.y) };

        /// <summary>A rect's four corners, counter-clockwise from the bottom left.</summary>
        public static List<Vector2> Corners4(Rect rect) =>
            new() { new Vector2(rect.xMin, rect.yMin), new Vector2(rect.xMax, rect.yMin), new Vector2(rect.xMax, rect.yMax), new Vector2(rect.xMin, rect.yMax) };

        private static Color Clear(Color color) => new(color.r, color.g, color.b, 0f);

        // Left of a to b (inside, for a counter-clockwise polygon): positive.
        private static float Side(Vector2 a, Vector2 b, Vector2 p) => (b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x);

        private static Vector2 Intersect(Vector2 p, Vector2 q, Vector2 a, Vector2 b)
        {
            float sp = Side(a, b, p), sq = Side(a, b, q);
            float t = Mathf.Approximately(sp, sq) ? 0f : sp / (sp - sq);
            return p + (q - p) * t;
        }
    }
}
