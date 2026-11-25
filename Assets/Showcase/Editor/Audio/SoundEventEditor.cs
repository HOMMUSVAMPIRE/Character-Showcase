using UnityEditor;
using UnityEngine;

namespace MeridianMotion.Showcase.Audio.EditorTools
{
    /// <summary>
    /// A Sound Event's inspector: its settings, its id shown but not editable (the library gives it), a Preview button that
    /// plays one of its clips as it would play (its volume and pitch ranges), and a warning when no Sound Library lists it
    /// (then a request by its id can't find it).
    /// </summary>
    [CustomEditor(typeof(SoundEvent))]
    public class SoundEventEditor : Editor
    {
        private static AudioSource previewSource;
        private static ClipPicker picker;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            SerializedProperty property = serializedObject.GetIterator();
            for (bool enter = true; property.NextVisible(enter); enter = false)
            {
                if (property.name == "m_Script") { continue; }
                using (new EditorGUI.DisabledScope(property.name == "id")) { EditorGUILayout.PropertyField(property, true); }
            }
            serializedObject.ApplyModifiedProperties();

            var sound = (SoundEvent)target;
            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(!sound.HasClips))
            {
                if (GUILayout.Button(sound.HasClips ? "Preview" : "Preview (no clips yet)")) { Preview(sound); }
            }
            if (!InALibrary(sound))
            {
                EditorGUILayout.HelpBox("No Sound Library lists this sound, so a request by its id won't find it. Open the " +
                                        "Sound Library and press Find Every Sound Event.", MessageType.Warning);
            }
        }

        private static bool InALibrary(SoundEvent sound)
        {
            foreach (string guid in AssetDatabase.FindAssets("t:" + nameof(SoundLibrary)))
            {
                var library = AssetDatabase.LoadAssetAtPath<SoundLibrary>(AssetDatabase.GUIDToAssetPath(guid));
                if (library != null && System.Array.IndexOf(System.Linq.Enumerable.ToArray(library.Sounds), sound) >= 0) { return true; }
            }
            return false;
        }

        /// <summary>Plays one of a sound's clips in the editor, on a hidden AudioSource kept for previews.</summary>
        private static void Preview(SoundEvent sound)
        {
            if (previewSource == null)
            {
                var obj = EditorUtility.CreateGameObjectWithHideFlags("Sound Event Preview", HideFlags.HideAndDontSave, typeof(AudioSource));
                previewSource = obj.GetComponent<AudioSource>();
                previewSource.playOnAwake = false;
                previewSource.spatialBlend = 0f;
            }
            picker ??= new ClipPicker(System.Environment.TickCount);
            previewSource.Stop();
            previewSource.clip = picker.Pick(sound);
            previewSource.volume = picker.Range(sound.Volume);
            previewSource.pitch = picker.Range(sound.Pitch);
            previewSource.loop = false;
            previewSource.Play();
        }
    }
}
