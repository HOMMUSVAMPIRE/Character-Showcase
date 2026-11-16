using System;
using System.Collections.Generic;
using System.Linq;
using Character;
using UnityEditor;
using UnityEngine;

namespace CharacterShowcase.EditorTools
{
    /// <summary>
    /// The inspector of a movement mode made of components (ComposedMovementMode): its own settings, then its components
    /// in order, each folded under its name with a toggle that turns it on and off, buttons that move it up and down the
    /// list (higher takes over the character first) and one that removes it, and a menu that adds any component the mode
    /// doesn't have yet; and a button that saves its tuning as a preset (see MovementTuningPresetEditor).
    /// </summary>
    [CustomEditor(typeof(ComposedMovementMode), true)]
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

            EditorGUILayout.Space();
            if (GUILayout.Button("Save Tuning as Preset…")) { MovementTuningPresetEditor.SaveAsPreset((ComposedMovementMode)target); }
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
