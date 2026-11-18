using System;
using System.Collections.Generic;
using System.Linq;
using Character;
using UnityEditor;
using UnityEngine;

namespace CharacterShowcase.EditorTools
{
    /// <summary>
    /// Keeps the project's movement mode assets in step with their code's default components
    /// (ComposedMovementMode.AddMissingDefaultComponents): a component added to the code's list, such as a new ability,
    /// is added to every mode asset made before it, at its default place. The movement mode's inspector does it for one
    /// mode (Add Missing Components); AddToAllModes for every one, from the command line too.
    /// </summary>
    public static class MovementModeSync
    {
        /// <summary>
        /// Adds the default components mode lacks, turned on or off as given, with Undo, and saves it. Returns the
        /// types it added (none if it had them all).
        /// </summary>
        public static List<Type> AddMissing(ComposedMovementMode mode, bool enabled)
        {
            if (mode == null || mode.MissingDefaultComponentTypes().Count == 0) { return new List<Type>(); }
            Undo.RecordObject(mode, "Add Missing Movement Components");
            List<Type> added = mode.AddMissingDefaultComponents(enabled);
            EditorUtility.SetDirty(mode);
            AssetDatabase.SaveAssetIfDirty(mode);
            Debug.Log($"[{nameof(MovementModeSync)}] Added to {mode.name}, turned {(enabled ? "on" : "off")}: " +
                      string.Join(", ", added.Select(MovementComponent.DisplayNameOf)), mode);
            return added;
        }

        /// <summary>
        /// The mode the default character moves with (the Character prefab's: SceneGameSetup.CharacterPrefabPath), or
        /// null: AddToAllModes turns what it adds on there, and off on every other mode.
        /// </summary>
        public static MovementMode DefaultCharacterMode()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SceneGameSetup.CharacterPrefabPath);
            return prefab != null && prefab.TryGetComponent(out MovementController controller) ? controller.movementHandler : null;
        }

        /// <summary>
        /// Adds the default components every movement mode asset in the project lacks: turned on for the default
        /// character's mode (DefaultCharacterMode), and off for every other (another character's, such as the fox's, whose
        /// body and animations may not suit the new component: turn it on there once they do). Returns how many it added.
        /// </summary>
        public static int AddToAllModes()
        {
            MovementMode defaultMode = DefaultCharacterMode();
            int count = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:" + nameof(ComposedMovementMode)))
            {
                var mode = AssetDatabase.LoadAssetAtPath<ComposedMovementMode>(AssetDatabase.GUIDToAssetPath(guid));
                count += AddMissing(mode, enabled: mode == defaultMode).Count;
            }
            return count;
        }

        /// <summary>
        /// For -executeMethod: AddToAllModes, then exits (code 0, or 1 if something went wrong). Run it after adding a
        /// component to a mode's default list outside the editor.
        /// </summary>
        public static void AddToAllModesFromCommandLine()
        {
            bool succeeded = true;
            try { Debug.Log($"[{nameof(MovementModeSync)}] Added {AddToAllModes()} component(s) to the movement modes."); }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                succeeded = false;
            }
            CommandLine.Finish(succeeded);
        }
    }
}
