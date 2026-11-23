using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using MeridianMotion.Characters;
using NUnit.Framework;
using UnityEngine.TestTools;
using UnityEngine;

namespace MeridianMotion.Tests.Models
{
    /// <summary>
    /// Tests of how the character's model is kept separate from the character: it's created at runtime by a
    /// CharacterModelSource, can be swapped for a different one (even one without an Animator, mid-run or mid-transition),
    /// stays hidden until it's swapped in, loading is cleaned up if the character goes away first, a placeholder or a
    /// fallback stands in, and sources release the models they made. (Fitting the character to its model is
    /// ModelFitTests'; how translators turn animation state into Animator parameters, AnimatorParameterTranslatorTests'.)
    /// </summary>
    [Category(TestArea.Models)]
    public class ModelTests : ModelTestsBase
    {
        /// <summary>
        /// A character left without its model (never loaded, as on a server) moves exactly as one with it, step for step,
        /// bit for bit: its body is the one its model would give it from its first step (fitted from its source's prepared
        /// body, not measured once the model arrives: see CharacterModelSlot.FitBodyFromSource), and nothing of the model
        /// (its animation, IK, hitboxes) touches the movement.
        /// </summary>
        [UnityTest]
        public IEnumerator WithoutItsModel_ACharacterMovesExactlyAsWithIt()
        {
            var poses = new List<(Vector3 position, Vector3 velocity)>();
            BodyShape withModel = default;
            for (int run = 0; run < 2; run++)
            {
                bool loads = run == 0;
                if (!loads) { yield return ReplaceRig(); }
                rig.Floor();
                rig.Box("Step", new Vector3(0f, 0.1f, 3f), new Vector3(4f, 0.2f, 1f));
                rig.SpawnCharacter(new Vector3(0f, 0f, 0f));
                // Before its Start, which would load it.
                rig.Models.LoadModelOnStart = loads;
                if (loads)
                {
                    yield return rig.WaitForModel();
                    withModel = rig.BodyNow;
                }
                else
                {
                    yield return null;
                    Assume.That(rig.Models.IsModelReady || rig.Models.IsLoadingModel, Is.False, "Test setup: the second character should have no model.");
                    Assert.That(rig.BodyNow, Is.EqualTo(withModel), "Without its model, the character should have the body its model gives it.");
                }
                int step = 0, differed = -1;
                yield return rig.Run(2f, i => i >= 30 && i < 38 ? CharacterTestRig.Jump(Vector3.forward) : CharacterTestRig.Move(Vector3.forward), _ =>
                {
                    (Vector3, Vector3) now = (rig.Position, rig.Velocity);
                    if (loads) { poses.Add(now); }
                    else if (differed < 0 && !now.Equals(poses[step])) { differed = step; }
                    step++;
                });
                if (!loads) { Assert.That(differed, Is.EqualTo(-1), $"Without its model, the character should move exactly as with it: step {differed} differed."); }
            }
        }

        [UnityTest]
        public IEnumerator Model_LoadsAtRuntime_AndIsSetUpForTheCharacter()
        {
            rig.Floor();
            rig.SpawnCharacter(new Vector3(0f, 0.05f, 0f));
            CharacterModelSlot models = rig.Models;
            MMotionAnimator animation = rig.Animation;
            int loadedEvents = 0;
            models.ModelLoaded += _ => loadedEvents++;
            Assert.That(models.ModelSource, Is.Not.Null, "The Character prefab should name a model source.");

            yield return rig.WaitForModel();

            GameObject model = models.ModelRoot;
            Assert.That(loadedEvents, Is.EqualTo(1), "ModelLoaded should be raised once.");
            Assert.That(model.transform.parent, Is.Not.Null);
            Assert.That(model.transform.IsChildOf(rig.Character.transform), Is.True, "The model should be created on the character.");
            Assert.That(model.layer, Is.EqualTo(rig.Character.layer), "The model should be on the character's layer.");
            Assert.That(models.Animator, Is.Not.Null);
            Assert.That(models.Animator.runtimeAnimatorController, Is.Not.Null);
            Assert.That(models.Animator.isHuman, Is.True);
            Assert.That(models.Animator.applyRootMotion, Is.False);
            Assert.That(models.IK, Is.Not.Null, "The model should have HumanoidIK.");
            Assert.That(models.Animator.GetComponent<HumanoidFootPlacementIK>(), Is.Not.Null, "The model should have foot placement.");
        }

        [UnityTest]
        public IEnumerator SwappingTheModel_ReplacesTheOldOne_AndWorksWithoutAnAnimator()
        {
            rig.Floor();
            rig.SpawnCharacter(new Vector3(0f, 0.05f, 0f));
            CharacterModelSlot models = rig.Models;
            MMotionAnimator animation = rig.Animation;
            yield return rig.WaitForModel();
            GameObject oldModel = models.ModelRoot;

            PrefabModelSource cube = CubeSource();
            bool loaded = false;
            yield return CharacterTestRig.WaitFor(models.LoadModelAsync(cube), l => loaded = l);
            yield return null; // Destroy takes effect at the end of the frame.

            Assert.That(loaded, Is.True);

            Assert.That(oldModel == null, Is.True, "The old model should be destroyed.");
            Assert.That(models.ModelRoot, Is.Not.Null);
            Assert.That(models.ModelRoot.name, Does.StartWith("Cube Model"));
            Assert.That(models.ModelRoot.transform.IsChildOf(rig.Character.transform), Is.True);
            Assert.That(models.ModelSource, Is.SameAs(cube));
            Assert.That(models.Animator, Is.Null);
            Assert.That(models.IK, Is.Null);

            // The character carries on as normal without an Animator.
            models.ModelRoot.SetActive(true);
            float startZ = rig.Position.z;
            const float walkTime = 0.3f;
            yield return rig.Run(walkTime, _ => CharacterTestRig.Move(Vector3.forward), _ =>
                animation.UpdateAnimator(rig.Movement.lastStep, Time.fixedDeltaTime));
            Assert.That(rig.Position.z - startZ, Is.GreaterThan(0.5f * rig.WalkSpeed * walkTime), "The character should still move.");
            Assert.That(animation.AnimationState.moving, Is.True, "The animation state should still be worked out.");
        }

        /// <summary>
        /// Stands in for a source that loads from somewhere else (Addressables, an asset bundle): it takes a while,
        /// records whether its cancellation cleanup ran, and releases its models itself.
        /// </summary>
        private class SlowSource : CharacterModelSource
        {
            /// <summary>How many frames it takes to make its model.</summary>
            public const int Frames = 30;
            public bool cleanedUp;
            public int released;

            public override async Awaitable<LoadedCharacterModel> CreateModelAsync(CharacterModelContext context, CancellationToken cancellationToken)
            {
                try
                {
                    for (int i = 0; i < Frames; i++) { await Awaitable.NextFrameAsync(cancellationToken); }
                }
                catch (System.OperationCanceledException)
                {
                    cleanedUp = true; // Where a real source would release what it had loaded so far.
                    throw;
                }
                var model = new GameObject("Slow Model");
                await SetUpModelAsync(model, context, cancellationToken);
                return new LoadedCharacterModel(model, Translator);
            }

            public override void ReleaseModel(LoadedCharacterModel model)
            {
                released++;
                base.ReleaseModel(model);
            }
        }

        // Frames past a load's own (SlowSource.Frames, or a prefab source's next frame) for it to have produced its model,
        // had it not been cancelled: a test that it doesn't waits this long, as there's nothing to wait for.
        private const int LoadSlackFrames = 10;

        /// <summary>
        /// A character destroyed while its model loads cancels the load (its source's cleanup runs) and leaves no model
        /// behind, whether the source is slow (stands in for Addressables) or instantiates straight away.
        /// </summary>
        [UnityTest]
        public IEnumerator DestroyingTheCharacterWhileItsModelLoads_CancelsTheLoad_AndLeavesNoModelBehind()
        {
            PrefabModelSource cube = CubeSource();
            for (int i = 0; i < 5; i++)
            {
                rig.SpawnCharacter(new Vector3(i * 3f, 0f, 0f));
                _ = rig.Models.LoadModelAsync(cube);
                Object.Destroy(rig.Character);
                yield return null;
            }
            for (int i = 0; i < LoadSlackFrames; i++) { yield return null; }
            foreach (GameObject obj in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include))
            {
                Assert.That(obj.name.StartsWith("Cube Model(Clone)"), Is.False, "A model finished loading for a character that no longer exists.");
            }

            var slow = Track(ScriptableObject.CreateInstance<SlowSource>());
            rig.SpawnCharacter(Vector3.zero);
            _ = rig.Models.LoadModelAsync(slow);
            yield return null;
            Assert.That(rig.Models.IsLoadingModel, Is.True, "Test setup: the model should still be loading.");

            Object.Destroy(rig.Character);
            yield return null;
            Assert.That(slow.cleanedUp, Is.True, "The source's cleanup should run when its character is destroyed.");
            for (int i = 0; i < SlowSource.Frames + LoadSlackFrames; i++) { yield return null; }
            Assert.That(GameObject.Find("Slow Model"), Is.Null, "The cancelled load shouldn't produce a model.");
        }

        [UnityTest]
        public IEnumerator LoadingAnotherModelMidLoad_CancelsTheFirst()
        {
            var slow = Track(ScriptableObject.CreateInstance<SlowSource>());
            rig.SpawnCharacter(Vector3.zero);
            bool slowLoaded = true;
            IEnumerator first = CharacterTestRig.WaitFor(rig.Models.LoadModelAsync(slow), l => slowLoaded = l);
            yield return null;

            yield return CharacterTestRig.WaitFor(rig.Models.LoadModelAsync(CubeSource()));
            yield return first;

            Assert.That(slow.cleanedUp, Is.True, "Replacing a model mid-load should cancel the first load.");
            Assert.That(slowLoaded, Is.False, "The first load should report that it didn't load.");
            Assert.That(rig.Models.ModelRoot.name, Does.StartWith("Cube Model"));
            for (int i = 0; i < SlowSource.Frames + LoadSlackFrames; i++) { yield return null; }
            Assert.That(GameObject.Find("Slow Model"), Is.Null, "The cancelled load shouldn't produce a model.");
        }

        [UnityTest]
        public IEnumerator SourcesReleaseTheModelsTheyMade_WhenReplacedOrTheCharacterGoes()
        {
            var slow = Track(ScriptableObject.CreateInstance<SlowSource>());
            rig.SpawnCharacter(Vector3.zero);
            bool loaded = false;
            yield return CharacterTestRig.WaitFor(rig.Models.LoadModelAsync(slow), l => loaded = l);
            Assert.That(loaded, Is.True);
            Assert.That(rig.Models.ModelRoot.name, Is.EqualTo("Slow Model"));
            Assert.That(rig.Models.ModelRoot.transform.IsChildOf(rig.Character.transform), Is.True, "SetUpModelAsync should place it on the character.");

            yield return CharacterTestRig.WaitFor(rig.Models.LoadModelAsync(CubeSource()));
            Assert.That(slow.released, Is.EqualTo(1), "The replaced model should go back to the source that made it.");

            yield return CharacterTestRig.WaitFor(rig.Models.LoadModelAsync(slow));
            Object.Destroy(rig.Character);
            yield return null;
            Assert.That(slow.released, Is.EqualTo(2), "A destroyed character's model should go back to its source too.");
        }

        /// <summary>Creates its model at once, then takes a while to finish setting it up.</summary>
        private class SlowSetupSource : CharacterModelSource
        {
            public GameObject template;

            public override async Awaitable<LoadedCharacterModel> CreateModelAsync(CharacterModelContext context, CancellationToken cancellationToken)
            {
                GameObject model = Instantiate(template, context.Parent);
                model.name = "Slow Setup Model";
                model.SetActive(true); // As a real model prefab would be; the test's template is kept inactive.
                Assert.That(model.activeInHierarchy, Is.False, "The context's parent should keep a new model hidden.");
                await SetUpModelAsync(model, context, cancellationToken);
                for (int i = 0; i < 10; i++) { await Awaitable.NextFrameAsync(cancellationToken); }
                return new LoadedCharacterModel(model, Translator);
            }
        }

        [UnityTest]
        public IEnumerator ANewModel_StaysHiddenUntilItIsSwappedIn_AndTheOldOneGoesAtTheSameTime()
        {
            rig.Floor();
            rig.SpawnCharacter(new Vector3(0f, 0.05f, 0f));
            CharacterModelSlot models = rig.Models;
            yield return rig.WaitForModel();
            GameObject oldModel = models.ModelRoot;

            var slow = Track(ScriptableObject.CreateInstance<SlowSetupSource>());
            slow.template = CubeSource().ModelPrefab;
            bool swapped = false;
            IEnumerator loading = CharacterTestRig.WaitFor(models.LoadModelAsync(slow), l => swapped = l);
            yield return null;
            yield return null;

            GameObject incoming = rig.Character.GetComponentsInChildren<Transform>(true)
                .Select(t => t.gameObject).FirstOrDefault(o => o.name == "Slow Setup Model");
            Assert.That(incoming, Is.Not.Null, "Test setup: the new model should have been created.");
            Assert.That(incoming.activeInHierarchy, Is.False, "A model still being set up shouldn't be seen.");
            Assert.That(oldModel.activeInHierarchy, Is.True, "The old model should stay until the new one is ready.");

            yield return loading;
            Assert.That(swapped, Is.True);
            Assert.That(incoming.activeInHierarchy, Is.True, "The new model should appear once it's swapped in.");
            Assert.That(oldModel == null || !oldModel.activeInHierarchy, Is.True,
                "The old model should disappear in the same frame the new one appears.");
            Assert.That(incoming.transform.parent, Is.SameAs(models.ModelParent));
        }

        [UnityTest]
        public IEnumerator ThePlaceholder_ShowsWhileThereIsNoModel()
        {
            rig.Floor();
            rig.SpawnCharacter(new Vector3(0f, 0.05f, 0f));
            GameObject placeholder = rig.Models.Placeholder;
            if (placeholder == null) { Assert.Ignore("The character has no placeholder."); }
            // Checked straight away: the model can finish loading within a frame or two.
            Assert.That(placeholder.activeSelf, Is.True, "The placeholder should show until the model is in.");

            yield return rig.WaitForModel();
            Assert.That(placeholder.activeSelf, Is.False, "The placeholder should hide once the model is in.");

            rig.Models.UnloadModel();
            Assert.That(placeholder.activeSelf, Is.True, "The placeholder should come back without a model.");
        }

        [UnityTest]
        public IEnumerator AModelThatFailsToLoad_IsReplacedByTheFallback()
        {
            rig.Floor();
            rig.SpawnCharacter(new Vector3(0f, 0.05f, 0f));
            CharacterModelSlot models = rig.Models;
            yield return rig.WaitForModel();
            PrefabModelSource cube = CubeSource();
            models.FallbackSource = cube;

            bool loaded = false;
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("didn't create a model"));
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Using the fallback model"));
            yield return CharacterTestRig.WaitFor(models.LoadModelAsync(Track(ScriptableObject.CreateInstance<FailingSource>())), l => loaded = l);

            Assert.That(loaded, Is.True, "The fallback should load in its place.");
            Assert.That(models.ModelRoot.name, Does.StartWith("Cube Model"));
        }

        /// <summary>
        /// A model swapped in while the character runs carries on from where the old one was: in the same state, the
        /// same way through it, with the same parameters, rather than starting over from standing. (Its foot placement
        /// carrying on is FootLockTests.ASwappedInModel_CarriesOnTheOldOnesFootPlacement.)
        /// </summary>
        [UnityTest]
        public IEnumerator SwappingTheModelMidRun_CarriesOnWhereTheOldOneWas()
        {
            rig.Floor();
            rig.SpawnCharacter(new Vector3(0f, 0.05f, 0f));
            yield return rig.WaitForModel();
            CharacterModelSlot models = rig.Models;
            if (models.Animator == null || models.Animator.runtimeAnimatorController == null) { Assert.Ignore("The character's model isn't animated."); }
            // Every float parameter the controller has (the speed, for the package's), to check they carry over.
            int[] floats = models.Animator.parameters.Where(p => p.type == AnimatorControllerParameterType.Float).Select(p => p.nameHash).ToArray();
            MMotionAnimator animation = rig.Animation;
            // The rig moves the character itself; the animation is fed each step, as the character's own update would.
            void Feed(int _) => animation.UpdateAnimator(rig.Movement.lastStep, Time.fixedDeltaTime);
            yield return rig.Run(1.5f, _ => CharacterTestRig.Move(Vector3.forward), Feed);

            AnimatorStateInfo was = default;
            float[] wasFloats = null;
            int swapFrame = -1;
            float swapTime = 0f;
            models.ModelReplacing += old =>
            {
                was = old.Animator.GetCurrentAnimatorStateInfo(0);
                wasFloats = floats.Select(old.Animator.GetFloat).ToArray();
                swapFrame = Time.frameCount;
                swapTime = Time.time;
            };
            _ = models.LoadModelAsync(OwnCopy());
            // Running on while the new model loads, until the frame after it's in.
            for (int i = 0; i < 300 && (swapFrame < 0 || Time.frameCount == swapFrame); i++)
            {
                yield return rig.Run(1, _ => CharacterTestRig.Move(Vector3.forward), Feed);
            }
            Assert.That(swapFrame, Is.GreaterThan(0), "Test setup: the new model should have loaded.");
            // A frame or so has passed since the swap, which the tolerance below covers.
            AnimatorStateInfo now = models.Animator.GetCurrentAnimatorStateInfo(0);

            TestContext.WriteLine($"Before the swap: state {was.fullPathHash}, {was.normalizedTime:0.000} through it. " +
                                  $"After: state {now.fullPathHash}, {now.normalizedTime:0.000}.");
            Assert.That(was.normalizedTime, Is.GreaterThan(0.5f), "Test setup: the old model should have been well into its state.");
            Assert.That(now.fullPathHash, Is.EqualTo(was.fullPathHash), "The new model should be in the state the old one was in.");
            // As far through, give or take the time that's passed since (now.length is the state's length at the speed
            // it plays at, which the blend tree raises for running), and a little.
            float passed = (Time.time - swapTime) / now.length;
            Assert.That(now.normalizedTime, Is.InRange(was.normalizedTime - 0.05f, was.normalizedTime + passed + 0.05f), "It should be as far through it.");
            for (int i = 0; i < floats.Length; i++)
            {
                // A frame's change at most (the speed is smoothed).
                Assert.That(models.Animator.GetFloat(floats[i]), Is.EqualTo(wasFloats[i]).Within(0.1f * Mathf.Abs(wasFloats[i]) + 1e-3f), "Its parameters should carry over.");
            }
        }

#if UNITY_EDITOR
        /// <summary>
        /// The handover from one model's Animator to another's, directly: a model swapped in mid-transition (taking off
        /// into a jump, say) is put back part-way through it, between the same two states, with its parameters. On a
        /// controller made here (two states, a transition between them on a bool), so it holds for any controller.
        /// </summary>
        [UnityTest]
        public IEnumerator AModelSwappedInMidTransition_CarriesOnPartWayThroughIt()
        {
            var controller = Track(new UnityEditor.Animations.AnimatorController());
            controller.AddLayer("Base");
            controller.AddParameter(AnimatorParameterTranslator.DefaultGroundedParameter, AnimatorControllerParameterType.Bool);
            UnityEditor.Animations.AnimatorStateMachine machine = controller.layers[0].stateMachine;
            AnimationClip Clip(string name)
            {
                var clip = Track(new AnimationClip { name = name });
                clip.SetCurve("", typeof(Transform), "localPosition.x", AnimationCurve.Linear(0f, 0f, 1f, 1f)); // A second long.
                return clip;
            }
            UnityEditor.Animations.AnimatorState ground = machine.AddState("Ground"), air = machine.AddState("Air");
            ground.motion = Clip("Ground");
            air.motion = Clip("Air");
            machine.defaultState = ground;
            Animator Spawn(string name)
            {
                var model = Track(new GameObject(name));
                var animator = model.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                return animator;
            }
            Animator old = Spawn("Old Model"), replacement = Spawn("New Model");
            var translator = Track(ScriptableObject.CreateInstance<AnimatorParameterTranslator>());
            yield return null;

            int groundState = Animator.StringToHash("Base.Ground"), airState = Animator.StringToHash("Base.Air");
            int grounded = Animator.StringToHash(AnimatorParameterTranslator.DefaultGroundedParameter);
            old.Play(groundState, 0, 0.3f);
            old.Update(0f);
            old.SetBool(grounded, false);
            old.CrossFadeInFixedTime(airState, 0.2f, 0);
            old.Update(0.08f);
            Assert.That(old.IsInTransition(0), Is.True, "Test setup: the old model should be part-way into the transition.");
            AnimatorTransitionInfo wasTransition = old.GetAnimatorTransitionInfo(0);
            AnimatorStateInfo wasNext = old.GetNextAnimatorStateInfo(0);

            ICharacterAnimationDriver oldDriver = translator.CreateDriver(old.gameObject, old);
            ICharacterAnimationDriver newDriver = translator.CreateDriver(replacement.gameObject, replacement);
            oldDriver.Retire();
            newDriver.ContinueFrom(oldDriver);
            replacement.Update(0f);

            Assert.That(replacement.GetBool(grounded), Is.False, "Parameters should carry over.");
            Assert.That(replacement.IsInTransition(0), Is.True, "The new model should be in the transition the old one was in.");
            Assert.That(replacement.GetCurrentAnimatorStateInfo(0).fullPathHash, Is.EqualTo(groundState));
            AnimatorStateInfo next = replacement.GetNextAnimatorStateInfo(0);
            Assert.That(next.fullPathHash, Is.EqualTo(airState));
            Assert.That(next.normalizedTime, Is.EqualTo(wasNext.normalizedTime).Within(0.02f));
            Assert.That(replacement.GetAnimatorTransitionInfo(0).normalizedTime, Is.EqualTo(wasTransition.normalizedTime).Within(0.02f),
                "It should be as far through the transition.");
        }
#endif
    }
}
