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
    /// Checks that getting about the level shows on the character's own model, through the shared Animator Controller:
    /// stepping up and over stairs eased, pushing, climbing a wall (each step on and off once) or a ladder, swinging on a
    /// rope and riding a zipline each play their state, the hands on the wall, the rungs, the rope or the cable. In the
    /// SampleContent category, as they check the package's own controller and model.
    /// </summary>
    [Category(TestArea.Animation)]
    [Category("SampleContent")]
    public class TraversalAnimationTests : AbilityAnimationTestsBase
    {
        [UnityTest]
        public IEnumerator SteppingUpOntoALowPlatform_EasesTheModelUp_WithTheFeetTuckedOntoTheTop()
        {
            yield return Spawn();
            rig.Body.position = new Vector3(0f, 3f, 0f);
            float share = Mathf.Lerp(rig.StepHeight / rig.StandingHeight, rig.MovementSetting<float>("vaultStepUpHeight"), 0.6f);
            float top = 3f + share * rig.StandingHeight, face = rig.FrontExtent + 0.25f;
            rig.Box("Low Platform", new Vector3(0f, top / 2f, face + 3f), new Vector3(10f, top, 6f));

            // The visuals stepped with the physics, as MMotionCharacter does each frame.
            Transform modelParent = rig.Models.ModelParent;
            Vector3 restingLocal = modelParent.localPosition;
            Transform leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot), rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            bool steppedUp = false;
            float deepestLag = 0f, deepestFoot = float.PositiveInfinity;
            yield return rig.Run(0.8f, _ => CharacterTestRig.Move(Vector3.forward), step =>
            {
                rig.Animation.SmoothRotateVisuals(Time.fixedDeltaTime, Quaternion.identity);
                StepAnimator(step);
                steppedUp |= rig.InState(CharacterState.SteppingUp);
                deepestLag = Mathf.Min(deepestLag, modelParent.position.y - rig.Character.transform.position.y);
                foreach (Transform foot in new[] { leftFoot, rightFoot })
                {
                    if (foot.position.z > face + 0.05f) { deepestFoot = Mathf.Min(deepestFoot, foot.position.y - top); }
                }
            });

            float size = rig.Controller.CurrentScale.y;
            Assert.That(steppedUp, Is.True, "Test setup: the character should step up onto the platform.");
            Assert.That(deepestLag, Is.LessThan(-0.05f * size), "The model should lag below the character as it eases up after the step's quick rise.");
            Assert.That(deepestLag, Is.GreaterThan(-0.36f * size), "The model shouldn't lag further than its most.");
            Assert.That(deepestFoot, Is.GreaterThan(0f), "No foot over the platform should sink into its top: they're tucked up onto it.");
            Assert.That(Vector3.Distance(modelParent.localPosition, restingLocal), Is.LessThan(1e-3f), "The model should have caught up with the character.");
        }

        [UnityTest]
        public IEnumerator JumpingOntoStairsAndClimbingThemOnTheRun_LandsRatherThanStayingInTheAir([Values(false, true)] bool sprint)
        {
            yield return Spawn();
            // A long flight rising ahead, in the level's stair profile (0.3 m steps, 0.4 m deep): walking up them lifts
            // the character at several metres a second each step, which used to read to the Animator as still rising
            // after the jump, all the way up.
            const int steps = 30;
            const float rise = 0.3f, run = 0.4f;
            if (rig.StepHeight < rise) { Assert.Ignore("The character's step height is lower than the level's steps."); }
            float highest = 0f;
            for (int i = 0; i < steps; i++)
            {
                rig.Box($"Step {i + 1}", new Vector3(0f, (i + 1) * rise / 2f, 2f + i * run + run / 2f), new Vector3(10f, (i + 1) * rise, run));
            }
            int landedAt = -1, airborneAfterLanding = 0;
            yield return rig.Run(2.5f, step =>
            {
                StepInputs inputs = sprint ? CharacterTestRig.Sprint(Vector3.forward) : CharacterTestRig.Move(Vector3.forward);
                if (step >= 8 && step < 14) { inputs.jump = CharacterTestRig.Held(); }
                return inputs;
            }, step =>
            {
                StepAnimator(step);
                highest = Mathf.Max(highest, rig.Position.y);
                if (step > 14 && landedAt < 0 && rig.Grounded) { landedAt = step; }
                bool inAir = InAnimatorState(HumanoidAnimatorStates.Airborne) || animator.GetCurrentAnimatorStateInfo(0).IsName(HumanoidAnimatorStates.Jump);
                // On the flight, that is: running off its top end falls, as it should.
                bool onFlight = rig.Position.z < 2f + (steps - 1) * run;
                if (landedAt >= 0 && step > landedAt + CharacterTestRig.StepsFor(0.3f) && onFlight && inAir) { airborneAfterLanding++; }
            });
            Assert.That(landedAt, Is.GreaterThan(0), "Test setup: the character should land on the stairs.");
            Assert.That(highest, Is.GreaterThan(5f * rise), "Test setup: the character should climb the stairs.");
            Assert.That(airborneAfterLanding, Is.Zero, "Once landed on the stairs, the Animator should land, not stay in the air while the character climbs.");
        }

        /// <summary>
        /// Walking up or down stairs, collision lifts the character onto each step, or drops it onto the next, in a
        /// physics step: the model is eased over each step instead (see MovementStep.stepChange), so it goes up and down
        /// the flight smoothly. Its hips jolted up or down a whole step at a time, ten times as sharply as walking.
        /// </summary>
        [UnityTest]
        public IEnumerator WalkingUpOrDownStairs_TheModelEasesOverEachStep([Values(true, false)] bool up)
        {
            yield return Spawn();
            const int steps = 12;
            float rise = 0.85f * rig.StepHeight, run = 4f / 3f * rise, top = steps * rise;
            for (int i = 0; i < steps - 1; i++)
            {
                float height = up ? rise * (i + 1) : top - rise * (i + 1);
                rig.Box($"Step {i + 1}", new Vector3(0f, height / 2f, 1f + i * run + run / 2f), new Vector3(10f, height, run));
            }
            rig.Box(up ? "Top Landing" : "Top", new Vector3(0f, top / 2f, up ? 1f + (steps - 1) * run + 5f : -4f), new Vector3(10f, top, 10f));
            if (!up)
            {
                rig.Body.position = new Vector3(0f, top + 0.05f, 0f);
                yield return rig.Run(0.3f, _ => CharacterTestRig.Idle(), StepAnimator);
            }
            Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            var hipsHeights = new List<float>();
            var rootHeights = new List<float>();
            bool stepped = false;
            yield return rig.Run(2f, _ => CharacterTestRig.Move(Vector3.forward), step =>
            {
                rig.Animation.SmoothRotateVisuals(Time.fixedDeltaTime, Quaternion.identity);
                StepAnimator(step);
                stepped |= rig.Movement.lastStep.stepChange != 0f;
                hipsHeights.Add(hips.position.y);
                rootHeights.Add(rig.Character.transform.position.y);
            });
            Assert.That(stepped, Is.True, "Test setup: the character should step up or down the stairs.");
            Assert.That(PeakJolt(hipsHeights), Is.LessThan(0.5f * PeakJolt(rootHeights)),
                "The model should ease over each step, not jolt up or down a step at a time as the character does.");
        }

        [UnityTest]
        public IEnumerator PushingABox_PlaysPush()
        {
            yield return Spawn();
            float near = rig.Position.z + rig.FrontExtent + 0.1f;
            rig.PushableBox(new Vector3(0f, 0.75f, near + 0.75f), Vector3.one * 1.5f);
            yield return ExpectState(HumanoidAnimatorStates.Push, 1f, step => CharacterTestRig.Interact(Vector3.forward));
        }

        [UnityTest]
        public IEnumerator ClimbingAClimbableWall_PlaysWallClimb_WithTheHandsOnTheWall()
        {
            yield return Spawn();
            rig.Floor();
            float face = rig.Position.z + rig.FrontExtent + 0.3f;
            rig.ClimbableBox("Wall", new Vector3(0f, 6f, face + 0.5f), new Vector3(10f, 12f, 1f));
            yield return ExpectState(HumanoidAnimatorStates.WallClimb, 1.5f, _ => CharacterTestRig.Move(Vector3.forward));
            Assert.That(rig.InState(CharacterState.WallClimbing), Is.True, "Test setup: the character should be climbing.");

            // Climbing up, the hands reach up the wall and hold onto it: on its face (as the library's climbs are made,
            // for a wall just in front of the body), not pushed into it or reaching at the air in front of it.
            float size = rig.Controller.CurrentScale.y;
            Transform left = animator.GetBoneTransform(HumanBodyBones.LeftHand), right = animator.GetBoneTransform(HumanBodyBones.RightHand);
            Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
            float nearest = float.PositiveInfinity, farthest = float.NegativeInfinity, highest = float.NegativeInfinity;
            yield return rig.Run(1f, _ => CharacterTestRig.Move(Vector3.forward), step =>
            {
                StepAnimator(step);
                foreach (Transform hand in new[] { left, right })
                {
                    float fromWall = face - hand.position.z;
                    nearest = Mathf.Min(nearest, fromWall);
                    farthest = Mathf.Max(farthest, fromWall);
                    highest = Mathf.Max(highest, hand.position.y - head.position.y);
                }
            });
            Assert.That(rig.InState(CharacterState.WallClimbing), Is.True);
            Assert.That(nearest, Is.GreaterThan(-0.01f * size), "The hands shouldn't go into the wall.");
            Assert.That(nearest, Is.LessThan(0.06f * size), "A hand should be on the wall, holding on.");
            Assert.That(farthest, Is.LessThan(0.25f * size), "The hands should be on the wall, not reaching at the air in front of it.");
            Assert.That(highest, Is.GreaterThan(0f), "Climbing up, a hand should reach up above the head.");
        }

        /// <summary>
        /// Stepping onto a wall from the ground and off it again, the step's animation plays once each way: its progress
        /// (which its pose is drawn at) never goes back while the Animator shows it, blending out of it included. Dropping
        /// back to 0 as the step ended drew the step's start again for the blend, so it seemed to play twice. Climb Enter
        /// hands over to the climb (Wall Climb), and Climb Exit to walking (Locomotion).
        /// </summary>
        [UnityTest]
        public IEnumerator SteppingOntoAndOffAWall_PlaysEachStepOnce()
        {
            yield return Spawn();
            rig.Floor();
            float face = rig.Position.z + rig.FrontExtent + 0.3f;
            rig.ClimbableBox("Wall", new Vector3(0f, 6f, face + 0.5f), new Vector3(10f, 12f, 1f));
            int enterShown = 0, exitShown = 0;
            float backwards = 0f;
            void Watch(string state, ref int shownFor, ref float last)
            {
                bool shown = animator.GetCurrentAnimatorStateInfo(0).IsName(state)
                             || (animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).IsName(state));
                if (!shown) { last = 0f; return; }
                float progress = animator.GetFloat(AnimatorParameterTranslator.DefaultClimbStepProgressParameter);
                if (shownFor > 0) { backwards = Mathf.Max(backwards, last - progress); }
                last = progress;
                shownFor++;
            }
            float lastEnter = 0f, lastExit = 0f;
            bool climbShown = false, walkingAfterExit = false;
            yield return rig.Run(1.4f, _ => CharacterTestRig.Move(Vector3.forward), step =>
            {
                StepAnimator(step);
                Watch(HumanoidAnimatorStates.ClimbEnter, ref enterShown, ref lastEnter);
                climbShown |= enterShown > 0 && InAnimatorState(HumanoidAnimatorStates.WallClimb);
            });
            yield return rig.Run(1.4f, _ => CharacterTestRig.Move(Vector3.back), step =>
            {
                StepAnimator(step);
                Watch(HumanoidAnimatorStates.ClimbExit, ref exitShown, ref lastExit);
                walkingAfterExit |= exitShown > 0 && InAnimatorState(HumanoidAnimatorStates.Locomotion);
            });
            Assert.That(enterShown, Is.GreaterThan(0), "Test setup: stepping onto the wall should have played Climb Enter.");
            Assert.That(exitShown, Is.GreaterThan(0), "Test setup: stepping off it should have played Climb Exit.");
            Assert.That(backwards, Is.LessThan(1e-4f), "A step's animation should play through once: its progress shouldn't go back while it shows.");
            Assert.That(climbShown, Is.True, $"Stepped onto the wall, the '{HumanoidAnimatorStates.WallClimb}' state should play.");
            // Stepped off late in the walk back, the blend into walking may not have begun yet: stood still, it does.
            if (!walkingAfterExit) { yield return ExpectState(HumanoidAnimatorStates.Locomotion, 1.2f, _ => CharacterTestRig.Idle()); }
        }

        [UnityTest]
        public IEnumerator ClimbingALadder_PlaysLadder_WithTheHandsAndFeetOnTheRungs()
        {
            yield return Spawn();
            rig.Floor();
            float face = rig.Position.z + rig.FrontExtent + 0.3f;
            MeridianMotion.Levels.ClimbableLadder ladder = rig.Ladder(new Vector3(rig.Position.x, 0f, face + 0.03f), 12f);
            yield return ExpectState(HumanoidAnimatorStates.Ladder, 2f, _ => CharacterTestRig.Move(Vector3.forward));
            yield return rig.Run(0.8f, _ => CharacterTestRig.Move(Vector3.forward), StepAnimator);
            // Holding still, every hand and foot on a rung.
            yield return rig.Run(0.5f, _ => CharacterTestRig.Idle(), StepAnimator);
            Assert.That(rig.Movement.wallClimb.ladder, Is.SameAs(ladder), "Test setup: the character should be on the ladder.");
            float size = rig.Controller.CurrentScale.y;
            float NearestRung(Transform bone)
            {
                float up = ladder.HeightOf(bone.position) / ladder.RungSpacing - 1f;
                float k = Mathf.Clamp(Mathf.Round(up), 0f, ladder.RungCount - 1);
                return Mathf.Abs(ladder.HeightOf(bone.position) - ladder.RungHeight((int)k));
            }
            foreach (HumanBodyBones hand in new[] { HumanBodyBones.LeftHand, HumanBodyBones.RightHand })
            {
                Transform bone = animator.GetBoneTransform(hand);
                Assert.That(NearestRung(bone), Is.LessThan(0.08f * size), $"The {hand} should hold a rung.");
                Assert.That(ladder.InFrontOf(bone.position), Is.InRange(-0.02f, 0.12f * size), $"The {hand} should be at the ladder's front.");
            }
            foreach (HumanBodyBones foot in new[] { HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot })
            {
                Transform bone = animator.GetBoneTransform(foot);
                Assert.That(NearestRung(bone), Is.LessThan(0.15f * size), $"The {foot} should stand on a rung.");
                Assert.That(ladder.InFrontOf(bone.position), Is.InRange(-0.02f, 0.2f * size), $"The {foot} should be at the ladder's front.");
            }
            // The hands on different rungs, and the feet: a climbing pose, not hanging square.
            float left = ladder.HeightOf(animator.GetBoneTransform(HumanBodyBones.LeftHand).position);
            float right = ladder.HeightOf(animator.GetBoneTransform(HumanBodyBones.RightHand).position);
            Assert.That(Mathf.Abs(left - right), Is.GreaterThan(0.5f * ladder.RungSpacing), "The hands should hold different rungs.");
        }

        [UnityTest]
        public IEnumerator SwingingOnARope_PlaysRopeSwing_WithTheHandsOnTheRope()
        {
            yield return Spawn();
            MeridianMotion.Levels.SwingRope rope = rig.Rope(new Vector3(0f, 10f, 0f), 8f);
            float grip = rig.MovementSetting<float>("ropeGripHeight") * rig.StandingHeight;
            rig.Teleport(new Vector3(0f, 6f - grip, -0.2f));
            yield return ExpectState(HumanoidAnimatorStates.RopeSwing, 0.6f, _ => CharacterTestRig.Idle());
            yield return rig.Run(0.4f, _ => CharacterTestRig.Idle(), StepAnimator);
            Assert.That(rig.InState(CharacterState.RopeSwinging), Is.True, "Test setup: the character should be on the rope.");

            float size = rig.Controller.CurrentScale.y;
            Vector3 tip = rope.Anchor + rope.Direction * rope.Length;
            foreach (HumanBodyBones bone in new[] { HumanBodyBones.LeftHand, HumanBodyBones.RightHand })
            {
                Transform hand = animator.GetBoneTransform(bone);
                Assert.That(DistanceToSegment(hand.position, rope.Anchor, tip), Is.LessThan(0.1f * size), $"The {bone} should hold the rope.");
            }
            float right = Vector3.Dot(animator.GetBoneTransform(HumanBodyBones.RightHand).position - rope.Anchor, -rope.Direction);
            float left = Vector3.Dot(animator.GetBoneTransform(HumanBodyBones.LeftHand).position - rope.Anchor, -rope.Direction);
            Assert.That(right - left, Is.GreaterThan(0.1f * size), "The right hand should hold the rope above the left.");
        }

        [UnityTest]
        public IEnumerator RidingAZipline_PlaysZipline_WithBothHandsOnTheCable()
        {
            yield return Spawn();
            MeridianMotion.Levels.ZiplineCable line = rig.Zipline(new Vector3(0f, 9f, 0f), new Vector3(0f, 5f, 40f));
            float hang = rig.MovementSetting<float>("ziplineHangHeight") * rig.StandingHeight;
            rig.Teleport(line.PointAt(2f) - Vector3.up * (hang - 0.15f));
            yield return ExpectState(HumanoidAnimatorStates.Zipline, 0.6f, _ => CharacterTestRig.Idle());
            yield return rig.Run(0.4f, _ => CharacterTestRig.Idle(), StepAnimator);
            Assert.That(rig.InState(CharacterState.Ziplining), Is.True, "Test setup: the character should be on the zipline.");

            float size = rig.Controller.CurrentScale.y;
            foreach (HumanBodyBones bone in new[] { HumanBodyBones.LeftHand, HumanBodyBones.RightHand })
            {
                Transform hand = animator.GetBoneTransform(bone);
                Assert.That(DistanceToSegment(hand.position, line.Start, line.End), Is.LessThan(0.1f * size), $"The {bone} should hold the cable.");
            }
        }
    }
}
