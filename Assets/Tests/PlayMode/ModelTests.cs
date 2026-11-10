using System.Collections;
using System.Collections.Generic;
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
            AnimationController animation = rig.Animation;
            int loadedEvents = 0;
            animation.ModelLoaded += _ => loadedEvents++;
            Assert.That(animation.ModelSource, Is.Not.Null, "The Character prefab should name a model source.");

            yield return rig.WaitForModel();

            GameObject model = animation.Model;
            Assert.That(loadedEvents, Is.EqualTo(1), "ModelLoaded should be raised once.");
            Assert.That(model.transform.parent, Is.Not.Null);
            Assert.That(model.transform.IsChildOf(rig.Character.transform), Is.True, "The model should be created on the character.");
            Assert.That(model.layer, Is.EqualTo(rig.Character.layer), "The model should be on the character's layer.");
            Assert.That(animation.Animator, Is.Not.Null);
            Assert.That(animation.Animator.runtimeAnimatorController, Is.Not.Null);
            Assert.That(animation.Animator.isHuman, Is.True);
            Assert.That(animation.Animator.applyRootMotion, Is.False);
            Assert.That(animation.IK, Is.Not.Null, "The model should have CharacterIK.");
            Assert.That(animation.Animator.GetComponent<FootPlacementIK>(), Is.Not.Null, "The model should have foot placement.");
        }

        [UnityTest]
        public IEnumerator SwappingTheModel_ReplacesTheOldOne_AndWorksWithoutAnAnimator()
        {
            rig.Floor();
            rig.SpawnCharacter(new Vector3(0f, 0.05f, 0f));
            AnimationController animation = rig.Animation;
            yield return rig.WaitForModel();
            GameObject mannequin = animation.Model;

            DirectCharacterModelSource cube = CubeSource();
            bool loaded = false;
            yield return CharacterTestRig.WaitFor(animation.LoadModelAsync(cube), l => loaded = l);
            yield return null; // Destroy takes effect at the end of the frame.

            Assert.That(loaded, Is.True);

            Assert.That(mannequin == null, Is.True, "The old model should be destroyed.");
            Assert.That(animation.Model, Is.Not.Null);
            Assert.That(animation.Model.name, Does.StartWith("Cube Model"));
            Assert.That(animation.Model.transform.IsChildOf(rig.Character.transform), Is.True);
            Assert.That(animation.ModelSource, Is.SameAs(cube));
            Assert.That(animation.Animator, Is.Null);
            Assert.That(animation.IK, Is.Null);

            // The character carries on as normal without an Animator.
            animation.Model.SetActive(true);
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
                _ = rig.Animation.LoadModelAsync(cube);
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
            _ = rig.Animation.LoadModelAsync(slow);
            yield return null;
            Assert.That(rig.Animation.IsLoadingModel, Is.True, "Test setup: the model should still be loading.");

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
            IEnumerator first = CharacterTestRig.WaitFor(rig.Animation.LoadModelAsync(slow), l => slowLoaded = l);
            yield return null;

            yield return CharacterTestRig.WaitFor(rig.Animation.LoadModelAsync(CubeSource()));
            yield return first;

            Assert.That(slow.cleanedUp, Is.True, "Replacing a model mid-load should cancel the first load.");
            Assert.That(slowLoaded, Is.False, "The first load should report that it didn't load.");
            Assert.That(rig.Animation.Model.name, Does.StartWith("Cube Model"));
            for (int i = 0; i < 40; i++) { yield return null; }
            Assert.That(GameObject.Find("Slow Model"), Is.Null, "The cancelled load shouldn't produce a model.");
        }

        [UnityTest]
        public IEnumerator SourcesReleaseTheModelsTheyMade_WhenReplacedOrTheCharacterGoes()
        {
            var slow = Track(ScriptableObject.CreateInstance<SlowSource>());
            rig.SpawnCharacter(Vector3.zero);
            bool loaded = false;
            yield return CharacterTestRig.WaitFor(rig.Animation.LoadModelAsync(slow), l => loaded = l);
            Assert.That(loaded, Is.True);
            Assert.That(rig.Animation.Model.name, Is.EqualTo("Slow Model"));
            Assert.That(rig.Animation.Model.transform.IsChildOf(rig.Character.transform), Is.True, "SetUpModelAsync should place it on the character.");

            yield return CharacterTestRig.WaitFor(rig.Animation.LoadModelAsync(CubeSource()));
            Assert.That(slow.released, Is.EqualTo(1), "The replaced model should go back to the source that made it.");

            yield return CharacterTestRig.WaitFor(rig.Animation.LoadModelAsync(slow));
            Object.Destroy(rig.Character);
            yield return null;
            Assert.That(slow.released, Is.EqualTo(2), "A destroyed character's model should go back to its source too.");
        }

        [UnityTest]
        public IEnumerator PreviewMeshes_ShowTheModelWhereItLoads()
        {
            rig.Floor();
            rig.SpawnCharacter(new Vector3(0f, 0.05f, 0f));
            yield return rig.WaitForModel();
            yield return null; // Let the skinned mesh update its bounds.

            var previews = new List<CharacterModelSource.PreviewMesh>();
            rig.Animation.ModelSource.GetPreviewMeshes(previews);
            Assert.That(previews, Is.Not.Empty);

            // The preview should sit on the character's feet and be about as tall as the loaded model.
            Transform parent = rig.Animation.Model.transform.parent;
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
            float modelHeight = rig.Animation.Model.GetComponentInChildren<Renderer>().bounds.size.y;
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
