using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MeridianMotion.UI
{
    /// <summary>
    /// What an icon is drawn with (UIIconLibrary): strokes with round caps and joins, filled convex polygons, dots, rings
    /// and arcs, given on the icons' 24-unit grid (0,0 its bottom left, y up) and drawn into a rect, scaled to fit it and
    /// centred, in one colour, their edges feathered as the shapes' are.
    /// </summary>
    public sealed class UIIconPainter
    {
        /// <summary>The grid's size: an icon's coordinates run from 0 to this.</summary>
        public const float GridSize = 24f;

        private readonly VertexHelper vh;
        private readonly Vector2 origin;
        private readonly float scale;
        private readonly Color color;
        private readonly float feather;

        /// <summary>A painter drawing into vh, the grid fitted to rect, in color, its edges feathered over feather.</summary>
        public UIIconPainter(VertexHelper vh, Rect rect, Color color, float feather)
        {
            this.vh = vh;
            this.color = color;
            this.feather = feather;
            scale = Mathf.Min(rect.width, rect.height) / GridSize;
            origin = rect.center - new Vector2(GridSize, GridSize) * (scale / 2f);
        }

        /// <summary>A stroke through the points, width wide, its ends and corners round.</summary>
        public void Line(float width, params Vector2[] points) => Stroke(width, closed: false, points);

        /// <summary>A closed stroke through the points (an outline), width wide, its corners round.</summary>
        public void Loop(float width, params Vector2[] points) => Stroke(width, closed: true, points);

        /// <summary>A filled convex polygon (its points in either order round it).</summary>
        public void Poly(params Vector2[] points)
        {
            var polygon = new List<Vector2>(points.Length);
            foreach (Vector2 point in points) { polygon.Add(At(point)); }
            if (SignedArea(polygon) < 0f) { polygon.Reverse(); }
            UIShapeMesh.AddFill(vh, polygon, color, feather);
        }

        /// <summary>A filled rect, between two corners.</summary>
        public void Box(float x0, float y0, float x1, float y1) => Poly(new Vector2(x0, y0), new Vector2(x1, y0), new Vector2(x1, y1), new Vector2(x0, y1));

        /// <summary>A rect's outline, between two corners, width wide.</summary>
        public void Frame(float x0, float y0, float x1, float y1, float width) =>
            Loop(width, new Vector2(x0, y0), new Vector2(x1, y0), new Vector2(x1, y1), new Vector2(x0, y1));

        /// <summary>A filled circle.</summary>
        public void Dot(Vector2 center, float radius) => UIShapeMesh.AddArc(vh, At(center), 0f, radius * scale, 0f, 360f, color, feather);

        /// <summary>A circle's outline, width wide (centred on the radius).</summary>
        public void Circle(Vector2 center, float radius, float width) => Arc(center, radius, width, 0f, 360f);

        /// <summary>An arc of a circle, width wide (centred on the radius), from startDegrees (0: right) through sweepDegrees (negative: clockwise), its ends round.</summary>
        public void Arc(Vector2 center, float radius, float width, float startDegrees, float sweepDegrees)
        {
            float half = width / 2f;
            UIShapeMesh.AddArc(vh, At(center), (radius - half) * scale, (radius + half) * scale, startDegrees, sweepDegrees, color, feather);
            if (Mathf.Abs(sweepDegrees) >= 360f) { return; }
            Dot(center + Direction(startDegrees) * radius, half);
            Dot(center + Direction(startDegrees + sweepDegrees) * radius, half);
        }

        /// <summary>Points round part of an ellipse (for a stroke): from startDegrees through sweepDegrees, a point about every 15 degrees.</summary>
        public static Vector2[] Ellipse(Vector2 center, float radiusX, float radiusY, float startDegrees, float sweepDegrees)
        {
            int steps = Mathf.Max(2, Mathf.CeilToInt(Mathf.Abs(sweepDegrees) / 15f));
            var points = new Vector2[steps + 1];
            for (int i = 0; i <= steps; i++)
            {
                Vector2 direction = Direction(startDegrees + sweepDegrees * i / steps);
                points[i] = center + new Vector2(direction.x * radiusX, direction.y * radiusY);
            }
            return points;
        }

        /// <summary>A point turned by degrees (anticlockwise) about the grid's centre.</summary>
        public static Vector2 Turned(Vector2 point, float degrees)
        {
            Vector2 centre = new(GridSize / 2f, GridSize / 2f), offset = point - centre;
            float radians = degrees * Mathf.Deg2Rad, cos = Mathf.Cos(radians), sin = Mathf.Sin(radians);
            return centre + new Vector2(offset.x * cos - offset.y * sin, offset.x * sin + offset.y * cos);
        }

        /// <summary>Points turned by degrees about the grid's centre.</summary>
        public static Vector2[] Turned(float degrees, params Vector2[] points)
        {
            var turned = new Vector2[points.Length];
            for (int i = 0; i < points.Length; i++) { turned[i] = Turned(points[i], degrees); }
            return turned;
        }

        private static Vector2 Direction(float degrees) => new(Mathf.Cos(degrees * Mathf.Deg2Rad), Mathf.Sin(degrees * Mathf.Deg2Rad));

        private Vector2 At(Vector2 grid) => origin + grid * scale;

        // Each segment a quad, each point a round cap or join (a dot of the stroke's width).
        private void Stroke(float width, bool closed, Vector2[] points)
        {
            if (points.Length == 0) { return; }
            float half = width / 2f * scale;
            int segments = closed ? points.Length : points.Length - 1;
            for (int i = 0; i < segments; i++)
            {
                Vector2 a = At(points[i]), b = At(points[(i + 1) % points.Length]);
                Vector2 along = b - a;
                if (along.sqrMagnitude < 1e-6f) { continue; }
                Vector2 normal = new Vector2(-along.y, along.x).normalized * half;
                UIShapeMesh.AddFill(vh, new List<Vector2> { a - normal, b - normal, b + normal, a + normal }, color, feather);
            }
            foreach (Vector2 point in points) { UIShapeMesh.AddArc(vh, At(point), 0f, half, 0f, 360f, color, feather); }
        }

        private static float SignedArea(List<Vector2> polygon)
        {
            float area = 0f;
            for (int i = 0; i < polygon.Count; i++)
            {
                Vector2 a = polygon[i], b = polygon[(i + 1) % polygon.Count];
                area += a.x * b.y - b.x * a.y;
            }
            return area;
        }
    }
}
