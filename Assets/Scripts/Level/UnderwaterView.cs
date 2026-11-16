using System;
using UnityEngine;

namespace Level
{
    /// <summary>
    /// The view from under the water, for the camera it's on: while the camera is in a body of water (see Water), the
    /// scene's fog becomes the water's, dense and tinted, so what's under the water fades into it with distance, and the
    /// sun is tinted and dimmed, as light coming down through the water is, so even what's near takes on its colour;
    /// out of the water, the scene's own fog and sun come back as they were. It knows only its camera's position and the water, nothing
    /// of what the camera follows (scene set-up puts it on the camera of a scene with water).
    ///
    /// It uses the scene's fog and sun (RenderSettings), which every material lit by the render pipeline, the water's
    /// own included, fades by and is lit by, so it needs nothing of the render pipeline. For more (a post-processing volume's colour
    /// grading, muffled sound), CameraSubmerged and CameraSurfaced say when the camera goes under and comes up.
    /// </summary>
    [DisallowMultipleComponent]
    public class UnderwaterView : MonoBehaviour
    {
        [Tooltip("The fog's colour under the water: what the water fades to with distance")]
        [SerializeField] private Color fogColour = new Color(0.05f, 0.24f, 0.31f);
        [Tooltip("How dense the fog is under the water (exponential): higher, it's murkier, and things fade out nearer")]
        [Min(0f)] [SerializeField] private float fogDensity = 0.3f;
        [Tooltip("What the sun's colour is multiplied by under the water (the scene's sun: RenderSettings.sun)")]
        [SerializeField] private Color lightTint = new Color(0.45f, 0.75f, 0.85f);
        [Tooltip("The share of the sun's intensity that reaches under the water")]
        [Range(0f, 1f)] [SerializeField] private float lightIntensity = 0.6f;
        [Tooltip("How far (m) above a water's surface the camera's position may be and still count as under it: the camera " +
                 "sees from its near plane, a little ahead of its position, so the look changes just as the view goes under")]
        [Min(0f)] [SerializeField] private float surfaceMargin = 0.05f;

        /// <summary>Raised when the camera goes under a water's surface, with that water.</summary>
        public event Action<Water> CameraSubmerged;
        /// <summary>Raised when the camera comes back above the water it was under, with that water.</summary>
        public event Action<Water> CameraSurfaced;

        /// <summary>The fog's colour under the water.</summary>
        public Color FogColour { get => fogColour; set { fogColour = value; if (IsUnderwater) { ApplyUnderwaterFog(); } } }
        /// <summary>How dense the fog is under the water (exponential).</summary>
        public float FogDensity { get => fogDensity; set { fogDensity = Mathf.Max(0f, value); if (IsUnderwater) { ApplyUnderwaterFog(); } } }
        /// <summary>Whether the camera is under a water's surface, as of the last check.</summary>
        public bool IsUnderwater => water != null;
        /// <summary>The water the camera is under, as of the last check, or null.</summary>
        public Water Water => water;

        private Water water;
        // The scene's own fog, kept while the camera's under the water, to put back when it comes up.
        private bool savedFog;
        private FogMode savedFogMode;
        private Color savedFogColour;
        private float savedFogDensity;
        // The sun as it was, tinted while the camera's under the water.
        private Light savedSun;
        private Color savedSunColour;
        private float savedSunIntensity;

        // After the camera has moved for the frame (cameras follow in LateUpdate; this checks after them, or a frame late).
        private void LateUpdate() => Check();

        private void OnDisable()
        {
            if (water == null) { return; }
            Water was = water;
            water = null;
            RestoreFog();
            CameraSurfaced?.Invoke(was);
        }

        /// <summary>
        /// Checks the camera against the water, now, changing the fog and raising CameraSubmerged or CameraSurfaced if
        /// it's gone under or come up since the last check. Called every frame (LateUpdate).
        /// </summary>
        public void Check()
        {
            Water now = Water.At(transform.position + Vector3.down * surfaceMargin);
            if (now == water) { return; }
            Water was = water;
            water = now;
            if (was == null) { SaveFog(); }
            if (now != null) { ApplyUnderwaterFog(); } else { RestoreFog(); }
            if (was != null) { CameraSurfaced?.Invoke(was); }
            if (now != null) { CameraSubmerged?.Invoke(now); }
        }

        private void SaveFog()
        {
            savedFog = RenderSettings.fog;
            savedFogMode = RenderSettings.fogMode;
            savedFogColour = RenderSettings.fogColor;
            savedFogDensity = RenderSettings.fogDensity;
            savedSun = RenderSettings.sun;
            if (savedSun != null)
            {
                savedSunColour = savedSun.color;
                savedSunIntensity = savedSun.intensity;
            }
        }

        private void ApplyUnderwaterFog()
        {
            RenderSettings.fog = true;
            // Exponential, as the levels' haze is: a build keeps only the fog modes its scenes use (see Graphics settings'
            // Fog Modes), so another mode could be missing from the shaders there.
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = fogColour;
            RenderSettings.fogDensity = fogDensity;
            if (savedSun != null)
            {
                savedSun.color = savedSunColour * lightTint;
                savedSun.intensity = savedSunIntensity * lightIntensity;
            }
        }

        private void RestoreFog()
        {
            RenderSettings.fog = savedFog;
            RenderSettings.fogMode = savedFogMode;
            RenderSettings.fogColor = savedFogColour;
            RenderSettings.fogDensity = savedFogDensity;
            if (savedSun != null)
            {
                savedSun.color = savedSunColour;
                savedSun.intensity = savedSunIntensity;
                savedSun = null;
            }
        }
    }
}
