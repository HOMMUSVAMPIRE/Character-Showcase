using UnityEngine;

namespace CameraSystem
{
    /// <summary>
    /// Trauma-based camera shake: callers add trauma (0-1), which decays over time. Shake intensity is
    /// trauma^exponent, sampled from smooth Perlin noise so it reads as a rumble rather than random jitter.
    /// </summary>
    public class CameraShake
    {
        // Chosen lazily: this object is created by CameraRig's field initializers, where Unity forbids Random calls.
        private float seed = -1f;
        private float time;

        public float Trauma { get; private set; }

        public void AddTrauma(float amount) => Trauma = Mathf.Clamp01(Trauma + Mathf.Max(0f, amount));

        public void Clear() => Trauma = 0f;

        /// <summary>Advances the shake and returns the positional (camera-local) and rotational offsets for this frame.</summary>
        public void Evaluate(CameraShakeSettings settings, float deltaTime, out Vector3 localOffset, out Quaternion rotationOffset)
        {
            localOffset = Vector3.zero;
            rotationOffset = Quaternion.identity;

            if (!settings.enabled) { Trauma = 0f; return; }
            if (Trauma <= 0f) { return; }

            if (seed < 0f) { seed = Random.value * 1000f; }
            time += deltaTime * settings.frequency;
            float intensity = Mathf.Pow(Trauma, settings.traumaExponent);

            localOffset = new Vector3(Noise(0), Noise(1), Noise(2)) * (settings.maxOffset * intensity);
            Vector3 angles = Vector3.Scale(new Vector3(Noise(3), Noise(4), Noise(5)), settings.maxAngles) * intensity;
            rotationOffset = Quaternion.Euler(angles);

            Trauma = Mathf.Max(0f, Trauma - settings.decayPerSecond * deltaTime);
        }

        // Perlin noise remapped to [-1, 1], with a separate channel per axis.
        private float Noise(int channel) => Mathf.PerlinNoise(seed + channel * 17.31f, time) * 2f - 1f;
    }
}
