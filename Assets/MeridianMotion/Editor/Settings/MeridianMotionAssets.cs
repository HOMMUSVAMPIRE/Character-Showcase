using UnityEditor;
using UnityEditor.PackageManager;

namespace MeridianMotion.EditorTools
{
    /// <summary>
    /// Where the package's own assets are, wherever it's installed (in Assets/, an embedded package or a read-only one
    /// under Packages/): each is found by its GUID, which its .meta fixes, so a tool never assumes the folders it came in.
    /// A path is the one this project had when the asset is missing (deleted, or not imported yet), so a tool reports
    /// the asset missing by the name it's known by. What the tools make goes in the folder Project Settings > Meridian
    /// Motion names (MeridianMotionSettings), never the package's.
    /// </summary>
    public static class MeridianMotionAssets
    {
        public static string CharacterPrefab => PathOf("2879140e65c9afe4aa77bb8c108127d5", "Assets/MeridianMotion/Content/Prefabs/Character.prefab");
        public static string CameraRigPrefab => PathOf("9e3b913ccd23a4fbe9e28087eaf83dbd", "Assets/MeridianMotion/Content/Prefabs/Camera Rig.prefab");
        public static string InputManagerPrefab => PathOf("4171f6b8e7ec346ab97049d080623f2c", "Assets/MeridianMotion/Content/Prefabs/Input Manager.prefab");
        public static string MannequinController => PathOf("0e62825eea9ca44eda12def1fb1dd704", "Assets/MeridianMotion/Content/Animation/Mannequin.controller");
        public static string MannequinLayout => PathOf("f6f3eaaea1df6475eb6e0dcbd9e04485", "Assets/MeridianMotion/Content/Animation/Mannequin Layout.asset");
        public static string MannequinTranslator => PathOf("39166854aeb7948389eaf8c2c237275a", "Assets/MeridianMotion/Content/Animation/Mannequin Animator Translator.asset");
        public static string GenericRigLayout => PathOf("ce559096014d4cce92c5e3a832c65901", "Assets/MeridianMotion/Content/Animation/Generic Rig Layout.asset");
        public static string MannequinModel => PathOf("3022c8572970842cf91f3ca29d242abc", "Assets/MeridianMotion/Content/Models/Humanoid/Mannequin/Mannequin.fbx");
        public static string MannequinModelSource => PathOf("fd051d156126e401fae044d417c1cb53", "Assets/MeridianMotion/Content/Models/Humanoid/Mannequin/Mannequin.asset");
        public static string MannequinModelPrefab => PathOf("ef978c06d96cd4a0fa7ca8e8ab8b5c2d", "Assets/MeridianMotion/Content/Models/Humanoid/Mannequin/Mannequin Model.prefab");
        public static string QuaterniusLibrary => PathOf("81c7fe1804b784862b854602574d6a05", "Assets/MeridianMotion/Content/Animation/Quaternius");
        public static string WaterEffectShader => PathOf("d0da885bfc194845aaf393fb8fa6dba7", "Assets/MeridianMotion/Content/Materials/WaterEffect.shader");
        public static string WaterSplashPrefab => PathOf("ea3a210b33f594878b973fe72b185d5d", "Assets/MeridianMotion/Content/Prefabs/Effects/Water Splash.prefab");
        public static string WaterRipplePrefab => PathOf("5b5e8c7877acb430497ed0149d01fec7", "Assets/MeridianMotion/Content/Prefabs/Effects/Water Ripple.prefab");
        public static string WaterSplashMaterial => PathOf("cced97e60acc14ce8921c02b4861dacd", "Assets/MeridianMotion/Content/Materials/Effects/WaterSplash.mat");
        public static string WaterRippleMaterial => PathOf("1982ffa5b39ec40518067641ac271cd8", "Assets/MeridianMotion/Content/Materials/Effects/WaterRipple.mat");

        /// <summary>The path of the asset with this GUID, or expected if there's none.</summary>
        public static string PathOf(string guid, string expected)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            return string.IsNullOrEmpty(path) || AssetDatabase.GetMainAssetTypeAtPath(path) == null ? expected : path;
        }

        /// <summary>
        /// Whether the asset at path can't be saved: it's in a package installed from the registry, Git or a tarball
        /// (Unity keeps those read-only, and replaces them on an upgrade). An embedded or local package can be.
        /// </summary>
        public static bool IsReadOnly(string path)
        {
            if (string.IsNullOrEmpty(path) || !path.StartsWith("Packages/")) { return false; }
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(path);
            return package != null && package.source != PackageSource.Embedded && package.source != PackageSource.Local;
        }
    }
}
