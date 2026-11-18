using System;
using System.Collections;
using Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CharacterShowcase.Tests
{
    /// <summary>
    /// Tests that the character's per-step and per-frame work allocates nothing on the managed heap (so never makes work
    /// for the garbage collector, whose pauses are hitches): the movement step, the animation fed from it
    /// (AnimationController.UpdateAnimator), the Animator's update with the IK it runs, and the fox's paw placement
    /// (PawPlacementIK.Solve). Each character runs a course once, so everything made the first time (a buffer, a lazily
    /// built table, code compiled on first use) is made, then runs it again, measured. The animation is stepped by hand
    /// with the physics, as the animation tests do.
    /// </summary>
    [Category(TestArea.Movement)]
    [Category(TestArea.Animation)]
    [Category("Performance")]
    public class AllocationTests : AnimationTestsBase
    {
        private const string FoxPrefabPath = "Assets/Prefabs/Fox.prefab";

        // Where each part of the work allocated, while measured.
        private int animationAllocations, animatorAllocations, pawAllocations;
        private bool measuring;
        private PawPlacementIK paws;
        private CharacterState seen;

        [SetUp]
        public void ForgetTheLastTest()
        {
            animationAllocations = animatorAllocations = pawAllocations = 0;
            measuring = false;
            paws = null;
        }

        /// <summary>The managed allocations work makes, added to total while measuring (see AllocationCounter).</summary>
        private void Measure(Action work, ref int total)
        {
            if (!measuring) { work(); return; }
            AllocationCounter.Start();
            work();
            total += AllocationCounter.Stop();
        }

        /// <summary>After each step: the animation fed and stepped, then the paws placed (the fox's), each measured.</summary>
        private void AnimateAndMeasure(int step)
        {
            Measure(() => FeedAnimator(step), ref animationAllocations);
            Measure(() => animator.Update(Time.fixedDeltaTime), ref animatorAllocations);
            if (paws != null) { Measure(() => paws.Solve(Time.fixedDeltaTime), ref pawAllocations); }
            if (measuring) { seen |= rig.Movement.lastFrameMovementData.state; }
        }

        /// <summary>
        /// The course, run from the start: walking forward, a jump, sprinting, then walking into the climbable wall ahead
        /// and climbing it; then back to the start, turning on the spot to settle.
        /// </summary>
        private IEnumerator RunTheCourse()
        {
            yield return rig.Run(3f, step =>
            {
                if (step >= 20 && step < 28) { return CharacterTestRig.Jump(step - 20, Vector3.forward); }
                if (step >= 40 && step < 70) { return CharacterTestRig.Sprint(Vector3.forward, step - 39); }
                return CharacterTestRig.Move(Vector3.forward);
            }, AnimateAndMeasure);
            rig.Teleport(new Vector3(0f, 0.05f, 0f));
            yield return rig.Run(0.5f, step => CharacterTestRig.Move(step < 10 ? Vector3.right : Vector3.zero), AnimateAndMeasure);
        }

        /// <summary>
        /// Runs the course twice, measuring the second, and asserts nothing was allocated (after checking the
        /// measurement sees an allocation, so it isn't passing for measuring nothing).
        /// </summary>
        private IEnumerator RunTwiceAndAssertNoAllocation(string who)
        {
            AllocationCounter.Start();
            var probe = new object[16];
            int probed = AllocationCounter.Stop();
            GC.KeepAlive(probe);
            Assume.That(probed, Is.GreaterThan(0), "Test setup: AllocationCounter should see an allocation.");

            yield return RunTheCourse();
            // The rig records the events raised as the movement steps (test code, in the measured step): cleared, its lists
            // keep the room the first run gave them, so they don't grow (allocating) through the second.
            rig.ClearEvents();
            measuring = true;
            rig.MeasuresStepAllocations = true;
            seen = CharacterState.Idle;
            yield return RunTheCourse();
            rig.MeasuresStepAllocations = false;
            measuring = false;

            TestContext.WriteLine($"{who}, running the course again (doing: {seen}): allocations: the movement {rig.StepAllocations}, " +
                                  $"UpdateAnimator {animationAllocations}, the Animator's update (and its IK) {animatorAllocations}, " +
                                  $"paw placement {pawAllocations}.");
            Assume.That(seen.HasFlag(CharacterState.Moving) && seen.HasFlag(CharacterState.Jumping), Is.True,
                $"Test setup: {who} should walk and jump (it did: {seen}).");
            Assert.That(rig.StepAllocations, Is.Zero, $"{who}'s movement steps shouldn't allocate.");
            Assert.That(animationAllocations, Is.Zero, $"Feeding {who}'s animation (AnimationController.UpdateAnimator) shouldn't allocate.");
            Assert.That(animatorAllocations, Is.Zero, $"{who}'s Animator update, with the IK it runs, shouldn't allocate.");
            Assert.That(pawAllocations, Is.Zero, $"{who}'s paw placement (PawPlacementIK.Solve) shouldn't allocate.");
        }

        /// <summary>The floor, and a climbable wall a few metres ahead.</summary>
        private void BuildCourse()
        {
            rig.Floor();
            rig.ClimbableBox("Wall", new Vector3(0f, 5f, 7f), new Vector3(10f, 10f, 1f));
            Physics.SyncTransforms();
        }

        [UnityTest]
        public IEnumerator TheCharactersStepsAndFrames_AllocateNothing()
        {
            BuildCourse();
            rig.SpawnCharacter(new Vector3(0f, 0.05f, 0f));
            animation = rig.Animation;
            yield return rig.WaitForModel();
            TakeOverAnimator();
            yield return RunTwiceAndAssertNoAllocation("The character");
            Assert.That(seen.HasFlag(CharacterState.WallClimbing), Is.True, $"Test setup: the character should climb the wall (it did: {seen}).");
        }

        [UnityTest]
        public IEnumerator TheFoxsStepsAndFrames_AllocateNothing()
        {
            GameObject prefab = CharacterTestRig.LoadPrefab(FoxPrefabPath);
            if (prefab == null) { Assert.Ignore($"There's no fox ('{FoxPrefabPath}'): generate it with Tools > Character Showcase > Generate Fox."); }
            BuildCourse();
            rig.SpawnCharacter(new Vector3(0f, 0.05f, 0f), prefab);
            animation = rig.Animation;
            yield return rig.WaitForModel();
            TakeOverAnimator();
            paws = animator.GetComponent<PawPlacementIK>();
            Assert.That(paws, Is.Not.Null, "The fox's model should have paw placement (PawPlacementIK): regenerate it.");
            yield return RunTwiceAndAssertNoAllocation("The fox");
        }
    }
}
