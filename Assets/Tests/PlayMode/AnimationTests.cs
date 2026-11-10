using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CharacterShowcase.Tests
{
    /// <summary>Tests that the character's Animator follows its movement: the dash animation plays while dashing, and
    /// every prepared character model keeps its walking feet planted, stands with its arms down and holds its torso
    /// upright, as the mannequin the clips were made for does.</summary>
    public class AnimationTests
    {
        private CharacterTestRig rig;
        private AnimationController animation;
        private Animator animator;

        [SetUp]
        public void SetUp() => rig = new CharacterTestRig();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            rig.Dispose();
            yield return null;
        }

        /// <summary>Feeds the latest movement to the animator, as ACCharacterController does each frame in the game.</summary>
        private void FeedAnimator(int _) => animation.UpdateAnimator(rig.Movement.lastFrameMovementData, Time.fixedDeltaTime);

        private bool InState(string state)
        {
            AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);
            AnimatorStateInfo next = animator.GetNextAnimatorStateInfo(0);
            return current.IsName(state) || (animator.IsInTransition(0) && next.IsName(state));
        }

        [UnityTest]
        public IEnumerator Dashing_PlaysTheDashAnimation_ThenReturnsToLocomotion()
        {
            rig.Floor();
            rig.SpawnCharacter(new Vector3(0f, 0.05f, 0f));
            animation = rig.Animation;
            yield return rig.WaitForModel();
            animator = animation.Animator;
            yield return rig.Run(0.3f, _ => CharacterTestRig.Idle(), FeedAnimator);
            Assert.That(InState("Locomotion"), Is.True, "Test setup: should start in Locomotion.");

            bool sawDash = false;
            int dashSteps = CharacterTestRig.StepsFor(rig.MovementSetting<float>("dashDuration"));
            yield return rig.Run(dashSteps - 1, step => step == 0 ? CharacterTestRig.Dash(0, Vector3.right) : CharacterTestRig.Idle(), step =>
            {
                FeedAnimator(step);
                sawDash |= InState("Dash");
            });
            yield return null; // Let the animator evaluate the last dash frame.
            sawDash |= InState("Dash");

            yield return rig.Run(0.5f, _ => CharacterTestRig.Idle(), FeedAnimator);
            yield return null;

            Assert.That(sawDash, Is.True, "The dash animation should play while dashing.");
            Assert.That(InState("Locomotion"), Is.True, "The character should return to Locomotion after a ground dash.");
        }

        /// <summary>
        /// Steps the model's animation by hand with the physics (see StepAnimator), so measurements don't depend on the
        /// frame rate, and stops interpolating the character so the model sits exactly where physics put it.
        /// </summary>
        private void TakeOverAnimator()
        {
            rig.Body.interpolation = RigidbodyInterpolation.None;
            animator = animation.Animator;
            animator.enabled = false;
        }

        private void StepAnimator(int step)
        {
            FeedAnimator(step);
            animator.Update(Time.fixedDeltaTime);
        }

        // The speed the Walk clip is made for (MannequinGenerator's blend tree), so it plays on its own, unblended.
        private const float WalkClipSpeed = 1.6f;
        // Height (m) above its lowest within which the ankle and the toe joint count as down. The heel lifts the ankle
        // several centimetres as the foot rolls onto its toes, and the toes lift as it swings.
        private const float FlatFootBand = 0.01f;
        // The fewest physics steps a flat foot must stay down for its movement to count (a walking foot is flat for
        // about a quarter of a second).
        private const int MinimumFlatSteps = 4;
        // The most the planted feet may slide, as a fraction of the character's speed.
        private const float AllowedFootSlide = 0.05f;

        /// <summary>
        /// Walks the character straight ahead and measures how fast its planted feet slide over the floor, as a fraction
        /// of its speed: how far each foot moves while it's flat on the floor (its ankle and toe joint both at their
        /// lowest), divided by how long it's flat. 0 means the feet stay put; 1, that they're dragged along with the
        /// character. A foot that slides backward (animation playing too fast) counts the same as one sliding forward.
        ///
        /// Walking, rather than running, because a walking foot stays flat for a good part of each step, while a running
        /// foot is down for only a few physics steps and is rolling onto its toes the whole time.
        /// </summary>
        private IEnumerator MeasureFootSlide(System.Action<float> result)
        {
            TakeOverAnimator();
            HumanBodyBones[,] bones = { { HumanBodyBones.LeftFoot, HumanBodyBones.LeftToes }, { HumanBodyBones.RightFoot, HumanBodyBones.RightToes } };
            Transform Bone(int foot, int part) => animator.GetBoneTransform(bones[foot, part]);
            for (int foot = 0; foot < 2; foot++)
            {
                Assert.That(Bone(foot, 0), Is.Not.Null, "Test setup: the model needs foot bones.");
                Assert.That(Bone(foot, 1), Is.Not.Null, "Test setup: the model needs toe bones.");
            }
            Inputs walk = CharacterTestRig.Move(Vector3.forward * (WalkClipSpeed / rig.MovementSetting<float>("walkSpeed")));

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
            float speed = Vector3.Distance(start, rig.Position) / (ankles[0].Count * Time.fixedDeltaTime);

            float moved = 0f;
            int flatSteps = 0;
            for (int foot = 0; foot < 2; foot++)
            {
                float lowestAnkle = ankles[foot].Min(p => p.y), lowestToe = toes[foot].Min(p => p.y);
                bool Flat(int i) => ankles[foot][i].y < lowestAnkle + FlatFootBand && toes[foot][i].y < lowestToe + FlatFootBand;
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
            Assert.That(speed, Is.EqualTo(WalkClipSpeed).Within(0.1f), "Test setup: the character should walk at the Walk clip's speed.");
            float slide = moved / (flatSteps * Time.fixedDeltaTime) / speed;
            TestContext.WriteLine($"Walking at {speed:0.000} m/s, feet flat for {flatSteps} steps, sliding at {slide:P1} of that.");
            result(slide);
        }

        /// <summary>Every model Prepare Character Models has set up, by definition path.</summary>
        private static IEnumerable<string> PreparedModels()
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.FindAssets($"t:{nameof(CharacterModelDefinition)}", new[] { "Assets/Models/CharacterModels" })
                .Select(UnityEditor.AssetDatabase.GUIDToAssetPath)
                .OrderBy(path => path, System.StringComparer.Ordinal)
                .ToArray();
#else
            return Enumerable.Empty<string>();
#endif
        }

        /// <summary>Spawns the character on a floor and gives it the model the definition describes.</summary>
        private IEnumerator SpawnWithModel(string definitionPath)
        {
#if UNITY_EDITOR
            var definition = UnityEditor.AssetDatabase.LoadAssetAtPath<CharacterModelDefinition>(definitionPath);
#else
            CharacterModelDefinition definition = null;
#endif
            Assert.That(definition, Is.Not.Null, $"Test setup: no model definition at '{definitionPath}'.");
            rig.Floor();
            rig.SpawnCharacter(new Vector3(0f, 0.05f, 0f));
            animation = rig.Animation;
            yield return rig.WaitForModel();
            yield return animation.LoadModel(definition);
            Assert.That(animation.ModelDefinition, Is.SameAs(definition));
            Assert.That(animation.Animator, Is.Not.Null, "The model should have an Animator.");
            Assert.That(animation.Animator.runtimeAnimatorController, Is.Not.Null, "The model's Animator should have a controller.");
        }

        [UnityTest]
        public IEnumerator EveryModel_KeepsItsPlantedFeetInPlace_WhenWalking([ValueSource(nameof(PreparedModels))] string definitionPath)
        {
            yield return SpawnWithModel(definitionPath);
            float slide = 0f;
            yield return MeasureFootSlide(s => slide = s);
            Assert.That(slide, Is.LessThan(AllowedFootSlide), $"The planted feet slide at {slide:P0} of the character's speed.");
        }

        /// <summary>
        /// Humanoid animation plays relative to each avatar's T-pose, so a model whose avatar took an A-pose as its T-pose
        /// holds its arms that much higher in every clip. Standing idle, the mannequin's upper arms hang about 12 degrees
        /// from straight down; a model rigged in an A-pose without its T-pose corrected holds them out near 70.
        /// </summary>
        [UnityTest]
        public IEnumerator EveryModel_StandsWithItsArmsDown([ValueSource(nameof(PreparedModels))] string definitionPath)
        {
            yield return SpawnWithModel(definitionPath);
            TakeOverAnimator();
            yield return rig.Run(1f, _ => CharacterTestRig.Idle(), StepAnimator);

            foreach ((HumanBodyBones upper, HumanBodyBones lower) in new[]
                     { (HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm), (HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm) })
            {
                Vector3 arm = animator.GetBoneTransform(lower).position - animator.GetBoneTransform(upper).position;
                float fromDown = Vector3.Angle(arm, Vector3.down);
                Assert.That(fromDown, Is.LessThan(30f), $"Standing idle, the {upper} is held {fromDown:0} degrees out from the body.");
            }
        }

        /// <summary>How far the torso (hips to head) leans forward from upright, in degrees, toward the character's facing.</summary>
        private float TorsoLean()
        {
            Vector3 torso = animator.GetBoneTransform(HumanBodyBones.Head).position - animator.GetBoneTransform(HumanBodyBones.Hips).position;
            Vector3 forward = rig.Character.transform.forward;
            return Mathf.Atan2(Vector3.Dot(torso, forward), Vector3.Dot(torso, Vector3.up)) * Mathf.Rad2Deg;
        }

        /// <summary>
        /// The clips lean the torso a little forward, more the faster the character goes: about 1 degree standing, 2
        /// walking and 6 running. Every model should lean the same way. A model whose avatar was made from a posed
        /// skeleton (or that plays clips stored relative to one) leans tens of degrees off instead.
        /// </summary>
        [UnityTest]
        public IEnumerator EveryModel_KeepsItsTorsoUpright([ValueSource(nameof(PreparedModels))] string definitionPath)
        {
            yield return SpawnWithModel(definitionPath);
            TakeOverAnimator();
            Inputs walk = CharacterTestRig.Move(Vector3.forward * (WalkClipSpeed / rig.MovementSetting<float>("walkSpeed")));
            foreach ((string activity, Inputs inputs) in new[] { ("standing", CharacterTestRig.Idle()), ("walking", walk), ("running", CharacterTestRig.Move(Vector3.forward)) })
            {
                // Settle into the activity, then average the lean over a couple of strides.
                yield return rig.Run(1f, _ => inputs, StepAnimator);
                float lean = 0f;
                int samples = 0;
                yield return rig.Run(1f, _ => inputs, step =>
                {
                    StepAnimator(step);
                    lean += TorsoLean();
                    samples++;
                });
                lean /= samples;
                TestContext.WriteLine($"Torso lean {activity}: {lean:0.0} degrees forward.");
                Assert.That(lean, Is.InRange(-3f, 12f), $"While {activity}, the torso leans {lean:0} degrees forward (negative is backward).");
            }
        }
    }
}
