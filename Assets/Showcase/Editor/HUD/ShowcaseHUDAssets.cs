using System;
using MeridianMotion.EditorTools;
using MeridianMotion.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace MeridianMotion.Showcase.HUD.EditorTools
{
    /// <summary>
    /// Makes the showcase HUD's prefabs where they're missing (Tools > Meridian Motion > Showcase > Create Showcase HUD, or
    /// CreateFromCommandLine): the Showcase HUD (a ShowcaseHUD, naming the name tag) and the NPC Label (a graphite tag with
    /// the NPC's name, in the levels' palette, over the NPC's head). Made once, they're hand-edited.
    /// </summary>
    public static class ShowcaseHUDAssets
    {
        /// <summary>The HUD prefab, which Set Up Scene adds to the showcase's scenes.</summary>
        public const string HUDPath = "Assets/Showcase/HUD/Showcase HUD.prefab";
        /// <summary>The NPCs' name tag.</summary>
        public const string NpcLabelPath = "Assets/Showcase/HUD/NPC Label.prefab";

        private static readonly Color Graphite = new(0x2D / 255f, 0x31 / 255f, 0x38 / 255f, 0.85f), OffWhite = new(0xF2 / 255f, 0xF4 / 255f, 0xF6 / 255f);
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
            try { Create(); }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                made = false;
            }
            if (Application.isBatchMode) { EditorApplication.Exit(made ? 0 : 1); }
        }

        /// <summary>Makes whichever of the HUD's prefabs are missing.</summary>
        public static void Create()
        {
            AssetFolders.Ensure("Assets/Showcase/HUD");
            GameObject label = AssetDatabase.LoadAssetAtPath<GameObject>(NpcLabelPath) ?? Save(NpcLabel(), NpcLabelPath);
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
            var root = new GameObject("NPC Label", typeof(RectTransform), typeof(Image));
            ((RectTransform)root.transform).sizeDelta = LabelSize;
            root.GetComponent<Image>().color = Graphite;
            root.GetComponent<Image>().raycastTarget = false;
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
            text.color = OffWhite;
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
