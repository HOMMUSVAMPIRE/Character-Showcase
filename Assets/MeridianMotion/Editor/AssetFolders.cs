using UnityEditor;

namespace MeridianMotion.EditorTools
{
    /// <summary>Making folders in the project for the tools' output.</summary>
    public static class AssetFolders
    {
        /// <summary>
        /// Makes the folder at path (Assets/MyGame/Models/Generic/Wolf, say) with every missing folder above it, so a tool works in
        /// a fresh project, where even Assets/Models may not be there. Returns path.
        /// </summary>
        public static string Ensure(string path)
        {
            path = path.TrimEnd('/');
            if (AssetDatabase.IsValidFolder(path)) { return path; }
            int slash = path.LastIndexOf('/');
            if (slash <= 0) { return path; }
            string parent = Ensure(path[..slash]);
            AssetDatabase.CreateFolder(parent, path[(slash + 1)..]);
            return path;
        }
    }
}
