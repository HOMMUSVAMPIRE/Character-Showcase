using UnityEngine;

namespace MeridianMotion.UI
{
    /// <summary>
    /// Where a minimap's picture comes from (MinimapView): its texture, and the part of the world it covers, on the ground
    /// (x and z, metres: its texture's left edge at WorldBounds.xMin, its bottom edge at yMin, which is world z). What makes
    /// the picture (a top-down render, a baked image of the level) is the game's.
    /// </summary>
    public interface IMinimapSource
    {
        /// <summary>The map's picture, north (world +z) up.</summary>
        Texture Map { get; }

        /// <summary>The ground the picture covers: x is world x, y is world z.</summary>
        Rect WorldBounds { get; }
    }

    /// <summary>A minimap source that's simply a texture and the ground it covers (a baked image of a level, set in the inspector).</summary>
    [System.Serializable]
    public class TextureMinimapSource : IMinimapSource
    {
        [Tooltip("The map's picture, north (world +z) up")]
        public Texture texture;
        [Tooltip("The ground the picture covers (x: world x, y: world z, metres)")]
        public Rect worldBounds = new(-50f, -50f, 100f, 100f);

        public Texture Map => texture;
        public Rect WorldBounds => worldBounds;
    }
}
