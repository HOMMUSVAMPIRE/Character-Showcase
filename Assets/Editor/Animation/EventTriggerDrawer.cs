using System;
using System.Collections.Generic;
using Character;
using UnityEditor;
using UnityEngine;

namespace CharacterShowcase.EditorTools
{
    /// <summary>
    /// Shows an AnimatorParameterTranslator's event trigger with its events as a menu of movement events to tick. The
    /// events are saved as a long (MovementEvents is 64 bits wide, which Unity doesn't save as an enum, and its own
    /// flags field only goes to 32 bits), so without this they'd show as a bare number.
    /// </summary>
    [CustomPropertyDrawer(typeof(AnimatorParameterTranslator.EventTrigger))]
    public class EventTriggerDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) =>
            2f * EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            SerializedProperty events = property.FindPropertyRelative("events");
            SerializedProperty trigger = property.FindPropertyRelative("trigger");
            EditorGUI.BeginProperty(position, label, property);
            var line = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            Rect field = EditorGUI.PrefixLabel(line, new GUIContent("Events", "The movement events that fire the trigger"));
            var mask = (MovementEvents)events.longValue;
            if (EditorGUI.DropdownButton(field, new GUIContent(Describe(mask)), FocusType.Keyboard))
            {
                // The menu outlives this call: it changes the property found again by its path.
                SerializedObject owner = events.serializedObject;
                string path = events.propertyPath;
                var menu = new GenericMenu();
                foreach (MovementEvents movementEvent in Enum.GetValues(typeof(MovementEvents)))
                {
                    if (movementEvent == MovementEvents.None) { continue; }
                    menu.AddItem(new GUIContent(ObjectNames.NicifyVariableName(movementEvent.ToString())), (mask & movementEvent) != 0, () =>
                    {
                        owner.Update();
                        SerializedProperty toggled = owner.FindProperty(path);
                        toggled.longValue ^= (long)movementEvent;
                        owner.ApplyModifiedProperties();
                    });
                }
                menu.DropDown(field);
            }
            line.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            EditorGUI.PropertyField(line, trigger);
            EditorGUI.EndProperty();
        }

        private static string Describe(MovementEvents mask)
        {
            if (mask == MovementEvents.None) { return "Nothing"; }
            var names = new List<string>();
            foreach (MovementEvents movementEvent in Enum.GetValues(typeof(MovementEvents)))
            {
                if (movementEvent != MovementEvents.None && (mask & movementEvent) != 0) { names.Add(ObjectNames.NicifyVariableName(movementEvent.ToString())); }
            }
            return string.Join(", ", names);
        }
    }
}
