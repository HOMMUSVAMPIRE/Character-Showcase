using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine.Profiling;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;

namespace MeridianMotion.Showcase.Grass.EditorTools
{
    /// <summary>
    /// Measures what the levels' grass costs (ReferenceAssets/OpenTasks.md, O1 and O9). It opens each level with lawns
    /// and renders fixed camera spots over its largest lawns, once with the grass and once without, and writes a report,
    /// and a picture of each spot with the grass, to Logs/GrassBenchmark/[time]. Its spots come from the lawns' own meshes ("Lawn (+…)" under each "Lawns" group), which
    /// the old grass and the new both have, so runs before and after a change are comparable. "The grass" is whatever
    /// draws blades: the old baked "Grass Tufts" objects, and any behaviour named GrassField (found by name, so this
    /// tool needs neither).
    /// </summary>
    public static class GrassBenchmark
    {
        /// <summary>The levels with lawns.</summary>
        public static readonly string[] Scenes =
        {
            "Assets/Showcase/Scenes/MeridianComplex/MeridianComplex.unity",
            "Assets/Showcase/Scenes/MeridianBasin/MeridianBasin.unity",
            "Assets/Showcase/Scenes/LevelPlayground/LevelPlayground.unity",
        };

        /// <summary>The size rendered at, and how many frames are timed at each spot (after WarmUpFrames untimed).</summary>
        private const int Width = 1920, Height = 1080, Frames = 100, WarmUpFrames = 10;

        /// <summary>How many of a level's lawn groups (the largest) get spots.</summary>
        private const int LawnGroupsPerScene = 2;

        /// <summary>A camera's eye height over the lawn (m), as the player's would be.</summary>
        private const float EyeHeight = 1.7f;

        /// <summary>How the old tufts are named (Graybox.GrassTufts), and the new grass's component.</summary>
        private const string TuftsName = "Grass Tufts", FieldTypeName = "GrassField";

        [MenuItem("Tools/Meridian Motion/Showcase/Measure Grass")]
        private static void MeasureFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) { return; }
            string path = Measure();
            EditorUtility.RevealInFinder(path);
        }

        /// <summary>
        /// The command line's way in: <c>-executeMethod MeridianMotion.Showcase.Grass.EditorTools.GrassBenchmark.MeasureFromCommandLine</c>,
        /// with <c>-scene [path]</c> for one level only.
        /// </summary>
        public static void MeasureFromCommandLine()
        {
            int code = 0;
            try { Measure(); }
            catch (Exception e) { Debug.LogException(e); code = 1; }
            EditorApplication.Exit(code);
        }

        /// <summary>Measures every level in Scenes, and writes the report: its path.</summary>
        public static string Measure()
        {
            runFolder = Path.Combine("Logs", "GrassBenchmark", DateTime.Now.ToString("yyyyMMdd-HHmmss"));
            Directory.CreateDirectory(runFolder);
            var report = new StringBuilder();
            report.AppendLine($"# Grass benchmark, {DateTime.Now:yyyy-MM-dd HH:mm}");
            report.AppendLine();
            report.AppendLine($"Unity {Application.unityVersion}, {SystemInfo.graphicsDeviceName} ({SystemInfo.graphicsDeviceType}), " +
                $"{SystemInfo.processorType}; {Width}×{Height}, quality level {QualitySettings.names[QualitySettings.GetQualityLevel()]}.");
            report.AppendLine($"Frame time is a frame rendered and waited for on the GPU (CPU and GPU together); GPU time the camera's render on the GPU. Each the median of {Frames}, with and without the grass alternating frame by frame.");
            report.AppendLine();
            // -scene [path] on the command line measures that level alone.
            string[] arguments = Environment.GetCommandLineArgs();
            int given = Array.IndexOf(arguments, "-scene");
            string[] scenes = given >= 0 && given + 1 < arguments.Length ? new[] { arguments[given + 1] } : Scenes;
            foreach (string scene in scenes)
            {
                if (!File.Exists(scene)) { report.AppendLine($"## {scene}\n\nMissing.\n"); continue; }
                MeasureScene(scene, report);
            }
            string path = Path.Combine(runFolder, "Report.md");
            File.WriteAllText(path, report.ToString());
            Debug.Log($"[Grass benchmark] Wrote {path}:\n{report}");
            return path;
        }

        private static void MeasureScene(string scenePath, StringBuilder report)
        {
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            report.AppendLine($"## {Path.GetFileNameWithoutExtension(scenePath)}");
            report.AppendLine();
            report.AppendLine($"Scene file: {new FileInfo(scenePath).Length / (1024f * 1024f):0.0} MB.");

            List<GameObject> tufts = AllObjects().Where(o => o.name == TuftsName).ToList();
            List<Behaviour> fields = AllObjects().SelectMany(o => o.GetComponents<Behaviour>())
                .Where(b => b != null && b.GetType().Name == FieldTypeName).ToList();
            long tuftVerts = 0, tuftTris = 0, tuftBytes = 0;
            foreach (GameObject o in tufts)
            {
                if (!o.TryGetComponent(out MeshFilter filter) || filter.sharedMesh == null) { continue; }
                Mesh mesh = filter.sharedMesh;
                tuftVerts += mesh.vertexCount;
                for (int i = 0; i < mesh.subMeshCount; i++) { tuftTris += (long)mesh.GetIndexCount(i) / 3; }
                tuftBytes += Profiler.GetRuntimeMemorySizeLong(mesh);
            }
            report.AppendLine($"Baked tufts: {tufts.Count} objects, {tuftVerts:N0} vertices, {tuftTris:N0} triangles, " +
                $"{tuftBytes / (1024f * 1024f):0.0} MB of meshes. GrassFields: {fields.Count}.");
            report.AppendLine();

            List<(string name, Vector3 position, Quaternion rotation)> spots = Spots();
            if (spots.Count == 0) { report.AppendLine("No lawns found.\n"); return; }

            report.AppendLine("| Spot | Frame, grass (ms) | Frame, none (ms) | Difference (ms) | GPU, grass (ms) | GPU, none (ms) | GPU difference (ms) | Tuft triangles in view |");
            report.AppendLine("| --- | --- | --- | --- | --- | --- | --- | --- |");

            var target = new RenderTexture(Width, Height, 24, RenderTextureFormat.DefaultHDR) { name = "Grass Benchmark" };
            var cameraObject = new GameObject("Grass Benchmark Camera") { hideFlags = HideFlags.DontSave };
            var camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 60f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 1000f;
            camera.targetTexture = target;
            camera.useOcclusionCulling = true;
            try
            {
                int spotNumber = 1;
                foreach ((string name, Vector3 position, Quaternion rotation) in spots)
                {
                    cameraObject.transform.SetPositionAndRotation(position, rotation);
                    (Sample with, Sample without) = RenderBoth(camera, target, tufts, fields);
                    SavePicture(camera, target, $"{Path.GetFileNameWithoutExtension(scenePath)} {spotNumber++} {name.Replace('/', ' ')}");
                    report.AppendLine($"| {name} | {with.ms:0.00} | {without.ms:0.00} | {with.ms - without.ms:0.00} | {Ms(with.gpuMs)} | {Ms(without.gpuMs)} | " +
                        $"{Ms(with.gpuMs - without.gpuMs)} | {VisibleTuftTriangles(camera, tufts):N0} |");
                }
            }
            finally
            {
                SetGrass(tufts, fields, true);
                camera.targetTexture = null;
                UnityEngine.Object.DestroyImmediate(cameraObject);
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
            }
            report.AppendLine();
            foreach ((string name, Vector3 position, Quaternion rotation) in spots)
            {
                report.AppendLine($"- {name}: at {position:F1}, looking {rotation.eulerAngles:F0}.");
            }
            report.AppendLine();
        }

        /// <summary>Where this run's report and pictures go (Logs/GrassBenchmark/[time]).</summary>
        private static string runFolder;

        /// <summary>Saves the camera's view, with the grass, as a PNG in the run's folder.</summary>
        private static void SavePicture(Camera camera, RenderTexture target, string name)
        {
            camera.Render();
            var ldr = RenderTexture.GetTemporary(Width, Height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(target, ldr);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = ldr;
            var picture = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            picture.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(ldr);
            File.WriteAllBytes(Path.Combine(runFolder, name + ".png"), picture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(picture);
        }

        private struct Sample { public double ms, gpuMs; }

        /// <summary>
        /// Renders the camera's view with the grass and without, alternating frame by frame (so drift in the machine's
        /// speed falls on both alike): WarmUpFrames pairs untimed, then Frames pairs timed, each frame waited for on the
        /// GPU. Each sample's the median; the GPU time is the camera's render on the GPU, where the platform reports it.
        /// </summary>
        private static (Sample with, Sample without) RenderBoth(Camera camera, RenderTexture target, List<GameObject> tufts, List<Behaviour> fields)
        {
            Recorder gpu = GpuRecorder();
            var times = new[] { new List<double>(), new List<double>() };
            var gpuTimes = new[] { new List<double>(), new List<double>() };
            for (int i = 0; i < WarmUpFrames + Frames; i++)
            {
                for (int k = 0; k < 2; k++)
                {
                    SetGrass(tufts, fields, k == 0);
                    var timer = Stopwatch.StartNew();
                    RenderOnce(camera, target);
                    if (i < WarmUpFrames) { continue; }
                    times[k].Add(timer.Elapsed.TotalMilliseconds);
                    if (gpu != null) { gpuTimes[k].Add(gpu.gpuElapsedNanoseconds / 1e6); }
                }
            }
            SetGrass(tufts, fields, true);
            if (gpu != null) { gpu.enabled = false; }
            Sample Of(int k) => new() { ms = Median(times[k]), gpuMs = gpuTimes[k].Count > 0 ? Median(gpuTimes[k]) : double.NaN };
            return (Of(0), Of(1));
        }

        /// <summary>A time in ms, or n/a where the platform didn't report it (GPU timers aren't there in batch mode).</summary>
        private static string Ms(double ms) => double.IsNaN(ms) ? "n/a" : ms.ToString("0.00");

        private static double Median(List<double> values)
        {
            values.Sort();
            return values[values.Count / 2];
        }

        /// <summary>The GPU timer of the camera's render, if the platform has one (it reports the frame before's).</summary>
        private static Recorder GpuRecorder()
        {
            if (!SystemInfo.supportsGpuRecorder) { return null; }
            foreach (string name in GpuSamplerNames)
            {
                Recorder recorder = Sampler.Get(name).GetRecorder();
                if (recorder == null || !recorder.isValid) { continue; }
                recorder.enabled = true;
                return recorder;
            }
            if (!loggedSamplers)
            {
                loggedSamplers = true;
                var names = new List<string>();
                Sampler.GetNames(names);
                Debug.Log("[Grass benchmark] No camera GPU sampler found; candidates: " +
                    string.Join(", ", names.Where(n => n.Contains("Camera") || n.Contains("Render Graph") || n.Contains("Opaque"))));
            }
            return null;
        }

        private static bool loggedSamplers;

        /// <summary>The samplers that time a camera's whole render, in the order tried.</summary>
        private static readonly string[] GpuSamplerNames = { "UniversalRenderPipeline.RenderSingleCameraInternal", "RenderSingleCameraInternal", "Render Camera" };

        /// <summary>
        /// About how many of the old tufts' triangles the camera draws: those of tuft renderers in its view and nearer
        /// than their LOD group's cut-off (worked out as the group itself does).
        /// </summary>
        private static long VisibleTuftTriangles(Camera camera, List<GameObject> tufts)
        {
            Plane[] planes = GeometryUtility.CalculateFrustumPlanes(camera);
            float viewHeightPerMetre = 2f * Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            long count = 0;
            foreach (GameObject o in tufts)
            {
                if (!o.TryGetComponent(out MeshRenderer renderer) || !o.TryGetComponent(out MeshFilter filter) || filter.sharedMesh == null) { continue; }
                if (!GeometryUtility.TestPlanesAABB(planes, renderer.bounds)) { continue; }
                if (o.TryGetComponent(out LODGroup lod))
                {
                    float distance = Vector3.Distance(camera.transform.position, o.transform.TransformPoint(lod.localReferencePoint));
                    float relative = lod.size * QualitySettings.lodBias / Mathf.Max(0.001f, distance * viewHeightPerMetre);
                    if (relative < lod.GetLODs()[0].screenRelativeTransitionHeight) { continue; }
                }
                for (int i = 0; i < filter.sharedMesh.subMeshCount; i++) { count += (long)filter.sharedMesh.GetIndexCount(i) / 3; }
            }
            return count;
        }

        /// <summary>One frame of the camera, waited for: a pixel read back, so the GPU has finished it.</summary>
        private static void RenderOnce(Camera camera, RenderTexture target)
        {
            camera.Render();
            AsyncGPUReadback.Request(target, 0, 0, 1, 0, 1, 0, 1).WaitForCompletion();
        }

        private static void SetGrass(List<GameObject> tufts, List<Behaviour> fields, bool on)
        {
            foreach (GameObject o in tufts) { o.SetActive(on); }
            foreach (Behaviour b in fields) { b.enabled = on; }
        }

        /// <summary>
        /// Two spots over each of the largest lawn groups: at eye height from a corner looking over it, and from above
        /// and to the side, looking down at its middle.
        /// </summary>
        private static List<(string, Vector3, Quaternion)> Spots()
        {
            var groups = new List<(string name, Bounds bounds)>();
            foreach (GameObject group in AllObjects().Where(o => o.name == "Lawns"))
            {
                Renderer[] lawns = group.GetComponentsInChildren<Renderer>(true).Where(r => r.name.StartsWith("Lawn (")).ToArray();
                if (lawns.Length == 0) { continue; }
                Bounds b = lawns[0].bounds;
                foreach (Renderer r in lawns) { b.Encapsulate(r.bounds); }
                string parent = group.transform.parent != null ? group.transform.parent.name : "root";
                groups.Add(($"{parent}/Lawns", b));
            }
            var spots = new List<(string, Vector3, Quaternion)>();
            foreach ((string name, Bounds b) in groups.OrderByDescending(g => g.bounds.size.x * g.bounds.size.z).Take(LawnGroupsPerScene))
            {
                float floor = b.min.y;
                Vector3 corner = new(b.min.x + 2f, floor + EyeHeight, b.min.z + 2f);
                Vector3 middle = new(b.center.x, floor, b.center.z);
                Vector3 look = middle - corner;
                look.y = 0f;
                spots.Add(($"{name}, eye height", corner, Quaternion.LookRotation(look) * Quaternion.Euler(10f, 0f, 0f)));
                float reach = Mathf.Max(b.size.x, b.size.z) * 0.5f;
                Vector3 above = middle + new Vector3(-reach, reach * 0.6f + 6f, -reach);
                spots.Add(($"{name}, from above", above, Quaternion.LookRotation(middle - above)));
            }
            return spots;
        }

        private static IEnumerable<GameObject> AllObjects() =>
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true)).Select(t => t.gameObject);
    }
}
