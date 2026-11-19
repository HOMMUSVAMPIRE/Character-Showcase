using System;
using MeridianMotion.Levels;
using UnityEngine;

namespace MeridianMotion.Characters
{
    /// <summary>
    /// Wading (see MeridianMotion.Levels.SwimmableWater): walking through water too shallow to swim in, the character walks and runs slower the
    /// deeper the water is over its feet: at its full speed where the water reaches Wade Start Depth, slowing to Wade
    /// Speed Multiplier of it where it's as deep as Swimming's Swim Depth (where it would swim). It only changes how the
    /// character walks (see MovementComponent.BeforeLocomotion), through the mode's MoveSpeed, as crouching does.
    /// </summary>
    [Serializable]
    public class Wading : MovementComponent
    {
        /// <summary>Wading looks like walking, slowed: no animation state of its own is needed.</summary>
        public override CharacterState LocomotionState => CharacterState.Wading;
        /// <summary>The events of Wading starting and ending (see MovementComponent.StateEvents).</summary>
        public override StateEventPair[] StateEvents => stateEvents;
        private static readonly StateEventPair[] stateEvents =
        {
            new(CharacterState.Wading, MovementEvents.WadeStarted, MovementEvents.WadeEnded),
        };

        [Tooltip("How deep water must be over the character's feet to slow it, as a share of its standing height: " +
                 "shallower (a puddle, the shallows' edge) it walks at full speed")]
        [Range(0f, 1f)] [SerializeField] private float wadeStartDepth = 0.1f;
        [Tooltip("The share of its walking and running speed the character keeps in water as deep as Swimming's Swim " +
                 "Depth (the deepest it wades in), easing from full speed at Wade Start Depth. 1 doesn't slow it")]
        [Range(0.05f, 1f)] [SerializeField] private float wadeSpeedMultiplier = 0.5f;

        /// <summary>How deep water must be over the feet to slow the character, as a share of its standing height.</summary>
        public float WadeStartDepth => wadeStartDepth;
        /// <summary>The share of its speed the character keeps wading as deep as it can (Swimming's Swim Depth).</summary>
        public float WadeSpeedMultiplier => wadeSpeedMultiplier;

        /// <summary>Every step, the character walks at full speed until wading says otherwise (only on the ground, below).</summary>
        public override void AdvanceTimers(MovementStep step, StepInputs inputs, MMotionMotor motor)
        {
            motor.wade.speedMultiplier = 1f;
        }

        /// <summary>
        /// On the ground in water at least Wade Start Depth deep over the feet (and too shallow to swim in: deeper,
        /// Swimming has the character), slows its walking by how deep it is, and marks the step as wading.
        /// </summary>
        public override void BeforeLocomotion(MovementStep step, StepInputs inputs, MMotionMotor motor)
        {
            if (!step.initialGrounded) { return; }
            Vector3 feet = step.initialPosition;
            // A skin's width above the feet, which rest a hair into the floor of the water (see Swimming).
            SwimmableWater water = SwimmableWater.At(feet + Vector3.up * motor.Skin);
            if (water == null) { return; }
            float height = motor.Body.StandingHeight;
            float under = water.SurfaceHeight - feet.y;
            float start = wadeStartDepth * height;
            if (under < start) { return; }

            // From the start depth to the swim depth (the deepest it wades), whether or not it swims; past it, as slow as
            // wading gets.
            float deepest = SettingsOf<Swimming>().SwimDepth * height;
            float share = deepest > start ? Mathf.Clamp01((under - start) / (deepest - start)) : 1f;
            motor.wade.speedMultiplier = Mathf.Lerp(1f, wadeSpeedMultiplier, share);
            step.state |= CharacterState.Wading;
        }
    }

    /// <summary>How much wading slows the character this step.</summary>
    public class WadeState
    {
        /// <summary>The share of its walking speed the character keeps this step (1 out of the water, or not wading).</summary>
        public float speedMultiplier = 1f;
    }

    // This state's place on the character (see MMotionMotor): declared here, beside its ability, so a new
    // ability adds its state in its own file rather than in MMotionMotor.cs.
    public partial class MMotionMotor
    {
        /// <summary>The character's wading state (Wading).</summary>
        public WadeState wade { get; } = new WadeState();
    }
}
