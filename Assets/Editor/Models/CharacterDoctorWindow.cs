using System.Collections.Generic;
using System.Linq;
using Character;
using UnityEditor;
using UnityEngine;

namespace CharacterShowcase.EditorTools
{
    /// <summary>
    /// Character Doctor (Tools > Character Showcase > Character Doctor): checks a character prefab (CharacterChecks) and
    /// lists each check with a light (pass, warn, fail), what it found and why it matters, and a button for the fix where a
    /// tool makes it. Also opened from a model source's and a CharacterModelHost's inspector, and run from the command line
    /// (CheckFromCommandLine), exiting with code 1 on a failure.
    /// </summary>
    public class CharacterDoctorWindow : EditorWindow
    {
        private GameObject prefab;
        private List<CharacterChecks.Check> checks;
        private Vector2 scroll;

        [MenuItem("Tools/Character Showcase/Character Doctor", priority = 3)]
        public static void Open() => Open(null);

        /// <summary>Opens the window on a character prefab (or the Character prefab, given none).</summary>
        public static void Open(GameObject character)
        {
            var window = GetWindow<CharacterDoctorWindow>(false, "Character Doctor");
            window.minSize = new Vector2(460f, 360f);
            window.prefab = character != null ? character : AssetDatabase.LoadAssetAtPath<GameObject>(SceneGameSetup.CharacterPrefabPath);
            window.checks = null;
        }

        private void OnGUI()
        {
            EditorGUI.BeginChangeCheck();
            prefab = (GameObject)EditorGUILayout.ObjectField(new GUIContent("Character", "A character prefab (one with an ACCharacterController)"), prefab, typeof(GameObject), false);
            if (EditorGUI.EndChangeCheck()) { checks = null; }
            if (prefab != null && prefab.GetComponent<ACCharacterController>() == null)
            {
                EditorGUILayout.HelpBox("That isn't a character prefab (it has no ACCharacterController).", MessageType.Warning);
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
                        GUILayout.Label(Icon(check.result), GUILayout.Width(20f), GUILayout.Height(18f));
                        EditorGUILayout.LabelField(check.name, EditorStyles.boldLabel);
                        if (check.fix != null && check.result != CharacterChecks.Result.Pass && GUILayout.Button(check.fixLabel, GUILayout.Width(190f)))
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

        private static GUIContent Icon(CharacterChecks.Result result) => EditorGUIUtility.IconContent(result switch
        {
            CharacterChecks.Result.Pass => "TestPassed",
            CharacterChecks.Result.Warn => "console.warnicon.sml",
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
                failed |= results.Any(c => c.result == CharacterChecks.Result.Fail);
                Debug.Log($"[Character Doctor] {each}:\n" + string.Join("\n", results));
            }
            CommandLine.Finish(!failed);
        }
    }

    /// <summary>The CharacterModelHost's inspector: as it is, with a button opening the Character Doctor on its prefab.</summary>
    [CustomEditor(typeof(CharacterModelHost))]
    public class CharacterModelHostEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space();
            if (GUILayout.Button("Check This Character (Character Doctor)"))
            {
                var host = (CharacterModelHost)target;
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
                    .FirstOrDefault(p => p.GetComponent<CharacterModelHost>()?.ModelSource == source);
                if (wearer == null)
                {
                    EditorUtility.DisplayDialog("Character Doctor", $"No character prefab wears {source.name} yet (its CharacterModelHost's Model Source). " +
                                                "Give one it (New Character makes one), then check it.", "OK");
                }
                else { CharacterDoctorWindow.Open(wearer); }
            }
        }
    }
}
