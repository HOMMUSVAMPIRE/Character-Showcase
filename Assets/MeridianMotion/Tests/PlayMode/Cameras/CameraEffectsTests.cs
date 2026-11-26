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
    /// Tests of the camera rig's effects: shake disturbing it and decaying back to rest (every rig's, unless it's turned
    /// off), and the field of view widening with the target's speed for its size and settling when it stops.
    /// </summary>
    [Category(TestArea.Camera)]
    public class CameraEffectsTests : CameraRigTestsBase
    {
        [UnityTest]
        public IEnumerator Shake_DisturbsCamera_ThenDecaysBackToRest()
        {
            yield return Frames(3);
            Vector3 rest = cam.transform.position;

            rig.AddTrauma(1f);
            float largestOffset = 0f;
            for (int i = 0; i < 10; i++)
            {
                yield return null;
                largestOffset = Mathf.Max(largestOffset, Vector3.Distance(cam.transform.position, rest));
            }
            yield return WaitUntil(() => rig.Trauma == 0f);
            // The frame its trauma runs out, the rig may still have placed the camera with the last of it (how much is left
            // depends on how long frames are, longer on CI): let it settle before measuring.
            yield return WaitUntilSettled(() => cam.transform.position, 1e-5f);

            Assert.That(largestOffset, Is.GreaterThan(0.001f), "Trauma should shake the camera.");
            Assert.That(rig.Trauma, Is.EqualTo(0f), "Trauma should decay away.");
            Assert.That(Vector3.Distance(cam.transform.position, rest), Is.LessThan(1e-4f), "Camera should return to rest after shaking.");
        }

        [UnityTest]
        public IEnumerator ShakeAll_ReachesEveryRig_UnlessShakeIsDisabled()
        {
            var second = Track(new GameObject("Second Camera"));
            second.AddComponent<Camera>();
            var secondRig = second.AddComponent<MMotionCameraRig>();
            secondRig.SetTarget(target);
            secondRig.Shake.enabled = false;
            yield return Frames(2);

            MMotionCameraRig.ShakeAll(0.5f);
            Assert.That(rig.Trauma, Is.EqualTo(0.5f).Within(1e-4f));
            // The rig updates after this, this frame: its trauma decays over this frame's time, however long it is (a
            // frame's game time varies, in the editor especially).
            float decayed = 0.5f - rig.Shake.decayPerSecond * Time.deltaTime;
            yield return null;

            Assume.That(decayed, Is.GreaterThan(0f), "Test setup: the frame should be short enough for the trauma to last it.");
            Assert.That(rig.Trauma, Is.EqualTo(decayed).Within(1e-4f), "Enabled rig should be shaking, its trauma decaying at its decay rate.");
            Assert.That(secondRig.Trauma, Is.EqualTo(0f), "A rig with shake disabled should ignore trauma.");
        }

        /// <summary>Moves the target in step with physics, as a character would, then returns the field of view.</summary>
        /// <summary>
        /// Moves the target at a steady speed and gives the camera's FOV, read while the target is still moving: the FOV
        /// the camera set in a frame whose every physics step moved the target. (Read a frame after stopping, it came from
        /// a frame partly spent stopped: in the editor, where a frame holds several physics steps, the speed it saw was
        /// lower, and the FOV was already easing back.)
        /// </summary>
        private IEnumerator MoveTargetAndMeasureFov(float speed, System.Action<float> result)
        {
            float fov = 0f;
            for (float t = 0f; t < 1.5f; t += Time.fixedDeltaTime)
            {
                yield return new WaitForFixedUpdate();
                fov = cam.fieldOfView; // As the camera set it last frame, with the target moving throughout.
                target.position += Vector3.forward * speed * Time.fixedDeltaTime;
            }
            result(fov);
        }

        private static readonly float[] SpeedSizes = { 1f, 0.25f, 4f };

        /// <summary>
        /// The field of view widens while the target moves fast, by the extra FOV at the speed of full effect, and
        /// settles back once it stops. Speed is judged in proportion to the target's size: a quarter-size target looks as
        /// fast at a quarter of the speed, and one four times the size needs four times the speed (moving at only twice
        /// the full-effect speed, it looks half as fast).
        /// </summary>
        [UnityTest]
        public IEnumerator SpeedFov_WidensWithSpeedForTheTargetsSize_AndSettlesWhenItStops([ValueSource(nameof(SpeedSizes))] float size)
        {
            rig.SpeedFov.enabled = true;
            if (size != 1f) { SizeTarget(size); }
            yield return Frames(3);
            float baseFov = cam.fieldOfView;

            float fullFov = 0f;
            yield return MoveTargetAndMeasureFov(rig.SpeedFov.maxSpeed * size, f => fullFov = f);
            Assert.That(fullFov, Is.EqualTo(baseFov + rig.SpeedFov.extraFov).Within(0.5f), "At the full-effect speed for its size, the FOV should widen by the extra FOV.");
            yield return WaitUntil(() => Mathf.Abs(cam.fieldOfView - baseFov) < 0.1f);
            Assert.That(cam.fieldOfView, Is.EqualTo(baseFov).Within(0.1f), "FOV should settle back once the target stops.");

            if (size > 1f)
            {
                float halfFov = 0f;
                yield return MoveTargetAndMeasureFov(rig.SpeedFov.maxSpeed * size / 2f, f => halfFov = f);
                float expected = Mathf.InverseLerp(rig.SpeedFov.minSpeed, rig.SpeedFov.maxSpeed, rig.SpeedFov.maxSpeed / 2f) * rig.SpeedFov.extraFov;
                Assert.That(halfFov, Is.EqualTo(baseFov + expected).Within(0.5f), "At half the full-effect speed for its size, it should look half as fast.");
            }
        }
    }
}
