using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MeridianMotion.Characters;
using UnityEditor;
using UnityEngine;

namespace MeridianMotion.EditorTools
{
    /// <summary>
    /// The inspector of a movement tuning preset (MovementTuningPreset): its notes, then each component it sets settings
    /// of, folded under the component's name, with a toggle beside each setting for whether the preset sets it (changing
    /// a value includes it); a menu that adds a component's settings; and buttons that save a movement mode's tuning into
    /// the preset and apply the preset to a mode (by default, the Character prefab's), with Undo, saving the mode so the
    /// movement envelope is written again where the level checks are in the project (see MovementEnvelopeAutoWriter).
    /// </summary>
    [CustomEditor(typeof(MovementTuningPreset))]
    public class MovementTuningPresetEditor : Editor
    {
        private const string PartsField = "parts";
        private const float ButtonWidth = 22f;
        // The mode to save from and apply to, kept while the editor is open (the Character prefab's to begin with).
        private static AbilityMovementMode chosenMode;

        /// <summary>The Character prefab's movement mode, if it's made of components.</summary>
        public static AbilityMovementMode DefaultMode()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterSceneSetup.CharacterPrefabPath);
            return prefab != null && prefab.TryGetComponent(out MMotionMotor movement) ? movement.movementMode as AbilityMovementMode : null;
        }

        /// <summary>Asks where to save a new preset holding all of the mode's tuning now, and saves it there.</summary>
        public static void SaveAsPreset(AbilityMovementMode mode)
        {
            string path = EditorUtility.SaveFilePanelInProject("Save Tuning as Preset", $"{mode.name} Tuning", "asset",
                "Where to save the preset (it holds every setting of every component; untick those it shouldn't set).");
            if (string.IsNullOrEmpty(path)) { return; }
            var preset = CreateInstance<MovementTuningPreset>();
            preset.Capture(mode);
            AssetDatabase.CreateAsset(preset, path);
            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(preset);
            Selection.activeObject = preset;
        }

        public override void OnInspectorGUI()
        {
            var preset = (MovementTuningPreset)target;
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, PartsField, "m_Script");

            EditorGUILayout.Space();
            chosenMode ??= DefaultMode();
            chosenMode = (AbilityMovementMode)EditorGUILayout.ObjectField(new GUIContent("Movement Mode",
                "The movement mode to save the tuning from, or apply the preset to (the Character prefab's, unless another is picked)"),
                chosenMode, typeof(AbilityMovementMode), false);
            using (new EditorGUI.DisabledScope(chosenMode == null))
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("Save From Mode", "Replaces the preset with all of the mode's tuning now")) &&
                    (preset.Parts.Count == 0 || EditorUtility.DisplayDialog("Save From Mode", $"Replace '{preset.name}' with all of '{chosenMode.name}'s tuning?", "Replace", "Cancel")))
                {
                    serializedObject.ApplyModifiedProperties();
                    Undo.RecordObject(preset, "Save Tuning From Mode");
                    preset.Capture(chosenMode);
                    EditorUtility.SetDirty(preset);
                    GUIUtility.ExitGUI();
                }
                if (GUILayout.Button(new GUIContent("Apply To Mode", "Sets the preset's settings on the mode (values and on/off only: never its components' order)")))
                {
                    serializedObject.ApplyModifiedProperties();
                    Apply(preset, chosenMode);
                    GUIUtility.ExitGUI();
                }
            }

            SerializedProperty parts = serializedObject.FindProperty(PartsField);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Settings", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Ticked settings are set by the preset; the rest are left as they are. Changing a value ticks it.", MessageType.None);
            for (int i = 0; i < parts.arraySize; i++)
            {
                if (DrawPart(parts, i)) { break; }
            }
            DrawAddMenu(preset, parts);
            serializedObject.ApplyModifiedProperties();
        }

        /// <summary>Applies the preset to the mode, with Undo, reporting what it changed and couldn't set.</summary>
        private static void Apply(MovementTuningPreset preset, AbilityMovementMode mode)
        {
            Undo.RecordObject(mode, $"Apply {preset.name}");
            var problems = new List<string>();
            int changed = preset.ApplyTo(mode, problems);
            EditorUtility.SetDirty(mode);
            AssetDatabase.SaveAssetIfDirty(mode);
            string report = $"Applied '{preset.name}' to '{mode.name}': {changed} setting{(changed == 1 ? "" : "s")} changed.";
            if (problems.Count > 0) { report += "\n\nLeft out:\n" + string.Join("\n", problems); }
            Debug.Log($"[Tuning preset] {report}", mode);
            EditorUtility.DisplayDialog("Apply To Mode", report, "OK");
        }

        /// <summary>Draws the part at index; returns whether the list changed (and drawing should stop for this frame).</summary>
        private static bool DrawPart(SerializedProperty parts, int index)
        {
            SerializedProperty part = parts.GetArrayElementAtIndex(index);
            SerializedProperty values = part.FindPropertyRelative("values");
            SerializedProperty settings = part.FindPropertyRelative("settings");
            Type type = values.managedReferenceValue?.GetType();
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    string title = type == null ? "Missing component (its type was renamed or deleted)" : $"{MovementComponent.DisplayNameOf(type)} ({settings.arraySize})";
                    part.isExpanded = EditorGUILayout.Foldout(part.isExpanded, title, true, EditorStyles.foldoutHeader);
                    if (GUILayout.Button(new GUIContent("✕", "Leave this component's settings out of the preset"), GUILayout.Width(ButtonWidth)))
                    {
                        parts.DeleteArrayElementAtIndex(index);
                        return true;
                    }
                }
                if (type == null || !part.isExpanded) { return false; }

                var included = new HashSet<string>();
                for (int i = 0; i < settings.arraySize; i++) { included.Add(settings.GetArrayElementAtIndex(i).stringValue); }
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("All", EditorStyles.miniButtonLeft)) { SetAll(settings, type, true); return true; }
                    if (GUILayout.Button("None", EditorStyles.miniButtonRight)) { SetAll(settings, type, false); return true; }
                }
                foreach (FieldInfo setting in MovementSettings.Of(type))
                {
                    SerializedProperty value = values.FindPropertyRelative(setting.Name);
                    if (value == null) { continue; }
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        bool wasIncluded = included.Contains(setting.Name);
                        bool include = EditorGUILayout.Toggle(wasIncluded, GUILayout.Width(16f));
                        string label = setting.Name == MovementSettings.EnabledSetting ? "Turned On" : value.displayName;
                        using (new EditorGUI.DisabledScope(!include))
                        {
                            EditorGUI.BeginChangeCheck();
                            EditorGUILayout.PropertyField(value, new GUIContent(label, value.tooltip), true);
                            if (EditorGUI.EndChangeCheck()) { include = true; }
                        }
                        if (include != wasIncluded) { SetIncluded(settings, setting.Name, include); }
                    }
                }
            }
            return false;
        }

        private static void SetIncluded(SerializedProperty settings, string setting, bool include)
        {
            for (int i = 0; i < settings.arraySize; i++)
            {
                if (settings.GetArrayElementAtIndex(i).stringValue != setting) { continue; }
                if (!include) { settings.DeleteArrayElementAtIndex(i); }
                return;
            }
            if (!include) { return; }
            settings.arraySize++;
            settings.GetArrayElementAtIndex(settings.arraySize - 1).stringValue = setting;
        }

        private static void SetAll(SerializedProperty settings, Type type, bool include)
        {
            settings.ClearArray();
            if (!include) { return; }
            foreach (FieldInfo setting in MovementSettings.Of(type))
            {
                settings.arraySize++;
                settings.GetArrayElementAtIndex(settings.arraySize - 1).stringValue = setting.Name;
            }
        }

        /// <summary>A menu that adds a component's settings to the preset (from the chosen mode's component if it has one, else its defaults), none ticked.</summary>
        private static void DrawAddMenu(MovementTuningPreset preset, SerializedProperty parts)
        {
            Type[] addable = TypeCache.GetTypesDerivedFrom<MovementComponent>()
                .Where(t => !t.IsAbstract && !t.IsGenericType && t.GetConstructor(Type.EmptyTypes) != null && preset.PartFor(t) == null)
                .OrderBy(MovementComponent.DisplayNameOf).ToArray();
            using (new EditorGUI.DisabledScope(addable.Length == 0))
            {
                if (!GUILayout.Button("Add Component's Settings")) { return; }
            }
            var menu = new GenericMenu();
            SerializedObject owner = parts.serializedObject;
            foreach (Type type in addable)
            {
                menu.AddItem(new GUIContent(MovementComponent.DisplayNameOf(type)), false, () =>
                {
                    owner.Update();
                    SerializedProperty list = owner.FindProperty(PartsField);
                    list.arraySize++;
                    SerializedProperty part = list.GetArrayElementAtIndex(list.arraySize - 1);
                    MovementComponent from = chosenMode != null ? chosenMode.Get(type, includeDisabled: true) : null;
                    part.FindPropertyRelative("values").managedReferenceValue = from != null ? MovementSettings.Clone(from) : (MovementComponent)Activator.CreateInstance(type);
                    part.FindPropertyRelative("settings").ClearArray();
                    part.isExpanded = true;
                    owner.ApplyModifiedProperties();
                });
            }
            menu.ShowAsContext();
        }
    }
}
