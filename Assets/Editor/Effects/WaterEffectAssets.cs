using MeridianMotion.Characters;
using UnityEditor;
using UnityEngine;

namespace MeridianMotion.EditorTools
{
    /// <summary>
    /// Makes the default water effects (see WaterEffects) and gives them to the Character prefab: a splash (a burst of
    /// droplets) and a ripple (rings spreading on the surface), each a prefab of one particle system, drawn with the
    /// noise-built Water Effect shader (no textures). Each asset is made only if it's missing, and kept after, so they
    /// can be edited by hand, or replaced by others (any prefab: see WaterEffects); delete one to have it made afresh.
    /// The Character prefab gets a WaterEffects if it has none, and these effects where its slots are empty.
    /// </summary>
    public static class WaterEffectAssets
    {
        /// <summary>The shader the default effects are drawn with.</summary>
        public const string ShaderPath = "Assets/Materials/WaterEffect.shader";
        /// <summary>Where the default effects' materials are.</summary>
        public const string MaterialFolder = "Assets/Materials/Effects";
        /// <summary>Where the default effects' prefabs are.</summary>
        public const string PrefabFolder = "Assets/Prefabs/Effects";
        /// <summary>The default splash's prefab.</summary>
        public const string SplashPath = PrefabFolder + "/Water Splash.prefab";
        /// <summary>The default ripple's prefab.</summary>
        public const string RipplePath = PrefabFolder + "/Water Ripple.prefab";
        private const string CharacterPrefabPath = CharacterSceneSetup.CharacterPrefabPath;

        [MenuItem("Tools/Meridian Motion/Create Water Effects")]
        public static void CreateFromMenu() => Create();

        /// <summary>For -executeMethod: makes what's missing, then exits (1 if it couldn't).</summary>
        public static void CreateFromCommandLine() => EditorApplication.Exit(Create() ? 0 : 1);

        /// <summary>Makes the default effects that are missing and gives them to the Character prefab. Returns whether it could.</summary>
        public static bool Create()
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            if (shader == null)
            {
                Debug.LogError($"[WaterEffectAssets] There's no water effect shader at '{ShaderPath}'.");
                return false;
            }
            EnsureFolder(MaterialFolder);
            EnsureFolder(PrefabFolder);
            Material splashMaterial = MaterialAt(MaterialFolder + "/WaterSplash.mat", shader, ringWidth: 0f, softness: 0.6f, breakup: 0.3f);
            Material rippleMaterial = MaterialAt(MaterialFolder + "/WaterRipple.mat", shader, ringWidth: 0.14f, softness: 0.1f, breakup: 0.55f);
            GameObject splash = PrefabAt(SplashPath, () => MakeSplash(splashMaterial));
            GameObject ripple = PrefabAt(RipplePath, () => MakeRipple(rippleMaterial));
            GiveToCharacter(splash, ripple);
            AssetDatabase.SaveAssets();
            Debug.Log($"[WaterEffectAssets] Water effects ready: '{SplashPath}', '{RipplePath}', on '{CharacterPrefabPath}'.");
            return true;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) { return; }
            int slash = path.LastIndexOf('/');
            AssetDatabase.CreateFolder(path[..slash], path[(slash + 1)..]);
        }

        private static Material MaterialAt(string path, Shader shader, float ringWidth, float softness, float breakup)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) { return material; }
            material = new Material(shader) { name = System.IO.Path.GetFileNameWithoutExtension(path) };
            material.SetFloat("_RingWidth", ringWidth);
            material.SetFloat("_Softness", softness);
            material.SetFloat("_Breakup", breakup);
            material.enableInstancing = true;
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static GameObject PrefabAt(string path, System.Func<GameObject> make)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null) { return prefab; }
            GameObject made = make();
            prefab = PrefabUtility.SaveAsPrefabAsset(made, path);
            Object.DestroyImmediate(made);
            return prefab;
        }

        /// <summary>A particle system that plays once when asked (not on awake), in world space, scaling with its transform.</summary>
        private static ParticleSystem NewSystem(string name, Material material, out ParticleSystemRenderer renderer)
        {
            var go = new GameObject(name);
            var system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = system.main;
            main.playOnAwake = false;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = 0f;
            renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return system;
        }

        /// <summary>A splash: a burst of droplets thrown up and out, falling back and fading.</summary>
        private static GameObject MakeSplash(Material material)
        {
            ParticleSystem system = NewSystem("Water Splash", material, out ParticleSystemRenderer renderer);
            ParticleSystem.MainModule main = system.main;
            main.duration = 1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 5.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.22f);
            main.gravityModifier = 1f;
            main.maxParticles = 64;
            main.startColor = new Color(0.92f, 0.96f, 1f, 0.85f);
            ParticleSystem.EmissionModule emission = system.emission;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 28) });
            // Up and out from a ring round where the character went in.
            ParticleSystem.ShapeModule shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 25f;
            shape.radius = 0.3f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            ParticleSystem.ColorOverLifetimeModule colour = system.colorOverLifetime;
            colour.enabled = true;
            colour.color = Fade(1f);
            ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.5f));
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            return system.gameObject;
        }

        /// <summary>A ripple: two rings on the surface, one after the other, spreading out and fading.</summary>
        private static GameObject MakeRipple(Material material)
        {
            ParticleSystem system = NewSystem("Water Ripple", material, out ParticleSystemRenderer renderer);
            ParticleSystem.MainModule main = system.main;
            main.duration = 1f;
            main.startLifetime = 1.4f;
            main.startSpeed = 0f;
            main.startSize = 1.8f;
            main.gravityModifier = 0f;
            main.maxParticles = 4;
            main.startColor = new Color(0.92f, 0.96f, 1f, 0.6f);
            ParticleSystem.EmissionModule emission = system.emission;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1), new ParticleSystem.Burst(0.25f, 1) });
            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = false;
            ParticleSystem.ColorOverLifetimeModule colour = system.colorOverLifetime;
            colour.enabled = true;
            colour.color = Fade(0.8f);
            ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.2f, 0f, 2f), new Keyframe(1f, 1f, 0.3f, 0f)));
            // Lying flat on the water.
            renderer.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            return system.gameObject;
        }

        /// <summary>White, from startAlpha at the start of the particle's life fading out to nothing at its end.</summary>
        private static ParticleSystem.MinMaxGradient Fade(float startAlpha)
        {
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(startAlpha, 0f), new GradientAlphaKey(0f, 1f) });
            return new ParticleSystem.MinMaxGradient(gradient);
        }

        /// <summary>Gives the Character prefab a WaterEffects (if it has none) with these effects in its empty slots.</summary>
        private static void GiveToCharacter(GameObject splash, GameObject ripple)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(CharacterPrefabPath);
            try
            {
                WaterEffects effects = root.GetComponent<WaterEffects>();
                if (effects == null) { effects = root.AddComponent<WaterEffects>(); }
                if (effects.SplashPrefab == null) { effects.SplashPrefab = splash; }
                if (effects.RipplePrefab == null) { effects.RipplePrefab = ripple; }
                PrefabUtility.SaveAsPrefabAsset(root, CharacterPrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
