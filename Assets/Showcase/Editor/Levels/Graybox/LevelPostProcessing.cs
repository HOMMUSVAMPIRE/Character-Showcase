using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MeridianMotion.Showcase.Levels
{
    /// <summary>
    /// A level's post-processing profile, as its build makes it: a light grade for a bright day (ACES tonemapping, a little
    /// bloom on the sky and sunlit faces, a touch more contrast). Made again on every build, unless the level keeps its
    /// own (a spec's "keepPostProcessing": LevelDefinition.KeepPostProcessing): then one that's there is left as it was
    /// tuned, and it's made only where there's none.
    /// </summary>
    public static class LevelPostProcessing
    {
        /// <summary>The profile at a path: the one there, if it's kept and there is one; else made (replacing any).</summary>
        public static VolumeProfile Profile(string path, bool keepExisting)
        {
            if (keepExisting)
            {
                var existing = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
                if (existing != null) { return existing; }
            }
            AssetDatabase.DeleteAsset(path);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);

            var tonemapping = Add<Tonemapping>(profile);
            tonemapping.mode.Override(TonemappingMode.ACES);

            var bloom = Add<Bloom>(profile);
            bloom.threshold.Override(1.1f);
            bloom.intensity.Override(0.25f);
            bloom.scatter.Override(0.6f);

            var color = Add<ColorAdjustments>(profile);
            color.postExposure.Override(0.1f);
            color.contrast.Override(8f);
            color.saturation.Override(6f);

            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static T Add<T>(VolumeProfile profile) where T : VolumeComponent
        {
            T component = profile.Add<T>();
            component.name = typeof(T).Name;
            component.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(component, profile);
            return component;
        }
    }
}
