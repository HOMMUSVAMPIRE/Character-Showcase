using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MeridianMotion.Showcase.Grass.EditorTools
{
    /// <summary>
    /// Makes the grass's assets where they're missing (then they're hand-edited): two styles to start from, at either end
    /// of what the grass can be, and the lawns' material. The level builders give new lawns the Lawn style and the
    /// material (Graybox.LawnStylePath, LawnMaterialPath).
    /// <list type="bullet">
    /// <item>Styles/Lawn.asset: short and dense, a mown lawn (the style's defaults);</item>
    /// <item>Styles/Wild Grass.asset: long, looser, drooping, in clumps that differ more, in drier colours;</item>
    /// <item>Lawn.mat: the Lawn shader, which the grass's field colours to match its style.</item>
    /// </list>
    /// </summary>
    public static class GrassAssets
    {
        /// <summary>Where the grass's assets are.</summary>
        public const string Folder = "Assets/Showcase/Grass";

        public const string LawnStylePath = Folder + "/Styles/Lawn.asset";
        public const string WildGrassStylePath = Folder + "/Styles/Wild Grass.asset";
        public const string LawnMaterialPath = Folder + "/Lawn.mat";

        [MenuItem("Tools/Meridian Motion/Showcase/Create Grass Assets")]
        private static void CreateFromMenu()
        {
            string made = Create();
            Debug.Log(made.Length > 0 ? $"[Grass] Made {made}." : "[Grass] The grass's assets are all there already.");
        }

        /// <summary>For -executeMethod: makes what's missing (see the class), then exits.</summary>
        public static void CreateFromCommandLine()
        {
            int code = 0;
            try { CreateFromMenu(); }
            catch (Exception e) { Debug.LogException(e); code = 1; }
            if (Application.isBatchMode) { EditorApplication.Exit(code); }
        }

        /// <summary>Makes whichever of the assets are missing: what it made, by name ("" if none).</summary>
        public static string Create()
        {
            Directory.CreateDirectory(Folder + "/Styles");
            AssetDatabase.Refresh();
            var made = new System.Collections.Generic.List<string>();
            if (AssetDatabase.LoadAssetAtPath<GrassStyle>(LawnStylePath) == null)
            {
                AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<GrassStyle>(), LawnStylePath);
                made.Add("the Lawn style");
            }
            if (AssetDatabase.LoadAssetAtPath<GrassStyle>(WildGrassStylePath) == null)
            {
                AssetDatabase.CreateAsset(WildGrass(), WildGrassStylePath);
                made.Add("the Wild Grass style");
            }
            if (AssetDatabase.LoadAssetAtPath<Material>(LawnMaterialPath) == null)
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(Folder + "/Lawn.shader");
                AssetDatabase.CreateAsset(new Material(shader) { name = "Lawn" }, LawnMaterialPath);
                made.Add("the lawn material");
            }
            AssetDatabase.SaveAssets();
            return string.Join(", ", made);
        }

        /// <summary>Tall wild grass: the far end of what the style can be.</summary>
        private static GrassStyle WildGrass()
        {
            GrassStyle s = ScriptableObject.CreateInstance<GrassStyle>();
            s.length = new Vector2(0.45f, 1.05f);
            s.width = new Vector2(0.03f, 0.055f);
            s.bend = new Vector2(0.25f, 0.75f);
            s.droop = 0.6f;
            s.spacing = 0.1f;
            s.edgeLength = 1.2f;
            s.clumpSize = 0.9f;
            s.clumpLength = 0.5f;
            s.clumpLean = 0.5f;
            s.rootColour = new Color(0.19f, 0.29f, 0.1f);
            s.tipColour = new Color(0.6f, 0.62f, 0.28f);
            s.colourVariation = 0.35f;
            s.clumpTint = 0.55f;
            s.rootShade = 0.6f;
            s.groundShade = 0.55f;
            s.translucency = 0.2f;
            s.detailDistance = 20f;
            s.segmentLength = 0.12f;
            s.segments = 7;
            return s;
        }
    }
}
