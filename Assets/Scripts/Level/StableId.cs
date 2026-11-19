using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MeridianMotion.Levels
{
    /// <summary>
    /// An ID for an object in a scene that's the same on every machine that has loaded the same level: a hash (64-bit
    /// FNV-1a) of its scene's name and its path of names from the scene's root, each with its place among same-named
    /// siblings. Never an instance ID (those differ between machines and runs). What a character's state names the level
    /// objects it's on or holding by, to cross a network (see Character.StateSerializer). Two objects of the same name
    /// under the same parent are told apart by their order there, so a level built the same way gives the same IDs.
    /// </summary>
    public static class StableId
    {
        private static readonly Dictionary<ulong, Transform> found = new();
        private static readonly StringBuilder path = new();

        /// <summary>The object's ID (0 for none).</summary>
        public static ulong Of(Transform transform)
        {
            if (transform == null) { return 0; }
            path.Clear();
            path.Append(transform.gameObject.scene.name);
            AppendPath(transform);
            ulong hash = 14695981039346656037UL;
            for (int i = 0; i < path.Length; i++) { hash = (hash ^ path[i]) * 1099511628211UL; }
            return hash;
        }

        /// <summary>The path from the scene's root, each step "/name#k", k its place among same-named siblings.</summary>
        private static void AppendPath(Transform transform)
        {
            if (transform.parent != null) { AppendPath(transform.parent); }
            path.Append('/').Append(transform.name).Append('#').Append(SameNamedBefore(transform));
        }

        private static int SameNamedBefore(Transform transform)
        {
            int before = 0;
            if (transform.parent != null)
            {
                Transform parent = transform.parent;
                for (int i = 0; i < transform.GetSiblingIndex(); i++)
                {
                    if (parent.GetChild(i).name == transform.name) { before++; }
                }
                return before;
            }
            foreach (GameObject root in transform.gameObject.scene.GetRootGameObjects())
            {
                if (root.transform == transform) { break; }
                if (root.name == transform.name) { before++; }
            }
            return before;
        }

        /// <summary>
        /// The object in the loaded scenes with this ID, or null. Looked up in a table of every object's ID, made again
        /// when an ID isn't in it (after the scene's changed). Slow the first time: for restoring a character's state, not
        /// every step.
        /// </summary>
        public static Transform Resolve(ulong id)
        {
            if (id == 0) { return null; }
            if (found.TryGetValue(id, out Transform transform) && transform != null && Of(transform) == id) { return transform; }
            found.Clear();
            for (int s = 0; s < SceneManager.sceneCount; s++)
            {
                Scene scene = SceneManager.GetSceneAt(s);
                if (!scene.isLoaded) { continue; }
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    foreach (Transform t in root.GetComponentsInChildren<Transform>(true)) { found[Of(t)] = t; }
                }
            }
            return found.TryGetValue(id, out transform) ? transform : null;
        }
    }
}
