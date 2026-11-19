using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MeridianMotion.Showcase.Levels
{
    // The window's inspector: a part's or piece's settings, each edited as its piece's parameter type.
    public partial class LevelEditorWindow
    {
        // ---- The inspector ----

        private void Inspector()
        {
            inspectorScroll = EditorGUILayout.BeginScrollView(inspectorScroll);
            EditorGUIUtility.labelWidth = 110f;
            if (selected == null && selectedPart >= 0 && selectedPart < spec.Parts.Count) { PartInspector(spec.Parts[selectedPart]); }
            else if (selected != null) { PieceInspector(selected); }
            else { EditorGUILayout.HelpBox("Pick a piece in the list, on the plan, or in the Scene view.", MessageType.None); }
            EditorGUILayout.EndScrollView();
        }

        private void PartInspector(LevelSpec.Part part)
        {
            EditorGUILayout.LabelField("Part", EditorStyles.boldLabel);
            string name = EditorGUILayout.DelayedTextField("Name", part.Name);
            if (name != part.Name && !string.IsNullOrWhiteSpace(name))
            {
                part.Name = name;
                Edited("Rename Part", all: false);
            }
            EditorGUILayout.LabelField("Pieces", part.Pieces.Count.ToString());
            EditorGUILayout.HelpBox("A part is built as one step of the level's build, starting from the generation settings' step size and deck dimensions.", MessageType.None);
            using (new EditorGUILayout.HorizontalScope())
            {
                int i = spec.Parts.IndexOf(part);
                if (GUILayout.Button("Move Up") && i > 0) { (spec.Parts[i], spec.Parts[i - 1]) = (spec.Parts[i - 1], spec.Parts[i]); selectedPart = i - 1; Edited("Move Part", all: true); }
                if (GUILayout.Button("Move Down") && i < spec.Parts.Count - 1) { (spec.Parts[i], spec.Parts[i + 1]) = (spec.Parts[i + 1], spec.Parts[i]); selectedPart = i + 1; Edited("Move Part", all: true); }
                if (GUILayout.Button("Delete")) { DeletePart(part); }
            }
        }

        private void PieceInspector(JsonObject piece)
        {
            string kind = piece["piece"] as string;
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(kind, EditorStyles.boldLabel);
                if (GUILayout.Button(new GUIContent("Frame", "Show it in the Scene view and the plan"), GUILayout.Width(52f))) { Frame(); }
                if (GUILayout.Button(new GUIContent("Duplicate", "Duplicate it (Ctrl/Cmd+D)"), GUILayout.Width(70f))) { Duplicate(); }
                if (GUILayout.Button(new GUIContent("Delete", "Delete it (Delete)"), GUILayout.Width(52f))) { Delete(); return; }
            }
            string problem = preview != null && preview.Problems.TryGetValue(piece, out string p) ? p : LevelSpec.Problem(piece);
            if (problem != null) { EditorGUILayout.HelpBox(problem, MessageType.Error); }

            if (kind == LevelSpec.StatePiece)
            {
                EditorGUILayout.HelpBox("Sets these for the pieces after it in its part.", MessageType.None);
                foreach ((string key, _, _) in LevelSpec.StateFields)
                {
                    bool has = piece.TryGetValue(key, out object v);
                    float value = has ? (float)(double)v : 0f;
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUI.BeginChangeCheck();
                        float changed = EditorGUILayout.DelayedFloatField(ObjectNames.NicifyVariableName(key), value);
                        if (EditorGUI.EndChangeCheck()) { piece[key] = (double)changed; Edited("Edit State", piece, all: true); }
                        if (has && GUILayout.Button("×", GUILayout.Width(20f))) { piece.Remove(key); Edited("Edit State", piece, all: true); }
                    }
                }
                return;
            }
            MethodInfo method = LevelSpecEdit.MethodOf(piece);
            if (method == null) { return; }
            foreach (ParameterInfo parameter in method.GetParameters()) { Setting(piece, parameter); }
            if (textError != null) { EditorGUILayout.HelpBox(textError, MessageType.Error); }
        }

        private void Setting(JsonObject piece, ParameterInfo p)
        {
            Type t = p.ParameterType;
            bool has = piece.ContainsKey(p.Name);
            object value = LevelSpecEdit.Get(piece, p);
            var label = new GUIContent(ObjectNames.NicifyVariableName(p.Name),
                LevelSpec.Describe(t) + (p.HasDefaultValue ? $" (default {LevelSpecJson.Write(LevelSpec.Encode(LevelSpec.DefaultOf(p), null))})" : ""));
            bool all = piece["piece"] is "Group" or "Placed" or "Turned";
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(false))
                {
                    GUI.color = has || !p.HasDefaultValue ? Color.white : new Color(1f, 1f, 1f, 0.6f);
                    EditorGUI.BeginChangeCheck();
                    object changed = Field(label, t, value, p, piece);
                    GUI.color = Color.white;
                    if (EditorGUI.EndChangeCheck() && changed != null)
                    {
                        if (changed is JsonText text) { piece[p.Name] = text.json; LevelSpecEdit.Tidy(piece); }
                        else { LevelSpecEdit.Set(piece, p, changed); }
                        Edited($"Edit {ObjectNames.NicifyVariableName(p.Name)}", piece, all);
                    }
                }
                if (has && p.HasDefaultValue && GUILayout.Button(new GUIContent("×", "Back to its default"), GUILayout.Width(20f)))
                {
                    piece.Remove(p.Name);
                    Edited($"Reset {ObjectNames.NicifyVariableName(p.Name)}", piece, all);
                }
            }
        }

        /// <summary>A setting edited as JSON text, applied once it reads.</summary>
        private sealed class JsonText
        {
            public object json;
        }

        private object Field(GUIContent label, Type t, object value, ParameterInfo p, JsonObject piece)
        {
            if (t == typeof(Transform))
            {
                string current = value as string ?? "";
                List<string> paths = preview != null && preview.IsOpen ? preview.GroupPaths() : new List<string> { "" };
                if (!paths.Contains(current)) { paths.Insert(0, current); }
                int i = EditorGUILayout.Popup(label, paths.IndexOf(current), paths.Select(x => x == "" ? "(the root)" : x.Replace("/", " ∕ ")).ToArray());
                return i >= 0 && paths[i] != current ? paths[i] : null;
            }
            if (t == typeof(float)) { return EditorGUILayout.DelayedFloatField(label, value is float f ? f : 0f); }
            if (t == typeof(int)) { return EditorGUILayout.DelayedIntField(label, value is int n ? n : 0); }
            if (t == typeof(bool)) { return EditorGUILayout.Toggle(label, value is true); }
            if (t == typeof(string)) { return EditorGUILayout.DelayedTextField(label, value as string ?? ""); }
            if (t.IsEnum) { return EditorGUILayout.EnumPopup(label, value as Enum ?? (Enum)Enum.GetValues(t).GetValue(0)); }
            if (t == typeof(Vector2))
            {
                var v = value is Vector2 v2 ? v2 : Vector2.zero;
                float[] c = Numbers(label, new[] { v.x, v.y }, "X", "Z");
                return c == null ? null : new Vector2(c[0], c[1]);
            }
            if (t == typeof(Vector3))
            {
                var v = value is Vector3 v3 ? v3 : Vector3.zero;
                float[] c = Numbers(label, new[] { v.x, v.y, v.z }, "X", "Y", "Z");
                return c == null ? null : new Vector3(c[0], c[1], c[2]);
            }
            if (t == typeof(UnityEngine.Rect))
            {
                var r = value is UnityEngine.Rect rect ? rect : default;
                float[] c = Numbers(label, new[] { r.xMin, r.yMin, r.xMax, r.yMax }, "X0", "Z0", "X1", "Z1");
                return c == null ? null : UnityEngine.Rect.MinMaxRect(c[0], c[1], c[2], c[3]);
            }
            // Lists, cells, tuples and randoms: as JSON.
            string key = p.Name;
            string shown = textEdits.TryGetValue(key, out string edit) ? edit
                : piece.TryGetValue(p.Name, out object json) ? LevelSpecJson.Write(json) : LevelSpecJson.Write(LevelSpec.Encode(value, null));
            using (new EditorGUILayout.VerticalScope())
            {
                EditorGUILayout.LabelField(label);
                string typed = EditorGUILayout.TextArea(shown, new GUIStyle(EditorStyles.textArea) { wordWrap = true }, GUILayout.MinHeight(34f));
                if (typed != shown) { textEdits[key] = typed; }
                if (textEdits.ContainsKey(key) && GUILayout.Button("Apply", GUILayout.Width(60f)))
                {
                    try
                    {
                        object parsed = LevelSpecJson.Parse(typed);
                        LevelSpec.Decode(parsed, t, null);
                        textEdits.Remove(key);
                        textError = null;
                        GUI.changed = true;
                        return new JsonText { json = parsed };
                    }
                    catch (Exception e) when (e is FormatException or InvalidCastException)
                    {
                        textError = $"{ObjectNames.NicifyVariableName(p.Name)}: {(e is FormatException ? e.Message : $"that isn't {LevelSpec.Describe(t)}.")}";
                    }
                }
            }
            return null;
        }

        /// <summary>A row of number fields that change once typed (not each keystroke); null if none did.</summary>
        private static float[] Numbers(GUIContent label, float[] values, params string[] names)
        {
            Rect r = EditorGUILayout.GetControlRect();
            r = EditorGUI.PrefixLabel(r, label);
            float w = r.width / values.Length;
            float[] result = null;
            int indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;
            float labelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 16f;
            for (int i = 0; i < values.Length; i++)
            {
                EditorGUI.BeginChangeCheck();
                float v = EditorGUI.DelayedFloatField(new Rect(r.x + i * w, r.y, w - 2f, r.height), names[i], values[i]);
                if (EditorGUI.EndChangeCheck())
                {
                    result ??= (float[])values.Clone();
                    result[i] = v;
                }
            }
            EditorGUIUtility.labelWidth = labelWidth;
            EditorGUI.indentLevel = indent;
            return result;
        }
    }
}
