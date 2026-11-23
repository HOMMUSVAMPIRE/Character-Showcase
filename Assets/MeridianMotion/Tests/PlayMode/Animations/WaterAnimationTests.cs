using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MeridianMotion.Characters;
using NUnit.Framework;
using UnityEngine.TestTools;
using UnityEngine;

namespace MeridianMotion.Tests.Animations
{
    /// <summary>
    /// Checks that being in the water shows on the character's own model, through the shared Animator Controller:
    /// swimming with the head just out of the water, diving and back, tilting with the body swimming down, and climbing
    /// (not vaulting) out over a low side. In the SampleContent category, as they check the package's own controller and
    /// model.
    /// </summary>
    [Category(TestArea.Animation)]
    [Category("SampleContent")]
    public class WaterAnimationTests : AbilityAnimationTestsBase
    {
        [UnityTest]
        public IEnumerator Swimming_PlaysSwim_WithTheHeadJustOutOfTheWater()
        {
            yield return Spawn();
            // Deep water, with no floor under it: only the swimming holds the character up.
            MeridianMotion.Levels.SwimmableWater water = rig.Water(new Vector3(0f, 10f, 0f), new Vector3(40f, 8f, 40f));
            float depth = rig.MovementSetting<float>("swimDepth") * rig.StandingHeight;
            rig.Teleport(new Vector3(0f, water.SurfaceHeight - depth - 0.1f, 0f));
            yield return ExpectState(HumanoidAnimatorStates.Swim, 0.6f, _ => CharacterTestRig.Idle());
            yield return rig.Run(1f, _ => CharacterTestRig.Idle(), StepAnimator);
            Assert.That(rig.InState(CharacterState.Swimming), Is.True, "Test setup: the character should be swimming.");

            // Treading water, the head (its pivot, at the top of the neck) is just above the surface: the swimming clips
            // are floated at the swim depth (see AnimationPreparation.FloatSwimsAtTheSwimDepth).
            float size = rig.Controller.CurrentScale.y;
            float head = animator.GetBoneTransform(HumanBodyBones.Head).position.y - water.SurfaceHeight;
            Assert.That(head, Is.InRange(0f, 0.35f * size), "Treading water, the swimmer's head should be just out of the water.");

            yield return ExpectState(HumanoidAnimatorStates.Swim, 1f, _ => CharacterTestRig.Move(Vector3.forward));
        }

        [UnityTest]
        public IEnumerator Diving_PlaysSwimUnderwater_AndBackAtTheSurface_Swim()
        {
            yield return Spawn();
            MeridianMotion.Levels.SwimmableWater water = rig.Water(new Vector3(0f, 12f, 0f), new Vector3(40f, 10f, 40f));
            float depth = rig.MovementSetting<float>("swimDepth") * rig.StandingHeight;
            rig.Teleport(new Vector3(0f, water.SurfaceHeight - depth, 0f));
            yield return ExpectState(HumanoidAnimatorStates.Swim, 0.6f, _ => CharacterTestRig.Idle());
            yield return ExpectState(HumanoidAnimatorStates.SwimUnderwater, 0.6f, step => CharacterTestRig.Crouch());
            Assert.That(rig.InState(CharacterState.Diving), Is.True, "Test setup: the character should be underwater.");
            // The swimming clips float the body the swim depth above the feet; underwater the body lies on them, and the
            // model is drawn lowered to it.
            yield return rig.Run(0.6f, _ => CharacterTestRig.Idle(), step =>
            {
                StepAnimator(step);
                rig.Animation.SmoothRotateVisuals(Time.fixedDeltaTime, Quaternion.identity);
            });
            float hips = animator.GetBoneTransform(HumanBodyBones.Hips).position.y - rig.Position.y;
            Assert.That(hips, Is.InRange(0f, rig.CurrentHeight), "Underwater, the swimmer should be drawn inside its lying body.");
            // Drifting without swimming too: lying, not treading water upright, its legs hanging through a floor it's near.
            float lowestFoot = Mathf.Min(animator.GetBoneTransform(HumanBodyBones.LeftFoot).position.y,
                animator.GetBoneTransform(HumanBodyBones.RightFoot).position.y) - rig.Position.y;
            Assert.That(lowestFoot, Is.GreaterThan(-0.1f * rig.Controller.CurrentScale.y), "Underwater, the swimmer's feet shouldn't hang below its lying body.");
            yield return rig.RunUntil(() => !rig.InState(CharacterState.Diving), step => CharacterTestRig.Jump(), StepAnimator, timeoutSeconds: 5f);
            yield return ExpectState(HumanoidAnimatorStates.Swim, 0.6f, _ => CharacterTestRig.Idle());
        }

        [UnityTest]
        public IEnumerator SwimmingDownUnderwater_TiltsTheModelWithTheBody()
        {
            yield return Spawn();
            MeridianMotion.Levels.SwimmableWater water = rig.Water(new Vector3(0f, 12f, 0f), new Vector3(40f, 10f, 40f));
            float depth = rig.MovementSetting<float>("swimDepth") * rig.StandingHeight;
            rig.Teleport(new Vector3(0f, water.SurfaceHeight - depth, 0f));
            yield return ExpectState(HumanoidAnimatorStates.SwimUnderwater, 0.6f, step => CharacterTestRig.Crouch());
            Assume.That(rig.InState(CharacterState.Diving), Is.True, "Test setup: the character should be underwater.");
            const float lookDown = 30f;
            rig.Look = Quaternion.Euler(lookDown, 0f, 0f);
            yield return rig.Run(1f, _ => CharacterTestRig.Move(Vector3.forward), step =>
            {
                StepAnimator(step);
                rig.Animation.SmoothRotateVisuals(Time.fixedDeltaTime, Quaternion.identity);
            });
            Assume.That(rig.Movement.BodyPitch, Is.EqualTo(-lookDown).Within(0.5f), "Test setup: the body should pitch down the way it swims.");
            Transform modelParent = rig.Animation.ModelHost.ModelParent;
            float modelPitch = Vector3.SignedAngle(Vector3.forward, modelParent.forward, Vector3.right);
            Assert.That(modelPitch, Is.EqualTo(lookDown).Within(2f), "The model should be tilted down with the body.");
            // Drawn inside the pitched body: its hips between the body's bottom and top, as the body is turned.
            Quaternion attitude = MMotionMotor.Attitude(rig.Body.rotation, rig.Movement.BodyPitch);
            Vector3 hips = Quaternion.Inverse(attitude) * (animator.GetBoneTransform(HumanBodyBones.Hips).position - rig.Position);
            Assert.That(hips.y, Is.InRange(0f, rig.CurrentHeight), "Pitched, the swimmer should still be drawn inside its lying body.");

            // Steering no more, the body levels out, and the model with it.
            yield return rig.Run(1f, _ => CharacterTestRig.Idle(), step =>
            {
                StepAnimator(step);
                rig.Animation.SmoothRotateVisuals(Time.fixedDeltaTime, Quaternion.identity);
            });
            Assert.That(rig.Movement.BodyPitch, Is.EqualTo(0f), "Without steering, the body should level out.");
            Assert.That(Vector3.Angle(modelParent.forward, Vector3.ProjectOnPlane(modelParent.forward, Vector3.up)), Is.LessThan(1f),
                "And the model with it.");
        }

        [UnityTest]
        public IEnumerator SwimmingIntoALowSide_PlaysClimb_NotVault()
        {
            yield return Spawn();
            // The model eases up the climb out each frame (see MMotionAnimator), which this measures: Unity's frames.
            rig.StepsPhysicsItself = false;
            // Deep water (no floor under it), with a side ahead whose top is a little out of the water: low enough to
            // vault onto from the water (see Swimming), which shows as climbing out.
            MeridianMotion.Levels.SwimmableWater water = rig.Water(new Vector3(0f, 10f, 0f), new Vector3(40f, 8f, 40f));
            float depth = rig.MovementSetting<float>("swimDepth") * rig.StandingHeight;
            float edge = 0.5f * rig.MovementSetting<float>("swimVaultHeight") * rig.StandingHeight;
            float top = water.SurfaceHeight + edge;
            rig.Box("Pool Side", new Vector3(0f, (top + water.BottomHeight) / 2f, 18f), new Vector3(20f, top - water.BottomHeight, 30f));
            rig.Teleport(new Vector3(0f, water.SurfaceHeight - depth, 0f));
            yield return ExpectState(HumanoidAnimatorStates.Swim, 0.6f, _ => CharacterTestRig.Idle());

            // Straight from swimming into the climb, the body rising with it: it once went through the air state first (a
            // step with neither swim nor climb), and the climb then started from its hang, below the swimmer's body, so
            // the model went up, down and up again.
            bool climbed = false, vaulted = false, airborne = false;
            float size = rig.Controller.CurrentScale.y, biggestDrop = 0f;
            Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            float lastHips = hips.position.y;
            yield return rig.Run(3f, _ => CharacterTestRig.Move(Vector3.forward), step =>
            {
                StepAnimator(step);
                climbed |= InAnimatorState(HumanoidAnimatorStates.Climb);
                vaulted |= InAnimatorState(HumanoidAnimatorStates.Vault);
                bool outOfWater = rig.InState(CharacterState.Vaulting);
                airborne |= outOfWater && InAnimatorState(HumanoidAnimatorStates.Airborne);
                if (outOfWater) { biggestDrop = Mathf.Max(biggestDrop, lastHips - hips.position.y); }
                lastHips = hips.position.y;
            });
            Assert.That(rig.Grounded && rig.Position.y > top - 0.05f, Is.True, "Test setup: the character should have got out onto the side.");
            Assert.That(climbed, Is.True, $"Getting out of the water onto the side should play the climb (the Animator is in '{CurrentAnimatorState()}').");
            Assert.That(vaulted, Is.False, "Getting out of the water shouldn't play the vault.");
            Assert.That(airborne, Is.False, "Getting out of the water shouldn't pass through the air state.");
            Assert.That(biggestDrop, Is.LessThan(0.1f * size), "Climbing out, the model's body shouldn't drop back as it rises.");
        }
    }
}
