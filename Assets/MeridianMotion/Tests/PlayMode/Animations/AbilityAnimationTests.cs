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
    /// Checks that the movement abilities show on the character's own model, through the shared Animator Controller
    /// (built from Mannequin Layout.asset): crouching, sliding, rolling, wall-running, hanging and climbing onto a ledge,
    /// vaulting and sliding down a wall each play their state, a crouched model fits its lowered body, a hanging model's
    /// hands reach up to the ledge, and the model moves smoothly into and out of them. (Getting about the level is
    /// TraversalAnimationTests', the water WaterAnimationTests'.) In the SampleContent category, as they check the
    /// package's own controller and model.
    /// </summary>
    [Category(TestArea.Animation)]
    [Category("SampleContent")]
    public class AbilityAnimationTests : AbilityAnimationTestsBase
    {
        [UnityTest]
        public IEnumerator Crouching_PlaysCrouch_AndTheModelFitsItsLoweredBody()
        {
            yield return Spawn();
            yield return ExpectState(HumanoidAnimatorStates.Crouch, 1f, step => step == 0 ? CharacterTestRig.Crouch() : CharacterTestRig.Idle());
            float top = HighestPointOfModel() - rig.Position.y;
            Assert.That(top, Is.LessThan(rig.CurrentHeight + 0.05f), "A crouching model should fit inside its crouched body.");

            yield return ExpectState(HumanoidAnimatorStates.Crouch, 0.6f, _ => CharacterTestRig.Move(Vector3.forward));
        }

        [UnityTest]
        public IEnumerator Sliding_PlaysSlide()
        {
            yield return Spawn();
            yield return rig.Run(0.4f, step => CharacterTestRig.Sprint(Vector3.forward), StepAnimator);
            yield return ExpectState(HumanoidAnimatorStates.Slide, 0.5f, step =>
            {
                StepInputs inputs = CharacterTestRig.Sprint(Vector3.forward);
                if (step == 0) { inputs.crouch = CharacterTestRig.Held(); }
                return inputs;
            });
        }

        [UnityTest]
        public IEnumerator Rolling_PlaysRoll_ThroughOnceOverTheRoll()
        {
            yield return Spawn();
            int rollSteps = CharacterTestRig.StepsFor(rig.Component<Rolling>().RollDuration);
            bool played = false;
            float halfway = -1f;
            yield return rig.Run(rollSteps, step => step == 0 ? CharacterTestRig.Roll() : CharacterTestRig.Idle(), step =>
            {
                StepAnimator(step);
                played |= InAnimatorState(HumanoidAnimatorStates.Roll);
                if (step == rollSteps / 2) { halfway = animator.GetFloat("RollProgress"); }
            });
            Assert.That(played, Is.True, $"The 'Roll' state should play (the Animator is in '{CurrentAnimatorState()}').");
            Assert.That(halfway, Is.EqualTo(0.5f).Within(0.1f), "The roll's animation should follow how far through the roll the character is.");
        }

        [UnityTest]
        public IEnumerator RollingWhileMoving_BlendsIntoTheStride_AsTheRollEnds()
        {
            yield return Spawn();
            int rollSteps = CharacterTestRig.StepsFor(rig.Component<Rolling>().RollDuration);
            int lastRollingStep = -1, inLocomotionAt = -1;
            string other = null;
            yield return rig.Run(rollSteps + 20, step => step == 0 ? CharacterTestRig.Roll(Vector3.forward) : CharacterTestRig.Move(Vector3.forward), step =>
            {
                StepAnimator(step);
                if (rig.InState(CharacterState.Rolling)) { lastRollingStep = step; }
                bool locomotion = InAnimatorState(HumanoidAnimatorStates.Locomotion);
                if (locomotion && inLocomotionAt < 0 && step > 2) { inLocomotionAt = step; }
                if (step > 2 && !locomotion && !InAnimatorState(HumanoidAnimatorStates.Roll)) { other ??= CurrentAnimatorState(); }
            });
            Assert.That(other, Is.Null, $"Walking on out of a roll should go straight from the roll to walking, not through '{other}'.");
            Assert.That(inLocomotionAt, Is.GreaterThan(0), "The roll should hand over to walking.");
            Assert.That(inLocomotionAt, Is.LessThanOrEqualTo(lastRollingStep), "The blend into walking should start before the roll ends, not after.");
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName(HumanoidAnimatorStates.Locomotion), Is.True);
        }

        [UnityTest]
        public IEnumerator WallRunning_PlaysWallRun_ForAWallOnThatSide([Values(1f, -1f)] float side)
        {
            yield return Spawn();
            rig.Body.position = new Vector3(0f, 6f, -10f);
            float face = rig.FrontExtent + 0.1f;
            rig.TraversableBox("Wall", new Vector3(side * (face + 0.5f), 10f, 0f), new Vector3(1f, 20f, 40f), MeridianMotion.Levels.TraversableWall.WallMove.WallRun);
            float speed = rig.Component<WallRunning>().WallRunSpeed * rig.Movement.SpeedScale;
            rig.Movement.OverrideVelocity(Vector3.forward * speed);
            yield return ExpectState(HumanoidAnimatorStates.WallRun, 0.4f, _ => CharacterTestRig.Move(Vector3.forward));
            yield return rig.Run(0.2f, _ => CharacterTestRig.Move(Vector3.forward), StepAnimator);
            Assert.That(rig.InState(CharacterState.WallRunning), Is.True, "Test setup: the character should still be wall-running.");
            Assert.That(animator.GetFloat("WallSide"), Is.EqualTo(side), "The Animator should be told which side the wall is on.");

            // The wall run for that side: the feet out toward the wall, the head leaning away from it (where the
            // libraries' wall runs are installed; the sprint fallback keeps both over the hips).
            if (QuaterniusWallRunsInstalled())
            {
                Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                float feet = 0.5f * (animator.GetBoneTransform(HumanBodyBones.LeftFoot).position.x + animator.GetBoneTransform(HumanBodyBones.RightFoot).position.x) - hips.position.x;
                float head = animator.GetBoneTransform(HumanBodyBones.Head).position.x - hips.position.x;
                // (On the wall: see WallRunning_KeepsTheModelOutOfTheWall.)
                Assert.That(feet * side, Is.GreaterThan(0.05f), "The feet should be out toward the wall.");
                Assert.That(head * side, Is.LessThan(-0.03f), "The body should lean away from the wall.");
            }
        }

        /// <summary>
        /// Running along a wall, the model stays out of it: its body held its clearance out from the wall (the wall run's
        /// feet are well out to the side), and its feet and hands put on the wall's face where the animation has them
        /// through it. With a slim body (one fitted to the model), the legs and a hand were drawn through the wall.
        /// </summary>
        [UnityTest]
        public IEnumerator WallRunning_KeepsTheModelOutOfTheWall([Values(false, true)] bool wideBody)
        {
            yield return Spawn();
            if (wideBody) { Assume.That(rig.Movement.SetBody(BodyShape.Capsule(0.5f, 2f), checkRoom: false), Is.True); }
            rig.Body.position = new Vector3(0f, 6f, -10f);
            float face = rig.SideExtent + 0.1f;
            rig.TraversableBox("Wall", new Vector3(face + 0.5f, 10f, 0f), new Vector3(1f, 20f, 40f), MeridianMotion.Levels.TraversableWall.WallMove.WallRun);
            rig.Movement.OverrideVelocity(Vector3.forward * rig.Component<WallRunning>().WallRunSpeed * rig.Movement.SpeedScale);
            yield return ExpectState(HumanoidAnimatorStates.WallRun, 0.4f, _ => CharacterTestRig.Move(Vector3.forward));
            HumanBodyBones[] bones = { HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot, HumanBodyBones.LeftHand, HumanBodyBones.RightHand, HumanBodyBones.Hips, HumanBodyBones.Head };
            float deepest = float.NegativeInfinity;
            string deepestBone = "";
            yield return rig.Run(0.8f, _ => CharacterTestRig.Move(Vector3.forward), step =>
            {
                StepAnimator(step);
                rig.Animation.SmoothRotateVisuals(Time.fixedDeltaTime, Quaternion.identity);
                if (step < 10) { return; } // Once it's settled at its distance from the wall.
                foreach (HumanBodyBones bone in bones)
                {
                    Transform t = animator.GetBoneTransform(bone);
                    if (t != null && t.position.x - face > deepest) { deepest = t.position.x - face; deepestBone = bone.ToString(); }
                }
            });
            Assume.That(rig.InState(CharacterState.WallRunning), Is.True, "Test setup: the character should still be wall-running.");
            float clearance = rig.MovementSetting<float>("wallRunClearance") * rig.StandingHeight;
            Assert.That(face - rig.Position.x, Is.EqualTo(Mathf.Max(clearance, rig.SideExtent)).Within(0.03f),
                "The character should be held its clearance out from the wall, or against it.");
            Assert.That(deepest, Is.LessThan(0.02f), $"The model's {deepestBone} shouldn't be drawn through the wall.");
        }

        private static bool QuaterniusWallRunsInstalled()
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.FindAssets("UAL2 t:Model", new[] { "Assets" }).Length > 0
                   && System.Array.Exists(UnityEditor.AssetDatabase.LoadAllAssetsAtPath(UnityEditor.AssetDatabase.GUIDToAssetPath(
                       UnityEditor.AssetDatabase.FindAssets("UAL2 t:Model", new[] { "Assets" })[0])), a => a is AnimationClip c && c.name == "WallRun_R_Loop");
#else
            return false;
#endif
        }

        [UnityTest]
        public IEnumerator HangingFromALedge_PlaysHang_WithTheHandsUpAtTheLedge_ThenClimbs()
        {
            yield return Spawn();
            rig.Body.position = new Vector3(0f, 3f, 0f);
            float top = 3f + rig.StandingHeight, face = rig.FrontExtent + 0.15f;
            rig.Box("Ledge Block", new Vector3(0f, top / 2f, face + 2f), new Vector3(10f, top, 4f));
            yield return ExpectState(HumanoidAnimatorStates.Hang, 0.6f, _ => CharacterTestRig.Move(Vector3.forward));
            yield return rig.Run(0.4f, _ => CharacterTestRig.Idle(), StepAnimator);

            // The hands hold the ledge: the wrists on its top, just back from its edge, either side of the character, the
            // fingers pointing over the edge; and the body hangs against the wall.
            float size = rig.Controller.CurrentScale.y, tolerance = 0.03f * size;
            Transform left = animator.GetBoneTransform(HumanBodyBones.LeftHand), right = animator.GetBoneTransform(HumanBodyBones.RightHand);
            Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            Assert.That(left != null && right != null && hips != null, "Test setup: the model should have hands and hips.");
            foreach (Transform hand in new[] { left, right })
            {
                Assert.That(hand.position.y, Is.EqualTo(top + 0.03f * size).Within(tolerance), $"The {hand.name} should rest on the ledge's top.");
                Assert.That(hand.position.z - face, Is.EqualTo(0.04f * size).Within(tolerance), $"The {hand.name} should be just back from the ledge's edge.");
                // Its first finger bone (not its hitbox).
                Transform finger = hand.Cast<Transform>().FirstOrDefault(child => !child.TryGetComponent(out BoneHitbox _));
                if (finger != null)
                {
                    Vector3 fingers = finger.position - hand.position;
                    Assert.That(Vector3.Dot(new Vector3(fingers.x, 0f, fingers.z).normalized, Vector3.forward), Is.GreaterThan(0.7f),
                        $"The {hand.name}'s fingers should point over the edge, into the ledge.");
                }
            }
            Assert.That(left.position.x, Is.LessThan(rig.Position.x - 0.1f), "The left hand should hold the ledge to the character's left.");
            Assert.That(right.position.x, Is.GreaterThan(rig.Position.x + 0.1f), "The right hand should hold the ledge to the character's right.");
            // As far apart as the Quaternius library's climbs grip ledges (0.75 to 1.4 m on the mannequin).
            float grip = Vector3.Distance(left.position, right.position) / size;
            Assert.That(grip, Is.InRange(0.7f, 1.4f), "The hands should grip the ledge about as far apart as the library's climbs do.");
            Assert.That(face - hips.position.z, Is.LessThan(0.25f * size), "The body should hang against the wall, not held a body's width away from it.");
            Assert.That(face - hips.position.z, Is.GreaterThan(0.05f * size), "The body shouldn't be pushed into the wall.");

            yield return ExpectState(HumanoidAnimatorStates.Climb, 0.4f, step => step == 0 ? CharacterTestRig.Jump() : CharacterTestRig.Idle());
        }

        [UnityTest]
        public IEnumerator ClimbingOntoALowLedge_StaysInClimb_ThoughCrouched()
        {
            yield return Spawn();
            rig.Body.position = new Vector3(0f, 3f, 0f);
            float top = 3f + rig.StandingHeight, face = rig.FrontExtent + 0.15f;
            rig.Box("Ledge Block", new Vector3(0f, top / 2f, face + 2f), new Vector3(10f, top, 4f));
            float clearance = rig.StandingHeight * Mathf.Lerp(rig.Component<Crouching>().CrouchHeight, 1f, 0.5f);
            rig.Box("Low Ceiling", new Vector3(0f, top + clearance + 0.5f, face + 2f), new Vector3(10f, 1f, 4f));
            yield return ExpectState(HumanoidAnimatorStates.Hang, 0.6f, _ => CharacterTestRig.Move(Vector3.forward));

            // Through the climb (crouched, as there's only room to crouch on top), the Animator stays in Climb.
            int climbingSteps = 0, outOfClimb = 0;
            yield return rig.Run(rig.Component<Ledges>().LedgeClimbDuration, step => step == 0 ? CharacterTestRig.Jump() : CharacterTestRig.Idle(), step =>
            {
                StepAnimator(step);
                if (!rig.InState(CharacterState.LedgeClimbing) || step < 4) { return; }
                climbingSteps++;
                if (!InAnimatorState(HumanoidAnimatorStates.Climb)) { outOfClimb++; }
            });
            Assert.That(climbingSteps, Is.GreaterThan(10), "Test setup: the character should climb.");
            Assert.That(rig.Movement.crouch.IsCrouched, Is.True, "Test setup: the climb should be crouched.");
            Assert.That(outOfClimb, Is.Zero, "A crouched climb shouldn't leave the Climb state.");
        }

        /// <summary>
        /// Sprinting over a low wall plays the vault, and through the middle of the vault the planted hand is on the wall's
        /// top, the body lowered for it to reach (rather than floating over the top, held clear of it by the character's
        /// body). That lowering eases down and back up over time: the vault swings over the top in a moment, its progress
        /// racing through the end of the hand's planting, and eased by progress alone the body popped back up a third of
        /// a metre in a frame or two.
        /// </summary>
        [UnityTest]
        public IEnumerator VaultingOverALowWall_PlaysVault_WithTheHandOnTheTop_AndTheHipsDontJump()
        {
            yield return Spawn();
            float share = Mathf.Lerp(rig.StepHeight / rig.StandingHeight, rig.MovementSetting<float>("ledgeGrabMinHeight"), 0.5f);
            float top = share * rig.StandingHeight;
            rig.Box("Low Wall", new Vector3(0f, top / 2f, 3f), new Vector3(10f, top, 0.4f));
            Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips), parent = rig.Models.ModelParent;
            Transform hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            bool vaulted = false, played = false;
            float biggestJump = 0f, closest = float.PositiveInfinity, size = rig.Controller.CurrentScale.y;
            Vector3 last = hips.position - parent.position;
            yield return rig.Run(1.4f, step => CharacterTestRig.Sprint(Vector3.forward), step =>
            {
                rig.Animation.SmoothRotateVisuals(Time.fixedDeltaTime, Quaternion.identity);
                StepAnimator(step);
                played |= InAnimatorState(HumanoidAnimatorStates.Vault);
                Vector3 inModel = hips.position - parent.position;
                bool vaulting = rig.InState(CharacterState.Vaulting);
                vaulted |= vaulting;
                if (vaulting) { biggestJump = Mathf.Max(biggestJump, Mathf.Abs(inModel.y - last.y)); }
                last = inModel;
                float progress = rig.Movement.vault.Progress;
                if (vaulting && progress > 0.35f && progress < 0.55f)
                {
                    closest = Mathf.Min(closest, Mathf.Abs(hand.position.y - top));
                }
            });
            Assert.That(vaulted, Is.True, "Test setup: the character should vault over the wall.");
            Assert.That(played, Is.True, $"The 'Vault' state should play (the Animator is in '{CurrentAnimatorState()}').");
            Assert.That(closest, Is.LessThan(0.08f * size), "Mid-vault, the planted hand should be on the wall's top.");
            Assert.That(biggestJump, Is.LessThan(0.12f * size), "Vaulting, the hips shouldn't jump up or down within the model.");
        }

        [UnityTest]
        public IEnumerator SlidingDownAWall_PlaysWallSlide_WithTheHandsOnTheWallAndTheBodyAgainstIt()
        {
            yield return Spawn();
            rig.Body.position = new Vector3(0f, 8f, 0f);
            float face = rig.FrontExtent + 0.1f;
            rig.TraversableBox("Wall", new Vector3(0f, 15f, face + 0.5f), new Vector3(10f, 30f, 1f), MeridianMotion.Levels.TraversableWall.WallMove.WallSlide);
            yield return ExpectState(HumanoidAnimatorStates.WallSlide, 0.4f, _ => CharacterTestRig.Move(Vector3.forward));
            yield return rig.Run(0.3f, _ => CharacterTestRig.Move(Vector3.forward), StepAnimator);
            Assert.That(rig.InState(CharacterState.WallSliding), Is.True, "Test setup: the character should still be sliding.");

            // As the Quaternius library's climbs hold hands against a wall: on it, about a metre apart, at head height.
            float size = rig.Controller.CurrentScale.y;
            Transform left = animator.GetBoneTransform(HumanBodyBones.LeftHand), right = animator.GetBoneTransform(HumanBodyBones.RightHand);
            Transform head = animator.GetBoneTransform(HumanBodyBones.Head), hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            foreach (Transform hand in new[] { left, right })
            {
                Assert.That(face - hand.position.z, Is.EqualTo(0.03f * size).Within(0.03f * size), $"The {hand.name} should be flat on the wall.");
                Assert.That(hand.position.y - head.position.y, Is.InRange(-0.2f * size, 0.2f * size), $"The {hand.name} should be at about head height.");
            }
            Assert.That(Vector3.Distance(left.position, right.position) / size, Is.InRange(0.7f, 1.4f), "The hands should be spread about as wide as the library's climbs hold them.");
            Assert.That(face - hips.position.z, Is.LessThan(0.3f * size), "The body should be against the wall, not held a body's width away from it.");
        }

        [UnityTest]
        public IEnumerator LandingFromAHighFallWhileMoving_PlaysRoll()
        {
            yield return Spawn();
            float fall = 1.2f * rig.MovementSetting<float>("landingRollMinFall") * rig.StandingHeight;
            rig.Floor();
            rig.Body.position = new Vector3(0f, fall, 0f);
            yield return rig.Launch(Vector3.right * 5f, CharacterTestRig.Move(Vector3.right));
            yield return ExpectState(HumanoidAnimatorStates.Roll, 2f, _ => CharacterTestRig.Move(Vector3.right));
        }

        /// <summary>
        /// Climbing up onto a ledge and walking on, the model doesn't jump as the climb hands over: the climb's animation
        /// is held where it ended while the Animator blends out of it. Its progress went back to 0, and the climb's clip
        /// to its start for the blend, so the hips jumped forward and back as the character stepped off the top.
        /// </summary>
        [UnityTest]
        public IEnumerator ClimbingOntoALedgeAndWalkingOn_TheHipsDontJumpAsTheClimbEnds()
        {
            yield return Spawn();
            rig.Body.position = new Vector3(0f, 3f, 0f);
            float top = 3f + rig.StandingHeight, face = rig.FrontExtent + 0.15f;
            rig.Box("Ledge Block", new Vector3(0f, top / 2f, face + 2f), new Vector3(10f, top, 4f));
            yield return ExpectState(HumanoidAnimatorStates.Hang, 0.8f, _ => CharacterTestRig.Move(Vector3.forward));
            Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips), parent = rig.Models.ModelParent;
            bool climbed = false;
            int afterClimb = 0;
            float biggestJump = 0f;
            Vector3 last = hips.position - parent.position;
            yield return rig.Run(1.8f, step => step == 0 ? CharacterTestRig.Jump(Vector3.forward) : CharacterTestRig.Move(Vector3.forward), step =>
            {
                rig.Animation.SmoothRotateVisuals(Time.fixedDeltaTime, Quaternion.identity);
                StepAnimator(step);
                bool climbing = rig.InState(CharacterState.LedgeClimbing);
                climbed |= climbing;
                Vector3 inModel = hips.position - parent.position;
                // Over the steps from the climb's end, as the Animator blends out of it.
                if (climbed && !climbing && afterClimb++ < 15) { biggestJump = Mathf.Max(biggestJump, Vector3.Distance(inModel, last)); }
                last = inModel;
            });
            Assert.That(climbed, Is.True, "Test setup: the character should climb onto the ledge.");
            Assert.That(biggestJump, Is.LessThan(0.08f * rig.Controller.CurrentScale.y), "As the climb ends, the hips shouldn't jump within the model.");
        }

        /// <summary>
        /// Sprinting over a low wall and running on, the model moves smoothly from the run into the vault and out of it:
        /// the vault's animation plays over at least its Vault Min Duration (finishing after a fast vault, as the
        /// character runs on), so it doesn't race through the swing over the top, the hands and feet whipping round and
        /// snapping into the run; and it blends in over long enough that the stride's feet don't snap to the vault's.
        /// </summary>
        [UnityTest]
        public IEnumerator SprintingOverALowWall_TheModelMovesSmoothlyIntoAndOutOfTheVault()
        {
            yield return Spawn();
            // The model eases over the vault each frame (see MMotionAnimator), which this measures: Unity's frames.
            rig.StepsPhysicsItself = false;
            float share = Mathf.Lerp(rig.StepHeight / rig.StandingHeight, rig.MovementSetting<float>("ledgeGrabMinHeight"), 0.5f);
            float top = share * rig.StandingHeight;
            rig.Box("Low Wall", new Vector3(0f, top / 2f, 4.2f), new Vector3(10f, top, 0.4f));
            HumanBodyBones[] bones = { HumanBodyBones.Hips, HumanBodyBones.Head, HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot, HumanBodyBones.LeftHand, HumanBodyBones.RightHand };
            Transform parent = rig.Models.ModelParent;
            var last = new Vector3[bones.Length];
            bool vaulted = false;
            int after = 0;
            float biggest = 0f;
            string which = "";
            yield return rig.Run(1.6f, step => CharacterTestRig.Sprint(Vector3.forward), step =>
            {
                rig.Animation.SmoothRotateVisuals(Time.fixedDeltaTime, Quaternion.identity);
                StepAnimator(step);
                bool vaulting = rig.InState(CharacterState.Vaulting);
                // Through the vault, and as its animation finishes and blends into what's next (not the landing after).
                bool measured = step > 0 && (vaulting || (vaulted && after++ < 12));
                vaulted |= vaulting;
                for (int b = 0; b < bones.Length; b++)
                {
                    Vector3 local = parent.InverseTransformPoint(animator.GetBoneTransform(bones[b]).position);
                    float moved = Vector3.Distance(local, last[b]);
                    if (measured && moved > biggest) { biggest = moved; which = bones[b].ToString(); }
                    last[b] = local;
                }
            });
            Assert.That(vaulted, Is.True, "Test setup: the character should vault the wall.");
            Assert.That(biggest, Is.LessThan(0.35f * rig.Controller.CurrentScale.y), $"The model's {which} shouldn't jump within it from one step to the next.");
        }

        [UnityTest]
        public IEnumerator RollingFromACrouch_BlendsIntoTheCrouch_WithoutStandingUp()
        {
            yield return Spawn();
            rig.Floor();
            yield return ExpectState(HumanoidAnimatorStates.Crouch, 0.5f, step => step == 0 ? CharacterTestRig.Crouch() : CharacterTestRig.Idle());
            int rollSteps = CharacterTestRig.StepsFor(rig.Component<Rolling>().RollDuration * rig.Movement.DurationScale);
            bool rolled = false, stood = false;
            yield return rig.Run(rollSteps + CharacterTestRig.StepsFor(0.6f),
                step => step == 0 ? CharacterTestRig.Roll(Vector3.forward) : CharacterTestRig.Move(Vector3.forward), step =>
                {
                    StepAnimator(step);
                    rolled |= InAnimatorState(HumanoidAnimatorStates.Roll);
                    stood |= InAnimatorState(HumanoidAnimatorStates.Locomotion);
                });
            Assert.That(rolled, Is.True, "Test setup: the roll should play.");
            Assert.That(rig.InState(CharacterState.Crouching), Is.True, "Test setup: the roll should end crouched.");
            Assert.That(stood, Is.False, $"A roll from a crouch shouldn't stand up before crouching again (the Animator is in '{CurrentAnimatorState()}').");
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName(HumanoidAnimatorStates.Crouch), Is.True, "The roll should end in the crouch.");
        }
    }
}
