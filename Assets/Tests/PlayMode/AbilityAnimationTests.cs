using System.Collections;
using Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CharacterShowcase.Tests
{
    /// <summary>
    /// Checks that the movement abilities show on the character's own model, through the shared Animator Controller
    /// (MannequinGenerator): crouching, sliding, rolling, wall-running, hanging, climbing and pushing each play their
    /// state, a crouched model fits its lowered collider, and a hanging model's hands reach up to the ledge. In the
    /// SampleContent category, as they check the package's own controller and model.
    /// </summary>
    [Category("SampleContent")]
    public class AbilityAnimationTests : AnimationTestsBase
    {
        /// <summary>Whether the base layer is in the named state, or on its way into it.</summary>
        private bool InAnimatorState(string state) =>
            animator.GetCurrentAnimatorStateInfo(0).IsName(state) || (animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).IsName(state));

        private string CurrentAnimatorState()
        {
            foreach (string name in new[] { "Locomotion", "Airborne", "Jump", "Land", "Dash", "Crouch", "Slide", "Roll", "Wall Run", "Hang", "Climb", "Push" })
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
        private IEnumerator ExpectState(string state, float seconds, System.Func<int, Inputs> inputs)
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
            foreach (SkinnedMeshRenderer skinned in rig.Models.Model.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                skinned.BakeMesh(baked, false);
                Matrix4x4 toWorld = Matrix4x4.TRS(skinned.transform.position, skinned.transform.rotation, Vector3.one);
                foreach (Vector3 vertex in baked.vertices) { highest = Mathf.Max(highest, toWorld.MultiplyPoint3x4(vertex).y); }
            }
            Object.Destroy(baked);
            return highest;
        }

        [UnityTest]
        public IEnumerator Crouching_PlaysCrouch_AndTheModelFitsItsLoweredCollider()
        {
            yield return Spawn();
            yield return ExpectState("Crouch", 1f, step => step == 0 ? CharacterTestRig.Crouch(0) : CharacterTestRig.Idle());
            float top = HighestPointOfModel() - rig.Position.y;
            Assert.That(top, Is.LessThan(rig.CurrentHeight + 0.05f), "A crouching model should fit inside its crouched collider.");

            yield return ExpectState("Crouch", 0.6f, _ => CharacterTestRig.Move(Vector3.forward));
        }

        [UnityTest]
        public IEnumerator Sliding_PlaysSlide()
        {
            yield return Spawn();
            yield return rig.Run(0.4f, step => CharacterTestRig.Sprint(Vector3.forward, step), StepAnimator);
            yield return ExpectState("Slide", 0.5f, step =>
            {
                Inputs inputs = CharacterTestRig.Sprint(Vector3.forward, step + 100);
                if (step == 0) { inputs.crouch = CharacterTestRig.Held(0); }
                return inputs;
            });
        }

        [UnityTest]
        public IEnumerator Rolling_PlaysRoll_ThroughOnceOverTheRoll()
        {
            yield return Spawn();
            int rollSteps = CharacterTestRig.StepsFor(rig.Mode<RollMode>().rollDuration);
            bool played = false;
            float halfway = -1f;
            yield return rig.Run(rollSteps, step => step == 0 ? CharacterTestRig.Roll(0) : CharacterTestRig.Idle(), step =>
            {
                StepAnimator(step);
                played |= InAnimatorState("Roll");
                if (step == rollSteps / 2) { halfway = animator.GetFloat("RollProgress"); }
            });
            Assert.That(played, Is.True, $"The 'Roll' state should play (the Animator is in '{CurrentAnimatorState()}').");
            Assert.That(halfway, Is.EqualTo(0.5f).Within(0.1f), "The roll's animation should follow how far through the roll the character is.");
        }

        [UnityTest]
        public IEnumerator RollingWhileMoving_BlendsIntoTheStride_AsTheRollEnds()
        {
            yield return Spawn();
            int rollSteps = CharacterTestRig.StepsFor(rig.Mode<RollMode>().rollDuration);
            int lastRollingStep = -1, inLocomotionAt = -1;
            string other = null;
            yield return rig.Run(rollSteps + 20, step => step == 0 ? CharacterTestRig.Roll(0, Vector3.forward) : CharacterTestRig.Move(Vector3.forward), step =>
            {
                StepAnimator(step);
                if (rig.InState(CharacterState.Rolling)) { lastRollingStep = step; }
                bool locomotion = InAnimatorState("Locomotion");
                if (locomotion && inLocomotionAt < 0 && step > 2) { inLocomotionAt = step; }
                if (step > 2 && !locomotion && !InAnimatorState("Roll")) { other ??= CurrentAnimatorState(); }
            });
            Assert.That(other, Is.Null, $"Walking on out of a roll should go straight from the roll to walking, not through '{other}'.");
            Assert.That(inLocomotionAt, Is.GreaterThan(0), "The roll should hand over to walking.");
            Assert.That(inLocomotionAt, Is.LessThanOrEqualTo(lastRollingStep), "The blend into walking should start before the roll ends, not after.");
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Locomotion"), Is.True);
        }

        [UnityTest]
        public IEnumerator WallRunning_PlaysWallRun_ForAWallOnThatSide([Values(1f, -1f)] float side)
        {
            yield return Spawn();
            rig.Body.position = new Vector3(0f, 6f, -10f);
            float face = rig.Radius + 0.1f;
            rig.Box("Wall", new Vector3(side * (face + 0.5f), 10f, 0f), new Vector3(1f, 20f, 40f));
            float speed = rig.Mode<WallRunningMode>().wallRunSpeed * rig.Movement.SpeedScale;
            rig.Movement.OverrideVelocity(Vector3.forward * speed);
            yield return ExpectState("Wall Run", 0.4f, _ => CharacterTestRig.Move(Vector3.forward));
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
                Assert.That(feet * side, Is.GreaterThan(0.1f), "The feet should be out toward the wall.");
                Assert.That(head * side, Is.LessThan(-0.03f), "The body should lean away from the wall.");
            }
        }

        private static bool QuaterniusWallRunsInstalled()
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.FindAssets("UAL2 t:Model", new[] { "Assets/Animation/Quaternius" }).Length > 0
                   && System.Array.Exists(UnityEditor.AssetDatabase.LoadAllAssetsAtPath(UnityEditor.AssetDatabase.GUIDToAssetPath(
                       UnityEditor.AssetDatabase.FindAssets("UAL2 t:Model", new[] { "Assets/Animation/Quaternius" })[0])), a => a is AnimationClip c && c.name == "WallRun_R_Loop");
#else
            return false;
#endif
        }

        [UnityTest]
        public IEnumerator HangingFromALedge_PlaysHang_WithTheHandsUpAtTheLedge_ThenClimbs()
        {
            yield return Spawn();
            rig.Body.position = new Vector3(0f, 3f, 0f);
            float top = 3f + rig.StandingHeight, face = rig.Radius + 0.15f;
            rig.Box("Ledge Block", new Vector3(0f, top / 2f, face + 2f), new Vector3(10f, top, 4f));
            yield return ExpectState("Hang", 0.6f, _ => CharacterTestRig.Move(Vector3.forward));
            yield return rig.Run(0.4f, _ => CharacterTestRig.Idle(), StepAnimator);

            // The hands hold the ledge: the wrists on its top, just back from its edge, either side of the character, the
            // fingers pointing over the edge; and the body hangs against the wall.
            float size = rig.Controller.Scale.y, tolerance = 0.03f * size;
            Transform left = animator.GetBoneTransform(HumanBodyBones.LeftHand), right = animator.GetBoneTransform(HumanBodyBones.RightHand);
            Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            Assert.That(left != null && right != null && hips != null, "Test setup: the model should have hands and hips.");
            foreach (Transform hand in new[] { left, right })
            {
                Assert.That(hand.position.y, Is.EqualTo(top + 0.03f * size).Within(tolerance), $"The {hand.name} should rest on the ledge's top.");
                Assert.That(hand.position.z - face, Is.EqualTo(0.04f * size).Within(tolerance), $"The {hand.name} should be just back from the ledge's edge.");
                if (hand.childCount > 0)
                {
                    Vector3 fingers = hand.GetChild(0).position - hand.position;
                    Assert.That(Vector3.Dot(new Vector3(fingers.x, 0f, fingers.z).normalized, Vector3.forward), Is.GreaterThan(0.7f),
                        $"The {hand.name}'s fingers should point over the edge, into the ledge.");
                }
            }
            Assert.That(left.position.x, Is.LessThan(rig.Position.x - 0.1f), "The left hand should hold the ledge to the character's left.");
            Assert.That(right.position.x, Is.GreaterThan(rig.Position.x + 0.1f), "The right hand should hold the ledge to the character's right.");
            // As far apart as the Quaternius library's climbs grip ledges (0.75 to 1.4 m on the mannequin).
            float grip = Vector3.Distance(left.position, right.position) / size;
            Assert.That(grip, Is.InRange(0.7f, 1.4f), "The hands should grip the ledge about as far apart as the library's climbs do.");
            Assert.That(face - hips.position.z, Is.LessThan(0.25f * size), "The body should hang against the wall, not a collider's width away from it.");
            Assert.That(face - hips.position.z, Is.GreaterThan(0.05f * size), "The body shouldn't be pushed into the wall.");

            yield return ExpectState("Climb", 0.4f, step => step == 0 ? CharacterTestRig.Jump(0) : CharacterTestRig.Idle());
        }

        [UnityTest]
        public IEnumerator ClimbingOntoALowLedge_StaysInClimb_ThoughCrouched()
        {
            yield return Spawn();
            rig.Body.position = new Vector3(0f, 3f, 0f);
            float top = 3f + rig.StandingHeight, face = rig.Radius + 0.15f;
            rig.Box("Ledge Block", new Vector3(0f, top / 2f, face + 2f), new Vector3(10f, top, 4f));
            float clearance = rig.StandingHeight * Mathf.Lerp(rig.Mode<CrouchMode>().crouchHeight, 1f, 0.5f);
            rig.Box("Low Ceiling", new Vector3(0f, top + clearance + 0.5f, face + 2f), new Vector3(10f, 1f, 4f));
            yield return ExpectState("Hang", 0.6f, _ => CharacterTestRig.Move(Vector3.forward));

            // Through the climb (crouched, as there's only room to crouch on top), the Animator stays in Climb.
            int climbingSteps = 0, outOfClimb = 0;
            yield return rig.Run(rig.Mode<LedgeMode>().ledgeClimbDuration, step => step == 0 ? CharacterTestRig.Jump(0) : CharacterTestRig.Idle(), step =>
            {
                StepAnimator(step);
                if (!rig.InState(CharacterState.LedgeClimbing) || step < 4) { return; }
                climbingSteps++;
                if (!InAnimatorState("Climb")) { outOfClimb++; }
            });
            Assert.That(climbingSteps, Is.GreaterThan(10), "Test setup: the character should climb.");
            Assert.That(rig.Movement.crouch.IsCrouched, Is.True, "Test setup: the climb should be crouched.");
            Assert.That(outOfClimb, Is.Zero, "A crouched climb shouldn't leave the Climb state.");
        }

        [UnityTest]
        public IEnumerator SlidingDownAWall_PlaysWallSlide_WithTheHandsOnTheWallAndTheBodyAgainstIt()
        {
            yield return Spawn();
            rig.Body.position = new Vector3(0f, 8f, 0f);
            float face = rig.Radius + 0.1f;
            rig.Box("Wall", new Vector3(0f, 15f, face + 0.5f), new Vector3(10f, 30f, 1f));
            yield return ExpectState("Wall Slide", 0.4f, _ => CharacterTestRig.Move(Vector3.forward));
            yield return rig.Run(0.3f, _ => CharacterTestRig.Move(Vector3.forward), StepAnimator);
            Assert.That(rig.InState(CharacterState.WallSliding), Is.True, "Test setup: the character should still be sliding.");

            // As the Quaternius library's climbs hold hands against a wall: on it, about a metre apart, at head height.
            float size = rig.Controller.Scale.y;
            Transform left = animator.GetBoneTransform(HumanBodyBones.LeftHand), right = animator.GetBoneTransform(HumanBodyBones.RightHand);
            Transform head = animator.GetBoneTransform(HumanBodyBones.Head), hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            foreach (Transform hand in new[] { left, right })
            {
                Assert.That(face - hand.position.z, Is.EqualTo(0.03f * size).Within(0.03f * size), $"The {hand.name} should be flat on the wall.");
                Assert.That(hand.position.y - head.position.y, Is.InRange(-0.2f * size, 0.2f * size), $"The {hand.name} should be at about head height.");
            }
            Assert.That(Vector3.Distance(left.position, right.position) / size, Is.InRange(0.7f, 1.4f), "The hands should be spread about as wide as the library's climbs hold them.");
            Assert.That(face - hips.position.z, Is.LessThan(0.3f * size), "The body should be against the wall, not a collider's width away from it.");
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
                Inputs inputs = sprint ? CharacterTestRig.Sprint(Vector3.forward, step) : CharacterTestRig.Move(Vector3.forward);
                if (step >= 8 && step < 14) { inputs.jump = CharacterTestRig.Held(step - 8); }
                return inputs;
            }, step =>
            {
                StepAnimator(step);
                highest = Mathf.Max(highest, rig.Position.y);
                if (step > 14 && landedAt < 0 && rig.Grounded) { landedAt = step; }
                bool inAir = InAnimatorState("Airborne") || animator.GetCurrentAnimatorStateInfo(0).IsName("Jump");
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
            float near = rig.Position.z + rig.Radius + 0.1f;
            rig.PushableBox(new Vector3(0f, 0.75f, near + 0.75f), Vector3.one * 1.5f);
            yield return ExpectState("Push", 1f, step => CharacterTestRig.Grab(step, Vector3.forward));
        }
    }
}
