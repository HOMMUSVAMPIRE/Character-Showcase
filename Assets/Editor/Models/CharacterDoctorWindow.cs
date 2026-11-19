using System.Collections.Generic;
using System.Linq;
using MeridianMotion.Characters;
using UnityEditor;
using UnityEngine;

namespace MeridianMotion.EditorTools
{
    /// <summary>
    /// Character Doctor (Tools > Meridian Motion > Character Doctor): checks a character prefab (CharacterChecks) and
    /// lists each check with a light (pass, warn, fail), what it found and why it matters, and a button for the fix where a
    /// tool makes it. Also opened from a model source's and a CharacterModelSlot's inspector, and run from the command line
    /// (CheckFromCommandLine), exiting with code 1 on a failure.
    /// </summary>
    public class CharacterDoctorWindow : EditorWindow
    {
        private GameObject prefab;
        private List<CharacterChecks.Check> checks;
        private Vector2 scroll;

        [MenuItem("Tools/Meridian Motion/Character Doctor", priority = 3)]
        public static void Open() => Open(null);

        /// <summary>Opens the window on a character prefab (or the Character prefab, given none).</summary>
        public static void Open(GameObject character)
        {
            var window = GetWindow<CharacterDoctorWindow>(false, "Character Doctor");
            window.minSize = new Vector2(460f, 360f);
            window.prefab = character != null ? character : AssetDatabase.LoadAssetAtPath<GameObject>(CharacterSceneSetup.CharacterPrefabPath);
            window.checks = null;
        }

        private void OnGUI()
        {
            EditorGUI.BeginChangeCheck();
            prefab = (GameObject)EditorGUILayout.ObjectField(new GUIContent("Character", "A character prefab (one with an MMotionCharacter)"), prefab, typeof(GameObject), false);
            if (EditorGUI.EndChangeCheck()) { checks = null; }
            if (prefab != null && prefab.GetComponent<MMotionCharacter>() == null)
            {
                EditorGUILayout.HelpBox("That isn't a character prefab (it has no MMotionCharacter).", MessageType.Warning);
                return;
            }
            if (prefab == null) { return; }
            checks ??= CharacterChecks.Run(prefab);
            if (GUILayout.Button("Check Again")) { checks = CharacterChecks.Run(prefab); }

            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (CharacterChecks.Check check in checks)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Label(Icon(check.status), GUILayout.Width(20f), GUILayout.Height(18f));
                        EditorGUILayout.LabelField(check.name, EditorStyles.boldLabel);
                        if (check.fix != null && check.status != CharacterChecks.CheckStatus.Pass && GUILayout.Button(check.fixLabel, GUILayout.Width(190f)))
                        {
                            check.fix();
                            checks = CharacterChecks.Run(prefab);
                            GUIUtility.ExitGUI();
                        }
                    }
                    EditorGUILayout.LabelField(check.message, EditorStyles.wordWrappedLabel);
                }
            }
            EditorGUILayout.EndScrollView();
        }

        private static GUIContent Icon(CharacterChecks.CheckStatus result) => EditorGUIUtility.IconContent(result switch
        {
            CharacterChecks.CheckStatus.Pass => "TestPassed",
            CharacterChecks.CheckStatus.Warn => "console.warnicon.sml",
            _ => "console.erroricon.sml",
        });

        /// <summary>
        /// For -executeMethod: checks the character prefab given by -prefab [path] (every character prefab in the project
        /// without it), logs each check, and exits with code 1 if any failed (warnings pass).
        /// </summary>
        public static void CheckFromCommandLine()
        {
            string path = CommandLine.Argument("-prefab");
            List<string> prefabs = path != null ? new List<string> { path } : CharacterChecks.CharacterPrefabs();
            bool failed = false;
            foreach (string each in prefabs)
            {
                var character = AssetDatabase.LoadAssetAtPath<GameObject>(each);
                if (character == null) { Debug.LogError($"[Character Doctor] No prefab at '{each}'."); failed = true; continue; }
                List<CharacterChecks.Check> results = CharacterChecks.Run(character);
                failed |= results.Any(c => c.status == CharacterChecks.CheckStatus.Fail);
                Debug.Log($"[Character Doctor] {each}:\n" + string.Join("\n", results));
            }
            CommandLine.Finish(!failed);
        }
    }

    /// <summary>The CharacterModelSlot's inspector: as it is, with a button opening the Character Doctor on its prefab.</summary>
    [CustomEditor(typeof(CharacterModelSlot))]
    public class CharacterModelHostEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space();
            if (GUILayout.Button("Check This Character (Character Doctor)"))
            {
                var host = (CharacterModelSlot)target;
                GameObject root = PrefabUtility.IsPartOfPrefabAsset(host) ? host.transform.root.gameObject : PrefabUtility.GetCorrespondingObjectFromSource(host.gameObject) ?? host.gameObject;
                CharacterDoctorWindow.Open(root);
            }
        }
    }

    /// <summary>A model source's inspector: as it is, with a button opening the Character Doctor on the first character that wears it.</summary>
    [CustomEditor(typeof(CharacterModelSource), true)]
    public class CharacterModelSourceEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space();
            if (GUILayout.Button("Check A Character Wearing It (Character Doctor)"))
            {
                var source = (CharacterModelSource)target;
                GameObject wearer = CharacterChecks.CharacterPrefabs().Select(AssetDatabase.LoadAssetAtPath<GameObject>)
                    .FirstOrDefault(p => p.GetComponent<CharacterModelSlot>()?.ModelSource == source);
                if (wearer == null)
                {
                    EditorUtility.DisplayDialog("Character Doctor", $"No character prefab wears {source.name} yet (its CharacterModelSlot's Model Source). " +
                                                "Give one it (New Character makes one), then check it.", "OK");
                }
                else { CharacterDoctorWindow.Open(wearer); }
            }
        }
    }
}
