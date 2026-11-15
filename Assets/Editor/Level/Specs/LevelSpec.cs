using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using UnityEngine;

namespace CharacterShowcase.EditorTools.Level
{
    /// <summary>Marks a Graybox piece a level spec can call: its parameters are what a spec gives it, by name.</summary>
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class PieceAttribute : Attribute { }

    /// <summary>
    /// A System.Random that remembers its seed, so a piece given one can be written into a level spec as that seed
    /// (Tree and Planter draw one of their own from the random they're given, then use it).
    /// </summary>
    public sealed class SeededRandom : System.Random
    {
        public readonly int Seed;
        public SeededRandom(int seed) : base(seed) => Seed = seed;
    }

    /// <summary>
    /// A level as data: its name, the scene it's built into, and its parts, each a list of calls to Graybox's pieces
    /// (the [Piece] methods) with their settings by parameter name, in the order they're made. Building one calls
    /// them in order (see Build), so a spec builds exactly what the builder it was recorded from builds (see
    /// LevelSpecRecorder), and a hand-written or generated one builds whatever it lists.
    ///
    /// In JSON (see ToJson, and Tools/LevelSpec/level-spec.schema.json, written by LevelSpecTools):
    /// <code>
    /// {"format": "character-showcase-level", "version": 1, "name": "Proving Ground", "scene": "Assets/…/ProvingGround.unity",
    ///  "parts": [{"name": "P2 Gap Run", "pieces": [
    ///     {"piece": "Group", "parent": "", "name": "P2 Gap Run"},
    ///     {"piece": "Platform", "parent": "P2 Gap Run", "name": "Platform A (+4)", "x0": 136, "x1": 148, …},
    ///     {"piece": "State", "stepRise": 0.3, "stepRun": 0.4, …}]}]}
    /// </code>
    /// Values: numbers; booleans; strings; surfaces and other enums by name; points [x, z] and [x, y, z]; areas
    /// [xMin, zMin, xMax, zMax]; cells {"name", "x0", "z0", "x1", "z1", "floor", "ceiling", "open"}; randoms {"seed"};
    /// lists as arrays; and a parent (any Transform) as its path under the level's root ("" is the root, "A/B" the B
    /// in A, "A/B#2" A's second child named B). Settings left out take the piece's defaults. A "State" entry sets the
    /// stairs' step size and the decks' and rails' dimensions for the pieces after it (each part starts from the
    /// generation settings').
    /// </summary>
    public sealed class LevelSpec
    {
        public const string Format = "character-showcase-level";
        public const int Version = 1;
        public const string StatePiece = "State";

        public string Name;
        public string Scene;
        public readonly List<Part> Parts = new();

        /// <summary>A part of a level: built as one step, its pieces in order.</summary>
        public sealed class Part
        {
            public string Name;
            public readonly List<JsonObject> Pieces = new();
        }

        // ---- The pieces ----

        private static Dictionary<string, MethodInfo> pieces;

        /// <summary>Every piece a spec can call, by name.</summary>
        public static IReadOnlyDictionary<string, MethodInfo> Pieces => pieces ??= typeof(Graybox)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.GetCustomAttribute<PieceAttribute>() != null)
            .ToDictionary(m => m.Name);

        /// <summary>The Graybox state a "State" entry sets, by its key.</summary>
        public static readonly (string key, Func<float> get, Action<float> set)[] StateFields =
        {
            ("stepRise", () => Graybox.StepRise, v => Graybox.StepRise = v),
            ("stepRun", () => Graybox.StepRun, v => Graybox.StepRun = v),
            ("bridgeLength", () => Graybox.BridgeLength, v => Graybox.BridgeLength = v),
            ("deck", () => Graybox.Deck, v => Graybox.Deck = v),
            ("railHeight", () => Graybox.RailHeight, v => Graybox.RailHeight = v),
            ("railThickness", () => Graybox.RailThickness, v => Graybox.RailThickness = v),
        };

        // ---- JSON ----

        /// <summary>The spec as JSON text: one line per piece, so it reads and diffs piece by piece.</summary>
        public string ToJson()
        {
            var sb = new StringBuilder();
            sb.Append("{\n");
            sb.Append("  \"format\": ").Append(LevelSpecJson.Write(Format)).Append(",\n");
            sb.Append("  \"version\": ").Append(Version).Append(",\n");
            sb.Append("  \"name\": ").Append(LevelSpecJson.Write(Name)).Append(",\n");
            sb.Append("  \"scene\": ").Append(LevelSpecJson.Write(Scene)).Append(",\n");
            sb.Append("  \"parts\": [");
            for (int p = 0; p < Parts.Count; p++)
            {
                sb.Append(p == 0 ? "\n" : ",\n");
                sb.Append("    {\n      \"name\": ").Append(LevelSpecJson.Write(Parts[p].Name)).Append(",\n      \"pieces\": [");
                for (int i = 0; i < Parts[p].Pieces.Count; i++)
                {
                    sb.Append(i == 0 ? "\n        " : ",\n        ");
                    LevelSpecJson.Write(sb, Parts[p].Pieces[i]);
                }
                sb.Append(Parts[p].Pieces.Count > 0 ? "\n      ]\n    }" : "]\n    }");
            }
            sb.Append(Parts.Count > 0 ? "\n  ]\n}\n" : "]\n}\n");
            return sb.ToString();
        }

        /// <summary>Reads a spec from JSON, checking its format and every piece (so a mistake shows before a build starts).</summary>
        public static LevelSpec FromJson(string json)
        {
            if (LevelSpecJson.Parse(json) is not JsonObject root) { throw new FormatException("A level spec is a JSON object."); }
            if (!root.TryGetValue("format", out object format) || (format as string) != Format)
            {
                throw new FormatException($"Not a level spec: its \"format\" should be \"{Format}\".");
            }
            if (root.TryGetValue("version", out object version) && Convert.ToInt32(version) > Version)
            {
                throw new FormatException($"The spec is version {version}; this reads up to version {Version}.");
            }
            var spec = new LevelSpec
            {
                Name = root.TryGetValue("name", out object name) ? name as string : null,
                Scene = root.TryGetValue("scene", out object scene) ? scene as string : null,
            };
            if (string.IsNullOrEmpty(spec.Name)) { throw new FormatException("A level spec needs a \"name\"."); }
            if (string.IsNullOrEmpty(spec.Scene) || !spec.Scene.StartsWith("Assets/") || !spec.Scene.EndsWith(".unity"))
            {
                throw new FormatException("A level spec needs a \"scene\": the path of the scene to build, \"Assets/….unity\".");
            }
            if (!root.TryGetValue("parts", out object parts) || parts is not List<object> partList)
            {
                throw new FormatException("A level spec needs \"parts\": a list.");
            }
            foreach (object p in partList)
            {
                if (p is not JsonObject partObject || !partObject.TryGetValue("name", out object partName) || partName is not string)
                {
                    throw new FormatException("Each part needs a \"name\".");
                }
                var part = new Part { Name = (string)partName };
                if (partObject.TryGetValue("pieces", out object list))
                {
                    if (list is not List<object> pieceList) { throw new FormatException($"Part '{part.Name}': \"pieces\" should be a list."); }
                    for (int i = 0; i < pieceList.Count; i++)
                    {
                        if (pieceList[i] is not JsonObject piece) { throw new FormatException($"Part '{part.Name}', piece {i + 1}: not an object."); }
                        try { Check(piece); }
                        catch (FormatException e) { throw new FormatException($"Part '{part.Name}', piece {i + 1}: {e.Message}"); }
                        part.Pieces.Add(piece);
                    }
                }
                spec.Parts.Add(part);
            }
            return spec;
        }

        /// <summary>Checks a piece entry names a piece and gives each of its settings a value of the right shape.</summary>
        public static void Check(JsonObject entry)
        {
            if (!entry.TryGetValue("piece", out object pieceName) || pieceName is not string name)
            {
                throw new FormatException("Each piece needs a \"piece\": which piece it is.");
            }
            if (name == StatePiece)
            {
                foreach (string key in entry.Keys.Where(k => k != "piece"))
                {
                    if (StateFields.All(f => f.key != key)) { throw new FormatException($"\"State\" has no setting '{key}'."); }
                    if (entry[key] is not double) { throw new FormatException($"\"State\" '{key}' should be a number."); }
                }
                return;
            }
            if (!Pieces.TryGetValue(name, out MethodInfo method)) { throw new FormatException($"There's no piece '{name}'."); }
            // Decoding every setting checks its shape; parents are only checked when built (they name what's built before).
            DecodeArguments(name, method, entry, null);
        }

        // ---- Building ----

        /// <summary>
        /// Builds a part of the spec under root (Graybox.Begin already called: the part starts from the generation
        /// settings' dimensions), each piece in order.
        /// </summary>
        public static void Build(Part part, Transform root)
        {
            foreach (JsonObject entry in part.Pieces)
            {
                try { BuildPiece(entry, root); }
                catch (InvalidOperationException e)
                {
                    throw new InvalidOperationException($"Part '{part.Name}': {LevelSpecJson.Write(entry)}: {e.Message}", e.InnerException ?? e);
                }
            }
        }

        /// <summary>
        /// Builds one piece under root (or, for a "State" entry, sets the dimensions it gives). Throws an
        /// InvalidOperationException saying what went wrong.
        /// </summary>
        public static void BuildPiece(JsonObject entry, Transform root)
        {
            string name = entry["piece"] as string;
            try
            {
                if (name == StatePiece)
                {
                    foreach ((string key, _, Action<float> set) in StateFields)
                    {
                        if (entry.TryGetValue(key, out object v)) { set((float)(double)v); }
                    }
                    return;
                }
                if (name == null || !Pieces.TryGetValue(name, out MethodInfo method)) { throw new FormatException($"There's no piece '{name}'."); }
                method.Invoke(null, DecodeArguments(name, method, entry, root));
            }
            catch (TargetInvocationException e) when (e.InnerException != null)
            {
                throw new InvalidOperationException($"building it failed: {e.InnerException.Message}", e.InnerException);
            }
            catch (FormatException e)
            {
                throw new InvalidOperationException(e.Message, e);
            }
        }

        /// <summary>
        /// Checks one piece entry as Check does, returning the problem (null if none): its settings' shapes and, given a
        /// root, that its parents are there.
        /// </summary>
        public static string Problem(JsonObject entry, Transform root = null)
        {
            try
            {
                Check(entry);
                if (root != null && entry["piece"] is string name && Pieces.TryGetValue(name, out MethodInfo method))
                {
                    DecodeArguments(name, method, entry, root);
                }
                return null;
            }
            catch (FormatException e) { return e.Message; }
        }

        /// <summary>A piece's arguments from an entry's settings (root null: only checking, no parents resolved).</summary>
        private static object[] DecodeArguments(string name, MethodInfo method, JsonObject entry, Transform root)
        {
            ParameterInfo[] parameters = method.GetParameters();
            foreach (string key in entry.Keys)
            {
                if (key != "piece" && parameters.All(p => p.Name != key))
                {
                    throw new FormatException($"'{name}' has no setting '{key}' (it has {string.Join(", ", parameters.Select(p => p.Name))}).");
                }
            }
            var args = new object[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                ParameterInfo p = parameters[i];
                if (entry.TryGetValue(p.Name, out object json))
                {
                    try { args[i] = Decode(json, p.ParameterType, root); }
                    catch (FormatException e) { throw new FormatException($"'{name}' setting '{p.Name}': {e.Message}"); }
                    catch (InvalidCastException) { throw new FormatException($"'{name}' setting '{p.Name}': {LevelSpecJson.Write(json)} isn't {Describe(p.ParameterType)}."); }
                }
                else if (p.HasDefaultValue) { args[i] = DefaultOf(p); }
                else if (p.GetCustomAttribute<ParamArrayAttribute>() != null) { args[i] = Array.CreateInstance(p.ParameterType.GetElementType(), 0); }
                else { throw new FormatException($"'{name}' needs a '{p.Name}' ({Describe(p.ParameterType)})."); }
            }
            return args;
        }

        /// <summary>A parameter's default, as a value of its type (C# stores some as null or their underlying number).</summary>
        public static object DefaultOf(ParameterInfo p)
        {
            Type t = p.ParameterType;
            object d = p.DefaultValue;
            if (d == null || d == DBNull.Value || d == Missing.Value) { return t.IsValueType ? Activator.CreateInstance(t) : null; }
            return t.IsEnum ? Enum.ToObject(t, d) : d;
        }

        // ---- Values ----

        /// <summary>A value as JSON (see the class notes), its Transforms as paths under root.</summary>
        public static object Encode(object value, Transform root)
        {
            switch (value)
            {
                case null: return null;
                // Numbers as doubles, as JSON reads them, so a spec holds them one way however it was made (written as
                // the float they came from: see LevelSpecJson.Number).
                case float f: return (double)f;
                case int n: return (double)n;
                case bool or string: return value;
                case Enum e: return e.ToString();
                case Vector2 v: return new List<object> { (double)v.x, (double)v.y };
                case Vector3 v: return new List<object> { (double)v.x, (double)v.y, (double)v.z };
                case UnityEngine.Rect r: return new List<object> { (double)r.xMin, (double)r.yMin, (double)r.xMax, (double)r.yMax };
                case Graybox.Cell c:
                    var cell = new JsonObject
                    {
                        ["name"] = c.Name, ["x0"] = (double)c.Area.xMin, ["z0"] = (double)c.Area.yMin, ["x1"] = (double)c.Area.xMax,
                        ["z1"] = (double)c.Area.yMax, ["floor"] = (double)c.Floor, ["ceiling"] = (double)c.Ceiling,
                    };
                    if (c.Open) { cell["open"] = true; }
                    return cell;
                case SeededRandom r: return new JsonObject { ["seed"] = (double)r.Seed };
                case System.Random: throw new ArgumentException("A random can only be recorded with its seed: give the piece a SeededRandom.");
                case Transform t: return PathOf(t, root);
                case ITuple tuple:
                    var items = new List<object>();
                    for (int i = 0; i < tuple.Length; i++) { items.Add(Encode(tuple[i], root)); }
                    return items;
                case IEnumerable list: return list.Cast<object>().Select(item => Encode(item, root)).ToList();
                default: throw new ArgumentException($"A level spec can't hold a {value.GetType().Name}.");
            }
        }

        /// <summary>A JSON value as a value of type t (root null: Transforms only checked to be paths).</summary>
        public static object Decode(object json, Type t, Transform root)
        {
            if (t == typeof(float)) { return (float)(double)json; }
            if (t == typeof(int))
            {
                double d = (double)json;
                if (d != Math.Floor(d)) { throw new FormatException($"{d} isn't a whole number."); }
                return (int)d;
            }
            if (t == typeof(bool)) { return (bool)json; }
            if (t == typeof(string)) { return json == null ? null : (string)json; }
            if (t.IsEnum)
            {
                if (json is string s && Enum.GetNames(t).Contains(s)) { return Enum.Parse(t, s); }
                throw new FormatException($"{LevelSpecJson.Write(json)} isn't one of {string.Join(", ", Enum.GetNames(t))}.");
            }
            if (t == typeof(Vector2)) { float[] v = Numbers(json, 2); return new Vector2(v[0], v[1]); }
            if (t == typeof(Vector3)) { float[] v = Numbers(json, 3); return new Vector3(v[0], v[1], v[2]); }
            if (t == typeof(UnityEngine.Rect)) { float[] v = Numbers(json, 4); return UnityEngine.Rect.MinMaxRect(v[0], v[1], v[2], v[3]); }
            if (t == typeof(Graybox.Cell))
            {
                var o = (JsonObject)json;
                float F(string key) => o.TryGetValue(key, out object v) ? (float)(double)v : throw new FormatException($"A cell needs '{key}'.");
                return new Graybox.Cell(o.TryGetValue("name", out object n) ? (string)n : "Cell", F("x0"), F("z0"), F("x1"), F("z1"),
                    F("floor"), F("ceiling"), o.TryGetValue("open", out object open) && (bool)open);
            }
            if (t == typeof(System.Random))
            {
                var o = (JsonObject)json;
                return new SeededRandom(o.TryGetValue("seed", out object seed) ? (int)(double)seed : throw new FormatException("A random needs a 'seed'."));
            }
            if (t == typeof(Transform))
            {
                if (json is not string path) { throw new FormatException("A parent is a path (a string)."); }
                return root == null ? null : Resolve(path, root);
            }
            if (t.IsGenericType && t.FullName.StartsWith("System.ValueTuple`"))
            {
                Type[] types = t.GetGenericArguments();
                var items = (List<object>)json;
                if (items.Count != types.Length) { throw new FormatException($"{LevelSpecJson.Write(json)} should have {types.Length} items."); }
                return Activator.CreateInstance(t, items.Select((item, i) => Decode(item, types[i], root)).ToArray());
            }
            Type element = ElementOf(t);
            if (element != null)
            {
                var items = (List<object>)json;
                Array array = Array.CreateInstance(element, items.Count);
                for (int i = 0; i < items.Count; i++) { array.SetValue(Decode(items[i], element, root), i); }
                if (t.IsAssignableFrom(array.GetType())) { return array; }
                var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(element));
                foreach (object item in array) { list.Add(item); }
                return list;
            }
            throw new FormatException($"A level spec can't give a {t.Name}.");
        }

        /// <summary>What a list type holds: T for T[], IEnumerable&lt;T&gt;, IList&lt;T&gt; and List&lt;T&gt;; null for anything else.</summary>
        public static Type ElementOf(Type t)
        {
            if (t.IsArray) { return t.GetElementType(); }
            if (t.IsGenericType && t != typeof(string))
            {
                Type d = t.GetGenericTypeDefinition();
                if (d == typeof(IEnumerable<>) || d == typeof(IList<>) || d == typeof(List<>) || d == typeof(IReadOnlyList<>)) { return t.GetGenericArguments()[0]; }
            }
            return null;
        }

        private static float[] Numbers(object json, int count)
        {
            if (json is not List<object> items || items.Count != count || items.Any(i => i is not double))
            {
                throw new FormatException($"{LevelSpecJson.Write(json)} should be {count} numbers.");
            }
            return items.Select(i => (float)(double)i).ToArray();
        }

        /// <summary>What a value of type t looks like in a spec, for error messages.</summary>
        public static string Describe(Type t)
        {
            if (t == typeof(float)) { return "a number"; }
            if (t == typeof(int)) { return "a whole number"; }
            if (t == typeof(bool)) { return "true or false"; }
            if (t == typeof(string)) { return "a string"; }
            if (t.IsEnum) { return "one of " + string.Join(", ", Enum.GetNames(t)); }
            if (t == typeof(Vector2)) { return "a point [x, z]"; }
            if (t == typeof(Vector3)) { return "a point [x, y, z]"; }
            if (t == typeof(UnityEngine.Rect)) { return "an area [xMin, zMin, xMax, zMax]"; }
            if (t == typeof(Graybox.Cell)) { return "a cell {name, x0, z0, x1, z1, floor, ceiling, open}"; }
            if (t == typeof(System.Random)) { return "a random {seed}"; }
            if (t == typeof(Transform)) { return "a parent's path"; }
            if (t.IsGenericType && t.FullName.StartsWith("System.ValueTuple`")) { return "[" + string.Join(", ", t.GetGenericArguments().Select(Describe)) + "]"; }
            Type element = ElementOf(t);
            return element != null ? $"a list of {Describe(element)}" : t.Name;
        }

        // ---- Paths ----

        /// <summary>
        /// Where t is under root: its names from root's child down, "/" between, with "#n" after the nth (from 2)
        /// child of a parent with the same name. "" is root.
        /// </summary>
        public static string PathOf(Transform t, Transform root)
        {
            var segments = new List<string>();
            for (Transform at = t; at != root; at = at.parent)
            {
                if (at == null) { throw new ArgumentException($"'{t.name}' isn't in the level being recorded."); }
                if (at.name.Contains('/') || at.name.Contains('#'))
                {
                    throw new ArgumentException($"A group used as a parent can't have '/' or '#' in its name ('{at.name}').");
                }
                int n = 1;
                for (int i = 0; i < at.GetSiblingIndex(); i++)
                {
                    if (at.parent.GetChild(i).name == at.name) { n++; }
                }
                segments.Add(n == 1 ? at.name : $"{at.name}#{n}");
            }
            segments.Reverse();
            return string.Join("/", segments);
        }

        /// <summary>The Transform at a path under root (see PathOf).</summary>
        public static Transform Resolve(string path, Transform root)
        {
            Transform at = root;
            if (path.Length == 0) { return at; }
            foreach (string segment in path.Split('/'))
            {
                int hash = segment.LastIndexOf('#');
                string name = segment;
                int n = 1;
                if (hash >= 0 && int.TryParse(segment[(hash + 1)..], out int number)) { name = segment[..hash]; n = number; }
                Transform found = null;
                foreach (Transform child in at)
                {
                    if (child.name == name && --n == 0) { found = child; break; }
                }
                at = found != null ? found : throw new FormatException($"There's nothing at '{path}' (no '{segment}' in '{PathOf(at, root)}') yet.");
            }
            return at;
        }
    }

    /// <summary>
    /// Records a build as a LevelSpec: each top-level call to a Graybox piece (not the pieces it makes itself), with
    /// its settings, and a "State" entry wherever the builder's changed the stairs' or decks' dimensions. Each piece
    /// calls Call first (with its arguments, in order); recording is only on while LevelSpecTools records a level.
    /// </summary>
    public static class LevelSpecRecorder
    {
        private static LevelSpec.Part part;
        private static Transform root;
        private static int depth;
        private static float[] lastState;

        /// <summary>Whether a build's being recorded.</summary>
        public static bool Recording => part != null;

        /// <summary>Starts recording into part, under root, the part starting from the generation settings' dimensions.</summary>
        internal static void Begin(LevelSpec.Part into, Transform levelRoot, float[] startState)
        {
            part = into;
            root = levelRoot;
            depth = 0;
            lastState = startState;
        }

        internal static void End() => part = null;

        /// <summary>The dimensions a "State" entry sets, as they are now.</summary>
        internal static float[] CurrentState() => LevelSpec.StateFields.Select(f => f.get()).ToArray();

        /// <summary>Ends a piece's call (disposing it), so the pieces it makes aren't recorded as well.</summary>
        public readonly struct Scope : IDisposable
        {
            private readonly bool counted;
            internal Scope(bool counted) => this.counted = counted;
            public void Dispose()
            {
                if (counted) { depth--; }
            }
        }

        /// <summary>Called by each piece as it starts, with its arguments in order: records it if it's a top-level call.</summary>
        public static Scope Call(string piece, params object[] args)
        {
            if (part == null) { return default; }
            if (depth++ > 0) { return new Scope(true); }

            float[] state = CurrentState();
            if (!state.SequenceEqual(lastState))
            {
                var entry = new JsonObject { ["piece"] = LevelSpec.StatePiece };
                for (int i = 0; i < state.Length; i++) { entry[LevelSpec.StateFields[i].key] = (double)state[i]; }
                part.Pieces.Add(entry);
                lastState = state;
            }

            ParameterInfo[] parameters = LevelSpec.Pieces[piece].GetParameters();
            if (args.Length != parameters.Length)
            {
                throw new InvalidOperationException($"{piece} records {args.Length} arguments but has {parameters.Length} parameters.");
            }
            var call = new JsonObject { ["piece"] = piece };
            for (int i = 0; i < parameters.Length; i++)
            {
                ParameterInfo p = parameters[i];
                object value = args[i];
                // Settings left at their defaults are left out, so specs say only what's chosen.
                if (p.HasDefaultValue && Equals(value, LevelSpec.DefaultOf(p))) { continue; }
                if (p.GetCustomAttribute<ParamArrayAttribute>() != null && value is Array { Length: 0 }) { continue; }
                call[p.Name] = LevelSpec.Encode(value, root);
            }
            part.Pieces.Add(call);
            return new Scope(true);
        }
    }
}
