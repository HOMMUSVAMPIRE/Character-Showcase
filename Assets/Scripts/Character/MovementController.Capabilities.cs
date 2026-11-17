using System.Collections.Generic;
using System.Globalization;
using Unity.Profiling;
using UnityEngine;

namespace Character
{
    // Capabilities: what the character can do, measured with its own movement (see MovementSimulation).
    public partial class MovementController
    {
        private static readonly ProfilerMarker MeasureMarker = new("Movement.MeasureCapabilities");
        private MovementCapabilities capabilities;
        private string capabilitiesKey;

        // Capabilities already measured, by everything they depend on (CapabilitiesKey), shared by every character: one
        // with the same tuning, size and collider as one measured before (another character, or this one back as it
        // was) reuses its measurement. Measurements are deterministic, so they're the same however often they're made,
        // and read-only, so they can be shared. Forgotten once it holds MaxSharedCapabilities, to stay small.
        private static readonly Dictionary<string, MovementCapabilities> sharedCapabilities = new();
        private const int MaxSharedCapabilities = 64;

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
                    if (!sharedCapabilities.TryGetValue(key, out capabilities))
                    {
                        using (MeasureMarker.Auto()) { capabilities = MovementSimulation.Measure(this); }
                        if (sharedCapabilities.Count >= MaxSharedCapabilities) { sharedCapabilities.Clear(); }
                        sharedCapabilities[key] = capabilities;
                    }
                    capabilitiesKey = key;
                }
                return capabilities;
            }
        }

        /// <summary>
        /// Everything the capabilities depend on, to know when they need measuring again, and which measurements another
        /// character can share: the movement mode (its type and settings), and everything MovementSimulation copies from
        /// the character (see CopyForSimulation, and its layer). Numbers are written in full, so a tiny change of size
        /// counts as a change. Something new that a measurement depends on must be added here.
        /// </summary>
        private string CapabilitiesKey()
        {
            CultureInfo invariant = CultureInfo.InvariantCulture;
            string F(float value) => value.ToString("R", invariant);
            string V(Vector3 value) => $"{F(value.x)},{F(value.y)},{F(value.z)}";
            string mode = movementHandler != null ? $"{movementHandler.GetType().FullName}:{JsonUtility.ToJson(movementHandler)}" : "";
            return string.Join("|", mode, V(Scale), Body.CapabilitiesKey(), sizeScaling.ToString(), F(maxStepHeight),
                F(maxSlopeAngle), F(skinWidth), F(groundSnapDistance), F(groundCheckRadius), F(movementSpeedMultiplier), F(jumpStrengthModifier),
                movementLayerMask.value.ToString(invariant), moveWithGround.ToString(), rotateWithGround.ToString(),
                inheritGroundVelocity.ToString(), F(maxGroundCarryPerStep), gameObject.layer.ToString(invariant));
        }

        /// <summary>
        /// Makes this controller (on a bare object with the given Rigidbody, not yet active) a copy of source for
        /// simulating its movement: the same settings, movement mode, body (a collider of its kind, as authored, and at the
        /// same size; or, given an authored radius and height, that body standing on its feet: see CharacterBody.CopyFor),
        /// but none of its state. See MovementSimulation.
        /// </summary>
        internal void CopyForSimulation(MovementController source, Rigidbody body, MovementMode mode,
            float? authoredRadius = null, float? authoredHeight = null)
        {
            rb = body;
            this.body = source.Body.CopyFor(this, authoredRadius, authoredHeight);
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
            posture = BodyPosture.Standing;
            ApplyScale(source.Scale);
        }
    }
}
