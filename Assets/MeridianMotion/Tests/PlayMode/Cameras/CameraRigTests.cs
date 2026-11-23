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
    /// Tests of the camera rig's views: third person over the shoulder (and swapped to the other), first person at eye
    /// height or its anchor, the pivot lowered under a low ceiling, modes refused when disabled, pitch and zoom clamped,
    /// the target hidden (its shadow kept) in first person, and camera zones forcing a mode. (Collision, following and the
    /// effects are CameraCollisionTests', CameraFollowTests' and CameraEffectsTests'.)
    /// </summary>
    [Category(TestArea.Camera)]
    public class CameraRigTests : CameraRigTestsBase
    {
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
        public IEnumerator SetShoulder_SlidesTheCameraOverTheOtherShoulder()
        {
            yield return Frames(3);
            bool other = !rig.ThirdPerson.startOnRightShoulder;
            Vector3 from = cam.transform.position;

            rig.SetShoulder(other);
            yield return PhysicsStepAndFrame();
            Vector3 sliding = cam.transform.position;
            yield return WaitUntilSettled(() => cam.transform.position.x + cam.transform.position.z, 0.0005f);

            Vector3 to = ExpectedThirdPersonPosition(rig.Zoom.defaultDistance, other);
            Assert.That(Vector3.Distance(cam.transform.position, to), Is.LessThan(0.01f), "The camera should end over the other shoulder.");
            Assert.That(Vector3.Distance(sliding, from), Is.GreaterThan(0f), "The camera should start moving at once.");
            Assert.That(Vector3.Distance(sliding, to), Is.GreaterThan(0.01f), "The camera should slide across, not cut.");
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
            yield return WaitUntil(() => rig.ThirdPersonBlend == 0f);

            Assert.That(midBlend, Is.InRange(0.05f, 0.95f), "Camera should blend between modes, not cut.");
            Assert.That(rig.ThirdPersonBlend, Is.EqualTo(0f));
            Assert.That(changes, Is.EqualTo(new[] { CameraMode.FirstPerson }), "ModeChanged should fire once.");
            Assert.That(Vector3.Distance(cam.transform.position, ExpectedFirstPersonPosition()), Is.LessThan(0.01f));
            Assert.That(cam.fieldOfView, Is.EqualTo(rig.FirstPerson.fieldOfView).Within(0.1f));
        }

        [UnityTest]
        public IEnumerator FirstPerson_FollowsTheEyeAnchor_InsteadOfTheFixedEyeHeight()
        {
            var anchor = Track(new GameObject("Eye Anchor")).transform;
            anchor.SetParent(target, false);
            anchor.localPosition = new Vector3(0f, 1.25f, 0f);
            rig.FirstPerson.eyeAnchor = anchor;
            rig.SetMode(CameraMode.FirstPerson);
            yield return WaitUntil(() => rig.ThirdPersonBlend == 0f);

            Vector3 eyes = target.position + Vector3.up * 1.25f + Quaternion.Euler(0f, rig.Yaw, 0f) * Vector3.forward * rig.FirstPerson.eyeForwardOffset;
            Assert.That(rig.EyeHeight, Is.EqualTo(1.25f).Within(1e-4f));
            Assert.That(Vector3.Distance(cam.transform.position, eyes), Is.LessThan(0.01f), "The eyes should be at the anchor's height.");

            // A taller target (a bigger model, say) raises the anchor, and the view with it.
            anchor.localPosition = new Vector3(0f, 1.6f, 0f);
            yield return Frames(30);
            Assert.That(cam.transform.position.y, Is.EqualTo(target.position.y + 1.6f).Within(0.01f));
        }

        /// <summary>
        /// Third person, the pivot is no higher than the target's eyes: one that lowers its eye anchor (ducking under
        /// something, lying down) is framed from lower down, and a low ceiling over it doesn't pull the camera in.
        /// </summary>
        [UnityTest]
        public IEnumerator ThirdPerson_ATargetLoweringItsEyes_LowersThePivot_UnderALowCeiling()
        {
            yield return Frames(3);
            if (!rig.Collision.enabled) { Assert.Ignore("Camera collision is off."); }
            var anchor = Track(new GameObject("Eye Anchor")).transform;
            anchor.SetParent(target, false);
            anchor.localPosition = new Vector3(0f, rig.ThirdPerson.pivotHeight + 0.2f, 0f);
            rig.FirstPerson.eyeAnchor = anchor;
            yield return Frames(5);
            Assert.That(rig.PivotHeight, Is.EqualTo(rig.ThirdPerson.pivotHeight).Within(1e-4f), "Eyes above the pivot height leave it where it is.");

            // Ducked under a ceiling over it all, its eyes below the ceiling, the pivot height above it.
            float eyes = 0.5f * rig.ThirdPerson.pivotHeight;
            anchor.localPosition = new Vector3(0f, eyes, 0f);
            float ceiling = eyes + 1.5f * rig.Collision.probeRadius;
            Box("Low Ceiling", target.position + new Vector3(0f, ceiling + 0.5f, 0f), new Vector3(40f, 1f, 40f));
            // Looking level, so the camera stays under the ceiling.
            rig.SetLookRotation(rig.Yaw, 0f);
            yield return WaitUntilSettled(() => cam.transform.position, 1e-4f);

            Assert.That(rig.PivotHeight, Is.EqualTo(eyes).Within(1e-4f), "The pivot should be no higher than the eyes.");
            Vector3 pivot = target.position + Vector3.up * eyes;
            Assert.That(Vector3.Distance(cam.transform.position, pivot), Is.GreaterThan(0.85f * rig.Zoom.defaultDistance),
                "Under a low ceiling, with its eyes under it, the camera should stay back behind the target.");
            Assert.That(cam.transform.position.y, Is.LessThan(target.position.y + ceiling), "And under the ceiling.");
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
            yield return WaitUntilSettled(() => cam.transform.position, 1e-4f);
            Assert.That(Vector3.Distance(cam.transform.position, ExpectedThirdPersonPosition(6f)), Is.LessThan(0.01f));

            rig.SetDistance(1000f, instant: true);
            Assert.That(rig.Distance, Is.EqualTo(rig.Zoom.maxDistance));
            rig.SetDistance(0f, instant: true);
            Assert.That(rig.Distance, Is.EqualTo(rig.Zoom.minDistance));
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
            yield return WaitUntil(() => rig.ThirdPersonBlend == 0f);
            Assert.That(renderer.shadowCastingMode, Is.EqualTo(ShadowCastingMode.ShadowsOnly), "Target should only cast a shadow in first person.");
            Assert.That(renderer.forceRenderingOff, Is.False);

            rig.SetMode(CameraMode.ThirdPerson);
            yield return WaitUntil(() => rig.ThirdPersonBlend == 1f);
            Assert.That(renderer.shadowCastingMode, Is.EqualTo(ShadowCastingMode.On), "Target should be fully visible again.");
            Assert.That(renderer.forceRenderingOff, Is.False);
        }

        [UnityTest]
        public IEnumerator FirstPerson_AlsoHidesRenderersAddedToTheTargetLater()
        {
            rig.FirstPerson.hideTarget = TargetHideMode.Hide;
            rig.SetMode(CameraMode.FirstPerson);
            yield return WaitUntil(() => rig.ThirdPersonBlend == 0f);

            // Like a character model that finishes loading after the camera started following the character.
            var model = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            Object.Destroy(model.GetComponent<Collider>());
            model.transform.SetParent(target, false);
            var renderer = model.GetComponent<Renderer>();
            yield return Frames(2);
            Assert.That(renderer.forceRenderingOff, Is.True, "A renderer added to the target later should be hidden too.");

            rig.SetMode(CameraMode.ThirdPerson);
            yield return WaitUntil(() => rig.ThirdPersonBlend == 1f);
            Assert.That(renderer.forceRenderingOff, Is.False, "It should be shown again in third person.");
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
