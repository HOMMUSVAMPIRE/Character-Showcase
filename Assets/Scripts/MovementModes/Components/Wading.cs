using System;
using Level;
using UnityEngine;

namespace Character
{
    /// <summary>
    /// Wading (see Level.Water): walking through water too shallow to swim in, the character walks and runs slower the
    /// deeper the water is over its feet: at its full speed where the water reaches Wade Start Depth, slowing to Wade
    /// Speed Multiplier of it where it's as deep as Swimming's Swim Depth (where it would swim). It only changes how the
    /// character walks (see MovementComponent.BeforeLocomotion), through the mode's MoveSpeed, as crouching does.
    /// </summary>
    [Serializable]
    public class Wading : MovementComponent
    {
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
        public override void AdvanceTimers(ref MovementData movementData, Inputs inputs, MovementController controller)
        {
            controller.wade.speedMultiplier = 1f;
        }

        /// <summary>
        /// On the ground in water at least Wade Start Depth deep over the feet (and too shallow to swim in: deeper,
        /// Swimming has the character), slows its walking by how deep it is, and marks the step as wading.
        /// </summary>
        public override void BeforeLocomotion(ref MovementData movementData, Inputs inputs, MovementController controller)
        {
            if (!movementData.initialGrounded) { return; }
            Vector3 feet = movementData.initialPosition;
            Water water = Water.At(feet);
            if (water == null) { return; }
            float height = controller.Body.StandingHeight;
            float under = water.SurfaceHeight - feet.y;
            float start = wadeStartDepth * height;
            if (under < start) { return; }

            // From the start depth to the swim depth (the deepest it wades), whether or not it swims; past it, as slow as
            // wading gets.
            float deepest = SettingsOf<Swimming>().SwimDepth * height;
            float share = deepest > start ? Mathf.Clamp01((under - start) / (deepest - start)) : 1f;
            controller.wade.speedMultiplier = Mathf.Lerp(1f, wadeSpeedMultiplier, share);
            movementData.state |= CharacterState.Wading;
        }
    }

    /// <summary>How much wading slows the character this step.</summary>
    public class WadeState
    {
        /// <summary>The share of its walking speed the character keeps this step (1 out of the water, or not wading).</summary>
        public float speedMultiplier = 1f;
    }
}
