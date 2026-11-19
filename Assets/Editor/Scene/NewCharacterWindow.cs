using System.Collections.Generic;
using System.Linq;
using Character;
using UnityEditor;
using UnityEngine;

namespace CharacterShowcase.EditorTools
{
    /// <summary>
    /// New Character (Tools > Character Showcase > New Character…): asks for a character's name, its model (one Prepare
    /// Character Models has prepared, or a model file to prepare), its movement (shared, or a copy to tune) and,
    /// optionally, an animation set, then makes it (NewCharacter) in Assets/Characters/[name], checks it (Character Doctor)
    /// and can set up the open scene to spawn it. See docs/adding-a-character.md.
    /// </summary>
    public class NewCharacterWindow : EditorWindow
    {
        private readonly NewCharacter.Answers answers = new();
        private bool setUpScene = true;
        private List<DirectCharacterModelSource> prepared;
        private Vector2 scroll;

        [MenuItem("Tools/Character Showcase/New Character…", priority = 2)]
        public static void Open() => GetWindow<NewCharacterWindow>(true, "New Character").minSize = new Vector2(460f, 420f);

        private void OnGUI()
        {
            prepared ??= CharacterModelImporter.FindReadyDefinitions().Concat(CreatureModelImporter.FindPrepared()).ToList();
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.HelpBox("Makes a character of your own: its prefab (a variant of Character.prefab, so improvements to it reach " +
                                    "yours), wearing your model, moving by its own copy of the movement to tune. Nothing is changed " +
                                    "until you press Make It.", MessageType.Info);
            answers.name = EditorGUILayout.TextField(new GUIContent("Name", "Its folder (Assets/Characters/[name]) and assets are named after it"), answers.name);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Its model", EditorStyles.boldLabel);
            string[] names = new[] { "(a model file to prepare)" }.Concat(prepared.Select(p => CreatureModelImporter.IsCreature(p) ? $"{p.name} (a creature)" : p.name)).ToArray();
            int index = answers.modelSource == null ? 0 : prepared.IndexOf(answers.modelSource as DirectCharacterModelSource) + 1;
            index = EditorGUILayout.Popup(new GUIContent("Prepared Model", "A model made ready: a rigged humanoid in Assets/Models/CharacterModels " +
                "(Prepare Character Models), or a creature in Assets/Models/Creatures (Prepare Creatures)"), Mathf.Max(0, index), names);
            answers.modelSource = index == 0 ? null : prepared[index - 1];
            if (answers.modelSource == null)
            {
                answers.modelFile = (GameObject)EditorGUILayout.ObjectField(new GUIContent("Model File", "A rigged model (an .fbx) in the project: " +
                    "copied into Assets/Models/CharacterModels/[name] (a humanoid) or Assets/Models/Creatures/[name] (a creature) and prepared"),
                    answers.modelFile, typeof(GameObject), false);
                bool creature = EditorGUILayout.Toggle(new GUIContent("It's A Creature", "Not a humanoid (on four legs, say): prepared as a creature, " +
                    "its bones and clips guessed from their names into its Creature Setup, to check (see docs/creatures.md)"), answers.creature);
                if (creature != answers.creature)
                {
                    answers.creature = creature;
                    // A creature moves as the fox does, not as a person: its movement starts from the fox's.
                    answers.movement = creature ? NewCharacter.Movement.CopyTheFoxs : NewCharacter.Movement.CopyTheDefault;
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Its movement and animation", EditorStyles.boldLabel);
            answers.movement = (NewCharacter.Movement)EditorGUILayout.EnumPopup(new GUIContent("Movement",
                "Copy The Default: its own copy of the default character's movement, to tune (its tuning is its own). Share The Default: the " +
                "same asset (tuning it tunes both). Copy The Fox's: the fox's, with what a fox can't do turned off."), answers.movement);
            answers.animationSet = (CharacterAnimationSet)EditorGUILayout.ObjectField(new GUIContent("Animation Set",
                "Optional: clips of its own in some roles (an idle, a run), over the shared animation (Create > Character Showcase > " +
                "Animation > Animation Set)"), answers.animationSet, typeof(CharacterAnimationSet), false);
            setUpScene = EditorGUILayout.Toggle(new GUIContent("Set Up The Open Scene", "Then set up the open scene to spawn it (Set Up Scene)"), setUpScene);

            EditorGUILayout.Space();
            string problem = NewCharacter.Problem(answers);
            if (problem != null) { EditorGUILayout.HelpBox(problem, MessageType.Warning); }
            else if (GUILayout.Button("Make It", GUILayout.Height(28f))) { Make(); }
            EditorGUILayout.EndScrollView();
        }

        private void Make()
        {
            var made = new List<string>();
            GameObject prefab = NewCharacter.Make(answers, made);
            if (prefab == null)
            {
                EditorUtility.DisplayDialog("New Character", "It couldn't be made: see the Console.", "OK");
                return;
            }
            bool scene = setUpScene && NewCharacter.SetUpOpenScene(prefab);
            Debug.Log($"[New Character] Made {answers.name}:\n" + string.Join("\n", made), prefab);
            EditorGUIUtility.PingObject(prefab);
            CharacterDoctorWindow.Open(prefab);
            EditorUtility.DisplayDialog("New Character", $"{answers.name} is made, in {NewCharacter.FolderOf(answers.name.Trim())}" +
                (scene ? ", and the open scene spawns it: press Play." : ".") + " The Character Doctor shows how it checks out.", "OK");
            Close();
        }
    }
}
