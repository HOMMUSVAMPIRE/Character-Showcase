using System.Collections.Generic;
using UnityEngine;

namespace MeridianMotion.Characters
{
    /// <summary>
    /// A Scriptable Object type which defines a very standard character movement scheme, made of components (see
    /// AbilityMovementMode and DefaultComponents, in Components/): walking and running, falling and steering in the air,
    /// and each ability, with its settings. What's left here is what several components share: the character's posture
    /// (its collider lowered while something keeps it low), the jump and crouch presses, and the speeds and accelerations
    /// the components ask the mode for (from the components whose settings they are, whether or not those are turned on).
    /// </summary>
    [CreateAssetMenu(fileName = "Movement Mode", menuName = "Meridian Motion/Movement/Movement Mode")] [System.Serializable]
    public partial class StandardMovementMode : AbilityMovementMode
    {
        /// <summary>
        /// Whether the jump button press is fresh enough to start something (within Jumping's jump input buffer, whether or
        /// not it's turned on: a press jumps off walls and ropes either way), and hasn't started anything already (holding
        /// the button only continues a jump)
        /// </summary>
        public override bool HasFreshJumpPress(StepInputs inputs, MMotionMotor motor)
        {
            return motor.jump.press.IsFresh(inputs.jump, SettingsOf<Jumping>().JumpInputBuffer);
        }

        /// <summary>
        /// The character's gravity (m/s²): Fall &amp; Air Handling's gravity strength (whether or not it's turned on), scaled
        /// as accelerations are (see MMotionMotor.AccelerationScale).
        /// </summary>
        public override float GravityAcceleration(MMotionMotor motor) =>
            SettingsOf<FallAndAirHandling>().GravityStrength * motor.AccelerationScale;

        /// <summary>
        /// Uses up a press of Crouch made within Crouching's crouch input buffer (whether or not it's turned on: a press
        /// lets go of a zipline either way), returning whether there was one.
        /// </summary>
        public override bool ConsumeCrouchPress(StepInputs inputs, MMotionMotor motor) =>
            motor.crouch.press.Consume(inputs.crouch, SettingsOf<Crouching>().CrouchInputBuffer);

        /// <summary>Fall &amp; Air Handling's air acceleration, scaled as accelerations are (see MMotionMotor.AccelerationScale).</summary>
        public override float AirAcceleration(MMotionMotor motor) =>
            SettingsOf<FallAndAirHandling>().AirAcceleration * motor.AccelerationScale;

        /// <summary>
        /// The components a new StandardMovementMode starts with, most committed first: a bounce pad first, since it launches
        /// whatever the character is doing on it (rolling, sliding, dashing); then what the character hangs from (a
        /// zipline, a rope), as they hold it over everything below; then a ledge, whose top is grabbed out of a slide down
        /// its wall, and a vault, over what's too low to grab; holding an object; and a climbable surface, climbed
        /// instead of run along or slid down, or swum into from the water. Then swimming, which deep water takes the
        /// character into from whatever is below it in the list; then a wall run and a slide down a wall. Then on the
        /// ground, a roll and a slide; and crouching and wading, which only change how the character walks. Then how it
        /// moves by itself (see MovementComponent.Locomotion): falling and steering in the air, whose ground friction comes
        /// before walking reads the momentum it leaves, and walking and running. Then dashing and jumping, which act on top of
        /// walking, a dash before the jump that cancels it (see MovementComponent.AfterLocomotion).
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
            yield return new Swimming();
            yield return new WallRunning();
            yield return new WallSliding();
            yield return new Rolling();
            yield return new Crouching();
            yield return new Wading();
            yield return new FallAndAirHandling();
            yield return new WalkingAndRunning();
            yield return new Dashing();
            yield return new Jumping();
        }

        /// <summary>
        /// Uses inputs and the starting data for the character at the beginning of the frame to fill out a movement data object
        /// with data on how the character should move this frame.
        /// </summary>
        /// <param name="step"></param>
        /// <param name="inputs"></param>
        /// <param name="controller"></param>
        public override void Run(MovementStep step, StepInputs inputs, MMotionMotor motor)
        {
            // A released jump button frees the next press to start a jump (checked here, before the dash and the other
            // abilities, which can skip jump processing while they last)
            motor.jump.press.Track(inputs.jump);
            AdvanceAbilityTimers(step, inputs, motor);
            AdvanceComponentTimers(step, inputs, motor);

            // Gravity, whatever has the character (see FallAndAirHandling).
            ComponentsBeforeAbilities(step, inputs, motor);

            // The abilities that take over the character's movement while they last, most committed first: each carries
            // on if it's under way, or starts if its inputs and surroundings call for it, and returns whether it has the
            // character this step (see DefaultComponents). Otherwise the character walks, dashes and jumps (crouched or not).
            bool abilityHasCharacter = ComponentsTakeOver(step, inputs, motor);
            if (!abilityHasCharacter)
            {
                // Crouching, standing up (see Crouching), and anything else that changes how the character walks.
                ComponentsBeforeLocomotion(step, inputs, motor);
                // The ground's friction, walking and running, steering in the air (see FallAndAirHandling and WalkingAndRunning).
                ComponentsLocomotion(step, inputs, motor);
                // Then dashing and jumping (see Dashing and Jumping), and anything else that acts on top of walking.
                ComponentsAfterWalking(step, inputs, motor);
            }

            // The body is held low while crouched, sliding or rolling (its posture), and stood up again where there's room.
            UpdatePosture(step, motor);
            // The fall speed limit (see FallAndAirHandling), last of all.
            ComponentsFinishStep(step, motor);
        }

        /// <summary>
        /// Frees the next press of each ability button once it's released, and advances the abilities' cooldowns (the
        /// components' advance their own: see AdvanceComponentTimers). Every step, whichever ability has the character, so
        /// their cooldowns run out meanwhile.
        /// </summary>
        private static void AdvanceAbilityTimers(MovementStep step, StepInputs inputs, MMotionMotor motor)
        {
            float deltaTime = step.deltaTime;
            // Crouch presses are tracked whether or not the character can crouch: they let go of ropes, ziplines and walls.
            motor.crouch.press.Track(inputs.crouch);
            // Air steering comes back after a wall jump, and is all back on the ground.
            motor.wallJump.elapsed += deltaTime;
            if (step.initialGrounded) { motor.wallJump.recovering = false; motor.wallJump.hasLastWall = false; }
        }

        /// <summary>
        /// Holds the character's body the way the first component to hold it does (low while crouched, sliding or rolling;
        /// lying while swimming underwater: see MovementComponent.Posture), where there's room; where there isn't (lying
        /// down beside a wall, say), it's held low, at the crouch height (Crouching's, whether or not it's turned on),
        /// until there is. With none holding it, it stands again; where there isn't room to (a slide or roll ending under
        /// something low, say), the character stays crouched, and stands up by itself once there is. Also marks the step
        /// as crouched.
        /// </summary>
        private void UpdatePosture(MovementStep step, MMotionMotor motor)
        {
            CrouchState crouch = motor.crouch;
            Vector3 feet = step.initialPosition;
            BodyPosture low = BodyPosture.Low(SettingsOf<Crouching>().CrouchHeight);
            BodyPosture? held = ComponentPosture(motor);
            if (held.HasValue)
            {
                if (!motor.SetPosture(held.Value, feet)) { motor.SetPosture(low, feet); }
            }
            else if (!motor.SetPosture(BodyPosture.Standing, feet))
            {
                // Low, if it's not already (coming up from lying under something low); where even that won't fit, it stays as it is.
                motor.SetPosture(low, feet);
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

            if (crouch.IsCrouched && !motor.slide.IsSliding && !motor.roll.IsRolling)
            {
                step.state |= CharacterState.Crouching;
            }
        }

        /// <summary>
        /// The speed to move at for these inputs: the crouch speed while crouched (Crouching's), otherwise the run speed
        /// while sprinting, or the walk speed (Walking &amp; Running's, whether or not it's turned on: they shape air
        /// steering and a roll's steering too; see WalkingAndRunning.MoveSpeed). Slowed by wading this step, if Wading's
        /// turned on (see Wading).
        /// </summary>
        public override float MoveSpeed(StepInputs inputs, MMotionMotor motor)
        {
            float speed = motor.crouch.IsCrouched ? SettingsOf<Crouching>().CrouchSpeed : SettingsOf<WalkingAndRunning>().MoveSpeed(inputs);
            return Get<Wading>() != null ? speed * motor.wade.speedMultiplier : speed;
        }
    }
}
