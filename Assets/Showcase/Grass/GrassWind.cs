using UnityEngine;

namespace MeridianMotion.Showcase.Grass
{
    /// <summary>
    /// The wind the level's grass sways in: a direction and strength, gusts (patches of stronger wind blowing across
    /// the lawns) and a flutter of each blade's own. One per level (the level builders add it beside the first lawns);
    /// without one, the grass sways in Defaults. How much a lawn's grass moves in it is its style's Sway. It's only
    /// seen: nothing reads it but the grass and the flora (whose shaders it gives it, as global values), and the
    /// movement never does.
    /// </summary>
    [ExecuteAlways]
    [AddComponentMenu("Meridian Motion/Showcase/Grass Wind")]
    public sealed class GrassWind : MonoBehaviour
    {
        [Tooltip("Which way the wind blows, as a compass bearing (degrees: 0 toward +z, 90 toward +x).")]
        [Range(0f, 360f)] public float direction = 60f;
        [Tooltip("How hard the wind blows: how far it pushes a blade's tip, in its lengths, between gusts (0: still).")]
        [Range(0f, 1f)] public float strength = 0.12f;
        [Tooltip("How wide the gusts are (m): patches of stronger wind that blow across the lawns.")]
        [Min(0.5f)] public float gustSize = 7f;
        [Tooltip("How fast the gusts blow across the lawns (m a second).")]
        [Min(0f)] public float gustSpeed = 3.5f;
        [Tooltip("How much harder the wind pushes in a gust (0: no gusts, 1: about twice as hard as between them).")]
        [Range(0f, 2f)] public float gustStrength = 0.9f;
        [Tooltip("How much each blade flutters on its own, back and forth (in its lengths).")]
        [Range(0f, 0.3f)] public float flutter = 0.04f;
        [Tooltip("How fast the blades flutter (radians a second).")]
        [Min(0f)] public float flutterSpeed = 5f;

        /// <summary>The wind the grass is in: the last one enabled, or null (then Defaults).</summary>
        public static GrassWind Active { get; private set; }

        /// <summary>The wind without a GrassWind in the scene: the component's defaults.</summary>
        public static GrassWind Defaults
        {
            get
            {
                if (defaults == null)
                {
                    var holder = new GameObject("Grass Wind (defaults)") { hideFlags = HideFlags.HideAndDontSave };
                    holder.SetActive(false);
                    defaults = holder.AddComponent<GrassWind>();
                }
                return defaults;
            }
        }

        private static GrassWind defaults;

        /// <summary>The wind's direction as a unit vector in the world's x and z.</summary>
        public Vector2 Direction => new(Mathf.Sin(direction * Mathf.Deg2Rad), Mathf.Cos(direction * Mathf.Deg2Rad));

        /// <summary>The time the wind blows by (s): the game's (so it stops when the game's paused), or the editor's out of play mode.</summary>
        public static float Time => Application.isPlaying ? UnityEngine.Time.time : (float)UnityEngine.Time.realtimeSinceStartupAsDouble;

        private void OnEnable() => Active = this;

        private static readonly int FloraWind = Shader.PropertyToID("_FloraWind"), FloraWindGusts = Shader.PropertyToID("_FloraWindGusts");
        private static readonly int FloraWindFlutter = Shader.PropertyToID("_FloraWindFlutter");

        /// <summary>
        /// Publishes the wind for everything else that sways in it (the flora's shaders, FloraCommon.hlsl), as global
        /// shader values, each frame: the grass reads it directly (GrassField).
        /// </summary>
        private void Update()
        {
            if (Active != this) { return; }
            Vector2 blowing = Direction;
            Shader.SetGlobalVector(FloraWind, new Vector4(blowing.x, blowing.y, strength, Time));
            Shader.SetGlobalVector(FloraWindGusts, new Vector4(gustSize, gustSpeed, gustStrength, flutter));
            Shader.SetGlobalVector(FloraWindFlutter, new Vector4(flutterSpeed, 0f, 0f, 0f));
        }

        private void OnDisable()
        {
            if (Active == this) { Active = null; }
        }
    }
}
