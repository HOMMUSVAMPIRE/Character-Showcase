using System.Collections;
using System.Collections.Generic;
using Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CharacterShowcase.Tests
{
    /// <summary>
    /// Tests of how the character's model is kept separate from the character: it's loaded at runtime from a
    /// CharacterModelDefinition, can be swapped for a different one (even one without an Animator), loading is
    /// cleaned up if the character goes away first, and translators turn animation state and events into Animator
    /// parameters.
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

        /// <summary>A definition whose model is a plain cube: no Animator, no humanoid rig.</summary>
        private CharacterModelDefinition CubeDefinition()
        {
            GameObject template = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            template.name = "Cube Model";
            Object.Destroy(template.GetComponent<Collider>());
            template.SetActive(false); // Only a template to copy; copies are activated below.
            var definition = Track(ScriptableObject.CreateInstance<CharacterModelDefinition>());
            definition.ModelPrefab = template;
            return definition;
        }

        [UnityTest]
        public IEnumerator Model_LoadsAtRuntime_AndIsSetUpForTheCharacter()
        {
            rig.Floor();
            rig.SpawnCharacter(new Vector3(0f, 0.05f, 0f));
            AnimationController animation = rig.Animation;
            int loadedEvents = 0;
            animation.ModelLoaded += _ => loadedEvents++;
            Assert.That(animation.ModelDefinition, Is.Not.Null, "The Character prefab should name a model definition.");

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

            CharacterModelDefinition cube = CubeDefinition();
            yield return animation.LoadModel(cube);
            yield return null; // Destroy takes effect at the end of the frame.

            Assert.That(mannequin == null, Is.True, "The old model should be destroyed.");
            Assert.That(animation.Model, Is.Not.Null);
            Assert.That(animation.Model.name, Does.StartWith("Cube Model"));
            Assert.That(animation.Model.transform.IsChildOf(rig.Character.transform), Is.True);
            Assert.That(animation.ModelDefinition, Is.SameAs(cube));
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
            CharacterModelDefinition cube = CubeDefinition();
            for (int i = 0; i < 5; i++)
            {
                rig.SpawnCharacter(new Vector3(i * 3f, 0f, 0f));
                rig.Animation.LoadModel(cube);
                Object.Destroy(rig.Character);
                yield return null;
            }
            for (int i = 0; i < 10; i++) { yield return null; }

            foreach (GameObject obj in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Assert.That(obj.name.StartsWith("Cube Model(Clone)"), Is.False, "A model finished loading for a character that no longer exists.");
            }
        }

        /// <summary>Stands in for a loader with something to clean up (such as an Addressables handle): it takes a while,
        /// and records whether its cancellation cleanup ran.</summary>
        private class SlowLoadingDefinition : CharacterModelDefinition
        {
            public bool cleanedUp;
            public override IEnumerator LoadModel(ModelLoadRequest request)
            {
                request.OnCancel(() => cleanedUp = true);
                for (int i = 0; i < 30; i++) { yield return null; }
                request.SetResult(new GameObject("Slow Model"));
            }
        }

        [UnityTest]
        public IEnumerator DestroyingTheCharacterWhileItsModelLoads_CancelsTheLoad()
        {
            var slow = Track(ScriptableObject.CreateInstance<SlowLoadingDefinition>());
            rig.SpawnCharacter(Vector3.zero);
            rig.Animation.LoadModel(slow);
            yield return null;
            Assert.That(rig.Animation.IsLoadingModel, Is.True, "Test setup: the model should still be loading.");

            Object.Destroy(rig.Character);
            yield return null;
            Assert.That(slow.cleanedUp, Is.True, "The loader's cleanup should run when its character is destroyed.");
        }

        [UnityTest]
        public IEnumerator LoadingAnotherModelMidLoad_CancelsTheFirst()
        {
            var slow = Track(ScriptableObject.CreateInstance<SlowLoadingDefinition>());
            rig.SpawnCharacter(Vector3.zero);
            rig.Animation.LoadModel(slow);
            yield return null;

            yield return rig.Animation.LoadModel(CubeDefinition());

            Assert.That(slow.cleanedUp, Is.True, "Replacing a model mid-load should cancel the first load.");
            Assert.That(rig.Animation.Model.name, Does.StartWith("Cube Model"));
            for (int i = 0; i < 40; i++) { yield return null; }
            Assert.That(GameObject.Find("Slow Model"), Is.Null, "The cancelled load shouldn't produce a model.");
        }

        [UnityTest]
        public IEnumerator PreviewMeshes_ShowTheModelWhereItLoads()
        {
            rig.Floor();
            rig.SpawnCharacter(new Vector3(0f, 0.05f, 0f));
            yield return rig.WaitForModel();
            yield return null; // Let the skinned mesh update its bounds.

            var previews = new List<CharacterModelDefinition.PreviewMesh>();
            rig.Animation.ModelDefinition.GetPreviewMeshes(previews);
            Assert.That(previews, Is.Not.Empty);

            // The preview should sit on the character's feet and be about as tall as the loaded model.
            Transform parent = rig.Animation.Model.transform.parent;
            float bottom = float.PositiveInfinity, top = float.NegativeInfinity;
            foreach (CharacterModelDefinition.PreviewMesh preview in previews)
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
