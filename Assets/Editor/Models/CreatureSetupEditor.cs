using Character;
using UnityEditor;
using UnityEngine;

namespace CharacterShowcase.EditorTools
{
    /// <summary>
    /// A creature setup's inspector: what preparing it couldn't find, first, as a warning; then its settings; and buttons
    /// that prepare it now, guess its bones and clips again from their names, and open its animation set.
    /// </summary>
    [CustomEditor(typeof(CreatureSetup))]
    public class CreatureSetupEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var setup = (CreatureSetup)target;
            if (setup.unfound.Length > 0)
            {
                EditorGUILayout.HelpBox("Preparing it couldn't find:\n• " + string.Join("\n• ", setup.unfound) +
                                        "\nSet them below (bones by their names in the model): it's prepared again by itself.", MessageType.Warning);
            }
            else
            {
                EditorGUILayout.HelpBox("Its bones and clips were guessed from their names when it was made: check them. A change here " +
                                        "prepares the creature again by itself (see docs/creatures.md).", MessageType.Info);
            }
            DrawDefaultInspector();
            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Prepare It Now"))
                {
                    AssetDatabase.SaveAssets();
                    CreatureModelImporter.Result result = CreatureModelImporter.Prepare(setup);
                    if (result.Succeeded) { Debug.Log($"[Prepare Creatures] {result}", result.source); }
                    else { Debug.LogError($"[Prepare Creatures] {result}", setup); }
                }
                if (GUILayout.Button("Guess Again") && EditorUtility.DisplayDialog("Guess Again",
                        "Replace its bones and clips with guesses from their names? What's set here now is lost (its animation set is kept).", "Guess", "Cancel"))
                {
                    Undo.RecordObject(setup, "Guess Creature Setup");
                    CharacterAnimationSet set = setup.animationSet;
                    CreatureModelImporter.Guess(setup);
                    if (set != null) { setup.animationSet = set; }
                    else if (setup.animationSet != null && !AssetDatabase.Contains(setup.animationSet))
                    {
                        string folder = System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(setup));
                        AssetDatabase.CreateAsset(setup.animationSet, AssetDatabase.GenerateUniqueAssetPath($"{folder}/{setup.model.name} Animation Set.asset"));
                    }
                    EditorUtility.SetDirty(setup);
                }
            }
            if (setup.animationSet != null && GUILayout.Button("Open Its Animation Set")) { Selection.activeObject = setup.animationSet; }
        }
    }
}
