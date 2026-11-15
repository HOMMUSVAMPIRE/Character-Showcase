using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace CharacterShowcase.EditorTools.Level
{
    /// <summary>
    /// Changes to a level spec's pieces, as the level editor makes them: a setting read and set by its parameter's type,
    /// a piece moved (every position it has, not its directions or sizes), and a new piece of any kind laid out where
    /// it's wanted.
    /// </summary>
    public static class LevelSpecEdit
    {
        /// <summary>Settings that are directions, sizes or amounts, not positions: moving a piece leaves them be.</summary>
        private static readonly HashSet<string> NotPositions = new()
        {
            "direction", "facing", "approach", "inward", "outward", "size", "travel", "spin",
        };

        /// <summary>Number settings that are a horizontal position along x or z, or a height.</summary>
        private static readonly HashSet<string> XNames = new() { "x0", "x1" }, ZNames = new() { "z0", "z1" };
        private static readonly HashSet<string> YNames = new() { "y0", "y1", "top", "floor", "height", "bottom", "openTop", "railTop", "hookBottom", "underside" };

        /// <summary>The method a piece entry calls, or null (a "State" entry, or an unknown piece).</summary>
        public static MethodInfo MethodOf(JsonObject entry) =>
            entry.TryGetValue("piece", out object p) && p is string name && LevelSpec.Pieces.TryGetValue(name, out MethodInfo m) ? m : null;

        /// <summary>A setting's value (its default if it's left out), as its parameter's type; null if it can't be read.</summary>
        public static object Get(JsonObject entry, ParameterInfo p)
        {
            if (!entry.TryGetValue(p.Name, out object json)) { return p.HasDefaultValue ? LevelSpec.DefaultOf(p) : null; }
            try { return p.ParameterType == typeof(Transform) ? json : LevelSpec.Decode(json, p.ParameterType, null); }
            catch (Exception) { return null; }
        }

        /// <summary>Sets a setting from a value of its parameter's type (a path string for a parent), keeping the settings in order.</summary>
        public static void Set(JsonObject entry, ParameterInfo p, object value)
        {
            entry[p.Name] = p.ParameterType == typeof(Transform) ? value as string ?? "" : LevelSpec.Encode(value, null);
            Tidy(entry);
        }

        /// <summary>Puts a piece's settings in its parameters' order, after "piece".</summary>
        public static void Tidy(JsonObject entry)
        {
            MethodInfo m = MethodOf(entry);
            if (m != null) { entry.Order(new[] { "piece" }.Concat(m.GetParameters().Select(p => p.Name))); }
        }

        // ---- Moving ----

        /// <summary>
        /// Moves a piece by (dx, dy, dz) in its parent's coordinates: every position it has (corners, points, centres,
        /// outlines, areas, cells, heights), but not its directions, sizes or lengths. Returns whether anything moved.
        /// </summary>
        public static bool Translate(JsonObject entry, float dx, float dy, float dz)
        {
            MethodInfo m = MethodOf(entry);
            if (m == null) { return false; }
            bool moved = false;
            foreach (ParameterInfo p in m.GetParameters())
            {
                if (!entry.TryGetValue(p.Name, out object json) || NotPositions.Contains(p.Name)) { continue; }
                object shifted = Shift(json, p.ParameterType, p.Name, dx, dy, dz);
                if (shifted != null)
                {
                    entry[p.Name] = shifted;
                    moved = true;
                }
            }
            return moved;
        }

        /// <summary>A setting's JSON moved, or null if it isn't a position.</summary>
        private static object Shift(object json, Type t, string name, float dx, float dy, float dz)
        {
            float F(object v) => (float)(double)v;
            if (t == typeof(float))
            {
                if (XNames.Contains(name)) { return (double)(F(json) + dx); }
                if (ZNames.Contains(name)) { return (double)(F(json) + dz); }
                if (YNames.Contains(name)) { return (double)(F(json) + dy); }
                return null;
            }
            if (json is not List<object> list) { return null; }
            if (t == typeof(Vector2) && list.Count == 2) { return new List<object> { (double)(F(list[0]) + dx), (double)(F(list[1]) + dz) }; }
            if (t == typeof(Vector3) && list.Count == 3) { return new List<object> { (double)(F(list[0]) + dx), (double)(F(list[1]) + dy), (double)(F(list[2]) + dz) }; }
            if (t == typeof(UnityEngine.Rect) && list.Count == 4)
            {
                return new List<object> { (double)(F(list[0]) + dx), (double)(F(list[1]) + dz), (double)(F(list[2]) + dx), (double)(F(list[3]) + dz) };
            }
            if (t.IsGenericType && t.FullName.StartsWith("System.ValueTuple`"))
            {
                // A [point, numbers...] item: the point moves, the rest (widths, opening heights in a wall) don't.
                Type[] items = t.GetGenericArguments();
                return list.Select((item, i) => Shift(item, items[i], "", dx, dy, dz) ?? item).ToList();
            }
            if (t == typeof(Graybox.Cell) || t == typeof(System.Random)) { return null; }
            Type element = LevelSpec.ElementOf(t);
            if (element == null) { return null; }
            if (element == typeof(float))
            {
                // Floors (Lawns): heights.
                return name == "floors" ? list.Select(v => (object)(double)(F(v) + dy)).ToList() : null;
            }
            if (element == typeof(Graybox.Cell))
            {
                return list.Select(c =>
                {
                    var cell = ((JsonObject)c).Clone();
                    foreach (string k in new[] { "x0", "x1" }) { cell[k] = (double)(F(cell[k]) + dx); }
                    foreach (string k in new[] { "z0", "z1" }) { cell[k] = (double)(F(cell[k]) + dz); }
                    foreach (string k in new[] { "floor", "ceiling" }) { cell[k] = (double)(F(cell[k]) + dy); }
                    return (object)cell;
                }).ToList();
            }
            return list.Select(item => Shift(item, element, "", dx, dy, dz) ?? item).ToList();
        }

        // ---- New pieces ----

        /// <summary>What a new piece's settings start as, by name: enough to see it and move it into place.</summary>
        private static readonly Dictionary<string, object> Starts = new()
        {
            ["x0"] = 0.0, ["x1"] = 4.0, ["z0"] = 0.0, ["z1"] = 4.0, ["y0"] = 0.0, ["y1"] = 1.0, ["width"] = 2.0, ["length"] = 4.0,
            ["half"] = 50.0, ["clear"] = 1.2, ["period"] = 4.0, ["thickness"] = 0.5, ["openTop"] = -1.0, ["railTop"] = 6.0,
            ["hookBottom"] = 2.0, ["underside"] = 3.0, ["text"] = "Label", ["course"] = "Course", ["role"] = "start-finish",
            ["number"] = "00", ["b"] = new List<object> { 4.0, 0.0 }, ["direction"] = new List<object> { 0.0, 1.0 },
            ["facing"] = new List<object> { 0.0, 1.0 }, ["approach"] = new List<object> { 0.0, 1.0 }, ["inward"] = new List<object> { 0.0, 1.0 },
            ["outward"] = new List<object> { 0.0, 1.0 }, ["travel"] = new List<object> { 0.0, 0.0, 4.0 },
            ["localCentre"] = new List<object> { 0.0, 0.5, 0.0 }, ["area"] = new List<object> { 0.0, 0.0, 4.0, 4.0 },
            ["floors"] = new List<object> { 0.0 }, ["random"] = new JsonObject { ["seed"] = 1.0 },
        };

        private static List<object> Square() => new()
        {
            new List<object> { 0.0, 0.0 }, new List<object> { 4.0, 0.0 }, new List<object> { 4.0, 4.0 }, new List<object> { 0.0, 4.0 },
        };

        /// <summary>
        /// A new piece of a kind, in parent, every setting it needs given a starting value (the rest left at their
        /// defaults), laid out at (x, y, z) in the parent's coordinates.
        /// </summary>
        public static JsonObject NewPiece(string piece, string parent, Vector3 at)
        {
            var entry = new JsonObject { ["piece"] = piece };
            if (piece == LevelSpec.StatePiece)
            {
                foreach ((string key, Func<float> get, _) in LevelSpec.StateFields) { entry[key] = (double)get(); }
                return entry;
            }
            MethodInfo m = LevelSpec.Pieces[piece];
            foreach (ParameterInfo p in m.GetParameters())
            {
                if (p.HasDefaultValue || p.GetCustomAttribute<ParamArrayAttribute>() != null) { continue; }
                Type t = p.ParameterType;
                if (t == typeof(Transform)) { entry[p.Name] = parent; }
                else if (p.Name == "name" || p.Name == "label") { entry[p.Name] = $"New {piece}"; }
                else if (Starts.TryGetValue(p.Name, out object start) && Fits(start, t)) { entry[p.Name] = start is JsonObject o ? o.Clone() : start; }
                else if (t == typeof(Vector2[])) { entry[p.Name] = Square(); }
                else if (t == typeof(Vector3) && p.Name == "size") { entry[p.Name] = new List<object> { 4.0, 0.5, 4.0 }; }
                else if (t == typeof(float) && p.Name == "size") { entry[p.Name] = 2.0; }
                else if (t == typeof(Vector2) && p.Name == "size") { entry[p.Name] = new List<object> { 2.0, 2.0 }; }
                else if (t == typeof(Vector3) && p.Name == "top") { entry[p.Name] = new List<object> { 0.0, 1.0, 0.0 }; }
                else if (t == typeof(float) && p.Name == "top") { entry[p.Name] = 1.0; }
                else if (LevelSpec.ElementOf(t) == typeof(Graybox.Cell))
                {
                    entry[p.Name] = new List<object> { new JsonObject { ["name"] = "Room", ["x0"] = 0.0, ["z0"] = 0.0, ["x1"] = 8.0, ["z1"] = 6.0, ["floor"] = 0.0, ["ceiling"] = 4.0 } };
                }
                else { entry[p.Name] = LevelSpec.Encode(Blank(t), null) ?? ""; }
            }
            Translate(entry, at.x, at.y, at.z);
            Tidy(entry);
            return entry;
        }

        private static bool Fits(object start, Type t) =>
            (start is double && (t == typeof(float) || t == typeof(int))) || (start is string && t == typeof(string))
            || (start is List<object> l && ((l.Count == 2 && t == typeof(Vector2)) || (l.Count == 3 && t == typeof(Vector3))
                || (l.Count == 4 && t == typeof(UnityEngine.Rect)) || (LevelSpec.ElementOf(t) == typeof(float))))
            || (start is JsonObject && t == typeof(System.Random));

        /// <summary>An empty value of a type: 0, false, "", a zero point, an empty list.</summary>
        public static object Blank(Type t)
        {
            if (t == typeof(string)) { return ""; }
            if (t == typeof(System.Random)) { return new SeededRandom(1); }
            Type element = LevelSpec.ElementOf(t);
            if (element != null) { return Array.CreateInstance(element, 0); }
            return t.IsValueType ? Activator.CreateInstance(t) : null;
        }
    }
}
