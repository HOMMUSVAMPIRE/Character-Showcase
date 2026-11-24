using MeridianMotion.UI;
using UnityEditor;

namespace MeridianMotion.UI.EditorTools
{
    /// <summary>
    /// In the editor, the package's theme (UI Theme, UIAssets.ThemePath) is the fallback theme (UITheme.Fallback): what
    /// prefabs, the gallery and a scene without a UI Manager are drawn in, so they look as they will in the game.
    /// </summary>
    [InitializeOnLoad]
    public static class UIThemeFallback
    {
        static UIThemeFallback() => EditorApplication.delayCall += () => Set();

        /// <summary>Makes the package's theme the fallback (if it's there), and returns it (else null).</summary>
        public static UITheme Set()
        {
            var theme = AssetDatabase.LoadAssetAtPath<UITheme>(UIAssets.ThemePath);
            if (theme != null) { UITheme.Fallback = theme; }
            return theme;
        }
    }
}
