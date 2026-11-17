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
    /// ramp, no part of it goes into them, its body pitched with them.
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

        /// <summary>A ramp rising at the angle given (degrees) toward +Z, its surface through the origin.</summary>
        private void Ramp(float angle)
        {
            Quaternion tilt = Quaternion.Euler(-angle, 0f, 0f);
            rig.Box("Ramp", tilt * Vector3.down * 0.5f, new Vector3(8f, 1f, 30f), tilt);
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
        /// Walking up a ramp (or along the flat, to compare), the planted paws stand on it, and no paw's joint (its wrist,
        /// or the hind paw's hock) goes into it. A swinging paw's contact isn't measured: it's a point fixed at the paw's
        /// toes, on the ground only while it's down, and it swings below the paw's joint as the paw curls.
        /// </summary>
        [UnityTest]
        public IEnumerator WalkingUpARamp_ThePlantedPawsStandOnIt_AndNoneSinksIntoIt([Values(0f, 15f)] float slope)
        {
            Ramp(slope);
            yield return SpawnFox(new Vector3(0f, 0.05f, 0f));
            float worstPlanted = 0f, lowestJoint = float.PositiveInfinity;
            string worstPaw = "", lowestPaw = "";
            yield return rig.Run(1.5f, _ => CharacterTestRig.Move(Vector3.forward), step =>
            {
                StepAndPlace(step);
                if (step < 30) { return; } // Once it's walking.
                for (int i = 0; i < paws.Legs.Length; i++)
                {
                    PawPlacementIK.Leg leg = paws.Legs[i];
                    if (paws.Planted(i) >= 1f)
                    {
                        float above = AboveGround(Contact(leg));
                        if (Mathf.Abs(above) > Mathf.Abs(worstPlanted)) { worstPlanted = above; worstPaw = leg.foot.name; }
                    }
                    float joint = AboveGround(leg.foot.position);
                    if (joint < lowestJoint) { lowestJoint = joint; lowestPaw = leg.foot.name; }
                }
            });
            TestContext.WriteLine($"Planted paws were at most {worstPlanted * 1000f:0} mm off the ground ({worstPaw}); the lowest paw " +
                                  $"joint was {lowestJoint * 1000f:0} mm above it ({lowestPaw}); the body pitched {paws.BodyPitch:0.0}°.");
            Assert.That(worstPlanted, Is.EqualTo(0f).Within(PlantedTolerance), "Walking, the planted paws should stand on the ground.");
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
            Assert.That(legDepth, Is.LessThan(LegsIntoGroundTolerance), "Its legs should stay (all but) out of the steps.");
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
    }
}
