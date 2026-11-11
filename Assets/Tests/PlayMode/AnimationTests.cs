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
            animator = rig.Models.Animator;
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
            animator = rig.Models.Animator;
            animator.enabled = false;
        }

        private void StepAnimator(int step)
        {
            FeedAnimator(step);
            animator.Update(Time.fixedDeltaTime);
        }

        // The speed the walk loop is made for (measured by MotionCaptureLocomotion), so it plays on its own, unblended:
        // its threshold in the shared controller's Locomotion blend tree.
        private static float WalkClipSpeed
        {
            get
            {
#if UNITY_EDITOR
                var controller = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>("Assets/Animation/Mannequin.controller");
                Assert.That(controller, Is.Not.Null, "Test setup: the shared Animator Controller should exist.");
                UnityEditor.Animations.ChildAnimatorState locomotion = controller.layers[0].stateMachine.states.First(s => s.state.name == "Locomotion");
                var tree = (UnityEditor.Animations.BlendTree)locomotion.state.motion;
                return tree.children.First(c => c.motion != null && c.motion.name == "Walk Cycle").threshold;
#else
                Assert.Ignore("Reads the controller in the editor.");
                return 0f;
#endif
            }
        }
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
            // Walk at the speed the Walk clip is made for, for the character's size: a model scaled to twice the depth
            // takes strides twice as long, so the Walk clip plays unblended at twice the speed.
            float walkSpeed = WalkClipSpeed * rig.Controller.Scale.z;
            Inputs walk = CharacterTestRig.Move(Vector3.forward * (walkSpeed / (rig.MovementSetting<float>("walkSpeed") * rig.Movement.SpeedScale)));

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
            Assert.That(speed, Is.EqualTo(walkSpeed).Within(0.1f * rig.Controller.Scale.z), "Test setup: the character should walk at the Walk clip's speed for its size.");
            float slide = moved / (flatSteps * Time.fixedDeltaTime) / speed;
            TestContext.WriteLine($"Walking at {speed:0.000} m/s, feet flat for {flatSteps} steps, sliding at {slide:P1} of that.");
            result(slide);
        }

        [UnityTest]
        public IEnumerator Walking_KeepsThePlantedFeetInPlace_AtOtherSizes([Values(0.25f, 0.5f, 2f)] float size)
        {
            rig.Floor();
            rig.SpawnCharacter(new Vector3(0f, 0.05f, 0f));
            animation = rig.Animation;
            yield return rig.WaitForModel();
            yield return new WaitForFixedUpdate(); // So the floor reaches physics queries before the room check.
            rig.Controller.SetScale(Vector3.one * size, immediately: true);

            float slide = 0f;
            yield return MeasureFootSlide(s => slide = s);
            Assert.That(slide, Is.LessThan(AllowedFootSlide), $"At {size}x, the planted feet slide at {slide:P0} of the character's speed.");
        }

        [UnityTest]
        public IEnumerator Walking_KeepsThePlantedFeetInPlace_WhenStretched()
        {
            rig.Floor();
            rig.SpawnCharacter(new Vector3(0f, 0.05f, 0f));
            animation = rig.Animation;
            yield return rig.WaitForModel();
            yield return new WaitForFixedUpdate(); // So the floor reaches physics queries before the room check.
            rig.Controller.SetScale(new Vector3(1f, 1.5f, 1.5f), immediately: true);

            float slide = 0f;
            yield return MeasureFootSlide(s => slide = s);
            Assert.That(slide, Is.LessThan(AllowedFootSlide), $"Stretched, the planted feet slide at {slide:P0} of the character's speed.");
        }

        /// <summary>Every model Prepare Character Models has set up, by definition path.</summary>
        private static IEnumerable<string> PreparedModels()
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.FindAssets($"t:{nameof(DirectCharacterModelSource)}", new[] { "Assets/Models/CharacterModels" })
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
            var definition = UnityEditor.AssetDatabase.LoadAssetAtPath<DirectCharacterModelSource>(definitionPath);
#else
            DirectCharacterModelSource definition = null;
#endif
            Assert.That(definition, Is.Not.Null, $"Test setup: no model definition at '{definitionPath}'.");
            rig.Floor();
            rig.SpawnCharacter(new Vector3(0f, 0.05f, 0f));
            animation = rig.Animation;
            yield return rig.WaitForModel();
            bool loaded = false;
            yield return CharacterTestRig.WaitFor(rig.Models.LoadModelAsync(definition), l => loaded = l);
            Assert.That(loaded, Is.True, "The model should load.");
            Assert.That(rig.Models.ModelSource, Is.SameAs(definition));
            Assert.That(rig.Models.Animator, Is.Not.Null, "The model should have an Animator.");
            Assert.That(rig.Models.Animator.runtimeAnimatorController, Is.Not.Null, "The model's Animator should have a controller.");
        }

        [UnityTest]
        public IEnumerator EveryModel_KeepsItsPlantedFeetInPlace_WhenWalking([ValueSource(nameof(PreparedModels))] string definitionPath)
        {
            yield return SpawnWithModel(definitionPath);
            float slide = 0f;
            yield return MeasureFootSlide(s => slide = s);
            Assert.That(slide, Is.LessThan(AllowedFootSlide), $"The planted feet slide at {slide:P0} of the character's speed.");
        }

        private static readonly Vector3[] GroundingScales =
        {
            Vector3.one, Vector3.one * 0.1f, Vector3.one * 10f,
            new(1f, 0.5f, 1f), new(1f, 2f, 1f), new(2f, 1f, 0.5f),
        };

        /// <summary>The lowest point of the model's meshes as they're posed now, in world space.</summary>
        private float LowestPointOfModel()
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
        /// Standing idle on a floor, every model's soles touch it: neither floating above it nor sunk into it, at any
        /// size, including stretched along one axis only. (Models whose files have rotated nodes above the skeleton,
        /// such as Blender's armature, used to be distorted and lifted off the ground when stretched, until Prepare
        /// Character Models straightened them; and each model's Ground Offset puts its soles, not its origin, on the
        /// ground.)
        /// </summary>
        [UnityTest]
        public IEnumerator EveryModel_StandsOnTheGround_AtAnySize([ValueSource(nameof(PreparedModels))] string definitionPath,
            [ValueSource(nameof(GroundingScales))] Vector3 scale)
        {
            (float lowest, float highest) soles = default;
            yield return MeasureStandingSoles(definitionPath, scale, 1f, s => soles = s);
            Assert.That(rig.Grounded, Is.True);
            // Within 2 mm for a model of normal size (the idle clip moves the feet a little), scaled with its height.
            float tolerance = 0.002f * scale.y;
            Assert.That(soles.lowest, Is.GreaterThan(-tolerance), $"The soles sink {-soles.lowest * 1000f:0.0} mm into the floor.");
            Assert.That(soles.highest, Is.LessThan(tolerance), $"The soles float {soles.highest * 1000f:0.0} mm above the floor.");
        }

        /// <summary>
        /// With foot placement off or fading (as in the air, dashing, or just after landing), the soles still rest on
        /// the floor. The model is placed for its soles where foot placement holds them; where its animation has them
        /// instead they can be a centimetre higher or lower (on a model of other proportions than the clips'), which
        /// foot placement makes up for in proportion to how far off it is. Off, they're as close as fully on. Part way
        /// (only while fading, for an eighth of a second), Unity's IK blends the leg's joint rotations, which shortens
        /// the leg a little: the feet dip a few millimetres even where foot placement has nothing to change.
        /// </summary>
        [UnityTest]
        public IEnumerator EveryModel_StandsOnTheGround_WithFootPlacementOffOrFading([ValueSource(nameof(PreparedModels))] string definitionPath,
            [Values(0f, 0.5f)] float footPlacementWeight)
        {
            (float lowest, float highest) soles = default;
            yield return MeasureStandingSoles(definitionPath, Vector3.one, footPlacementWeight, s => soles = s);
            float sink = footPlacementWeight > 0f ? 0.005f : 0.002f;
            Assert.That(soles.lowest, Is.GreaterThan(-sink), $"The soles sink {-soles.lowest * 1000f:0.0} mm into the floor.");
            Assert.That(soles.highest, Is.LessThan(0.002f), $"The soles float {soles.highest * 1000f:0.0} mm above the floor.");
        }

        /// <summary>
        /// A source that hasn't been measured (made by hand or in code, rather than by Prepare Character Models)
        /// measures its model as it loads, and stands it on the ground just the same.
        /// </summary>
        [UnityTest]
        public IEnumerator AnUnmeasuredSource_MeasuresItsModelOnLoad_AndStandsItOnTheGround([ValueSource(nameof(PreparedModels))] string definitionPath)
        {
#if UNITY_EDITOR
            var prepared = UnityEditor.AssetDatabase.LoadAssetAtPath<DirectCharacterModelSource>(definitionPath);
#else
            DirectCharacterModelSource prepared = null;
#endif
            var unmeasured = ScriptableObject.CreateInstance<DirectCharacterModelSource>();
            unmeasured.ModelPrefab = prepared.ModelPrefab;
            unmeasured.Translator = prepared.Translator;
            Assert.That(unmeasured.MeasureGroundOnLoad, Is.True, "A new source should measure on load.");
            Assert.That(unmeasured.GroundOffset, Is.Zero);
            try
            {
                rig.Floor();
                rig.SpawnCharacter(new Vector3(0f, 0.05f, 0f));
                animation = rig.Animation;
                yield return rig.WaitForModel();
                yield return CharacterTestRig.WaitFor(rig.Models.LoadModelAsync(unmeasured));
                TakeOverAnimator();
                var feet = animator.GetComponent<FootPlacementIK>();
                Assert.That(feet.FloorOffset, Is.EqualTo(prepared.GroundOffset).Within(1e-4f), "Measured on load, as Prepare Character Models measures it.");
                Assert.That(feet.AnimatedFloorOffset, Is.EqualTo(prepared.AnimatedGroundOffset).Within(1e-4f));
                float lowest = float.PositiveInfinity, highest = float.NegativeInfinity;
                yield return rig.Run(1f, _ => CharacterTestRig.Idle(), step =>
                {
                    StepAnimator(step);
                    if (step < CharacterTestRig.StepsFor(0.5f)) { return; }
                    float sole = LowestPointOfModel();
                    lowest = Mathf.Min(lowest, sole);
                    highest = Mathf.Max(highest, sole);
                });
                Assert.That(lowest, Is.GreaterThan(-0.002f));
                Assert.That(highest, Is.LessThan(0.002f));
            }
            finally
            {
                Object.Destroy(unmeasured);
            }
        }

        private static readonly Vector3[] StretchedScales = { new(1f, 0.5f, 1f), new(1f, 2f, 1f), new(2f, 1f, 0.5f) };

        /// <summary>
        /// With foot placement only partly on (as while it fades in on landing), a stretched model stands as the same
        /// model does unstretched, for its height. Foot placement plants each foot relative to where the animation has
        /// it, which Unity reports wrongly for stretched models (see CharacterUtilities.AnimatedIKGoalPosition).
        /// (Compared with the unstretched model rather than the floor: half way between foot placement's pose and the
        /// animation's own, a retargeted model's soles can be several millimetres off the floor at any size, as the
        /// two poses differ a little.)
        /// </summary>
        [UnityTest]
        public IEnumerator StretchedModels_StandAsTheyDoUnstretched_WithFootPlacementPartlyOn([ValueSource(nameof(PreparedModels))] string definitionPath,
            [ValueSource(nameof(StretchedScales))] Vector3 scale)
        {
            (float lowest, float highest) normal = default, stretched = default;
            yield return MeasureStandingSoles(definitionPath, Vector3.one, 0.5f, s => normal = s);
            rig.Dispose();
            yield return null;
            rig = new CharacterTestRig();
            yield return MeasureStandingSoles(definitionPath, scale, 0.5f, s => stretched = s);
            Assert.That(rig.Grounded, Is.True);
            float lowest = stretched.lowest / scale.y, highest = stretched.highest / scale.y;
            TestContext.WriteLine($"For its height: soles between {lowest * 1000f:0.00} and {highest * 1000f:0.00} mm, against " +
                                  $"{normal.lowest * 1000f:0.00} and {normal.highest * 1000f:0.00} mm unstretched.");
            Assert.That(lowest, Is.EqualTo(normal.lowest).Within(0.001f), "Its lowest point should be where it is unstretched, for its height.");
            Assert.That(highest, Is.EqualTo(normal.highest).Within(0.001f), "Its highest point should be where it is unstretched, for its height.");
        }

        /// <summary>
        /// A knee hint set on its own bends the knee and leaves the foot where the animation has it, on a model stretched
        /// to twice its height too. Unity only applies a hint while its limb's goal is active, so CharacterIK turns the
        /// goal on, aimed at where the animation has the foot, which Unity reports at half its height on such a model
        /// (see CharacterUtilities.AnimatedIKGoalPosition): uncorrected, the foot is pulled up off the floor.
        /// </summary>
        [UnityTest]
        public IEnumerator AKneeHintOnItsOwn_LeavesTheFootWhereTheAnimationHasIt_OnAStretchedModel()
        {
            rig.Floor();
            rig.SpawnCharacter(Vector3.zero);
            animation = rig.Animation;
            yield return rig.WaitForModel();
            yield return new WaitForFixedUpdate();
            rig.Controller.SetScale(new Vector3(1f, 2f, 1f), immediately: true);
            TakeOverAnimator();
            animator.GetComponent<FootPlacementIK>().enabled = false; // Only the hint.
            yield return rig.Run(0.5f, _ => CharacterTestRig.Idle(), StepAnimator);
            Transform foot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            Transform knee = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
            Vector3 before = foot.position;

            var ik = animator.GetComponent<CharacterIK>();
            ik.SetHint(IKLimb.LeftFoot, knee.position + rig.Character.transform.forward * 0.2f, 1f);
            ik.SnapWeights();
            yield return rig.Run(0.2f, _ => CharacterTestRig.Idle(), StepAnimator);
            float moved = Vector3.Distance(foot.position, before);
            TestContext.WriteLine($"The foot moved {moved * 1000f:0.0} mm.");
            Assert.That(moved, Is.LessThan(0.01f), "The foot should stay where the animation has it.");
        }

        private static readonly Vector3[] GoalScales =
        {
            Vector3.one, new(1f, 2f, 1f), new(2f, 1f, 1f), new(1f, 1f, 2f), new(1f, 0.6f, 1f), Vector3.one * 2f, Vector3.one * 0.5f,
        };

        /// <summary>
        /// A goal is a world position, however the model is stretched: moving a hand's or foot's goal moves the hand or
        /// foot as far (see CharacterUtilities.IKGoalPositionToSet, without which a stretched model's feet miss by up to
        /// 10 cm, and its hands by up to 44). Measured from a goal near the animation's (not from the animated pose: a
        /// model of other proportions than the clips' own has its hands and feet a little off the goals Unity reports,
        /// which are the clips'), then moved.
        /// </summary>
        [UnityTest]
        public IEnumerator MovingAGoal_MovesTheHandOrFootAsFar_AtAnyStretch(
            [ValueSource(nameof(PreparedModels))] string definitionPath, [ValueSource(nameof(GoalScales))] Vector3 scale,
            [Values(IKLimb.LeftFoot, IKLimb.RightHand)] IKLimb limb)
        {
            yield return SpawnWithModel(definitionPath);
            yield return new WaitForFixedUpdate();
            Assert.That(rig.Controller.SetScale(scale, immediately: true), Is.True, "Test setup: the character should resize.");
            TakeOverAnimator();
            animator.GetComponent<FootPlacementIK>().enabled = false; // Only the goal.
            yield return rig.Run(0.5f, _ => CharacterTestRig.Idle(), StepAnimator);

            bool foot = limb == IKLimb.LeftFoot;
            AvatarIKGoal goal = foot ? AvatarIKGoal.LeftFoot : AvatarIKGoal.RightHand;
            Transform bone = animator.GetBoneTransform(foot ? HumanBodyBones.LeftFoot : HumanBodyBones.RightHand);
            var ik = animator.GetComponent<CharacterIK>();
            // Forward, up and in toward the body, within reach at any of the sizes (the smallest's leg is under half a
            // metre).
            Vector3 inward = rig.Character.transform.right * (foot ? 1f : -1f); // The left foot, or the right hand.
            Vector3 goalShift = rig.Character.transform.forward * 0.08f + Vector3.up * 0.06f + inward * 0.04f;
            Vector3 shift = Vector3.zero;
            // From a fifth of the way from the animation's goal to the limb's start, so the limb is bent, within reach
            // either way (hands hang straight). Turned as the animation has it, so the bone measured keeps its offset
            // from the point Unity places.
            Transform start = animator.GetBoneTransform(foot ? HumanBodyBones.LeftUpperLeg : HumanBodyBones.RightUpperArm);
            // Where the goal is each frame without the shift, so the idle's own movement (motion capture is never quite
            // still) can be taken out of how far the bone moves.
            Vector3 aimedAt = Vector3.zero;
            void Aim(Animator a)
            {
                aimedAt = Vector3.Lerp(CharacterUtilities.AnimatedIKGoalPosition(a, goal), start.position, 0.2f);
                ik.SetGoal(limb, aimedAt + shift, a.GetIKRotation(goal), 1f, 1f);
            }
            ik.SetGoal(limb, bone.position, bone.rotation, 1f, 1f);
            ik.SnapWeights();
            ik.BeforeSolve += Aim;
            Vector3 before, after;
            try
            {
                yield return rig.Run(0.2f, _ => CharacterTestRig.Idle(), StepAnimator);
                before = bone.position - aimedAt;
                shift = goalShift;
                yield return rig.Run(0.2f, _ => CharacterTestRig.Idle(), StepAnimator);
                after = bone.position - aimedAt;
            }
            finally { ik.BeforeSolve -= Aim; }

            Vector3 moved = after - before;
            float error = Vector3.Distance(moved, shift);
            TestContext.WriteLine($"At {scale}, the {limb} moved {moved * 1000f} mm for a goal moved {shift * 1000f} mm: " +
                                  $"{error * 1000f:0.0} mm off.");
            // Within a few millimetres, or (a hand on a stretched model, whose start is only roughly where Unity takes
            // it to be) a centimetre.
            bool stretched = !Mathf.Approximately(scale.x, scale.y) || !Mathf.Approximately(scale.y, scale.z);
            float allowed = foot ? 0.003f : stretched ? 0.01f : 0.005f;
            Assert.That(error, Is.LessThan(allowed), $"The {limb} should move as far as its goal did.");
        }

        /// <summary>
        /// Stands the model idle on a floor at the given scale, with foot placement at the given weight, and gives back
        /// how far its soles were from the floor once settled, at their lowest and highest.
        /// </summary>
        private IEnumerator MeasureStandingSoles(string definitionPath, Vector3 scale, float footPlacementWeight,
            System.Action<(float lowest, float highest)> result)
        {
            yield return SpawnWithModel(definitionPath);
            yield return new WaitForFixedUpdate(); // So the floor reaches physics queries before the room check.
            Assert.That(rig.Controller.SetScale(scale, immediately: true), Is.True);
            TakeOverAnimator();
            animator.GetComponent<FootPlacementIK>().weight = footPlacementWeight;
            float lowest = float.PositiveInfinity, highest = float.NegativeInfinity;
            yield return rig.Run(1f, _ => CharacterTestRig.Idle(), step =>
            {
                StepAnimator(step);
                if (step < CharacterTestRig.StepsFor(0.5f)) { return; } // Settled first.
                float sole = LowestPointOfModel();
                lowest = Mathf.Min(lowest, sole);
                highest = Mathf.Max(highest, sole);
            });
            TestContext.WriteLine($"At {scale}: soles between {lowest * 1000f:0.00} and {highest * 1000f:0.00} mm from the floor.");
            result((lowest, highest));
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
