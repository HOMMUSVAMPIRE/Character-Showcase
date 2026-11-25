using System;
using System.Collections;
using System.IO;
using System.Linq;
using MeridianMotion.Characters;
using MeridianMotion.Showcase.Grass;
using MeridianMotion.Tests;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace MeridianMotion.Showcase.Tests.Grass
{
    /// <summary>
    /// Pictures, not a test (Explicit: run it by name, -testFilter GrassMotionPictures): the grass moving, to look at.
    /// It plays MeridianComplex in the Wild Grass style, stands the game camera above its largest lawn, looking down, has the
    /// player's character stand in front of it for a second and then walk off (its legs pushing the grass), and saves a picture every quarter second for a few seconds (the wind's gusts and the bender's wake), to
    /// Logs/GrassBenchmark/Motion-[time].
    /// </summary>
    [Category(TestArea.Levels)]
    [TestTiming]
    public class GrassMotionPictures
    {
        private Scene scene;

        /// <summary>Walks the character toward +x (the move input in the world, its look's yaw being 0) while walking's set.</summary>
        private sealed class Walk : CharacterInputSource
        {
            public bool walking;

            public override CharacterInputFrame FrameFor(uint tick, MMotionMotor character) =>
                CharacterInputFrame.Holding(MovementSimulation.Move(walking ? Vector3.right : Vector3.zero), tick);
        }

        /// <summary>Unloads the level it played, so nothing run after it finds it there.</summary>
        [UnityTearDown]
        public IEnumerator UnloadTheLevel()
        {
            if (scene.IsValid() && scene.isLoaded)
            {
                AsyncOperation unloading = SceneManager.UnloadSceneAsync(scene);
                while (unloading != null && !unloading.isDone) { yield return null; }
            }
        }

        private const string ScenePath = "Assets/Showcase/Scenes/MeridianComplex/MeridianComplex.unity";

        [UnityTest, Explicit("Pictures to look at: run it by name.")]
        public IEnumerator Pictures()
        {
#if UNITY_EDITOR
            // Explicit isn't kept to by a category filter (-testCategory, run_tests.py --area): in batch mode, unless the
            // filter names this, it's skipped, so it never runs with a suite (it plays a whole level).
            string[] arguments = Environment.GetCommandLineArgs();
            int filter = Array.IndexOf(arguments, "-testFilter");
            if (Application.isBatchMode && (filter < 0 || filter + 1 >= arguments.Length || !arguments[filter + 1].Contains("GrassMotionPictures")))
            {
                Assert.Ignore("A diagnostic: run it by name (-testFilter GrassMotionPictures).");
            }
            AsyncOperation loading = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            while (!loading.isDone) { yield return null; }
            scene = SceneManager.GetSceneByPath(ScenePath);
            SceneManager.SetActiveScene(scene);
#else
            Assert.Ignore("Editor only.");
            yield break;
#endif
            for (int i = 0; i < 60; i++) { yield return null; }
            Camera camera = Camera.main;
            Assert.That(camera, Is.Not.Null);
            foreach (MonoBehaviour behaviour in camera.transform.root.GetComponentsInChildren<MonoBehaviour>())
            {
                if (!behaviour.GetType().Namespace?.StartsWith("UnityEngine") ?? true) { behaviour.enabled = false; }
            }
            var target = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            camera.targetTexture = target;

            // The largest lawn, and a spot on it: a camera low over it, a bender walking across 3 m in front.
            Bounds lawn = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None).Where(r => r.name.StartsWith("Lawn ("))
                .OrderByDescending(r => r.bounds.size.x * r.bounds.size.z).First().bounds;
            Vector3 at = new(lawn.center.x, lawn.min.y, lawn.center.z);
            camera.transform.SetPositionAndRotation(at + new Vector3(0f, 2.4f, -2f), Quaternion.Euler(45f, 0f, 0f));
            // The player's character walks across in front, driven from here (its legs push the grass).
            CharacterSpawner spawner = Object.FindAnyObjectByType<CharacterSpawner>();
            Assert.That(spawner != null && spawner.PlayerCharacter != null, "The scene should spawn the player (Set Up Scene).");
            var player = spawner.PlayerCharacter.GetComponent<MMotionCharacter>();
            var walk = player.gameObject.AddComponent<Walk>();
            player.InputSource = walk;
            player.GetComponent<MMotionMotor>().Teleport(at + new Vector3(-2.5f, 0f, 0.5f), Quaternion.LookRotation(Vector3.right));
#if UNITY_EDITOR
            // In the Wild Grass style: long grass shows the wind and the wake best.
            var wild = UnityEditor.AssetDatabase.LoadAssetAtPath<GrassStyle>("Assets/Showcase/Grass/Styles/Wild Grass.asset");
            if (wild != null) { foreach (GrassField field in Object.FindObjectsByType<GrassField>(FindObjectsSortMode.None)) { field.Style = wild; } }
#endif
            // -stillAir: no wind, so only the wake moves the grass.
            if (Environment.GetCommandLineArgs().Contains("-stillAir") && GrassWind.Active != null) { GrassWind.Active.strength = 0f; GrassWind.Active.flutter = 0f; GrassWind.Active.gustStrength = 0f; }

            string folder = Path.Combine("Logs", "GrassBenchmark", "Motion-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"));
            Directory.CreateDirectory(folder);
            float start = Time.time, next = start;
            int picture = 0;
            while (Time.time - start < 4f)
            {
                float t = Time.time - start;
                // Standing a second, then walking off to the right.
                walk.walking = t > 1f;
                yield return null;
                if (Time.time < next) { continue; }
                next += 0.25f;
                AsyncGPUReadbackRequest request = AsyncGPUReadback.Request(target, 0, TextureFormat.RGBA32);
                request.WaitForCompletion();
                var image = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
                image.SetPixelData(request.GetData<byte>(), 0);
                File.WriteAllBytes(Path.Combine(folder, $"{picture++:00} at {t:0.0} s.png"), image.EncodeToPNG());
                Object.Destroy(image);
            }
            player.InputSource = null;
            Object.Destroy(walk);
            camera.targetTexture = null;
            target.Release();
            Debug.Log($"[Grass motion] Wrote {folder}.");
        }
    }
}
