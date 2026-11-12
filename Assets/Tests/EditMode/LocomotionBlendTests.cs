using System.Collections.Generic;
using System.Linq;
using CharacterShowcase.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace CharacterShowcase.Tests.EditMode
{
    /// <summary>
    /// Checks of the shared Animator Controller's Locomotion blend tree, as MannequinGenerator builds it, played on the
    /// mannequin: every loop is offset to land its left foot at the same moment, so blending between them doesn't mix
    /// one foot's stride with the other's; and each loop shown at the speed its strides are made for (the walk, the
    /// sprint, and the sprint sped up to the top) keeps its planted feet in step with the ground there. In the
    /// SampleContent category, and skipped where the controller (or the mannequin) isn't there.
    /// </summary>
    [Category(PreparedModelTests.SampleContent)]
    public class LocomotionBlendTests
    {
        private static (BlendTree tree, GameObject mannequin) Load()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(CharacterModelImporter.ControllerPath);
            if (controller == null) { Assert.Ignore($"There's no Animator Controller at {CharacterModelImporter.ControllerPath}."); }
            var mannequin = AssetDatabase.LoadAssetAtPath<GameObject>(MannequinGenerator.ModelPrefabPath);
            if (mannequin == null) { Assert.Ignore($"There's no mannequin at {MannequinGenerator.ModelPrefabPath} to play the loops on."); }
            AnimatorState locomotion = controller.layers[0].stateMachine.states.Select(s => s.state).FirstOrDefault(s => s.name == "Locomotion");
            if (locomotion == null || locomotion.motion is not BlendTree tree) { Assert.Ignore("The controller has no Locomotion blend tree."); return default; }
            return (tree, mannequin);
        }

        // The loops: every child but the standing idle at the bottom.
        private static IEnumerable<ChildMotion> Loops(BlendTree tree) => tree.children.Where(c => c.threshold > 0f && c.motion is AnimationClip);

        [Test]
        public void EveryLoop_LandsItsLeftFoot_AtTheSameMoment()
        {
            (BlendTree tree, GameObject mannequin) = Load();
            var problems = new List<string>();
            foreach (ChildMotion child in Loops(tree))
            {
                float touchDown = MotionCaptureLocomotion.TouchDownPhase(mannequin, (AnimationClip)child.motion);
                float off = Mathf.Abs(Mathf.Repeat(touchDown - child.cycleOffset + 0.5f, 1f) - 0.5f);
                if (off > 0.03f)
                {
                    problems.Add($"{child.motion.name} at {child.threshold:0.00} m/s: its left foot lands {touchDown:0.000} of the way through it, " +
                                 $"but it's offset by {child.cycleOffset:0.000}");
                }
            }
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }

        [Test]
        public void LoopsShownAtTheirOwnStrides_KeepTheirFeetInStep()
        {
            (BlendTree tree, GameObject mannequin) = Load();
            var problems = new List<string>();
            ChildMotion[] loops = Loops(tree).ToArray();
            // The walk at its own speed, the sprint at its own speed, and the top of the tree (the sprint sped up).
            IEnumerable<ChildMotion> atOwnStrides = loops.Where(c => Mathf.Approximately(c.timeScale, 1f)).Append(loops.Last());
            foreach (ChildMotion child in atOwnStrides.Distinct())
            {
                float strides = MotionCaptureLocomotion.StrideSpeed(mannequin, (AnimationClip)child.motion) * child.timeScale;
                if (Mathf.Abs(strides - child.threshold) > 0.05f * child.threshold)
                {
                    problems.Add($"{child.motion.name} at {child.threshold:0.00} m/s, played {child.timeScale:0.00} times as fast: its strides cover {strides:0.00} m/s");
                }
            }
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }
    }
}
