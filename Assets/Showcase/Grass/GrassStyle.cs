using UnityEngine;

namespace MeridianMotion.Showcase.Grass
{
    /// <summary>
    /// How a level's grass looks and how much of it there is: its blades' length, shape, colour and spacing, its clumps,
    /// and how far it's drawn. From a short lawn to tall wild grass: the field reads its style every frame, so a change
    /// shows at once, in the editor and in play mode (and the lawns under it follow). Without one, a field uses these
    /// defaults (a short lawn). Two to start from are in Assets/Showcase/Grass/Styles (Create Grass Assets makes them).
    /// </summary>
    [CreateAssetMenu(menuName = "Meridian Motion/Showcase/Grass Style", fileName = "Grass Style")]
    public sealed class GrassStyle : ScriptableObject
    {
        [Header("Blades")]
        [Tooltip("How long the blades are (m), each picking a length in this range. From a short lawn to tall wild grass.")]
        public Vector2 length = new(0.1f, 0.22f);
        [Tooltip("How wide the blades are at the root (m), each picking a width in this range. They taper to a point.")]
        public Vector2 width = new(0.025f, 0.04f);
        [Tooltip("How far the blades bend over, each picking a bend in this range: 0 stands straight up, 1 leans its tip about its length forward.")]
        public Vector2 bend = new(0.15f, 0.5f);
        [Tooltip("How much long blades droop under their own weight, their tips falling and reaching further (0: none). Short blades hardly droop at all.")]
        [Range(0f, 1f)] public float droop = 0.1f;
        [Tooltip("Blades are placed on a grid this far apart (m), each nudged at random within its cell, where the lawn's mask says there's grass. Smaller is denser: the count goes as one over its square.")]
        [Min(0.02f)] public float spacing = 0.08f;
        [Tooltip("How much longer the grass grows at a lawn's edge, against walls and paving, than in its middle (1: no longer).")]
        [Min(0.1f)] public float edgeLength = 1.4f;

        [Header("Motion")]
        [Tooltip("How much this grass moves in the wind (the level's Grass Wind): 0 not at all, 1 as the wind says. The wind pushes a blade's tip by some of its length, so long grass sways further.")]
        [Range(0f, 3f)] public float sway = 1f;

        [Header("Clumps")]
        [Tooltip("Blades grow in clumps about this wide (m), which differ in length and tint, and whose blades lean out from their middle.")]
        [Min(0.05f)] public float clumpSize = 0.6f;
        [Tooltip("How much a clump's blades are longer or shorter than the rest, together (0: not at all, 1: from nothing to twice).")]
        [Range(0f, 1f)] public float clumpLength = 0.25f;
        [Tooltip("How much a clump's blades lean out from its middle, rather than each its own way (0: their own way, 1: all outward).")]
        [Range(0f, 1f)] public float clumpLean = 0.2f;

        [Header("Colour")]
        [Tooltip("The blades' colour at the root, where they meet the lawn. The lawn under near grass is this, shaded.")]
        public Color rootColour = new(0.21f, 0.34f, 0.11f);
        [Tooltip("The blades' colour at the tip.")]
        public Color tipColour = new(0.5f, 0.66f, 0.26f);
        [Tooltip("How much the blades' colour varies from one to the next (0: none).")]
        [Range(0f, 1f)] public float colourVariation = 0.25f;
        [Tooltip("How much clumps differ in tint, from a warm, dry green to a cool, lush one (0: none). The lawn under them is tinted the same.")]
        [Range(0f, 1f)] public float clumpTint = 0.3f;
        [Tooltip("How dark the blades are at the root, where they shade each other (1: no darker than their colour).")]
        [Range(0f, 1f)] public float rootShade = 0.75f;
        [Tooltip("How dark the lawn is under near grass, where it's seen between the blades in their shade (1: the root colour).")]
        [Range(0f, 1f)] public float groundShade = 0.7f;
        [Tooltip("How much sunlight shows through the blades when the camera looks toward the sun (0: none).")]
        [Range(0f, 1f)] public float translucency = 0.15f;
        [Tooltip("Whether the blades cast shadows: worth trying for tall grass, and measuring (Measure Grass). Only blades in the camera's view cast them.")]
        public bool castsShadows;

        [Header("Distance and detail")]
        [Tooltip("How far from the camera grass is drawn (m). It thins out and shrinks toward this, so it doesn't pop; the lawn under it is drawn beyond, in the grass's colour.")]
        [Min(1f)] public float drawDistance = 60f;
        [Tooltip("Blades nearer than this (m) are drawn curved, in segments; those beyond as one triangle each, which can't be told apart at that distance.")]
        [Min(0f)] public float detailDistance = 14f;
        [Tooltip("Grass starts thinning out this far from the camera (m), the blades left widened to cover.")]
        [Min(0f)] public float thinFrom = 8f;
        [Tooltip("What fraction of the blades are left at the draw distance.")]
        [Range(0.02f, 1f)] public float thinTo = 0.12f;
        [Tooltip("A near blade has a segment for every this much of its length (m), so long blades curve smoothly and short ones cost little.")]
        [Min(0.01f)] public float segmentLength = 0.06f;
        [Tooltip("The most segments a near blade has, however long.")]
        [Range(1, 10)] public int segments = 4;

        /// <summary>The longest a blade can be (m): its longest length, made longer by its clump and at a lawn's edge.</summary>
        public float Tallest => length.y * (1f + clumpLength) * Mathf.Max(1f, edgeLength);
    }
}
