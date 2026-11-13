using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CharacterShowcase.EditorTools.Level
{
    /// <summary>
    /// Level specs (LevelSpec) from the levels' builders and back: exports each level as a spec (recording its build:
    /// LevelSpecRecorder) and checks the spec builds the same level, object for object; builds a level from a spec,
    /// like a builder (the build window, or -executeMethod); and writes the specs' JSON Schema for other tools.
    ///
    /// Menu: Tools > Character Showcase > Level > Specs. Command line (Unity closed):
    /// <c>-executeMethod CharacterShowcase.EditorTools.Level.LevelSpecTools.ExportFromCommandLine [-level "Proving Ground"]</c>,
    /// <c>...LevelSpecTools.BuildFromCommandLine -spec Assets/Levels/ProvingGround.level.json</c> and
    /// <c>...LevelSpecTools.WriteSchemaFromCommandLine</c>.
    /// </summary>
    public static class LevelSpecTools
    {
        /// <summary>Where exported specs go: "[scene file name].level.json".</summary>
        public const string SpecFolder = "Assets/Levels";
        /// <summary>The specs' JSON Schema, for tools outside Unity (relative to the project folder).</summary>
        public const string SchemaPath = "Tools/LevelSpec/level-spec.schema.json";

        private static readonly Dictionary<string, Func<GrayboxScene>> Levels = new()
        {
            ["Meridian Complex"] = () => MeridianComplexBuilder.Scene,
            ["Level Playground"] = () => LevelPlaygroundBuilder.Scene,
            ["Proving Ground"] = () => ProvingGroundBuilder.Scene,
        };

        /// <summary>The levels that have builders, which can be exported as specs.</summary>
        public static IReadOnlyList<string> LevelNames => Levels.Keys.ToList();

        /// <summary>Where a level's spec is exported to.</summary>
        public static string SpecPathFor(string scenePath) => $"{SpecFolder}/{Path.GetFileNameWithoutExtension(scenePath)}.level.json";

        // ---- Recording and building ----

        /// <summary>
        /// Builds parts in a new scene (replacing what's open) under a root named name, in the levels' palette, as
        /// GrayboxScene does up to its environment (no environment, saving or bakes), with each part's recording
        /// started and ended by around (null: none). Returns the root.
        /// </summary>
        private static Transform BuildParts(string name, IEnumerable<(string name, Action<Transform, Material> build)> parts,
            Action<string, Transform> before = null, Action after = null)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Material[] palette = GrayboxScene.Palette();
            Material concrete = palette[(int)Graybox.Surface.Concrete];
            Transform root = new GameObject(name).transform;
            Graybox.ClearDeferred();
            foreach ((string partName, Action<Transform, Material> build) in parts)
            {
                Graybox.UsePalette(palette);
                // Each part starts from the generation settings' dimensions, as a spec's parts are built.
                Graybox.Begin(concrete);
                before?.Invoke(partName, root);
                try { build(root, concrete); }
                finally { after?.Invoke(); }
            }
            Graybox.UsePalette(palette);
            Graybox.Begin(concrete);
            Graybox.RunDeferred();
            return root;
        }

        /// <summary>Records a level's build as a spec. The level's left built in the open scene; returns its root too.</summary>
        internal static LevelSpec Record(GrayboxScene level, out Transform root)
        {
            var spec = new LevelSpec { Name = level.Name, Scene = level.ScenePath };
            root = BuildParts(level.Name, level.Parts,
                (partName, levelRoot) =>
                {
                    var part = new LevelSpec.Part { Name = partName };
                    spec.Parts.Add(part);
                    LevelSpecRecorder.Begin(part, levelRoot, LevelSpecRecorder.CurrentState());
                },
                LevelSpecRecorder.End);
            return spec;
        }

        /// <summary>A level built from a spec, as the builders' levels are (GrayboxScene: its steps, environment and bakes).</summary>
        internal static GrayboxScene SceneFor(LevelSpec spec) => new(spec.Name, spec.Scene,
            spec.Parts.Select(part => (part.Name, (Action<Transform, Material>)((root, material) =>
            {
                Graybox.Begin(material);
                LevelSpec.Build(part, root);
            }))).ToArray());

        /// <summary>
        /// Records a level as a spec, writes it out and reads it back, and builds that: returns the spec, and how what
        /// it built differs from what the builder built (empty if it's the same, object for object).
        /// </summary>
        internal static LevelSpec RecordAndCheck(GrayboxScene level, out List<string> differences)
        {
            LevelSpec spec = Record(level, out Transform built);
            List<string> expected = Snapshot(built);
            LevelSpec read = LevelSpec.FromJson(spec.ToJson());
            Transform rebuilt = BuildParts(read.Name, SceneFor(read).Parts);
            differences = Compare(expected, Snapshot(rebuilt));
            return spec;
        }

        /// <summary>For tests: how a level rebuilt from its own spec (round-tripped through JSON) differs from the level (empty: none).</summary>
        public static List<string> CheckRoundTrip(string levelName)
        {
            RecordAndCheck(Levels[levelName](), out List<string> differences);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            return differences;
        }

        /// <summary>For tests: builds a spec's parts (as JSON text) in a new scene, without its environment or bakes; returns the root.</summary>
        public static Transform BuildForTest(string json)
        {
            LevelSpec spec = LevelSpec.FromJson(json);
            return BuildParts(spec.Name, SceneFor(spec).Parts);
        }

        // ---- Comparing builds ----

        private static string F(float v) => v.ToString("0.####", CultureInfo.InvariantCulture);
        private static string F(Vector3 v) => $"({F(v.x)}, {F(v.y)}, {F(v.z)})";

        /// <summary>
        /// A level's objects as lines of text, in hierarchy order: each object's path, transform, static flags and
        /// layer, and what's on it (meshes by size and a checksum of their vertices, materials, colliders, text,
        /// lights, LOD groups), to compare two builds by.
        /// </summary>
        public static List<string> Snapshot(Transform root)
        {
            var lines = new List<string>();
            void Visit(Transform t, string path)
            {
                var sb = new StringBuilder(path);
                GameObject go = t.gameObject;
                // A label's mesh is TextMeshPro's to make, when it next updates: its text is compared, not its mesh.
                bool label = go.GetComponents<Component>().Any(c => c.GetType().Name.StartsWith("TextMeshPro"));
                sb.Append($" | {F(t.localPosition)} {F(t.localEulerAngles)} {F(t.localScale)} | {GameObjectUtility.GetStaticEditorFlags(go)} {go.layer} {go.activeSelf}");
                foreach (Component c in go.GetComponents<Component>())
                {
                    switch (c)
                    {
                        case Transform: break;
                        case MeshFilter when label: break;
                        case MeshFilter mf when mf.sharedMesh != null:
                            Mesh m = mf.sharedMesh;
                            Vector3 sum = Vector3.zero;
                            foreach (Vector3 v in m.vertices) { sum += v; }
                            sb.Append($" | Mesh {m.vertexCount}v {m.subMeshCount}s {F(m.bounds.center)} {F(m.bounds.size)} {F(sum)}");
                            break;
                        case Renderer r:
                            sb.Append($" | {r.GetType().Name} {string.Join(",", r.sharedMaterials.Select(mat => mat != null ? mat.name : "none"))} {r.shadowCastingMode}");
                            break;
                        case BoxCollider b: sb.Append($" | Box {F(b.center)} {F(b.size)} {b.isTrigger} {b.enabled}"); break;
                        case Collider col: sb.Append($" | {col.GetType().Name} {F(col.bounds.center)} {F(col.bounds.size)} {col.isTrigger}"); break;
                        case Light l: sb.Append($" | Light {l.type} {F(l.intensity)} {F(l.range)} {l.color}"); break;
                        case LODGroup g: sb.Append($" | LOD {g.lodCount} {string.Join(",", g.GetLODs().Select(lod => F(lod.screenRelativeTransitionHeight)))}"); break;
                        default:
                            sb.Append($" | {c.GetType().Name}");
                            if (c.GetType().GetProperty("text")?.GetValue(c) is string text) { sb.Append($" \"{text}\""); }
                            break;
                    }
                }
                lines.Add(sb.ToString());
                var seen = new Dictionary<string, int>();
                foreach (Transform child in t)
                {
                    seen[child.name] = seen.TryGetValue(child.name, out int n) ? n + 1 : 1;
                    Visit(child, $"{path}/{child.name}{(seen[child.name] > 1 ? "#" + seen[child.name] : "")}");
                }
            }
            Visit(root, root.name);
            return lines;
        }

        /// <summary>How two snapshots differ: the first few lines that do, and the counts if they differ in length.</summary>
        public static List<string> Compare(List<string> expected, List<string> actual, int most = 12)
        {
            var differences = new List<string>();
            if (expected.Count != actual.Count) { differences.Add($"{expected.Count} objects expected, {actual.Count} built."); }
            for (int i = 0; i < Math.Min(expected.Count, actual.Count) && differences.Count < most; i++)
            {
                if (expected[i] != actual[i]) { differences.Add($"expected: {expected[i]}\n   built: {actual[i]}"); }
            }
            return differences;
        }

        // ---- Exporting ----

        /// <summary>
        /// Exports levels (by name) as specs in Assets/Levels, each checked by building it again from the spec; writes
        /// the schema too. Leaves an empty scene open. Returns whether every spec built its level exactly.
        /// </summary>
        public static bool Export(IEnumerable<string> levelNames, Func<string, string, bool> replaceChanged = null)
        {
            bool allSame = true;
            Directory.CreateDirectory(SpecFolder);
            foreach (string name in levelNames)
            {
                GrayboxScene level = Levels[name]();
                var timer = System.Diagnostics.Stopwatch.StartNew();
                LevelSpec spec = RecordAndCheck(level, out List<string> differences);
                string path = SpecPathFor(level.ScenePath);
                string json = spec.ToJson();
                // A spec that's changed since it was exported (in the Level Editor, say) is only replaced if asked.
                if (File.Exists(path) && File.ReadAllText(path) != json && (replaceChanged == null || !replaceChanged(name, path)))
                {
                    Debug.LogWarning($"[Level specs] Kept '{path}': it isn't what the {name}'s builder builds (edited since it was exported?). " +
                                     "Export it with -force (or say Replace) to replace it with the builder's.");
                    allSame = false;
                    continue;
                }
                File.WriteAllText(path, json);
                int pieces = spec.Parts.Sum(p => p.Pieces.Count);
                if (differences.Count == 0)
                {
                    Debug.Log($"[Level specs] Exported '{name}' to '{path}': {spec.Parts.Count} parts, {pieces} pieces; it builds the same level ({timer.Elapsed.TotalSeconds:0.0} s).");
                }
                else
                {
                    allSame = false;
                    Debug.LogError($"[Level specs] Exported '{name}' to '{path}', but it doesn't build the same level:\n{string.Join("\n", differences)}");
                }
            }
            WriteSchema();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AssetDatabase.Refresh();
            return allSame;
        }

        [MenuItem("Tools/Character Showcase/Level/Specs/Export Level Specs")]
        private static void ExportFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) { return; }
            string open = EditorSceneManager.GetActiveScene().path;
            try
            {
                Export(LevelNames, (name, path) => EditorUtility.DisplayDialog("Export Level Specs",
                    $"'{path}' isn't what the {name}'s builder builds: it's been edited since it was exported (in the Level Editor?), or the builder's changed.\n\n" +
                    "Replace it with the builder's? Edits made to the spec will be lost.", "Replace", "Keep the Spec"));
            }
            finally
            {
                if (!string.IsNullOrEmpty(open)) { EditorSceneManager.OpenScene(open); }
            }
        }

        /// <summary>
        /// For -executeMethod: exports every level's spec, or the one named by -level; exits 1 if a spec doesn't build
        /// its level, or one's been changed since it was exported (kept unless -force is given).
        /// </summary>
        public static void ExportFromCommandLine()
        {
            string level = Argument("-level");
            if (level != null && !Levels.ContainsKey(level))
            {
                Debug.LogError($"[Level specs] There's no level '{level}': {string.Join(", ", LevelNames)}.");
                EditorApplication.Exit(1);
                return;
            }
            bool force = Environment.GetCommandLineArgs().Contains("-force");
            bool ok = Export(level != null ? new[] { level } : LevelNames, (_, _) => force);
            if (Application.isBatchMode) { EditorApplication.Exit(ok ? 0 : 1); }
        }

        // ---- Building from a spec ----

        [MenuItem("Tools/Character Showcase/Level/Specs/Build Level from Spec…")]
        private static void BuildFromMenu()
        {
            string path = EditorUtility.OpenFilePanel("Build Level from Spec", SpecFolder, "json");
            if (string.IsNullOrEmpty(path)) { return; }
            LevelSpec spec;
            try { spec = LevelSpec.FromJson(File.ReadAllText(path)); }
            catch (FormatException e)
            {
                EditorUtility.DisplayDialog("Build Level from Spec", $"'{Path.GetFileName(path)}' isn't a level spec that can be built:\n\n{e.Message}", "OK");
                return;
            }
            LevelBuildWindow.BuildFromMenu(SceneFor(spec));
        }

        /// <summary>For -executeMethod: builds the level in the spec at -spec (its scene, environment and bakes), as its builder would.</summary>
        public static void BuildFromCommandLine()
        {
            string path = Argument("-spec");
            if (path == null)
            {
                Debug.LogError("[Level specs] Give the spec to build with -spec <path>.");
                EditorApplication.Exit(1);
                return;
            }
            SceneFor(LevelSpec.FromJson(File.ReadAllText(path))).Build();
        }

        // ---- The schema ----

        [MenuItem("Tools/Character Showcase/Level/Specs/Write Spec Schema")]
        private static void WriteSchemaFromMenu() => WriteSchema();

        /// <summary>For -executeMethod: writes the specs' JSON Schema.</summary>
        public static void WriteSchemaFromCommandLine() => WriteSchema();

        /// <summary>Writes the specs' JSON Schema (draft 2020-12), from the pieces' parameters, to SchemaPath.</summary>
        public static void WriteSchema()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SchemaPath));
            File.WriteAllText(SchemaPath, LevelSpecJson.WritePretty(Schema()) + "\n");
            Debug.Log($"[Level specs] Wrote the spec schema to '{SchemaPath}' ({LevelSpec.Pieces.Count} pieces).");
        }

        /// <summary>The specs' JSON Schema, as JSON values.</summary>
        public static JsonObject Schema()
        {
            JsonObject Obj(params (string key, object value)[] entries)
            {
                var o = new JsonObject();
                foreach ((string key, object value) in entries) { o[key] = value; }
                return o;
            }
            List<object> L(params object[] items) => items.ToList();
            JsonObject Ref(string name) => Obj(("$ref", "#/$defs/" + name));
            JsonObject Numbers(int n, string description) => Obj(("type", "array"), ("items", Obj(("type", "number"))),
                ("minItems", n), ("maxItems", n), ("description", description));

            JsonObject TypeOf(Type t)
            {
                if (t == typeof(float)) { return Obj(("type", "number")); }
                if (t == typeof(int)) { return Obj(("type", "integer")); }
                if (t == typeof(bool)) { return Obj(("type", "boolean")); }
                if (t == typeof(string)) { return Obj(("type", L("string", "null"))); }
                if (t.IsEnum) { return Ref(t.Name.ToLowerInvariant()); }
                if (t == typeof(Vector2)) { return Ref("point2"); }
                if (t == typeof(Vector3)) { return Ref("point3"); }
                if (t == typeof(UnityEngine.Rect)) { return Ref("area"); }
                if (t == typeof(Graybox.Cell)) { return Ref("cell"); }
                if (t == typeof(System.Random)) { return Ref("random"); }
                if (t == typeof(Transform)) { return Ref("path"); }
                if (t.IsGenericType && t.FullName.StartsWith("System.ValueTuple`"))
                {
                    Type[] items = t.GetGenericArguments();
                    return Obj(("type", "array"), ("prefixItems", items.Select(i => (object)TypeOf(i)).ToList()),
                        ("minItems", items.Length), ("maxItems", items.Length));
                }
                Type element = LevelSpec.ElementOf(t);
                if (element != null) { return Obj(("type", "array"), ("items", TypeOf(element))); }
                throw new NotSupportedException($"No schema for a {t.Name}.");
            }

            var defs = new JsonObject
            {
                ["point2"] = Numbers(2, "A point on the ground plane: [x, z], metres, +z north."),
                ["point3"] = Numbers(3, "A point or size: [x, y, z], metres."),
                ["area"] = Numbers(4, "An area on the ground plane: [xMin, zMin, xMax, zMax], metres."),
                ["path"] = Obj(("type", "string"), ("description",
                    "A parent: its path under the level's root, names separated by '/' ('' is the root), '#n' after the nth (from 2) child with the same name.")),
                ["random"] = Obj(("type", "object"), ("properties", Obj(("seed", Obj(("type", "integer"))))), ("required", L("seed")),
                    ("additionalProperties", false), ("description", "A random number generator, by its seed.")),
                ["cell"] = Obj(("type", "object"), ("description", "A room or passage of an enclosed network (Graybox.Cells): its area, floor and ceiling heights, and whether it's open above."),
                    ("properties", Obj(("name", Obj(("type", "string"))), ("x0", Obj(("type", "number"))), ("z0", Obj(("type", "number"))),
                        ("x1", Obj(("type", "number"))), ("z1", Obj(("type", "number"))), ("floor", Obj(("type", "number"))),
                        ("ceiling", Obj(("type", "number"))), ("open", Obj(("type", "boolean"))))),
                    ("required", L("x0", "z0", "x1", "z1", "floor", "ceiling")), ("additionalProperties", false)),
            };
            foreach (Type e in LevelSpec.Pieces.Values.SelectMany(m => m.GetParameters()).Select(p => p.ParameterType)
                         .Where(t => t.IsEnum).Distinct())
            {
                defs[e.Name.ToLowerInvariant()] = Obj(("enum", Enum.GetNames(e).Cast<object>().ToList()));
            }
            var stateProperties = Obj(("piece", Obj(("const", LevelSpec.StatePiece))));
            foreach ((string key, _, _) in LevelSpec.StateFields) { stateProperties[key] = Obj(("type", "number")); }
            defs["piece." + LevelSpec.StatePiece] = Obj(("type", "object"), ("properties", stateProperties), ("required", L("piece")),
                ("additionalProperties", false), ("description", "Sets the stairs' step size and the decks' and rails' dimensions for the pieces after it in its part."));

            var oneOf = new List<object> { Ref("piece." + LevelSpec.StatePiece) };
            foreach ((string name, MethodInfo method) in LevelSpec.Pieces.OrderBy(p => p.Key, StringComparer.Ordinal))
            {
                var properties = Obj(("piece", Obj(("const", name))));
                var required = new List<object> { "piece" };
                foreach (ParameterInfo p in method.GetParameters())
                {
                    JsonObject type = TypeOf(p.ParameterType);
                    if (p.HasDefaultValue)
                    {
                        object d = LevelSpec.DefaultOf(p);
                        var withDefault = new JsonObject();
                        foreach (string k in type.Keys) { withDefault[k] = type[k]; }
                        withDefault["default"] = LevelSpec.Encode(d, null);
                        type = withDefault;
                    }
                    else if (p.GetCustomAttribute<ParamArrayAttribute>() == null) { required.Add(p.Name); }
                    properties[p.Name] = type;
                }
                defs["piece." + name] = Obj(("type", "object"), ("properties", properties), ("required", required), ("additionalProperties", false));
                oneOf.Add(Ref("piece." + name));
            }
            defs["piece"] = Obj(("oneOf", oneOf));
            defs["part"] = Obj(("type", "object"), ("properties", Obj(("name", Obj(("type", "string"))),
                    ("pieces", Obj(("type", "array"), ("items", Ref("piece")))))),
                ("required", L("name")), ("additionalProperties", false));

            return Obj(
                ("$schema", "https://json-schema.org/draft/2020-12/schema"),
                ("title", "Character Showcase level spec"),
                ("description", "A level as the Graybox pieces it's built from, in order (see LevelSpec). Written by LevelSpecTools from the pieces' parameters: regenerate it, don't edit it."),
                ("type", "object"),
                ("properties", Obj(
                    ("format", Obj(("const", LevelSpec.Format))),
                    ("version", Obj(("type", "integer"), ("maximum", LevelSpec.Version))),
                    ("name", Obj(("type", "string"), ("minLength", 1))),
                    ("scene", Obj(("type", "string"), ("pattern", "^Assets/.+\\.unity$"))),
                    ("parts", Obj(("type", "array"), ("items", Ref("part")))))),
                ("required", L("format", "name", "scene", "parts")),
                ("additionalProperties", false),
                ("$defs", defs));
        }

        private static string Argument(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }
    }
}
