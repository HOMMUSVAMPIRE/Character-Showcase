using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using MeridianMotion.Showcase.Grass;
using MeridianMotion.Tests;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace MeridianMotion.Showcase.Tests.Grass
{
    /// <summary>
    /// A diagnostic, not a test (Explicit: run it by name, -testFilter MemoryDiagnostic, with -memoryScene [path] for
    /// another level than LevelPlayground): where a level's memory goes in play mode. Memory before the level's loaded
    /// and after a few seconds of it playing, the grass's own (its masks and blade lists on the GPU), and the loaded
    /// objects that take the most, by type and one by one, to Logs/GrassBenchmark/Memory-[time].md.
    /// </summary>
    [Category(TestArea.Levels)]
    [TestTiming]
    public class MemoryDiagnostic
    {
        private const string DefaultScene = "Assets/Showcase/Scenes/LevelPlayground/LevelPlayground.unity";
        private Scene scene;

        [UnityTearDown]
        public IEnumerator UnloadTheLevel()
        {
            if (scene.IsValid() && scene.isLoaded)
            {
                AsyncOperation unloading = SceneManager.UnloadSceneAsync(scene);
                while (unloading != null && !unloading.isDone) { yield return null; }
            }
        }

        [UnityTest, Explicit("A diagnostic: run it by name.")]
        public IEnumerator WhereTheMemoryGoes()
        {
            string[] arguments = Environment.GetCommandLineArgs();
            int filter = Array.IndexOf(arguments, "-testFilter");
            if (Application.isBatchMode && (filter < 0 || filter + 1 >= arguments.Length || !arguments[filter + 1].Contains("MemoryDiagnostic")))
            {
                Assert.Ignore("A diagnostic: run it by name (-testFilter MemoryDiagnostic).");
            }
            int given = Array.IndexOf(arguments, "-memoryScene");
            string scenePath = given >= 0 && given + 1 < arguments.Length ? arguments[given + 1] : DefaultScene;

            var report = new StringBuilder($"# Memory, {Path.GetFileNameWithoutExtension(scenePath)}, {DateTime.Now:yyyy-MM-dd HH:mm}\n\n");
            report.AppendLine($"{(Application.isEditor ? "In the editor (the totals include the editor's own)" : "In a player")}, {SystemInfo.graphicsDeviceName}.\n");
            report.AppendLine("| | Total allocated | Total reserved | Managed heap used | Graphics driver |");
            report.AppendLine("| --- | --- | --- | --- | --- |");
            report.AppendLine(Totals("Before the level"));
#if UNITY_EDITOR
            AsyncOperation loading = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(scenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            while (!loading.isDone) { yield return null; }
#else
            Assert.Ignore("Editor only.");
            yield break;
#endif
            scene = SceneManager.GetSceneByPath(scenePath);
            SceneManager.SetActiveScene(scene);
            // The game camera renders into a 1080p target (in batch mode there's no screen for it), looking about, so
            // the grass's blade lists grow as they would on a screen.
            Camera camera = Camera.main;
            RenderTexture target = null;
            if (camera != null && Application.isBatchMode)
            {
                target = new RenderTexture(1920, 1080, 24);
                camera.targetTexture = target;
            }
            float until = Time.realtimeSinceStartup + 4f;
            while (Time.realtimeSinceStartup < until)
            {
                if (camera != null) { camera.transform.Rotate(0f, 90f * Time.unscaledDeltaTime, 0f, Space.World); }
                yield return null;
            }
            report.AppendLine(Totals("After 4 s of it"));

            (long grassBytes, long capacity, long asked) = GrassField.GpuMemory();
            report.AppendLine($"\nThe grass on the GPU: {Mb(grassBytes)} ({GrassField.FieldCount} fields; blade lists for {capacity:N0} blades, " +
                $"{asked:N0} asked for in the last frame read back); the bend map: {(GrassBending.Map != null ? Mb(Profiler.GetRuntimeMemorySizeLong(GrassBending.Map)) : "none")}.\n");

            if (target != null) { camera.targetTexture = null; target.Release(); }
            Object[] all = Resources.FindObjectsOfTypeAll<Object>();
            report.AppendLine("## By type (every loaded object, the editor's included)\n\n| Type | Objects | Memory |\n| --- | --- | --- |");
            foreach (var type in all.GroupBy(o => o.GetType().Name).Select(g => (name: g.Key, count: g.Count(), bytes: g.Sum(Profiler.GetRuntimeMemorySizeLong)))
                         .OrderByDescending(t => t.bytes).Take(15))
            {
                report.AppendLine($"| {type.name} | {type.count:N0} | {Mb(type.bytes)} |");
            }
            report.AppendLine("\n## The largest objects\n\n| Object | Type | Memory |\n| --- | --- | --- |");
            foreach (Object o in all.OrderByDescending(Profiler.GetRuntimeMemorySizeLong).Take(25))
            {
                report.AppendLine($"| {(string.IsNullOrEmpty(o.name) ? "(unnamed)" : o.name)} | {o.GetType().Name} | {Mb(Profiler.GetRuntimeMemorySizeLong(o))} |");
            }

            string folder = Path.Combine("Logs", "GrassBenchmark");
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, $"Memory-{DateTime.Now:yyyyMMdd-HHmmss}.md");
            File.WriteAllText(path, report.ToString());
            Debug.Log($"[Memory] Wrote {path}:\n{report}");
        }

        private static string Totals(string when) =>
            $"| {when} | {Mb(Profiler.GetTotalAllocatedMemoryLong())} | {Mb(Profiler.GetTotalReservedMemoryLong())} | " +
            $"{Mb(Profiler.GetMonoUsedSizeLong())} | {Mb(Profiler.GetAllocatedMemoryForGraphicsDriver())} |";

        private static string Mb(long bytes) => $"{bytes / (1024.0 * 1024.0):0.0} MB";
    }
}
