using System.Collections.Generic;
using MeridianMotion.Characters;
using UnityEditor;
using UnityEngine;

namespace MeridianMotion.EditorTools
{
    /// <summary>
    /// Shows an AnimatorParameterTranslator's event trigger with its events as a menu of movement events to tick, by name
    /// (MovementEvents.Named): MovementEvents is a set of 256 flags, so without this it'd show as its four words.
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
            var mask = (MovementEvents)events.boxedValue;
            if (EditorGUI.DropdownButton(field, new GUIContent(Describe(mask)), FocusType.Keyboard))
            {
                // The menu outlives this call: it changes the property found again by its path.
                SerializedObject owner = events.serializedObject;
                string path = events.propertyPath;
                var menu = new GenericMenu();
                foreach ((string name, MovementEvents movementEvent) in MovementEvents.Named)
                {
                    menu.AddItem(new GUIContent(ObjectNames.NicifyVariableName(name)), mask.HasAny(movementEvent), () =>
                    {
                        owner.Update();
                        SerializedProperty toggled = owner.FindProperty(path);
                        toggled.boxedValue = (MovementEvents)toggled.boxedValue ^ movementEvent;
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
            foreach ((string name, MovementEvents movementEvent) in MovementEvents.Named)
            {
                if (mask.HasAny(movementEvent)) { names.Add(ObjectNames.NicifyVariableName(name)); }
            }
            return string.Join(", ", names);
        }
    }
}
