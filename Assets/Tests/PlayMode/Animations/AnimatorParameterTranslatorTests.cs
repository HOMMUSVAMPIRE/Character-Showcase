using System.Collections;
using Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CharacterShowcase.Tests.Animations
{
    /// <summary>
    /// Tests of an Animator Parameter Translator's bool parameters, set in its inspector: one showing a character's state
    /// (State: …, an ability's own, with no code) is set while the state is; one showing the animation state's own bool is
    /// set from it; one whose source is unknown, or whose parameter the controller doesn't have, is skipped (the first with a
    /// warning); and a new translator's defaults all name the mannequin's parameters and sources that exist. Each drives an
    /// Animator of its own with the character model's Animator Controller (the mannequin's), so the character's own
    /// animation doesn't set them too.
    /// </summary>
    [Category(TestArea.Animation)]
    public class AnimatorParameterTranslatorTests : CharacterRigFixture
    {
        protected override bool MovementOnly => false;

        private Animator animator;

        /// <summary>An Animator of its own, playing the character model's controller, given a frame to start.</summary>
        private IEnumerator AnimatorWithTheModelsController()
        {
            yield return rig.SpawnSettled(Vector3.zero, settleSeconds: 0f);
            RuntimeAnimatorController controller = rig.Models.Animator != null ? rig.Models.Animator.runtimeAnimatorController : null;
            Assume.That(controller, Is.Not.Null, "Test setup: the character's model should have an Animator Controller.");
            var holder = rig.Track(new GameObject("Translator Test Animator"));
            animator = holder.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            yield return null;
        }

        private ICharacterAnimationDriver Driver(params AnimatorParameterTranslator.BoolParameter[] bools)
        {
            var translator = rig.Track(ScriptableObject.CreateInstance<AnimatorParameterTranslator>());
            translator.BoolParameters = bools;
            return translator.CreateDriver(animator.gameObject, animator);
        }

        [UnityTest]
        public IEnumerator ABoolShowingACharactersState_IsSetWhileTheStateIs()
        {
            yield return AnimatorWithTheModelsController();
            ICharacterAnimationDriver driver = Driver(
                new AnimatorParameterTranslator.BoolParameter(AnimatorParameterTranslator.SourceOf(CharacterState.WallRunning), AnimatorParameterTranslator.DefaultWallRunningParameter));

            driver.UpdateAnimation(new CharacterAnimationState { state = CharacterState.WallRunning | CharacterState.Moving }, 0.02f);
            Assert.That(animator.GetBool(AnimatorParameterTranslator.DefaultWallRunningParameter), Is.True, "Wall running, its bool should be set.");
            driver.UpdateAnimation(new CharacterAnimationState { state = CharacterState.Grounded }, 0.02f);
            Assert.That(animator.GetBool(AnimatorParameterTranslator.DefaultWallRunningParameter), Is.False, "No longer wall running, its bool should be cleared.");
        }

        [UnityTest]
        public IEnumerator ABoolShowingTheAnimationStatesOwn_IsSetFromIt()
        {
            yield return AnimatorWithTheModelsController();
            // The animation's Swimming, from the character's Diving: what it shows is the source's, whatever the parameter's name.
            ICharacterAnimationDriver driver = Driver(
                new AnimatorParameterTranslator.BoolParameter("Grounded", AnimatorParameterTranslator.DefaultGroundedParameter),
                new AnimatorParameterTranslator.BoolParameter(AnimatorParameterTranslator.SourceOf(CharacterState.Diving), AnimatorParameterTranslator.DefaultSwimmingParameter));

            driver.UpdateAnimation(new CharacterAnimationState { grounded = true, state = CharacterState.Diving }, 0.02f);
            Assert.That(animator.GetBool(AnimatorParameterTranslator.DefaultGroundedParameter), Is.True);
            Assert.That(animator.GetBool(AnimatorParameterTranslator.DefaultSwimmingParameter), Is.True);
            driver.UpdateAnimation(new CharacterAnimationState { grounded = false, state = CharacterState.Grounded }, 0.02f);
            Assert.That(animator.GetBool(AnimatorParameterTranslator.DefaultGroundedParameter), Is.False,
                "It shows the animation state's grounded, not the character's Grounded state.");
            Assert.That(animator.GetBool(AnimatorParameterTranslator.DefaultSwimmingParameter), Is.False);
        }

        [UnityTest]
        public IEnumerator ABoolWithAnUnknownSource_OrAParameterTheControllerLacks_IsSkipped()
        {
            yield return AnimatorWithTheModelsController();
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("'No Such Value'"));
            ICharacterAnimationDriver driver = Driver(
                new AnimatorParameterTranslator.BoolParameter("No Such Value", AnimatorParameterTranslator.DefaultDashingParameter),
                new AnimatorParameterTranslator.BoolParameter(AnimatorParameterTranslator.SourceOf(CharacterState.Dashing), "NoSuchParameter"));
            Assert.DoesNotThrow(() => driver.UpdateAnimation(new CharacterAnimationState { state = CharacterState.Dashing, dashing = true }, 0.02f));
            Assert.That(animator.GetBool(AnimatorParameterTranslator.DefaultDashingParameter), Is.False, "An unknown source shouldn't set its parameter.");
        }

        [UnityTest]
        public IEnumerator AFloatShowingAnAbilityValue_IsSetFromIt_AndLeftAsItIsUntilItsSet()
        {
            yield return AnimatorWithTheModelsController();
            var translator = rig.Track(ScriptableObject.CreateInstance<AnimatorParameterTranslator>());
            translator.BoolParameters = new AnimatorParameterTranslator.BoolParameter[0];
            translator.PlaybackParameters = new AnimatorParameterTranslator.PlaybackParameter[0];
            translator.FloatParameters = new[]
            {
                new AnimatorParameterTranslator.FloatParameter(AnimatorParameterTranslator.AbilitySourceOf("Test Tilt"), AnimatorParameterTranslator.DefaultRollProgressParameter),
                new AnimatorParameterTranslator.FloatParameter("Wall Side", AnimatorParameterTranslator.DefaultWallSideParameter),
            };
            ICharacterAnimationDriver driver = translator.CreateDriver(animator.gameObject, animator);
            var values = new AbilityValues();
            animator.SetFloat(AnimatorParameterTranslator.DefaultRollProgressParameter, 0.25f);
            driver.UpdateAnimation(new CharacterAnimationState { abilityValues = values }, 0.02f);
            Assert.That(animator.GetFloat(AnimatorParameterTranslator.DefaultRollProgressParameter), Is.EqualTo(0.25f),
                "An ability value not set yet leaves its parameter as it is.");

            values.Set(new AbilityValueId("Test Tilt"), 0.75f);
            driver.UpdateAnimation(new CharacterAnimationState { abilityValues = values, wallRunning = true, wallSide = -1f }, 0.02f);
            Assert.That(animator.GetFloat(AnimatorParameterTranslator.DefaultRollProgressParameter), Is.EqualTo(0.75f), "Set, it shows the value.");
            Assert.That(animator.GetFloat(AnimatorParameterTranslator.DefaultWallSideParameter), Is.EqualTo(-1f));
            driver.UpdateAnimation(new CharacterAnimationState { abilityValues = values }, 0.02f);
            Assert.That(animator.GetFloat(AnimatorParameterTranslator.DefaultWallSideParameter), Is.EqualTo(-1f),
                "Out of a wall run, Wall Side keeps the side it was (its source shows nothing).");
        }

        [UnityTest]
        public IEnumerator ANewTranslatorsBools_NameTheMannequinsParameters_AndSourcesThatExist()
        {
            yield return AnimatorWithTheModelsController();
            var parameters = new System.Collections.Generic.HashSet<string>();
            foreach (AnimatorControllerParameter parameter in animator.parameters)
            {
                if (parameter.type == AnimatorControllerParameterType.Bool) { parameters.Add(parameter.name); }
            }
            foreach (AnimatorParameterTranslator.BoolParameter entry in AnimatorParameterTranslator.DefaultBoolParameters())
            {
                Assert.That(AnimatorParameterTranslator.TryResolve(entry.source, out _, out _), Is.True, $"The default source '{entry.source}' should exist.");
                Assert.That(parameters, Does.Contain(entry.parameter), $"The mannequin's controller should have the default bool '{entry.parameter}'.");
            }
        }
    }
}
