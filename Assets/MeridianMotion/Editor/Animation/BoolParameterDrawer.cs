using System;
using MeridianMotion.Characters;
using UnityEditor;
using UnityEngine;

namespace MeridianMotion.EditorTools
{
    /// <summary>
    /// Shows an AnimatorParameterTranslator's bool parameter with its source as a menu: the animation state's own bools
    /// (AnimatorParameterTranslator.AnimationBools), then each of the character's states (State: …), so an ability's
    /// state is shown by picking it, with no code. The source is saved as text, so without this it'd be a bare text field
    /// to spell right.
    /// </summary>
    [CustomPropertyDrawer(typeof(AnimatorParameterTranslator.BoolParameter))]
    public class BoolParameterDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) =>
            2f * EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            SerializedProperty source = property.FindPropertyRelative(nameof(AnimatorParameterTranslator.BoolParameter.source));
            SerializedProperty parameter = property.FindPropertyRelative(nameof(AnimatorParameterTranslator.BoolParameter.parameter));
            EditorGUI.BeginProperty(position, label, property);
            var line = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            Rect field = EditorGUI.PrefixLabel(line, new GUIContent("Shows", "What the parameter shows: a value of the " +
                "animation state, worked out for how the animation should look, or one of the character's states as the movement has it"));
            bool known = AnimatorParameterTranslator.TryResolve(source.stringValue, out _, out _);
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
                foreach ((string name, _) in AnimatorParameterTranslator.AnimationBools) { Add("Animation State/" + name, name); }
                foreach ((string name, CharacterState state) in CharacterState.Named)
                {
                    Add("Character State/" + ObjectNames.NicifyVariableName(name), AnimatorParameterTranslator.SourceOf(state));
                }
                menu.DropDown(field);
            }
            line.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            EditorGUI.PropertyField(line, parameter, new GUIContent("Parameter", "The name of the Animator bool parameter it sets"));
            EditorGUI.EndProperty();
        }
    }
}
