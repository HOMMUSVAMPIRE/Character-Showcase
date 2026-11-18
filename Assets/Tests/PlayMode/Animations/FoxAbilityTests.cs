using System.Collections;
using System.Linq;
using Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CharacterShowcase.Tests.Animations
{
    /// <summary>
    /// Tests of the fox's animation and what it can do beyond walking (Fox.prefab, with its movement mode, its Animator and
    /// its paw placement): its own Animator's states as it trots, crouches and jumps (Locomotion, Crouch, Airborne);
    /// swimming, at the surface of deep water at its swim depth, its head out of the water, paddling (Swim), at its swim
    /// speed, without diving; and wall-running, along a wall in mid-air (Wall Run), its body rolled toward the wall, its
    /// paws on the wall's face and nothing of it through the wall. Its tuning is read from its own movement mode. The
    /// model's animation is stepped with the physics, and its paws placed after each step, as they are each frame in the
    /// game.
    /// </summary>
    public class FoxAbilityTests : AnimationTestsBase
    {
        // Its wall run and swimming read what paw placement updates each frame (the wall's roll, the swim's paddle).
        protected override bool StepsPhysicsWithTheAnimation => false;

        private const string FoxPrefabPath = "Assets/Prefabs/Fox.prefab";

        private PawPlacementIK paws;

        /// <summary>Spawns the fox at the feet position given (the world already built), and takes over its animation.</summary>
        private IEnumerator SpawnFox(Vector3 feet)
        {
            GameObject prefab = CharacterTestRig.LoadPrefab(FoxPrefabPath);
            if (prefab == null) { Assert.Ignore($"There's no fox ('{FoxPrefabPath}'): the fox is the showcase's, not part of the package."); }
            Physics.SyncTransforms();
            rig.SpawnCharacter(feet, prefab);
            animation = rig.Animation;
            yield return rig.WaitForModel();
            TakeOverAnimator();
            paws = animator.GetComponent<PawPlacementIK>();
            Assert.That(paws, Is.Not.Null, "The fox's model should have paw placement (PawPlacementIK): regenerate it.");
        }

        /// <summary>Steps the animation, as the game does each frame, then places the paws on the pose it wrote.</summary>
        private void StepAndPlace(int step)
        {
            StepAnimator(step);
            paws.Solve(Time.fixedDeltaTime);
        }

        private bool InState(string state) => animator.GetCurrentAnimatorStateInfo(0).IsName(state);

        private Transform Bone(string name) => animator.GetComponentsInChildren<Transform>().First(t => t.name == name);

        [UnityTest]
        public IEnumerator TheFox_PlaysLocomotion_Crouch_AndAirborne()
        {
            rig.Floor();
            yield return SpawnFox(new Vector3(0f, 0.05f, 0f));
            yield return rig.Run(0.3f, _ => CharacterTestRig.Idle(), StepAnimator); // Settled on the floor.

            yield return rig.Run(0.5f, _ => CharacterTestRig.Move(Vector3.forward), StepAnimator);
            Assert.That(InState("Locomotion"), Is.True, "Trotting, the fox should play Locomotion.");
            yield return rig.Run(0.5f, step => step == 0 ? CharacterTestRig.Crouch() : CharacterTestRig.Idle(), StepAnimator);
            Assert.That(InState("Crouch"), Is.True, "Crouched, the fox should play Crouch.");
            yield return rig.Run(0.5f, step => step == 0 ? CharacterTestRig.Crouch() : CharacterTestRig.Idle(), StepAnimator);
            bool airborne = false;
            yield return rig.Run(0.6f, step => CharacterTestRig.Jump(), step => { StepAnimator(step); airborne |= InState("Airborne"); });
            Assert.That(airborne, Is.True, "Jumping, the fox should play Airborne.");
        }

        /// <summary>Deep water with no floor under it (only swimming holds the fox up), and the fox in it, swimming.</summary>
        private IEnumerator SwimInDeepWater(System.Action<Level.Water> made)
        {
            Level.Water water = rig.Water(new Vector3(0f, 10f, 0f), new Vector3(40f, 8f, 40f));
            made(water);
            yield return SpawnFox(new Vector3(0f, 9.5f, 0f));
            yield return rig.Run(1.5f, _ => CharacterTestRig.Idle(), StepAndPlace);
            Assume.That(rig.Swimming, Is.True, "Test setup: the fox should be swimming.");
        }

        [UnityTest]
        public IEnumerator InDeepWater_TheFoxSwimsAtItsSwimDepth_ItsHeadOutOfTheWater_Paddling()
        {
            Level.Water water = null;
            yield return SwimInDeepWater(w => water = w);
            float depth = rig.SwimDepth;
            Assert.That(water.SurfaceHeight - rig.Position.y, Is.EqualTo(depth).Within(0.03f), "The fox should float with its feet the swim depth below the surface.");
            float head = Bone("Head").position.y - water.SurfaceHeight;
            float back = Bone("Spine1").position.y - water.SurfaceHeight;
            TestContext.WriteLine($"Swimming, the fox's head is {head * 1000f:0} mm above the surface, its back (Spine1) {back * 1000f:0} mm.");
            Assert.That(head, Is.GreaterThan(0.05f), "Its head should be out of the water.");
            Assert.That(back, Is.InRange(-0.2f, 0.1f), "Its back should be about at the waterline.");
            Assert.That(InState("Swim"), Is.True, "Swimming, the fox should paddle (Swim).");
            Assert.That(paws.ActiveWeight, Is.EqualTo(0f), "Swimming, its paws shouldn't be placed on the ground.");
        }

        [UnityTest]
        public IEnumerator Swimming_TheFoxGoesTheWayItsSteered_AtItsSwimSpeed_AndDoesntDive()
        {
            Level.Water water = null;
            yield return SwimInDeepWater(w => water = w);
            float speed = 0f;
            yield return rig.Run(0.6f, _ => CharacterTestRig.Move(Vector3.forward), StepAndPlace);
            yield return rig.MeanSpeed(0.5f, _ => CharacterTestRig.Move(Vector3.forward), s => speed = s);
            float swimSpeed = rig.MovementSetting<float>("swimSpeed") * rig.Movement.SpeedScale;
            Assert.That(speed, Is.EqualTo(swimSpeed).Within(0.1f * swimSpeed), "The fox should swim at its swim speed.");
            // Crouch dives a person; the fox stays at the surface.
            yield return rig.Run(0.8f, step => CharacterTestRig.Crouch(), StepAndPlace);
            Assert.That(rig.Movement.swim.IsUnderwater, Is.False, "The fox shouldn't dive.");
            Assert.That(water.SurfaceHeight - rig.Position.y, Is.EqualTo(rig.SwimDepth).Within(0.05f), "It should stay at the surface.");
        }

        /// <summary>A tall wall along +Z on the fox's right or left, its face a little beside the fox; the fox in mid-air beside it.</summary>
        private IEnumerator LaunchAlongAWall(float side, System.Action<float> face)
        {
            rig.Floor();
            yield return SpawnFox(new Vector3(0f, 0.05f, -10f));
            rig.Body.position = new Vector3(0f, 6f, -10f);
            float wallFace = rig.SideExtent + 0.1f;
            rig.Box("Wall", new Vector3(side * (wallFace + 0.5f), 10f, 0f), new Vector3(1f, 20f, 40f));
            Physics.SyncTransforms();
            face(wallFace);
            rig.Movement.OverrideVelocity(Vector3.forward * rig.Component<WallRunning>().WallRunSpeed * rig.Movement.SpeedScale);
        }

        /// <summary>
        /// Moving fast along a wall in mid-air, steering along it, the fox runs along the wall (Wall Run), its body rolled
        /// toward the wall, its paws on the wall's face (each paw's mesh comes within a little of it), and nothing of it through the
        /// wall: its mesh, all of it, stays on its side of the wall's face.
        /// </summary>
        [UnityTest]
        public IEnumerator AlongAWall_TheFoxRunsOnIt_RolledToIt_ItsPawsOnItsFace([Values(1f, -1f)] float side)
        {
            float face = 0f;
            yield return LaunchAlongAWall(side, f => face = f);
            var skin = animator.GetComponentInChildren<SkinnedMeshRenderer>();
            var baked = new Mesh();
            // Which leg's paw each vertex is (skinned most to a leg's foot bone, or one under it), or -1.
            BoneWeight[] weights = skin.sharedMesh.boneWeights;
            int[] pawOf = weights.Select(w =>
            {
                Transform bone = skin.bones[w.boneIndex0];
                return System.Array.FindIndex(paws.Legs, leg => bone == leg.foot || bone.IsChildOf(leg.foot));
            }).ToArray();
            float deepest = float.NegativeInfinity, rolled = 0f;
            var nearest = Enumerable.Repeat(float.PositiveInfinity, paws.Legs.Length).ToArray();
            bool ran = false, played = false;
            try
            {
                yield return rig.Run(0.8f, _ => CharacterTestRig.Move(Vector3.forward), step =>
                {
                    StepAndPlace(step);
                    ran |= rig.InState(CharacterState.WallRunning);
                    played |= InState("Wall Run");
                    if (step < 10 || !rig.InState(CharacterState.WallRunning)) { return; } // Once it's settled on the wall.
                    rolled = Mathf.Max(rolled, paws.WallRoll);
                    skin.BakeMesh(baked, true);
                    Vector3[] vertices = baked.vertices;
                    for (int v = 0; v < vertices.Length; v++)
                    {
                        float past = side * skin.transform.TransformPoint(vertices[v]).x - face;
                        deepest = Mathf.Max(deepest, past);
                        int leg = pawOf[v];
                        if (leg >= 0) { nearest[leg] = Mathf.Min(nearest[leg], -past); }
                    }
                });
            }
            finally { Object.Destroy(baked); }
            TestContext.WriteLine($"Running along a wall on the {(side > 0 ? "right" : "left")}: rolled {rolled:0.00} of the way to it; its mesh at most " +
                                  $"{deepest * 1000f:0} mm past the wall's face; each paw's mesh nearest to it {string.Join(", ", nearest.Select(n => $"{n * 1000f:0} mm"))}.");
            Assert.That(ran, Is.True, "The fox should run along the wall.");
            Assert.That(played, Is.True, "Running along a wall, the fox should play Wall Run.");
            Assert.That(rolled, Is.EqualTo(1f).Within(0.01f), "Its body should be rolled to the wall.");
            Assert.That(deepest, Is.LessThan(0.02f), "Nothing of the fox should go through the wall.");
            Assert.That(nearest, Has.All.LessThan(0.03f), "Its paws should be put on the wall's face.");
        }
    }
}
