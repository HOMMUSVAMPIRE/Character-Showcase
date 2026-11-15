using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace CharacterShowcase.EditorTools.Level
{
    /// <summary>
    /// A JSON object that keeps its keys in the order they were added, so a level spec reads (and diffs) in the order
    /// it was written: each piece's settings in its parameters' order.
    /// </summary>
    public sealed class JsonObject
    {
        private readonly List<string> keys = new();
        private readonly Dictionary<string, object> values = new();

        public IReadOnlyList<string> Keys => keys;
        public int Count => keys.Count;

        public object this[string key]
        {
            get => values[key];
            set
            {
                if (!values.ContainsKey(key)) { keys.Add(key); }
                values[key] = value;
            }
        }

        public bool TryGetValue(string key, out object value) => values.TryGetValue(key, out value);
        public bool ContainsKey(string key) => values.ContainsKey(key);

        /// <summary>Removes a key (and its value); returns whether it was there.</summary>
        public bool Remove(string key)
        {
            if (!values.Remove(key)) { return false; }
            keys.Remove(key);
            return true;
        }

        /// <summary>Puts the keys in the given order (any not in it after, as they were).</summary>
        public void Order(IEnumerable<string> order)
        {
            var sorted = order.Where(values.ContainsKey).Distinct().ToList();
            sorted.AddRange(keys.Where(k => !sorted.Contains(k)));
            keys.Clear();
            keys.AddRange(sorted);
        }

        /// <summary>A deep copy (lists and objects copied, so changing one doesn't change the other).</summary>
        public JsonObject Clone() => (JsonObject)CloneValue(this);

        private static object CloneValue(object value)
        {
            switch (value)
            {
                case JsonObject o:
                    var copy = new JsonObject();
                    foreach (string k in o.keys) { copy[k] = CloneValue(o.values[k]); }
                    return copy;
                case List<object> list: return list.Select(CloneValue).ToList();
                default: return value;
            }
        }
    }

    /// <summary>
    /// Reads and writes the JSON level specs are kept in (see LevelSpec), with no dependencies: objects are JsonObjects,
    /// arrays List&lt;object&gt;, numbers doubles, and strings, bools and null themselves. Numbers are written so they
    /// read back as the same float.
    /// </summary>
    public static class LevelSpecJson
    {
        // ---- Reading ----

        /// <summary>Parses JSON text; throws a FormatException saying where it went wrong.</summary>
        public static object Parse(string text)
        {
            int i = 0;
            object value = ReadValue(text, ref i);
            SkipSpace(text, ref i);
            if (i < text.Length) { throw Error(text, i, "Unexpected text after the JSON"); }
            return value;
        }

        private static FormatException Error(string text, int at, string message)
        {
            int line = 1, column = 1;
            for (int k = 0; k < at && k < text.Length; k++)
            {
                if (text[k] == '\n') { line++; column = 1; } else { column++; }
            }
            return new FormatException($"{message} (line {line}, column {column}).");
        }

        private static void SkipSpace(string s, ref int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i])) { i++; }
        }

        private static object ReadValue(string s, ref int i)
        {
            SkipSpace(s, ref i);
            if (i >= s.Length) { throw Error(s, i, "Unexpected end of the JSON"); }
            char c = s[i];
            if (c == '{') { return ReadObject(s, ref i); }
            if (c == '[') { return ReadArray(s, ref i); }
            if (c == '"') { return ReadString(s, ref i); }
            if (c == '-' || char.IsDigit(c)) { return ReadNumber(s, ref i); }
            if (Literal(s, ref i, "true")) { return true; }
            if (Literal(s, ref i, "false")) { return false; }
            if (Literal(s, ref i, "null")) { return null; }
            throw Error(s, i, $"Unexpected '{c}'");
        }

        private static bool Literal(string s, ref int i, string word)
        {
            if (string.CompareOrdinal(s, i, word, 0, word.Length) != 0) { return false; }
            i += word.Length;
            return true;
        }

        private static JsonObject ReadObject(string s, ref int i)
        {
            var obj = new JsonObject();
            i++;
            SkipSpace(s, ref i);
            if (i < s.Length && s[i] == '}') { i++; return obj; }
            while (true)
            {
                SkipSpace(s, ref i);
                if (i >= s.Length || s[i] != '"') { throw Error(s, i, "Expected a key in quotes"); }
                string key = ReadString(s, ref i);
                SkipSpace(s, ref i);
                if (i >= s.Length || s[i] != ':') { throw Error(s, i, "Expected ':'"); }
                i++;
                if (obj.ContainsKey(key)) { throw Error(s, i, $"The key '{key}' appears twice"); }
                obj[key] = ReadValue(s, ref i);
                SkipSpace(s, ref i);
                if (i < s.Length && s[i] == ',') { i++; continue; }
                if (i < s.Length && s[i] == '}') { i++; return obj; }
                throw Error(s, i, "Expected ',' or '}'");
            }
        }

        private static List<object> ReadArray(string s, ref int i)
        {
            var list = new List<object>();
            i++;
            SkipSpace(s, ref i);
            if (i < s.Length && s[i] == ']') { i++; return list; }
            while (true)
            {
                list.Add(ReadValue(s, ref i));
                SkipSpace(s, ref i);
                if (i < s.Length && s[i] == ',') { i++; continue; }
                if (i < s.Length && s[i] == ']') { i++; return list; }
                throw Error(s, i, "Expected ',' or ']'");
            }
        }

        private static string ReadString(string s, ref int i)
        {
            var sb = new StringBuilder();
            i++;
            while (i < s.Length)
            {
                char c = s[i++];
                if (c == '"') { return sb.ToString(); }
                if (c != '\\') { sb.Append(c); continue; }
                if (i >= s.Length) { break; }
                char e = s[i++];
                switch (e)
                {
                    case '"': sb.Append('"'); break;
                    case '\\': sb.Append('\\'); break;
                    case '/': sb.Append('/'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'u':
                        if (i + 4 > s.Length) { throw Error(s, i, "Bad \\u escape"); }
                        sb.Append((char)int.Parse(s.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                        i += 4;
                        break;
                    default: throw Error(s, i, $"Bad escape '\\{e}'");
                }
            }
            throw Error(s, i, "Unterminated string");
        }

        private static double ReadNumber(string s, ref int i)
        {
            int start = i;
            while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) { i++; }
            if (!double.TryParse(s.Substring(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture, out double d))
            {
                throw Error(s, start, "Bad number");
            }
            return d;
        }

        // ---- Writing ----

        /// <summary>Writes a value on one line (objects and arrays inline).</summary>
        public static string Write(object value)
        {
            var sb = new StringBuilder();
            Write(sb, value);
            return sb.ToString();
        }

        /// <summary>Writes a value into sb on one line.</summary>
        public static void Write(StringBuilder sb, object value)
        {
            switch (value)
            {
                case null: sb.Append("null"); break;
                case bool b: sb.Append(b ? "true" : "false"); break;
                case string str: WriteString(sb, str); break;
                case float f: sb.Append(Number(f)); break;
                case double d: sb.Append(Number(d)); break;
                case int n: sb.Append(n.ToString(CultureInfo.InvariantCulture)); break;
                case JsonObject obj:
                    sb.Append('{');
                    for (int k = 0; k < obj.Count; k++)
                    {
                        if (k > 0) { sb.Append(", "); }
                        WriteString(sb, obj.Keys[k]);
                        sb.Append(": ");
                        Write(sb, obj[obj.Keys[k]]);
                    }
                    sb.Append('}');
                    break;
                case IEnumerable<object> list:
                    sb.Append('[');
                    bool first = true;
                    foreach (object item in list)
                    {
                        if (!first) { sb.Append(", "); }
                        first = false;
                        Write(sb, item);
                    }
                    sb.Append(']');
                    break;
                default: throw new ArgumentException($"Can't write a {value.GetType().Name} as JSON.");
            }
        }

        /// <summary>Writes a value over several lines, indented: objects a key a line, arrays of plain values on one.</summary>
        public static string WritePretty(object value)
        {
            var sb = new StringBuilder();
            WritePretty(sb, value, "");
            return sb.ToString();
        }

        private static void WritePretty(StringBuilder sb, object value, string indent)
        {
            string inner = indent + "  ";
            switch (value)
            {
                case JsonObject obj when obj.Count > 0:
                    sb.Append("{\n");
                    for (int k = 0; k < obj.Count; k++)
                    {
                        sb.Append(inner);
                        WriteString(sb, obj.Keys[k]);
                        sb.Append(": ");
                        WritePretty(sb, obj[obj.Keys[k]], inner);
                        sb.Append(k < obj.Count - 1 ? ",\n" : "\n");
                    }
                    sb.Append(indent).Append('}');
                    break;
                case List<object> list when list.Exists(item => item is JsonObject or List<object>):
                    sb.Append("[\n");
                    for (int k = 0; k < list.Count; k++)
                    {
                        sb.Append(inner);
                        WritePretty(sb, list[k], inner);
                        sb.Append(k < list.Count - 1 ? ",\n" : "\n");
                    }
                    sb.Append(indent).Append(']');
                    break;
                default:
                    Write(sb, value);
                    break;
            }
        }

        /// <summary>A float as the shortest text that reads back as the same float.</summary>
        public static string Number(float f)
        {
            if (float.IsNaN(f) || float.IsInfinity(f)) { throw new ArgumentException("JSON has no NaN or infinity."); }
            // "R" round-trips; the shortest of the precisions that also round-trips reads better (0.3, not 0.300000012).
            for (int digits = 1; digits <= 9; digits++)
            {
                string text = f.ToString("G" + digits, CultureInfo.InvariantCulture);
                if (float.Parse(text, CultureInfo.InvariantCulture) == f) { return Tidy(text); }
            }
            return Tidy(f.ToString("R", CultureInfo.InvariantCulture));
        }

        private static string Number(double d) => d == Math.Floor(d) && Math.Abs(d) < 1e15
            ? ((long)d).ToString(CultureInfo.InvariantCulture)
            : Number((float)d);

        // Integers without an exponent where they fit; exponents with a small "e", as Python's json writes them, so a
        // spec written by either reads the same.
        private static string Tidy(string text) => text.Contains("E") && float.Parse(text, CultureInfo.InvariantCulture) is var v
            && v == Math.Floor(v) && Math.Abs(v) < 1e9f ? ((long)v).ToString(CultureInfo.InvariantCulture) : text.Replace("E", "e");

        private static void WriteString(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) { sb.Append("\\u").Append(((int)c).ToString("x4")); }
                        else { sb.Append(c); }
                        break;
                }
            }
            sb.Append('"');
        }
    }
}
