using System;
using System.Collections.Generic;
using System.Linq;
using Character;
using UnityEditor;
using UnityEngine;

namespace CharacterShowcase.EditorTools
{
    /// <summary>
    /// Shows an AnimatorParameterTranslator's float parameter with its source as a menu: the animation state's own values
    /// (AnimatorParameterTranslator.AnimationFloats), then every value the movement components publish (their
    /// AbilityValueNames: Ability: …), so an ability's value is shown by picking it, with no code. The source is saved as
    /// text, as a bool parameter's is (see BoolParameterDrawer).
    /// </summary>
    [CustomPropertyDrawer(typeof(AnimatorParameterTranslator.FloatParameter))]
    public class FloatParameterDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) =>
            3f * EditorGUIUtility.singleLineHeight + 2f * EditorGUIUtility.standardVerticalSpacing;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            SerializedProperty source = property.FindPropertyRelative(nameof(AnimatorParameterTranslator.FloatParameter.source));
            SerializedProperty parameter = property.FindPropertyRelative(nameof(AnimatorParameterTranslator.FloatParameter.parameter));
            SerializedProperty smoothed = property.FindPropertyRelative(nameof(AnimatorParameterTranslator.FloatParameter.smoothed));
            EditorGUI.BeginProperty(position, label, property);
            var line = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            Rect field = EditorGUI.PrefixLabel(line, new GUIContent("Shows", "What the parameter shows: a value of the animation " +
                "state, worked out for how the animation should look, or a value an ability publishes"));
            bool known = AnimatorParameterTranslator.TryResolveFloat(source.stringValue, out _, out _);
            string shown = string.IsNullOrEmpty(source.stringValue) ? "Nothing" : known ? source.stringValue : $"{source.stringValue} (unknown)";
            if (EditorGUI.DropdownButton(field, new GUIContent(shown), FocusType.Keyboard))
            {
                // The menu outlives this call: it changes the property found again by its path.
                SerializedObject owner = source.serializedObject;
                string path = source.propertyPath;
                string current = source.stringValue;
                var menu = new GenericMenu();
                void Add(string menuPath, string value) => menu.AddItem(new GUIContent(menuPath), value == current, () =>
                {
                    owner.Update();
                    owner.FindProperty(path).stringValue = value;
                    owner.ApplyModifiedProperties();
                });
                foreach ((string name, _) in AnimatorParameterTranslator.AnimationFloats) { Add("Animation State/" + name, name); }
                foreach (string name in PublishedValueNames()) { Add("Ability Value/" + name, AnimatorParameterTranslator.AbilitySourceOf(name)); }
                menu.DropDown(field);
            }
            line.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            EditorGUI.PropertyField(line, parameter, new GUIContent("Parameter", "The name of the Animator float parameter it sets"));
            line.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            EditorGUI.PropertyField(line, smoothed, new GUIContent("Smoothed", "Eased toward its value over Speed Damp Time, so changes blend rather than snap"));
            EditorGUI.EndProperty();
        }

        /// <summary>Every ability value a movement component declares (MovementComponent.AbilityValueNames), sorted.</summary>
        private static IEnumerable<string> PublishedValueNames()
        {
            var names = new SortedSet<string>(StringComparer.Ordinal);
            foreach (Type type in TypeCache.GetTypesDerivedFrom<MovementComponent>())
            {
                if (type.IsAbstract || type.GetConstructor(Type.EmptyTypes) == null) { continue; }
                foreach (string name in ((MovementComponent)Activator.CreateInstance(type)).AbilityValueNames) { names.Add(name); }
            }
            return names;
        }
    }
}
