using System;
using MeridianMotion.EditorTools;
using MeridianMotion.UI;
using MeridianMotion.UI.EditorTools;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace MeridianMotion.Showcase.HUD.EditorTools
{
    /// <summary>
    /// Makes the showcase HUD's prefabs where they're missing (Tools > Meridian Motion > Showcase > Create Showcase HUD, or
    /// CreateFromCommandLine): the Showcase HUD (a ShowcaseHUD, naming the name tag) and the NPC Label (a small chamfered
    /// panel with the NPC's name, in the UI's theme, over the NPC's head). Made once, they're hand-edited; given -rebuild,
    /// the NPC Label is remade in place (UIAssets.ReplaceContents), as Rebuild UI Assets remakes the UI's.
    /// </summary>
    public static class ShowcaseHUDAssets
    {
        /// <summary>The HUD prefab, which Set Up Scene adds to the showcase's scenes.</summary>
        public const string HUDPath = "Assets/Showcase/HUD/Showcase HUD.prefab";
        /// <summary>The NPCs' name tag.</summary>
        public const string NpcLabelPath = "Assets/Showcase/HUD/NPC Label.prefab";

        // The tag's panel: how opaque, and how far its corners are cut.
        private const float LabelOpacity = 0.85f, LabelChamfer = 7f;
        private static readonly Vector2 LabelSize = new(220f, 40f);
        // Over a standing person's head (m above the NPC's feet).
        private static readonly Vector3 LabelAboveFeet = new(0f, 2.2f, 0f);
        private const float LabelFontSize = 24f;

        [MenuItem("Tools/Meridian Motion/Showcase/Create Showcase HUD", priority = 200)]
        public static void CreateFromMenu() => Create();

        /// <summary>For -executeMethod: Create, then exits (code 1 if it threw).</summary>
        public static void CreateFromCommandLine()
        {
            bool made = true;
            try { Create(Array.IndexOf(Environment.GetCommandLineArgs(), "-rebuild") >= 0); }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                made = false;
            }
            if (Application.isBatchMode) { EditorApplication.Exit(made ? 0 : 1); }
        }

        /// <summary>Makes whichever of the HUD's prefabs are missing (rebuilding, remakes the NPC Label in place).</summary>
        public static void Create(bool rebuild = false)
        {
            AssetFolders.Ensure("Assets/Showcase/HUD");
            UIThemeFallback.Set();
            var label = AssetDatabase.LoadAssetAtPath<GameObject>(NpcLabelPath);
            if (label == null) { label = Save(NpcLabel(), NpcLabelPath); }
            else if (rebuild)
            {
                GameObject made = NpcLabel();
                try
                {
                    using var editing = new PrefabUtility.EditPrefabContentsScope(NpcLabelPath);
                    UIAssets.ReplaceContents(editing.prefabContentsRoot, made);
                }
                finally { UnityEngine.Object.DestroyImmediate(made); }
            }
            if (AssetDatabase.LoadAssetAtPath<GameObject>(HUDPath) == null)
            {
                var hud = new GameObject("Showcase HUD").AddComponent<ShowcaseHUD>();
                hud.NpcLabelPrefab = label;
                Save(hud.gameObject, HUDPath);
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[ShowcaseHUDAssets] The showcase HUD is ready: '{HUDPath}'.");
        }

        private static GameObject Save(GameObject made, string path)
        {
            try { return PrefabUtility.SaveAsPrefabAsset(made, path); }
            finally { UnityEngine.Object.DestroyImmediate(made); }
        }

        private static GameObject NpcLabel()
        {
            var root = new GameObject("NPC Label", typeof(RectTransform));
            ((RectTransform)root.transform).sizeDelta = LabelSize;
            var panel = root.AddComponent<ChamferedBox>();
            panel.Chamfer = LabelChamfer;
            panel.Corners = UIShapeMesh.Corners.TopLeft | UIShapeMesh.Corners.BottomRight;
            panel.OutlineRole = UIColorRole.Highlight;
            panel.color = new Color(1f, 1f, 1f, LabelOpacity);
            panel.raycastTarget = false;
            var label = root.AddComponent<NpcLabel>();
            label.ParentCanvas = UICanvas.InWorld;
            label.AllowMultiple = true;
            label.CloseOnCancelButton = false;
            label.NeedsCursor = false;
            var textObject = new GameObject("Name", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(root.transform, false);
            var rect = (RectTransform)textObject.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var text = textObject.GetComponent<TextMeshProUGUI>();
            text.fontSize = LabelFontSize;
            var themed = textObject.AddComponent<ThemedText>();
            themed.Display = true;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            var fields = new SerializedObject(label);
            fields.FindProperty("nameText").objectReferenceValue = text;
            fields.FindProperty("worldPositionOffset").vector3Value = LabelAboveFeet;
            fields.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }
    }
}
