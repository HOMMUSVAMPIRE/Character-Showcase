using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MeridianMotion.Characters;
using NUnit.Framework;
using UnityEngine.TestTools;
using UnityEngine;

namespace MeridianMotion.Tests.Animations
{
    /// <summary>
    /// Tests of animating the character's model, on whatever model the character has (its source's), so they hold for
    /// any team's model and animations: foot placement standing it on the ground at any size, stretch and foot placement
    /// weight; its planted feet sliding no more at other sizes than at its own; and IK goals and hints on stretched
    /// models. Tests that need a humanoid are skipped on a model that isn't one. (SampleContentAnimationTests checks
    /// the package's own models and animations.)
    /// </summary>
    public class AnimationTests : AnimationTestsBase
    {
        private static IEnumerable<TestCaseData> Groundings()
        {
            // Unity's coroutine tests take their cases as TestCaseData expecting no result.
            TestCaseData Case(Vector3 scale, float weight) => new TestCaseData(scale, weight).Returns(null);
            // Foot placement on, at any size and stretch.
            yield return Case(Vector3.one, 1f);
            yield return Case(Vector3.one * 0.1f, 1f);
            yield return Case(Vector3.one * 10f, 1f);
            yield return Case(new Vector3(1f, 0.5f, 1f), 1f);
            yield return Case(new Vector3(1f, 2f, 1f), 1f);
            yield return Case(new Vector3(2f, 1f, 0.5f), 1f);
            // Off, and part way (as it fades in on landing).
            yield return Case(Vector3.one, 0f);
            yield return Case(Vector3.one, 0.5f);
            // Part way, stretched.
            yield return Case(new Vector3(1f, 0.5f, 1f), 0.5f);
            yield return Case(new Vector3(1f, 2f, 1f), 0.5f);
            yield return Case(new Vector3(2f, 1f, 0.5f), 0.5f);
        }

        // The soles measured unstretched, by foot placement weight: the baseline the stretched part-way cases compare
        // with, measured once per run of the fixture (by the first case that needs it, or by the unstretched case at
        // that weight) rather than again for each. Numbers only, so nothing of a disposed rig is kept.
        private readonly Dictionary<float, (float lowest, float highest)> unstretchedSoles = new();

        /// <summary>Starts each run of the fixture with no baselines measured, so none is left from an earlier run.</summary>
        [OneTimeSetUp]
        public void ForgetBaselines()
        {
            unstretchedSoles.Clear();
            ownSizeSlide.Clear();
        }

        /// <summary>
        /// Standing idle on a floor, the model's soles touch it: neither floating above it nor sunk into it, at any size,
        /// including stretched along one axis only, with foot placement on, off or fading. The model is placed for its
        /// soles where foot placement holds them; where its animation has them instead they can be a centimetre higher or
        /// lower (on a model of other proportions than the clips'), which foot placement makes up for in proportion to
        /// how far off it is. Part way (only while fading, for an eighth of a second), Unity's IK blends the leg's joint
        /// rotations, which shortens the leg a little: the feet dip a few millimetres even where foot placement has
        /// nothing to change, so stretched, part way, the model is compared with itself unstretched instead. At its own
        /// size with foot placement on, a model that's one of the package's prepared ones also holds its arms down
        /// (SampleContentAnimationTests.EveryModel_StandsOnTheGround_WithItsArmsDown checks the others, and leaves this
        /// one to this case, the same run).
        /// </summary>
        [UnityTest, TestCaseSource(nameof(Groundings))]
        public IEnumerator StandsOnTheGround_AtAnySize_WithFootPlacementOnOffOrFading(Vector3 scale, float footPlacementWeight)
        {
            bool partWay = footPlacementWeight > 0f && footPlacementWeight < 1f, stretched = scale != Vector3.one;
            (float lowest, float highest) soles = default, normal = default;
            if (partWay && stretched && !unstretchedSoles.TryGetValue(footPlacementWeight, out normal))
            {
                yield return SpawnWithOwnModel();
                yield return MeasureStandingSoles(Vector3.one, footPlacementWeight, s => normal = s);
                unstretchedSoles[footPlacementWeight] = normal;
                yield return ReplaceRig();
            }
            yield return SpawnWithOwnModel();
            yield return MeasureStandingSoles(scale, footPlacementWeight, s => soles = s);
            if (!stretched) { unstretchedSoles[footPlacementWeight] = soles; }
            Assert.That(rig.Grounded, Is.True);

            if (partWay && stretched)
            {
                float lowest = soles.lowest / scale.y, highest = soles.highest / scale.y;
                Assert.That(lowest, Is.EqualTo(normal.lowest).Within(0.001f), "Its lowest point should be where it is unstretched, for its height.");
                Assert.That(highest, Is.EqualTo(normal.highest).Within(0.001f), "Its highest point should be where it is unstretched, for its height.");
                yield break;
            }
            // Within 2 mm for a model of normal size (5 below while fading), scaled with its height.
            float sink = (partWay ? 0.005f : 0.002f) * scale.y, rise = 0.002f * scale.y;
            Assert.That(soles.lowest, Is.GreaterThan(-sink), $"The soles sink {-soles.lowest * 1000f:0.0} mm into the floor.");
            Assert.That(soles.highest, Is.LessThan(rise), $"The soles float {soles.highest * 1000f:0.0} mm above the floor.");
            if (!stretched && footPlacementWeight >= 1f && OwnModelIsPrepared()) { AssertArmsDown(); }
        }

        /// <summary>
        /// A source that hasn't been measured (made by hand or in code, rather than by Prepare Humanoid Models)
        /// measures its model as it loads, and stands it on the ground just the same: one made from the character's own
        /// model, as a copy of its source that hasn't been measured.
        /// </summary>
        [UnityTest]
        public IEnumerator AnUnmeasuredSource_MeasuresItsModelOnLoad_AndStandsItOnTheGround()
        {
            yield return SpawnWithOwnModel();
            if (rig.Models.ModelSource is not PrefabModelSource own) { Assert.Ignore("The character's model source doesn't reference its model directly."); yield break; }
            var unmeasured = ScriptableObject.CreateInstance<PrefabModelSource>();
            unmeasured.ModelPrefab = own.ModelPrefab;
            unmeasured.Translator = own.Translator;
            unmeasured.AnimatorController = own.AnimatorController;
            unmeasured.Avatar = own.Avatar;
            Assert.That(unmeasured.MeasureGroundOnLoad, Is.True, "A new source should measure on load.");
            Assert.That(unmeasured.GroundOffset, Is.Zero);
            try
            {
                yield return CharacterTestRig.WaitFor(rig.Models.LoadModelAsync(unmeasured));
                TakeOverAnimator();
                var feet = animator.GetComponent<HumanoidFootPlacementIK>();
                if (feet != null && !own.MeasureGroundOnLoad)
                {
                    Assert.That(feet.FloorOffset, Is.EqualTo(own.GroundOffset).Within(1e-4f), "Measured on load, as Prepare Humanoid Models measured the character's own.");
                    Assert.That(feet.AnimatedFloorOffset, Is.EqualTo(own.AnimatedGroundOffset).Within(1e-4f));
                }
                (float lowest, float highest) soles = default;
                yield return MeasureStandingSoles(Vector3.one, feet != null ? feet.weight : 1f, s => soles = s);
                Assert.That(soles.lowest, Is.GreaterThan(-0.002f));
                Assert.That(soles.highest, Is.LessThan(0.002f));
            }
            finally
            {
                Object.Destroy(unmeasured);
            }
        }

        // The planted feet's slide at the character's own size, by walking speed: the baseline every case compares with,
        // measured once per run of the fixture rather than again for each case. Numbers only, so nothing of a disposed
        // rig is kept.
        private readonly Dictionary<float, float> ownSizeSlide = new();

        private static readonly Vector3[] SlideScales = { Vector3.one * 0.25f, Vector3.one * 0.5f, Vector3.one * 2f, new(1f, 1.5f, 1.5f) };

        /// <summary>
        /// The model's planted feet slide no more at another size, or stretched, than at its own size, walking at the
        /// same speed for its size: its animation is played at the speed that matches its strides (which scale with the
        /// character's depth). Walking slowly (a tenth of the character's walking speed, a walk for any tuning), and
        /// compared with itself at its own size, so it holds whatever its animations are. (How little the package's own
        /// animations slide is SampleContentAnimationTests'.)
        /// </summary>
        [UnityTest]
        public IEnumerator PlantedFeet_SlideNoMore_AtOtherSizesOrStretched([ValueSource(nameof(SlideScales))] Vector3 scale)
        {
            float normal = 0f, scaled = 0f;
            yield return SpawnWithOwnModel();
            float speed = 0.1f * rig.WalkSpeed;
            if (!ownSizeSlide.TryGetValue(speed, out normal))
            {
                yield return MeasureFootSlide(speed, s => normal = s);
                ownSizeSlide[speed] = normal;
                yield return ReplaceRig();
                yield return SpawnWithOwnModel();
            }
            yield return new WaitForFixedUpdate(); // So the floor reaches physics queries before the room check.
            Assert.That(rig.Controller.SetScale(scale, immediately: true), Is.True);
            yield return MeasureFootSlide(speed * scale.z, s => scaled = s);
            Assert.That(scaled, Is.LessThan(normal + 0.02f), $"At {scale}, the planted feet slide at {scaled:P1} of the character's speed, against {normal:P1} at its own size.");
        }

        /// <summary>
        /// A knee hint set on its own bends the knee and leaves the foot where it is, on a model stretched to twice its
        /// height as unstretched. Unity only applies a hint while its limb's goal is active, so HumanoidIK turns the goal
        /// on, aimed at where the animation has the foot, which Unity reports at half its height on such a model (see
        /// HumanoidIKGoals.AnimatedIKGoalPosition): uncorrected, the foot is pulled up off the floor. Compared with
        /// the same model unstretched, as on a model of other proportions than its clips' own, the goal Unity reports is
        /// a little off the foot, so the hint moves it that much at any size.
        /// </summary>
        [UnityTest]
        public IEnumerator AKneeHintOnItsOwn_MovesTheFootNoMore_OnAStretchedModel()
        {
            float normal = 0f, stretched = 0f;
            IEnumerator Measure(Vector3 scale, System.Action<float> result)
            {
                yield return SpawnWithOwnModel();
                yield return new WaitForFixedUpdate();
                Assert.That(rig.Controller.SetScale(scale, immediately: true), Is.True);
                TakeOverAnimator();
                var placement = animator.GetComponent<HumanoidFootPlacementIK>();
                if (placement != null) { placement.enabled = false; } // Only the hint.
                var ik = animator.GetComponent<HumanoidIK>();
                if (ik == null) { Assert.Ignore("The model has no HumanoidIK."); }
                yield return rig.Run(0.5f, _ => CharacterTestRig.Idle(), StepAnimator);
                Transform foot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                Transform knee = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
                Vector3 before = foot.position;
                ik.SetHint(IKLimb.LeftFoot, knee.position + rig.Character.transform.forward * 0.2f, 1f);
                ik.SnapWeights();
                yield return rig.Run(0.2f, _ => CharacterTestRig.Idle(), StepAnimator);
                result(Vector3.Distance(foot.position, before));
            }
            yield return Measure(Vector3.one, m => normal = m);
            yield return ReplaceRig();
            yield return Measure(new Vector3(1f, 2f, 1f), m => stretched = m);
            TestContext.WriteLine($"The foot moved {stretched * 1000f:0.0} mm stretched, {normal * 1000f:0.0} mm unstretched.");
            Assert.That(stretched, Is.LessThan(2f * normal + 0.01f), "Stretched, the hint should move the foot no more than unstretched (for its height).");
        }

        private static readonly Vector3[] GoalScales =
        {
            Vector3.one, new(1f, 2f, 1f), new(2f, 1f, 1f), new(1f, 1f, 2f), new(1f, 0.6f, 1f), Vector3.one * 2f, Vector3.one * 0.5f,
        };

        /// <summary>
        /// A goal is a world position, however the model is stretched: moving a hand's or foot's goal moves the hand or
        /// foot as far (see HumanoidIKGoals.IKGoalPositionToSet, without which a stretched model's feet miss by up to
        /// 10 cm, and its hands by up to 44). Measured from a goal near the animation's (not from the animated pose: a
        /// model of other proportions than the clips' own has its hands and feet a little off the goals Unity reports,
        /// which are the clips'), then moved.
        /// </summary>
        [UnityTest]
        public IEnumerator MovingAGoal_MovesTheHandOrFootAsFar_AtAnyStretch(
            [ValueSource(nameof(GoalScales))] Vector3 scale, [Values(IKLimb.LeftFoot, IKLimb.RightHand)] IKLimb limb)
        {
            yield return SpawnWithOwnModel();
            yield return new WaitForFixedUpdate();
            Assert.That(rig.Controller.SetScale(scale, immediately: true), Is.True, "Test setup: the character should resize.");
            TakeOverAnimator();
            var placement = animator.GetComponent<HumanoidFootPlacementIK>();
            if (placement != null) { placement.enabled = false; } // Only the goal.
            var ik = animator.GetComponent<HumanoidIK>();
            if (ik == null) { Assert.Ignore("The model has no HumanoidIK."); }
            yield return rig.Run(0.5f, _ => CharacterTestRig.Idle(), StepAnimator);

            bool foot = limb == IKLimb.LeftFoot;
            AvatarIKGoal goal = foot ? AvatarIKGoal.LeftFoot : AvatarIKGoal.RightHand;
            Transform bone = animator.GetBoneTransform(foot ? HumanBodyBones.LeftFoot : HumanBodyBones.RightHand);
            // Forward, up and in toward the body, by a few percent of the character's height (8, 6 and 4 cm on a 2 m
            // character): within reach at any of the sizes.
            float body = CharacterTestRig.PrefabBody().height / 2f;
            Vector3 inward = rig.Character.transform.right * (foot ? 1f : -1f); // The left foot, or the right hand.
            Vector3 goalShift = (rig.Character.transform.forward * 0.08f + Vector3.up * 0.06f + inward * 0.04f) * body;
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
                aimedAt = Vector3.Lerp(HumanoidIKGoals.AnimatedIKGoalPosition(a, goal), start.position, 0.2f);
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
    }

    /// <summary>
    /// Checks of the package's own character models and animations (every model Prepare Humanoid Models has set up in
    /// Assets/Models/Humanoid, playing the shared controller): their planted feet stay put walking at the speed
    /// the walk loop is made for, they stand on the ground, hold their arms down standing and keep their torsos upright,
    /// as the mannequin does. In the SampleContent category, and skipped where there are no prepared models: a team
    /// using its own models and animations can leave them out.
    /// </summary>
    [Category("SampleContent")]
    public class SampleContentAnimationTests : AnimationTestsBase
    {
        private const string NoModels = "(no prepared models)";
        // The most the planted feet may slide, as a fraction of the character's speed. Motion capture's planted feet
        // roll and drift a little under a body moving at a steady speed (the walk loop's slide 4.5 to 5 percent on the
        // package's models), where keyframed feet can be held exactly.
        private const float AllowedFootSlide = 0.06f;

        /// <summary>Every model Prepare Humanoid Models has set up, by source path, or a placeholder if there are none.</summary>
        private static IEnumerable<string> PreparedModels()
        {
#if UNITY_EDITOR
            string[] paths = UnityEditor.AssetDatabase.IsValidFolder(ModelsFolder)
                ? UnityEditor.AssetDatabase.FindAssets($"t:{nameof(PrefabModelSource)}", new[] { ModelsFolder })
                    .Select(UnityEditor.AssetDatabase.GUIDToAssetPath)
                    .OrderBy(path => path, System.StringComparer.Ordinal)
                    .ToArray()
                : System.Array.Empty<string>();
            return paths.Length > 0 ? paths : new[] { NoModels };
#else
            return new[] { NoModels };
#endif
        }

        /// <summary>
        /// Every prepared model but the character's own, or a placeholder if there are none: standing the character's own
        /// on the ground at its own size with foot placement on is the same run as the (1, 1, 1) case at full weight of
        /// AnimationTests.StandsOnTheGround_AtAnySize_WithFootPlacementOnOffOrFading, which checks its arms down too.
        /// </summary>
        private static IEnumerable<string> PreparedModelsButTheCharactersOwn()
        {
            string own = CharactersOwnSourcePath();
            string[] others = PreparedModels().Where(path => path != own).ToArray();
            return others.Length > 0 ? others : new[] { NoModels };
        }

        private IEnumerator SpawnWithPreparedModel(string path)
        {
            if (path == NoModels) { Assert.Ignore($"There are no prepared models in {ModelsFolder}."); }
#if UNITY_EDITOR
            var source = UnityEditor.AssetDatabase.LoadAssetAtPath<PrefabModelSource>(path);
#else
            PrefabModelSource source = null;
#endif
            Assert.That(source, Is.Not.Null, $"Test setup: no model source at '{path}'.");
            yield return SpawnWithModel(source);
            animator = rig.Models.Animator;
        }

        /// <summary>
        /// The speed the slowest walk loop is made for, so it plays on its own, unblended: the threshold of the first loop
        /// in the model's controller's Locomotion blend tree played at its own pace (the one after standing).
        /// </summary>
        private float WalkLoopSpeed()
        {
#if UNITY_EDITOR
            var controller = animator.runtimeAnimatorController as UnityEditor.Animations.AnimatorController;
            var tree = controller != null
                ? controller.layers[0].stateMachine.states.FirstOrDefault(s => s.state.name == "Locomotion").state?.motion as UnityEditor.Animations.BlendTree
                : null;
            UnityEditor.Animations.ChildMotion walk = tree != null
                ? tree.children.FirstOrDefault(c => c.motion != null && c.threshold > 0f && Mathf.Approximately(c.timeScale, 1f))
                : default;
            if (walk.motion == null) { Assert.Ignore("The model's controller isn't the package's (no walk loop in a Locomotion blend tree)."); }
            return walk.threshold;
#else
            Assert.Ignore("Reads the controller in the editor.");
            return 0f;
#endif
        }

        [UnityTest]
        public IEnumerator EveryModel_KeepsItsPlantedFeetInPlace_WhenWalking([ValueSource(nameof(PreparedModels))] string path)
        {
            yield return SpawnWithPreparedModel(path);
            float slide = 0f;
            yield return MeasureFootSlide(WalkLoopSpeed(), s => slide = s);
            Assert.That(slide, Is.LessThan(AllowedFootSlide), $"The planted feet slide at {slide:P0} of the character's speed.");
        }

        /// <summary>
        /// Standing idle, every model's soles touch the floor and it holds its arms down (see AssertArmsDown). The
        /// character's own model is left to AnimationTests (see PreparedModelsButTheCharactersOwn).
        /// </summary>
        [UnityTest]
        public IEnumerator EveryModel_StandsOnTheGround_WithItsArmsDown([ValueSource(nameof(PreparedModelsButTheCharactersOwn))] string path)
        {
            yield return SpawnWithPreparedModel(path);
            (float lowest, float highest) soles = default;
            yield return MeasureStandingSoles(Vector3.one, 1f, s => soles = s);
            Assert.That(soles.lowest, Is.GreaterThan(-0.002f), $"The soles sink {-soles.lowest * 1000f:0.0} mm into the floor.");
            Assert.That(soles.highest, Is.LessThan(0.002f), $"The soles float {soles.highest * 1000f:0.0} mm above the floor.");
            AssertArmsDown();
        }

        /// <summary>How far the torso (hips to head) leans forward from upright, in degrees, toward the character's facing.</summary>
        private float TorsoLean()
        {
            Vector3 torso = animator.GetBoneTransform(HumanBodyBones.Head).position - animator.GetBoneTransform(HumanBodyBones.Hips).position;
            Vector3 forward = rig.Character.transform.forward;
            return Mathf.Atan2(Vector3.Dot(torso, forward), Vector3.Dot(torso, Vector3.up)) * Mathf.Rad2Deg;
        }

        /// <summary>
        /// The clips lean the torso a little forward: about 1 degree standing and 12 walking (the Quaternius walk carries
        /// the head forward). At full stick it depends on the tuning, since the walk speed picks the gait: about 2 in the
        /// brisk walk at 3 m/s, about 22 in the jog at 5 m/s. Every model should lean about the same way. A model whose
        /// avatar was made from a posed skeleton (or that plays clips stored relative to one) leans tens of degrees off
        /// instead (about 45), so the range is wide enough for the clips' own carriage at any gait and narrow enough to
        /// catch that.
        /// </summary>
        [UnityTest]
        public IEnumerator EveryModel_KeepsItsTorsoUpright([ValueSource(nameof(PreparedModels))] string path)
        {
            yield return SpawnWithPreparedModel(path);
            TakeOverAnimator();
            StepInputs walk = CharacterTestRig.Move(Vector3.forward * (WalkLoopSpeed() / rig.WalkSpeed));
            foreach ((string activity, StepInputs inputs) in new[] { ("standing", CharacterTestRig.Idle()), ("walking", walk), ("running", CharacterTestRig.Move(Vector3.forward)) })
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
                Assert.That(lean, Is.InRange(-5f, 30f), $"While {activity}, the torso leans {lean:0} degrees forward (negative is backward).");
            }
        }
    }
}
