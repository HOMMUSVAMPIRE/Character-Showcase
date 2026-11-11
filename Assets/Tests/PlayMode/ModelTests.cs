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
            GameObject mannequin = models.Model;

            DirectCharacterModelSource cube = CubeSource();
            bool loaded = false;
            yield return CharacterTestRig.WaitFor(models.LoadModelAsync(cube), l => loaded = l);
            yield return null; // Destroy takes effect at the end of the frame.

            Assert.That(loaded, Is.True);

            Assert.That(mannequin == null, Is.True, "The old model should be destroyed.");
            Assert.That(models.Model, Is.Not.Null);
            Assert.That(models.Model.name, Does.StartWith("Cube Model"));
            Assert.That(models.Model.transform.IsChildOf(rig.Character.transform), Is.True);
            Assert.That(models.ModelSource, Is.SameAs(cube));
            Assert.That(models.Animator, Is.Null);
            Assert.That(models.IK, Is.Null);

            // The character carries on as normal without an Animator.
            models.Model.SetActive(true);
            yield return rig.Run(0.3f, _ => CharacterTestRig.Move(Vector3.forward), _ =>
                animation.UpdateAnimator(rig.Movement.lastFrameMovementData, Time.fixedDeltaTime));
            Assert.That(rig.Position.z, Is.GreaterThan(1f), "The character should still move.");
            Assert.That(animation.AnimationState.moving, Is.True, "The animation state should still be worked out.");
        }

        [UnityTest]
        public IEnumerator DestroyingTheCharacterWhileItsModelLoads_LeavesNoModelBehind()
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

            foreach (GameObject obj in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Assert.That(obj.name.StartsWith("Cube Model(Clone)"), Is.False, "A model finished loading for a character that no longer exists.");
            }
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

        [UnityTest]
        public IEnumerator DestroyingTheCharacterWhileItsModelLoads_CancelsTheLoad()
        {
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
            GameObject mannequin = models.Model;

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
            Assert.That(mannequin.activeInHierarchy, Is.True, "The old model should stay until the new one is ready.");

            yield return loading;
            Assert.That(swapped, Is.True);
            Assert.That(incoming.activeInHierarchy, Is.True, "The new model should appear once it's swapped in.");
            Assert.That(mannequin == null || !mannequin.activeInHierarchy, Is.True,
                "The old model should disappear in the same frame the new one appears.");
            Assert.That(incoming.transform.parent, Is.SameAs(models.ModelParent));
        }

        [UnityTest]
        public IEnumerator ThePlaceholder_ShowsWhileThereIsNoModel()
        {
            rig.Floor();
            rig.SpawnCharacter(new Vector3(0f, 0.05f, 0f));
            GameObject placeholder = rig.Character.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(t => t.name == "Model Placeholder")?.gameObject;
            Assert.That(placeholder, Is.Not.Null, "The Character prefab should have a placeholder.");
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

        [UnityTest]
        public IEnumerator FitToHeight_ScalesTheModel_AndTheEyePointFollowsIt()
        {
            rig.Floor();
            rig.SpawnCharacter(new Vector3(0f, 0.05f, 0f));
            CharacterModelHost models = rig.Models;
            yield return rig.WaitForModel();
            float mannequinEyes = models.EyeHeight;
            Assert.That(mannequinEyes, Is.InRange(1.55f, 1.72f), "The mannequin's eyes are in its visor, a little over 1.6 m up.");
            Transform eyePoint = rig.Character.transform.Find("Eye Point");
            Assert.That(eyePoint, Is.Not.Null, "The Character prefab should have an eye point.");
            Assert.That(eyePoint.localPosition.y, Is.EqualTo(mannequinEyes).Within(1e-4f));

            // The same model fitted to a smaller height: everything about its size should follow.
            var smaller = Track(ScriptableObject.CreateInstance<DirectCharacterModelSource>());
            var mannequin = (DirectCharacterModelSource)models.ModelSource;
            smaller.ModelPrefab = mannequin.ModelPrefab;
            smaller.Translator = mannequin.Translator;
            smaller.Height = ModelHeight.FitToHeight;
            smaller.FitHeight = 1.2f;
            yield return CharacterTestRig.WaitFor(models.LoadModelAsync(smaller));

            Assert.That(models.ModelHeightMeters, Is.EqualTo(1.2f).Within(0.01f), "The model should be fitted to 1.2 m.");
            float expectedEyes = mannequinEyes * 1.2f / CharacterModelSource.MeasureHeight(mannequin.ModelPrefab);
            Assert.That(models.EyeHeight, Is.EqualTo(expectedEyes).Within(0.02f), "The eyes should scale with the model.");
            Assert.That(eyePoint.localPosition.y, Is.EqualTo(models.EyeHeight).Within(1e-4f));
        }

        /// <summary>A source for the mannequin's model, fitted to the given height.</summary>
        private DirectCharacterModelSource FittedMannequin(float height)
        {
            var mannequin = (DirectCharacterModelSource)rig.Models.ModelSource;
            var fitted = Track(ScriptableObject.CreateInstance<DirectCharacterModelSource>());
            fitted.ModelPrefab = mannequin.ModelPrefab;
            fitted.Translator = mannequin.Translator;
            fitted.GroundOffset = mannequin.GroundOffset;
            fitted.Height = ModelHeight.FitToHeight;
            fitted.FitHeight = height;
            return fitted;
        }

        [UnityTest]
        public IEnumerator FitToHeight_OnAResizedCharacter_IsInProportionToIt()
        {
            rig.Floor();
            rig.SpawnCharacter(new Vector3(0f, 0.05f, 0f));
            yield return rig.WaitForModel();
            yield return new WaitForFixedUpdate();
            rig.Controller.SetScale(Vector3.one * 2f, immediately: true);
            yield return CharacterTestRig.WaitFor(rig.Models.LoadModelAsync(FittedMannequin(1.2f)));
            // Fit To Height sizes the model for the character as authored; the character's size multiplies that, as it
            // does any other model's height.
            Assert.That(rig.Models.ModelHeightMeters, Is.EqualTo(2.4f).Within(0.02f), "On a character twice the size, a model fitted to 1.2 m should be 2.4 m.");
        }

        private CapsuleCollider Capsule => rig.Character.GetComponent<CapsuleCollider>();

        [UnityTest]
        public IEnumerator ColliderFit_Height_MakesTheColliderAsTallAsTheModel_InProportion()
        {
            rig.Floor();
            rig.SpawnCharacter(new Vector3(0f, 0.05f, 0f));
            yield return rig.WaitForModel();
            yield return new WaitForFixedUpdate();
            float radius = Capsule.radius, height = Capsule.height;
            rig.Models.ColliderFitting = ColliderFit.Height;
            yield return CharacterTestRig.WaitFor(rig.Models.LoadModelAsync(FittedMannequin(1.2f)));

            Assert.That(Capsule.height, Is.EqualTo(1.2f).Within(0.01f), "The collider should be as tall as the model.");
            Assert.That(Capsule.radius, Is.EqualTo(radius * 1.2f / height).Within(0.005f), "Its width should keep its proportions.");
            Assert.That(Capsule.center.y, Is.EqualTo(Capsule.height / 2f).Within(1e-4f), "It should stand on the character's feet.");
            yield return rig.Run(0.5f, _ => CharacterTestRig.Idle());
            Assert.That(rig.Grounded, Is.True);
            Assert.That(rig.Position.y, Is.EqualTo(0f).Within(0.005f));

            // The character's size still multiplies it.
            rig.Controller.SetScale(Vector3.one * 2f, immediately: true);
            Assert.That(Capsule.height, Is.EqualTo(2.4f).Within(0.02f));
        }

        [UnityTest]
        public IEnumerator ColliderFit_HeightAndWidth_MakesTheColliderAsWideAsTheModelsShoulders()
        {
            rig.Floor();
            rig.SpawnCharacter(new Vector3(0f, 0.05f, 0f));
            yield return rig.WaitForModel();
            yield return new WaitForFixedUpdate();
            rig.Models.ColliderFitting = ColliderFit.HeightAndWidth;
            yield return CharacterTestRig.WaitFor(rig.Models.LoadModelAsync(rig.Models.ModelSource));

            Animator animator = rig.Models.Animator;
            float shoulders = Vector3.Distance(animator.GetBoneTransform(HumanBodyBones.LeftUpperArm).position,
                animator.GetBoneTransform(HumanBodyBones.RightUpperArm).position);
            Assert.That(Capsule.height, Is.EqualTo(rig.Models.ModelHeightMeters).Within(0.01f));
            Assert.That(Capsule.radius, Is.EqualTo(0.5f * shoulders * rig.Models.ColliderWidthMargin).Within(0.01f),
                "The collider should be as wide as the shoulders, times the margin.");
            Assert.That(Capsule.radius, Is.InRange(0.15f, 0.4f), "A human's shoulders, with room for the arms.");
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
            yield return CharacterTestRig.WaitFor(rig.Models.LoadModelAsync(FittedMannequin(1.2f)));
            yield return rig.Run(0.3f, _ => CharacterTestRig.Idle());
            Assert.That(Capsule.height, Is.EqualTo(1.2f).Within(0.01f), "Test setup: the shorter collider should fit.");

            yield return CharacterTestRig.WaitFor(rig.Models.LoadModelAsync(FittedMannequin(1.8f)));
            yield return rig.Run(0.3f, _ => CharacterTestRig.Idle());
            Assert.That(rig.Models.IsFittingCollider, Is.True, "Under the ceiling, it should wait to grow.");
            Assert.That(Capsule.height, Is.EqualTo(1.2f).Within(0.01f), "It shouldn't grow into the ceiling.");
            Assert.That(rig.Position.y, Is.EqualTo(0f).Within(0.005f), "It shouldn't be pushed through the floor.");

            yield return rig.Run(1.5f, _ => CharacterTestRig.Move(Vector3.forward));
            Assert.That(rig.Position.z, Is.GreaterThan(3f), "Test setup: it should have walked out from under the ceiling.");
            Assert.That(rig.Models.IsFittingCollider, Is.False);
            Assert.That(Capsule.height, Is.EqualTo(1.8f).Within(0.01f), "Out in the open, it should grow to fit the model.");
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
            controller.AddParameter("Land", AnimatorControllerParameterType.Trigger);
            // No VerticalSpeed or Dashing parameters: the translator should skip them without complaint.

            var model = Track(new GameObject("Translator Test Model"));
            var animator = model.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;

            var translator = Track(ScriptableObject.CreateInstance<AnimatorParameterTranslator>());
            var serialized = new UnityEditor.SerializedObject(translator);
            serialized.FindProperty("speedDampTime").floatValue = 0f;
            UnityEditor.SerializedProperty triggers = serialized.FindProperty("eventTriggers");
            triggers.arraySize = 1;
            triggers.GetArrayElementAtIndex(0).FindPropertyRelative("events").intValue = (int)MovementEvents.Landed;
            triggers.GetArrayElementAtIndex(0).FindPropertyRelative("trigger").stringValue = "Land";
            serialized.ApplyModifiedPropertiesWithoutUndo();
            yield return null;

            ICharacterAnimationDriver driver = translator.CreateDriver(model, animator);
            Assert.That(driver, Is.Not.Null);
            var state = new CharacterAnimationState { horizontalSpeed = 4f, grounded = true, verticalSpeed = 2f, dashing = true };
            driver.UpdateAnimation(state, 0.02f);
            Assert.That(animator.GetFloat(AnimatorParameterTranslator.DefaultSpeedParameter), Is.EqualTo(4f).Within(1e-4f));
            Assert.That(animator.GetBool(AnimatorParameterTranslator.DefaultGroundedParameter), Is.True);

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
