using System.Collections;
using System.Collections.Generic;
using CameraSystem;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

namespace CharacterShowcase.Tests
{
    /// <summary>
    /// Tests of the camera rig's placement and features. The rig follows a plain Transform rather than the character,
    /// mirroring how it is decoupled from the character code, and is driven through its public API rather than input.
    /// Expected positions are derived from the rig's own settings, so retuning the camera doesn't break the tests.
    /// </summary>
    public class CameraRigTests
    {
        private readonly List<Object> created = new();
        private float previousTimeScale;
        private Transform target;
        private CameraRig rig;
        private Camera cam;

        [SetUp]
        public void SetUp()
        {
            previousTimeScale = Time.timeScale;
            Time.timeScale = 4f; // Run faster than real time; the rig uses scaled time throughout.

            target = Track(new GameObject("Camera Target")).transform;
            var cameraObject = Track(new GameObject("Test Camera"));
            cam = cameraObject.AddComponent<Camera>();
            rig = cameraObject.AddComponent<CameraRig>();
            rig.SetTarget(target);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (Object obj in created) { if (obj != null) { Object.Destroy(obj); } }
            created.Clear();
            Time.timeScale = previousTimeScale;
            yield return null;
        }

        private T Track<T>(T obj) where T : Object
        {
            created.Add(obj);
            return obj;
        }

        private GameObject Box(string name, Vector3 center, Vector3 size)
        {
            GameObject box = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            box.name = name;
            box.transform.position = center;
            box.transform.localScale = size;
            // Colliders moved this frame only reach the physics engine on the next physics step; sync now so the
            // camera's collision queries see the geometry immediately.
            Physics.SyncTransforms();
            return box;
        }

        private static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++) { yield return null; }
        }

        /// <summary>Waits for a physics step (the rig samples its target once per step) and a frame to apply it.</summary>
        private static IEnumerator PhysicsStepAndFrame()
        {
            yield return new WaitForFixedUpdate();
            yield return null;
        }

        /// <summary>Where the third person camera should sit for the current settings, with nothing in the way.</summary>
        private Vector3 ExpectedThirdPersonPosition(float distance)
        {
            ThirdPersonSettings tp = rig.ThirdPerson;
            Quaternion yaw = Quaternion.Euler(0f, rig.Yaw, 0f);
            Vector3 shoulder = target.position + Vector3.up * (tp.pivotHeight + tp.shoulderOffset.y)
                               + yaw * Vector3.right * (tp.shoulderOffset.x * (tp.startOnRightShoulder ? 1f : -1f));
            return shoulder + rig.LookRotation * Vector3.back * distance;
        }

        private Vector3 ExpectedFirstPersonPosition()
        {
            FirstPersonSettings fp = rig.FirstPerson;
            return target.position + Vector3.up * fp.eyeHeight + Quaternion.Euler(0f, rig.Yaw, 0f) * Vector3.forward * fp.eyeForwardOffset;
        }

        [UnityTest]
        public IEnumerator ThirdPerson_SitsOverTheShoulderAtDefaultDistance()
        {
            yield return Frames(3);

            Assert.That(rig.Mode, Is.EqualTo(CameraMode.ThirdPerson));
            Assert.That(Vector3.Distance(cam.transform.position, ExpectedThirdPersonPosition(rig.Zoom.defaultDistance)), Is.LessThan(0.01f));
            Assert.That(Quaternion.Angle(cam.transform.rotation, rig.LookRotation), Is.LessThan(0.1f), "Camera should face its look rotation.");
            Assert.That(cam.fieldOfView, Is.EqualTo(rig.ThirdPerson.fieldOfView).Within(0.1f));
        }

        [UnityTest]
        public IEnumerator FirstPerson_SitsAtEyeHeight_AfterBlendingOverTransitionDuration()
        {
            yield return Frames(3);
            var changes = new List<CameraMode>();
            rig.ModeChanged += changes.Add;

            Assert.That(rig.SetMode(CameraMode.FirstPerson), Is.True);
            yield return new WaitForSeconds(rig.Modes.transitionDuration * 0.5f);
            float midBlend = rig.ThirdPersonBlend;
            yield return new WaitForSeconds(rig.Modes.transitionDuration);

            Assert.That(midBlend, Is.InRange(0.05f, 0.95f), "Camera should blend between modes, not cut.");
            Assert.That(rig.ThirdPersonBlend, Is.EqualTo(0f));
            Assert.That(changes, Is.EqualTo(new[] { CameraMode.FirstPerson }), "ModeChanged should fire once.");
            Assert.That(Vector3.Distance(cam.transform.position, ExpectedFirstPersonPosition()), Is.LessThan(0.01f));
            Assert.That(cam.fieldOfView, Is.EqualTo(rig.FirstPerson.fieldOfView).Within(0.1f));
        }

        [UnityTest]
        public IEnumerator SetMode_RefusesDisabledModes()
        {
            rig.Modes.allowFirstPerson = false;
            yield return Frames(2);

            Assert.That(rig.SetMode(CameraMode.FirstPerson), Is.False);
            yield return Frames(2);
            Assert.That(rig.Mode, Is.EqualTo(CameraMode.ThirdPerson));
        }

        [UnityTest]
        public IEnumerator Pitch_IsClampedToTheModesLimits()
        {
            yield return Frames(2);

            rig.SetLookRotation(0f, 89f);
            Assert.That(rig.Pitch, Is.EqualTo(rig.ThirdPerson.maxPitch).Within(0.01f));
            rig.SetLookRotation(0f, -89f);
            Assert.That(rig.Pitch, Is.EqualTo(rig.ThirdPerson.minPitch).Within(0.01f));
        }

        [UnityTest]
        public IEnumerator Zoom_SetsDistance_ClampedToLimits()
        {
            yield return Frames(2);

            rig.SetDistance(6f, instant: true);
            // Collision pulls in instantly but eases back out (returnSmoothTime), so give it time to extend.
            yield return new WaitForSeconds(rig.Collision.returnSmoothTime * 6f);
            Assert.That(Vector3.Distance(cam.transform.position, ExpectedThirdPersonPosition(6f)), Is.LessThan(0.01f));

            rig.SetDistance(1000f, instant: true);
            Assert.That(rig.Distance, Is.EqualTo(rig.Zoom.maxDistance));
            rig.SetDistance(0f, instant: true);
            Assert.That(rig.Distance, Is.EqualTo(rig.Zoom.minDistance));
        }

        [UnityTest]
        public IEnumerator Collision_PullsCameraInFrontOfWall()
        {
            // A wall 2 m behind the target, between it and the camera's default position 3.5 m back.
            Box("Wall", new Vector3(0f, 2f, -2.25f), new Vector3(10f, 4f, 0.5f));
            yield return Frames(3);

            float wallFace = -2f;
            Assert.That(cam.transform.position.z, Is.GreaterThan(wallFace + rig.Collision.probeRadius * 0.9f),
                "Camera should stay in front of the wall.");
            Assert.That(cam.transform.position.z, Is.LessThan(wallFace + rig.Collision.probeRadius + 0.1f),
                "Camera should only pull in as far as needed.");
        }

        [UnityTest]
        public IEnumerator Collision_IgnoresTheTargetsOwnColliders()
        {
            // A collider on the target big enough to contain the shoulder point and the camera's path.
            BoxCollider body = target.gameObject.AddComponent<BoxCollider>();
            body.center = new Vector3(0f, 1.5f, 0f);
            body.size = new Vector3(3f, 3f, 3f);
            Physics.SyncTransforms();
            yield return Frames(3);

            Assert.That(Vector3.Distance(cam.transform.position, ExpectedThirdPersonPosition(rig.Zoom.defaultDistance)), Is.LessThan(0.01f),
                "Camera should not collide with the target it follows.");
        }

        [UnityTest]
        public IEnumerator FollowsMovedTarget_Smoothly()
        {
            yield return Frames(3);

            target.position += new Vector3(2f, 0f, 0f); // Under the teleport distance, so the camera should ease over.
            yield return PhysicsStepAndFrame();
            Vector3 shortlyAfter = cam.transform.position;
            yield return new WaitForSeconds(1f);

            Vector3 expected = ExpectedThirdPersonPosition(rig.Zoom.defaultDistance);
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
            yield return new WaitForSeconds(1f / rig.Shake.decayPerSecond + 0.1f);

            Assert.That(largestOffset, Is.GreaterThan(0.001f), "Trauma should shake the camera.");
            Assert.That(rig.Trauma, Is.EqualTo(0f), "Trauma should decay away.");
            Assert.That(Vector3.Distance(cam.transform.position, rest), Is.LessThan(1e-4f), "Camera should return to rest after shaking.");
        }

        [UnityTest]
        public IEnumerator ShakeAll_ReachesEveryRig_UnlessShakeIsDisabled()
        {
            var second = Track(new GameObject("Second Camera"));
            second.AddComponent<Camera>();
            var secondRig = second.AddComponent<CameraRig>();
            secondRig.SetTarget(target);
            secondRig.Shake.enabled = false;
            yield return Frames(2);

            CameraRig.ShakeAll(0.5f);
            Assert.That(rig.Trauma, Is.EqualTo(0.5f).Within(1e-4f));
            yield return null;

            Assert.That(rig.Trauma, Is.GreaterThan(0.4f), "Enabled rig should be shaking.");
            Assert.That(secondRig.Trauma, Is.EqualTo(0f), "A rig with shake disabled should ignore trauma.");
        }

        [UnityTest]
        public IEnumerator FirstPerson_HidesTargetButKeepsShadow_AndRestoresItAfter()
        {
            var model = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            Object.Destroy(model.GetComponent<Collider>());
            model.transform.SetParent(target, false);
            var renderer = model.GetComponent<Renderer>();
            renderer.shadowCastingMode = ShadowCastingMode.On;
            rig.SetTarget(target); // Re-collect renderers now that the target has one.
            rig.FirstPerson.hideTarget = TargetHideMode.ShadowsOnly;
            yield return Frames(2);

            rig.SetMode(CameraMode.FirstPerson);
            yield return new WaitForSeconds(rig.Modes.transitionDuration + 0.1f);
            Assert.That(renderer.shadowCastingMode, Is.EqualTo(ShadowCastingMode.ShadowsOnly), "Target should only cast a shadow in first person.");
            Assert.That(renderer.forceRenderingOff, Is.False);

            rig.SetMode(CameraMode.ThirdPerson);
            yield return new WaitForSeconds(rig.Modes.transitionDuration + 0.1f);
            Assert.That(renderer.shadowCastingMode, Is.EqualTo(ShadowCastingMode.On), "Target should be fully visible again.");
            Assert.That(renderer.forceRenderingOff, Is.False);
        }

        [UnityTest]
        public IEnumerator SpeedFov_WidensWhileTargetMovesFast_AndSettlesWhenItStops()
        {
            rig.SpeedFov.enabled = true;
            yield return Frames(3);
            float baseFov = cam.fieldOfView;

            // Move the target in step with physics, as a character would, at the speed of full effect.
            float speed = rig.SpeedFov.maxSpeed;
            for (float t = 0f; t < 1.5f; t += Time.fixedDeltaTime)
            {
                yield return new WaitForFixedUpdate();
                target.position += Vector3.forward * speed * Time.fixedDeltaTime;
            }
            yield return null;
            float movingFov = cam.fieldOfView;

            yield return new WaitForSeconds(2f);

            Assert.That(movingFov, Is.EqualTo(baseFov + rig.SpeedFov.extraFov).Within(0.5f), "FOV should widen by the extra FOV at full speed.");
            Assert.That(cam.fieldOfView, Is.EqualTo(baseFov).Within(0.1f), "FOV should settle back once the target stops.");
        }

        [UnityTest]
        public IEnumerator Landing_DipsTheCamera_ThenRecovers()
        {
            rig.Shake.enabled = false;            // Isolate the dip from landing shake.
            rig.ThirdPerson.verticalDamping = 0f; // And from the follow lag, which would otherwise offset it.
            target.position = new Vector3(0f, 20f, 0f);
            rig.SetTarget(target);
            yield return Frames(3);

            // Fall at a steady 12 m/s, then stop dead as if hitting the ground.
            for (int i = 0; i < 30; i++)
            {
                yield return new WaitForFixedUpdate();
                target.position += Vector3.down * 12f * Time.fixedDeltaTime;
            }
            Vector3 rest = ExpectedThirdPersonPosition(rig.Zoom.defaultDistance);
            float lowest = float.PositiveInfinity;
            for (int i = 0; i < 40; i++)
            {
                yield return new WaitForFixedUpdate();
                yield return null;
                lowest = Mathf.Min(lowest, cam.transform.position.y);
            }
            yield return new WaitForSeconds(1f);

            Assert.That(rest.y - lowest, Is.GreaterThan(0.05f), "Camera should dip on landing.");
            Assert.That(cam.transform.position.y, Is.EqualTo(rest.y).Within(0.01f), "Camera should spring back up after landing.");
        }

        [UnityTest]
        public IEnumerator CameraZone_ForcesFirstPersonInside_AndReleasesOnExit()
        {
            yield return Frames(3);
            var changes = new List<CameraMode>();
            rig.ModeChanged += changes.Add;

            GameObject zoneObject = Box("Zone", target.position, new Vector3(4f, 4f, 4f));
            var zone = zoneObject.AddComponent<CameraZone>();
            zone.forceMode = true;
            zone.mode = CameraMode.FirstPerson;
            yield return Frames(2);

            Assert.That(rig.Mode, Is.EqualTo(CameraMode.FirstPerson), "Zone should force first person.");
            Assert.That(rig.RequestedMode, Is.EqualTo(CameraMode.ThirdPerson), "The player's own choice should be kept.");

            target.position += new Vector3(20f, 0f, 0f); // Leave the zone.
            yield return PhysicsStepAndFrame();
            yield return Frames(2);

            Assert.That(rig.Mode, Is.EqualTo(CameraMode.ThirdPerson), "Leaving the zone should restore the player's choice.");
            Assert.That(changes, Is.EqualTo(new[] { CameraMode.FirstPerson, CameraMode.ThirdPerson }));
        }
    }
}
