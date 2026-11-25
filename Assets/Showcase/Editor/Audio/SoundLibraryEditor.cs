using UnityEditor;
using UnityEngine;

namespace MeridianMotion.Showcase.Audio.EditorTools
{
    /// <summary>
    /// A Sound Library's inspector: its lists, and Find Every Sound Event, which adds every Sound Event in the project and
    /// gives each without an id (or with another's) the next free one (AudioAssets.FillLibrary).
    /// </summary>
    [CustomEditor(typeof(SoundLibrary))]
    public class SoundLibraryEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space();
            if (GUILayout.Button("Find Every Sound Event"))
            {
                AudioAssets.FillLibrary((SoundLibrary)target);
                AssetDatabase.SaveAssets();
            }
        }
    }

    /// <summary>
    /// Gives a Sound Event made in the project its id and a place in the Sound Library (the first in the project), after
    /// it's imported, so ids are never 0 and never reused.
    /// </summary>
    public class SoundEventPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            bool newSound = false;
            foreach (string path in imported)
            {
                // Only Sound Events (a level rebuild imports large assets this needn't load).
                if (!path.EndsWith(".asset") || AssetDatabase.GetMainAssetTypeAtPath(path) != typeof(SoundEvent)) { continue; }
                var sound = AssetDatabase.LoadAssetAtPath<SoundEvent>(path);
                // New, or a duplicate (which keeps its original's id).
                if (sound != null && (sound.Id.IsNone || IdTakenByAnother(sound))) { newSound = true; break; }
            }
            if (!newSound) { return; }
            // After the import: changing assets while they're imported isn't allowed.
            EditorApplication.delayCall += () =>
            {
                string[] libraries = AssetDatabase.FindAssets("t:" + nameof(SoundLibrary));
                if (libraries.Length == 0) { return; }
                var library = AssetDatabase.LoadAssetAtPath<SoundLibrary>(AssetDatabase.GUIDToAssetPath(libraries[0]));
                if (library == null) { return; }
                AudioAssets.FillLibrary(library);
                AssetDatabase.SaveAssets();
            };
        }

        private static bool IdTakenByAnother(SoundEvent sound)
        {
            foreach (string guid in AssetDatabase.FindAssets("t:" + nameof(SoundLibrary)))
            {
                var library = AssetDatabase.LoadAssetAtPath<SoundLibrary>(AssetDatabase.GUIDToAssetPath(guid));
                if (library == null) { continue; }
                SoundEvent holder = library.Find(sound.Id);
                if (holder != null && holder != sound) { return true; }
            }
            return false;
        }
    }
}
