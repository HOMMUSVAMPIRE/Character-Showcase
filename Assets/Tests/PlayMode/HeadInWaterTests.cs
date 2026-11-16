using System.Collections;
using System.Collections.Generic;
using Character;
using Level;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CharacterShowcase.Tests
{
    /// <summary>
    /// Tests of the hook for the head going under the water and coming up (AnimationController's HeadSubmerged and
    /// HeadSurfaced, for breath and the like), on the character's own model: swimming at the surface (the stroke's head
    /// bobbing at the water line) nothing's raised; diving raises HeadSubmerged once, its head under the surface, and rising back up raises
    /// HeadSurfaced once, its head above it; falling in from high, the head goes under with the plunge and comes up again;
    /// and on dry land nothing's raised.
    /// </summary>
    [Category(TestArea.Animation)]
    public class HeadInWaterTests : AnimationTestsBase
    {
        private Water water;
        private readonly List<(bool submerged, Water water, float headAboveSurface)> raised = new();
        private System.Action<Water> onSubmerged, onSurfaced;
        private AnimationController watched;

        // The fixture lives on from test to test, and a test's character goes on raising events for a frame while it's
        // destroyed: so each test stops listening to its character, and starts with an empty record.
        [SetUp]
        public void ClearRecord() => raised.Clear();

        [TearDown]
        public void StopListening()
        {
            if (watched == null) { return; }
            watched.HeadSubmerged -= onSubmerged;
            watched.HeadSurfaced -= onSurfaced;
            watched = null;
        }

        private float Margin => animation.HeadWaterMargin * rig.Controller.Scale.y;

        private float HeadAboveSurface => animator.GetBoneTransform(HumanBodyBones.Head).position.y - water.SurfaceHeight;

        /// <summary>Spawns the character with its own model, its animation stepped by hand with the physics, recording the head's events.</summary>
        private IEnumerator Spawn()
        {
            yield return SpawnWithOwnModel();
            TakeOverAnimator();
            watched = animation;
            onSubmerged = w => raised.Add((true, w, HeadAboveSurface));
            onSurfaced = w => raised.Add((false, w, HeadAboveSurface));
            animation.HeadSubmerged += onSubmerged;
            animation.HeadSurfaced += onSurfaced;
            yield return rig.Run(0.3f, _ => CharacterTestRig.Idle(), Step);
        }

        /// <summary>Each step, the model posed and its head checked against the water (as each frame, LateUpdate does).</summary>
        private void Step(int step)
        {
            StepAnimator(step);
            animation.CheckHeadInWater();
        }

        /// <summary>Deep water hanging above the floor (nothing under it but the water), the character swimming at its surface.</summary>
        private IEnumerator SwimmingAtTheSurface()
        {
            yield return Spawn();
            water = rig.Water(new Vector3(0f, 12f, 0f), new Vector3(40f, 10f, 40f));
            float depth = rig.MovementSetting<float>("swimDepth") * rig.StandingHeight;
            rig.Teleport(new Vector3(0f, water.SurfaceHeight - depth, 0f));
            yield return rig.Run(1f, _ => CharacterTestRig.Idle(), Step);
            Assume.That(rig.Swimming, Is.True, "Test setup: the character should be swimming.");
            raised.Clear();
        }

        [UnityTest]
        public IEnumerator SwimmingAtTheSurface_KeepsTheHeadOut_RaisingNothing()
        {
            yield return SwimmingAtTheSurface();
            yield return rig.Run(1f, _ => CharacterTestRig.Move(Vector3.forward), Step);
            Assert.That(raised, Is.Empty, "Swimming at the surface, the head bobbing at the water line shouldn't count as going under (or flicker in and out).");
            Assert.That(animation.IsHeadUnderwater, Is.False);
        }

        [UnityTest]
        public IEnumerator Diving_RaisesHeadSubmerged_AndRisingBack_RaisesHeadSurfaced()
        {
            yield return SwimmingAtTheSurface();
            yield return rig.Run(1, _ => CharacterTestRig.Crouch(0), Step);
            yield return rig.Run(1f, step => CharacterTestRig.Crouch(step + 1), Step);
            Assert.That(raised.Count, Is.EqualTo(1), "Diving under, the head should go under once.");
            Assert.That(raised[0].submerged, Is.True);
            Assert.That(raised[0].water, Is.SameAs(water));
            Assert.That(raised[0].headAboveSurface, Is.LessThan(-Margin + 1e-3f), "HeadSubmerged should be raised once the head is the margin under the surface.");
            Assert.That(animation.IsHeadUnderwater, Is.True);
            Assert.That(animation.HeadWater, Is.SameAs(water));

            yield return rig.RunUntil(() => raised.Count > 1, step => CharacterTestRig.Jump(step), Step, timeoutSeconds: 5f);
            yield return rig.Run(0.5f, _ => CharacterTestRig.Idle(), Step);
            Assert.That(raised.Count, Is.EqualTo(2), "Rising back up, the head should come up once.");
            Assert.That(raised[1].submerged, Is.False);
            Assert.That(raised[1].water, Is.SameAs(water));
            Assert.That(raised[1].headAboveSurface, Is.GreaterThanOrEqualTo(Margin - 1e-3f), "HeadSurfaced should be raised once the head is the margin above the surface.");
            Assert.That(animation.IsHeadUnderwater, Is.False);
        }

        [UnityTest]
        public IEnumerator FallingInFromHigh_TheHeadGoesUnder_AndComesUpAgain()
        {
            yield return Spawn();
            water = rig.Water(new Vector3(0f, 12f, 0f), new Vector3(40f, 10f, 40f));
            rig.Teleport(new Vector3(0f, water.SurfaceHeight + 6f, 0f));
            raised.Clear();
            yield return rig.Run(4f, _ => CharacterTestRig.Idle(), Step);
            Assert.That(raised.Count, Is.EqualTo(2), "Plunging in, the head should go under and come up once each.");
            Assert.That(raised[0].submerged, Is.True);
            Assert.That(raised[1].submerged, Is.False);
            Assert.That(animation.IsHeadUnderwater, Is.False);
        }

        [UnityTest]
        public IEnumerator OnDryLand_NothingIsRaised()
        {
            yield return Spawn();
            water = rig.Water(new Vector3(30f, 0f, 30f), new Vector3(10f, 3f, 10f)); // Elsewhere.
            yield return rig.Run(1f, _ => CharacterTestRig.Move(Vector3.forward), Step);
            Assert.That(raised, Is.Empty);
            Assert.That(animation.IsHeadUnderwater, Is.False);
        }
    }
}
