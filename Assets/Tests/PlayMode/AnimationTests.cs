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
    public abstract class AnimationTestsBase
    {
        protected CharacterTestRig rig;
        protected AnimationController animation;
        protected Animator animator;

        [SetUp]
        public void SetUp() => rig = new CharacterTestRig();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            rig.Dispose();
            yield return null;
        }

        /// <summary>Starts again with a new rig, for a test comparing two runs.</summary>
        protected IEnumerator Restart()
        {
            rig.Dispose();
            yield return null;
            rig = new CharacterTestRig();
        }

        /// <summary>Feeds the latest movement to the animator, as ACCharacterController does each frame in the game.</summary>
        protected void FeedAnimator(int _) => animation.UpdateAnimator(rig.Movement.lastFrameMovementData, Time.fixedDeltaTime);

        /// <summary>
        /// Steps the model's animation by hand with the physics (see StepAnimator), so measurements don't depend on the
        /// frame rate, and stops interpolating the character so the model sits exactly where physics put it.
        /// </summary>
        protected void TakeOverAnimator()
        {
            rig.Body.interpolation = RigidbodyInterpolation.None;
            animator = rig.Models.Animator;
            animator.enabled = false;
        }

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
            yield return rig.Run(1f, _ => CharacterTestRig.Idle(), step =>
            {
                StepAnimator(step);
                if (step < CharacterTestRig.StepsFor(0.5f)) { return; } // Settled first.
                float sole = LowestPointOfModel();
                lowest = Mathf.Min(lowest, sole);
                highest = Mathf.Max(highest, sole);
            });
            TestContext.WriteLine($"At {scale}, foot placement at {footPlacementWeight}: soles between {lowest * 1000f:0.00} and {highest * 1000f:0.00} mm from the floor.");
            result((lowest, highest));
        }
    }

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

        /// <summary>
        /// Standing idle on a floor, the model's soles touch it: neither floating above it nor sunk into it, at any size,
        /// including stretched along one axis only, with foot placement on, off or fading. The model is placed for its
        /// soles where foot placement holds them; where its animation has them instead they can be a centimetre higher or
        /// lower (on a model of other proportions than the clips'), which foot placement makes up for in proportion to
        /// how far off it is. Part way (only while fading, for an eighth of a second), Unity's IK blends the leg's joint
        /// rotations, which shortens the leg a little: the feet dip a few millimetres even where foot placement has
        /// nothing to change, so stretched, part way, the model is compared with itself unstretched instead.
        /// </summary>
        [UnityTest, TestCaseSource(nameof(Groundings))]
        public IEnumerator StandsOnTheGround_AtAnySize_WithFootPlacementOnOffOrFading(Vector3 scale, float footPlacementWeight)
        {
            bool partWay = footPlacementWeight > 0f && footPlacementWeight < 1f, stretched = scale != Vector3.one;
            (float lowest, float highest) soles = default, normal = default;
            if (partWay && stretched)
            {
                yield return SpawnWithOwnModel();
                yield return MeasureStandingSoles(Vector3.one, footPlacementWeight, s => normal = s);
                yield return Restart();
            }
            yield return SpawnWithOwnModel();
            yield return MeasureStandingSoles(scale, footPlacementWeight, s => soles = s);
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
        }

        /// <summary>
        /// A source that hasn't been measured (made by hand or in code, rather than by Prepare Character Models)
        /// measures its model as it loads, and stands it on the ground just the same: one made from the character's own
        /// model, as a copy of its source that hasn't been measured.
        /// </summary>
        [UnityTest]
        public IEnumerator AnUnmeasuredSource_MeasuresItsModelOnLoad_AndStandsItOnTheGround()
        {
            yield return SpawnWithOwnModel();
            if (rig.Models.ModelSource is not DirectCharacterModelSource own) { Assert.Ignore("The character's model source doesn't reference its model directly."); yield break; }
            var unmeasured = ScriptableObject.CreateInstance<DirectCharacterModelSource>();
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
                var feet = animator.GetComponent<FootPlacementIK>();
                if (feet != null && !own.MeasureGroundOnLoad)
                {
                    Assert.That(feet.FloorOffset, Is.EqualTo(own.GroundOffset).Within(1e-4f), "Measured on load, as Prepare Character Models measured the character's own.");
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
            yield return MeasureFootSlide(speed, s => normal = s);
            yield return Restart();
            yield return SpawnWithOwnModel();
            yield return new WaitForFixedUpdate(); // So the floor reaches physics queries before the room check.
            Assert.That(rig.Controller.SetScale(scale, immediately: true), Is.True);
            yield return MeasureFootSlide(speed * scale.z, s => scaled = s);
            Assert.That(scaled, Is.LessThan(normal + 0.02f), $"At {scale}, the planted feet slide at {scaled:P1} of the character's speed, against {normal:P1} at its own size.");
        }

        /// <summary>
        /// A knee hint set on its own bends the knee and leaves the foot where it is, on a model stretched to twice its
        /// height as unstretched. Unity only applies a hint while its limb's goal is active, so CharacterIK turns the goal
        /// on, aimed at where the animation has the foot, which Unity reports at half its height on such a model (see
        /// CharacterUtilities.AnimatedIKGoalPosition): uncorrected, the foot is pulled up off the floor. Compared with
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
                var placement = animator.GetComponent<FootPlacementIK>();
                if (placement != null) { placement.enabled = false; } // Only the hint.
                var ik = animator.GetComponent<CharacterIK>();
                if (ik == null) { Assert.Ignore("The model has no CharacterIK."); }
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
            yield return Restart();
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
        /// foot as far (see CharacterUtilities.IKGoalPositionToSet, without which a stretched model's feet miss by up to
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
            var placement = animator.GetComponent<FootPlacementIK>();
            if (placement != null) { placement.enabled = false; } // Only the goal.
            var ik = animator.GetComponent<CharacterIK>();
            if (ik == null) { Assert.Ignore("The model has no CharacterIK."); }
            yield return rig.Run(0.5f, _ => CharacterTestRig.Idle(), StepAnimator);

            bool foot = limb == IKLimb.LeftFoot;
            AvatarIKGoal goal = foot ? AvatarIKGoal.LeftFoot : AvatarIKGoal.RightHand;
            Transform bone = animator.GetBoneTransform(foot ? HumanBodyBones.LeftFoot : HumanBodyBones.RightHand);
            // Forward, up and in toward the body, by a few percent of the character's height (8, 6 and 4 cm on a 2 m
            // character): within reach at any of the sizes.
            float body = CharacterTestRig.PrefabCapsule().height / 2f;
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
    }

    /// <summary>
    /// Checks of the package's own character models and animations (every model Prepare Character Models has set up in
    /// Assets/Models/CharacterModels, playing the shared controller): their planted feet stay put walking at the speed
    /// the walk loop is made for, they stand on the ground, hold their arms down standing and keep their torsos upright,
    /// as the mannequin does. In the SampleContent category, and skipped where there are no prepared models: a team
    /// using its own models and animations can leave them out.
    /// </summary>
    [Category("SampleContent")]
    public class SampleContentAnimationTests : AnimationTestsBase
    {
        private const string ModelsFolder = "Assets/Models/CharacterModels";
        private const string NoModels = "(no prepared models)";
        // The most the planted feet may slide, as a fraction of the character's speed. Motion capture's planted feet
        // roll and drift a little under a body moving at a steady speed (the walk loop's slide 4.5 to 5 percent on the
        // package's models), where keyframed feet can be held exactly.
        private const float AllowedFootSlide = 0.06f;

        /// <summary>Every model Prepare Character Models has set up, by source path, or a placeholder if there are none.</summary>
        private static IEnumerable<string> PreparedModels()
        {
#if UNITY_EDITOR
            string[] paths = UnityEditor.AssetDatabase.IsValidFolder(ModelsFolder)
                ? UnityEditor.AssetDatabase.FindAssets($"t:{nameof(DirectCharacterModelSource)}", new[] { ModelsFolder })
                    .Select(UnityEditor.AssetDatabase.GUIDToAssetPath)
                    .OrderBy(path => path, System.StringComparer.Ordinal)
                    .ToArray()
                : System.Array.Empty<string>();
            return paths.Length > 0 ? paths : new[] { NoModels };
#else
            return new[] { NoModels };
#endif
        }

        private IEnumerator SpawnWithPreparedModel(string path)
        {
            if (path == NoModels) { Assert.Ignore($"There are no prepared models in {ModelsFolder}."); }
#if UNITY_EDITOR
            var source = UnityEditor.AssetDatabase.LoadAssetAtPath<DirectCharacterModelSource>(path);
#else
            DirectCharacterModelSource source = null;
#endif
            Assert.That(source, Is.Not.Null, $"Test setup: no model source at '{path}'.");
            yield return SpawnWithModel(source);
            animator = rig.Models.Animator;
        }

        /// <summary>
        /// The speed the walk loop is made for (measured by MotionCaptureLocomotion), so it plays on its own, unblended:
        /// its threshold in the model's controller's Locomotion blend tree.
        /// </summary>
        private float WalkLoopSpeed()
        {
#if UNITY_EDITOR
            var controller = animator.runtimeAnimatorController as UnityEditor.Animations.AnimatorController;
            var tree = controller != null
                ? controller.layers[0].stateMachine.states.FirstOrDefault(s => s.state.name == "Locomotion").state?.motion as UnityEditor.Animations.BlendTree
                : null;
            UnityEditor.Animations.ChildMotion walk = tree != null ? tree.children.FirstOrDefault(c => c.motion != null && c.motion.name == "Walk Cycle") : default;
            if (walk.motion == null) { Assert.Ignore("The model's controller isn't the package's (no Walk Cycle in a Locomotion blend tree)."); }
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

        [UnityTest]
        public IEnumerator EveryModel_StandsOnTheGround([ValueSource(nameof(PreparedModels))] string path)
        {
            yield return SpawnWithPreparedModel(path);
            (float lowest, float highest) soles = default;
            yield return MeasureStandingSoles(Vector3.one, 1f, s => soles = s);
            Assert.That(soles.lowest, Is.GreaterThan(-0.002f), $"The soles sink {-soles.lowest * 1000f:0.0} mm into the floor.");
            Assert.That(soles.highest, Is.LessThan(0.002f), $"The soles float {soles.highest * 1000f:0.0} mm above the floor.");
        }

        /// <summary>
        /// Humanoid animation plays relative to each avatar's T-pose, so a model whose avatar took an A-pose as its T-pose
        /// holds its arms that much higher in every clip. Standing idle, the mannequin's upper arms hang about 12 degrees
        /// from straight down; a model rigged in an A-pose without its T-pose corrected holds them out near 70.
        /// </summary>
        [UnityTest]
        public IEnumerator EveryModel_StandsWithItsArmsDown([ValueSource(nameof(PreparedModels))] string path)
        {
            yield return SpawnWithPreparedModel(path);
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
        /// The clips lean the torso a little forward, more the faster the character goes: about 1 degree standing, 5
        /// walking and 11 running. Every model should lean the same way. A model whose avatar was made from a posed
        /// skeleton (or that plays clips stored relative to one) leans tens of degrees off instead.
        /// </summary>
        [UnityTest]
        public IEnumerator EveryModel_KeepsItsTorsoUpright([ValueSource(nameof(PreparedModels))] string path)
        {
            yield return SpawnWithPreparedModel(path);
            TakeOverAnimator();
            Inputs walk = CharacterTestRig.Move(Vector3.forward * (WalkLoopSpeed() / rig.WalkSpeed));
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
