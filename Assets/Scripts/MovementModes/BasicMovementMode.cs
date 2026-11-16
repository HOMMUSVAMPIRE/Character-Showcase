using System.Collections.Generic;
using UnityEngine;

namespace Character
{
    /// <summary>
    /// A Scriptable Object type which defines a very standard character movement scheme, made of components (see
    /// ComposedMovementMode and DefaultComponents, in Components/): walking and running, falling and steering in the air,
    /// and each ability, with its settings. What's left here is what several components share: the character's posture
    /// (its collider lowered while something keeps it low), the jump and crouch presses, and the speeds and accelerations
    /// the components ask the mode for (from the components whose settings they are, whether or not those are turned on).
    /// </summary>
    [CreateAssetMenu(fileName = "BasicMovementMode", menuName = "Scriptable Objects/MovementModes/Basic")] [System.Serializable]
    public partial class BasicMovementMode : ComposedMovementMode
    {
        /// <summary>
        /// Whether the jump button press is fresh enough to start something (within Jumping's jump input buffer, whether or
        /// not it's turned on: a press jumps off walls and ropes either way), and hasn't started anything already (holding
        /// the button only continues a jump)
        /// </summary>
        public override bool IsNewJumpPress(Inputs inputs, MovementController controller)
        {
            return inputs.jump != null && inputs.jump.value && inputs.jump.holdDuration <= SettingsOf<Jumping>().JumpInputBuffer
                   && !controller.jump.pressConsumed;
        }

        /// <summary>
        /// The character's gravity (m/s²): Fall &amp; Air Handling's gravity strength (whether or not it's turned on), scaled
        /// as accelerations are (see MovementController.AccelerationScale).
        /// </summary>
        public override float GravityAcceleration(MovementController controller) =>
            SettingsOf<FallAndAirHandling>().GravityStrength * controller.AccelerationScale;

        /// <summary>
        /// Uses up a press of Crouch made within Crouching's crouch input buffer (whether or not it's turned on: a press
        /// lets go of a zipline either way), returning whether there was one.
        /// </summary>
        public override bool ConsumeCrouchPress(Inputs inputs, MovementController controller) =>
            controller.crouch.press.Consume(inputs.crouch, SettingsOf<Crouching>().CrouchInputBuffer);

        /// <summary>Fall &amp; Air Handling's air acceleration, scaled as accelerations are (see MovementController.AccelerationScale).</summary>
        public override float AirAcceleration(MovementController controller) =>
            SettingsOf<FallAndAirHandling>().AirAcceleration * controller.AccelerationScale;

        /// <summary>
        /// The components a new BasicMovementMode starts with, most committed first: a bounce pad first, since it launches
        /// whatever the character is doing on it (rolling, sliding, dashing); then what the character hangs from (a
        /// zipline, a rope), as they hold it over everything below; then a ledge, whose top is grabbed out of a slide down
        /// its wall, and a vault, over what's too low to grab; holding an object; and a climbable surface, climbed
        /// instead of run along or slid down, before a wall run and a slide down a wall. Then on the ground, a roll and a
        /// slide; and crouching, which only changes how the character walks. Then how it moves by itself (see
        /// MovementComponent.Locomotion): falling and steering in the air, whose ground friction comes before walking reads
        /// the momentum it leaves, and walking and running. Then dashing and jumping, which act on top of walking, a dash
        /// before the jump that cancels it (see MovementComponent.AfterWalking).
        /// </summary>
        protected override IEnumerable<MovementComponent> DefaultComponents()
        {
            yield return new BouncePads();
            yield return new Ziplines();
            yield return new RopeSwinging();
            yield return new Ledges();
            yield return new Vaulting();
            yield return new PushingAndPulling();
            yield return new WallClimbing();
            yield return new WallRunning();
            yield return new WallSliding();
            yield return new Rolling();
            yield return new Crouching();
            yield return new FallAndAirHandling();
            yield return new WalkingAndRunning();
            yield return new Dashing();
            yield return new Jumping();
        }

        /// <summary>
        /// Uses inputs and the starting data for the character at the beginning of the frame to fill out a movement data object
        /// with data on how the character should move this frame.
        /// </summary>
        /// <param name="movementData"></param>
        /// <param name="inputs"></param>
        /// <param name="controller"></param>
        public override void ProcessMovementData(ref MovementData movementData, Inputs inputs, MovementController controller)
        {
            // A released jump button frees the next press to start a jump (checked here, before the dash and the other
            // abilities, which can skip jump processing while they last)
            if (inputs.jump == null || !inputs.jump.value) { controller.jump.pressConsumed = false; }
            AdvanceAbilityTimers(ref movementData, inputs, controller);
            AdvanceComponentTimers(ref movementData, inputs, controller);

            // Gravity, whatever has the character (see FallAndAirHandling).
            ComponentsBeforeAbilities(ref movementData, inputs, controller);

            // The abilities that take over the character's movement while they last, most committed first: each carries
            // on if it's under way, or starts if its inputs and surroundings call for it, and returns whether it has the
            // character this step (see DefaultComponents). Otherwise the character walks, dashes and jumps (crouched or not).
            bool abilityHasCharacter = ComponentsTakeOver(ref movementData, inputs, controller);
            if (!abilityHasCharacter)
            {
                // Crouching, standing up (see Crouching), and anything else that changes how the character walks.
                ComponentsBeforeLocomotion(ref movementData, inputs, controller);
                // The ground's friction, walking and running, steering in the air (see FallAndAirHandling and WalkingAndRunning).
                ComponentsLocomotion(ref movementData, inputs, controller);
                // Then dashing and jumping (see Dashing and Jumping), and anything else that acts on top of walking.
                ComponentsAfterWalking(ref movementData, inputs, controller);
            }

            // The collider is lowered while crouched, sliding or rolling, and raised again where there's room.
            UpdatePosture(ref movementData, controller);
            // The fall speed limit (see FallAndAirHandling), last of all.
            ComponentsFinishStep(ref movementData, controller);
        }

        /// <summary>
        /// Frees the next press of each ability button once it's released, and advances the abilities' cooldowns (the
        /// components' advance their own: see AdvanceComponentTimers). Every step, whichever ability has the character, so
        /// their cooldowns run out meanwhile.
        /// </summary>
        private static void AdvanceAbilityTimers(ref MovementData movementData, Inputs inputs, MovementController controller)
        {
            float deltaTime = movementData.deltaTime;
            // Crouch presses are tracked whether or not the character can crouch: they let go of ropes, ziplines and walls.
            controller.crouch.press.Track(inputs.crouch);
            // Air steering comes back after a wall jump, and is all back on the ground.
            controller.wallJump.elapsed += deltaTime;
            if (movementData.initialGrounded) { controller.wallJump.recovering = false; controller.wallJump.hasLastWall = false; }
        }

        /// <summary>
        /// Lowers the character's collider to the crouch height (Crouching's, whether or not it's turned on: rolls go
        /// low either way) while a component keeps it low (crouched, sliding, rolling), and raises it again otherwise.
        /// Where there isn't room to stand (a slide or roll ending under something low, say), the character stays
        /// crouched, and stands up by itself once there is. Also marks the step as crouched.
        /// </summary>
        private void UpdatePosture(ref MovementData movementData, MovementController controller)
        {
            CrouchState crouch = controller.crouch;
            if (ComponentKeepsLow(controller)) { controller.SetHeightFraction(SettingsOf<Crouching>().CrouchHeight, movementData.initialPosition); }
            else if (!controller.SetHeightFraction(1f, movementData.initialPosition))
            {
                crouch.IsCrouched = true;
                crouch.wantsToStand = true;
            }
            else if (crouch.IsCrouched)
            {
                // Stood up with nothing keeping the character crouched: Crouching was turned off (or removed) while it
                // was, and there's room now.
                crouch.IsCrouched = false;
                crouch.wantsToStand = false;
            }

            if (crouch.IsCrouched && !controller.slide.IsSliding && !controller.roll.IsRolling)
            {
                movementData.state |= CharacterState.Crouching;
            }
        }

        /// <summary>
        /// The speed to move at for these inputs: the crouch speed while crouched (Crouching's), otherwise the run speed
        /// while sprinting, or the walk speed (Walking &amp; Running's, whether or not it's turned on: they shape air
        /// steering and a roll's steering too; see WalkingAndRunning.MoveSpeed).
        /// </summary>
        public override float MoveSpeed(Inputs inputs, MovementController controller) =>
            controller.crouch.IsCrouched ? SettingsOf<Crouching>().CrouchSpeed : SettingsOf<WalkingAndRunning>().MoveSpeed(inputs);
    }
}
