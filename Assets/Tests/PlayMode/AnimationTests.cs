using System.Collections;
using Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CharacterShowcase.Tests
{
    /// <summary>Tests that the character's Animator follows its movement: the dash animation plays while dashing.</summary>
    public class AnimationTests
    {
        private CharacterTestRig rig;
        private AnimationController animation;
        private Animator animator;

        [SetUp]
        public void SetUp() => rig = new CharacterTestRig();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            rig.Dispose();
            yield return null;
        }

        /// <summary>Feeds the latest movement to the animator, as ACCharacterController does each frame in the game.</summary>
        private void FeedAnimator(int _) => animation.UpdateAnimator(rig.Movement.lastFrameMovementData, Time.fixedDeltaTime);

        private bool InState(string state)
        {
            AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);
            AnimatorStateInfo next = animator.GetNextAnimatorStateInfo(0);
            return current.IsName(state) || (animator.IsInTransition(0) && next.IsName(state));
        }

        [UnityTest]
        public IEnumerator Dashing_PlaysTheDashAnimation_ThenReturnsToLocomotion()
        {
            rig.Floor();
            rig.SpawnCharacter(new Vector3(0f, 0.05f, 0f));
            animation = rig.Character.GetComponent<AnimationController>();
            animator = rig.Character.GetComponentInChildren<Animator>();
            yield return rig.Run(0.3f, _ => CharacterTestRig.Idle(), FeedAnimator);
            Assert.That(InState("Locomotion"), Is.True, "Test setup: should start in Locomotion.");

            bool sawDash = false;
            int dashSteps = CharacterTestRig.StepsFor(rig.MovementSetting<float>("dashDuration"));
            yield return rig.Run(dashSteps - 1, step => step == 0 ? CharacterTestRig.Dash(0, Vector3.right) : CharacterTestRig.Idle(), step =>
            {
                FeedAnimator(step);
                sawDash |= InState("Dash");
            });
            yield return null; // Let the animator evaluate the last dash frame.
            sawDash |= InState("Dash");

            yield return rig.Run(0.5f, _ => CharacterTestRig.Idle(), FeedAnimator);
            yield return null;

            Assert.That(sawDash, Is.True, "The dash animation should play while dashing.");
            Assert.That(InState("Locomotion"), Is.True, "The character should return to Locomotion after a ground dash.");
        }
    }
}
