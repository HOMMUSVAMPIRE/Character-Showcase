using System.Collections.Generic;
using System.IO;
using Level;
using UnityEditor;
using UnityEngine;

namespace CharacterShowcase.EditorTools
{
    /// <summary>
    /// The assets painting climbable surfaces makes (see PaintedClimbableSurface): the mask textures, saved as assets so
    /// they're kept with the scene, and meshes with a UV2 for renderers whose meshes haven't one. They're kept in
    /// Assets/ClimbablePaint, a folder per scene.
    /// </summary>
    public static class ClimbablePaintAssets
    {
        public const string Folder = "Assets/ClimbablePaint";
        /// <summary>Unity's cube, with a UV2 (every face of Unity's own has the same UVs), for the cubes painted onto.</summary>
        public const string CubePath = Folder + "/Cube (UV2).asset";
        /// <summary>The climbable mask every unpainted object with a climbable material shares: 1 pixel of black.</summary>
        public const string NoMaskPath = "Assets/Materials/ClimbableMask_None.png";

        /// <summary>
        /// Gives every material in the project that can show climbable areas (it has a climbable mask property) but has no
        /// mask the shared one meaning nothing's painted (see NoMaskPath). Painted objects give theirs masks of their own,
        /// per renderer, without changing the material.
        /// </summary>
        [MenuItem("Tools/Character Showcase/Set Up Climbable Materials")]
        public static void SetUpMaterials()
        {
            var none = AssetDatabase.LoadAssetAtPath<Texture>(NoMaskPath);
            if (none == null) { Debug.LogError($"[Climbable Paint] No mask texture at '{NoMaskPath}'."); return; }
            int count = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets" }))
            {
                // Material files only: a shader graph holds a material of its own, which isn't saved.
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".mat")) { continue; }
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null || !material.HasProperty(PaintedClimbableSurface.MaskProperty) || material.GetTexture(PaintedClimbableSurface.MaskProperty) != null) { continue; }
                material.SetTexture(PaintedClimbableSurface.MaskProperty, none);
                EditorUtility.SetDirty(material);
                count++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[Climbable Paint] Gave {count} material(s) the shared unpainted mask.");
        }

        /// <summary>
        /// Paints a surface's masks again (PaintedClimbableSurface.RebuildMasks), saving any new one as an asset and
        /// writing the changed ones to disk.
        /// </summary>
        public static void RebuildMasks(PaintedClimbableSurface surface)
        {
            if (surface == null) { return; }
            List<PaintedClimbableSurface.RendererMask> changed = surface.RebuildMasks();
            bool newAssets = false;
            foreach (PaintedClimbableSurface.RendererMask mask in changed)
            {
                if (mask.texture == null) { continue; }
                if (EditorUtility.IsPersistent(mask.texture))
                {
                    EditorUtility.SetDirty(mask.texture);
                    AssetDatabase.SaveAssetIfDirty(mask.texture);
                    continue;
                }
                string path = AssetDatabase.GenerateUniqueAssetPath($"{SceneFolder(surface)}/{Safe(surface.name)} - {Safe(mask.renderer.name)} Climbable Mask.asset");
                AssetDatabase.CreateAsset(mask.texture, path);
                newAssets = true;
            }
            if (newAssets || changed.Count > 0) { EditorUtility.SetDirty(surface); }
        }

        /// <summary>The object's mesh renderers with a material that shows climbable areas, but whose mesh has no UV2 (or can't be read).</summary>
        public static List<MeshRenderer> RenderersNeedingUV2(PaintedClimbableSurface surface)
        {
            var needing = new List<MeshRenderer>();
            foreach (MeshRenderer renderer in surface.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (PaintedClimbableSurface.HasMaskMaterial(renderer) && PaintedClimbableSurface.MaskMesh(renderer) == null
                    && renderer.TryGetComponent(out MeshFilter filter) && filter.sharedMesh != null)
                {
                    needing.Add(renderer);
                }
            }
            return needing;
        }

        /// <summary>
        /// Gives a renderer's mesh a UV2: Unity's cube is swapped for one with a UV2 (see CubePath); a mesh imported from a
        /// model file has Generate Lightmap UVs (and Read/Write) turned on in its import settings, but only if
        /// changeImporters (it changes the model for everything using it); any other mesh is swapped for a copy with a
        /// UV2 unwrapped by Unity, saved as an asset. Returns whether the mesh has one now.
        /// </summary>
        public static bool GiveUV2(MeshRenderer renderer, bool changeImporters)
        {
            if (renderer == null || !renderer.TryGetComponent(out MeshFilter filter) || filter.sharedMesh == null) { return false; }
            Mesh mesh = filter.sharedMesh;
            string path = AssetDatabase.GetAssetPath(mesh);
            if (path.StartsWith("Library/unity default resources") && mesh.name == "Cube")
            {
                Undo.RecordObject(filter, "Give Mesh a UV2");
                filter.sharedMesh = Cube();
                return true;
            }
            if (AssetImporter.GetAtPath(path) is ModelImporter importer)
            {
                if (!changeImporters) { return false; }
                importer.generateSecondaryUV = true;
                importer.isReadable = true;
                importer.SaveAndReimport();
                return PaintedClimbableSurface.MaskMesh(renderer) != null;
            }
            Mesh copy = Object.Instantiate(mesh);
            copy.name = $"{mesh.name} (UV2)";
            Unwrapping.GenerateSecondaryUVSet(copy);
            AssetDatabase.CreateAsset(copy, AssetDatabase.GenerateUniqueAssetPath($"{SceneFolder(renderer)}/{Safe(renderer.name)} (UV2).asset"));
            Undo.RecordObject(filter, "Give Mesh a UV2");
            filter.sharedMesh = copy;
            return true;
        }

        /// <summary>Unity's cube with a UV2, made the first time it's needed.</summary>
        public static Mesh Cube()
        {
            var cube = AssetDatabase.LoadAssetAtPath<Mesh>(CubePath);
            if (cube != null) { return cube; }
            GameObject probe = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube = Object.Instantiate(probe.GetComponent<MeshFilter>().sharedMesh);
            Object.DestroyImmediate(probe);
            cube.name = "Cube (UV2)";
            Unwrapping.GenerateSecondaryUVSet(cube);
            EnsureFolder(Folder);
            AssetDatabase.CreateAsset(cube, CubePath);
            return cube;
        }

        // The folder for an object's paint assets: one per scene (or for objects in no saved scene, Untitled).
        private static string SceneFolder(Component component)
        {
            string scene = component.gameObject.scene.IsValid() && !string.IsNullOrEmpty(component.gameObject.scene.name)
                ? component.gameObject.scene.name : "Untitled";
            string folder = $"{Folder}/{Safe(scene)}";
            EnsureFolder(folder);
            return folder;
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) { return; }
            string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent)) { EnsureFolder(parent); }
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }

        private static string Safe(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) { name = name.Replace(c, '_'); }
            return name.Replace('/', '_').Replace('|', '_');
        }
    }
}
