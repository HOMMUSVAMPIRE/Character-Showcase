using System.Collections.Generic;
using System.Linq;
using MeridianMotion.Characters;
using UnityEditor;
using UnityEngine;

namespace MeridianMotion.EditorTools
{
    /// <summary>
    /// The inspector of an animation set (CharacterAnimationSet): its layout and controller, then every slot its layout has
    /// (its clip slots), each with the clip the layout plays there and a field for the set's own, so a slot is filled by
    /// dropping a clip on it rather than typing its name; and a button that builds its controller.
    /// </summary>
    [CustomEditor(typeof(CharacterAnimationSet))]
    public class CharacterAnimationSetEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var set = (CharacterAnimationSet)target;
            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(CharacterAnimationSet.layout)));
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(CharacterAnimationSet.controller)));
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(CharacterAnimationSet.notes)));
            serializedObject.ApplyModifiedProperties();

            if (set.layout == null)
            {
                EditorGUILayout.HelpBox("Choose the Animator Layout it starts from (the mannequin's, Assets/Animation/Mannequin Layout.asset, for a humanoid).", MessageType.Info);
                return;
            }
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Slots", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Each slot is one of the layout's clip slots. Drop a clip on a slot to play it there; leave it " +
                                    "empty to keep the layout's (shown beside it).", MessageType.None);
            var bySlot = set.clips.Where(c => !string.IsNullOrEmpty(c.slot)).GroupBy(c => c.slot).ToDictionary(g => g.Key, g => g.First().clip);
            bool changed = false;
            foreach (ClipSlot slot in set.layout.clips)
            {
                AnimationClip layoutClip = slot.candidates?.FirstOrDefault(c => c != null);
                bySlot.TryGetValue(slot.name, out AnimationClip own);
                using (new EditorGUILayout.HorizontalScope())
                {
                    string shown = layoutClip != null ? layoutClip.name : string.IsNullOrEmpty(slot.fallbackSlot) ? "(none)" : $"(as {slot.fallbackSlot})";
                    EditorGUI.BeginChangeCheck();
                    var picked = (AnimationClip)EditorGUILayout.ObjectField(new GUIContent(slot.name, $"The layout plays {shown} here."), own, typeof(AnimationClip), false);
                    if (EditorGUI.EndChangeCheck()) { bySlot[slot.name] = picked; changed = true; }
                    EditorGUILayout.LabelField(own == null ? shown : "", EditorStyles.miniLabel, GUILayout.Width(140f));
                }
            }
            if (changed)
            {
                Undo.RecordObject(set, "Change Animation Set");
                set.clips = bySlot.Where(r => r.Value != null).Select(r => new CharacterAnimationSet.SlotClip { slot = r.Key, clip = r.Value }).ToArray();
                EditorUtility.SetDirty(set);
            }
            IEnumerable<string> unknown = set.clips.Select(c => c.slot).Where(slot => set.layout.clips.All(s => s.name != slot));
            if (unknown.Any())
            {
                EditorGUILayout.HelpBox("These slots aren't the layout's (renamed or removed there), so they're not used: " + string.Join(", ", unknown), MessageType.Warning);
            }
            EditorGUILayout.Space();
            if (GUILayout.Button("Build Its Animator Controller")) { AnimatorControllerBuilder.Build(set); }
        }
    }
}
