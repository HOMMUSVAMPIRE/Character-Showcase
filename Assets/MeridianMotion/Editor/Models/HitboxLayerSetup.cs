using System.Collections.Generic;
using System.Linq;
using MeridianMotion.Characters;
using UnityEditor;
using UnityEngine;

namespace MeridianMotion.EditorTools
{
    /// <summary>
    /// The layer a character's hitboxes are on (CharacterModelSlot's Hitbox Layer): adding the CharacterHitbox layer to a
    /// project that hasn't got it (a package can't bring its own layers), or choosing one the project already has. For
    /// the Character Doctor, Set Up Scene and Start Here.
    /// </summary>
    public static class HitboxLayerSetup
    {
        // The first layer a project's own may take (0-7 are Unity's, some of them unnamed but reserved).
        private const int FirstUserLayer = 8;
        private const string TagManagerPath = "ProjectSettings/TagManager.asset";

        /// <summary>Whether the project has a layer of this name.</summary>
        public static bool Exists(string layer) => !string.IsNullOrEmpty(layer) && LayerMask.NameToLayer(layer) >= 0;

        /// <summary>
        /// Adds a layer of this name in the first free user layer (Tags and Layers), returning its number: the one it has
        /// if it's there already, or -1 if all 24 user layers are taken.
        /// </summary>
        public static int Add(string layer)
        {
            int existing = LayerMask.NameToLayer(layer);
            if (existing >= 0) { return existing; }
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath(TagManagerPath)[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");
            for (int i = FirstUserLayer; i < layers.arraySize; i++)
            {
                SerializedProperty slot = layers.GetArrayElementAtIndex(i);
                if (!string.IsNullOrEmpty(slot.stringValue)) { continue; }
                slot.stringValue = layer;
                tagManager.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssets();
                Debug.Log($"[Hitbox Layer] Added the '{layer}' layer (layer {i}) to Tags and Layers.");
                return i;
            }
            Debug.LogError($"[Hitbox Layer] Every user layer is taken, so '{layer}' couldn't be added: choose a layer the project has instead.");
            return -1;
        }

        /// <summary>
        /// The project's layers a character's hitboxes may go on: named, not Default or Ignore Raycast (what aims at the
        /// character would miss it), and not one its movement queries (movementMask), which would meet its own hitboxes.
        /// </summary>
        public static List<string> Usable(int movementMask)
        {
            var usable = new List<string>();
            for (int i = 0; i < 32; i++)
            {
                string name = LayerMask.LayerToName(i);
                if (string.IsNullOrEmpty(name) || i == 0 || name == "Ignore Raycast" || (movementMask & (1 << i)) != 0) { continue; }
                usable.Add(name);
            }
            return usable;
        }

        /// <summary>
        /// Puts the hitboxes of the character prefab's models on this layer (its CharacterModelSlot's Hitbox Layer), which
        /// variants of it take too. Returns whether it could (not in a read-only package).
        /// </summary>
        public static bool Use(GameObject characterPrefab, string layer)
        {
            string path = AssetDatabase.GetAssetPath(characterPrefab);
            if (MeridianMotionAssets.IsReadOnly(path))
            {
                Debug.LogError($"[Hitbox Layer] {path} is in a read-only package: set the layer on a character of your own (New Character makes one).");
                return false;
            }
            var slot = characterPrefab.GetComponent<CharacterModelSlot>();
            if (slot == null) { return false; }
            var serialized = new SerializedObject(slot);
            serialized.FindProperty("hitboxLayer").stringValue = layer;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SavePrefabAsset(characterPrefab);
            return true;
        }

        /// <summary>The problem with the character's hitbox layer, or null if there's none: for the checks' messages.</summary>
        public static string Problem(GameObject characterPrefab)
        {
            var slot = characterPrefab.GetComponent<CharacterModelSlot>();
            var movement = characterPrefab.GetComponent<MMotionMotor>();
            if (slot == null || movement == null) { return null; }
            int layer = slot.HitboxLayerIndex;
            if (layer < 0)
            {
                return $"The project has no '{slot.HitboxLayer}' layer (its CharacterModelSlot's Hitbox Layer), so its hitboxes stay on the " +
                       "layer their model was saved with, where what's meant to meet them may not. Add the layer, or choose one the project has.";
            }
            if (layer == 0) { return "Its hitboxes are on Default, where everything meets them: choose a layer of their own."; }
            if ((movement.MovementLayerMask & (1 << layer)) != 0)
            {
                return $"Its movement's layers (MMotionMotor's Movement Layer Mask) include '{slot.HitboxLayer}', its hitboxes' layer, " +
                       "so its movement would meet its own hitboxes. Choose another layer, or take it out of the mask.";
            }
            return null;
        }

        /// <summary>The usable layers (Usable) for this character's movement, for a choice of one.</summary>
        public static string[] ChoicesFor(GameObject characterPrefab)
        {
            var movement = characterPrefab.GetComponent<MMotionMotor>();
            return Usable(movement != null ? movement.MovementLayerMask : 0).ToArray();
        }
    }
}
