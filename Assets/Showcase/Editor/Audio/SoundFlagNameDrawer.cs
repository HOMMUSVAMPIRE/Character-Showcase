using System.Collections.Generic;
using MeridianMotion.Characters;
using MeridianMotion.Showcase.CharacterAudio;
using UnityEditor;
using UnityEngine;

namespace MeridianMotion.Showcase.Audio.EditorTools
{
    /// <summary>
    /// Draws a Sound Set row's flag name ([SoundFlagName]) as a list of the movement events' or the states' names (the
    /// package's and the project's own), so it can't be mistyped. A name that isn't one is shown as it is, marked missing.
    /// </summary>
    [CustomPropertyDrawer(typeof(SoundFlagNameAttribute))]
    public class SoundFlagNameDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.PropertyField(position, property, label);
                return;
            }
            var names = new List<string>();
            if (((SoundFlagNameAttribute)attribute).kind == SoundFlagKind.Event)
            {
                foreach ((string name, MovementEvents _) in MovementEvents.Named) { if (!string.IsNullOrEmpty(name)) { names.Add(name); } }
            }
            else
            {
                foreach ((string name, CharacterState _) in CharacterState.Named) { if (!string.IsNullOrEmpty(name)) { names.Add(name); } }
            }
            int index = names.IndexOf(property.stringValue);
            if (index < 0)
            {
                names.Insert(0, string.IsNullOrEmpty(property.stringValue) ? "(none)" : $"{property.stringValue} (missing)");
                index = 0;
            }
            using var check = new EditorGUI.ChangeCheckScope();
            int chosen = EditorGUI.Popup(position, label.text, index, names.ToArray());
            if (check.changed && !names[chosen].EndsWith("(missing)") && names[chosen] != "(none)") { property.stringValue = names[chosen]; }
        }
    }
}
