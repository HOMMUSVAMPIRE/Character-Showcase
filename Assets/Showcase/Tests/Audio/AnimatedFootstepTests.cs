using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MeridianMotion.Characters;
using MeridianMotion.Showcase.Audio;
using MeridianMotion.Showcase.CharacterAudio;
using MeridianMotion.Tests;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MeridianMotion.Showcase.Tests.Audio
{
    /// <summary>
    /// Tests of footsteps with the character's model (CharacterSounds): a footstep plays as each animated foot comes down,
    /// so walking and running make as many as the animation's footfalls, in step with them, not as many as a distance
    /// would. The Animator is stepped with the movement, as the animation tests step it.
    /// </summary>
    [Category(TestArea.Audio)]
    public class AnimatedFootstepTests : AnimationTestsBase
    {
        private CharacterAudioRig audio;
        private SoundEvent step;

        [SetUp]
        public void MakeAudio() => audio = new CharacterAudioRig();

        [TearDown]
        public void RemoveAudio() => audio.Dispose();

        private IEnumerator WithFootsteps()
        {
            yield return SpawnWithOwnModel();
            TakeOverAnimator();
            yield return rig.Run(CharacterTestRig.StepsFor(0.5f), _ => CharacterTestRig.Idle(), StepAnimator);
            step = audio.Sound();
            audio.set.Footsteps = new FootstepSettings { step = step };
            rig.Character.AddComponent<CharacterSounds>().SoundSet = audio.set;
            audio.played.Clear();
        }

        /// <summary>How many times a foot came down (its height crossing the middle of its range, going down).</summary>
        private static int Footfalls(List<float> heights)
        {
            float middle = (heights.Min() + heights.Max()) / 2f;
            int falls = 0;
            for (int i = 1; i < heights.Count; i++) { if (heights[i - 1] > middle && heights[i] <= middle) { falls++; } }
            return falls;
        }

        private IEnumerator FootstepsMatchTheFeet(System.Func<int, StepInputs> input)
        {
            yield return WithFootsteps();
            yield return rig.Run(CharacterTestRig.StepsFor(1f), input, StepAnimator);
            audio.played.Clear();
            Transform left = animator.GetBoneTransform(HumanBodyBones.LeftFoot), right = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            var lefts = new List<float>();
            var rights = new List<float>();
            yield return rig.Run(CharacterTestRig.StepsFor(3f), input, s =>
            {
                StepAnimator(s);
                lefts.Add(left.position.y - rig.Movement.Feet.y);
                rights.Add(right.position.y - rig.Movement.Feet.y);
            });
            int footfalls = Footfalls(lefts) + Footfalls(rights);
            Assume.That(footfalls, Is.GreaterThan(3), "the feet stepped");
            Assert.That(audio.Count(step), Is.InRange(footfalls - 1, footfalls + 1), $"{footfalls} footfalls in the animation");
        }

        [UnityTest]
        public IEnumerator Walking_AFootstepForEachFootfall() => FootstepsMatchTheFeet(_ => CharacterTestRig.Move(Vector3.forward));

        [UnityTest]
        public IEnumerator Running_AFootstepForEachFootfall() => FootstepsMatchTheFeet(_ => CharacterTestRig.Sprint(Vector3.forward));

        [UnityTest]
        public IEnumerator StandingStill_NoFootsteps()
        {
            yield return WithFootsteps();
            yield return rig.Run(CharacterTestRig.StepsFor(2f), _ => CharacterTestRig.Idle(), StepAnimator);
            Assert.That(audio.Count(step), Is.Zero);
        }
    }
}
