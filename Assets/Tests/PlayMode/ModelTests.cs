using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CharacterShowcase.Tests
{
    /// <summary>
    /// Tests of how the character's model is kept separate from the character: it's created at runtime by a
    /// CharacterModelSource, can be swapped for a different one (even one without an Animator), loading is cleaned up
    /// if the character goes away first, sources release the models they made, and translators turn animation state
    /// and events into Animator parameters.
    /// </summary>
    public class ModelTests
    {
        private CharacterTestRig rig;
        private readonly List<Object> created = new();

        [SetUp]
        public void SetUp() => rig = new CharacterTestRig();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            rig.Dispose();
            foreach (Object obj in created) { if (obj != null) { Object.Destroy(obj); } }
            created.Clear();
            yield return null;
        }

        private T Track<T>(T obj) where T : Object
        {
            created.Add(obj);
            return obj;
        }

        /// <summary>A source whose model is a plain cube: no Animator, no humanoid rig.</summary>
        private DirectCharacterModelSource CubeSource()
        {
            GameObject template = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            template.name = "Cube Model";
            Object.Destroy(template.GetComponent<Collider>());
            template.SetActive(false); // Only a template to copy; copies are activated below.
            var source = Track(ScriptableObject.CreateInstance<DirectCharacterModelSource>());
            source.ModelPrefab = template;
            return source;
        }

        [UnityTest]
        public IEnumerator Model_LoadsAtRuntime_AndIsSetUpForTheCharacter()
        {
            rig.Floor();
            rig.SpawnCharacter(new Vector3(0f, 0.05f, 0f));
            CharacterModelHost models = rig.Models;
            AnimationController animation = rig.Animation;
            int loadedEvents = 0;
            models.ModelLoaded += _ => loadedEvents++;
            Assert.That(models.ModelSource, Is.Not.Null, "The Character prefab should name a model source.");

            yield return rig.WaitForModel();

            GameObject model = models.Model;
            Assert.That(loadedEvents, Is.EqualTo(1), "ModelLoaded should be raised once.");
            Assert.That(model.transform.parent, Is.Not.Null);
            Assert.That(model.transform.IsChildOf(rig.Character.transform), Is.True, "The model should be created on the character.");
            Assert.That(model.layer, Is.EqualTo(rig.Character.layer), "The model should be on the character's layer.");
            Assert.That(models.Animator, Is.Not.Null);
            Assert.That(models.Animator.runtimeAnimatorController, Is.Not.Null);
            Assert.That(models.Animator.isHuman, Is.True);
            Assert.That(models.Animator.applyRootMotion, Is.False);
            Assert.That(models.IK, Is.Not.Null, "The model should have CharacterIK.");
            Assert.That(models.Animator.GetComponent<FootPlacementIK>(), Is.Not.Null, "The model should have foot placement.");
        }

        [UnityTest]
        public IEnumerator SwappingTheModel_ReplacesTheOldOne_AndWorksWithoutAnAnimator()
        {
            rig.Floor();
            rig.SpawnCharacter(new Vector3(0f, 0.05f, 0f));
            CharacterModelHost models = rig.Models;
            AnimationController animation = rig.Animation;
            yield return rig.WaitForModel();
            GameObject oldModel = models.Model;

            DirectCharacterModelSource cube = CubeSource();
            bool loaded = false;
            yield return CharacterTestRig.WaitFor(models.LoadModelAsync(cube), l => loaded = l);
            yield return null; // Destroy takes effect at the end of the frame.

            Assert.That(loaded, Is.True);

            Assert.That(oldModel == null, Is.True, "The old model should be destroyed.");
            Assert.That(models.Model, Is.Not.Null);
            Assert.That(models.Model.name, Does.StartWith("Cube Model"));
            Assert.That(models.Model.transform.IsChildOf(rig.Character.transform), Is.True);
            Assert.That(models.ModelSource, Is.SameAs(cube));
            Assert.That(models.Animator, Is.Null);
            Assert.That(models.IK, Is.Null);

            // The character carries on as normal without an Animator.
            models.Model.SetActive(true);
            float startZ = rig.Position.z;
            const float walkTime = 0.3f;
            yield return rig.Run(walkTime, _ => CharacterTestRig.Move(Vector3.forward), _ =>
                animation.UpdateAnimator(rig.Movement.lastFrameMovementData, Time.fixedDeltaTime));
            Assert.That(rig.Position.z - startZ, Is.GreaterThan(0.5f * rig.WalkSpeed * walkTime), "The character should still move.");
            Assert.That(animation.AnimationState.moving, Is.True, "The animation state should still be worked out.");
        }

        /// <summary>
        /// Stands in for a source that loads from somewhere else (Addressables, an asset bundle): it takes a while,
        /// records whether its cancellation cleanup ran, and releases its models itself.
        /// </summary>
        private class SlowSource : CharacterModelSource
        {
            public bool cleanedUp;
            public int released;

            public override async Awaitable<CharacterModel> CreateModelAsync(CharacterModelContext context, CancellationToken cancellationToken)
            {
                try
                {
                    for (int i = 0; i < 30; i++) { await Awaitable.NextFrameAsync(cancellationToken); }
                }
                catch (System.OperationCanceledException)
                {
                    cleanedUp = true; // Where a real source would release what it had loaded so far.
                    throw;
                }
                var model = new GameObject("Slow Model");
                await SetUpModelAsync(model, context, cancellationToken);
                return new CharacterModel(model, Translator);
            }

            public override void ReleaseModel(CharacterModel model)
            {
                released++;
                base.ReleaseModel(model);
            }
        }

        /// <summary>
        /// A character destroyed while its model loads cancels the load (its source's cleanup runs) and leaves no model
        /// behind, whether the source is slow (stands in for Addressables) or instantiates straight away.
        /// </summary>
        [UnityTest]
        public IEnumerator DestroyingTheCharacterWhileItsModelLoads_CancelsTheLoad_AndLeavesNoModelBehind()
        {
            DirectCharacterModelSource cube = CubeSource();
            for (int i = 0; i < 5; i++)
            {
                rig.SpawnCharacter(new Vector3(i * 3f, 0f, 0f));
                _ = rig.Models.LoadModelAsync(cube);
                Object.Destroy(rig.Character);
                yield return null;
            }
            for (int i = 0; i < 10; i++) { yield return null; }
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
            for (int i = 0; i < 40; i++) { yield return null; }
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
            Assert.That(rig.Models.Model.name, Does.StartWith("Cube Model"));
            for (int i = 0; i < 40; i++) { yield return null; }
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
            Assert.That(rig.Models.Model.name, Is.EqualTo("Slow Model"));
            Assert.That(rig.Models.Model.transform.IsChildOf(rig.Character.transform), Is.True, "SetUpModelAsync should place it on the character.");

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

            public override async Awaitable<CharacterModel> CreateModelAsync(CharacterModelContext context, CancellationToken cancellationToken)
            {
                GameObject model = Instantiate(template, context.Parent);
                model.name = "Slow Setup Model";
                model.SetActive(true); // As a real model prefab would be; the test's template is kept inactive.
                Assert.That(model.activeInHierarchy, Is.False, "The context's parent should keep a new model hidden.");
                await SetUpModelAsync(model, context, cancellationToken);
                for (int i = 0; i < 10; i++) { await Awaitable.NextFrameAsync(cancellationToken); }
                return new CharacterModel(model, Translator);
            }
        }

        /// <summary>Never manages to create a model.</summary>
        private class FailingSource : CharacterModelSource
        {
            public override async Awaitable<CharacterModel> CreateModelAsync(CharacterModelContext context, CancellationToken cancellationToken)
            {
                await Awaitable.NextFrameAsync(cancellationToken);
                return null;
            }
        }

        [UnityTest]
        public IEnumerator ANewModel_StaysHiddenUntilItIsSwappedIn_AndTheOldOneGoesAtTheSameTime()
        {
            rig.Floor();
            rig.SpawnCharacter(new Vector3(0f, 0.05f, 0f));
            CharacterModelHost models = rig.Models;
            yield return rig.WaitForModel();
            GameObject oldModel = models.Model;

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
            CharacterModelHost models = rig.Models;
            yield return rig.WaitForModel();
            DirectCharacterModelSource cube = CubeSource();
            models.FallbackSource = cube;

            bool loaded = false;
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("didn't create a model"));
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Using the fallback model"));
            yield return CharacterTestRig.WaitFor(models.LoadModelAsync(Track(ScriptableObject.CreateInstance<FailingSource>())), l => loaded = l);

            Assert.That(loaded, Is.True, "The fallback should load in its place.");
            Assert.That(models.Model.name, Does.StartWith("Cube Model"));
        }

        /// <summary>
        /// The character's own model's source, which these tests copy to try its settings out on the same model; skips the
        /// test if the source doesn't reference its model directly (it loads it from somewhere else, say).
        /// </summary>
        private DirectCharacterModelSource OwnSource()
        {
            if (rig.Models.ModelSource is DirectCharacterModelSource own && own.ModelPrefab != null) { return own; }
            Assert.Ignore("The character's model source doesn't reference its model directly.");
            return null;
        }

        /// <summary>A source for the character's own model, fitted to the given height.</summary>
        private DirectCharacterModelSource Fitted(float height)
        {
            DirectCharacterModelSource own = OwnSource();
            var fitted = Track(ScriptableObject.CreateInstance<DirectCharacterModelSource>());
            fitted.ModelPrefab = own.ModelPrefab;
            fitted.Translator = own.Translator;
            fitted.AnimatorController = own.AnimatorController;
            fitted.Avatar = own.Avatar;
            fitted.GroundOffset = own.GroundOffset;
            fitted.AnimatedGroundOffset = own.AnimatedGroundOffset;
            fitted.MeasureGroundOnLoad = own.MeasureGroundOnLoad;
            fitted.Height = ModelHeight.FitToHeight;
            fitted.FitHeight = height;
            return fitted;
        }

        /// <summary>
        /// Fit To Height scales the model to the height asked for, for the character as authored, and its eyes (and the
        /// eye point) follow; on a resized character, the character's size multiplies that, as it does any model's.
        /// </summary>
        [UnityTest]
        public IEnumerator FitToHeight_ScalesTheModel_InProportionToTheCharacter_AndTheEyesFollow([Values(1f, 2f)] float size)
        {
            rig.Floor();
            rig.SpawnCharacter(new Vector3(0f, 0.05f, 0f));
            CharacterModelHost models = rig.Models;
            yield return rig.WaitForModel();
            yield return new WaitForFixedUpdate();
            float ownEyes = models.EyeHeight, ownHeight = models.ModelHeightMeters;
            Assert.That(ownEyes, Is.InRange(0.5f * ownHeight, ownHeight), "The eyes should be in the model's upper half.");
            if (models.EyePoint != null) { Assert.That(models.EyePoint.localPosition.y, Is.EqualTo(ownEyes).Within(1e-4f)); }
            if (size != 1f) { rig.Controller.SetScale(Vector3.one * size, immediately: true); }

            // The same model fitted to a smaller height: everything about its size should follow.
            const float fit = 1.2f;
            DirectCharacterModelSource fitted = Fitted(fit);
            yield return CharacterTestRig.WaitFor(models.LoadModelAsync(fitted));

            Assert.That(models.ModelHeightMeters, Is.EqualTo(fit * size).Within(0.01f * size), $"The model should be fitted to {fit} m, times the character's size.");
            float expectedEyes = ownEyes * fit / ownHeight * size;
            Assert.That(models.EyeHeight, Is.EqualTo(expectedEyes).Within(0.02f * size), "The eyes should scale with the model.");
            if (models.EyePoint != null) { Assert.That(models.EyePoint.localPosition.y, Is.EqualTo(models.EyeHeight).Within(1e-4f)); }
        }

        private CapsuleCollider Capsule => rig.Character.GetComponent<CapsuleCollider>();

        /// <summary>
        /// Collider Fit makes the collider as tall as the model, standing on the character's feet: its width in proportion
        /// as authored (Height), or as wide as the model's shoulders, times the margin (Height And Width). The character's
        /// size still multiplies it.
        /// </summary>
        [UnityTest]
        public IEnumerator ColliderFit_MakesTheColliderAsTallAsTheModel_AndAsWideAsAsked(
            [Values(ColliderFit.Height, ColliderFit.HeightAndWidth)] ColliderFit fit)
        {
            rig.Floor();
            rig.SpawnCharacter(new Vector3(0f, 0.05f, 0f));
            yield return rig.WaitForModel();
            yield return new WaitForFixedUpdate();
            float radius = Capsule.radius, height = Capsule.height;
            rig.Models.ColliderFitting = fit;
            yield return CharacterTestRig.WaitFor(rig.Models.LoadModelAsync(Fitted(1.2f)));

            Assert.That(Capsule.height, Is.EqualTo(1.2f).Within(0.01f), "The collider should be as tall as the model.");
            Assert.That(Capsule.center.y, Is.EqualTo(Capsule.height / 2f).Within(1e-4f), "It should stand on the character's feet.");
            if (fit == ColliderFit.Height)
            {
                Assert.That(Capsule.radius, Is.EqualTo(radius * 1.2f / height).Within(0.005f), "Its width should keep its proportions.");
            }
            else
            {
                Animator animator = rig.Models.Animator;
                if (animator == null || !animator.isHuman) { Assert.Ignore("The character's model isn't a humanoid, so it has no shoulders to fit."); }
                float shoulders = Vector3.Distance(animator.GetBoneTransform(HumanBodyBones.LeftUpperArm).position,
                    animator.GetBoneTransform(HumanBodyBones.RightUpperArm).position);
                Assert.That(Capsule.radius, Is.EqualTo(0.5f * shoulders * rig.Models.ColliderWidthMargin).Within(0.01f),
                    "The collider should be as wide as the shoulders, times the margin.");
            }
            yield return rig.Run(0.5f, _ => CharacterTestRig.Idle());
            Assert.That(rig.Grounded, Is.True);
            Assert.That(rig.Position.y, Is.EqualTo(0f).Within(0.005f));

            // The character's size still multiplies it.
            float fitted = Capsule.height;
            rig.Controller.SetScale(Vector3.one * 2f, immediately: true);
            Assert.That(Capsule.height, Is.EqualTo(2f * fitted).Within(0.02f));
        }

        [UnityTest]
        public IEnumerator ColliderFit_WaitsForRoom_BeforeGrowing()
        {
            // A ceiling 1.5 m up from z = -2 to z = 2: room for a 1.2 m collider, not a 1.8 m one.
            rig.Box("Low Ceiling", new Vector3(0f, 2f, 0f), new Vector3(10f, 1f, 4f));
            rig.Floor();
            rig.SpawnCharacter(new Vector3(0f, 0.05f, 0f));
            yield return rig.WaitForModel();
            rig.Models.ColliderFitting = ColliderFit.Height;
            yield return CharacterTestRig.WaitFor(rig.Models.LoadModelAsync(Fitted(1.2f)));
            yield return rig.Run(0.3f, _ => CharacterTestRig.Idle());
            Assert.That(Capsule.height, Is.EqualTo(1.2f).Within(0.01f), "Test setup: the shorter collider should fit.");

            yield return CharacterTestRig.WaitFor(rig.Models.LoadModelAsync(Fitted(1.8f)));
            yield return rig.Run(0.3f, _ => CharacterTestRig.Idle());
            Assert.That(rig.Models.IsFittingCollider, Is.True, "Under the ceiling, it should wait to grow.");
            Assert.That(Capsule.height, Is.EqualTo(1.2f).Within(0.01f), "It shouldn't grow into the ceiling.");
            Assert.That(rig.Position.y, Is.EqualTo(0f).Within(0.005f), "It shouldn't be pushed through the floor.");

            AnimationController animation = rig.Animation;
            // The rig moves the character itself; the animation is fed each step, as the character's own update would.
            void Feed(int _) => animation.UpdateAnimator(rig.Movement.lastFrameMovementData, Time.fixedDeltaTime);
            yield return rig.Run(rig.SecondsToWalk(3f + rig.Radius, 1f), _ => CharacterTestRig.Move(Vector3.forward), Feed);
            Assert.That(rig.Position.z, Is.GreaterThan(3f), "Test setup: it should have walked out from under the ceiling.");
            Assert.That(rig.Models.IsFittingCollider, Is.False);
            Assert.That(Capsule.height, Is.EqualTo(1.8f).Within(0.01f), "Out in the open, it should grow to fit the model.");
        }

        /// <summary>A source for the character's own model (or the given prefab), as its own, but a separate asset.</summary>
        private DirectCharacterModelSource OwnCopy(GameObject prefab = null)
        {
            DirectCharacterModelSource own = OwnSource();
            var copy = Track(ScriptableObject.CreateInstance<DirectCharacterModelSource>());
            copy.ModelPrefab = prefab != null ? prefab : own.ModelPrefab;
            copy.Translator = own.Translator;
            copy.AnimatorController = own.AnimatorController;
            copy.Avatar = own.Avatar;
            copy.GroundOffset = own.GroundOffset;
            copy.AnimatedGroundOffset = own.AnimatedGroundOffset;
            copy.MeasureGroundOnLoad = false;
            return copy;
        }

        /// <summary>
        /// A model swapped in while the character runs carries on from where the old one was: in the same state, the
        /// same way through it, with the same parameters, and its feet as planted; rather than starting over from
        /// standing, with its feet placement fading back in.
        /// </summary>
        [UnityTest]
        public IEnumerator SwappingTheModelMidRun_CarriesOnWhereTheOldOneWas()
        {
            rig.Floor();
            rig.SpawnCharacter(new Vector3(0f, 0.05f, 0f));
            yield return rig.WaitForModel();
            CharacterModelHost models = rig.Models;
            if (models.Animator == null || models.Animator.runtimeAnimatorController == null) { Assert.Ignore("The character's model isn't animated."); }
            // Every float parameter the controller has (the speed, for the package's), to check they carry over.
            int[] floats = models.Animator.parameters.Where(p => p.type == AnimatorControllerParameterType.Float).Select(p => p.nameHash).ToArray();
            AnimationController animation = rig.Animation;
            // The rig moves the character itself; the animation is fed each step, as the character's own update would.
            void Feed(int _) => animation.UpdateAnimator(rig.Movement.lastFrameMovementData, Time.fixedDeltaTime);
            yield return rig.Run(1.5f, _ => CharacterTestRig.Move(Vector3.forward), Feed);

            AnimatorStateInfo was = default;
            float[] wasFloats = null;
            float wasFeet = -1f, newFeet = -1f;
            int swapFrame = -1;
            float swapTime = 0f;
            models.ModelReplacing += old =>
            {
                was = old.Animator.GetCurrentAnimatorStateInfo(0);
                wasFloats = floats.Select(old.Animator.GetFloat).ToArray();
                FootPlacementIK feet = old.Animator.GetComponent<FootPlacementIK>();
                wasFeet = feet != null ? feet.ActiveWeight : -1f;
                swapFrame = Time.frameCount;
                swapTime = Time.time;
            };
            models.ModelLoaded += created =>
            {
                FootPlacementIK feet = created.Animator.GetComponent<FootPlacementIK>();
                newFeet = feet != null ? feet.ActiveWeight : -1f;
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

            TestContext.WriteLine($"Before the swap: state {was.fullPathHash}, {was.normalizedTime:0.000} through it, feet at {wasFeet:0.00}. " +
                                  $"After: state {now.fullPathHash}, {now.normalizedTime:0.000}, feet at {newFeet:0.00}.");
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
            if (wasFeet >= 0f)
            {
                Assert.That(wasFeet, Is.GreaterThan(0.99f), "Test setup: the old model's feet should have been placed.");
                Assert.That(newFeet, Is.EqualTo(wasFeet), "Its feet should be placed as the old one's were, not fading back in.");
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

        /// <summary>
        /// A source measuring on load measures a model once, for every copy of it: the measurement is kept until it's
        /// forgotten (or the source edited, or its model, controller or avatar changed), as a changed model shows.
        /// </summary>
        [UnityTest]
        public IEnumerator MeasuringOnLoad_MeasuresAModelOnce_UntilTheMeasurementIsForgotten()
        {
            rig.Floor();
            rig.SpawnCharacter(new Vector3(0f, 0.05f, 0f));
            yield return rig.WaitForModel();
            DirectCharacterModelSource own = OwnSource();
            if (rig.Models.Animator == null || rig.Models.Animator.GetComponent<FootPlacementIK>() == null) { Assert.Ignore("The character's model has no foot placement to measure for."); }
            GameObject template = Track(Object.Instantiate(own.ModelPrefab));
            template.SetActive(false);
            DirectCharacterModelSource source = OwnCopy(template);
            source.MeasureGroundOnLoad = true;
            float FloorOffset() => rig.Models.Animator.GetComponent<FootPlacementIK>().FloorOffset;

            yield return CharacterTestRig.WaitFor(rig.Models.LoadModelAsync(source));
            float first = FloorOffset();
            if (!own.MeasureGroundOnLoad) { Assert.That(first, Is.EqualTo(own.GroundOffset).Within(1e-4f), "Test setup: measured as Prepare Character Models measured it."); }

            // A block under the model's feet: its lowest point, so its soles would be measured there.
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(block.GetComponent<Collider>());
            block.transform.SetParent(template.transform, false);
            block.transform.localPosition = new Vector3(0f, -0.3f, 0f);
            block.transform.localScale = Vector3.one * 0.2f;

            yield return CharacterTestRig.WaitFor(rig.Models.LoadModelAsync(source));
            Assert.That(FloorOffset(), Is.EqualTo(first), "Another copy of the model should use the measurement already made.");

            source.ForgetMeasurement();
            yield return CharacterTestRig.WaitFor(rig.Models.LoadModelAsync(source));
            Assert.That(FloorOffset(), Is.EqualTo(-0.4f).Within(0.01f), "Once forgotten, the model should be measured again.");
        }

        [UnityTest]
        public IEnumerator PreviewMeshes_ShowTheModelWhereItLoads()
        {
            rig.Floor();
            rig.SpawnCharacter(new Vector3(0f, 0.05f, 0f));
            yield return rig.WaitForModel();
            yield return null; // Let the skinned mesh update its bounds.

            var previews = new List<CharacterModelSource.PreviewMesh>();
            rig.Models.ModelSource.GetPreviewMeshes(previews);
            Assert.That(previews, Is.Not.Empty);

            // The preview should sit on the character's feet and be about as tall as the loaded model.
            Transform parent = rig.Models.Model.transform.parent;
            float bottom = float.PositiveInfinity, top = float.NegativeInfinity;
            foreach (CharacterModelSource.PreviewMesh preview in previews)
            {
                Bounds bounds = preview.mesh.bounds;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 local = bounds.center + Vector3.Scale(bounds.extents,
                        new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
                    float y = preview.matrix.MultiplyPoint3x4(local).y;
                    bottom = Mathf.Min(bottom, y);
                    top = Mathf.Max(top, y);
                }
            }
            float modelHeight = rig.Models.Model.GetComponentInChildren<Renderer>().bounds.size.y;
            Assert.That(bottom, Is.EqualTo(0f).Within(0.15f), "The preview should stand on the character's feet.");
            Assert.That(top - bottom, Is.EqualTo(modelHeight).Within(0.25f), "The preview should be the model's size.");
            Assert.That(parent, Is.Not.Null);
        }

#if UNITY_EDITOR
        [UnityTest]
        public IEnumerator AnimatorParameterTranslator_SetsParameters_FiresEventTriggers_AndSkipsMissingOnes()
        {
            var controller = Track(new UnityEditor.Animations.AnimatorController());
            controller.AddLayer("Base");
            controller.layers[0].stateMachine.AddState("Idle");
            controller.AddParameter(AnimatorParameterTranslator.DefaultSpeedParameter, AnimatorControllerParameterType.Float);
            controller.AddParameter(AnimatorParameterTranslator.DefaultGroundedParameter, AnimatorControllerParameterType.Bool);
            controller.AddParameter(AnimatorParameterTranslator.DefaultDashingParameter, AnimatorControllerParameterType.Bool);
            controller.AddParameter("Land", AnimatorControllerParameterType.Trigger);
            // No VerticalSpeed parameter: the translator should skip it without complaint.

            var model = Track(new GameObject("Translator Test Model"));
            var animator = model.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;

            var translator = Track(ScriptableObject.CreateInstance<AnimatorParameterTranslator>());
            var serialized = new UnityEditor.SerializedObject(translator);
            serialized.FindProperty("speedDampTime").floatValue = 0f;
            UnityEditor.SerializedProperty triggers = serialized.FindProperty("eventTriggers");
            triggers.arraySize = 1;
            triggers.GetArrayElementAtIndex(0).FindPropertyRelative("events").longValue = (long)MovementEvents.Landed;
            triggers.GetArrayElementAtIndex(0).FindPropertyRelative("trigger").stringValue = "Land";
            serialized.ApplyModifiedPropertiesWithoutUndo();
            yield return null;

            ICharacterAnimationDriver driver = translator.CreateDriver(model, animator);
            Assert.That(driver, Is.Not.Null);
            var state = new CharacterAnimationState { horizontalSpeed = 4f, grounded = true, verticalSpeed = 2f, dashing = true };
            driver.UpdateAnimation(state, 0.02f);
            Assert.That(animator.GetFloat(AnimatorParameterTranslator.DefaultSpeedParameter), Is.EqualTo(4f).Within(1e-4f));
            Assert.That(animator.GetBool(AnimatorParameterTranslator.DefaultGroundedParameter), Is.True);
            Assert.That(animator.GetBool(AnimatorParameterTranslator.DefaultDashingParameter), Is.True, "Dashing should be passed on (it plays the dash animation).");

            driver.HandleMovementEvent(MovementEvents.Jumped, state);
            Assert.That(animator.GetBool("Land"), Is.False, "Only the bound event should fire the trigger.");
            driver.HandleMovementEvent(MovementEvents.Landed, state);
            Assert.That(animator.GetBool("Land"), Is.True, "Landing should fire the Land trigger.");

            LogAssert.NoUnexpectedReceived();
            Assert.That(translator.CreateDriver(model, null), Is.Null, "There's nothing to drive without an Animator.");
        }
#endif
    }
}
