using System;
using System.Collections.Generic;
using System.Linq;
using MeridianMotion.Characters;
using UnityEditor;
using UnityEngine;

namespace MeridianMotion.EditorTools
{
    /// <summary>
    /// Keeps the project's movement mode assets in step with their code's default components
    /// (AbilityMovementMode.AddMissingDefaultComponents): a component added to the code's list, such as a new ability,
    /// is added to every mode asset made before it, at its default place. The movement mode's inspector does it for one
    /// mode (Add Missing Components); AddToAllModes for every one, from the command line too.
    /// </summary>
    public static class MovementModeSync
    {
        /// <summary>
        /// Adds the default components mode lacks, turned on or off as given, with Undo, and saves it. Returns the
        /// types it added (none if it had them all).
        /// </summary>
        public static List<Type> AddMissing(AbilityMovementMode mode, bool enabled)
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
        /// The mode the default character moves with (the Character prefab's: CharacterSceneSetup.CharacterPrefabPath), or
        /// null: AddToAllModes turns what it adds on there, and off on every other mode.
        /// </summary>
        public static MovementMode DefaultCharacterMode()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterSceneSetup.CharacterPrefabPath);
            return prefab != null && prefab.TryGetComponent(out MMotionMotor motor) ? motor.movementMode : null;
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
            foreach (string guid in AssetDatabase.FindAssets("t:" + nameof(AbilityMovementMode)))
            {
                var mode = AssetDatabase.LoadAssetAtPath<AbilityMovementMode>(AssetDatabase.GUIDToAssetPath(guid));
                count += AddMissing(mode, enabled: mode == defaultMode).Count;
            }
            return count;
        }

        /// <summary>
        /// The project's own movement mode assets (the paths of those outside the package, which an upgrade would
        /// replace: New Character's copies, say), by path: those a project's ability (New Ability's Your Project) is given to.
        /// </summary>
        public static List<string> ProjectModes()
        {
            var paths = new List<string>();
            foreach (string guid in AssetDatabase.FindAssets("t:" + nameof(AbilityMovementMode)))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!MeridianMotionAssets.InPackage(path) && !MeridianMotionAssets.IsReadOnly(path)) { paths.Add(path); }
            }
            paths.Sort(string.CompareOrdinal);
            return paths;
        }

        /// <summary>
        /// Gives the component type (a project's ability) to each of the movement mode assets at paths that hasn't got it,
        /// turned on, just after its component of type after (by class name; or first, for null; or last, if it hasn't
        /// one), with Undo, and saves them. Returns the paths of those it was added to.
        /// </summary>
        public static List<string> AddToProjectModes(Type type, string after, IEnumerable<string> paths)
        {
            var added = new List<string>();
            foreach (string path in paths)
            {
                var mode = AssetDatabase.LoadAssetAtPath<AbilityMovementMode>(path);
                if (mode == null || mode.Get(type, includeDisabled: true) != null) { continue; }
                int at = mode.Components.Count;
                if (after == null) { at = 0; }
                else
                {
                    for (int i = 0; i < mode.Components.Count; i++)
                    {
                        if (mode.Components[i] != null && mode.Components[i].GetType().Name == after) { at = i + 1; break; }
                    }
                }
                Undo.RecordObject(mode, "Add Movement Component");
                mode.Insert(at, (MovementComponent)Activator.CreateInstance(type));
                EditorUtility.SetDirty(mode);
                AssetDatabase.SaveAssetIfDirty(mode);
                added.Add(path);
            }
            if (added.Count > 0)
            {
                Debug.Log($"[{nameof(MovementModeSync)}] Added {MovementComponent.DisplayNameOf(type)}, turned on, to: {string.Join(", ", added)}");
            }
            return added;
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
