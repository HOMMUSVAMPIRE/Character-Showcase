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
    /// What the camera rig's fixtures share (CameraRigTests, CameraCollisionTests, CameraFollowTests, CameraEffectsTests):
    /// a rig following a plain Transform rather than the character, mirroring how it is decoupled from the character code,
    /// driven through its public API rather than input, sped up; and where it should sit, worked out from its own settings,
    /// so retuning the camera doesn't break the tests.
    /// </summary>
    [TestTiming]
    public abstract class CameraRigTestsBase
    {
        /// <summary>
        /// How much faster than real time the camera tests run, at most: the fastest they all pass at reliably (as the
        /// character tests' CharacterTestRig.TimeScale).
        /// </summary>
        protected const float TimeScale = 50f;
        /// <summary>
        /// The most game time (s) one frame may cover (see FrameTimeLimit). The camera smooths and eases frame by frame,
        /// and the tests watch it do so, which needs small frames: sped up 50 times, a frame of the editor's (drawn 60
        /// times a second, say) would otherwise cover most of a second, finishing a blend or a landing's dip before it
        /// could be seen. In batch mode, whose frames are far shorter, this doesn't slow anything.
        /// </summary>
        protected const float MaxFrameTime = 0.05f;
        /// <summary>
        /// The game time (s) each frame covers while a landing's dip is measured: exactly this, every frame (see
        /// MeasureLandingDip).
        /// </summary>
        protected const float DipFrameTime = 0.02f;

        protected readonly List<Object> created = new();
        protected FrameTimeLimit timeLimit;
        protected Transform target;
        protected MMotionCameraRig rig;
        protected Camera cam;

        [SetUp]
        public void SetUp()
        {
            // Faster than real time: the rig uses scaled time throughout, so only how long a test takes changes.
            timeLimit = FrameTimeLimit.Start(TimeScale, MaxFrameTime);

            target = Track(new GameObject("Camera Target")).transform;
            var cameraObject = Track(new GameObject("Test Camera"));
            cam = cameraObject.AddComponent<Camera>();
            rig = cameraObject.AddComponent<MMotionCameraRig>();
            rig.SetTarget(target);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (Object obj in created) { if (obj != null) { Object.Destroy(obj); } }
            created.Clear();
            Time.captureDeltaTime = 0f; // Should a dip's measurement have been cut short.
            Object.Destroy(timeLimit.gameObject);
            yield return null;
        }

        protected T Track<T>(T obj) where T : Object
        {
            created.Add(obj);
            return obj;
        }

        protected GameObject Box(string name, Vector3 center, Vector3 size)
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

        protected static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++) { yield return null; }
        }

        /// <summary>Waits for a physics step (the rig samples its target once per step) and a frame to apply it.</summary>
        protected static IEnumerator PhysicsStepAndFrame()
        {
            yield return new WaitForFixedUpdate();
            yield return null;
        }

        /// <summary>Where the third person camera should sit for the current settings, with nothing in the way.</summary>
        protected Vector3 ExpectedThirdPersonPosition(float distance) => ExpectedThirdPersonPosition(distance, rig.ThirdPerson.startOnRightShoulder);

        /// <summary>Where the third person camera should sit over the right shoulder (or the left), with nothing in the way.</summary>
        protected Vector3 ExpectedThirdPersonPosition(float distance, bool rightShoulder)
        {
            ThirdPersonSettings tp = rig.ThirdPerson;
            Quaternion yaw = Quaternion.Euler(0f, rig.Yaw, 0f);
            Vector3 shoulder = target.position + Vector3.up * (tp.pivotHeight + tp.shoulderOffset.y)
                               + yaw * Vector3.right * (tp.shoulderOffset.x * (rightShoulder ? 1f : -1f));
            return shoulder + rig.LookRotation * Vector3.back * distance;
        }

        protected Vector3 ExpectedFirstPersonPosition()
        {
            FirstPersonSettings fp = rig.FirstPerson;
            return target.position + Vector3.up * fp.eyeHeight + Quaternion.Euler(0f, rig.Yaw, 0f) * Vector3.forward * fp.eyeForwardOffset;
        }

        /// <summary>Gives the target a size reference scaled to the given size, as the character's model parent is.</summary>
        protected Transform SizeTarget(float size)
        {
            var sizeReference = Track(new GameObject("Size Reference")).transform;
            sizeReference.SetParent(target, false);
            sizeReference.localScale = Vector3.one * size;
            rig.TargetSizeReference = sizeReference;
            return sizeReference;
        }
    }
}
