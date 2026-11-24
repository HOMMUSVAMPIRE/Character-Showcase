using System;
using MeridianMotion.EditorTools;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace MeridianMotion.UI.EditorTools
{
    /// <summary>
    /// The UI's fonts (ReferenceAssets/OpenTasks.md, H3): Chakra Petch (SIL Open Font License; its licence is beside it in
    /// Content/UI/Fonts), Bold for display (headers, numerals, buttons) and Regular for body text. Rebuild UI Fonts makes
    /// each one's TextMeshPro font asset from its font file: a static SDF atlas of the characters in Characters, so it
    /// doesn't change as text is drawn. An asset that's there is rebuilt in place, keeping its GUID; one that isn't is made
    /// in the output folder. Create UI Assets sets them on the theme where its fonts are empty.
    /// </summary>
    public static class UIFonts
    {
        // The package's own, found by GUID (as UIAssets' are).
        private static string BoldFontPath => MeridianMotionAssets.PathOf("a9b402cbc73474c22a2c45e3aedca423", "Assets/MeridianMotion/Content/UI/Fonts/ChakraPetch-Bold.ttf");
        private static string RegularFontPath => MeridianMotionAssets.PathOf("a7dace76d4eed44829faed769dc6cf99", "Assets/MeridianMotion/Content/UI/Fonts/ChakraPetch-Regular.ttf");
        /// <summary>The display font's TextMeshPro asset.</summary>
        public static string DisplayAssetPath => UIAssets.ExistingOrOutput(MeridianMotionAssets.PathOf("2ed61163d33834c2290a6a8f417c01c4", "Assets/MeridianMotion/Content/UI/Fonts/Chakra Petch Bold SDF.asset"), "Fonts/Chakra Petch Bold SDF.asset");
        /// <summary>The body font's TextMeshPro asset.</summary>
        public static string BodyAssetPath => UIAssets.ExistingOrOutput(MeridianMotionAssets.PathOf("6090cd42e51a64f96aa2fae0c695637f", "Assets/MeridianMotion/Content/UI/Fonts/Chakra Petch Regular SDF.asset"), "Fonts/Chakra Petch Regular SDF.asset");

        /// <summary>The characters baked into the atlases: printable ASCII, Latin-1, and the punctuation the UI uses.</summary>
        public static readonly string Characters = BuildCharacters();

        // The atlas: its glyphs' size (points), the padding round each (the SDF's spread), and its size.
        private const int SamplingPointSize = 64, Padding = 6, AtlasSize = 1024;

        [MenuItem("Tools/Meridian Motion/Utilities/Rebuild UI Fonts", priority = 63)]
        public static void RebuildFromMenu() => Rebuild();

        /// <summary>For -executeMethod: Rebuild, then exits (code 1 if it couldn't).</summary>
        public static void RebuildFromCommandLine()
        {
            bool built;
            try { built = Rebuild(); }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                built = false;
            }
            if (Application.isBatchMode) { EditorApplication.Exit(built ? 0 : 1); }
        }

        /// <summary>Makes (or rebuilds) both font assets. False if a font file's missing or a character couldn't be added.</summary>
        public static bool Rebuild() => Build(BoldFontPath, DisplayAssetPath) != null & Build(RegularFontPath, BodyAssetPath) != null;

        /// <summary>The display font's asset, made if it's missing (null if its font file is).</summary>
        public static TMP_FontAsset Display() => Load(DisplayAssetPath) ?? Build(BoldFontPath, DisplayAssetPath);

        /// <summary>The body font's asset, made if it's missing (null if its font file is).</summary>
        public static TMP_FontAsset Body() => Load(BodyAssetPath) ?? Build(RegularFontPath, BodyAssetPath);

        // The asset at path, or a true null (so ?? can follow it).
        private static TMP_FontAsset Load(string path)
        {
            var asset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            return asset != null ? asset : null;
        }

        private static TMP_FontAsset Build(string fontPath, string assetPath)
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>(fontPath);
            if (font == null)
            {
                Debug.LogError($"[UIFonts] There's no font at '{fontPath}'.");
                return null;
            }
            // Made afresh (so a change to the atlas's settings takes), its glyphs added, then made static, so drawing
            // text never changes it (or the repository).
            TMP_FontAsset made = TMP_FontAsset.CreateFontAsset(font, SamplingPointSize, Padding, GlyphRenderMode.SDFAA, AtlasSize, AtlasSize,
                AtlasPopulationMode.Dynamic, enableMultiAtlasSupport: false);
            bool all = made.TryAddCharacters(Characters, out string missing);
            if (!all) { Debug.LogError($"[UIFonts] '{font.name}' has no glyphs (or no room in its atlas) for: {missing}"); }
            made.atlasPopulationMode = AtlasPopulationMode.Static;
            string name = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            made.atlasTextures[0].name = name + " Atlas";
            made.material.name = name + " Material";

            var asset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
            if (asset == null)
            {
                AssetFolders.Ensure(System.IO.Path.GetDirectoryName(assetPath)?.Replace('\\', '/'));
                made.name = name;
                AssetDatabase.CreateAsset(made, assetPath);
                asset = made;
            }
            else
            {
                // Rebuilt in place: the old atlas and material taken out, the new copied in, so its GUID (and what names the
                // font) stays. Its material is a new object, so text that named the old one takes the font's own again.
                foreach (UnityEngine.Object part in AssetDatabase.LoadAllAssetRepresentationsAtPath(assetPath)) { UnityEngine.Object.DestroyImmediate(part, true); }
                EditorUtility.CopySerialized(made, asset);
                asset.name = name;
            }
            // Its atlas and material are saved inside it.
            AssetDatabase.AddObjectToAsset(made.atlasTextures[0], asset);
            AssetDatabase.AddObjectToAsset(made.material, asset);
            if (asset != made) { UnityEngine.Object.DestroyImmediate(made); }
            EditorUtility.SetDirty(asset);
            EditorUtility.SetDirty(asset.atlasTextures[0]);
            AssetDatabase.SaveAssets();
            Debug.Log($"[UIFonts] Built {assetPath} ({asset.characterTable.Count} characters).");
            return all ? asset : null;
        }

        private static string BuildCharacters()
        {
            var characters = new System.Text.StringBuilder();
            for (char c = ' '; c <= '~'; c++) { characters.Append(c); }
            // Latin-1, but for the micro sign, which Chakra Petch hasn't.
            for (char c = '\u00A1'; c <= '\u00FF'; c++) { if (c != '\u00B5') { characters.Append(c); } }
            characters.Append("\u2013\u2014\u2018\u2019\u201C\u201D\u2022\u2026\u2122\u2190\u2191\u2192\u2193");
            return characters.ToString();
        }
    }
}
