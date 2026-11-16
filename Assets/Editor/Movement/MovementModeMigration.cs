using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Character;
using UnityEditor;
using UnityEngine;

namespace CharacterShowcase.EditorTools
{
    /// <summary>
    /// Moves a movement mode asset's settings into its components, after settings have moved from the mode into a
    /// component in code (as BasicMovementMode's abilities are, a few at a time). Unity drops a saved value whose field
    /// has gone, and FormerlySerializedAs can't follow a field into another object, so this reads the values still in the
    /// asset's file and sets the component fields of the same name (component fields keep the names they had on the mode).
    /// A mode saved before it had components gets its default components first; one that has components but lacks a
    /// default component whose settings are still saved on the mode gets that component, at its default place in the
    /// order (one removed on purpose has no saved settings left, so it isn't added back). Single values (numbers, toggles,
    /// choices) and curves are carried over: anything else that moves (a list, say) needs adding here. Run it once after such a change
    /// (Tools > Character Showcase > Movement > Move Settings Into Components, or -executeMethod
    /// CharacterShowcase.EditorTools.MovementModeMigration.MigrateFromCommandLine), then check the asset's diff.
    /// </summary>
    public static class MovementModeMigration
    {
        // A setting of the mode itself in its asset file: two spaces in (under "MonoBehaviour:"), a name and a single value.
        private static readonly Regex TopLevelValue = new(@"^  (?!m_)(\w+): ?(\S.*)?$", RegexOptions.Multiline);
        // A setting of the mode itself saved as a block (a curve): its name, then the lines indented under it.
        private static readonly Regex TopLevelBlock = new(@"^  (?!m_)(\w+):[ \t]*\r?\n((?:    .*\r?\n)+)", RegexOptions.Multiline);
        // One key of a saved curve, and a number in it.
        private static readonly Regex CurveKey = new(@"^    - serializedVersion: \d+\r?\n((?:      .*\r?\n)+)", RegexOptions.Multiline);
        private static readonly Regex CurveValue = new(@"^\s*(\w+): (\S+)", RegexOptions.Multiline);

        [MenuItem("Tools/Character Showcase/Movement/Move Settings Into Components")]
        private static void MigrateFromMenu()
        {
            EditorUtility.DisplayDialog("Move Settings Into Components", Migrate(), "OK");
        }

        /// <summary>For -executeMethod.</summary>
        public static void MigrateFromCommandLine()
        {
            Debug.Log($"[Movement mode migration]\n{Migrate()}");
            if (Application.isBatchMode) { EditorApplication.Exit(0); }
        }

        /// <summary>Migrates every ComposedMovementMode asset, returning a report of what it set.</summary>
        public static string Migrate()
        {
            var report = new StringBuilder();
            foreach (string guid in AssetDatabase.FindAssets("t:ComposedMovementMode"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var mode = AssetDatabase.LoadAssetAtPath<ComposedMovementMode>(path);
                if (mode == null) { continue; }
                string text = File.ReadAllText(path);
                report.AppendLine(path);
                if (!Regex.IsMatch(text, @"^  components:", RegexOptions.Multiline))
                {
                    mode.ResetComponents();
                    report.AppendLine("  saved before it had components: given its default components");
                }

                var saved = new Dictionary<string, string>();
                foreach (Match match in TopLevelValue.Matches(text))
                {
                    if (match.Groups[2].Success) { saved[match.Groups[1].Value] = match.Groups[2].Value.Trim(); }
                }
                foreach (Match match in TopLevelBlock.Matches(text)) { saved[match.Groups[1].Value] = match.Groups[2].Value; }
                AddComponentsWithSavedSettings(mode, saved, report);

                var serialized = new SerializedObject(mode);
                SerializedProperty list = serialized.FindProperty("components");
                for (int i = 0; i < list.arraySize; i++)
                {
                    SerializedProperty element = list.GetArrayElementAtIndex(i);
                    if (element.managedReferenceValue == null) { continue; }
                    string componentName = element.managedReferenceValue.GetType().Name;
                    SerializedProperty child = element.Copy();
                    SerializedProperty end = element.GetEndProperty();
                    for (bool more = child.Next(true); more && !SerializedProperty.EqualContents(child, end); more = child.Next(false))
                    {
                        if (!saved.TryGetValue(child.name, out string value)) { continue; }
                        if (Set(child, value)) { report.AppendLine($"  {componentName}.{child.name} = {Describe(child, value)}"); }
                        else { report.AppendLine($"  {componentName}.{child.name}: can't carry over '{value}' ({child.propertyType}): set it by hand"); }
                    }
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(mode);
            }
            AssetDatabase.SaveAssets();
            return report.ToString();
        }

        /// <summary>
        /// Adds each default component the mode lacks whose settings are saved on the mode, after the components that come
        /// before it by default.
        /// </summary>
        private static void AddComponentsWithSavedSettings(ComposedMovementMode mode, Dictionary<string, string> saved, StringBuilder report)
        {
            const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            Type[] defaults = mode.DefaultComponentTypes().ToArray();
            for (int d = 0; d < defaults.Length; d++)
            {
                Type type = defaults[d];
                if (mode.Components.Any(c => c != null && c.GetType() == type)) { continue; }
                bool hasSaved = type.GetFields(Fields).Any(f => !f.IsNotSerialized && (f.IsPublic || f.IsDefined(typeof(SerializeField), false)) && saved.ContainsKey(f.Name));
                if (!hasSaved) { continue; }
                // After the last component the mode has that comes before this one by default.
                int index = 0;
                for (int i = 0; i < mode.Components.Count; i++)
                {
                    MovementComponent component = mode.Components[i];
                    if (component != null && Array.IndexOf(defaults, component.GetType(), 0, d) >= 0) { index = i + 1; }
                }
                mode.Insert(index, (MovementComponent)Activator.CreateInstance(type));
                report.AppendLine($"  added {type.Name} (its settings were saved on the mode)");
            }
        }

        private static string Describe(SerializedProperty property, string value) =>
            property.propertyType == SerializedPropertyType.AnimationCurve ? $"a curve of {property.animationCurveValue.length} keys" : value;

        /// <summary>A curve as Unity saves it (its keys, and how it carries on before and after them).</summary>
        private static bool TryParseCurve(string block, out AnimationCurve curve)
        {
            curve = null;
            if (!block.Contains("m_Curve:")) { return false; }
            CultureInfo invariant = CultureInfo.InvariantCulture;
            var keys = new List<Keyframe>();
            foreach (Match key in CurveKey.Matches(block))
            {
                var values = new Dictionary<string, string>();
                foreach (Match value in CurveValue.Matches(key.Groups[1].Value)) { values[value.Groups[1].Value] = value.Groups[2].Value; }
                float F(string name) => values.TryGetValue(name, out string v) ? float.Parse(v, NumberStyles.Float, invariant) : 0f;
                var frame = new Keyframe(F("time"), F("value"), F("inSlope"), F("outSlope"), F("inWeight"), F("outWeight"))
                {
                    weightedMode = (WeightedMode)(int)F("weightedMode"),
                };
#pragma warning disable CS0618 // tangentMode is how Unity saves a key's tangents; there's no other way to set it all at once.
                frame.tangentMode = (int)F("tangentMode");
#pragma warning restore CS0618
                keys.Add(frame);
            }
            curve = new AnimationCurve(keys.ToArray());
            Match pre = Regex.Match(block, @"m_PreInfinity: (\d+)"), post = Regex.Match(block, @"m_PostInfinity: (\d+)");
            if (pre.Success) { curve.preWrapMode = SavedWrapMode(int.Parse(pre.Groups[1].Value, invariant)); }
            if (post.Success) { curve.postWrapMode = SavedWrapMode(int.Parse(post.Groups[1].Value, invariant)); }
            return true;
        }

        /// <summary>
        /// How a curve carries on past its keys, from how Unity saves it: its own numbering (0 ping-pong, 1 loop, 2 clamp),
        /// not WrapMode's.
        /// </summary>
        private static WrapMode SavedWrapMode(int saved) => saved switch
        {
            0 => WrapMode.PingPong,
            1 => WrapMode.Loop,
            _ => WrapMode.ClampForever,
        };

        private static bool Set(SerializedProperty property, string value)
        {
            CultureInfo invariant = CultureInfo.InvariantCulture;
            switch (property.propertyType)
            {
                case SerializedPropertyType.Float when float.TryParse(value, NumberStyles.Float, invariant, out float f):
                    property.floatValue = f;
                    return true;
                case SerializedPropertyType.Integer when long.TryParse(value, NumberStyles.Integer, invariant, out long n):
                    property.longValue = n;
                    return true;
                case SerializedPropertyType.Boolean when value is "0" or "1":
                    property.boolValue = value == "1";
                    return true;
                case SerializedPropertyType.Enum when int.TryParse(value, NumberStyles.Integer, invariant, out int e):
                    property.intValue = e;
                    return true;
                case SerializedPropertyType.AnimationCurve when TryParseCurve(value, out AnimationCurve curve):
                    property.animationCurveValue = curve;
                    return true;
                default:
                    return false;
            }
        }
    }
}
