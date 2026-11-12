using System.Collections;
using Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CharacterShowcase.Tests
{
    /// <summary>
    /// Tests of foot locking (FootPlacementIK): the model turns after the character about its feet, which drags planted
    /// feet round with it; locking holds a planted foot where it was put down, so the feet stay put as the model turns.
    /// Compared with locking turned off on the same model.
    /// </summary>
    public class FootLockTests : AnimationTestsBase
    {
        private void TurnAndAnimate(int step)
        {
            rig.Animation.SmoothRotateVisuals(Time.fixedDeltaTime, rig.Movement.ConsumeGroundRotation());
            StepAnimator(step);
        }

        /// <summary>Turns the character on the spot by the given angle, and gives how far its feet slid while the model came round.</summary>
        private IEnumerator TurnOnTheSpot(float footLock, float degrees, System.Action<float> slid)
        {
            var feet = animator.GetComponent<FootPlacementIK>();
            if (feet == null) { Assert.Ignore("The model has no foot placement."); }
            var field = typeof(FootPlacementIK).GetField("footLock", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            field.SetValue(feet, footLock);
            yield return rig.Run(1f, _ => CharacterTestRig.Idle(), TurnAndAnimate);

            Transform left = animator.GetBoneTransform(HumanBodyBones.LeftFoot), right = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            Vector3 leftStart = left.position, rightStart = right.position;
            float most = 0f;
            rig.Body.rotation = rig.Body.rotation * Quaternion.Euler(0f, degrees, 0f);
            yield return rig.Run(0.6f, _ => CharacterTestRig.Idle(), step =>
            {
                TurnAndAnimate(step);
                float l = new Vector2(left.position.x - leftStart.x, left.position.z - leftStart.z).magnitude;
                float r = new Vector2(right.position.x - rightStart.x, right.position.z - rightStart.z).magnitude;
                most = Mathf.Max(most, l, r);
            });
            slid(most);
        }

        [UnityTest]
        public IEnumerator TurningOnTheSpot_PlantedFeetStayWhereTheyWerePutDown()
        {
            yield return SpawnWithOwnModel();
            TakeOverAnimator();
            float unlocked = 0f, locked = 0f;
            yield return TurnOnTheSpot(0f, 60f, s => unlocked = s);
            yield return TurnOnTheSpot(1f, -60f, s => locked = s);
            TestContext.WriteLine($"Feet slid {unlocked * 100f:0.0} cm unlocked, {locked * 100f:0.0} cm locked, turning 60 degrees on the spot.");
            Assert.That(unlocked, Is.GreaterThan(0.05f), "Test setup: unlocked, the turn should drag the feet round.");
            Assert.That(locked, Is.LessThan(0.3f * unlocked), "Locked, planted feet should stay where they were put down as the model turns.");
            Assert.That(locked, Is.LessThan(0.03f * rig.Controller.Scale.y), "Locked feet should barely move.");
        }
    }
}
