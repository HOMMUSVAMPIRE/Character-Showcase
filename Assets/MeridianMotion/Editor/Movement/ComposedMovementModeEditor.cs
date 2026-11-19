using System;
using System.Collections.Generic;
using System.Linq;
using MeridianMotion.Characters;
using UnityEditor;
using UnityEngine;

namespace MeridianMotion.EditorTools
{
    /// <summary>
    /// The inspector of a movement mode made of components (AbilityMovementMode): its own settings, then its components
    /// in order, each folded under its name with a toggle that turns it on and off, buttons that move it up and down the
    /// list (higher takes over the character first) and one that removes it, and a menu that adds any component the mode
    /// doesn't have yet; a warning, with buttons that add them, if it lacks any of its code's default components (see
    /// MovementModeSync); and a button that saves its tuning as a preset (see MovementTuningPresetEditor).
    /// </summary>
    [CustomEditor(typeof(AbilityMovementMode), true)]
    public class ComposedMovementModeEditor : Editor
    {
        private const string ComponentsField = "components";
        private const string EnabledField = "enabled";
        private const float ButtonWidth = 22f;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, ComponentsField);

            SerializedProperty list = serializedObject.FindProperty(ComponentsField);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Components", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Where several components could take over the character in a step, the one higher in the list does.", MessageType.None);
            for (int i = 0; i < list.arraySize; i++)
            {
                if (DrawComponent(list, i)) { break; }
            }
            DrawAddMenu(list);
            serializedObject.ApplyModifiedProperties();
            DrawMissingComponents((AbilityMovementMode)target);

            EditorGUILayout.Space();
            if (GUILayout.Button("Save Tuning as Preset…")) { MovementTuningPresetEditor.SaveAsPreset((AbilityMovementMode)target); }
        }

        /// <summary>Draws the component at index; returns whether the list changed (and drawing should stop for this frame).</summary>
        private static bool DrawComponent(SerializedProperty list, int index)
        {
            SerializedProperty element = list.GetArrayElementAtIndex(index);
            object value = element.managedReferenceValue;
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (value == null)
                    {
                        EditorGUILayout.LabelField("Missing component (its type was renamed or deleted)", EditorStyles.boldLabel);
                    }
                    else
                    {
                        SerializedProperty enabled = element.FindPropertyRelative(EnabledField);
                        if (enabled != null) { enabled.boolValue = EditorGUILayout.Toggle(enabled.boolValue, GUILayout.Width(16f)); }
                        element.isExpanded = EditorGUILayout.Foldout(element.isExpanded, MovementComponent.DisplayNameOf(value.GetType()), true, EditorStyles.foldoutHeader);
                    }
                    using (new EditorGUI.DisabledScope(index == 0))
                    {
                        if (GUILayout.Button("▲", GUILayout.Width(ButtonWidth))) { list.MoveArrayElement(index, index - 1); return true; }
                    }
                    using (new EditorGUI.DisabledScope(index == list.arraySize - 1))
                    {
                        if (GUILayout.Button("▼", GUILayout.Width(ButtonWidth))) { list.MoveArrayElement(index, index + 1); return true; }
                    }
                    if (GUILayout.Button("✕", GUILayout.Width(ButtonWidth)))
                    {
                        list.DeleteArrayElementAtIndex(index);
                        return true;
                    }
                }
                if (value == null || !element.isExpanded) { return false; }

                using (new EditorGUI.IndentLevelScope())
                using (new EditorGUI.DisabledScope(!element.FindPropertyRelative(EnabledField).boolValue))
                {
                    SerializedProperty child = element.Copy();
                    SerializedProperty end = element.GetEndProperty();
                    for (bool more = child.NextVisible(true); more && !SerializedProperty.EqualContents(child, end); more = child.NextVisible(false))
                    {
                        if (child.name != EnabledField) { EditorGUILayout.PropertyField(child, true); }
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// If the mode lacks any of its code's default components (a new ability, added to the code after the asset was
        /// made), says which, with buttons that add them at their default places, turned on or off (MovementModeSync).
        /// </summary>
        private void DrawMissingComponents(AbilityMovementMode mode)
        {
            List<Type> missing = mode.MissingDefaultComponentTypes();
            if (missing.Count == 0) { return; }
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                $"This mode doesn't have {(missing.Count == 1 ? "a default component" : missing.Count + " default components")}: " +
                string.Join(", ", missing.Select(MovementComponent.DisplayNameOf)) + ". A tuning preset can't add a " +
                "component, so add them here: each goes at its default place in the list. Turn them off for a character " +
                "whose body or animations don't suit them yet.", MessageType.Warning);
            using (new EditorGUILayout.HorizontalScope())
            {
                bool on = GUILayout.Button("Add Missing Components, Turned On");
                bool off = GUILayout.Button("Add Them Turned Off");
                if (!on && !off) { return; }
                MovementModeSync.AddMissing(mode, enabled: on);
                serializedObject.Update();
                GUIUtility.ExitGUI();
            }
        }

        /// <summary>A button with a menu of the components the mode doesn't have yet, which adds the one picked at the end.</summary>
        private static void DrawAddMenu(SerializedProperty list)
        {
            var present = new HashSet<Type>();
            for (int i = 0; i < list.arraySize; i++)
            {
                object value = list.GetArrayElementAtIndex(i).managedReferenceValue;
                if (value != null) { present.Add(value.GetType()); }
            }
            Type[] addable = TypeCache.GetTypesDerivedFrom<MovementComponent>()
                .Where(t => !t.IsAbstract && !t.IsGenericType && t.GetConstructor(Type.EmptyTypes) != null && !present.Contains(t))
                .OrderBy(MovementComponent.DisplayNameOf).ToArray();
            using (new EditorGUI.DisabledScope(addable.Length == 0))
            {
                if (!GUILayout.Button("Add Component")) { return; }
            }
            var menu = new GenericMenu();
            SerializedObject owner = list.serializedObject;
            foreach (Type type in addable)
            {
                menu.AddItem(new GUIContent(MovementComponent.DisplayNameOf(type)), false, () =>
                {
                    owner.Update();
                    SerializedProperty components = owner.FindProperty(ComponentsField);
                    components.arraySize++;
                    components.GetArrayElementAtIndex(components.arraySize - 1).managedReferenceValue = Activator.CreateInstance(type);
                    owner.ApplyModifiedProperties();
                });
            }
            menu.ShowAsContext();
        }
    }
}
