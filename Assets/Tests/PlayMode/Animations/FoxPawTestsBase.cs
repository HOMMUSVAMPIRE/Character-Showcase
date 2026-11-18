using System.Collections;
using System.Collections.Generic;
using Character;
using NUnit.Framework;
using UnityEngine;

namespace CharacterShowcase.Tests.Animations
{
    /// <summary>
    /// What the fox's paw placement tests share (FoxPawPlacementTests, FoxTerrainClearanceTests): spawning the fox with its
    /// animation stepped by hand and its paws placed after each step, as they are each frame in the game; and measuring
    /// where its paws and mesh are against the ground.
    /// </summary>
    public abstract class FoxPawTestsBase : AnimationTestsBase
    {
        protected const string FoxPrefabPath = "Assets/Prefabs/Fox.prefab";
        // How far (m) a planted paw may be off the ground under it: a paw counts as planted until the animation has lifted
        // it Planted Height (see PawPlacementIK), and one tilted to a slope has its toes a little off it either way.
        protected const float PlantedTolerance = 0.03f;

        protected PawPlacementIK paws;

        /// <summary>Spawns the fox at the feet position given (the world already built), and takes over its animation.</summary>
        protected IEnumerator SpawnFox(Vector3 feet)
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
        protected void StepAndPlace(int step)
        {
            StepAnimator(step);
            paws.Solve(Time.fixedDeltaTime);
        }

        protected IEnumerator Settle(float seconds = 0.6f) => rig.Run(seconds, _ => CharacterTestRig.Idle(), StepAndPlace);

        /// <summary>Where a leg's paw touches the ground (its toes), as posed now.</summary>
        protected Vector3 Contact(PawPlacementIK.Leg leg) => leg.foot.TransformPoint(leg.contact);

        /// <summary>How high (m) a point is above the ground under it (not the character's own body).</summary>
        protected float AboveGround(Vector3 point)
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

        /// <summary>A ramp rising at the angle given (degrees) toward +Z, its surface through the origin.</summary>
        protected void Ramp(float angle)
        {
            Quaternion tilt = Quaternion.Euler(-angle, 0f, 0f);
            rig.Box("Ramp", tilt * Vector3.down * 0.5f, new Vector3(8f, 1f, 30f), tilt);
        }

        // ---- The fox's mesh, as posed ----

        private SkinnedMeshRenderer skin;
        private Mesh baked;
        private Vector3[] vertices;
        // For each vertex: whether it's skinned most to a leg's bones; and which leg's paw it's on (its foot bone or one
        // under it), or -1.
        private bool[] onLegs;
        private int[] onPaw;

        [TearDown]
        public void DestroyBakedMesh()
        {
            if (baked != null) { Object.Destroy(baked); }
            boxes = null;
        }

        /// <summary>Bakes the fox's mesh as it's posed now (into vertices, in the skin's space), once a step at most.</summary>
        private void BakeMesh()
        {
            if (skin == null || skin.transform.root != rig.Character.transform)
            {
                skin = animator.GetComponentInChildren<SkinnedMeshRenderer>();
                onLegs = null;
            }
            if (onLegs == null)
            {
                BoneWeight[] weights = skin.sharedMesh.boneWeights;
                onLegs = new bool[weights.Length];
                onPaw = new int[weights.Length];
                for (int i = 0; i < weights.Length; i++)
                {
                    Transform bone = skin.bones[weights[i].boneIndex0];
                    onLegs[i] = bone.name.Contains("Leg") || bone.name.Contains("Paw") || bone.name.Contains("Foot");
                    onPaw[i] = -1;
                    for (int leg = 0; leg < paws.Legs.Length; leg++)
                    {
                        Transform foot = paws.Legs[leg].foot;
                        if (bone == foot || bone.IsChildOf(foot)) { onPaw[i] = leg; break; }
                    }
                }
            }
            if (baked == null) { baked = new Mesh(); }
            skin.BakeMesh(baked, true);
            vertices = baked.vertices;
        }

        /// <summary>
        /// How high (m) each leg's paw's sole is above the ground under it: the lowest point of the mesh skinned to its
        /// foot bone and those under it. (A planted paw rolls onto its toes as it pushes off, and its sole is where it
        /// touches.) The mesh baked once for all the legs.
        /// </summary>
        protected float[] SolesAboveGround()
        {
            BakeMesh();
            Matrix4x4 toWorld = skin.transform.localToWorldMatrix;
            var soles = new Vector3[paws.Legs.Length];
            for (int leg = 0; leg < soles.Length; leg++) { soles[leg] = Vector3.positiveInfinity; }
            for (int v = 0; v < vertices.Length; v++)
            {
                int leg = onPaw[v];
                if (leg < 0) { continue; }
                Vector3 point = toWorld.MultiplyPoint3x4(vertices[v]);
                if (point.y < soles[leg].y) { soles[leg] = point; }
            }
            var above = new float[soles.Length];
            for (int leg = 0; leg < soles.Length; leg++) { above[leg] = AboveGround(soles[leg]); }
            return above;
        }

        // How far (m) the fox's legs may go into the ground: a paw's toes touching a stair's riser, its pad pressed into a
        // steep slope as it tilts to it, a shin or forearm grazing a step's edge as it comes up onto the landing.
        protected const float LegsIntoGroundTolerance = 0.07f;
        // On a flight of stairs as steep as the levels', a little further: a front paw lifting off a tread, the body gone
        // on up over it, has its forearm across the corner of the step above for a moment.
        protected const float StairLegsIntoGroundTolerance = 0.09f;
        // How far (m) the rest of the fox (its body, head and tail) may go into the ground: a graze.
        protected const float BodyIntoGroundTolerance = 0.02f;

        /// <summary>A test box, as the sweep below measures against it: found once per test, not every step.</summary>
        private struct Box
        {
            public Bounds bounds;
            public Matrix4x4 toLocal;
            public Vector3 center, half, scale;
        }

        private List<Box> boxes;

        /// <summary>
        /// How far (m) the fox's mesh is into the ground (the test's boxes) now, at its deepest: its legs' (the vertices
        /// skinned most to a leg's bones) and the rest of it. How far into a box is to the box's nearest face, so a paw's
        /// toes touching a stair's riser count for the little they're into it, not for the step's height. The boxes are
        /// found when it's first asked, once the world's built (they don't move); a vertex outside a box's bounds is
        /// outside the box, so isn't measured against it.
        /// </summary>
        protected (float legs, float body) IntoGround()
        {
            if (boxes == null)
            {
                boxes = new List<Box>();
                foreach (BoxCollider box in Object.FindObjectsByType<BoxCollider>())
                {
                    if (box.transform.IsChildOf(rig.Character.transform)) { continue; }
                    boxes.Add(new Box
                    {
                        bounds = box.bounds,
                        toLocal = box.transform.worldToLocalMatrix,
                        center = box.center,
                        half = box.size / 2f,
                        scale = box.transform.lossyScale,
                    });
                }
            }
            BakeMesh();
            Matrix4x4 toWorld = skin.transform.localToWorldMatrix;
            float legDepth = 0f, bodyDepth = 0f;
            for (int v = 0; v < vertices.Length; v++)
            {
                Vector3 point = toWorld.MultiplyPoint3x4(vertices[v]);
                float depth = 0f;
                for (int b = 0; b < boxes.Count; b++)
                {
                    Box box = boxes[b];
                    if (!box.bounds.Contains(point)) { continue; }
                    Vector3 local = box.toLocal.MultiplyPoint3x4(point) - box.center;
                    float x = (box.half.x - Mathf.Abs(local.x)) * box.scale.x, y = (box.half.y - Mathf.Abs(local.y)) * box.scale.y,
                        z = (box.half.z - Mathf.Abs(local.z)) * box.scale.z;
                    if (x > 0f && y > 0f && z > 0f) { depth = Mathf.Max(depth, Mathf.Min(x, Mathf.Min(y, z))); }
                }
                if (onLegs[v]) { legDepth = Mathf.Max(legDepth, depth); }
                else { bodyDepth = Mathf.Max(bodyDepth, depth); }
            }
            return (legDepth, bodyDepth);
        }

        /// <summary>
        /// Records, walking, how each paw comes down onto the ground and how low its joint goes: each paw's sole at its
        /// lowest over each half second (longer than the walk's stride), the worst of those, and the lowest any paw's joint
        /// (its wrist, or a hind paw's hock) goes. Call Measure after each step, once the fox is walking.
        /// </summary>
        protected sealed class PawContacts
        {
            private const int Window = 25;
            private readonly FoxPawTestsBase tests;
            private readonly float[] lowestSole;
            private int measured;
            public float worstTouch, lowestJoint = float.PositiveInfinity;
            public string worstPaw = "", lowestPaw = "";

            public PawContacts(FoxPawTestsBase tests)
            {
                this.tests = tests;
                lowestSole = new float[tests.paws.Legs.Length];
            }

            public void Measure()
            {
                float[] soles = tests.SolesAboveGround();
                for (int i = 0; i < soles.Length; i++)
                {
                    PawPlacementIK.Leg leg = tests.paws.Legs[i];
                    if (measured % Window == 0) { lowestSole[i] = float.PositiveInfinity; }
                    lowestSole[i] = Mathf.Min(lowestSole[i], soles[i]);
                    if (measured % Window == Window - 1 && Mathf.Abs(lowestSole[i]) > Mathf.Abs(worstTouch))
                    {
                        worstTouch = lowestSole[i];
                        worstPaw = leg.foot.name;
                    }
                    float joint = tests.AboveGround(leg.foot.position);
                    if (joint < lowestJoint) { lowestJoint = joint; lowestPaw = leg.foot.name; }
                }
                measured++;
            }

            /// <summary>Asserts each paw came down onto the ground every stride, and none went into it.</summary>
            public void AssertComesDownOnTheGround()
            {
                TestContext.WriteLine($"Coming down, a paw's sole was at most {worstTouch * 1000f:0} mm off the ground ({worstPaw}); the lowest " +
                                      $"paw joint was {lowestJoint * 1000f:0} mm above it ({lowestPaw}).");
                Assert.That(worstTouch, Is.EqualTo(0f).Within(PlantedTolerance), "Walking, each paw should come down onto the ground, every stride.");
                Assert.That(lowestJoint, Is.GreaterThan(0f), "Walking, no paw should go into the ground.");
            }
        }
    }
}
