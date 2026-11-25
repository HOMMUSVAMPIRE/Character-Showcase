using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MeridianMotion.EditorTools;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MeridianMotion.Showcase.Pipeline
{
    /// <summary>
    /// Pictures of the flora's variants, to choose seeds by (ReferenceAssets/OpenTasks.md, P12): for each kind, its
    /// variants side by side (each at its first LOD, the same distance for its size, the same sun), coloured by the level's
    /// palette (the Lawn grass style's colours, as the levels' grass gives them), one picture per kind, with a report of
    /// which slot has which seed, in Logs/FloraVariants/[time]. Tools > Meridian Motion > Showcase > Picture Flora Variants,
    /// the Flora Seeds inspector's Picture the Variants, or PictureFromCommandLine. It opens a new, empty scene to take them in.
    /// </summary>
    public static class FloraPictures
    {
        private const int Cell = 512;
        private const string LawnStylePath = "Assets/Showcase/Grass/Styles/Lawn.asset";

        [MenuItem("Tools/Meridian Motion/Showcase/Picture Flora Variants", priority = 103)]
        private static void PictureFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) { return; }
            EditorUtility.RevealInFinder(Picture());
        }

        /// <summary>For -executeMethod: pictures the variants (see the class), then exits.</summary>
        public static void PictureFromCommandLine()
        {
            bool ok = true;
            try { Picture(); }
            catch (Exception e) { Debug.LogException(e); ok = false; }
            CommandLine.Finish(ok);
        }

        /// <summary>Takes the pictures (see the class): returns the folder they're in.</summary>
        public static string Picture()
        {
            EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            foreach (Camera c in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)) { Object.DestroyImmediate(c.gameObject); }
            SetPalette();
            string folder = Path.Combine("Logs", "FloraVariants", DateTime.Now.ToString("yyyyMMdd-HHmmss"));
            Directory.CreateDirectory(folder);
            FloraSeeds seeds = FloraGenerator.Seeds();
            var report = new StringBuilder($"# Flora variants, {DateTime.Now:yyyy-MM-dd HH:mm}\n\nEach picture's variants left to right, by slot.\n\n");

            var target = new RenderTexture(Cell, Cell, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
            var cameraObject = new GameObject("Flora Picture Camera") { hideFlags = HideFlags.DontSave };
            var camera = cameraObject.AddComponent<Camera>();
            camera.targetTexture = target;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.55f, 0.62f, 0.7f);
            camera.fieldOfView = 30f;
            try
            {
                foreach (FloraSeeds.Kind kind in seeds.kinds)
                {
                    var variants = new List<(string name, int seed, GameObject prefab)>();
                    for (int slot = 0; slot < (kind.seeds?.Length ?? 0); slot++)
                    {
                        string variant = FloraSeeds.VariantName(kind.kind, slot);
                        if (variant == null) { continue; }
                        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{FloraGenerator.PrefabFolder}/{Path.GetFileName(variant)}.prefab");
                        if (prefab != null) { variants.Add((Path.GetFileName(variant), kind.seeds[slot], prefab)); }
                    }
                    if (variants.Count == 0) { continue; }
                    var sheet = new Texture2D(Cell * variants.Count, Cell, TextureFormat.RGBA32, false);
                    for (int i = 0; i < variants.Count; i++)
                    {
                        var placed = (GameObject)PrefabUtility.InstantiatePrefab(variants[i].prefab);
                        if (placed.TryGetComponent(out LODGroup group)) { group.ForceLOD(0); }
                        Bounds b = PlantBounds(placed);
                        float size = Mathf.Max(b.size.y, Mathf.Max(b.size.x, b.size.z));
                        float distance = size / (2f * Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad)) * 1.15f;
                        Vector3 aim = b.center;
                        cameraObject.transform.SetPositionAndRotation(aim + new Vector3(0f, size * 0.15f, -distance), Quaternion.LookRotation(new Vector3(0f, -size * 0.15f, distance)));
                        camera.nearClipPlane = distance * 0.05f;
                        camera.farClipPlane = distance * 4f;
                        // Twice: a fresh scene's first render has no shadows or lighting set up yet (the first plant came
                        // out black, its bark red), so the picture is the second.
                        camera.Render();
                        camera.Render();
                        RenderTexture previous = RenderTexture.active;
                        RenderTexture.active = target;
                        sheet.ReadPixels(new Rect(0, 0, Cell, Cell), Cell * i, 0);
                        RenderTexture.active = previous;
                        Object.DestroyImmediate(placed);
                    }
                    sheet.Apply();
                    File.WriteAllBytes(Path.Combine(folder, $"{kind.kind}.png"), sheet.EncodeToPNG());
                    Object.DestroyImmediate(sheet);
                    report.AppendLine($"- **{kind.kind}** ({kind.kind}.png): {string.Join(", ", variants.Select(v => $"{v.name} (seed {v.seed})"))}");
                }
            }
            finally
            {
                camera.targetTexture = null;
                Object.DestroyImmediate(cameraObject);
                target.Release();
                Object.DestroyImmediate(target);
            }
            File.WriteAllText(Path.Combine(folder, "Variants.md"), report.ToString());
            Debug.Log($"[Flora pictures] Wrote {folder}.");
            return folder;
        }

        /// <summary>The level's palette, as a level's grass gives it (GrassField): the Lawn style's root and tip colours.</summary>
        private static void SetPalette()
        {
            var style = AssetDatabase.LoadAssetAtPath<ScriptableObject>(LawnStylePath);
            if (style == null) { Shader.SetGlobalFloat("_FloraPalette", 0f); return; }
            var read = new SerializedObject(style);
            Shader.SetGlobalVector("_FloraRootColour", read.FindProperty("rootColour").colorValue.linear);
            Shader.SetGlobalVector("_FloraTipColour", read.FindProperty("tipColour").colorValue.linear);
            Shader.SetGlobalFloat("_FloraPalette", 1f);
            Shader.SetGlobalFloat("_FloraSwayScale", 0f);
        }

        private static Bounds PlantBounds(GameObject placed)
        {
            Renderer[] renderers = placed.TryGetComponent(out LODGroup group) && group.GetLODs().Length > 0
                ? group.GetLODs()[0].renderers.Where(r => r != null).ToArray() : placed.GetComponentsInChildren<Renderer>();
            Bounds b = renderers[0].bounds;
            foreach (Renderer r in renderers) { b.Encapsulate(r.bounds); }
            return b;
        }
    }
}
