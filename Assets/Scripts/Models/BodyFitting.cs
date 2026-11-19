using UnityEngine;

namespace MeridianMotion.Characters
{
    /// <summary>
    /// Fitting a character's body (see BodyShape) to a model, from its measurements in its bind pose: as Prepare Character
    /// Models fits a model's Body Definition, and as a character fits its body to a model whose source has none, as it
    /// loads (see CharacterModelSlot's Body Fit). A humanoid gets an upright capsule as tall as it is and as wide as its
    /// shoulders (times a margin for its arms), lying as thick as it is deep; a generic-rig character prepared by the Generic-rig character Model
    /// Importer, capsules along its torso and a sphere for its head (Generic-rig character); anything else, a box its size.
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

        /// <summary>
        /// A generic-rig character's body (one on legs, not a humanoid: see the Generic-rig character Model Importer), as parts of one body (see
        /// BodyDefinition's Extra Parts): capsules lying along its torso, front to back, as long as it is, and its head a
        /// sphere, so it's rounded, with no corners to catch on things. Its torso is the bounds (m, in the model's space,
        /// standing in its bind pose) of the mesh its spine carries, its head of the mesh its head carries (hasHead false:
        /// none). The torso's capsules reach from lift (m) above its feet up to its back, whatever its belly's height: the
        /// legs under it are held off the ground as the body hovers over steps (see Hover Over Steps), so lift is the
        /// character's step height. They're as round as the torso is wide, stacked to fill that height (or, flatter than
        /// it's wide, side by side across it). The head is as round
        /// as it is wide, resting on its underside (ears and crests above it aren't the body's), and no lower than lift
        /// either. The first part is the body's Shape, the rest its Extra Parts.
        /// </summary>
        public static BodyShape[] GenericRig(Bounds torso, Bounds head, bool hasHead, float lift)
        {
            var parts = new System.Collections.Generic.List<BodyShape>();
            // From a step above its feet, whatever its belly's height: anything taller than a step under it must meet the
            // body (its legs' hitboxes don't stop it), as anything a step high or lower is stepped over.
            float bottom = lift;
            float top = Mathf.Max(torso.max.y, bottom + MinimumPartThickness);
            float deep = top - bottom, wide = Mathf.Max(torso.size.x, MinimumPartThickness);
            float length = torso.size.z, ahead = torso.center.z;
            if (deep >= wide)
            {
                // Stacked, as many as fill its depth, the top one's top at its back.
                float radius = 0.5f * wide;
                int count = PartsToFill(deep / wide);
                for (int i = 0; i < count; i++)
                {
                    float y = count == 1 ? 0.5f * (bottom + top) : Mathf.Lerp(bottom + radius, top - radius, i / (count - 1f));
                    parts.Add(LyingCapsule(radius, length, new Vector3(0f, y, ahead)));
                }
            }
            else
            {
                // Side by side, as many as fill its width.
                float radius = 0.5f * deep;
                int count = PartsToFill(wide / deep);
                for (int i = 0; i < count; i++)
                {
                    float x = count == 1 ? 0f : Mathf.Lerp(-0.5f * wide + radius, 0.5f * wide - radius, i / (count - 1f));
                    parts.Add(LyingCapsule(radius, length, new Vector3(x, bottom + radius, ahead)));
                }
            }
            if (hasHead)
            {
                float radius = 0.5f * Mathf.Max(Mathf.Min(head.size.x, head.size.z), MinimumPartThickness);
                BodyShape sphere = BodyShape.Sphere(radius);
                sphere.center = new Vector3(0f, Mathf.Max(head.min.y, lift) + radius, head.center.z);
                parts.Add(sphere);
            }
            return parts.ToArray();
        }

        // How many round parts fill a torso that many times longer one way across than the other: another only once it
        // would leave more than TorsoCoverSlack of a part's width uncovered (one more for a sliver is a part on a part).
        private static int PartsToFill(float ratio) => Mathf.Max(1, Mathf.CeilToInt(ratio - TorsoCoverSlack));

        /// <summary>The share of a part's width a generic-rig character's torso may stick out of its parts before another is added.</summary>
        public const float TorsoCoverSlack = 0.2f;

        /// <summary>The thinnest (m) a generic-rig character's body or head is made, however thin its mesh measures.</summary>
        public const float MinimumPartThickness = 0.05f;

        // A capsule lying along the body (front to back), its middle at center: as long as length, or as round as it is if
        // shorter.
        private static BodyShape LyingCapsule(float radius, float length, Vector3 center) => new BodyShape
        {
            kind = BodyShapeKind.Capsule, direction = 2, radius = radius, height = Mathf.Max(length, 2f * radius), center = center,
        };

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
