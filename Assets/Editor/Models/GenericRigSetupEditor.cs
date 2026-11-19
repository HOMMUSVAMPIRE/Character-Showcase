using MeridianMotion.Characters;
using UnityEditor;
using UnityEngine;

namespace MeridianMotion.EditorTools
{
    /// <summary>
    /// A generic-rig character setup's inspector: what preparing it couldn't find, first, as a warning; then its settings; and buttons
    /// that prepare it now, guess its bones and clips again from their names, and open its animation set.
    /// </summary>
    [CustomEditor(typeof(GenericRigSetup))]
    public class GenericRigSetupEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var setup = (GenericRigSetup)target;
            if (setup.missingBones.Length > 0)
            {
                EditorGUILayout.HelpBox("Preparing it couldn't find:\n• " + string.Join("\n• ", setup.missingBones) +
                                        "\nSet them below (bones by their names in the model): it's prepared again by itself.", MessageType.Warning);
            }
            else
            {
                EditorGUILayout.HelpBox("Its bones and clips were guessed from their names when it was made: check them. A change here " +
                                        "prepares the generic-rig character again by itself (see docs/generic-rigs.md).", MessageType.Info);
            }
            DrawDefaultInspector();
            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Prepare It Now"))
                {
                    AssetDatabase.SaveAssets();
                    GenericRigModelImporter.Result result = GenericRigModelImporter.Prepare(setup);
                    if (result.Succeeded) { Debug.Log($"[Prepare Generic Rig Models] {result}", result.source); }
                    else { Debug.LogError($"[Prepare Generic Rig Models] {result}", setup); }
                }
                if (GUILayout.Button("Guess Again") && EditorUtility.DisplayDialog("Guess Again",
                        "Replace its bones and clips with guesses from their names? What's set here now is lost (its animation set is kept).", "Guess", "Cancel"))
                {
                    Undo.RecordObject(setup, "Guess Generic Rig Setup");
                    CharacterAnimationSet set = setup.animationSet;
                    GenericRigModelImporter.Guess(setup);
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
