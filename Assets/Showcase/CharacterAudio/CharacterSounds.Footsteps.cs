using MeridianMotion.Characters;
using MeridianMotion.Showcase.Audio;
using UnityEngine;

namespace MeridianMotion.Showcase.CharacterAudio
{
    public partial class CharacterSounds
    {
        // The quietest footstep (at the movement's Walk Speed and below), as a share of full strength.
        private const float QuietestStep = 0.4f;
        // A foot counts as lifted once its foot IK's planted is below this, and as coming down once it's back above
        // PlantedAt after being lifted: the gap keeps a foot hovering about the threshold from stepping twice.
        private const float LiftedBelow = 0.2f, PlantedAt = 0.8f;

        // The states whose own sounds stand for the feet: no footsteps while the character's in one.
        private static readonly CharacterState NoFootsteps = CharacterState.Sliding | CharacterState.Rolling |
            CharacterState.WallRunning | CharacterState.WallClimbing | CharacterState.WallSliding | CharacterState.LedgeHanging |
            CharacterState.LedgeClimbing | CharacterState.RopeSwinging | CharacterState.Ziplining | CharacterState.Swimming |
            CharacterState.Wading | CharacterState.Diving | CharacterState.Vaulting;

        private MMotionCharacter character;
        private CharacterModelSlot models;
        private WalkingAndRunning walking;
        private MovementMode walkingFrom;
        // The loaded model's feet, as its foot IK sees them (a humanoid's two, a generic rig's legs), or none.
        private HumanoidFootPlacementIK humanoidFeet;
        private LegPlacementIK legFeet;
        private bool[] footLifted = new bool[0];
        private FootstepCadence cadence;
        private bool wasOnFeet;

        private void HookFootsteps(bool on)
        {
            if (character == null) { character = GetComponent<MMotionCharacter>(); }
            if (models == null) { models = GetComponent<CharacterModelSlot>(); }
            if (character != null)
            {
                if (on) { character.BeforeStep += HandleBeforeStep; }
                else { character.BeforeStep -= HandleBeforeStep; }
            }
            if (models != null)
            {
                if (on)
                {
                    models.ModelLoaded += FindFeet;
                    models.ModelUnloaded += LoseFeet;
                    if (models.LoadedModel != null) { FindFeet(models.LoadedModel); }
                }
                else
                {
                    models.ModelLoaded -= FindFeet;
                    models.ModelUnloaded -= LoseFeet;
                }
            }
        }

        private void FindFeet(LoadedCharacterModel model)
        {
            GameObject root = model?.Root;
            humanoidFeet = root != null ? root.GetComponentInChildren<HumanoidFootPlacementIK>(true) : null;
            legFeet = humanoidFeet == null && root != null ? root.GetComponentInChildren<LegPlacementIK>(true) : null;
            int feet = humanoidFeet != null ? 2 : legFeet != null && legFeet.Legs != null ? legFeet.Legs.Length : 0;
            footLifted = new bool[feet];
        }

        private void LoseFeet(LoadedCharacterModel model)
        {
            humanoidFeet = null;
            legFeet = null;
            footLifted = new bool[0];
        }

        private bool HasFeet => (humanoidFeet != null && humanoidFeet.isActiveAndEnabled) || (legFeet != null && legFeet.isActiveAndEnabled);

        private float Planted(int foot) => humanoidFeet != null ? humanoidFeet.Planted(foot) : legFeet.Planted(foot);

        // The movement's walking and running speeds (its Walking And Running: what the steps' loudness and, without feet
        // to follow, their cadence go by), at the character's size.
        private void WalkingSpeeds(out float walk, out float run)
        {
            if (walkingFrom != motor.movementMode || walking == null)
            {
                walkingFrom = motor.movementMode;
                walking = (motor.movementMode as AbilityMovementMode)?.Get<WalkingAndRunning>(includeDisabled: true);
            }
            walk = (walking != null ? walking.WalkSpeed : 5f) * motor.SpeedScale;
            run = Mathf.Max(walk + 0.01f, (walking != null ? walking.RunSpeed : 10f) * motor.SpeedScale);
        }

        // Before each step, the step just taken (the motor's last) is walked. With a model, its feet are followed: a step
        // as each comes down. Without, a cadence by speed. Either way on the movement's own steps, so frame rate doesn't
        // change them, however fast the test rig runs them.
        private void HandleBeforeStep(MMotionCharacter stepping, in CharacterInputFrame frame)
        {
            if (soundSet == null || motor == null || motor.lastStep == null || AudioService.Instance == null) { return; }
            MovementStep step = motor.lastStep;
            FootstepSettings settings = soundSet.Footsteps;
            Vector3 velocity = step.finalVelocity;
            float speed = new Vector2(velocity.x, velocity.z).magnitude;
            WalkingSpeeds(out float walk, out float run);

            bool onFeet = step.state.HasAny(CharacterState.Grounded) && !step.state.HasAny(NoFootsteps);
            bool moving = speed >= settings.minSpeed * motor.SpeedScale;
            bool stepped = false;
            if (HasFeet)
            {
                for (int i = 0; i < footLifted.Length; i++)
                {
                    float planted = Planted(i);
                    // Off its feet (in the air, sliding), a foot has to lift again before it can step: landing isn't a step.
                    if (!onFeet) { footLifted[i] = false; continue; }
                    if (planted < LiftedBelow) { footLifted[i] = true; }
                    else if (planted >= PlantedAt && footLifted[i])
                    {
                        footLifted[i] = false;
                        if (moving) { stepped = true; }
                    }
                }
            }
            else
            {
                float pace = Mathf.Lerp(settings.walkCadence, settings.runCadence, Mathf.InverseLerp(walk, run, speed));
                float stride = speed / Mathf.Max(0.1f, pace);
                if (!onFeet || !wasOnFeet) { cadence.Reset(stride); }
                if (onFeet && moving) { stepped = cadence.Advance(speed * step.deltaTime, stride); }
            }
            wasOnFeet = onFeet;
            if (!stepped) { return; }

            bool crouched = step.state.HasAny(CharacterState.Crouching);
            Vector3 feet = motor.Feet;
            SoundSurface surface = surfaces?.Find(motor.groundTransform, feet);
            SoundEvent sound = crouched && settings.crouchedStep != null
                ? CharacterSoundSet.For(settings.crouchedStep, settings.crouchedBySurface, surface)
                : CharacterSoundSet.For(settings.step, settings.bySurface, surface);
            Play(sound, feet, Mathf.Lerp(QuietestStep, 1f, Mathf.InverseLerp(walk, run, speed)));
        }
    }
}
