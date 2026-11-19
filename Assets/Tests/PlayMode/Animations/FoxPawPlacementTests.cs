using System.Collections;
using MeridianMotion.Characters;
using NUnit.Framework;
using UnityEngine.TestTools;
using UnityEngine;

namespace MeridianMotion.Tests.Animations
{
    /// <summary>
    /// Tests of the fox's paw placement (LegPlacementIK, on Fox Model.prefab): standing on a ramp, uphill or down, its paws
    /// are on the ramp and its body pitched with it; standing over a step's edge, its hind paws reach down to the floor
    /// below; and walking, along the flat or up a ramp, each paw comes down onto the ground and none sinks into it. The
    /// model's animation is stepped with the physics, and its paws placed after each step, as they are each frame in the
    /// game. Its planted paws stay where they were put down as it turns, and lock and let go stride by stride as it walks.
    /// (Whether it stays out of stairs and steep ramps, its whole mesh measured against them: FoxTerrainClearanceTests.)
    /// </summary>
    public class FoxPawPlacementTests : FoxPawTestsBase
    {
        /// <summary>
        /// The fox's model swapped for a new one (its own, loaded again) mid-stance, on a ramp: the new model's paw placement
        /// carries on from the old one's (see IModelHandover), faded in as far, the body pitched as far and the paws
        /// locked as they were, rather than fading back in from nothing.
        /// </summary>
        [UnityTest]
        public IEnumerator ASwappedInModel_CarriesOnTheOldOnesPawPlacement()
        {
            Ramp(15f);
            yield return SpawnFox(new Vector3(0f, 0.05f, 0f));
            LegPlacementIK old = paws;
            Assume.That(old.ActiveWeight, Is.GreaterThan(0.9f), "Test setup: the fox's paw placement should be faded in.");
            Assume.That(Mathf.Abs(old.BodyPitch), Is.GreaterThan(5f), "Test setup: the fox should be pitched on the ramp.");
            // The old model's, as it's replaced.
            float weight = 0f, pitch = 0f;
            float[] locks = new float[old.Legs.Length];
            rig.Models.ModelReplacing += _ =>
            {
                weight = old.ActiveWeight;
                pitch = old.BodyPitch;
                for (int i = 0; i < locks.Length; i++) { locks[i] = old.LockWeight(i); }
            };

            // Read as the new model is swapped in, before its first update: what it starts from.
            LegPlacementIK swapped = null;
            float swappedWeight = 0f, swappedPitch = 0f;
            float[] swappedLocks = new float[locks.Length];
            rig.Models.ModelLoaded += model =>
            {
                swapped = model.Root.GetComponentInChildren<LegPlacementIK>();
                if (swapped == null) { return; }
                swappedWeight = swapped.ActiveWeight;
                swappedPitch = swapped.BodyPitch;
                for (int i = 0; i < swappedLocks.Length; i++) { swappedLocks[i] = swapped.LockWeight(i); }
            };
            yield return CharacterTestRig.WaitFor(rig.Models.LoadModelAsync(rig.Models.ModelSource));

            Assert.That(swapped, Is.Not.Null.And.Not.SameAs(old), "Test setup: a new model should have been swapped in.");
            Assert.That(swappedWeight, Is.EqualTo(weight).Within(1e-4f), "Paw placement should stay faded in, not fade in again.");
            Assert.That(swappedPitch, Is.EqualTo(pitch).Within(1e-3f), "The body should stay pitched as it was.");
            for (int i = 0; i < locks.Length; i++)
            {
                Assert.That(swappedLocks[i], Is.EqualTo(locks[i]).Within(1e-4f), $"{swapped.Legs[i].foot.name} should stay locked as it was.");
            }
        }

        [UnityTest]
        public IEnumerator OnARamp_ThePawsStandOnIt_AndTheBodyPitchesWithIt([Values(15f, -15f)] float slope)
        {
            Ramp(slope);
            yield return SpawnFox(new Vector3(0f, 0.05f, 0f));
            Assume.That(rig.Grounded, Is.True, "Test setup: the fox should stand on the ramp.");
            foreach (LegPlacementIK.Leg leg in paws.Legs)
            {
                float above = AboveGround(Contact(leg));
                TestContext.WriteLine($"{leg.foot.name}: {above * 1000f:0} mm above the ramp.");
                Assert.That(above, Is.EqualTo(0f).Within(PlantedTolerance), $"{leg.foot.name} should stand on the ramp.");
            }
            TestContext.WriteLine($"The body is pitched {paws.BodyPitch:0.0}° on a {slope}° ramp.");
            Assert.That(paws.BodyPitch, Is.EqualTo(slope).Within(5f), "The body should pitch with the ramp.");
        }

        [UnityTest]
        public IEnumerator OverAStepsEdge_TheHindPawsReachDownToTheFloor()
        {
            rig.Floor();
            // A step a little over half the hind legs' reach down high, its edge just behind the fox's middle: its body
            // rests on the step, its hind paws over the floor below.
            float stepHeight = 0.15f;
            rig.Box("Step", new Vector3(0f, stepHeight / 2f, 5f), new Vector3(8f, stepHeight, 10f));
            yield return SpawnFox(new Vector3(0f, stepHeight + 0.02f, 0.1f));
            Assume.That(rig.Position.y, Is.EqualTo(stepHeight).Within(0.02f), "Test setup: the fox should stand on the step.");
            foreach (LegPlacementIK.Leg leg in paws.Legs)
            {
                Vector3 contact = Contact(leg);
                float above = AboveGround(contact);
                TestContext.WriteLine($"{leg.foot.name} at z {contact.z:0.00}: {above * 1000f:0} mm above the ground.");
                Assert.That(above, Is.EqualTo(0f).Within(PlantedTolerance), $"{leg.foot.name} should stand on the ground under it.");
            }
            Assert.That(paws.BodyPitch, Is.GreaterThan(1f), "Its hind paws lower than its front, the body should pitch nose up.");
        }

        /// <summary>
        /// Walking up a ramp, each paw comes down onto it every stride (its sole, at its lowest over a stride, on the ramp:
        /// not left floating above it, nor pushed into it), and no paw's joint (its wrist, or the hind paw's hock) goes
        /// into it, its body pitched with it. (Along the flat: Walking_EachPawLocksAsItsPutDown_AndLetsGoAsItLifts.)
        /// </summary>
        [UnityTest]
        public IEnumerator WalkingUpARamp_EachPawComesDownOnIt_AndNoneSinksIntoIt()
        {
            const float slope = 15f;
            Ramp(slope);
            yield return SpawnFox(new Vector3(0f, 0.05f, 0f));
            var contacts = new PawContacts(this);
            yield return rig.Run(2f, _ => CharacterTestRig.Move(Vector3.forward), step =>
            {
                StepAndPlace(step);
                if (step >= 30) { contacts.Measure(); } // Once it's walking.
            });
            TestContext.WriteLine($"The body pitched {paws.BodyPitch:0.0}° on a {slope}° ramp.");
            contacts.AssertComesDownOnTheGround();
            Assert.That(paws.BodyPitch, Is.EqualTo(slope).Within(5f), "Walking up it, the body should pitch with it.");
        }

        /// <summary>Turns the model after the character, as the game does each frame, then steps and places it.</summary>
        private void TurnStepAndPlace(int step)
        {
            rig.Animation.SmoothRotateVisuals(Time.fixedDeltaTime, rig.Movement.ConsumeGroundRotation());
            StepAndPlace(step);
        }

        /// <summary>
        /// Turns the fox on the spot by the given angle, and gives how far a planted paw slid, while planted, as the model
        /// came round (a paw lifted and put down again, stepping round, starts again from where it's put down).
        /// </summary>
        private IEnumerator TurnOnTheSpot(float footLock, float degrees, System.Action<float> slid)
        {
            var field = typeof(LegPlacementIK).GetField("footLock", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            field.SetValue(paws, footLock);
            yield return rig.Run(1f, _ => CharacterTestRig.Idle(), TurnStepAndPlace);
            var putDown = new Vector3?[paws.Legs.Length];
            float most = 0f;
            rig.Body.rotation = rig.Body.rotation * Quaternion.Euler(0f, degrees, 0f);
            yield return rig.Run(0.6f, _ => CharacterTestRig.Idle(), step =>
            {
                TurnStepAndPlace(step);
                for (int i = 0; i < putDown.Length; i++)
                {
                    Vector3 now = Contact(paws.Legs[i]);
                    if (paws.Planted(i) < 0.999f) { putDown[i] = null; continue; }
                    putDown[i] ??= now;
                    Vector3 start = putDown[i].Value;
                    most = Mathf.Max(most, new Vector2(now.x - start.x, now.z - start.z).magnitude);
                }
            });
            slid(most);
        }

        /// <summary>
        /// Turning a little on the spot (no further than a lock holds: Max Lock Distance), the model turns after the
        /// character about its feet, which drags its planted paws round with it, its long body's furthest of all; locked,
        /// they stay where they were put down. Compared with locking turned off.
        /// </summary>
        [UnityTest]
        public IEnumerator TurningALittleOnTheSpot_PlantedPawsStayWhereTheyWerePutDown()
        {
            rig.Floor();
            yield return SpawnFox(new Vector3(0f, 0.05f, 0f));
            float unlocked = 0f, locked = 0f;
            yield return TurnOnTheSpot(0f, 15f, s => unlocked = s);
            yield return TurnOnTheSpot(1f, -15f, s => locked = s);
            TestContext.WriteLine($"Paws slid {unlocked * 100f:0.0} cm unlocked, {locked * 100f:0.0} cm locked, turning 15 degrees on the spot.");
            Assert.That(unlocked, Is.GreaterThan(0.05f), "Test setup: unlocked, the turn should drag the paws round.");
            Assert.That(locked, Is.LessThan(0.2f * unlocked), "Locked, planted paws should stay where they were put down as the model turns.");
        }

        /// <summary>
        /// Walking along the flat, each paw locks as it's put down and lets go as it lifts, stride after stride (never
        /// stuck, and never left behind), and while fully locked, it stays put; and each paw comes down onto the ground
        /// every stride, none sinking into it (as WalkingUpARamp_EachPawComesDownOnIt_AndNoneSinksIntoIt does up a ramp).
        /// </summary>
        [UnityTest]
        public IEnumerator Walking_EachPawLocksAsItsPutDown_AndLetsGoAsItLifts()
        {
            rig.Floor();
            yield return SpawnFox(new Vector3(0f, 0.05f, 0f));
            var locks = new int[paws.Legs.Length];
            var wasLocked = new bool[paws.Legs.Length];
            var lockedAt = new Vector3[paws.Legs.Length];
            float slid = 0f;
            var contacts = new PawContacts(this);
            yield return rig.Run(2f, _ => CharacterTestRig.Move(Vector3.forward), step =>
            {
                StepAndPlace(step);
                if (step >= 30) { contacts.Measure(); } // Once it's walking.
                for (int i = 0; i < locks.Length; i++)
                {
                    bool full = paws.LockWeight(i) >= 1f;
                    Vector3 contact = Contact(paws.Legs[i]);
                    if (full && !wasLocked[i]) { locks[i]++; lockedAt[i] = contact; }
                    else if (full) { slid = Mathf.Max(slid, new Vector2(contact.x - lockedAt[i].x, contact.z - lockedAt[i].z).magnitude); }
                    wasLocked[i] = full;
                }
            });
            TestContext.WriteLine($"Paws locked {string.Join(", ", locks)} times in two seconds' walking; fully locked, a paw moved at most {slid * 1000f:0} mm.");
            Assert.That(locks, Has.All.GreaterThanOrEqualTo(2), "Each paw should lock as it's put down, stride after stride.");
            Assert.That(slid, Is.LessThan(0.005f), "Fully locked, a paw should stay where it was put down.");
            Assert.That(rig.Position.z, Is.GreaterThan(rig.WalkSpeed), "Test setup: the fox should walk.");
            contacts.AssertComesDownOnTheGround();
        }
    
        /// <summary>
        /// Records, each frame after the paws are placed (LateUpdate, late in the frame), how high each paw's contact is
        /// above a platform's top as drawn (its Transform, interpolated between physics steps).
        /// </summary>
        [DefaultExecutionOrder(10000)]
        private class PawHeightRecorder : MonoBehaviour
        {
            public LegPlacementIK paws;
            public Transform platform;
            public float platformHalfHeight;
            public readonly System.Collections.Generic.List<float[]> frames = new();

            private void LateUpdate()
            {
                if (paws == null || platform == null) { return; }
                float top = platform.position.y + platformHalfHeight;
                // Each paw's height above it, and last, the body's (its root bone's).
                var heights = new float[paws.Legs.Length + 1];
                for (int i = 0; i < paws.Legs.Length; i++) { heights[i] = paws.Legs[i].foot.TransformPoint(paws.Legs[i].contact).y - top; }
                heights[paws.Legs.Length] = paws.RootBone.position.y - top;
                frames.Add(heights);
            }
        }

        /// <summary>
        /// Standing on a platform rising and falling, frame by frame as the game draws it (the platform and the fox each
        /// interpolated between physics steps, the paws placed each frame after the Animator), the paws stay on the
        /// platform: their height above it barely changes from one frame to the next.
        /// </summary>
        [UnityTest]
        public IEnumerator OnARisingAndFallingPlatform_ThePawsStayOnIt_FrameByFrame()
        {
            GameObject prefab = CharacterTestRig.LoadPrefab(FoxPrefabPath);
            if (prefab == null) { Assert.Ignore($"There's no fox ('{FoxPrefabPath}'): the fox is the showcase's, not part of the package."); }
            var size = new Vector3(4f, 0.3f, 4f);
            MeridianMotion.Levels.TickedPlatform platform = rig.Platform(Vector3.zero, size, new Vector3(0f, 1.5f, 0f), 3f, Vector3.zero);
            platform.enabled = false; // Held still while the fox's model loads (frames in which the fox isn't stepped).
            Physics.SyncTransforms();
            rig.SpawnCharacter(new Vector3(0f, 0.05f, 0f), prefab);
            animation = rig.Animation;
            yield return rig.WaitForModel();
            platform.enabled = true;
            animator = rig.Models.Animator;
            paws = animator.GetComponent<LegPlacementIK>();
            Assert.That(paws, Is.Not.Null, "The fox's model should have paw placement (LegPlacementIK): regenerate it.");
            // The Animator plays and the paws are placed each frame, as in the game; only the movement is fed to it here.
            yield return rig.Run(1f, _ => CharacterTestRig.Idle(), FeedAnimator);
            var recorder = new GameObject("Paw Height Recorder").AddComponent<PawHeightRecorder>();
            recorder.paws = paws;
            recorder.platform = platform.transform;
            recorder.platformHalfHeight = size.y / 2f;
            try
            {
                yield return rig.Run(3f, _ => CharacterTestRig.Idle(), FeedAnimator);
            }
            finally { Object.Destroy(recorder.gameObject); }
            float jump = 0f, highest = 0f, bodyJump = 0f;
            int body = paws.Legs.Length;
            for (int f = 1; f < recorder.frames.Count; f++)
            {
                for (int i = 0; i < paws.Legs.Length; i++)
                {
                    jump = Mathf.Max(jump, Mathf.Abs(recorder.frames[f][i] - recorder.frames[f - 1][i]));
                    highest = Mathf.Max(highest, Mathf.Abs(recorder.frames[f][i]));
                }
                bodyJump = Mathf.Max(bodyJump, Mathf.Abs(recorder.frames[f][body] - recorder.frames[f - 1][body]));
            }
            TestContext.WriteLine($"Over {recorder.frames.Count} frames, a paw's height above the platform changed by at most {jump * 1000f:0.0} mm " +
                                  $"from one frame to the next (the body's by {bodyJump * 1000f:0.0} mm), and was at most {highest * 1000f:0} mm off it.");
            Assume.That(recorder.frames.Count, Is.GreaterThan(20), "Test setup: there should be frames to measure.");
            Assert.That(jump, Is.LessThan(0.005f), "The paws shouldn't jitter on the platform from one frame to the next.");
            Assert.That(bodyJump, Is.LessThan(0.005f), "The body shouldn't jitter on the platform from one frame to the next.");
            Assert.That(highest, Is.LessThan(PlantedTolerance), "The paws should stay on the platform.");
        }
    
        /// <summary>
        /// Turning on the spot (the character turned, the model turning after it), the fox bends its body into the turn
        /// (its head turned toward it, past its hips), and steps round: the Animator is told it's turning, which plays
        /// Turn Step, and each paw is lifted and put down again as the body comes round.
        /// </summary>
        [UnityTest]
        public IEnumerator TurningOnTheSpot_TheFoxBendsIntoTheTurn_AndStepsRound([Values(90f, -90f)] float degrees)
        {
            rig.Floor();
            yield return SpawnFox(new Vector3(0f, 0.05f, 0f));
            yield return rig.Run(0.3f, _ => CharacterTestRig.Idle(), TurnStepAndPlace);
            Transform head = System.Linq.Enumerable.First(animator.GetComponentsInChildren<Transform>(), t => t.name == "Head");
            Transform hips = paws.RootBone;
            float bend = 0f, headTurn = 0f, turnSpeed = 0f;
            var lifted = new bool[paws.Legs.Length];
            rig.Body.rotation = rig.Body.rotation * Quaternion.Euler(0f, degrees, 0f);
            yield return rig.Run(1f, _ => CharacterTestRig.Idle(), step =>
            {
                TurnStepAndPlace(step);
                float sign = Mathf.Sign(degrees);
                bend = Mathf.Max(bend, sign * paws.TurnBend);
                headTurn = Mathf.Max(headTurn, sign * Vector3.SignedAngle(Vector3.ProjectOnPlane(animator.transform.forward, Vector3.up),
                    Vector3.ProjectOnPlane(head.position - hips.position, Vector3.up), Vector3.up));
                turnSpeed = Mathf.Max(turnSpeed, sign * animator.GetFloat("TurnSpeed"));
                for (int i = 0; i < lifted.Length; i++) { lifted[i] |= paws.Planted(i) < 0.5f; }
            });
            TestContext.WriteLine($"Turning {degrees}°, the body bent as much as {bend:0.0}° into the turn, the head as much as {headTurn:0.0}° " +
                                  $"past the hips' line; the Animator's TurnSpeed reached {turnSpeed:0}°/s; paws lifted: {string.Join(", ", lifted)}.");
            Assert.That(bend, Is.GreaterThan(5f), "The body should bend into the turn.");
            Assert.That(headTurn, Is.GreaterThan(3f), "The head should turn into the turn, past the line of the body.");
            Assert.That(turnSpeed, Is.GreaterThan(10f), "The Animator should be told the fox is turning.");
            Assert.That(lifted, Has.All.True, "Turning on the spot, the fox should step round: each paw lifted and put down again.");
        }
    }
}