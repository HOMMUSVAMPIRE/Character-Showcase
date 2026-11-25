using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using GrassWind = MeridianMotion.Showcase.Grass.GrassWind;

namespace MeridianMotion.Showcase.Levels
{
    // The flora the foliage pieces place (ReferenceAssets/OpenTasks.md, category P): the prefabs Generate Flora makes
    // (Assets/Showcase/Prefabs/Flora, FloraGenerator), leaf-card trees and bushes with their LODs, picked from the
    // foliage settings' lists by the piece's seeded random and sized to the settings' ranges. Flora isn't static-batched:
    // copies of a prefab are drawn together (instanced), which a batched copy can't be. Without the prefabs (a project
    // that hasn't generated them), the pieces build their faceted shapes as before, with a warning.
    public static partial class Graybox
    {
        /// <summary>Where Generate Flora makes the flora's prefabs (FloraGenerator.PrefabFolder).</summary>
        public const string FloraFolder = "Assets/Showcase/Prefabs/Flora/";

        private static readonly Dictionary<string, (GameObject prefab, Bounds bounds)> floraCache = new();
        private static bool warnedNoFlora;

        /// <summary>One of names' flora prefabs, picked by random (one draw either way), or null if there's none of it.</summary>
        private static GameObject PickFlora(string[] names, System.Random random)
        {
            int pick = random.Next();
            if (names == null || names.Length == 0) { return null; }
            GameObject prefab = Flora(names[pick % names.Length]).prefab;
            if (prefab == null && !warnedNoFlora)
            {
                warnedNoFlora = true;
                Debug.LogWarning($"[Graybox] No flora prefab '{names[pick % names.Length]}' in '{FloraFolder}': the trees and shrubs are built faceted. " +
                                 "Tools > Meridian Motion > Showcase > Generate Flora makes them.");
            }
            return prefab;
        }

        /// <summary>A flora prefab by name, and its first LOD's bounds (in its own space), looked up once a build.</summary>
        private static (GameObject prefab, Bounds bounds) Flora(string name)
        {
            if (floraCache.TryGetValue(name, out var found) && found.prefab != null) { return found; }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FloraFolder + name + ".prefab");
            Bounds bounds = default;
            if (prefab != null)
            {
                Renderer[] first = prefab.TryGetComponent(out LODGroup group) && group.GetLODs().Length > 0
                    ? group.GetLODs()[0].renderers : prefab.GetComponentsInChildren<Renderer>();
                bool any = false;
                foreach (Renderer renderer in first)
                {
                    if (renderer == null || !renderer.TryGetComponent(out MeshFilter filter) || filter.sharedMesh == null) { continue; }
                    Matrix4x4 toRoot = prefab.transform.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
                    Bounds b = filter.sharedMesh.bounds;
                    for (int c = 0; c < 8; c++)
                    {
                        Vector3 corner = toRoot.MultiplyPoint3x4(b.center + Vector3.Scale(b.extents, new Vector3((c & 1) != 0 ? 1 : -1, (c & 2) != 0 ? 1 : -1, (c & 4) != 0 ? 1 : -1)));
                        if (any) { bounds.Encapsulate(corner); } else { bounds = new Bounds(corner, Vector3.zero); any = true; }
                    }
                }
            }
            floraCache[name] = (prefab, bounds);
            return (prefab, bounds);
        }

        /// <summary>
        /// A flora prefab placed under parent at localPosition (its foot), turned (degrees) and scaled so its first LOD is
        /// height tall. Not static-batched (see the file's top); seen by occlusion culling. The level's wind is added if
        /// it hasn't one, for the flora to sway in.
        /// </summary>
        private static GameObject PlaceFlora(GameObject prefab, Transform parent, Vector3 localPosition, float turn, float height)
        {
            var placed = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.gameObject.scene);
            placed.transform.SetParent(parent, false);
            placed.transform.localPosition = localPosition;
            placed.transform.localRotation = Quaternion.Euler(0f, turn, 0f);
            float tall = Flora(prefab.name).bounds.size.y;
            placed.transform.localScale = Vector3.one * (tall > 0.01f ? height / tall : 1f);
            foreach (Transform part in placed.GetComponentsInChildren<Transform>(true))
            {
                GameObjectUtility.SetStaticEditorFlags(part.gameObject, StaticEditorFlags.OccludeeStatic);
            }
            EnsureWind(parent);
            return placed;
        }

        /// <summary>The level's wind (a GrassWind under parent's root), added if it hasn't one: the flora and grass sway in it.</summary>
        private static void EnsureWind(Transform parent)
        {
            if (parent.root.GetComponentInChildren<GrassWind>(true) != null) { return; }
            new GameObject("Grass Wind", typeof(GrassWind)).transform.SetParent(parent.root, false);
        }

        /// <summary>A shrub to place once what it stands in is made (a lawn chunk, a planter): its prefab, foot, turn and height.</summary>
        private struct ShrubPlacement { public GameObject prefab; public Vector3 at; public float turn, height; }

        /// <summary>Places the shrubs gathered while a mesh was built (see Shrub), under parent.</summary>
        private static void PlaceShrubs(List<ShrubPlacement> shrubs, Transform parent)
        {
            foreach (ShrubPlacement s in shrubs) { PlaceFlora(s.prefab, parent, s.at, s.turn, s.height); }
        }

        /// <summary>Forgets the flora looked up (a new build's prefabs may have been made again).</summary>
        public static void ForgetFlora()
        {
            floraCache.Clear();
            warnedNoFlora = false;
        }
    }
}
