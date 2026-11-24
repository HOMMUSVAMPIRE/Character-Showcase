using UnityEngine;

namespace MeridianMotion.Showcase.Grass
{
    /// <summary>
    /// How a level's grass looks and how much of it there is: its blades' size, colour and spacing, and how far it's drawn.
    /// A GrassField reads it every frame, so a change shows at once, in the editor and in play mode. Without one, a field
    /// uses these defaults (a short lawn).
    /// </summary>
    [CreateAssetMenu(menuName = "Meridian Motion/Showcase/Grass Style", fileName = "Grass Style")]
    public sealed class GrassStyle : ScriptableObject
    {
        [Header("Blades")]
        [Tooltip("How long the blades are (m), each picking a length in this range. From a short lawn to tall wild grass.")]
        public Vector2 length = new(0.12f, 0.26f);
        [Tooltip("How wide the blades are at the root (m), each picking a width in this range. They taper to a point.")]
        public Vector2 width = new(0.025f, 0.04f);
        [Tooltip("How far the blades bend over, each picking a bend in this range: 0 stands straight up, 1 leans its tip about its length forward.")]
        public Vector2 bend = new(0.15f, 0.55f);
        [Tooltip("Blades are placed on a grid this far apart (m), each nudged at random within its cell, where the lawn's mask says there's grass. Smaller is denser: the count goes as one over its square.")]
        [Min(0.02f)] public float spacing = 0.08f;

        [Header("Colour")]
        [Tooltip("The blades' colour at the root, where they meet the lawn: close to the lawn's own, so the two read as one.")]
        public Color rootColour = new(0.24f, 0.38f, 0.13f);
        [Tooltip("The blades' colour at the tip.")]
        public Color tipColour = new(0.55f, 0.72f, 0.28f);
        [Tooltip("How much the blades' colour varies from one to the next (0: none).")]
        [Range(0f, 1f)] public float colourVariation = 0.25f;

        [Header("Distance")]
        [Tooltip("How far from the camera grass is drawn (m). It thins out and shrinks toward this, so it doesn't pop; the lawn under it is drawn beyond.")]
        [Min(1f)] public float drawDistance = 60f;
        [Tooltip("Blades nearer than this (m) are drawn in full, curved; those beyond as one triangle each, which can't be told apart at that distance.")]
        [Min(0f)] public float detailDistance = 14f;
        [Tooltip("Grass starts thinning out this far from the camera (m), the blades left widened to cover.")]
        [Min(0f)] public float thinFrom = 8f;
        [Tooltip("What fraction of the blades are left at the draw distance.")]
        [Range(0.02f, 1f)] public float thinTo = 0.12f;
        [Tooltip("How many segments a blade nearer than Detail Distance is drawn with: more curve smoothly.")]
        [Range(1, 8)] public int segments = 4;
    }
}
