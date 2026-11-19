using System.Collections.Generic;
using System.Linq;
using MeridianMotion.Characters;
using UnityEditor;
using UnityEngine;

namespace MeridianMotion.EditorTools
{
    /// <summary>
    /// New Character (Tools > Meridian Motion > New Character…): asks for a character's name, its model (one Prepare
    /// Character Models has prepared, or a model file to prepare), its movement (shared, or a copy to tune) and,
    /// optionally, an animation set, then makes it (NewCharacter) in Assets/Showcase/Characters/[name], checks it (Character Doctor)
    /// and can set up the open scene to spawn it. See docs/adding-a-character.md.
    /// </summary>
    public class NewCharacterWindow : EditorWindow
    {
        private readonly NewCharacter.Answers answers = new();
        private bool setUpScene = true;
        private List<PrefabModelSource> prepared;
        private Vector2 scroll;

        [MenuItem("Tools/Meridian Motion/New Character…", priority = 11)]
        public static void Open() => GetWindow<NewCharacterWindow>(true, "New Character").minSize = new Vector2(460f, 420f);

        private void OnGUI()
        {
            prepared ??= HumanoidModelImporter.FindPreparedSources().Concat(GenericRigModelImporter.FindPreparedSources()).ToList();
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.HelpBox("Makes a character of your own: its prefab (a variant of Character.prefab, so improvements to it reach " +
                                    "yours), wearing your model, moving by its own copy of the movement to tune. Nothing is changed " +
                                    "until you press Make It.", MessageType.Info);
            answers.name = EditorGUILayout.TextField(new GUIContent("Name", "Its folder (Assets/Showcase/Characters/[name]) and assets are named after it"), answers.name);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Its model", EditorStyles.boldLabel);
            string[] names = new[] { "(a model file to prepare)" }.Concat(prepared.Select(p => GenericRigModelImporter.IsGenericRig(p) ? $"{p.name} (generic rig)" : p.name)).ToArray();
            int index = answers.modelSource == null ? 0 : prepared.IndexOf(answers.modelSource as PrefabModelSource) + 1;
            index = EditorGUILayout.Popup(new GUIContent("Prepared Model", $"A model made ready: a rigged humanoid in {HumanoidModelImporter.ModelsFolder} " +
                $"(Prepare Humanoid Models), or a generic-rig character in {GenericRigModelImporter.ModelsFolder} (Prepare Generic Rig Models)"), Mathf.Max(0, index), names);
            answers.modelSource = index == 0 ? null : prepared[index - 1];
            if (answers.modelSource == null)
            {
                answers.modelFile = (GameObject)EditorGUILayout.ObjectField(new GUIContent("Model File", "A rigged model (an .fbx) in the project: " +
                    $"copied into {HumanoidModelImporter.ModelsFolder}/[name] (a humanoid) or {GenericRigModelImporter.ModelsFolder}/[name] (generic rig) and prepared"),
                    answers.modelFile, typeof(GameObject), false);
                bool genericRig = EditorGUILayout.Toggle(new GUIContent("Generic Rig (Not Humanoid)", "Not a humanoid (on four legs, say): prepared as a generic-rig character, " +
                    "its bones and clips guessed from their names into its Generic Rig Setup, to check (see docs/generic-rigs.md)"), answers.genericRig);
                if (genericRig != answers.genericRig)
                {
                    answers.genericRig = genericRig;
                    // A generic-rig character moves as the fox does, not as a person: its movement starts from the fox's.
                    answers.movement = genericRig ? NewCharacter.Movement.CopyFox : NewCharacter.Movement.CopyTheDefault;
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Its movement and animation", EditorStyles.boldLabel);
            answers.movement = (NewCharacter.Movement)EditorGUILayout.EnumPopup(new GUIContent("Movement",
                "Copy The Default: its own copy of the default character's movement, to tune (its tuning is its own). Share The Default: the " +
                "same asset (tuning it tunes both). Copy The Fox's: the fox's, with what a fox can't do turned off."), answers.movement);
            answers.animationSet = (CharacterAnimationSet)EditorGUILayout.ObjectField(new GUIContent("Animation Set",
                "Optional: clips of its own in some slots (an idle, a run), over the shared animation (Create > Meridian Motion > " +
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
