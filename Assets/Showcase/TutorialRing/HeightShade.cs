using UnityEngine;

namespace MeridianMotion.Showcase.TutorialRing
{
    /// <summary>
    /// Turns on the showcase's height shading for its scene: what's low in the world is drawn darker, down to Darkest at
    /// Bottom, and nothing's tinted from Top up (a full-screen pass on the project's renderers, the Height Shade
    /// material: see TutorialRingAssets). Off in any scene without one, so the others look as they did. Works in the
    /// editor's views too.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("Meridian Motion/Showcase/Height Shade")]
    public class HeightShade : MonoBehaviour
    {
        private static readonly int HeightShadeId = Shader.PropertyToID("_HeightShade");

        [Tooltip("The world height (m) things are darkest at (the ground)")]
        [SerializeField] private float bottom;
        [Tooltip("The world height (m) from which nothing's tinted (the tops of the walls)")]
        [SerializeField] private float top = 27f;
        [Tooltip("How bright things are at Bottom, as a share of their own colour (1: no shading)")]
        [Range(0f, 1f)] [SerializeField] private float darkest = 0.5f;

        /// <summary>The world height things are darkest at.</summary>
        public float Bottom { get => bottom; set { bottom = value; Apply(); } }
        /// <summary>The world height from which nothing's tinted.</summary>
        public float Top { get => top; set { top = value; Apply(); } }
        /// <summary>How bright things are at Bottom (1: no shading).</summary>
        public float Darkest { get => darkest; set { darkest = Mathf.Clamp01(value); Apply(); } }

        /// <summary>The shading the pass applies now: (bottom, top, darkest, on).</summary>
        public static Vector4 Current => Shader.GetGlobalVector(HeightShadeId);

        private void OnEnable() => Apply();
        private void OnValidate() { if (isActiveAndEnabled) { Apply(); } }
        private void OnDisable() => Shader.SetGlobalVector(HeightShadeId, Vector4.zero);

        private void Apply() => Shader.SetGlobalVector(HeightShadeId, new Vector4(bottom, top, darkest, 1f));
    }
}
