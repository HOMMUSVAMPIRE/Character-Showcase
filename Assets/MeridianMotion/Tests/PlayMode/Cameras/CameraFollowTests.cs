using System.Collections;
using System.Collections.Generic;
using MeridianMotion.Cameras;
using NUnit.Framework;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using UnityEngine;
using static MeridianMotion.Tests.Waits;

namespace MeridianMotion.Tests.Cameras
{
    /// <summary>
    /// Tests of how the camera rig follows its target: smoothly when it moves, at once when it teleports or a new one is
    /// given (looking its way, with its eye anchor and size), framing it the same at any size, and dipping on a landing in
    /// proportion to it (but not down stairs, at any frame rate).
    /// </summary>
    [Category(TestArea.Camera)]
    public class CameraFollowTests : CameraRigTestsBase
    {
        [UnityTest]
        public IEnumerator FollowsMovedTarget_Smoothly()
        {
            yield return Frames(3);

            target.position += new Vector3(2f, 0f, 0f); // Under the teleport distance, so the camera should ease over.
            yield return PhysicsStepAndFrame();
            Vector3 shortlyAfter = cam.transform.position;
            Vector3 expected = ExpectedThirdPersonPosition(rig.Zoom.defaultDistance);
            yield return WaitUntil(() => Vector3.Distance(cam.transform.position, expected) < 0.01f);

            Assert.That(Vector3.Distance(shortlyAfter, expected), Is.GreaterThan(0.05f), "Camera should ease toward the target, not jump.");
            Assert.That(Vector3.Distance(cam.transform.position, expected), Is.LessThan(0.01f), "Camera should catch up with the target.");
        }

        [UnityTest]
        public IEnumerator TeleportedTarget_SnapsCameraImmediately()
        {
            yield return Frames(3);

            target.position += new Vector3(50f, 0f, 0f); // Well over the teleport distance.
            yield return PhysicsStepAndFrame();

            Assert.That(Vector3.Distance(cam.transform.position, ExpectedThirdPersonPosition(rig.Zoom.defaultDistance)), Is.LessThan(0.01f),
                "Camera should jump straight to a teleported target instead of flying across the level.");
        }

        [UnityTest]
        public IEnumerator Follow_SnapsToANewTarget_LookingTheWayItFaces()
        {
            yield return Frames(3);
            float pitch = rig.Pitch;
            var spawned = Track(new GameObject("Spawned Target")).transform;
            spawned.SetPositionAndRotation(new Vector3(40f, 2f, -30f), Quaternion.Euler(0f, 120f, 0f));

            rig.Follow(spawned);
            target = spawned; // So the expected position below is worked out from the new target.
            yield return null;

            Assert.That(rig.Target, Is.SameAs(spawned));
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(rig.Yaw, 120f)), Is.LessThan(0.01f), "Camera should look the way its new target faces.");
            Assert.That(rig.Pitch, Is.EqualTo(pitch).Within(0.01f), "Following a new target shouldn't change the pitch.");
            Assert.That(Vector3.Distance(cam.transform.position, ExpectedThirdPersonPosition(rig.Zoom.defaultDistance)), Is.LessThan(0.01f),
                "Camera should jump straight to its new target instead of flying across the level.");
        }

        [UnityTest]
        public IEnumerator Follow_WithAnEyeAnchorAndSizeReference_UsesTheNewTargets()
        {
            yield return Frames(3);
            var spawned = Track(new GameObject("Spawned Target")).transform;
            spawned.position = new Vector3(-20f, 0f, 15f);
            var eyes = Track(new GameObject("Eyes")).transform;
            eyes.SetParent(spawned, false);
            eyes.localPosition = Vector3.up * 1.6f;
            var size = Track(new GameObject("Size")).transform;
            size.SetParent(spawned, false);
            size.localScale = Vector3.one * 2f;

            rig.Follow(spawned, eyes, size);
            yield return null;

            Assert.That(rig.Target, Is.SameAs(spawned));
            Assert.That(rig.FirstPerson.eyeAnchor, Is.SameAs(eyes));
            Assert.That(rig.TargetSizeReference, Is.SameAs(size));
            Assert.That(rig.SizeScale, Is.EqualTo(2f).Within(1e-4f), "The camera should frame the new target at its size.");

            rig.Follow(target, null, null);
            Assert.That(rig.FirstPerson.eyeAnchor, Is.Null, "A target without an eye anchor shouldn't keep the old target's.");
            Assert.That(rig.TargetSizeReference, Is.Null, "A target without a size reference shouldn't keep the old target's.");
        }

        private static readonly float[] OtherSizes = { 0.1f, 2f, 10f, 100f };

        /// <summary>
        /// The camera frames a target of any size as it does one of normal size, scaled with it: its pivot, shoulder
        /// offset, distance and near clip plane all in proportion (and the far clip plane further for a bigger one).
        /// </summary>
        [UnityTest]
        public IEnumerator TheCameraFramesATarget_TheSameWayAtAnySize([ValueSource(nameof(OtherSizes))] float size)
        {
            yield return Frames(3);
            Vector3 offset = cam.transform.position - target.position;
            float near = cam.nearClipPlane, far = cam.farClipPlane;

            SizeTarget(size);
            rig.SetTarget(target); // Snaps, so this checks where it settles rather than how it eases there.
            yield return Frames(3);

            Assert.That(Vector3.Distance(cam.transform.position - target.position, offset * size), Is.LessThan(1e-3f * size),
                "The camera should sit where it does for a target of normal size, scaled with the target.");
            Assert.That(Quaternion.Angle(cam.transform.rotation, rig.LookRotation), Is.LessThan(0.1f));
            Assert.That(cam.nearClipPlane, Is.EqualTo(near * size).Within(near * size * 1e-3f), "The near clip plane should scale with the target.");
            Assert.That(cam.farClipPlane, Is.EqualTo(far * Mathf.Max(1f, size)).Within(far * 1e-3f),
                "The far clip plane should scale up for a bigger target, and stay for a smaller one.");
        }

        /// <summary>
        /// Drops the target at a steady speed for its size (12 m/s at normal size) and stops it dead, as if it hit the
        /// ground, and gives back how far the camera dipped, relative to the target's size.
        /// </summary>
        private IEnumerator MeasureLandingDip(float size, System.Action<float> result)
        {
            // Every frame exactly DipFrameTime of game time, however long it really takes: the dip's lowest point is
            // looked for frame by frame, and FrameTimeLimit can only cap a frame by how long the last one took, so a
            // frame much longer than the one before (the editor's hitches) covered far more game time, sampled the dip
            // more coarsely, and the two sizes' dips differed by more than the test allows. So the limit is off while
            // the dip's measured, at a time scale of 1, and each frame is DipFrameTime long (captureDeltaTime): the
            // limit would otherwise still set the time scale from each frame's real length.
            timeLimit.enabled = false;
            Time.timeScale = 1f;
            Time.captureDeltaTime = DipFrameTime;
            rig.Shake.enabled = false;
            rig.ThirdPerson.verticalDamping = 0f;
            target.position = new Vector3(0f, 20f * size, 0f);
            rig.SetTarget(target);
            yield return Frames(3);
            for (int i = 0; i < 30; i++)
            {
                yield return new WaitForFixedUpdate();
                target.position += Vector3.down * 12f * size * Time.fixedDeltaTime;
            }
            // The lowest the camera dips, looked for every frame (not every physics step, which could step over it).
            float rest, lowest = float.PositiveInfinity;
            for (float t = 0f; t < 0.8f; t += Time.deltaTime)
            {
                yield return null;
                lowest = Mathf.Min(lowest, cam.transform.position.y);
            }
            yield return WaitUntilSettled(() => cam.transform.position.y, 1e-5f);
            rest = cam.transform.position.y;
            Time.captureDeltaTime = 0f;
            timeLimit.enabled = true;
            result((rest - lowest) / size);
        }

        /// <summary>
        /// A target walking down stairs drops onto each step in a physics step or two (as a character's movement snaps it
        /// down onto the next step): that's not a landing, however fast the drop reads, and doesn't dip or shake the camera,
        /// at any frame rate (landings are judged step by step: see CameraTargetTracker.Step).
        /// </summary>
        [UnityTest]
        public IEnumerator WalkingDownStairs_DoesntDipOrShakeTheCamera_AtAnyFrameRate([Values(0f, 0.05f, 0.1f)] float frameTime)
        {
            if (!rig.Landing.enabled) { Assert.Ignore("Landing dips are off."); }
            // Frames frameTime long (several physics steps each), as at a low frame rate: judged frame by frame, two frames
            // in a row each holding a drop onto the next stair read as a fall, then a landing.
            if (frameTime > 0f)
            {
                timeLimit.enabled = false;
                Time.timeScale = 1f;
                Time.captureDeltaTime = frameTime;
            }
            target.position = new Vector3(0f, 20f, 0f);
            rig.SetTarget(target);
            yield return Frames(3);
            const float rise = 0.3f, tread = 0.4f, speed = 5f;
            float lowest = 0f, mostTrauma = 0f, along = 0f;
            for (int i = 0; i < 100; i++)
            {
                yield return new WaitForFixedUpdate();
                float before = along;
                along += speed * Time.fixedDeltaTime;
                // Level along each tread, then down the whole rise in the step that crosses its edge.
                if (Mathf.FloorToInt(along / tread) > Mathf.FloorToInt(before / tread)) { target.position += Vector3.down * rise; }
                target.position += Vector3.forward * speed * Time.fixedDeltaTime;
                lowest = Mathf.Min(lowest, rig.LandingDip);
                mostTrauma = Mathf.Max(mostTrauma, rig.Trauma);
            }
            Time.captureDeltaTime = 0f;
            timeLimit.enabled = true;
            Assert.That(lowest, Is.EqualTo(0f).Within(1e-4f), "Stepping down stairs shouldn't dip the camera as a landing does.");
            Assert.That(mostTrauma, Is.EqualTo(0f).Within(1e-4f), "Nor shake it.");
        }

        /// <summary>
        /// Landing dips the camera, then it springs back to where it sits at rest; landing as fast for its size, a target
        /// of any size dips it as far for its size.
        /// </summary>
        [UnityTest]
        public IEnumerator Landing_DipsTheCamera_InProportionToTheTarget_ThenRecovers([ValueSource(nameof(OtherSizes))] float size)
        {
            float normalDip = 0f, dip = 0f;
            yield return MeasureLandingDip(1f, d => normalDip = d);
            Assert.That(cam.transform.position.y, Is.EqualTo(ExpectedThirdPersonPosition(rig.Zoom.defaultDistance).y).Within(0.01f),
                "Camera should spring back up after landing.");
            SizeTarget(size);
            yield return MeasureLandingDip(size, d => dip = d);
            // As the landing settings dip it for a 12 m/s landing (the spring's peak is about that).
            float expectedDip = Mathf.Min((12f - rig.Landing.minImpactSpeed) * rig.Landing.dipPerSpeed, rig.Landing.maxDip);
            if (!rig.Landing.enabled || expectedDip <= 0f) { Assert.Ignore("Landing dips are off."); }
            Assert.That(normalDip, Is.EqualTo(expectedDip).Within(0.3f * expectedDip), "The camera should dip as far as its landing settings say.");
            Assert.That(dip, Is.EqualTo(normalDip).Within(normalDip * 0.05f),
                "Landing as fast for its size, a target of any size should dip the camera as far for its size.");
        }
    }
}
