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
        public IEnumerator WallRunning_PlaysWallRun()
        {
            yield return Spawn();
            rig.Body.position = new Vector3(0f, 6f, -10f);
            float face = rig.Radius + 0.1f;
            rig.Box("Wall", new Vector3(face + 0.5f, 10f, 0f), new Vector3(1f, 20f, 40f));
            float speed = rig.Mode<WallRunningMode>().wallRunSpeed * rig.Movement.SpeedScale;
            rig.Movement.OverrideVelocity(Vector3.forward * speed);
            yield return ExpectState("Wall Run", 0.6f, _ => CharacterTestRig.Move(Vector3.forward));
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
        public IEnumerator PushingABox_PlaysPush()
        {
            yield return Spawn();
            float near = rig.Position.z + rig.Radius + 0.1f;
            rig.PushableBox(new Vector3(0f, 0.75f, near + 0.75f), Vector3.one * 1.5f);
            yield return ExpectState("Push", 1f, step => CharacterTestRig.Grab(step, Vector3.forward));
        }
    }
}
