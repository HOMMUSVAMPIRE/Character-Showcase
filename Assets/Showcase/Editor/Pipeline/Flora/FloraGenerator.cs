using System.Collections.Generic;
using System.IO;
using System.Linq;
using MeridianMotion.EditorTools;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace MeridianMotion.Showcase.Pipeline
{
    /// <summary>
    /// Generates the showcase's flora: trees, bushes, ferns, vines and ivy as textured leaf cards, each with its LOD chain
    /// down to a billboard (ReferenceAssets/OpenTasks.md, category P). Tools > Meridian Motion > Showcase > Generate Flora
    /// (or GenerateFromCommandLine):
    /// <list type="number">
    /// <item>runs Tools/Flora/generate_flora.py in Blender, which writes the models and their textures into ModelFolder
    /// (Trees/, Bushes/, Vines/, Ivy/, Textures/);</item>
    /// <item>sets up the textures' imports (normal maps; leaf textures' mipmaps keeping their alpha coverage, so far
    /// trees don't go bald; sizes by kind; compressed) and the models' (no cameras, lights or animation; tangents for the
    /// normal maps; each model's materials remapped to the flora's own);</item>
    /// <item>makes the flora's materials, one per kind, in ModelFolder/Materials, on the flora's shaders
    /// (Assets/Showcase/Flora): leaf cards, bush cores and billboards on Flora Leaf, bark on Flora Bark. A material that's
    /// there keeps its settings (they're hand-tuned): only its shader and textures are set again;</item>
    /// <item>makes a prefab of each model in PrefabFolder (a variant of the model, so it follows it), its LOD Group
    /// cross-fading between LODs, and its last LOD (the billboard) casting no shadow.</item>
    /// </list>
    /// The level builders place the prefabs (Graybox.Tree, Planter, and the lawns' shrubs). With -skipBlender (or from the
    /// menu, without Blender), it does all but the first step, from the models that are there.
    /// </summary>
    public static class FloraGenerator
    {
        private const string LogName = "Generate Flora";

        /// <summary>Where Blender writes the models and textures, and the materials are made.</summary>
        public const string ModelFolder = "Assets/Showcase/Models/Flora";

        /// <summary>Where the flora's prefabs are made: the level builders place them from here.</summary>
        public const string PrefabFolder = "Assets/Showcase/Prefabs/Flora";

        private const string MaterialFolder = ModelFolder + "/Materials";
        private const string TextureFolder = ModelFolder + "/Textures";
        private const string GeneratorScript = "Tools/Flora/generate_flora.py";
        private const string LeafShaderPath = "Assets/Showcase/Flora/FloraLeaf.shader";
        private const string BarkShaderPath = "Assets/Showcase/Flora/FloraBark.shader";
        private static readonly string[] Categories = { "Trees", "Bushes", "Vines", "Ivy" };

        /// <summary>Each texture kind's largest size (texels): billboards are a few pixels across where they're drawn.</summary>
        private static int MaxSize(string name) => name.StartsWith("T_Card_") ? 1024 : 512;

        [MenuItem("Tools/Meridian Motion/Showcase/Generate Flora", priority = 102)]
        public static void GenerateFromMenu()
        {
            if (!BlenderRunner.FindOrAsk("Generate Flora", "the flora", "flora models", out string blender)) { return; }
            Generate(blender);
        }

        /// <summary>
        /// Generates the flora from the command line (-executeMethod MeridianMotion.Showcase.Pipeline.FloraGenerator.GenerateFromCommandLine):
        /// -blender &lt;path&gt; to say where Blender is, or -skipBlender to set up the models that are there.
        /// </summary>
        public static void GenerateFromCommandLine()
        {
            if (!BlenderRunner.FromCommandLine(LogName, "flora models", out string blender))
            {
                CommandLine.Finish(false);
                return;
            }
            CommandLine.Finish(Generate(blender));
        }

        /// <summary>Generates the flora (in Blender, unless blenderPath is null), then sets it up (see the class). Returns whether it succeeded.</summary>
        public static bool Generate(string blenderPath)
        {
            Directory.CreateDirectory(ModelFolder);
            if (blenderPath != null && !BlenderRunner.RunScript(blenderPath, GeneratorScript, ModelFolder, "flora", "Generate Flora", LogName))
            {
                return false;
            }
            AssetDatabase.Refresh();
            string[] models = Categories.Where(c => AssetDatabase.IsValidFolder($"{ModelFolder}/{c}"))
                .SelectMany(c => AssetDatabase.FindAssets("t:Model", new[] { $"{ModelFolder}/{c}" })).Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => p.EndsWith(".fbx")).Distinct().OrderBy(p => p).ToArray();
            if (models.Length == 0)
            {
                Debug.LogError($"[{LogName}] There are no flora models in '{ModelFolder}'. Generate them with Blender.");
                return false;
            }
            SetUpTextures();
            Directory.CreateDirectory(MaterialFolder);
            Directory.CreateDirectory(PrefabFolder);
            AssetDatabase.Refresh();
            var made = new List<string>();
            foreach (string model in models)
            {
                SetUpModel(model);
                made.Add(MakePrefab(model));
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[{LogName}] Made {made.Count} flora prefabs in '{PrefabFolder}': {string.Join(", ", made.Select(Path.GetFileNameWithoutExtension))}.");
            return true;
        }

        /// <summary>The textures' imports: see the class.</summary>
        private static void SetUpTextures()
        {
            foreach (string path in AssetDatabase.FindAssets("t:Texture2D", new[] { TextureFolder }).Select(AssetDatabase.GUIDToAssetPath))
            {
                if (AssetImporter.GetAtPath(path) is not TextureImporter importer) { continue; }
                string name = Path.GetFileNameWithoutExtension(path);
                bool normal = name.EndsWith("_Normal");
                importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                importer.sRGBTexture = !normal;
                importer.alphaSource = normal ? TextureImporterAlphaSource.None : TextureImporterAlphaSource.FromInput;
                importer.mipmapEnabled = true;
                // Leaf shapes keep their coverage in the smaller mips, so trees far off don't thin out to nothing.
                importer.mipMapsPreserveCoverage = !normal && !name.StartsWith("T_Bark_");
                importer.alphaTestReferenceValue = 0.5f;
                importer.wrapMode = name.StartsWith("T_Bark_") ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
                importer.maxTextureSize = MaxSize(name);
                importer.textureCompression = TextureImporterCompression.Compressed;
                importer.SaveAndReimport();
            }
        }

        /// <summary>A model's import (see the class), its materials remapped to the flora's own (made where missing).</summary>
        private static void SetUpModel(string path)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            importer.importCameras = false;
            importer.importLights = false;
            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.importBlendShapes = false;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
            importer.SaveAndReimport();

            // The model's own materials, by name (M_Card_Oak, M_Bark_Oak, M_Imp_Tree_Oak_01...), and those already remapped.
            var names = new HashSet<string>(AssetDatabase.LoadAllAssetsAtPath(path).OfType<Material>().Select(m => m.name));
            foreach (var remapped in importer.GetExternalObjectMap()) { if (remapped.Key.type == typeof(Material)) { names.Add(remapped.Key.name); } }
            foreach (string name in names)
            {
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), name), FloraMaterial(name));
            }
            importer.SaveAndReimport();
        }

        /// <summary>The flora's material of a name (M_Card_Oak...): made if it's missing, its shader and textures set again if not.</summary>
        private static Material FloraMaterial(string name)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            string key = name.StartsWith("M_") ? name.Substring(2) : name;
            bool bark = key.StartsWith("Bark_");
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(bark ? BarkShaderPath : LeafShaderPath);
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool made = material == null;
            if (made)
            {
                material = new Material(shader) { name = name };
                // A bush's dense middle has no texture: a dark green, recoloured to the palette with the leaves.
                if (key.StartsWith("Flat_")) { material.SetColor("_BaseColor", new Color(0.18f, 0.35f, 0.12f)); }
                // A billboard is a still picture of the whole plant: it sways with it, but doesn't flutter.
                if (key.StartsWith("Imp_")) { material.SetFloat("_Flutter", 0f); material.SetFloat("_CanopyNormal", 0.3f); }
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            material.enableInstancing = true;
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{TextureFolder}/T_{key}_BaseColor.png"));
            material.SetTexture("_NormalMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{TextureFolder}/T_{key}_Normal.png"));
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>
        /// The prefab of a model (see the class), at PrefabFolder/[its name].prefab: made again over the one there, so it
        /// keeps its GUID and the levels' instances follow.
        /// </summary>
        private static string MakePrefab(string modelPath)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            try
            {
                if (instance.TryGetComponent(out LODGroup group))
                {
                    LOD[] lods = group.GetLODs();
                    float[] heights = lods.Length switch
                    {
                        4 => new[] { 0.45f, 0.2f, 0.07f, 0.01f },
                        3 => new[] { 0.3f, 0.1f, 0.015f },
                        2 => new[] { 0.12f, 0.03f },
                        _ => lods.Select(l => l.screenRelativeTransitionHeight).ToArray(),
                    };
                    for (int i = 0; i < lods.Length; i++)
                    {
                        lods[i].screenRelativeTransitionHeight = heights[i];
                        lods[i].fadeTransitionWidth = 0.25f;
                    }
                    group.SetLODs(lods);
                    // Dithered between LODs rather than switched, so they don't pop (the flora's shaders cross-fade).
                    group.fadeMode = LODFadeMode.CrossFade;
                    group.animateCrossFading = true;
                    // The last LOD (a billboard, a flat card) casts no shadow: a few pixels across, and its shadow a card's.
                    foreach (Renderer renderer in lods[^1].renderers) { if (renderer != null) { renderer.shadowCastingMode = ShadowCastingMode.Off; } }
                    group.RecalculateBounds();
                }
                string path = $"{PrefabFolder}/{model.name}.prefab";
                PrefabUtility.SaveAsPrefabAsset(instance, path);
                return path;
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }
    }
}
