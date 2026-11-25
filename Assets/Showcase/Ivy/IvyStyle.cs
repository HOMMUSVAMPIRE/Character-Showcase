using UnityEngine;

namespace MeridianMotion.Showcase.Ivy
{
    /// <summary>
    /// How an IvyField's ivy looks and how much of it there is (ReferenceAssets/OpenTasks.md, Q5): its leaves' size, shape
    /// and spacing, their colours and lighting, their flutter, the distances they're drawn and thinned to, and how far the
    /// stems are drawn. Read every frame, so a change shows at once. Where the ivy grows (its masks) is the level's, made
    /// as it's built (IvyGrowth, with the level builders' foliage settings): changing those needs a rebuild.
    /// </summary>
    [CreateAssetMenu(menuName = "Meridian Motion/Showcase/Ivy Style", fileName = "Ivy")]
    public sealed class IvyStyle : ScriptableObject
    {
        [Header("Leaves")]
        [Tooltip("How far apart (m) the cells a leaf can grow on are: closer, for denser ivy (their number goes as one over this squared, and so does what they cost).")]
        [Min(0.02f)] public float spacing = 0.045f;
        [Tooltip("A leaf's length (m): each picked between these.")]
        public Vector2 leafSize = new(0.08f, 0.15f);
        [Tooltip("How far (m) leaves stand out from their wall, on their stalks (some deeper, some further; further over a stem).")]
        [Min(0f)] public float standOff = 0.04f;
        [Tooltip("The most a leaf's tip tilts out from the wall (degrees).")]
        [Range(0f, 80f)] public float tilt = 50f;
        [Tooltip("How far a leaf turns from hanging straight down, either way (degrees).")]
        [Range(0f, 180f)] public float spread = 100f;
        [Tooltip("How far a near leaf's sides fold back along its midrib (a share of its length).")]
        [Range(0f, 0.5f)] public float fold = 0.18f;

        [Header("Colour and light")]
        [Tooltip("The colour of the darkest leaves (shaded, deep in the growth, darker still).")]
        public Color darkColour = new(0.13f, 0.25f, 0.07f);
        [Tooltip("The colour of the lightest leaves.")]
        public Color lightColour = new(0.36f, 0.52f, 0.17f);
        [Tooltip("How much each leaf's colour varies between the two (0: by the broad patches over the wall only).")]
        [Range(0f, 1f)] public float colourVariation = 0.5f;
        [Tooltip("How far the leaves' greens are taken toward the level's grass palette (the lawns' Root and Tip Colour), where the level has grass.")]
        [Range(0f, 1f)] public float paletteBlend = 0.4f;
        [Tooltip("The brightness at which a leaf is given the palette's tip colour (lighter leaves past it, darker toward the root colour).")]
        [Range(0.05f, 1f)] public float leafLuma = 0.3f;
        [Tooltip("How much light wraps round a leaf (thin leaves are lit a little from behind).")]
        [Range(0f, 1f)] public float wrap = 0.3f;
        [Tooltip("How much sunlight comes through a leaf seen against the sun.")]
        [Range(0f, 1f)] public float translucency = 0.2f;
        [Tooltip("How much sun a leaf in shadow still gets (0: none): the levels' shadows are coarse for something this fine.")]
        [Range(0f, 1f)] public float shadowFloor = 0.35f;
        [Tooltip("How glossy the leaves are: ivy's are waxy.")]
        [Range(0f, 1f)] public float gloss = 0.25f;

        [Header("Wind")]
        [Tooltip("How much a leaf's tip flutters in the level's wind (the grass's Grass Wind).")]
        [Range(0f, 2f)] public float flutter = 0.5f;

        [Header("Vines")]
        [Tooltip("How much further out from the wall a vine's leaves stand than ivy's (times Stand Off): they dangle from their strands rather than cling.")]
        [Range(1f, 4f)] public float vineStandOut = 1.8f;
        [Tooltip("How much more a vine's leaves flutter than ivy's at the strand's tip (times Flutter); less toward its top.")]
        [Range(1f, 5f)] public float vineFlutter = 2.5f;
        [Tooltip("How much smaller a vine's leaves get toward its tip (0: not at all, 0.6: 40% of their size there).")]
        [Range(0f, 0.8f)] public float vineTipShrink = 0.45f;

        [Header("Distances")]
        [Tooltip("How far (m) leaves are drawn; beyond, the wall carries the ivy's colour where it grows (the far layer).")]
        [Min(1f)] public float drawDistance = 90f;
        [Tooltip("How far (m) leaves are drawn in full (an ivy leaf's outline); beyond, a flat diamond each.")]
        [Min(0f)] public float detailDistance = 20f;
        [Tooltip("How far (m) from the camera the leaves start thinning out, as the square of the distance.")]
        [Min(0.1f)] public float thinFrom = 8f;
        [Tooltip("The least share of the leaves kept, however far (those kept are larger, to cover).")]
        [Range(0.005f, 1f)] public float thinTo = 0.08f;
        [Tooltip("How far (m) the stems are drawn: further off, they're under the leaves or too thin to see.")]
        [Min(0f)] public float stemDistance = 25f;
        [Tooltip("Over how much of the draw distance, at its end, the far layer fades in under the thinning leaves (a share of it).")]
        [Range(0.05f, 1f)] public float farFade = 0.35f;

        [Header("Shadows and quality")]
        [Tooltip("Whether the leaves cast shadows onto their wall. Off by default: with them, the ivy cost up to about three times as much (ReferenceAssets/OpenTasks.md, Q1).")]
        public bool castsShadows;
        [Tooltip("How much further apart the leaves' cells are at each quality level (Project Settings > Quality, in order): fewer leaves where the GPU is slower.")]
        public float[] spacingByQuality = { 1.5f, 1f };

        /// <summary>The spacing at the quality level in use (Spacing, times the level's Spacing By Quality).</summary>
        public float SpacingNow
        {
            get
            {
                int level = QualitySettings.GetQualityLevel();
                float scale = spacingByQuality != null && level < spacingByQuality.Length ? Mathf.Max(0.1f, spacingByQuality[level]) : 1f;
                return spacing * scale;
            }
        }
    }
}
