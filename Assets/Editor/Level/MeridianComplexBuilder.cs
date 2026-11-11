using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace CharacterShowcase.EditorTools.Level
{
    /// <summary>
    /// Builds the Meridian Complex's graybox as one scene, under a "Meridian Complex" root, all in one placeholder
    /// material (smooth, matte cream concrete): its seven areas, each by its own builder (01 Concourse, 02 Kinetic
    /// Gardens, 03 Ascension Spire, 04 Fabrication Hall, 05 Suspension Yard, 06 Service Network, 07 Calibration
    /// Annex), joined at their connections, on a placeholder ground; and a sunny-midday environment for URP under
    /// "Environment": a sun, a procedural sky (which lights the scene's ambient and reflections), light haze, and a
    /// global post-processing volume.
    ///
    /// There's no camera, character or other game setup: the post-processing shows in the Scene view (with its
    /// effects on) and in any camera that has post-processing on. Rebuilding replaces the scene and its environment
    /// assets, so edits made to them by hand are lost: once it's being worked on by hand, stop rebuilding it.
    /// </summary>
    public static class MeridianComplexBuilder
    {
        public const string ScenePath = "Assets/Scenes/MeridianComplex/MeridianComplex.unity";
        private const string EnvironmentFolder = "Assets/Scenes/MeridianComplex/Environment";
        private const string SkyPath = EnvironmentFolder + "/Sky_SunnyMidday.mat";
        private const string ProfilePath = EnvironmentFolder + "/MeridianComplex_PostProcessing.asset";
        private const string LightingPath = EnvironmentFolder + "/MeridianComplex_Lighting.lighting";
        private const string MaterialFolder = "Assets/Scenes/MeridianComplex/Materials";
        public const string LevelMaterialPath = MaterialFolder + "/Placeholder_SmoothConcrete.mat";

        // ---- The level's placeholder material (tunable; only used when the material's first made) ----

        // Smooth, matte cream concrete: a warm off-white, mostly rough (a broad, soft sheen, no sharp highlights).
        private static readonly Color ConcreteColor = new(0.78f, 0.72f, 0.6f);
        private const float ConcreteSmoothness = 0.2f;

        private const float GroundHalf = 220f;

        // ---- The environment (tunable) ----

        // Midday: the sun high (65° up) in the south-south-east, so terraces and stairs cast short, readable shadows.
        private static readonly Vector3 SunRotation = new(65f, -30f, 0f);
        private static readonly Color SunColor = new(1f, 0.96f, 0.9f);
        private const float SunIntensity = 2f;
        private static readonly Color HazeColor = new(0.74f, 0.82f, 0.9f);
        private const float HazeDensity = 0.0015f;  // Exponential: about 14% haze at 100 m, 45% at 400 m.

        [MenuItem("Tools/Character Showcase/Level/Build Meridian Complex")]
        public static void BuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) { return; }
            Build();
            EditorSceneManager.OpenScene(ScenePath);
        }

        /// <summary>For -executeMethod.</summary>
        public static void BuildFromCommandLine() => Build();

        public static void Build()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Directory.CreateDirectory(EnvironmentFolder);
            Directory.CreateDirectory(MaterialFolder);
            AssetDatabase.Refresh();
            Material concrete = LevelMaterial();

            var complex = new GameObject("Meridian Complex").transform;
            MeridianConcourseBuilder.BuildInto(complex, concrete);
            MeridianKineticGardensBuilder.BuildInto(complex, concrete);
            MeridianAscensionSpireBuilder.BuildInto(complex, concrete);
            MeridianFabricationHallBuilder.BuildInto(complex, concrete);
            MeridianSuspensionYardBuilder.BuildInto(complex, concrete);
            MeridianServiceNetworkBuilder.BuildInto(complex, concrete);
            MeridianCalibrationAnnexBuilder.BuildInto(complex, concrete);
            BuildGround(complex, concrete);
            BuildEnvironment(Graybox.Group(complex, "Environment"));

            EditorSceneManager.SaveScene(scene, ScenePath);
            BakeEnvironmentLighting(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[MeridianComplexBuilder] Built the Meridian Complex graybox in '{ScenePath}'.");
        }

        /// <summary>
        /// The placeholder material for all the level geometry: URP Lit, smooth matte cream concrete, untextured. Made the
        /// first time the level's built, then kept, so changes made to it by hand survive rebuilding.
        /// </summary>
        private static Material LevelMaterial()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(LevelMaterialPath);
            if (existing != null) { return existing; }
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Placeholder_SmoothConcrete" };
            material.SetColor("_BaseColor", ConcreteColor);
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Smoothness", ConcreteSmoothness);
            AssetDatabase.CreateAsset(material, LevelMaterialPath);
            return material;
        }

        /// <summary>
        /// A placeholder ground at ±0 under the whole complex (a little over its 400 × 400 m, for the districts that
        /// reach past it), open where the Service Network and the Fabrication Hall's basements go below it.
        /// </summary>
        private static void BuildGround(Transform complex, Material material)
        {
            Graybox.Begin(material);
            var openings = new List<UnityEngine.Rect>(MeridianServiceNetworkBuilder.GroundOpenings);
            openings.AddRange(MeridianFabricationHallBuilder.GroundOpenings);
            Graybox.SlabWithHoles(Graybox.Group(complex, "Ground (placeholder)"), "Ground",
                UnityEngine.Rect.MinMaxRect(-GroundHalf, -GroundHalf, GroundHalf, GroundHalf), openings, -1f, 0f);
        }

        // ---- The environment ----

        private static void BuildEnvironment(Transform environment)
        {
            var sunObject = new GameObject("Sun");
            sunObject.transform.SetParent(environment, false);
            sunObject.transform.localRotation = Quaternion.Euler(SunRotation);
            var sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = SunColor;
            sun.intensity = SunIntensity;
            sun.shadows = LightShadows.Soft;
            sun.lightmapBakeType = LightmapBakeType.Realtime;
            sunObject.AddComponent<UniversalAdditionalLightData>();

            Material sky = Replace(SkyPath, new Material(Shader.Find("Skybox/Procedural")));
            sky.SetFloat("_SunDisk", 2f);              // High quality.
            sky.SetFloat("_SunSize", 0.04f);
            sky.SetFloat("_SunSizeConvergence", 5f);
            sky.SetFloat("_AtmosphereThickness", 0.9f); // A little thinner than the default: a clearer, bluer sky.
            sky.SetColor("_SkyTint", new Color(0.5f, 0.55f, 0.62f));
            sky.SetColor("_GroundColor", new Color(0.6f, 0.6f, 0.58f)); // Neutral, for beyond the ground placeholder.
            sky.SetFloat("_Exposure", 1.25f);
            EditorUtility.SetDirty(sky);

            RenderSettings.skybox = sky;
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            RenderSettings.reflectionIntensity = 1f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = HazeColor;
            RenderSettings.fogDensity = HazeDensity;

            var volumeObject = new GameObject("Post-Processing (Global Volume)");
            volumeObject.transform.SetParent(environment, false);
            var volume = volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = BuildPostProcessingProfile();
        }

        /// <summary>A light grade for a bright day: ACES tonemapping, a little bloom on the sky and sunlit faces, a touch more contrast.</summary>
        private static VolumeProfile BuildPostProcessingProfile()
        {
            VolumeProfile profile = Replace(ProfilePath, ScriptableObject.CreateInstance<VolumeProfile>());

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

        /// <summary>
        /// Lighting settings with no baked or realtime GI (the graybox has no lightmaps), then a bake: with nothing to
        /// lightmap, it only renders the sky into the scene's ambient lighting and default reflections.
        /// </summary>
        private static void BakeEnvironmentLighting(Scene scene)
        {
            var settings = Replace(LightingPath, new LightingSettings { name = "MeridianComplex_Lighting" });
            settings.bakedGI = false;
            settings.realtimeGI = false;
            settings.realtimeEnvironmentLighting = true;
            EditorUtility.SetDirty(settings);
            Lightmapping.SetLightingSettingsForScene(scene, settings);
            if (!Lightmapping.Bake())
            {
                Debug.LogWarning("[MeridianComplexBuilder] The environment lighting didn't bake: use Window > Rendering > Lighting > Generate Lighting.");
            }
        }

        /// <summary>Saves asset at path, replacing what was there.</summary>
        private static T Replace<T>(string path, T asset) where T : Object
        {
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }
    }
}
