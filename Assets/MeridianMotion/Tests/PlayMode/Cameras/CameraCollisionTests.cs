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
    /// Tests of the camera rig's collision: pulled in front of a wall (at any size) and not for one clear of it, swapping
    /// to the other shoulder when a wall blocks part of it and back once it's gone, never pulled in by a wall the target
    /// runs along or a corridor narrower than its probe, and ignoring the target's own colliders.
    /// </summary>
    [Category(TestArea.Camera)]
    public class CameraCollisionTests : CameraRigTestsBase
    {
        private static readonly float[] CollisionSizes = { 1f, 0.1f, 10f };

        /// <summary>
        /// A wall between the target and the camera pulls the camera in front of it, only as far as needed; one just
        /// behind the camera, clear of it, doesn't move it. At any size, as the probe scales with the target: at a tenth
        /// of the size, the wall clear of the camera is still within reach of a full-size probe.
        /// </summary>
        [UnityTest]
        public IEnumerator Collision_PullsTheCameraInFrontOfAWall_AndIgnoresOnesClearOfIt([ValueSource(nameof(CollisionSizes))] float size)
        {
            if (size != 1f) { SizeTarget(size); }
            rig.SetTarget(target);
            yield return Frames(3);
            Vector3 clear = cam.transform.position;
            Vector3 back = rig.LookRotation * Vector3.back;
            back = new Vector3(back.x, 0f, back.z).normalized;
            float behind = Vector3.Dot(clear - target.position, back), probe = rig.Collision.probeRadius * size;

            // Clear of it: just past the camera, further than the probe (scaled) reaches, but nearer than a full-size one.
            float gap = size < 1f ? rig.Collision.probeRadius * Mathf.Sqrt(size) : 2f * probe;
            GameObject clearWall = Box("Wall Behind", target.position + back * (behind + gap + 0.05f * size) + Vector3.up * 2f * size,
                new Vector3(10f * size, 4f * size, 0.1f * size) + Vector3.zero);
            clearWall.transform.rotation = Quaternion.LookRotation(back);
            Physics.SyncTransforms();
            yield return Frames(3);
            Assert.That(Vector3.Distance(cam.transform.position, clear), Is.LessThan(1e-3f * size), "The camera shouldn't be pulled in by a wall clear of it.");
            Object.DestroyImmediate(clearWall);

            // Between them: half way back.
            float face = 0.5f * behind;
            GameObject wall = Box("Wall", target.position + back * (face + 0.25f * size) + Vector3.up * 2f * size, new Vector3(10f * size, 4f * size, 0.5f * size));
            wall.transform.rotation = Quaternion.LookRotation(back);
            Physics.SyncTransforms();
            yield return Frames(3);
            float camera = Vector3.Dot(cam.transform.position - target.position, back);
            Assert.That(camera, Is.LessThan(face - probe * 0.9f), "Camera should stay in front of the wall.");
            Assert.That(camera, Is.GreaterThan(face - probe - 0.1f * size), "Camera should only pull in as far as needed.");
        }

        /// <summary>A wall along the target's right, its face a little closer than the shoulder offset (as for a target running along it).</summary>
        private GameObject WallOnTheShoulderSide()
        {
            float face = 0.9f * rig.ThirdPerson.shoulderOffset.x;
            return Box("Wall", target.position + new Vector3(face + 0.5f, 2f, 0f), new Vector3(1f, 8f, 40f));
        }

        [UnityTest]
        public IEnumerator AWallPartlyBlockingTheShoulder_SwapsToTheOtherShoulder_AtFullDistance()
        {
            yield return Frames(3);
            if (!rig.ThirdPerson.startOnRightShoulder || !rig.ThirdPerson.autoSwapWhenBlocked || !rig.Collision.enabled)
            {
                Assert.Ignore("The camera doesn't start on the right shoulder with auto-swap and collision on.");
            }
            WallOnTheShoulderSide();
            yield return WaitUntilSettled(() => cam.transform.position, 1e-4f);

            Vector3 local = Quaternion.Inverse(Quaternion.Euler(0f, rig.Yaw, 0f)) * (cam.transform.position - target.position);
            Vector3 pivot = target.position + Vector3.up * rig.ThirdPerson.pivotHeight;
            Assert.That(local.x, Is.LessThan(-0.5f * rig.ThirdPerson.shoulderOffset.x), "The camera should move to the shoulder away from the wall.");
            Assert.That(Vector3.Distance(cam.transform.position, pivot), Is.GreaterThan(0.9f * rig.Zoom.defaultDistance),
                "Beside a wall, the camera should keep its distance rather than be pulled in against it.");
        }

        [UnityTest]
        public IEnumerator ATargetComingUpAgainstAWallAndRunningAlongIt_NeverPullsTheCameraIn()
        {
            yield return Frames(3);
            if (!rig.Collision.enabled) { Assert.Ignore("Camera collision is off."); }
            // A wall along the right, beside the target's path. The target runs forward and drifts over until it's
            // against the wall, as a character starting a wall run does; the view behind it stays clear throughout.
            float face = 0.9f * rig.ThirdPerson.shoulderOffset.x;
            Box("Wall", target.position + new Vector3(face + 1.5f + 0.5f, 2f, 20f), new Vector3(1f, 8f, 60f));
            const float forwardSpeed = 9f, sideSpeed = 3f;
            float nearest = float.PositiveInfinity;
            for (float t = 0f; t < 1.2f; t += Time.fixedDeltaTime)
            {
                Vector3 p = target.position;
                target.position = new Vector3(Mathf.Min(p.x + sideSpeed * Time.fixedDeltaTime, 1.5f), p.y, p.z + forwardSpeed * Time.fixedDeltaTime);
                yield return PhysicsStepAndFrame();
                Vector3 pivot = target.position + Vector3.up * rig.ThirdPerson.pivotHeight;
                if (t > 0.3f) { nearest = Mathf.Min(nearest, Vector3.Distance(cam.transform.position, pivot)); }
            }
            Assert.That(nearest, Is.GreaterThan(0.85f * rig.Zoom.defaultDistance),
                "A wall alongside the target, not behind it, shouldn't pull the camera in (it used to snap in to just behind the target's head).");
        }

        /// <summary>
        /// A target closer to a wall than the collision probe is wide (a slim character against it): the probe would start
        /// inside the wall, which read as fully blocked, snapping the camera in. It starts from just clear of it instead.
        /// </summary>
        [UnityTest]
        public IEnumerator ATargetCloserToAWallThanTheProbeIsWide_KeepsTheCameraAtFullDistance()
        {
            yield return Frames(3);
            if (!rig.Collision.enabled) { Assert.Ignore("Camera collision is off."); }
            // On the left, away from the shoulder, its face half the probe's radius from the pivot.
            float face = 0.5f * rig.Collision.probeRadius;
            Box("Wall", target.position + new Vector3(-(face + 0.5f), 2f, 0f), new Vector3(1f, 8f, 40f));
            yield return WaitUntilSettled(() => cam.transform.position, 1e-4f);

            Vector3 pivot = target.position + Vector3.up * rig.ThirdPerson.pivotHeight;
            Assert.That(Vector3.Distance(cam.transform.position, pivot), Is.GreaterThan(0.9f * rig.Zoom.defaultDistance),
                "Right beside a wall, the camera shouldn't be pulled in, with the view behind clear.");
            Assert.That(cam.transform.position.x, Is.GreaterThan(target.position.x - face + rig.Collision.probeRadius * 0.9f),
                "It should stay out of the wall.");
        }

        /// <summary>A corridor narrower than the probe is wide: the probe shrinks to fit, rather than reading as blocked.</summary>
        [UnityTest]
        public IEnumerator InACorridorNarrowerThanTheProbe_TheCameraStaysAtFullDistance_BetweenTheWalls()
        {
            yield return Frames(3);
            if (!rig.Collision.enabled) { Assert.Ignore("Camera collision is off."); }
            float half = 0.6f * rig.Collision.probeRadius;
            foreach (float side in new[] { -1f, 1f })
            {
                Box(side < 0f ? "Wall (West)" : "Wall (East)", target.position + new Vector3(side * (half + 0.5f), 2f, 0f), new Vector3(1f, 8f, 40f));
            }
            yield return WaitUntilSettled(() => cam.transform.position, 1e-4f);

            Vector3 pivot = target.position + Vector3.up * rig.ThirdPerson.pivotHeight;
            Assert.That(Vector3.Distance(cam.transform.position, pivot), Is.GreaterThan(0.9f * rig.Zoom.defaultDistance),
                "Down a narrow corridor, the camera should stay back behind the target.");
            Assert.That(Mathf.Abs(cam.transform.position.x - target.position.x), Is.LessThan(half), "Between the walls.");
        }

        [UnityTest]
        public IEnumerator OnceTheWallIsGone_TheCameraReturnsToItsShoulder()
        {
            yield return Frames(3);
            if (!rig.ThirdPerson.startOnRightShoulder || !rig.ThirdPerson.autoSwapWhenBlocked || !rig.Collision.enabled)
            {
                Assert.Ignore("The camera doesn't start on the right shoulder with auto-swap and collision on.");
            }
            GameObject wall = WallOnTheShoulderSide();
            yield return WaitUntilSettled(() => cam.transform.position, 1e-4f);
            wall.SetActive(false);
            Physics.SyncTransforms();
            yield return WaitUntilSettled(() => cam.transform.position, 1e-4f);
            Assert.That(Vector3.Distance(cam.transform.position, ExpectedThirdPersonPosition(rig.Zoom.defaultDistance)), Is.LessThan(0.05f),
                "With the wall gone, the camera should go back over its own shoulder.");
        }

        [UnityTest]
        public IEnumerator AWallOnTheOtherSide_DoesNotSwapTheShoulder()
        {
            yield return Frames(3);
            float face = 0.9f * rig.ThirdPerson.shoulderOffset.x;
            Box("Wall", target.position + new Vector3(-face - 0.5f, 2f, 0f), new Vector3(1f, 8f, 40f));
            yield return new WaitForSeconds(rig.ThirdPerson.shoulderSwapTime * 3f);
            Assert.That(Vector3.Distance(cam.transform.position, ExpectedThirdPersonPosition(rig.Zoom.defaultDistance)), Is.LessThan(0.05f),
                "A wall away from the camera's shoulder shouldn't move it.");
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
    }
}
