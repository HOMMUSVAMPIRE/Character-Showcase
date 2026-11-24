using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MeridianMotion.Showcase.Grass.EditorTools
{
    /// <summary>
    /// A grass style's inspector: its settings, and a button that gives it to every grass field in the open scenes, so
    /// trying a style on a level is one click (and Undo puts the old ones back).
    /// </summary>
    [CustomEditor(typeof(GrassStyle))]
    public sealed class GrassStyleEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space();
            GrassField[] fields = Fields();
            using (new EditorGUI.DisabledScope(fields.Length == 0))
            {
                if (GUILayout.Button(fields.Length == 0 ? "No grass fields in the open scenes" : $"Use in the Open Scenes ({fields.Length} grass fields)"))
                {
                    Undo.RecordObjects(fields, "Use Grass Style");
                    foreach (GrassField field in fields)
                    {
                        field.Style = (GrassStyle)target;
                        EditorUtility.SetDirty(field);
                    }
                }
            }
        }

        private static GrassField[] Fields() =>
            Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt).Where(s => s.isLoaded)
                .SelectMany(s => s.GetRootGameObjects()).SelectMany(o => o.GetComponentsInChildren<GrassField>(true)).ToArray();
    }
}
