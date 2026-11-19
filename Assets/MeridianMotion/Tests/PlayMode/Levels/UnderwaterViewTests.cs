using System.Collections.Generic;
using MeridianMotion.Levels;
using NUnit.Framework;
using UnityEngine;

namespace MeridianMotion.Tests.Levels
{
    /// <summary>
    /// Tests of the camera's underwater view (UnderwaterView): a camera going under a water's surface changes the scene's
    /// fog to the water's and tints and dims the sun, and raises CameraSubmerged; coming back up puts the scene's own fog
    /// and sun back as they were and raises CameraSurfaced; a camera just above the surface (within the margin) counts as under; moving about above or under
    /// the water raises nothing more; and turning the view off while under puts the scene's fog back.
    /// </summary>
    [Category(TestArea.Levels)]
    [TestTiming]
    public class UnderwaterViewTests
    {
        private readonly List<Object> created = new();
        private readonly List<(bool submerged, SwimmableWater water)> raised = new();
        private SwimmableWater water;
        private UnderwaterView view;
        private (bool fog, FogMode mode, Color colour, float density) sceneFog;
        private Light sceneSun, sun;
        private static readonly Color SunColour = new(1f, 0.95f, 0.9f);
        private const float SunIntensity = 1.5f;

        [SetUp]
        public void SetUp()
        {
            sceneFog = (RenderSettings.fog, RenderSettings.fogMode, RenderSettings.fogColor, RenderSettings.fogDensity);
            // The scene's own fog, as the levels' haze is.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.8f, 0.85f, 0.9f);
            RenderSettings.fogDensity = 0.002f;
            sceneSun = RenderSettings.sun;
            var sunObject = new GameObject("Sun");
            created.Add(sunObject);
            sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = SunColour;
            sun.intensity = SunIntensity;
            RenderSettings.sun = sun;

            var waterObject = new GameObject("Water");
            created.Add(waterObject);
            water = waterObject.AddComponent<SwimmableWater>();
            water.Size = new Vector3(20f, 5f, 20f);
            var cameraObject = new GameObject("Camera", typeof(Camera));
            created.Add(cameraObject);
            cameraObject.transform.position = new Vector3(0f, 2f, 0f);
            view = cameraObject.AddComponent<UnderwaterView>();
            raised.Clear();
            view.CameraSubmerged += w => raised.Add((true, w));
            view.CameraSurfaced += w => raised.Add((false, w));
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in created) { if (o != null) { Object.DestroyImmediate(o); } }
            created.Clear();
            (RenderSettings.fog, RenderSettings.fogMode, RenderSettings.fogColor, RenderSettings.fogDensity) = sceneFog;
            RenderSettings.sun = sceneSun;
        }

        private void MoveCameraTo(float height)
        {
            view.transform.position = new Vector3(0f, height, 0f);
            view.Refresh();
        }

        private void AssertSceneFog()
        {
            Assert.That(sun.color, Is.EqualTo(SunColour), "The sun should be as it was.");
            Assert.That(sun.intensity, Is.EqualTo(SunIntensity));
            Assert.That(RenderSettings.fogColor, Is.EqualTo(new Color(0.8f, 0.85f, 0.9f)));
            Assert.That(RenderSettings.fogDensity, Is.EqualTo(0.002f));
            Assert.That(RenderSettings.fogMode, Is.EqualTo(FogMode.Exponential));
            Assert.That(RenderSettings.fog, Is.True);
        }

        [Test]
        public void UnderTheSurface_TheFogIsTheWaters_AndBackAbove_TheScenesAgain()
        {
            MoveCameraTo(2f);
            Assert.That(view.IsUnderwater, Is.False);
            AssertSceneFog();

            MoveCameraTo(-1f);
            Assert.That(view.IsUnderwater, Is.True);
            Assert.That(view.Water, Is.SameAs(water));
            Assert.That(RenderSettings.fog, Is.True);
            Assert.That(RenderSettings.fogColor, Is.EqualTo(view.FogColor), "Under the water, the fog should be the water's colour.");
            Assert.That(RenderSettings.fogDensity, Is.EqualTo(view.FogDensity), "Under the water, the fog should be the water's density.");
            Assert.That(sun.intensity, Is.LessThan(SunIntensity), "Under the water, less of the sun should reach.");
            Assert.That(sun.color, Is.Not.EqualTo(SunColour), "Under the water, the sun should be tinted.");
            Assert.That(raised, Is.EqualTo(new List<(bool, SwimmableWater)> { (true, water) }));

            MoveCameraTo(-3f); // Moving about under the water raises nothing more.
            MoveCameraTo(2f);
            Assert.That(view.IsUnderwater, Is.False);
            AssertSceneFog();
            Assert.That(raised, Is.EqualTo(new List<(bool, SwimmableWater)> { (true, water), (false, water) }));
        }

        [Test]
        public void JustAboveTheSurface_WithinTheMargin_CountsAsUnder()
        {
            MoveCameraTo(0.02f);
            Assert.That(view.IsUnderwater, Is.True, "Just above the surface, the camera's view is already going under.");
            MoveCameraTo(0.5f);
            Assert.That(view.IsUnderwater, Is.False);
        }

        [Test]
        public void OutsideTheWatersFootprint_IsntUnder()
        {
            view.transform.position = new Vector3(15f, -1f, 0f);
            view.Refresh();
            Assert.That(view.IsUnderwater, Is.False);
            Assert.That(raised, Is.Empty);
            AssertSceneFog();
        }

        [Test]
        public void TurningTheViewOffUnderwater_PutsTheScenesFogBack()
        {
            MoveCameraTo(-1f);
            Assume.That(view.IsUnderwater, Is.True);
            view.enabled = false;
            Assert.That(view.IsUnderwater, Is.False);
            AssertSceneFog();
            Assert.That(raised[^1], Is.EqualTo((false, water)));
        }
    }
}
