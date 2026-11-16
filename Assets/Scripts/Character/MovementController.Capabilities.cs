using UnityEngine;

namespace Character
{
    // Capabilities: what the character can do, measured with its own movement (see MovementSimulation).
    public partial class MovementController
    {
        private MovementCapabilities capabilities;
        private string capabilitiesKey;

        /// <summary>
        /// What the character can do as it's tuned and sized now: how far it jumps, how high it climbs, how far it slides,
        /// rolls and wall-runs, what it steps up and fits under (see MovementCapabilities). Measured by running a copy of
        /// the character's own movement in a world of its own (MovementSimulation), so it can't drift from what the
        /// movement does; worked out the first time it's asked for, and again after the movement mode's settings, the
        /// character's size or its collider change. Measuring takes a moment (a few hundred physics steps of movement,
        /// without rendering), so ask for it when it's needed rather than every frame.
        /// </summary>
        public MovementCapabilities Capabilities
        {
            get
            {
                string key = CapabilitiesKey();
                if (capabilities == null || key != capabilitiesKey)
                {
                    capabilities = MovementSimulation.Measure(this);
                    capabilitiesKey = key;
                }
                return capabilities;
            }
        }

        /// <summary>Everything the capabilities depend on, to know when they need measuring again.</summary>
        private string CapabilitiesKey()
        {
            RememberBaseCapsule();
            string mode = movementHandler != null ? JsonUtility.ToJson(movementHandler) : "";
            return $"{mode}|{Scale}|{baseRadius}|{baseHeight}|{baseCenter}|{sizeScaling}|{maxStepHeight}|{maxSlopeAngle}|{skinWidth}|" +
                   $"{groundSnapDistance}|{groundCheckRadius}|{movementSpeedMultiplier}|{jumpStrengthModifier}";
        }

        /// <summary>
        /// Makes this controller (on a bare object with the given Rigidbody and capsule, not yet active) a copy of source
        /// for simulating its movement: the same settings, movement mode, collider (as authored, and at the same size; or,
        /// given an authored radius and height, that collider standing on its feet), but none of its state. See
        /// MovementSimulation.
        /// </summary>
        internal void CopyForSimulation(MovementController source, Rigidbody body, CapsuleCollider collider, MovementMode mode,
            float? authoredRadius = null, float? authoredHeight = null)
        {
            source.RememberBaseCapsule();
            rb = body;
            capsule = collider;
            movementHandler = mode;
            movementSpeedMultiplier = source.movementSpeedMultiplier;
            jumpStrengthModifier = source.jumpStrengthModifier;
            groundCheckRadius = source.groundCheckRadius;
            movementLayerMask = source.movementLayerMask;
            maxStepHeight = source.maxStepHeight;
            maxSlopeAngle = source.maxSlopeAngle;
            skinWidth = source.skinWidth;
            groundSnapDistance = source.groundSnapDistance;
            sizeScaling = source.sizeScaling;
            moveWithGround = source.moveWithGround;
            rotateWithGround = source.rotateWithGround;
            inheritGroundVelocity = source.inheritGroundVelocity;
            maxGroundCarryPerStep = source.maxGroundCarryPerStep;
            baseRadius = source.baseRadius;
            baseHeight = source.baseHeight;
            baseCenter = source.baseCenter;
            if (authoredRadius.HasValue && authoredHeight.HasValue && authoredHeight.Value > 0f)
            {
                baseHeight = authoredHeight.Value;
                baseRadius = Mathf.Min(authoredRadius.Value, baseHeight * 0.5f);
                baseCenter = new Vector3(baseCenter.x, baseHeight * 0.5f, baseCenter.z);
            }
            hasBaseCapsule = true;
            heightFraction = 1f;
            ApplyScale(source.Scale);
        }
    }
}
