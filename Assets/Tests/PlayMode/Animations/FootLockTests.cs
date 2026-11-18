using System.Collections;
using Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CharacterShowcase.Tests.Animations
{
    /// <summary>
    /// Tests of foot placement (FootPlacementIK): the model turns after the character about its feet, which drags planted
    /// feet round with it; locking holds a planted foot where it was put down, so the feet stay put as the model turns
    /// (compared with locking turned off on the same model). On a moving platform, the feet are placed on it as it's drawn,
    /// so they don't jitter against it from frame to frame.
    /// </summary>
    [Category(TestArea.Animation)]
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

        /// <summary>
        /// The character's model swapped for a new one (its own, loaded again) while standing: the new model's foot
        /// placement carries on from the old one's (see IModelHandover), faded in as far and the pelvis as low, rather than
        /// fading back in from nothing.
        /// </summary>
        [UnityTest]
        public IEnumerator ASwappedInModel_CarriesOnTheOldOnesFootPlacement()
        {
            yield return SpawnWithOwnModel();
            yield return rig.Run(1f, _ => CharacterTestRig.Idle(), FeedAnimator);
            var old = rig.Models.Model.GetComponentInChildren<FootPlacementIK>();
            if (old == null) { Assert.Ignore("The model has no foot placement."); }
            Assume.That(old.ActiveWeight, Is.GreaterThan(0.9f), "Test setup: foot placement should be faded in.");
            // The old model's, as it's replaced (it keeps animating until then).
            float weight = 0f, pelvis = 0f;
            rig.Models.ModelReplacing += _ => { weight = old.ActiveWeight; pelvis = old.PelvisOffset; };

            // Read as the new model is swapped in, before its first update: what it starts from.
            FootPlacementIK swapped = null;
            float swappedWeight = 0f, swappedPelvis = 0f;
            rig.Models.ModelLoaded += model =>
            {
                swapped = model.Root.GetComponentInChildren<FootPlacementIK>();
                if (swapped == null) { return; }
                swappedWeight = swapped.ActiveWeight;
                swappedPelvis = swapped.PelvisOffset;
            };
            yield return CharacterTestRig.WaitFor(rig.Models.LoadModelAsync(rig.Models.ModelSource));

            Assert.That(swapped, Is.Not.Null.And.Not.SameAs(old), "Test setup: a new model should have been swapped in.");
            Assert.That(swappedWeight, Is.EqualTo(weight).Within(1e-4f), "Foot placement should stay faded in, not fade in again.");
            Assert.That(swappedPelvis, Is.EqualTo(pelvis).Within(1e-4f), "The pelvis should stay as low as it was.");
        }

        /// <summary>
        /// Records, each frame after the feet are placed (LateUpdate, late in the frame), how high each foot is above a
        /// platform's top as drawn (its Transform, interpolated between physics steps), and the character (its root, as
        /// drawn).
        /// </summary>
        [DefaultExecutionOrder(10000)]
        private class FootHeightRecorder : MonoBehaviour
        {
            public Transform left, right, root, platform;
            public float platformHalfHeight;
            public readonly System.Collections.Generic.List<Vector3> frames = new();

            private void LateUpdate()
            {
                if (left == null || right == null || root == null || platform == null) { return; }
                float top = platform.position.y + platformHalfHeight;
                frames.Add(new Vector3(left.position.y - top, right.position.y - top, root.position.y - top));
            }
        }

        /// <summary>
        /// Standing on a platform rising and falling, frame by frame as the game draws it (the platform and the character
        /// each interpolated between physics steps, the feet placed each frame after the Animator), the feet stay on the
        /// platform: their height above it barely changes from one frame to the next, and the character's (its root's) not
        /// at all. Placed where physics has the platform (its latest step), the feet jumped up and down against it; and
        /// the character, reading the platform a step late, rode it a step behind, flipping from below its top to above
        /// it as it turned round (see MovingPlatform.FixedUpdate).
        /// </summary>
        [UnityTest]
        public IEnumerator OnARisingAndFallingPlatform_TheFeetStayOnIt_FrameByFrame()
        {
            var size = new Vector3(4f, 0.3f, 4f);
            Level.MovingPlatform platform = rig.Platform(Vector3.zero, size, new Vector3(0f, 1.5f, 0f), 3f, Vector3.zero);
            platform.enabled = false; // Held still while the model loads (frames in which the character isn't stepped).
            Physics.SyncTransforms();
            rig.SpawnCharacter(new Vector3(0f, 0.05f, 0f));
            animation = rig.Animation;
            yield return rig.WaitForModel();
            animator = rig.Models.Animator;
            if (animator == null || !animator.isHuman || animator.GetComponent<FootPlacementIK>() == null)
            {
                Assert.Ignore("The character's model isn't a humanoid with foot placement.");
            }
            platform.enabled = true;
            // The Animator plays and the feet are placed each frame, as in the game; only the movement is fed to it here.
            yield return rig.Run(1f, _ => CharacterTestRig.Idle(), FeedAnimator);
            var recorder = new GameObject("Foot Height Recorder").AddComponent<FootHeightRecorder>();
            recorder.left = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            recorder.right = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            recorder.root = rig.Character.transform;
            recorder.platform = platform.transform;
            recorder.platformHalfHeight = size.y / 2f;
            try
            {
                yield return rig.Run(3f, _ => CharacterTestRig.Idle(), FeedAnimator);
            }
            finally { Object.Destroy(recorder.gameObject); }
            float jump = 0f, rootJump = 0f;
            for (int f = 1; f < recorder.frames.Count; f++)
            {
                Vector3 change = recorder.frames[f] - recorder.frames[f - 1];
                jump = Mathf.Max(jump, Mathf.Abs(change.x), Mathf.Abs(change.y));
                rootJump = Mathf.Max(rootJump, Mathf.Abs(change.z));
            }
            TestContext.WriteLine($"Over {recorder.frames.Count} frames, a foot's height above the platform changed by at most {jump * 1000f:0.0} mm " +
                                  $"from one frame to the next (the character's by {rootJump * 1000f:0.0} mm).");
            Assume.That(recorder.frames.Count, Is.GreaterThan(20), "Test setup: there should be frames to measure.");
            Assert.That(jump, Is.LessThan(0.002f), "The feet shouldn't jitter on the platform from one frame to the next.");
            Assert.That(rootJump, Is.LessThan(0.001f), "The character should ride the platform as it's drawn, frame by frame.");
        }
    }
}
