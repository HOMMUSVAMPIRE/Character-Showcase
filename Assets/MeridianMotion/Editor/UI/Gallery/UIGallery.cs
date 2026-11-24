using System;
using System.Collections.Generic;
using System.IO;
using MeridianMotion.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace MeridianMotion.UI.EditorTools
{
    /// <summary>
    /// The UI gallery (ReferenceAssets/OpenTasks.md, H2): every shape, widget and icon in every state, laid out in pages on
    /// the theme's backdrop, to be looked at beside the reference sheet. Open UI Gallery builds it in a new, unsaved scene;
    /// CaptureFromCommandLine renders each page to a PNG in Logs/UIGallery (its canvas a screen-space camera's, drawn into
    /// a render texture, so batch mode can capture it). A tool, not a test: its pictures are judged by eye. A page is a
    /// method of a part of this class (UIGallery.[Page].cs), listed in Pages. Given -galleryFonts [folder], it also
    /// captures the Type page in each font file there (Type (font).png), to compare fonts before one's added.
    /// </summary>
    public static partial class UIGallery
    {
        /// <summary>A page's name (its picture's file name) and what builds it.</summary>
        private static readonly (string name, Action<GalleryPage> build)[] Pages =
        {
            ("Shapes", ShapesPage),
            ("Type", TypePage),
            ("Icons", IconsPage),
            ("Windows", WindowsPage),
            ("Widgets", WidgetsPage),
        };

        // The pictures' size: the canvases' reference resolution, so a page is drawn at the size the UI is designed for.
        private const int Width = 1920, Height = 1080;
        private const string CaptureFolder = "Logs/UIGallery";

        [MenuItem("Tools/Meridian Motion/Utilities/Open UI Gallery", priority = 62)]
        public static void OpenFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) { return; }
            UIThemeFallback.Set();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Camera camera = NewCamera();
            float x = 0f;
            // The pages side by side in the world, each its own canvas; the Game view shows the first.
            foreach ((string name, Action<GalleryPage> build) in Pages)
            {
                GalleryPage page = NewPage(name, camera, Theme);
                build(page);
                if (x > 0f)
                {
                    // Pages after the first are world-space copies beside it, to scroll to in the Scene view.
                    page.Canvas.renderMode = RenderMode.WorldSpace;
                    page.Canvas.transform.position = new Vector3(x, 0f, 0f);
                }
                x += Width + 200f;
            }
            Debug.Log($"[UIGallery] Built {Pages.Length} page(s) in a new scene: the Game view shows the first.");
        }

        /// <summary>For -executeMethod: renders every page to Logs/UIGallery/[page].png, then exits (code 1 if it couldn't).</summary>
        public static void CaptureFromCommandLine()
        {
            bool captured;
            try { captured = Capture(); }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                captured = false;
            }
            if (Application.isBatchMode) { EditorApplication.Exit(captured ? 0 : 1); }
        }

        /// <summary>Renders every page to a PNG in Logs/UIGallery, in a new scene (not saved). False if it couldn't.</summary>
        public static bool Capture()
        {
            UIThemeFallback.Set();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            string folder = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", CaptureFolder);
            Directory.CreateDirectory(folder);
            Camera camera = NewCamera();
            var target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
            camera.targetTexture = target;
            var picture = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            try
            {
                foreach ((string name, Action<GalleryPage> build) in Pages) { CapturePage(name, build, Theme, camera, target, picture, folder); }
                foreach (string file in FontCandidates())
                {
                    TMP_FontAsset font = TMP_FontAsset.CreateFontAsset(file, 0, 90, 9, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 1024, 1024);
                    if (font == null)
                    {
                        Debug.LogError($"[UIGallery] Couldn't load the font '{file}'.");
                        return false;
                    }
                    UITheme theme = UnityEngine.Object.Instantiate(Theme);
                    theme.DisplayFont = font;
                    theme.BodyFont = font;
                    CapturePage($"Type ({Path.GetFileNameWithoutExtension(file)})", TypePage, theme, camera, target, picture, folder);
                    UnityEngine.Object.DestroyImmediate(theme);
                }
                return true;
            }
            finally
            {
                camera.targetTexture = null;
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(picture);
            }
        }

        // Builds a page in theme, renders it to name.png in folder, and takes it down.
        private static void CapturePage(string name, Action<GalleryPage> build, UITheme theme, Camera camera, RenderTexture target, Texture2D picture, string folder)
        {
            GalleryPage page = NewPage(name, camera, theme);
            build(page);
            Canvas.ForceUpdateCanvases();
            foreach (TMP_Text text in page.Canvas.GetComponentsInChildren<TMP_Text>()) { text.ForceMeshUpdate(); }
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            picture.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            picture.Apply();
            RenderTexture.active = previous;
            string path = Path.Combine(folder, name + ".png");
            File.WriteAllBytes(path, picture.EncodeToPNG());
            Debug.Log($"[UIGallery] Captured {path}");
            UnityEngine.Object.DestroyImmediate(page.Canvas.gameObject);
        }

        // The font files in the folder after -galleryFonts on the command line, if it's given.
        private static IEnumerable<string> FontCandidates()
        {
            string[] args = Environment.GetCommandLineArgs();
            int at = Array.IndexOf(args, "-galleryFonts");
            if (at < 0 || at + 1 >= args.Length || !Directory.Exists(args[at + 1])) { yield break; }
            var files = new List<string>(Directory.GetFiles(args[at + 1]));
            files.Sort(StringComparer.Ordinal);
            foreach (string file in files)
            {
                string extension = Path.GetExtension(file).ToLowerInvariant();
                if (extension == ".ttf" || extension == ".otf") { yield return file; }
            }
        }

        /// <summary>The theme the gallery draws in: the package's (the UI Manager's), else the built-in one.</summary>
        internal static UITheme Theme
        {
            get
            {
                var theme = AssetDatabase.LoadAssetAtPath<UITheme>(UIAssets.ThemePath);
                return theme != null ? theme : UITheme.BuiltIn;
            }
        }

        private static Camera NewCamera()
        {
            var camera = new GameObject("Gallery Camera").AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Theme.Color(UIColorRole.Backdrop);
            camera.orthographic = true;
            camera.cullingMask = 1 << LayerMask.NameToLayer("UI");
            camera.transform.position = new Vector3(0f, 0f, -10f);
            return camera;
        }

        private static GalleryPage NewPage(string name, Camera camera, UITheme theme)
        {
            var canvasObject = new GameObject($"Gallery: {name}", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvasObject.layer = LayerMask.NameToLayer("UI");
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 5f;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(Width, Height);
            scaler.matchWidthOrHeight = 0.5f;
            return new GalleryPage(name, canvas, theme);
        }
    }
}
