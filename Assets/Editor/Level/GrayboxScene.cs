using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CharacterShowcase.EditorTools.Level
{
    /// <summary>
    /// A generated graybox scene and how it's built: its parts (each built by a builder, under a root named for the
    /// scene, in the levels' palette of materials: see Palette), then a sunny-midday environment for URP under
    /// "Environment": a sun, a procedural sky (which lights the scene's ambient and reflections), light haze, and a
    /// global post-processing volume. The Meridian Complex (MeridianComplexBuilder) and the Level Playground
    /// (LevelPlaygroundBuilder) are both built this way, so they look and light the same.
    ///
    /// There's no camera, character or other game setup: the post-processing shows in the Scene view (with its
    /// effects on) and in any camera that has post-processing on. Rebuilding replaces the scene and its environment
    /// assets (in an Environment folder beside it), so edits made to them by hand are lost.
    /// </summary>
    internal sealed class GrayboxScene
    {
        /// <summary>
        /// Where the levels' materials are, one for each Graybox.Surface ("Level_[Surface].mat"), shared by every graybox
        /// scene, so tuning one tunes them all.
        /// </summary>
        public const string MaterialFolder = "Assets/Materials/Level";
        /// <summary>The shader they're made with: noise-built concrete, world-space, no textures (tint, panels, metal and all).</summary>
        public const string SurfaceShaderPath = "Assets/Materials/NoiseConcrete.shadergraph";
        // The climbable mask every unpainted surface shares: 1 pixel of black, nothing climbable (see the shader's
        // Climbable settings, and PaintedClimbableSurface, which gives painted objects masks of their own).
        public const string NoClimbableMaskPath = "Assets/Materials/ClimbableMask_None.png";

        // ---- The palette (tunable; only used when a material's first made, so they can be edited by hand) ----

        /// <summary>How a surface looks: the NoiseConcrete shader's settings for it.</summary>
        private readonly struct Look
        {
            public readonly Color tint, emission;
            public readonly float metallic, smoothness, bump, grain, pitting, stains, panels, panelSize;

            public Look(Color tint, float metallic, float smoothness, float bump, float grain, float pitting, float stains,
                float panels = 0f, float panelSize = 2f, Color emission = default)
            {
                this.tint = tint; this.metallic = metallic; this.smoothness = smoothness; this.bump = bump; this.grain = grain;
                this.pitting = pitting; this.stains = stains; this.panels = panels; this.panelSize = panelSize; this.emission = emission;
            }
        }

        // The Meridian Complex style sheet's palette: concrete (#D6D9DD), graphite (#2D3138), steel grey (#7E8793), and
        // orange (#E07A2F) as the accent. The concretes are darker than the sheet's swatch, which washes out to white in
        // the midday sun; the ground darker still, so what stands on it reads. Metals have no pits and only fine grain,
        // like paint over steel.
        private static readonly Look[] Looks =
        {
            /* Concrete */ new(new Color(0.7f, 0.71f, 0.73f), 0f, 0.15f, 0.012f, 0.35f, 0.6f, 0.12f),
            /* Panel    */ new(new Color(0.76f, 0.77f, 0.79f), 0f, 0.18f, 0.01f, 0.3f, 0.45f, 0.1f, panels: 1f, panelSize: 2f),
            /* Ground   */ new(new Color(0.5f, 0.52f, 0.54f), 0f, 0.12f, 0.012f, 0.4f, 0.5f, 0.15f, panels: 1f, panelSize: 4f),
            /* Graphite */ new(new Color(0.176f, 0.192f, 0.22f), 0.6f, 0.45f, 0.005f, 0.12f, 0f, 0.06f),
            /* Steel    */ new(new Color(0.494f, 0.529f, 0.576f), 0.75f, 0.5f, 0.006f, 0.15f, 0.05f, 0.08f),
            /* Accent   */ new(new Color(0.878f, 0.478f, 0.184f), 0.1f, 0.4f, 0.004f, 0.1f, 0f, 0.05f),
            // The glowing surfaces: bright enough (above the bloom's threshold) to glow in daylight, as fixtures do. The cyan
            // is kept low enough that tonemapping doesn't wash it out to white.
            /* Light    */ new(new Color(1f, 0.95f, 0.88f), 0f, 0.6f, 0f, 0f, 0f, 0f, emission: new Color(1f, 0.93f, 0.82f) * 3f),
            /* Cyan     */ new(new Color(0.08f, 0.3f, 0.34f), 0f, 0.6f, 0f, 0f, 0f, 0f, emission: new Color(0.2f, 0.75f, 0.84f)),
            /* White    */ new(new Color(0.86f, 0.87f, 0.88f), 0f, 0.3f, 0.003f, 0.1f, 0f, 0.05f),
            // Foliage and soil get coarse grain and strong stains: leafy and earthy mottling, from the same noise.
            /* Foliage  */ new(new Color(0.26f, 0.42f, 0.17f), 0f, 0.12f, 0.02f, 0.9f, 0f, 0.35f),
            /* Soil     */ new(new Color(0.27f, 0.2f, 0.14f), 0f, 0.08f, 0.02f, 0.7f, 0.2f, 0.25f),
            // Grass: a fresher green than the trees' leaves, with strong stains for patchy, mown-lawn mottling.
            /* Grass    */ new(new Color(0.3f, 0.46f, 0.19f), 0f, 0.1f, 0.018f, 0.85f, 0f, 0.3f),
        };


        /// <summary>What it's called: its root object's name, and how the build refers to it.</summary>
        public readonly string Name;
        public readonly string ScenePath;
        private readonly string skyPath, profilePath, lightingPath, lightingName;
        private readonly (string name, Action<Transform, Material> build)[] parts;

        /// <summary>
        /// A scene at scenePath built from parts, in order: each a name for showing progress ("Building [name]") and a
        /// builder that builds it under the scene's root in the material it's given. Its environment assets are kept
        /// in an Environment folder beside the scene, named after it.
        /// </summary>
        public GrayboxScene(string name, string scenePath, params (string name, Action<Transform, Material> build)[] parts)
        {
            Name = name;
            ScenePath = scenePath;
            this.parts = parts;
            string folder = Path.GetDirectoryName(scenePath).Replace('\\', '/') + "/Environment";
            lightingName = Path.GetFileNameWithoutExtension(scenePath) + "_Lighting";
            skyPath = folder + "/Sky_SunnyMidday.mat";
            profilePath = folder + "/" + Path.GetFileNameWithoutExtension(scenePath) + "_PostProcessing.asset";
            lightingPath = folder + "/" + lightingName + ".lighting";
        }

        private string EnvironmentFolder => Path.GetDirectoryName(skyPath);

        /// <summary>Its parts, in order: what a level spec records (see LevelSpecTools).</summary>
        internal IReadOnlyList<(string name, Action<Transform, Material> build)> Parts => parts;

        /// <summary>Builds the scene and bakes its lighting in one go, which holds up the editor until it's done.</summary>
        public void Build()
        {
            var build = new BuildState();
            foreach (BuildStep step in Steps())
            {
                var timer = System.Diagnostics.Stopwatch.StartNew();
                step.run(build);
                Log($"{step.name}: {timer.Elapsed.TotalSeconds:0.0} s.");
            }
            var bakeTimer = System.Diagnostics.Stopwatch.StartNew();
            if (!Lightmapping.Bake()) { WarnLightingNotBaked(); }
            Log($"{BakeStepName}: {bakeTimer.Elapsed.TotalSeconds:0.0} s.");
            BakeOcclusion();
            Finish(build);
        }

        // ---- The build, in steps ----

        /// <summary>What the build's steps share as they go.</summary>
        internal sealed class BuildState
        {
            public Scene scene;
            public Material concrete;
            public Material[] palette;
            public Transform root;
        }

        /// <summary>One step of the build: what it's called (for showing progress) and what it does.</summary>
        internal readonly struct BuildStep
        {
            public readonly string name;
            public readonly Action<BuildState> run;
            /// <summary>
            /// Whether the scene can still be left as it was if the build stops after this step: not once the
            /// environment's assets (which the old scene references) have been replaced.
            /// </summary>
            public readonly bool cancellable;

            public BuildStep(string name, Action<BuildState> run, bool cancellable = true)
            {
                this.name = name;
                this.run = run;
                this.cancellable = cancellable;
            }
        }

        internal const string BakeStepName = "Baking the environment lighting";
        internal const string OcclusionStepName = "Baking the occlusion culling";

        /// <summary>
        /// Sets the occlusion bake's parameters from the settings (Culling), and says whether to bake it at all.
        /// The bake divides the level into cells and works out, for each, which solid pieces (occluders: see
        /// Graybox.MakeStatic) hide which others, so cameras skip what's hidden, not only what's out of view.
        /// </summary>
        private static bool PrepareOcclusion()
        {
            GrayboxSettings.CullingSettings culling = GrayboxSettings.Load().culling;
            if (!culling.bakeOcclusion) { return false; }
            StaticOcclusionCulling.smallestOccluder = culling.smallestOccluder;
            StaticOcclusionCulling.smallestHole = culling.smallestHole;
            StaticOcclusionCulling.backfaceThreshold = culling.backfaceThreshold;
            return true;
        }

        /// <summary>Bakes the open scene's occlusion culling in one go (if the settings say to), after its lighting: saved with it by Finish.</summary>
        internal void BakeOcclusion()
        {
            if (!PrepareOcclusion()) { return; }
            var timer = System.Diagnostics.Stopwatch.StartNew();
            if (!StaticOcclusionCulling.Compute()) { WarnOcclusionNotBaked(); }
            Log($"{OcclusionStepName}: {timer.Elapsed.TotalSeconds:0.0} s.");
        }

        /// <summary>Starts baking the occlusion culling in the background (if the settings say to): false if it isn't, or couldn't start.</summary>
        internal bool StartOcclusionBake() => PrepareOcclusion() && StaticOcclusionCulling.GenerateInBackground();

        internal void WarnOcclusionNotBaked() =>
            Debug.LogWarning($"[{Name} build] The occlusion culling didn't bake: use Window > Rendering > Occlusion Culling > Bake.");

        /// <summary>
        /// The build up to the lighting bake, which comes after them (Lightmapping.Bake or BakeAsync), followed by
        /// Finish. Run in order, each in one go. Building into a new scene, so the scene on disk is only replaced when
        /// the new one is saved.
        /// </summary>
        internal List<BuildStep> Steps()
        {
            var steps = new List<BuildStep>
            {
                new("Starting a new scene", build =>
                {
                    build.scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                    Directory.CreateDirectory(EnvironmentFolder);
                    Directory.CreateDirectory(MaterialFolder);
                    AssetDatabase.Refresh();
                    build.palette = Palette();
                    build.concrete = build.palette[(int)Graybox.Surface.Concrete];
                    build.root = new GameObject(Name).transform;
                    Graybox.ClearDeferred();
                }),
            };
            foreach ((string name, Action<Transform, Material> buildPart) in parts)
            {
                steps.Add(new BuildStep($"Building {name}", build =>
                {
                    Graybox.UsePalette(build.palette);
                    buildPart(build.root, build.concrete);
                }));
            }
            // What had to wait for the whole level (pads and marks on the floor under them, trees where there's room).
            steps.Add(new BuildStep("Placing the finishing touches", build =>
            {
                Graybox.UsePalette(build.palette);
                Graybox.Begin(build.concrete);
                Graybox.RunDeferred();
            }));
            steps.Add(new BuildStep("Setting up the environment", build => BuildEnvironment(Graybox.Group(build.root, "Environment")), cancellable: false));
            steps.Add(new BuildStep("Saving the scene", build =>
            {
                EditorSceneManager.SaveScene(build.scene, ScenePath);
                PrepareLighting(build.scene);
            }, cancellable: false));
            return steps;
        }

        /// <summary>After the bake (or without it, if it was cancelled): saves the scene with its lighting, and the assets.</summary>
        internal void Finish(BuildState build)
        {
            EditorSceneManager.SaveScene(build.scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Log($"Built the {Name} graybox in '{ScenePath}'.");
        }

        internal void Log(string message) => Debug.Log($"[{Name} build] {message}");

        internal void WarnLightingNotBaked() =>
            Debug.LogWarning($"[{Name} build] The environment lighting didn't bake: use Window > Rendering > Lighting > Generate Lighting.");

        /// <summary>
        /// The levels' materials, one for each Graybox.Surface, indexed by it: each made the first time a level's built
        /// (from its Look, in the NoiseConcrete shader), then kept, so changes made to them by hand survive rebuilding.
        /// </summary>
        internal static Material[] Palette()
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(SurfaceShaderPath);
            var surfaces = (Graybox.Surface[])Enum.GetValues(typeof(Graybox.Surface));
            var palette = new Material[surfaces.Length];
            foreach (Graybox.Surface surface in surfaces)
            {
                string path = $"{MaterialFolder}/Level_{surface}.mat";
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    material = MakeMaterial(shader, Looks[(int)surface]);
                    material.name = $"Level_{surface}";
                    AssetDatabase.CreateAsset(material, path);
                }
                palette[(int)surface] = material;
            }
            return palette;
        }

        private static Material MakeMaterial(Shader shader, Look look)
        {
            if (shader == null)
            {
                // Without the shader graph, plain URP Lit in the look's colour.
                Debug.LogWarning($"[Graybox] No surface shader at '{SurfaceShaderPath}': the level's materials are plain URP Lit.");
                var lit = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                lit.SetColor("_BaseColor", look.tint);
                lit.SetFloat("_Metallic", look.metallic);
                lit.SetFloat("_Smoothness", look.smoothness);
                return lit;
            }
            var material = new Material(shader);
            material.SetColor("_Tint", look.tint);
            material.SetFloat("_Metallic", look.metallic);
            material.SetFloat("_Smoothness", look.smoothness);
            material.SetFloat("_BumpStrength", look.bump);
            material.SetFloat("_Grain", look.grain);
            material.SetFloat("_Pitting", look.pitting);
            material.SetFloat("_StainStrength", look.stains);
            material.SetFloat("_Panels", look.panels);
            material.SetFloat("_PanelSize", look.panelSize);
            material.SetColor("_Emission", look.emission);
            if (material.HasProperty("_ClimbableMask")) { material.SetTexture("_ClimbableMask", AssetDatabase.LoadAssetAtPath<Texture>(NoClimbableMaskPath)); }
            if (look.emission.maxColorComponent > 0f) { material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive; }
            return material;
        }

        // ---- The environment ----

        private void BuildEnvironment(Transform environment)
        {
            var sunObject = new GameObject("Sun");
            sunObject.transform.SetParent(environment, false);
            GrayboxSettings.EnvironmentSettings settings = GrayboxSettings.Load().environment;
            sunObject.transform.localRotation = Quaternion.Euler(settings.sunRotation);
            var sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = settings.sunColour;
            sun.intensity = settings.sunIntensity;
            sun.shadows = LightShadows.Soft;
            sun.lightmapBakeType = LightmapBakeType.Realtime;
            sunObject.AddComponent<UniversalAdditionalLightData>();

            Material sky = Replace(skyPath, new Material(Shader.Find("Skybox/Procedural")));
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
            RenderSettings.fogColor = settings.hazeColour;
            RenderSettings.fogDensity = settings.hazeDensity;

            var volumeObject = new GameObject("Post-Processing (Global Volume)");
            volumeObject.transform.SetParent(environment, false);
            var volume = volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = BuildPostProcessingProfile();
        }

        /// <summary>A light grade for a bright day: ACES tonemapping, a little bloom on the sky and sunlit faces, a touch more contrast.</summary>
        private VolumeProfile BuildPostProcessingProfile()
        {
            VolumeProfile profile = Replace(profilePath, ScriptableObject.CreateInstance<VolumeProfile>());

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
        /// Lighting settings with no baked or realtime GI (the graybox has no lightmaps), ready for the bake: with nothing
        /// to lightmap, it only renders the sky into the scene's ambient lighting and default reflections (and any
        /// reflection probes the scene has).
        /// </summary>
        private void PrepareLighting(Scene scene)
        {
            var settings = Replace(lightingPath, new LightingSettings { name = lightingName });
            settings.bakedGI = false;
            settings.realtimeGI = false;
            settings.realtimeEnvironmentLighting = true;
            EditorUtility.SetDirty(settings);
            Lightmapping.SetLightingSettingsForScene(scene, settings);
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
