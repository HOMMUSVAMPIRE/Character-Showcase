using UnityEngine;

namespace Character
{
    /// <summary>
    /// Fitting a character's body (see BodyShape) to a model, from its measurements in its bind pose: as Prepare Character
    /// Models fits a model's Body Definition, and as a character fits its body to a model whose source has none, as it
    /// loads (see CharacterModelHost's Collider Fit). A humanoid gets an upright capsule as tall as it is and as wide as its
    /// shoulders (times a margin for its arms), lying as thick as it is deep; anything else, a box its size.
    /// </summary>
    public static class BodyFitting
    {
        /// <summary>A humanoid body's width as a multiple of its shoulder width (between its upper arm joints), leaving room for its arms.</summary>
        public const float DefaultShoulderMargin = 1.4f;

        /// <summary>
        /// A humanoid's body: an upright capsule standing on the feet, height tall, as wide as its shoulders (shoulderWidth,
        /// between its upper arm joints) times margin (no more than half its height), and lying no thicker than depth (its
        /// size front to back, standing; 0 or more than its width: as thick as it's wide).
        /// </summary>
        public static BodyShape Humanoid(float height, float shoulderWidth, float margin, float depth)
        {
            float radius = 0.5f * shoulderWidth * margin;
            return BodyShape.Capsule(radius, height, depth > 0f && depth < 2f * radius ? depth : 0f);
        }

        /// <summary>
        /// A body for a model of the given size (m: across, up, front to back), standing on the feet and centred over them:
        /// a box its size (lying, as thick as it's deep).
        /// </summary>
        public static BodyShape Box(Vector3 size) => BodyShape.Box(size);

        /// <summary>
        /// A body for a model: a humanoid's (see Humanoid) if it has shoulders to measure (shoulderWidth more than 0),
        /// otherwise a box (see Box). Size is the model's (m, standing in its bind pose: across, up, front to back).
        /// </summary>
        public static BodyShape ForModel(Vector3 size, float shoulderWidth, float margin = DefaultShoulderMargin) =>
            shoulderWidth > 0f ? Humanoid(size.y, shoulderWidth, margin, Mathf.Min(size.x, size.z)) : Box(size);

        /// <summary>The body scaled, every way alike, to stand height (m) tall (its proportions kept): 'own' if it has no height.</summary>
        public static BodyShape ScaledToHeight(BodyShape own, float height)
        {
            float ownHeight = StandingHeightOf(own);
            return ownHeight > 0f && height > 0f ? own.Scaled(height / ownHeight) : own;
        }

        /// <summary>How tall (m) a body stands, from the feet (its origin) to its top.</summary>
        public static float StandingHeightOf(BodyShape shape)
        {
            float half = shape.kind switch
            {
                BodyShapeKind.Box => 0.5f * shape.size.y,
                BodyShapeKind.Capsule when shape.direction == 1 => 0.5f * Mathf.Max(shape.height, 2f * shape.radius),
                _ => shape.radius,
            };
            return shape.center.y + half;
        }
    }
}
