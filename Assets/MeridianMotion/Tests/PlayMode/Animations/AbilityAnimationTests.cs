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
    /// (built from Mannequin Layout.asset): crouching, sliding, rolling, wall-running, hanging, climbing, vaulting, pushing, swinging on a
    /// rope, riding a zipline and swimming each play their state, a crouched model fits its lowered body, a hanging
    /// model's hands reach up to the ledge, the rope or the cable, and a swimmer's head is just out of the water. In the
    /// SampleContent category, as they check the package's own controller and model.
    /// </summary>
    [Category(TestArea.Animation)]
    [Category("SampleContent")]
    public class AbilityAnimationTests : AnimationTestsBase
    {
        /// <summary>Whether the base layer is in the named state, or on its way into it.</summary>
        private bool InAnimatorState(string state) =>
            animator.GetCurrentAnimatorStateInfo(0).IsName(state) || (animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).IsName(state));

        /// <summary>Every state the generator makes (HumanoidAnimatorStates), for naming the one playing.</summary>
        private static readonly string[] AllStates = typeof(HumanoidAnimatorStates)
            .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()).ToArray();

        private string CurrentAnimatorState()
        {
            foreach (string name in AllStates)
            {
                if (animator.GetCurrentAnimatorStateInfo(0).IsName(name)) { return name; }
            }
            return "(another state)";
        }

        /// <summary>Spawns the character with its own model, its animation stepped by hand with the physics.</summary>
        private IEnumerator Spawn()
        {
            yield return SpawnWithOwnModel();
            TakeOverAnimator();
            yield return rig.Run(0.3f, _ => CharacterTestRig.Idle(), StepAnimator);
        }

        /// <summary>Runs the given inputs, stepping the animation, and asserts the named state played at some point.</summary>
        private IEnumerator ExpectState(string state, float seconds, System.Func<int, StepInputs> inputs)
        {
            bool played = false;
            yield return rig.Run(seconds, inputs, step => { StepAnimator(step); played |= InAnimatorState(state); });
            Assert.That(played, Is.True, $"The '{state}' state should play (the Animator is in '{CurrentAnimatorState()}').");
        }

        /// <summary>The highest point of the model's meshes as they're posed now, in world space.</summary>
        private float HighestPointOfModel()
        {
            float highest = float.NegativeInfinity;
            var baked = new Mesh();
            foreach (SkinnedMeshRenderer skinned in rig.Models.ModelRoot.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                skinned.BakeMesh(baked, false);
                Matrix4x4 toWorld = Matrix4x4.TRS(skinned.transform.position, skinned.transform.rotation, Vector3.one);
                foreach (Vector3 vertex in baked.vertices) { highest = Mathf.Max(highest, toWorld.MultiplyPoint3x4(vertex).y); }
            }
            Object.Destroy(baked);
            return highest;
        }

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
            rig.Box("Wall", new Vector3(side * (face + 0.5f), 10f, 0f), new Vector3(1f, 20f, 40f));
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
            rig.Box("Wall", new Vector3(face + 0.5f, 10f, 0f), new Vector3(1f, 20f, 40f));
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
        public IEnumerator SlidingDownAWall_PlaysWallSlide_WithTheHandsOnTheWallAndTheBodyAgainstIt()
        {
            yield return Spawn();
            rig.Body.position = new Vector3(0f, 8f, 0f);
            float face = rig.FrontExtent + 0.1f;
            rig.Box("Wall", new Vector3(0f, 15f, face + 0.5f), new Vector3(10f, 30f, 1f));
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
        public IEnumerator LandingFromAHighFallWhileMoving_PlaysRoll()
        {
            yield return Spawn();
            float fall = 1.2f * rig.MovementSetting<float>("landingRollMinFall") * rig.StandingHeight;
            rig.Floor();
            rig.Body.position = new Vector3(0f, fall, 0f);
            yield return rig.Launch(Vector3.right * 5f, CharacterTestRig.Move(Vector3.right));
            yield return ExpectState(HumanoidAnimatorStates.Roll, 2f, _ => CharacterTestRig.Move(Vector3.right));
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

        /// <summary>How far a point is from the line between two points (the segment between them).</summary>
        private static float DistanceToSegment(Vector3 point, Vector3 from, Vector3 to)
        {
            Vector3 along = to - from;
            float t = Mathf.Clamp01(Vector3.Dot(point - from, along) / Mathf.Max(along.sqrMagnitude, 1e-8f));
            return Vector3.Distance(point, from + along * t);
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

        /// <summary>The largest change in speed (m/s²) between physics steps of a height over the steps.</summary>
        private static float PeakJolt(List<float> heights)
        {
            float dt = Time.fixedDeltaTime, peak = 0f;
            for (int i = 2; i < heights.Count; i++) { peak = Mathf.Max(peak, Mathf.Abs(heights[i] - 2f * heights[i - 1] + heights[i - 2]) / (dt * dt)); }
            return peak;
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
