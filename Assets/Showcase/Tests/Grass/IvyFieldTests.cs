using System.Collections;
using System.Collections.Generic;
using MeridianMotion.Showcase.Ivy;
using MeridianMotion.Tests;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

namespace MeridianMotion.Showcase.Tests.Grass
{
    /// <summary>
    /// Tests of an ivy field placing its leaves (IvyField, IvyLeaves.compute: ReferenceAssets/OpenTasks.md, Q3 and Q8), on
    /// one wall face made in code (8 by 4 m, at z = 0, looking toward -z, its mask full) with a camera of its own: leaves
    /// are placed on a face turned toward the camera and none on one turned away; none beyond the style's Draw Distance
    /// (and the counts say so at once, not what was last in view); a camera's leaf lists grow to fit what it's asked for,
    /// and shrink once they've been roomy a while; and what it draws has no pixels that aren't numbers (which bloom
    /// spreads into flashes).
    /// </summary>
    [Category(TestArea.Levels)]
    [TestTiming]
    public class IvyFieldTests
    {
        private GameObject field, view, sun;
        private Camera camera;
        private IvyField ivy;
        private IvyStyle style;

        [SetUp]
        public void SetUp()
        {
            Assume.That(IvyField.Supported, "This platform can't run the ivy's compute shaders.");
            view = new GameObject("Ivy Test Camera");
            camera = view.AddComponent<Camera>();
            camera.targetTexture = new RenderTexture(320, 180, 24);
            Look(new Vector3(0f, 2f, -6f), 0f);

            // A style with every leaf drawn near and none thinned, so what's placed doesn't hang on the distance.
            style = ScriptableObject.CreateInstance<IvyStyle>();
            style.spacing = 0.1f;
            style.drawDistance = 30f;
            style.detailDistance = 30f;
            style.thinFrom = 30f;
            style.spacingByQuality = new[] { 1f, 1f };
        }

        [TearDown]
        public void TearDown()
        {
            if (field != null) { Object.Destroy(field); }
            if (sun != null) { Object.Destroy(sun); }
            if (view != null)
            {
                RenderTexture target = camera.targetTexture;
                Object.Destroy(view);
                if (target != null) { target.Release(); }
            }
            if (style != null) { Object.Destroy(style); }
        }

        /// <summary>The camera at from, turned yaw degrees from looking along +z (at the face).</summary>
        private void Look(Vector3 from, float yaw) => view.transform.SetPositionAndRotation(from, Quaternion.Euler(0f, yaw, 0f));

        private void MakeField()
        {
            field = new GameObject("Ivy");
            field.SetActive(false);
            ivy = field.AddComponent<IvyField>();
            const int width = 80, height = 40;
            var density = new byte[width * height];
            for (int i = 0; i < density.Length; i++) { density[i] = 255; }
            ivy.SetFaces(new List<IvyField.Face>
            {
                new()
                {
                    corner = new Vector3(-4f, 0f, 0f), along = Vector3.right, up = Vector3.up, outward = Vector3.back,
                    size = new Vector2(8f, 4f), width = width, height = height, density = density,
                    stems = IvyGrowth.Pack(new List<List<Vector3>>()),
                },
            });
            ivy.Style = style;
#if UNITY_EDITOR
            ivy.SetLook(UnityEditor.AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/Showcase/Ivy/IvyLeaves.compute"),
                UnityEditor.AssetDatabase.LoadAssetAtPath<Shader>("Assets/Showcase/Ivy/Ivy.shader"),
                UnityEditor.AssetDatabase.LoadAssetAtPath<Shader>("Assets/Showcase/Ivy/IvyFar.shader"), null);
#endif
            field.SetActive(true);
        }

        private static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++) { yield return null; }
        }

        [UnityTest]
        public IEnumerator LeavesArePlaced_OnAFaceTurnedTowardTheCamera_AndNoneOnOneTurnedAway()
        {
            MakeField();
            yield return Frames(5);
            Assert.That(ivy.Counts(camera).near, Is.GreaterThan(100), "Looking at the face, there should be leaves.");
            // Behind the wall, looking back at it: the face's back, whose leaves would be on the far side.
            Look(new Vector3(0f, 2f, 6f), 180f);
            yield return Frames(5);
            Assert.That(ivy.Counts(camera), Is.EqualTo((0, 0)), "Looking at the face's back, there should be none.");
        }

        [UnityTest]
        public IEnumerator NoLeavesArePlaced_BeyondTheDrawDistance()
        {
            MakeField();
            yield return Frames(5);
            Assert.That(ivy.Counts(camera).near, Is.GreaterThan(100), "Test setup: near the face, there should be leaves.");
            Look(new Vector3(0f, 2f, -(style.drawDistance + 5f)), 0f);
            yield return Frames(5);
            Assert.That(ivy.Counts(camera), Is.EqualTo((0, 0)), "Beyond the draw distance there should be none (not the counts last asked for).");
        }

        [UnityTest]
        public IEnumerator ACamerasLeafLists_GrowToFitWhatItsAskedFor()
        {
            // Dense enough that the near list's starting size can't hold them: it grows, and then holds them all.
            style.spacing = 0.02f;
            MakeField();
            yield return Frames(10);
            int near = ivy.Counts(camera).near;
            Assert.That(near, Is.GreaterThan(IvyField.StartingLeaves), "Test setup: the camera should ask for more leaves than the starting list holds.");
            Assert.That(ivy.Capacities(camera)?.near, Is.GreaterThanOrEqualTo(near), "The near list should have grown to hold them.");
        }

        [UnityTest]
        public IEnumerator ACamerasLeafLists_ShrinkOnceTheyveBeenRoomyAWhile()
        {
            style.spacing = 0.02f;
            MakeField();
            yield return Frames(10);
            int grown = ivy.Capacities(camera)?.near ?? 0;
            // Far fewer leaves (thinned to a hundredth, without rebuilding the field as a new spacing would): the list
            // holds over four times what's asked for, and after a few seconds of that it halves.
            style.thinFrom = 0.5f;
            style.thinTo = 0.01f;
            float until = Time.realtimeSinceStartup + 7f;
            while (Time.realtimeSinceStartup < until) { yield return null; }
            Assert.That(ivy.Capacities(camera)?.near, Is.LessThan(grown), "The near list should have shrunk.");
        }

        [UnityTest]
        public IEnumerator WhatItDraws_HasNoPixelsThatArentNumbers()
        {
            sun = new GameObject("Ivy Test Sun");
            Light light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(40f, 20f, 0f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.blue;
            var linear = new RenderTexture(320, 180, 24, RenderTextureFormat.ARGBHalf);
            RenderTexture before = camera.targetTexture;
            camera.targetTexture = linear;
            before.Release();
            MakeField();
            Look(new Vector3(0f, 2f, -2.5f), 0f);
            yield return Frames(5);
            camera.Render();
            AsyncGPUReadbackRequest request = AsyncGPUReadback.Request(linear, 0, TextureFormat.RGBAFloat);
            request.WaitForCompletion();
            int nans = 0, leaves = 0;
            foreach (Color c in request.GetData<Color>())
            {
                float most = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
                if (float.IsNaN(most) || float.IsInfinity(most)) { nans++; }
                else if (c.b < 0.9f || c.g > 0.05f) { leaves++; }
            }
            Assert.That(leaves, Is.GreaterThan(320 * 180 / 10), "Test setup: the leaves should fill much of the view.");
            Assert.That(nans, Is.Zero, "No pixel should be NaN or infinite.");
        }
    }
}
