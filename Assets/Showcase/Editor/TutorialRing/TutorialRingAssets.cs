using System;
using MeridianMotion.Characters;
using MeridianMotion.EditorTools;
using MeridianMotion.UI;
using MeridianMotion.UI.EditorTools;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MeridianMotion.Showcase.TutorialRing.EditorTools
{
    /// <summary>
    /// Makes the Tutorial Ring's assets where they're missing (Tools > Meridian Motion > Showcase > Tutorial Ring > Create
    /// Tutorial Ring Assets, or CreateFromCommandLine): its lessons (Tutorial Lessons.asset: the eighteen lessons, their
    /// prompts and what does each), its character's tuning (Tutorial No Air Jumps.asset: Jumping's Max Jumps at 1, applied
    /// by the spawner to the character's own copy of its movement), and its Lesson Prompt window. Made once, they're
    /// hand-edited; given -rebuild, the Lesson Prompt is remade in place, as Rebuild UI Assets remakes the UI's.
    /// </summary>
    public static class TutorialRingAssets
    {
        /// <summary>The folder the tutorial's own assets are in.</summary>
        public const string Folder = "Assets/Showcase/TutorialRing";
        /// <summary>The lessons.</summary>
        public const string CatalogPath = Folder + "/Tutorial Lessons.asset";
        /// <summary>The Lesson Prompt window.</summary>
        public const string PromptPath = Folder + "/Lesson Prompt.prefab";
        /// <summary>The tutorial character's tuning: one jump.</summary>
        public const string PresetPath = "Assets/Showcase/Movement/Tutorial No Air Jumps.asset";

        // The prompt: its size, how far above the screen's bottom, its text's size, and its backing's opacity and corners.
        private static readonly Vector2 PromptSize = new(900f, 84f);
        private const float PromptAboveBottom = 150f, PromptFontSize = 34f, BackingOpacity = 0.8f, BackingChamfer = 12f, TextPadding = 24f;

        /// <summary>
        /// The lessons, in the order they're taught (the design's: ReferenceAssets/Implementation Guidance/TutorialRingDocs): id, title, prompt,
        /// follow-up, the event that does it and the state it must be in. The level's zones name the ids.
        /// </summary>
        public static readonly (string id, string title, string prompt, string followUp, string clearsOn, string clearsOnlyWhile)[] Lessons =
        {
            ("L01-move", "Move and look", "Move with {Move} and look around with {Look}.", "", "", ""),
            ("L02-jump", "Jump", "Press {Jump} to jump up the step.", "", "Jumped", ""),
            ("L03-sprint", "Sprint and sprint jump", "Hold {Sprint} to run, then press {Jump} to leap the gap.", "", "Jumped", ""),
            ("L04-crouch", "Crouch", "Press {Crouch} to crouch through the tunnel, and again to stand.", "", "CrouchStarted", ""),
            ("L05-slide", "Slide", "Sprint with {Sprint}, then press {Crouch} to slide under the bar.", "A slide keeps your speed: carry it across the gap.", "SlideStarted", ""),
            ("L06-roll", "Roll", "Press {Roll} to roll through before the blockers close.", "", "RollStarted", ""),
            ("L07-vault", "Vault", "Run at the low wall: you vault it by yourself.", "", "VaultStarted", ""),
            ("L08-ledge", "Ledge grab and climb", "Jump at the wall to grab its ledge, then press {Jump} to climb up.", "", "LedgeClimbStarted", ""),
            ("L09-wallrun", "Wall run", "Only plated walls with level grooves can be run along. Sprint, jump and steer along this one to run across.", "", "WallRunStarted", ""),
            ("L10-walljump", "Wall slide and wall jump", "Only plated walls with upright grooves can be slid down. Jump at one, then press {Jump} to leap to the other.", "Keep going: wall to wall, up the shaft.", "WallJumped", ""),
            ("L11-climb", "Climbable panel", "Move into the marked panel to climb it. {Jump} leaps off.", "", "WallClimbStarted", ""),
            ("L12-ladder", "Ladder", "Walk into the ladder to climb it.", "", "WallClimbStarted", ""),
            ("L13-crate", "Push and pull", "Hold {Interact} at the crate, then steer to push or pull it.", "Push it against the ledge and climb up.", "GrabStarted", ""),
            ("L14-bounce", "Bounce pad", "Step onto the bounce pad to be launched up.", "", "Bounced", ""),
            ("L15-rope", "Rope swing", "Jump to catch the rope, swing with {Move}, and press {Jump} to let go.", "", "RopeGrabbed", ""),
            ("L16-zipline", "Zipline", "Jump up to the cable to ride the zipline down.", "", "ZiplineStarted", ""),
            ("L17-swim", "Swim and dive", "Swim out, then press {Crouch} to dive under the arch.", "", "DiveStarted", ""),
            ("L18-dash", "Dash and air dash", "Press {Dash} to dash.", "In the air, {Dash} carries a sprint jump further: cross the gap.", "DashStarted", ""),
        };

        [MenuItem("Tools/Meridian Motion/Showcase/Tutorial Ring/Create Tutorial Ring Assets", priority = 210)]
        public static void CreateFromMenu() => Create();

        /// <summary>For -executeMethod: Create (-rebuild remakes the Lesson Prompt in place), then exits (code 1 if it threw).</summary>
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

        /// <summary>Makes whichever of the tutorial's assets are missing (rebuilding, remakes the Lesson Prompt in place).</summary>
        public static void Create(bool rebuild = false)
        {
            AssetFolders.Ensure(Folder);
            AssetFolders.Ensure("Assets/Showcase/Movement");
            UIThemeFallback.Set();
            if (AssetDatabase.LoadAssetAtPath<TutorialLessonCatalog>(CatalogPath) == null) { AssetDatabase.CreateAsset(MakeCatalog(), CatalogPath); }
            if (AssetDatabase.LoadAssetAtPath<MovementTuningPreset>(PresetPath) == null) { AssetDatabase.CreateAsset(MakePreset(), PresetPath); }
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PromptPath) == null)
            {
                GameObject made = PromptWindow();
                try { PrefabUtility.SaveAsPrefabAsset(made, PromptPath); }
                finally { UnityEngine.Object.DestroyImmediate(made); }
            }
            else if (rebuild)
            {
                GameObject made = PromptWindow();
                try
                {
                    using var editing = new PrefabUtility.EditPrefabContentsScope(PromptPath);
                    UIAssets.ReplaceContents(editing.prefabContentsRoot, made);
                }
                finally { UnityEngine.Object.DestroyImmediate(made); }
            }
            InstallHeightShade();
            AssetDatabase.SaveAssets();
            Debug.Log($"[TutorialRingAssets] The Tutorial Ring's assets are ready in '{Folder}' (and '{PresetPath}').");
        }

        /// <summary>The height shading's material (its shader, HeightShade.shader, beside it).</summary>
        public const string HeightShadeMaterialPath = "Assets/Showcase/Materials/Height Shade.mat";
        /// <summary>What the height shading's pass is called on the renderers.</summary>
        public const string HeightShadeFeatureName = "Height Shade";

        /// <summary>
        /// Makes the height shading's material where it's missing, and adds its pass (a Full Screen Pass Renderer Feature,
        /// before post-processing, reading depth) to every URP renderer of the project's render pipeline assets that
        /// hasn't one. It does nothing in a scene without a HeightShade component.
        /// </summary>
        public static void InstallHeightShade()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(HeightShadeMaterialPath);
            if (material == null)
            {
                Shader shader = Shader.Find("Hidden/Meridian Motion/Showcase/Height Shade");
                if (shader == null) { Debug.LogWarning("[TutorialRingAssets] The Height Shade shader isn't there: no height shading."); return; }
                material = new Material(shader) { name = "Height Shade" };
                AssetDatabase.CreateAsset(material, HeightShadeMaterialPath);
            }
            foreach (string guid in AssetDatabase.FindAssets("t:UniversalRendererData"))
            {
                var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(AssetDatabase.GUIDToAssetPath(guid));
                if (data == null) { continue; }
                // After post-processing, so it scales the colour as it's shown, not HDR values the tonemapping would then
                // squeeze back towards white.
                const FullScreenPassRendererFeature.InjectionPoint injection = FullScreenPassRendererFeature.InjectionPoint.AfterRenderingPostProcessing;
                var existing = data.rendererFeatures.Find(f => f != null && f.name == HeightShadeFeatureName) as FullScreenPassRendererFeature;
                if (existing != null)
                {
                    if (existing.injectionPoint != injection) { existing.injectionPoint = injection; EditorUtility.SetDirty(existing); EditorUtility.SetDirty(data); }
                    continue;
                }
                var feature = ScriptableObject.CreateInstance<FullScreenPassRendererFeature>();
                feature.name = HeightShadeFeatureName;
                feature.passMaterial = material;
                feature.injectionPoint = injection;
                feature.requirements = ScriptableRenderPassInput.Depth;
                feature.fetchColorBuffer = true;
                AssetDatabase.AddObjectToAsset(feature, data);
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId);
                var fields = new SerializedObject(data);
                SerializedProperty list = fields.FindProperty("m_RendererFeatures"), map = fields.FindProperty("m_RendererFeatureMap");
                list.arraySize++;
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = feature;
                map.arraySize++;
                map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
                fields.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(data);
                Debug.Log($"[TutorialRingAssets] Added the height shading's pass to '{data.name}'.");
            }
        }

        /// <summary>A catalog of the lessons (Lessons).</summary>
        public static TutorialLessonCatalog MakeCatalog()
        {
            var catalog = ScriptableObject.CreateInstance<TutorialLessonCatalog>();
            foreach (var (id, title, prompt, followUp, clearsOn, clearsOnlyWhile) in Lessons)
            {
                catalog.Lessons.Add(new TutorialLesson
                {
                    id = id, title = title, prompt = prompt, followUp = followUp, clearsOn = clearsOn, clearsOnlyWhile = clearsOnlyWhile,
                });
            }
            return catalog;
        }

        /// <summary>The tutorial character's tuning: Jumping's Max Jumps at 1, nothing else.</summary>
        public static MovementTuningPreset MakePreset()
        {
            var preset = ScriptableObject.CreateInstance<MovementTuningPreset>();
            preset.Include(typeof(Jumping), "maxJumps", true);
            ((Jumping)preset.PartFor(typeof(Jumping)).values).MaxJumps = 1;
            var fields = new SerializedObject(preset);
            fields.FindProperty("notes").stringValue = "The Tutorial Ring's character: one jump, no air jumps (its lessons and course are sized for it). " +
                                                       "Applied by its spawner's Tuning Preset to the character's own copy of its movement.";
            fields.ApplyModifiedPropertiesWithoutUndo();
            return preset;
        }

        // The prompt: a dark chamfered backing at the screen's lower centre, with a line of large text.
        private static GameObject PromptWindow()
        {
            var root = new GameObject("Lesson Prompt", typeof(RectTransform));
            var rect = (RectTransform)root.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = PromptSize;
            rect.anchoredPosition = new Vector2(0f, PromptAboveBottom);
            var prompt = root.AddComponent<TutorialLessonPrompt>();
            prompt.ParentCanvas = UICanvas.Default;
            prompt.CloseOnCancelButton = false;
            prompt.NeedsCursor = false;
            prompt.ExemptFromAutoClose = true;
            prompt.ClickBlockerStyle = ClickBlockStyle.None;

            var contents = new GameObject("Contents", typeof(RectTransform));
            contents.transform.SetParent(root.transform, false);
            Stretch((RectTransform)contents.transform, 0f);
            var backing = contents.AddComponent<ChamferedBox>();
            backing.Chamfer = BackingChamfer;
            backing.Corners = UIShapeMesh.Corners.TopLeft | UIShapeMesh.Corners.BottomRight;
            backing.OutlineRole = UIColorRole.Highlight;
            backing.color = new Color(1f, 1f, 1f, BackingOpacity);
            backing.raycastTarget = false;

            var textObject = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(contents.transform, false);
            Stretch((RectTransform)textObject.transform, TextPadding);
            var text = textObject.GetComponent<TextMeshProUGUI>();
            text.fontSize = PromptFontSize;
            text.enableAutoSizing = true;
            text.fontSizeMin = PromptFontSize * 0.6f;
            text.fontSizeMax = PromptFontSize;
            textObject.AddComponent<ThemedText>();
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;

            var fields = new SerializedObject(prompt);
            fields.FindProperty("text").objectReferenceValue = text;
            fields.FindProperty("contents").objectReferenceValue = contents;
            fields.ApplyModifiedPropertiesWithoutUndo();
            contents.SetActive(false);
            return root;
        }

        private static void Stretch(RectTransform rect, float inset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, 0f);
            rect.offsetMax = new Vector2(-inset, 0f);
        }
    }
}
