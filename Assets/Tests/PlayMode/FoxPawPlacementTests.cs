using System.Collections;
using Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CharacterShowcase.Tests
{
    /// <summary>
    /// Tests of the fox's paw placement (PawPlacementIK, on Fox Model.prefab): standing on a ramp, uphill or down, its paws
    /// are on the ramp and its body pitched with it; standing over a step's edge, its hind paws reach down to the floor
    /// below; and walking up a ramp, its planted paws stand on it and none sinks into it. The model's animation is stepped with the physics, and its paws
    /// placed after each step, as they are each frame in the game. Walking up and down a flight of stairs and up a steep
    /// ramp, no part of it goes into them, its body pitched with them. Its planted paws stay where they were put down as
    /// it turns, and lock and let go stride by stride as it walks.
    /// </summary>
    public class FoxPawPlacementTests : AnimationTestsBase
    {
        private const string FoxPrefabPath = "Assets/Prefabs/Fox.prefab";
        // How far (m) a planted paw may be off the ground under it: a paw counts as planted until the animation has lifted
        // it Planted Height (see PawPlacementIK), and one tilted to a slope has its toes a little off it either way.
        private const float PlantedTolerance = 0.03f;

        private PawPlacementIK paws;

        /// <summary>Spawns the fox at the feet position given (the world already built), and takes over its animation.</summary>
        private IEnumerator SpawnFox(Vector3 feet)
        {
            GameObject prefab = CharacterTestRig.LoadPrefab(FoxPrefabPath);
            if (prefab == null) { Assert.Ignore($"There's no fox ('{FoxPrefabPath}'): generate it with Tools > Character Showcase > Generate Fox."); }
            Physics.SyncTransforms();
            rig.SpawnCharacter(feet, prefab);
            animation = rig.Animation;
            yield return rig.WaitForModel();
            TakeOverAnimator();
            paws = animator.GetComponent<PawPlacementIK>();
            if (paws == null) { Assert.Fail("The fox's model should have paw placement (PawPlacementIK): regenerate it."); }
            yield return Settle();
        }

        /// <summary>Steps the animation, as the game does each frame, then places the paws on the pose it wrote.</summary>
        private void StepAndPlace(int step)
        {
            StepAnimator(step);
            paws.Solve(Time.fixedDeltaTime);
        }

        private IEnumerator Settle(float seconds = 0.6f) => rig.Run(seconds, _ => CharacterTestRig.Idle(), StepAndPlace);

        /// <summary>Where a leg's paw touches the ground (its toes), as posed now.</summary>
        private Vector3 Contact(PawPlacementIK.Leg leg) => leg.foot.TransformPoint(leg.contact);

        /// <summary>How high (m) a point is above the ground under it (not the character's own body).</summary>
        private float AboveGround(Vector3 point)
        {
            RaycastHit[] hits = Physics.RaycastAll(point + Vector3.up, Vector3.down, 3f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.PositiveInfinity;
            foreach (RaycastHit hit in hits)
            {
                if (hit.transform.IsChildOf(rig.Character.transform)) { continue; }
                best = Mathf.Min(best, point.y - hit.point.y);
            }
            Assert.That(best, Is.LessThan(float.PositiveInfinity), "Test setup: there should be ground under each paw.");
            return best;
        }

        // How far (m) the fox's legs may go into the ground: a paw's toes touching a stair's riser, its pad pressed into a
        // steep slope as it tilts to it, a shin or forearm grazing a step's edge as it comes up onto the landing.
        private const float LegsIntoGroundTolerance = 0.07f;
        // On a flight of stairs as steep as the levels', a little further: a front paw lifting off a tread, the body gone
        // on up over it, has its forearm across the corner of the step above for a moment.
        private const float StairLegsIntoGroundTolerance = 0.09f;
        // How far (m) the rest of the fox (its body, head and tail) may go into the ground: a graze.
        private const float BodyIntoGroundTolerance = 0.02f;
        private Mesh baked;
        private bool[] onLegs;

        /// <summary>
        /// How far (m) the fox's mesh is into the ground (the test's boxes) now, at its deepest: its legs' (the vertices
        /// skinned most to a leg's bones) and the rest of it. How far into a box is to the box's nearest face, so a paw's
        /// toes touching a stair's riser count for the little they're into it, not for the step's height.
        /// </summary>
        private (float legs, float body) IntoGround()
        {
            var skin = animator.GetComponentInChildren<SkinnedMeshRenderer>();
            if (onLegs == null)
            {
                BoneWeight[] weights = skin.sharedMesh.boneWeights;
                onLegs = new bool[weights.Length];
                for (int i = 0; i < weights.Length; i++)
                {
                    string bone = skin.bones[weights[i].boneIndex0].name;
                    onLegs[i] = bone.Contains("Leg") || bone.Contains("Paw") || bone.Contains("Foot");
                }
            }
            if (baked == null) { baked = new Mesh(); }
            skin.BakeMesh(baked, true);
            Vector3[] vertices = baked.vertices;
            BoxCollider[] boxes = Object.FindObjectsByType<BoxCollider>(FindObjectsSortMode.None);
            float legDepth = 0f, bodyDepth = 0f;
            for (int v = 0; v < vertices.Length; v++)
            {
                Vector3 point = skin.transform.TransformPoint(vertices[v]);
                float depth = 0f;
                foreach (BoxCollider box in boxes)
                {
                    if (box.transform.IsChildOf(rig.Character.transform)) { continue; }
                    Vector3 local = box.transform.InverseTransformPoint(point) - box.center;
                    Vector3 half = box.size / 2f, size = box.transform.lossyScale;
                    float x = (half.x - Mathf.Abs(local.x)) * size.x, y = (half.y - Mathf.Abs(local.y)) * size.y, z = (half.z - Mathf.Abs(local.z)) * size.z;
                    if (x > 0f && y > 0f && z > 0f) { depth = Mathf.Max(depth, Mathf.Min(x, Mathf.Min(y, z))); }
                }
                if (onLegs[v]) { legDepth = Mathf.Max(legDepth, depth); }
                else { bodyDepth = Mathf.Max(bodyDepth, depth); }
            }
            return (legDepth, bodyDepth);
        }

        [TearDown]
        public void DestroyBakedMesh()
        {
            if (baked != null) { Object.Destroy(baked); }
        }

        /// <summary>
        /// How high (m) a leg's paw's sole is above the ground under it: the lowest point of the mesh skinned to its foot
        /// bone and those under it. (A planted paw rolls onto its toes as it pushes off, and its sole is where it touches.)
        /// </summary>
        private float SoleAboveGround(PawPlacementIK.Leg leg)
        {
            var skin = animator.GetComponentInChildren<SkinnedMeshRenderer>();
            if (baked == null) { baked = new Mesh(); }
            skin.BakeMesh(baked, true);
            Vector3[] vertices = baked.vertices;
            BoneWeight[] weights = skin.sharedMesh.boneWeights;
            Vector3 sole = Vector3.positiveInfinity;
            for (int v = 0; v < vertices.Length; v++)
            {
                Transform bone = skin.bones[weights[v].boneIndex0];
                if (bone != leg.foot && !bone.IsChildOf(leg.foot)) { continue; }
                Vector3 point = skin.transform.TransformPoint(vertices[v]);
                if (point.y < sole.y) { sole = point; }
            }
            return AboveGround(sole);
        }

        /// <summary>A ramp rising at the angle given (degrees) toward +Z, its surface through the origin.</summary>
        private void Ramp(float angle)
        {
            Quaternion tilt = Quaternion.Euler(-angle, 0f, 0f);
            rig.Box("Ramp", tilt * Vector3.down * 0.5f, new Vector3(8f, 1f, 30f), tilt);
        }

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
            PawPlacementIK old = paws;
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
            PawPlacementIK swapped = null;
            float swappedWeight = 0f, swappedPitch = 0f;
            float[] swappedLocks = new float[locks.Length];
            rig.Models.ModelLoaded += model =>
            {
                swapped = model.Root.GetComponentInChildren<PawPlacementIK>();
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
            foreach (PawPlacementIK.Leg leg in paws.Legs)
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
            foreach (PawPlacementIK.Leg leg in paws.Legs)
            {
                Vector3 contact = Contact(leg);
                float above = AboveGround(contact);
                TestContext.WriteLine($"{leg.foot.name} at z {contact.z:0.00}: {above * 1000f:0} mm above the ground.");
                Assert.That(above, Is.EqualTo(0f).Within(PlantedTolerance), $"{leg.foot.name} should stand on the ground under it.");
            }
            Assert.That(paws.BodyPitch, Is.GreaterThan(1f), "Its hind paws lower than its front, the body should pitch nose up.");
        }

        /// <summary>
        /// Walking up a ramp (or along the flat, to compare), each paw comes down onto it every stride (its sole, at its
        /// lowest over a stride, on the ramp: not left floating above it, nor pushed into it), and no paw's joint (its
        /// wrist, or the hind paw's hock) goes into it.
        /// </summary>
        [UnityTest]
        public IEnumerator WalkingUpARamp_EachPawComesDownOnIt_AndNoneSinksIntoIt([Values(0f, 15f)] float slope)
        {
            Ramp(slope);
            yield return SpawnFox(new Vector3(0f, 0.05f, 0f));
            // Each paw's sole at its lowest over each half second (longer than the walk's stride), and the lowest a paw's
            // joint goes.
            const int window = 25;
            var lowestSole = new float[paws.Legs.Length];
            float worstTouch = 0f, lowestJoint = float.PositiveInfinity;
            string worstPaw = "", lowestPaw = "";
            yield return rig.Run(2f, _ => CharacterTestRig.Move(Vector3.forward), step =>
            {
                StepAndPlace(step);
                if (step < 30) { return; } // Once it's walking.
                for (int i = 0; i < paws.Legs.Length; i++)
                {
                    PawPlacementIK.Leg leg = paws.Legs[i];
                    if ((step - 30) % window == 0) { lowestSole[i] = float.PositiveInfinity; }
                    lowestSole[i] = Mathf.Min(lowestSole[i], SoleAboveGround(leg));
                    if ((step - 30) % window == window - 1 && Mathf.Abs(lowestSole[i]) > Mathf.Abs(worstTouch))
                    {
                        worstTouch = lowestSole[i];
                        worstPaw = leg.foot.name;
                    }
                    float joint = AboveGround(leg.foot.position);
                    if (joint < lowestJoint) { lowestJoint = joint; lowestPaw = leg.foot.name; }
                }
            });
            TestContext.WriteLine($"Coming down, a paw's sole was at most {worstTouch * 1000f:0} mm off the ground ({worstPaw}); the lowest " +
                                  $"paw joint was {lowestJoint * 1000f:0} mm above it ({lowestPaw}); the body pitched {paws.BodyPitch:0.0}°.");
            Assert.That(worstTouch, Is.EqualTo(0f).Within(PlantedTolerance), "Walking, each paw should come down onto the ground, every stride.");
            Assert.That(lowestJoint, Is.GreaterThan(0f), "Walking, no paw should go into the ground.");
            Assert.That(paws.BodyPitch, Is.EqualTo(slope).Within(5f), "Walking up it, the body should pitch with it.");
        }
    
        /// <summary>
        /// Walking up or down a flight of stairs (as steep as the levels'), the fox stays out of the steps (its legs all
        /// but touching them: see IntoGround), its body pitched up or down the flight, not left level as the flat
        /// animation has it. Its long box body rests on the step under its front end going up and its back end going
        /// down, half a metre above the flight under its middle: its body has to be lowered onto the flight, along it.
        /// </summary>
        [UnityTest]
        public IEnumerator WalkingUpOrDownStairs_TheFoxStaysOutOfTheSteps_AndPitchesWithThem([Values(true, false)] bool up)
        {
            rig.Floor();
            yield return SpawnFox(new Vector3(0f, 0.05f, 0f));
            // As steep as the levels' stairs (3 up to 4 along), steps a little under the step height.
            const int steps = 12;
            float rise = 0.85f * rig.StepHeight, run = 4f / 3f * rise, top = steps * rise;
            for (int i = 0; i < steps - 1; i++)
            {
                float height = up ? rise * (i + 1) : top - rise * (i + 1);
                rig.Box($"Step {i + 1}", new Vector3(0f, height / 2f, 1.5f + i * run + run / 2f), new Vector3(6f, height, run));
            }
            rig.Box("Landing", new Vector3(0f, top / 2f, up ? 1.5f + (steps - 1) * run + 5f : -3.5f), new Vector3(6f, top, 10f));
            Physics.SyncTransforms();
            if (!up)
            {
                rig.Teleport(new Vector3(0f, top + 0.02f, -1f));
                yield return Settle();
            }
            float legDepth = 0f, bodyDepth = 0f, pitch = 0f;
            float firstStep = 1.5f, lastStep = 1.5f + (steps - 1) * run;
            yield return rig.Run(rig.SecondsToWalk(lastStep - firstStep + 2f), _ => CharacterTestRig.Move(Vector3.forward), step =>
            {
                StepAndPlace(step);
                float z = rig.Position.z;
                if (z < firstStep - 0.5f) { return; } // From reaching the flight (not starting off on the flat).
                (float legsIn, float bodyIn) = IntoGround();
                legDepth = Mathf.Max(legDepth, legsIn);
                bodyDepth = Mathf.Max(bodyDepth, bodyIn);
                // Pitched most on the flight's middle, with all four paws on it.
                if (z > firstStep + 1f && z < lastStep - 1f) { pitch = up ? Mathf.Max(pitch, paws.BodyPitch) : Mathf.Min(pitch, paws.BodyPitch); }
            });
            float slope = Mathf.Atan2(rise, run) * Mathf.Rad2Deg;
            TestContext.WriteLine($"At deepest, its legs went {legDepth * 1000f:0} mm into the steps and its body {bodyDepth * 1000f:0} mm; " +
                                  $"its body pitched as much as {pitch:0.0}° on {slope:0.0}° stairs.");
            Assume.That(rig.Position.z, Is.GreaterThan(lastStep), "Test setup: the fox should walk the flight.");
            Assert.That(bodyDepth, Is.LessThan(BodyIntoGroundTolerance), "The fox's body, head and tail should stay out of the steps.");
            Assert.That(legDepth, Is.LessThan(StairLegsIntoGroundTolerance), "Its legs should stay (all but) out of the steps.");
            Assert.That(Mathf.Abs(pitch), Is.EqualTo(slope).Within(8f), "The body should pitch with the stairs.");
        }

        /// <summary>
        /// Walking up a steep ramp (the levels' steepest slope is 40°), the fox stays out of it, its body pitched with it,
        /// its tail held up off it (Counter Pitch Bones) rather than swung down into it.
        /// </summary>
        [UnityTest]
        public IEnumerator WalkingUpASteepRamp_TheFoxStaysOutOfIt([Values(30f, 40f)] float slope)
        {
            rig.Floor();
            Quaternion tilt = Quaternion.Euler(-slope, 0f, 0f);
            const float length = 6f;
            Vector3 bottom = new Vector3(0f, 0f, 1.5f), along = tilt * Vector3.forward * length;
            rig.Box("Ramp", bottom + along / 2f + tilt * Vector3.down * 0.5f, new Vector3(6f, 1f, length), tilt);
            rig.Box("Ramp Top", new Vector3(0f, along.y / 2f, bottom.z + along.z + 5f), new Vector3(6f, along.y, 10f));
            yield return SpawnFox(new Vector3(0f, 0.05f, 0f));
            float legDepth = 0f, bodyDepth = 0f, pitch = 0f;
            yield return rig.Run(rig.SecondsToWalk(along.z), _ => CharacterTestRig.Move(Vector3.forward), step =>
            {
                StepAndPlace(step);
                float z = rig.Position.z;
                if (z < bottom.z - 1f || z > bottom.z + along.z) { return; } // On the ramp.
                (float legsIn, float bodyIn) = IntoGround();
                legDepth = Mathf.Max(legDepth, legsIn);
                bodyDepth = Mathf.Max(bodyDepth, bodyIn);
                pitch = Mathf.Max(pitch, paws.BodyPitch);
            });
            TestContext.WriteLine($"At deepest, its legs went {legDepth * 1000f:0} mm into the ramp and its body {bodyDepth * 1000f:0} mm; " +
                                  $"its body pitched as much as {pitch:0.0}° on a {slope}° ramp.");
            Assume.That(rig.Position.z, Is.GreaterThan(bottom.z + 1f), "Test setup: the fox should walk up the ramp.");
            Assert.That(bodyDepth, Is.LessThan(BodyIntoGroundTolerance), "The fox's body, head and tail should stay out of the ramp.");
            Assert.That(legDepth, Is.LessThan(LegsIntoGroundTolerance), "Its legs should stay (all but) out of the ramp.");
            Assert.That(pitch, Is.EqualTo(slope).Within(5f), "The body should pitch with the ramp.");
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
        private IEnumerator TurnOnTheSpot(float pawLock, float degrees, System.Action<float> slid)
        {
            var field = typeof(PawPlacementIK).GetField("pawLock", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            field.SetValue(paws, pawLock);
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
        /// Walking, each paw locks as it's put down and lets go as it lifts, stride after stride (never stuck, and never
        /// left behind), and while fully locked, it stays put.
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
            yield return rig.Run(2f, _ => CharacterTestRig.Move(Vector3.forward), step =>
            {
                StepAndPlace(step);
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
        }
    
        /// <summary>
        /// Records, each frame after the paws are placed (LateUpdate, late in the frame), how high each paw's contact is
        /// above a platform's top as drawn (its Transform, interpolated between physics steps).
        /// </summary>
        [DefaultExecutionOrder(10000)]
        private class PawHeightRecorder : MonoBehaviour
        {
            public PawPlacementIK paws;
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
            if (prefab == null) { Assert.Ignore($"There's no fox ('{FoxPrefabPath}'): generate it with Tools > Character Showcase > Generate Fox."); }
            var size = new Vector3(4f, 0.3f, 4f);
            Level.MovingPlatform platform = rig.Platform(Vector3.zero, size, new Vector3(0f, 1.5f, 0f), 3f, Vector3.zero);
            platform.enabled = false; // Held still while the fox's model loads (frames in which the fox isn't stepped).
            Physics.SyncTransforms();
            rig.SpawnCharacter(new Vector3(0f, 0.05f, 0f), prefab);
            animation = rig.Animation;
            yield return rig.WaitForModel();
            platform.enabled = true;
            animator = rig.Models.Animator;
            paws = animator.GetComponent<PawPlacementIK>();
            Assert.That(paws, Is.Not.Null, "The fox's model should have paw placement (PawPlacementIK): regenerate it.");
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
