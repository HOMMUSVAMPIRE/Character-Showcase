using System.Collections.Generic;
using System.Linq;
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

        /// <summary>
        /// The level's flora seed (a level spec's "floraSeed", or its LevelDefinition's FloraSeed; set as a build starts):
        /// which variant of its kind each plant gets, and how it's mirrored and shaped, is a hash of it and the plant's own
        /// draw. Changing it reshuffles them and leaves every plant where it is, as big and turned as it was.
        /// </summary>
        public static int FloraSeed;

        /// <summary>The most a plant's width (x, z) differs from its height's scale, either way (a fraction).</summary>
        public const float WidthVariation = 0.1f;

        /// <summary>A plant picked (see PickFlora): its prefab, whether it's mirrored, and its width's scale.</summary>
        private struct FloraPick { public GameObject prefab; public bool mirrored; public float width; }

        /// <summary>
        /// A plant from kinds (the foliage settings' names: a kind, "Tree_Oak", stands for each of its variants,
        /// Tree_Oak_01...; a full name for itself). Its kind is picked by one draw from random, as before; which variant,
        /// whether it's mirrored and its width by a hash of the level's FloraSeed and that draw (see FloraSeed). No prefab
        /// (none generated): a pick with none, after one warning.
        /// </summary>
        private static FloraPick PickFlora(string[] kinds, System.Random random)
        {
            int draw = random.Next();
            if (kinds == null || kinds.Length == 0) { return default; }
            string kind = kinds[draw % kinds.Length];
            List<string> variants = VariantsOf(kind);
            if (variants.Count == 0)
            {
                if (!warnedNoFlora)
                {
                    warnedNoFlora = true;
                    Debug.LogWarning($"[Graybox] No flora prefab '{kind}' (or '{kind}_...') in '{FloraFolder}': the trees and shrubs are built faceted. " +
                                     "Tools > Meridian Motion > Showcase > Generate Flora makes them.");
                }
                return default;
            }
            uint hash = Mix((uint)FloraSeed, (uint)draw);
            return new FloraPick
            {
                prefab = Flora(variants[(int)(hash % (uint)variants.Count)]).prefab,
                mirrored = (Mix(hash, 1u) & 1u) != 0,
                width = 1f + WidthVariation * ((Mix(hash, 2u) % 2001u) / 1000f - 1f),
            };
        }

        /// <summary>A well-mixed hash of two numbers (a few rounds of multiply and shift).</summary>
        private static uint Mix(uint a, uint b)
        {
            uint h = a * 0x9E3779B1u ^ b * 0x85EBCA77u;
            h ^= h >> 15; h *= 0x2C1B3C6Du; h ^= h >> 12; h *= 0x297A2D39u; h ^= h >> 15;
            return h;
        }

        private static readonly Dictionary<string, List<string>> variantCache = new();

        /// <summary>
        /// A kind's variants, by name, in order (ordinal): the prefab named it, or every prefab named it and "_" and more
        /// (Tree_Oak_01, Bush_Flowering_Pink). Listed once a build.
        /// </summary>
        private static List<string> VariantsOf(string kind)
        {
            if (variantCache.TryGetValue(kind, out List<string> found)) { return found; }
            string folder = FloraFolder.TrimEnd('/');
            var names = AssetDatabase.IsValidFolder(folder)
                ? AssetDatabase.FindAssets("t:Prefab", new[] { folder }).Select(AssetDatabase.GUIDToAssetPath)
                    .Select(p => System.IO.Path.GetFileNameWithoutExtension(p))
                    .Where(n => n == kind || n.StartsWith(kind + "_", System.StringComparison.Ordinal))
                    .OrderBy(n => n, System.StringComparer.Ordinal).ToList()
                : new List<string>();
            variantCache[kind] = names;
            return names;
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
        /// A plant (see PickFlora) placed under parent at localPosition (its foot), turned (degrees), scaled so its first
        /// LOD is height tall, mirrored and its width varied as picked. Not static-batched (see the file's top); seen by occlusion culling. The level's wind is added if
        /// it hasn't one, for the flora to sway in.
        /// </summary>
        private static GameObject PlaceFlora(FloraPick pick, Transform parent, Vector3 localPosition, float turn, float height)
        {
            GameObject prefab = pick.prefab;
            var placed = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.gameObject.scene);
            placed.transform.SetParent(parent, false);
            placed.transform.localPosition = localPosition;
            placed.transform.localRotation = Quaternion.Euler(0f, turn, 0f);
            float tall = Flora(prefab.name).bounds.size.y;
            float scale = tall > 0.01f ? height / tall : 1f;
            // Mirrored (on x) and its width varied from its height's scale: a variant's copies seldom read as the same plant.
            float width = scale * (pick.width > 0f ? pick.width : 1f);
            placed.transform.localScale = new Vector3(pick.mirrored ? -width : width, scale, width);
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
        private struct ShrubPlacement { public FloraPick pick; public Vector3 at; public float turn, height; }

        /// <summary>Places the shrubs gathered while a mesh was built (see Shrub), under parent.</summary>
        private static void PlaceShrubs(List<ShrubPlacement> shrubs, Transform parent)
        {
            foreach (ShrubPlacement s in shrubs) { PlaceFlora(s.pick, parent, s.at, s.turn, s.height); }
        }

        /// <summary>Forgets the flora looked up (a new build's prefabs may have been made again).</summary>
        public static void ForgetFlora()
        {
            floraCache.Clear();
            variantCache.Clear();
            warnedNoFlora = false;
        }
    }
}
