using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using MeridianMotion.Showcase.Ivy;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;

namespace MeridianMotion.Showcase.Grass.EditorTools
{
    /// <summary>
    /// Measure Ivy: pictures and times a level's GPU ivy (ReferenceAssets/OpenTasks.md, Q1-Q7). It opens a scene (the
    /// Tutorial Ring, or -scene [path]), finds its ivy fields, and from the longest ivy face's middle pictures it close up,
    /// from across a lane, from across the ring's middle, from further than the leaves are drawn (the far layer), and
    /// along the wall, and the longest vine face close up and from across a lane. At each spot it times the frame without
    /// the ivy, with all of it, and with each of its parts alone (IvyField.Parts: the leaves, placed and drawn; the stems;
    /// the far layer), and counts the leaves placed near and far and the stem vertices drawn. With -baseline [scene], it
    /// then times the same spots in that scene with its ivy prefabs on and off (the prefab ivy the GPU ivy replaced, for
    /// comparison). Writes the pictures and a report, held to the budget (IvyBudget), to Logs/IvyPictures/[time]. The
    /// scenes aren't saved. -executeMethod MeridianMotion.Showcase.Grass.EditorTools.IvyPictures.PictureFromCommandLine
    /// (with -styleSet name=value,... to try the style's settings on a copy, by their field names).
    /// </summary>
    public static class IvyPictures
    {
        private const string DefaultScene = "Assets/Showcase/Scenes/TutorialRing/TutorialRing.unity";
        private const int Width = 1920, Height = 1080, WarmUp = 8, Frames = 60;

        /// <summary>
        /// The ivy's budget (ms): the most it may add to a frame at the busiest spot in the Tutorial Ring, at 1080p on the
        /// development Mac (decided 2026-11-24: ReferenceAssets/OpenTasks.md, category Q).
        /// </summary>
        public const double IvyBudget = 0.5;

        /// <summary>What a frame's timed with: no ivy, all of it, and each part alone.</summary>
        private static readonly (string name, IvyField.Parts parts)[] States =
        {
            ("none", IvyField.Parts.None), ("all", IvyField.Parts.All), ("leaves", IvyField.Parts.Leaves),
            ("stems", IvyField.Parts.Stems), ("far layer", IvyField.Parts.FarLayer),
        };

        /// <summary>What the ivy prefabs were grouped under in a level built before the GPU ivy (Graybox's Ivy and Vines pieces).</summary>
        private static readonly string[] PrefabIvyGroups = { "Ivy", "Vines" };

        [MenuItem("Tools/Meridian Motion/Showcase/Measure Ivy")]
        private static void MeasureFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) { return; }
            EditorUtility.RevealInFinder(Path.Combine(Picture(), "Report.md"));
        }

        /// <summary>For -executeMethod: see the class.</summary>
        public static void PictureFromCommandLine()
        {
            int code = 0;
            try { Picture(); }
            catch (Exception e) { Debug.LogException(e); code = 1; }
            EditorApplication.Exit(code);
        }

        public static string Picture()
        {
            string[] arguments = Environment.GetCommandLineArgs();
            int given = Array.IndexOf(arguments, "-scene");
            string scene = given >= 0 && given + 1 < arguments.Length ? arguments[given + 1] : DefaultScene;
            string folder = Path.Combine("Logs", "IvyPictures", DateTime.Now.ToString("yyyyMMdd-HHmmss"));
            Directory.CreateDirectory(folder);
            EditorSceneManager.OpenScene(scene, OpenSceneMode.Single);
            Physics.SyncTransforms();
            List<IvyField> fields = UnityEngine.Object.FindObjectsByType<IvyField>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).ToList();
            if (fields.Count == 0 || fields.All(f => f.Faces.Count == 0)) { throw new InvalidOperationException($"{scene} has no ivy: rebuild it."); }
            ApplyStyleSettings(fields);
            var all = fields.SelectMany(f => f.Faces.Select(x => (field: f, face: x))).ToList();
            (IvyField field, IvyField.Face face) = all.Where(p => !p.face.hangs).DefaultIfEmpty(all[0]).OrderByDescending(p => p.face.size.x).First();
            (IvyField vineField, IvyField.Face vines) = all.Where(p => p.face.hangs).OrderByDescending(p => p.face.size.x).FirstOrDefault();

            var report = new StringBuilder($"# Ivy pictures, {Path.GetFileNameWithoutExtension(scene)}, {DateTime.Now:yyyy-MM-dd HH:mm}\n\n");
            report.AppendLine($"Unity {Application.unityVersion}, {SystemInfo.graphicsDeviceName}; {Width}×{Height}. Each time the median of {Frames} frames, " +
                "the ivy's states alternating frame by frame (none, all, then each part alone), each frame waited for. The parts' columns are each part alone over the frame without the ivy.");
            report.AppendLine();
            int faceCount = fields.Sum(f => f.Faces.Count);
            report.AppendLine($"- **Ivy:** {fields.Count} field(s), {faceCount} faces, {fields.Sum(f => f.Faces.Sum(x => x.size.x * x.size.y)):N0} m² of wall, " +
                $"{fields.Sum(f => f.Faces.Sum(x => IvyGrowth.Unpack(x.stems).Count)):N0} stems ({fields.Sum(f => f.StemVertices):N0} stem vertices), saved masks and stems {fields.Sum(f => f.Faces.Sum(x => (long)(x.density?.Length ?? 0) + (x.stems?.Length ?? 0))) / (1024f * 1024f):0.0} MB.");
            report.AppendLine($"- **Pictured:** {field.name}'s longest ivy face, {face.size.x:0.0} × {face.size.y:0.0} m" +
                (vines != null ? $"; its longest vine face, {vines.size.x:0.0} × {vines.size.y:0.0} m ({all.Count(p => p.face.hangs)} vine faces)." : "; no vines."));
            report.AppendLine($"- **Style:** {Describe(field.Style)}.");
            report.AppendLine();
            report.AppendLine("| Spot | Frame, without (ms) | All (ms) | Leaves (ms) | Stems (ms) | Far layer (ms) | Leaves near / far | Stem vertices drawn | GPU memory (MB) |");
            report.AppendLine("| --- | --- | --- | --- | --- | --- | --- | --- | --- |");

            var target = new RenderTexture(Width, Height, 24, RenderTextureFormat.DefaultHDR) { name = "Ivy Pictures" };
            var cameraObject = new GameObject("Ivy Pictures Camera") { hideFlags = HideFlags.DontSave };
            var camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 60f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 1500f;
            camera.targetTexture = target;
            camera.useOcclusionCulling = true;
            List<(string name, Vector3 position, Vector3 lookAt)> spots = Spots(field, face).ToList();
            if (vines != null) { spots.AddRange(VineSpots(vineField, vines)); }
            double busiest = 0;
            string busiestSpot = "";
            try
            {
                int number = 1;
                foreach ((string name, Vector3 position, Vector3 lookAt) in spots)
                {
                    cameraObject.transform.SetPositionAndRotation(position, Quaternion.LookRotation(lookAt - position));
                    double[] times = Time(camera, target, States.Length, k => { IvyField.Suspended = k == 0; IvyField.Drawn = States[k].parts; });
                    IvyField.Suspended = false;
                    IvyField.Drawn = IvyField.Parts.All;
                    for (int i = 0; i < 3; i++) { RenderOnce(camera, target); AsyncGPUReadback.WaitAllRequests(); }
                    int near = fields.Sum(f => f.Counts(camera).near), far = fields.Sum(f => f.Counts(camera).far);
                    int stemVertices = fields.Sum(f => f.StemVerticesDrawn);
                    SavePicture(camera, target, Path.Combine(folder, $"{number++:00} {name}.png"));
                    double Over(int k) => times[k] - times[0];
                    if (Over(1) > busiest) { busiest = Over(1); busiestSpot = name; }
                    report.AppendLine($"| {name} | {times[0]:0.00} | {Over(1):0.00} | {Over(2):0.00} | {Over(3):0.00} | {Over(4):0.00} | {near:N0} / {far:N0} | {stemVertices:N0} | {IvyField.GpuMemory() / (1024f * 1024f):0.0} |");
                }
                report.AppendLine();
                report.AppendLine($"**Budget:** the most the ivy adds is {busiest:0.00} ms ({busiestSpot}), {(busiest <= IvyBudget ? "within" : "over")} the budget of {IvyBudget} ms " +
                    "(IvyPictures.IvyBudget; noise is about ±0.2 ms).");
            }
            finally
            {
                IvyField.Suspended = false;
                IvyField.Drawn = IvyField.Parts.All;
                camera.targetTexture = null;
                UnityEngine.Object.DestroyImmediate(cameraObject);
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
            }
            int baseline = Array.IndexOf(arguments, "-baseline");
            if (baseline >= 0 && baseline + 1 < arguments.Length) { MeasureBaseline(arguments[baseline + 1], spots, report); }
            File.WriteAllText(Path.Combine(folder, "Report.md"), report.ToString());
            Debug.Log($"[Ivy pictures] Wrote {folder}:\n{report}");
            return folder;
        }

        /// <summary>
        /// -baseline [scene]: the same spots timed in a scene with prefab ivy (one built before the GPU ivy), its ivy
        /// prefabs' renderers (under the Ivy and Vines pieces' groups) on and off.
        /// </summary>
        private static void MeasureBaseline(string scene, List<(string name, Vector3 position, Vector3 lookAt)> spots, StringBuilder report)
        {
            EditorSceneManager.OpenScene(scene, OpenSceneMode.Single);
            List<Renderer> prefabIvy = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects()
                .SelectMany(r => r.GetComponentsInChildren<Transform>(true))
                .Where(t => PrefabIvyGroups.Contains(t.name))
                .SelectMany(t => t.GetComponentsInChildren<Renderer>(true)).Distinct().ToList();
            report.AppendLine();
            report.AppendLine($"## Baseline: the prefab ivy, {Path.GetFileNameWithoutExtension(scene)}");
            report.AppendLine();
            report.AppendLine($"{prefabIvy.Count:N0} renderers, {prefabIvy.Select(r => r.GetComponentInParent<LODGroup>()).Where(g => g != null).Distinct().Count():N0} LOD groups; scene file {new FileInfo(scene).Length / (1024f * 1024f):0.0} MB. The same spots, its ivy off and on.");
            report.AppendLine();
            report.AppendLine("| Spot | Frame, without (ms) | Prefab ivy (ms) |");
            report.AppendLine("| --- | --- | --- |");
            var target = new RenderTexture(Width, Height, 24, RenderTextureFormat.DefaultHDR) { name = "Ivy Baseline" };
            var cameraObject = new GameObject("Ivy Baseline Camera") { hideFlags = HideFlags.DontSave };
            var camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 60f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 1500f;
            camera.targetTexture = target;
            camera.useOcclusionCulling = true;
            try
            {
                foreach ((string name, Vector3 position, Vector3 lookAt) in spots)
                {
                    cameraObject.transform.SetPositionAndRotation(position, Quaternion.LookRotation(lookAt - position));
                    double[] times = Time(camera, target, 2, k => { foreach (Renderer r in prefabIvy) { r.enabled = k == 1; } });
                    report.AppendLine($"| {name} | {times[0]:0.00} | {times[1] - times[0]:0.00} |");
                }
            }
            finally
            {
                foreach (Renderer r in prefabIvy) { r.enabled = true; }
                camera.targetTexture = null;
                UnityEngine.Object.DestroyImmediate(cameraObject);
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        /// <summary>Where a face's ivy is pictured from (in the world): name, eye, and what it's looking at.</summary>
        private static IEnumerable<(string, Vector3, Vector3)> Spots(IvyField field, IvyField.Face f)
        {
            Vector3 Point(float u, float v, float outward) => field.transform.TransformPoint(f.corner + f.along * u + f.up * v + f.outward * outward);
            float middle = f.size.x / 2f, beyond = field.Style.drawDistance * 1.15f;
            yield return ("close up (2.5 m)", Point(middle, 1.7f, 2.5f), Point(middle, 1.5f, 0f));
            yield return ("across a lane (12 m)", Point(middle, 3f, 12f), Point(middle, 3.5f, 0f));
            yield return ("across the middle (50 m)", Point(middle, 6f, 50f), Point(middle, 4f, 0f));
            yield return ($"beyond the leaves ({beyond:0} m)", Point(middle, 10f, beyond), Point(middle, 4f, 0f));
            yield return ("along the wall", Point(3f, 1.8f, 5f), Point(18f, 2.5f, 0f));
        }

        /// <summary>Where a vine face is pictured from: close up under its middle's top, and from across a lane.</summary>
        private static IEnumerable<(string, Vector3, Vector3)> VineSpots(IvyField field, IvyField.Face f)
        {
            Vector3 Point(float u, float v, float outward) => field.transform.TransformPoint(f.corner + f.along * u + f.up * v + f.outward * outward);
            float middle = f.size.x / 2f;
            yield return ("vines close up (3 m)", Point(middle, f.size.y * 0.3f, 3f), Point(middle, f.size.y * 0.6f, 0f));
            yield return ("vines across a lane (12 m)", Point(middle, f.size.y * 0.2f, 12f), Point(middle, f.size.y * 0.5f, 0f));
        }

        /// <summary>-styleSet name=value,...: each field's style copied with those settings changed (its fields, by name).</summary>
        private static void ApplyStyleSettings(List<IvyField> fields)
        {
            string[] arguments = Environment.GetCommandLineArgs();
            int set = Array.IndexOf(arguments, "-styleSet");
            if (set < 0 || set + 1 >= arguments.Length) { return; }
            foreach (IvyField field in fields)
            {
                IvyStyle copy = UnityEngine.Object.Instantiate(field.Style);
                foreach (string pair in arguments[set + 1].Split(','))
                {
                    string[] parts = pair.Split('=');
                    System.Reflection.FieldInfo setting = typeof(IvyStyle).GetField(parts[0]);
                    if (setting == null) { throw new ArgumentException($"An ivy style has no setting '{parts[0]}'."); }
                    object value = setting.FieldType == typeof(bool) ? bool.Parse(parts[1])
                        : setting.FieldType == typeof(Vector2) ? new Vector2(Float(parts[1].Split(':')[0]), Float(parts[1].Split(':')[1]))
                        : Float(parts[1]);
                    setting.SetValue(copy, value);
                }
                field.Style = copy;
            }
        }

        private static float Float(string s) => float.Parse(s, System.Globalization.CultureInfo.InvariantCulture);

        private static string Describe(IvyStyle s) =>
            $"spacing {s.spacing}, leaf size {s.leafSize.x}-{s.leafSize.y} m, draw {s.drawDistance} m, detail {s.detailDistance} m, " +
            $"thin from {s.thinFrom} m to {s.thinTo}, stems to {s.stemDistance} m, far fade {s.farFade}, shadows {s.castsShadows}";

        /// <summary>
        /// The median frame time (ms) of each of a number of states, set by the given action before each frame: the
        /// states alternate frame by frame, so drift in the machine's speed falls on all alike. (The ivy's are set by
        /// IvyField.Suspended and IvyField.Drawn, not by turning fields off: a field turned on again remakes everything
        /// it's drawn with.)
        /// </summary>
        private static double[] Time(Camera camera, RenderTexture target, int states, Action<int> set)
        {
            var times = Enumerable.Range(0, states).Select(_ => new List<double>()).ToArray();
            for (int i = 0; i < WarmUp + Frames; i++)
            {
                for (int k = 0; k < states; k++)
                {
                    set(k);
                    var timer = Stopwatch.StartNew();
                    RenderOnce(camera, target);
                    if (i >= WarmUp) { times[k].Add(timer.Elapsed.TotalMilliseconds); }
                }
            }
            double Median(List<double> v) { v.Sort(); return v[v.Count / 2]; }
            return times.Select(Median).ToArray();
        }

        private static void RenderOnce(Camera camera, RenderTexture target)
        {
            camera.Render();
            AsyncGPUReadback.Request(target, 0, 0, 1, 0, 1, 0, 1).WaitForCompletion();
        }

        private static void SavePicture(Camera camera, RenderTexture target, string path)
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
            File.WriteAllBytes(path, picture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(picture);
        }
    }
}
