using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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
    /// A diagnostic, not a test (Explicit: run only by name, -testFilter GrassFlashDiagnostic): looks for flashes in
    /// the grass in play mode, where the user saw them and batch-mode renders didn't. It plays MeridianComplex as the
    /// game does (its spawner, its camera rig, its scripts), takes the game camera over (turning off the rig's own
    /// scripts), walks it across the largest lawn at eye height a little each frame, and reads each frame back. A block
    /// of pixels whose brightness jumps for one frame against the frames either side is a flash. The counts, and the
    /// frames with the most, go to Logs/GrassBenchmark/PlayFlashes-[time].
    /// </summary>
    [Category(TestArea.Levels)]
    [TestTiming]
    public class GrassFlashDiagnostic
    {
        private const string ScenePath = "Assets/Showcase/Scenes/MeridianComplex/MeridianComplex.unity";
        private const int Frames = 300, Width = 960, Height = 540, Block = 16;
        private const float Step = 0.05f, Turn = 0.15f, EyeHeight = 1.7f;

        [UnityTest, Explicit("A diagnostic: run it by name.")]
        public IEnumerator FlashesInPlayMode()
        {
#if UNITY_EDITOR
            AsyncOperation loading = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            while (!loading.isDone) { yield return null; }
#else
            Assert.Ignore("Editor only.");
            yield break;
#endif
            // A few seconds for the spawner, the camera rig and the grass to settle.
            for (int i = 0; i < 120; i++) { yield return null; }

            Camera camera = Camera.main;
            Assert.That(camera, Is.Not.Null, "The scene should have a game camera (Set Up Scene).");
            foreach (MonoBehaviour behaviour in camera.transform.root.GetComponentsInChildren<MonoBehaviour>())
            {
                // The rig's own scripts would move the camera; Unity's (the camera's URP data) stay.
                if (!behaviour.GetType().Namespace?.StartsWith("UnityEngine") ?? true) { behaviour.enabled = false; }
            }
            // Without post-processing (bloom spreads a single bad pixel into a flash over the screen), so the pixels
            // themselves show; ones that aren't numbers are counted, with what's under them.
            var cameraData = camera.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            if (cameraData != null) { cameraData.renderPostProcessing = false; }
            var target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGBHalf) { name = "Grass Flash Diagnostic" };
            var bad = new StringBuilder();
            camera.targetTexture = target;

            (Vector3 start, Quaternion facing) = Start();
            Vector3 forward = facing * Vector3.forward;
            forward.y = 0f;
            forward.Normalize();
            var images = new List<float[]>();
            for (int f = 0; f < Frames; f++)
            {
                camera.transform.SetPositionAndRotation(start + forward * (Step * f), Quaternion.Euler(0f, Turn * f, 0f) * facing);
                // The frame renders after this resumes; read back once it has (WaitForEndOfFrame doesn't run in batch mode).
                yield return null;
                AsyncGPUReadbackRequest request = AsyncGPUReadback.Request(target, 0, TextureFormat.RGBAFloat);
                request.WaitForCompletion();
                Unity.Collections.NativeArray<Color> pixels = request.GetData<Color>();
                var luminance = new float[pixels.Length];
                int nans = 0, firstBad = -1;
                float brightest = 0f;
                for (int k = 0; k < pixels.Length; k++)
                {
                    Color c = pixels[k];
                    luminance[k] = 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
                    if (float.IsNaN(luminance[k]) || float.IsInfinity(luminance[k]) || luminance[k] > 50f) { nans++; if (firstBad < 0) { firstBad = k; } }
                    else { brightest = Mathf.Max(brightest, luminance[k]); }
                }
                images.Add(luminance);
                if (nans > 0)
                {
                    // What's at the first bad pixel: the collider a ray from the camera through it meets.
                    Ray ray = camera.ViewportPointToRay(new Vector3((firstBad % Width + 0.5f) / Width, (firstBad / Width + 0.5f) / Height, 0f));
                    string hit = Physics.Raycast(ray, out RaycastHit h, 500f) ? $"{h.collider.name} ({PathOf(h.collider.transform)}) at {h.distance:0.0} m" : "nothing with a collider";
                    bad.AppendLine($"| {f} | {nans} | ({firstBad % Width}, {firstBad / Width}) | {pixels[firstBad]} | {hit} | {brightest:0.00} |");
                }
            }
            camera.targetTexture = null;
            Write(images, bad.ToString());
            target.Release();
            Object.Destroy(target);
        }

        /// <summary>Where the walk starts: at eye height inside a corner of the largest lawn group, facing its middle.</summary>
        private static (Vector3, Quaternion) Start()
        {
            Bounds largest = default;
            float most = -1f;
            foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Where(t => t.name == "Lawns"))
            {
                Renderer[] lawns = t.GetComponentsInChildren<Renderer>().Where(r => r.name.StartsWith("Lawn (")).ToArray();
                if (lawns.Length == 0) { continue; }
                Bounds b = lawns[0].bounds;
                foreach (Renderer r in lawns) { b.Encapsulate(r.bounds); }
                if (b.size.x * b.size.z > most) { most = b.size.x * b.size.z; largest = b; }
            }
            Assert.That(most, Is.GreaterThan(0f), "The scene should have lawns.");
            var corner = new Vector3(largest.min.x + 2f, largest.min.y + EyeHeight, largest.min.z + 2f);
            Vector3 look = new Vector3(largest.center.x, corner.y, largest.center.z) - corner;
            return (corner, Quaternion.LookRotation(look) * Quaternion.Euler(10f, 0f, 0f));
        }

        private static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;

        private static void Write(List<float[]> images, string bad)
        {
            string folder = Path.Combine("Logs", "GrassBenchmark", "PlayFlashes-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"));
            Directory.CreateDirectory(folder);
            int bw = Width / Block, bh = Height / Block;
            List<float[]> blocks = images.Select(image =>
            {
                var b = new float[bw * bh];
                for (int k = 0; k < image.Length; k++)
                {
                    int bx = (k % Width) / Block, by = (k / Width) / Block;
                    if (bx < bw && by < bh) { b[by * bw + bx] += image[k] / (Block * Block); }
                }
                return b;
            }).ToList();
            bool Flash(int f, int k) => float.IsNaN(blocks[f][k]) || blocks[f][k] - Mathf.Max(blocks[f - 1][k], blocks[f + 1][k]) > 0.04f + 0.15f * Mathf.Max(blocks[f - 1][k], blocks[f + 1][k]);

            var counts = new int[Frames];
            var lines = new StringBuilder();
            for (int f = 1; f + 1 < Frames; f++)
            {
                for (int k = 0; k < bw * bh; k++) { if (Flash(f, k)) { counts[f]++; } }
                lines.AppendLine($"| {f} | {counts[f]} |");
            }
            var report = new StringBuilder($"# Flashes in play mode, {Path.GetFileNameWithoutExtension(ScenePath)}\n\n");
            report.AppendLine($"The game camera moves {Step} m and turns {Turn}° a frame. Blocks ({Block} pixels square) flashing, a frame on average: " +
                $"{counts.Skip(1).Take(Frames - 2).Average():N1} of {bw * bh:N0}; frames with any: {counts.Count(c => c > 0)}.\n");
            report.AppendLine(bad.Length == 0 ? "No pixels that aren't numbers (or over 50).\n" :
                "Pixels that aren't numbers (NaN or infinite) or over 50, and what's under the first:\n\n| Frame | Pixels | First | Its colour | Under it | Brightest good pixel |\n| --- | --- | --- | --- | --- | --- |\n" + bad);
            report.AppendLine("| Frame | Blocks flashing |\n| --- | --- |");
            report.Append(lines);
            File.WriteAllText(Path.Combine(folder, "Flashes.md"), report.ToString());

            foreach (int f in Enumerable.Range(1, Frames - 2).OrderByDescending(f => counts[f]).Take(3))
            {
                for (int g = f - 1; g <= f + 1; g++)
                {
                    var picture = new Texture2D(Width, Height, TextureFormat.RGB24, false);
                    for (int k = 0; k < images[g].Length; k++)
                    {
                        float v = Mathf.Clamp01(images[g][k]);
                        int bk = Mathf.Min((k / Width) / Block, bh - 1) * bw + Mathf.Min((k % Width) / Block, bw - 1);
                        bool edge = (k % Width) % Block == 0 || (k / Width) % Block == 0;
                        picture.SetPixel(k % Width, k / Width, g == f && edge && Flash(f, bk) ? Color.magenta : new Color(v, v, v));
                    }
                    File.WriteAllBytes(Path.Combine(folder, $"frame {f:000} ({g - f:+0;-0;0}).png"), picture.EncodeToPNG());
                    Object.Destroy(picture);
                }
            }
            Debug.Log($"[Grass flashes] Wrote {folder}.");
        }
    }
}
