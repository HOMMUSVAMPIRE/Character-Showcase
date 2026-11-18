using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CharacterShowcase.Tests
{
    /// <summary>What the animation tests share: a rig, stepping the model's animation with the physics, and measuring it.</summary>
    [Category(TestArea.Animation)]
    public abstract class AnimationTestsBase : CharacterRigFixture
    {
        // Its tests need Unity's frames (the model's, animation's or IK's per-frame updates): see CharacterRigFixture.MovementOnly.
        protected override bool MovementOnly => false;

        protected AnimationController animation;
        protected Animator animator;


        /// <summary>Feeds the latest movement to the animator, as ACCharacterController does each frame in the game.</summary>
        protected void FeedAnimator(int _) => animation.UpdateAnimator(rig.Movement.lastFrameMovementData, Time.fixedDeltaTime);

        /// <summary>
        /// Steps the model's animation by hand with the physics (see StepAnimator), so measurements don't depend on the
        /// frame rate, and stops interpolating the character so the model sits exactly where physics put it. With the
        /// animation stepped by hand, nothing waits for Unity's frames, so the rig steps the physics itself from here on
        /// (far faster, the same steps: see CharacterTestRig.StepsPhysicsItself), unless the world has something only
        /// Unity's steps move (a moving platform, which the rig then waits for), or the fixture says its tests still need
        /// Unity's frames (StepsPhysicsWithTheAnimation).
        /// </summary>
        protected void TakeOverAnimator()
        {
            rig.Body.interpolation = RigidbodyInterpolation.None;
            animator = rig.Models.Animator;
            animator.enabled = false;
            rig.StepsPhysicsItself = StepsPhysicsWithTheAnimation;
        }

        /// <summary>
        /// Whether, once the Animator is taken over (TakeOverAnimator), the rig steps the physics itself. Off for a fixture
        /// whose tests still read what's updated each frame (the model's easing, or the animation state paw placement reads).
        /// </summary>
        protected virtual bool StepsPhysicsWithTheAnimation => true;

        protected void StepAnimator(int step)
        {
            FeedAnimator(step);
            animator.Update(Time.fixedDeltaTime);
        }

        /// <summary>Spawns the character on a floor with its own model, and skips the test if that isn't a humanoid.</summary>
        protected IEnumerator SpawnWithOwnModel()
        {
            rig.Floor();
            rig.SpawnCharacter(new Vector3(0f, 0.05f, 0f));
            animation = rig.Animation;
            yield return rig.WaitForModel();
            Animator modelAnimator = rig.Models.Animator;
            if (modelAnimator == null || !modelAnimator.isHuman || modelAnimator.runtimeAnimatorController == null)
            {
                Assert.Ignore("The character's model isn't an animated humanoid.");
            }
        }

        /// <summary>Spawns the character on a floor and gives it the model the source describes.</summary>
        protected IEnumerator SpawnWithModel(CharacterModelSource source)
        {
            rig.Floor();
            rig.SpawnCharacter(new Vector3(0f, 0.05f, 0f));
            animation = rig.Animation;
            yield return rig.WaitForModel();
            bool loaded = false;
            yield return CharacterTestRig.WaitFor(rig.Models.LoadModelAsync(source), l => loaded = l);
            Assert.That(loaded, Is.True, "The model should load.");
            Assert.That(rig.Models.Animator, Is.Not.Null, "The model should have an Animator.");
            Assert.That(rig.Models.Animator.runtimeAnimatorController, Is.Not.Null, "The model's Animator should have a controller.");
        }

        // Height (m, for a character of normal size) above its lowest within which the ankle and the toe joint count as
        // down. The heel lifts the ankle several centimetres as the foot rolls onto its toes, and the toes lift as it swings.
        private const float FlatFootBand = 0.01f;
        // The fewest physics steps a flat foot must stay down for its movement to count (a walking foot is flat for
        // about a quarter of a second).
        private const int MinimumFlatSteps = 4;

        /// <summary>
        /// Walks the character straight ahead at the given speed (m/s) and measures how fast its planted feet slide over
        /// the floor, as a fraction of its speed: how far each foot moves while it's flat on the floor (its ankle and toe
        /// joint both at their lowest), divided by how long it's flat. 0 means the feet stay put; 1, that they're dragged
        /// along with the character. A foot that slides backward (animation playing too fast) counts the same as one
        /// sliding forward.
        ///
        /// Walking, rather than running, because a walking foot stays flat for a good part of each step, while a running
        /// foot is down for only a few physics steps and is rolling onto its toes the whole time.
        /// </summary>
        protected IEnumerator MeasureFootSlide(float speed, System.Action<float> result)
        {
            TakeOverAnimator();
            HumanBodyBones[,] bones = { { HumanBodyBones.LeftFoot, HumanBodyBones.LeftToes }, { HumanBodyBones.RightFoot, HumanBodyBones.RightToes } };
            Transform Bone(int foot, int part) => animator.GetBoneTransform(bones[foot, part]);
            for (int foot = 0; foot < 2; foot++)
            {
                if (Bone(foot, 0) == null || Bone(foot, 1) == null) { Assert.Ignore("The model has no foot or toe bones to watch."); }
            }
            Inputs walk = CharacterTestRig.Move(Vector3.forward * Mathf.Min(1f, speed / rig.WalkSpeed));

            // Reach walking speed and let the speed blend settle before measuring.
            yield return rig.Run(1f, _ => walk, StepAnimator);
            var ankles = new List<Vector3>[] { new(), new() };
            var toes = new List<Vector3>[] { new(), new() };
            Vector3 start = rig.Position;
            yield return rig.Run(3f, _ => walk, step =>
            {
                StepAnimator(step);
                for (int foot = 0; foot < 2; foot++)
                {
                    ankles[foot].Add(Bone(foot, 0).position);
                    toes[foot].Add(Bone(foot, 1).position);
                }
            });
            float measured = Vector3.Distance(start, rig.Position) / (ankles[0].Count * Time.fixedDeltaTime);

            float moved = 0f;
            int flatSteps = 0;
            for (int foot = 0; foot < 2; foot++)
            {
                float lowestAnkle = ankles[foot].Min(p => p.y), lowestToe = toes[foot].Min(p => p.y);
                // The band scales with the character's height, as its foot lifts do.
                float band = FlatFootBand * rig.Controller.Scale.y;
                bool Flat(int i) => ankles[foot][i].y < lowestAnkle + band && toes[foot][i].y < lowestToe + band;
                // Each run of flat steps is one footfall: measure how far the foot got from where it landed.
                for (int i = 0; i < ankles[foot].Count; i++)
                {
                    if (!Flat(i)) { continue; }
                    int end = i;
                    while (end + 1 < ankles[foot].Count && Flat(end + 1)) { end++; }
                    if (end - i >= MinimumFlatSteps)
                    {
                        Vector3 offset = toes[foot][end] - toes[foot][i];
                        moved += new Vector2(offset.x, offset.z).magnitude;
                        flatSteps += end - i;
                    }
                    i = end;
                }
            }
            Assert.That(flatSteps, Is.GreaterThan(4 * MinimumFlatSteps), "Test setup: the feet should land flat several times.");
            Assert.That(rig.Grounded, Is.True);
            Assert.That(measured, Is.EqualTo(speed).Within(0.1f * speed), "Test setup: the character should walk at the speed asked for.");
            float slide = moved / (flatSteps * Time.fixedDeltaTime) / measured;
            TestContext.WriteLine($"At {rig.Controller.Scale}, walking at {measured:0.000} m/s, feet flat for {flatSteps} steps, sliding at {slide:P1} of that.");
            result(slide);
        }

        /// <summary>The lowest point of the model's meshes as they're posed now, in world space.</summary>
        protected float LowestPointOfModel()
        {
            float lowest = float.PositiveInfinity;
            var baked = new Mesh();
            foreach (SkinnedMeshRenderer skinned in rig.Models.Model.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                // Baked without the renderer's scale taken out: world units, relative to its position and rotation.
                skinned.BakeMesh(baked, false);
                Matrix4x4 toWorld = Matrix4x4.TRS(skinned.transform.position, skinned.transform.rotation, Vector3.one);
                foreach (Vector3 vertex in baked.vertices) { lowest = Mathf.Min(lowest, toWorld.MultiplyPoint3x4(vertex).y); }
            }
            Object.Destroy(baked);
            return lowest;
        }

        /// <summary>
        /// Stands the (already spawned) model idle at the given scale, with foot placement at the given weight, and gives
        /// back how far its soles were from the floor once settled, at their lowest and highest.
        /// </summary>
        protected IEnumerator MeasureStandingSoles(Vector3 scale, float footPlacementWeight, System.Action<(float lowest, float highest)> result)
        {
            yield return new WaitForFixedUpdate(); // So the floor reaches physics queries before the room check.
            Assert.That(rig.Controller.SetScale(scale, immediately: true), Is.True);
            TakeOverAnimator();
            var feet = animator.GetComponent<FootPlacementIK>();
            if (feet != null) { feet.weight = footPlacementWeight; }
            float lowest = float.PositiveInfinity, highest = float.NegativeInfinity;
            // Settled first: resizing at once can drop the character onto the floor (a tenth of its size, from 4 cm
            // up), and a landing animation crouches for most of a second before it stands.
            yield return rig.Run(2f, _ => CharacterTestRig.Idle(), step =>
            {
                StepAnimator(step);
                if (step < CharacterTestRig.StepsFor(1.5f)) { return; }
                float sole = LowestPointOfModel();
                lowest = Mathf.Min(lowest, sole);
                highest = Mathf.Max(highest, sole);
            });
            TestContext.WriteLine($"At {scale}, foot placement at {footPlacementWeight}: soles between {lowest * 1000f:0.00} and {highest * 1000f:0.00} mm from the floor.");
            result((lowest, highest));
        }

        /// <summary>Where Prepare Character Models puts the package's own models (the ones SampleContentAnimationTests checks).</summary>
        protected const string ModelsFolder = "Assets/Models/CharacterModels";

        /// <summary>The asset path of the character prefab's own model source (in the editor; null in a player, or if it has none).</summary>
        protected static string CharactersOwnSourcePath()
        {
#if UNITY_EDITOR
            GameObject prefab = CharacterTestRig.CharacterPrefab();
            CharacterModelHost host = prefab != null ? prefab.GetComponent<CharacterModelHost>() : null;
            return host != null && host.ModelSource != null ? UnityEditor.AssetDatabase.GetAssetPath(host.ModelSource) : null;
#else
            return null;
#endif
        }

        /// <summary>Whether the character's own model is one of the package's prepared models (in ModelsFolder).</summary>
        protected static bool OwnModelIsPrepared()
        {
            string path = CharactersOwnSourcePath();
            return !string.IsNullOrEmpty(path) && path.StartsWith(ModelsFolder + "/", System.StringComparison.Ordinal);
        }

        /// <summary>
        /// Humanoid animation plays relative to each avatar's T-pose, so a model whose avatar took an A-pose as its T-pose
        /// holds its arms that much higher in every clip. Standing idle, the mannequin's upper arms hang about 12 degrees
        /// from straight down; a model rigged in an A-pose without its T-pose corrected holds them out near 70. Checked on
        /// a model standing idle (the animator taken over and stepped for a while).
        /// </summary>
        protected void AssertArmsDown()
        {
            foreach ((HumanBodyBones upper, HumanBodyBones lower) in new[]
                     { (HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm), (HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm) })
            {
                Vector3 arm = animator.GetBoneTransform(lower).position - animator.GetBoneTransform(upper).position;
                float fromDown = Vector3.Angle(arm, Vector3.down);
                Assert.That(fromDown, Is.LessThan(30f), $"Standing idle, the {upper} is held {fromDown:0} degrees out from the body.");
            }
        }
    }
}
