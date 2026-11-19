using System.Linq;
using MeridianMotion.Characters;
using MeridianMotion.EditorTools;
using NUnit.Framework;
using UnityEditor.Animations;
using UnityEditor;
using UnityEngine;

namespace MeridianMotion.Tests.EditMode.Animations
{
    /// <summary>
    /// Tests of animation sets (CharacterAnimationSet) and the clip slots they fill: a set's controller is its layout's,
    /// with its clips in the slots it fills and the layout's in the rest (the mannequin's, with an idle of the set's own,
    /// differs from the shared controller in that clip alone); it's made beside the set the first time it's built; and a
    /// slot with no clip of its own plays its fallback slot's.
    /// </summary>
    [Category(TestArea.Animation)]
    [TestTiming]
    public class AnimationSetTests
    {
        private const string Folder = "Assets/ZzAnimationSetTests";

        [SetUp]
        public void MakeFolder()
        {
            if (!AssetDatabase.IsValidFolder(Folder)) { AssetDatabase.CreateFolder("Assets", "ZzAnimationSetTests"); }
        }

        [TearDown]
        public void RemoveFolder() => AssetDatabase.DeleteAsset(Folder);

        private static AnimationClip MannequinClip(string name) =>
            AssetDatabase.LoadAllAssetsAtPath(HumanoidModelImporter.MannequinModelPath).OfType<AnimationClip>().First(c => c.name == name);

        [Test]
        public void ASetsController_IsItsLayoutsWithItsClips_InTheRolesItFills_AndMadeBesideIt()
        {
            var layout = AssetDatabase.LoadAssetAtPath<AnimatorLayout>(HumanoidModelImporter.LayoutPath);
            Assume.That(layout, Is.Not.Null, "Test setup: the mannequin's layout should be there.");
            var set = ScriptableObject.CreateInstance<CharacterAnimationSet>();
            set.layout = layout;
            set.clips = new[] { new CharacterAnimationSet.SlotClip { slot = "Idle", clip = MannequinClip("Hang") } };
            AssetDatabase.CreateAsset(set, Folder + "/Test Set.asset");

            AnimatorController built = AnimatorControllerBuilder.Build(set);
            Assert.That(built, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(built), Is.EqualTo(Folder + "/Test Set Controller.controller"), "Its controller is made beside it.");
            Assert.That(set.controller, Is.SameAs(built));

            string[] own = AnimatorControllerDescription.Describe(built).Split('\n').Skip(1).ToArray();
            string[] shared = AnimatorControllerDescription.Describe(AssetDatabase.LoadAssetAtPath<AnimatorController>(HumanoidModelImporter.ControllerPath)).Split('\n').Skip(1).ToArray();
            Assert.That(own.Length, Is.EqualTo(shared.Length), "The same states, motions and transitions as the layout's own controller.");
            string[] differing = own.Where((line, i) => line != shared[i]).ToArray();
            Assert.That(differing, Has.Length.EqualTo(1), "Only the slot it fills should differ: " + string.Join(" | ", differing));
            Assert.That(differing[0], Does.Contain("clip Hang"), "Its idle is the set's.");
        }

        [Test]
        public void ASlotWithNoClipOfItsOwn_PlaysItsFallbackSlots()
        {
            var controller = AnimatorController.CreateAnimatorControllerAtPath(Folder + "/Fallback.controller");
            var layout = ScriptableObject.CreateInstance<AnimatorLayout>();
            layout.controller = controller;
            layout.ikPass = false;
            layout.defaultState = "Run";
            layout.clips = new[]
            {
                new ClipSlot { name = "Walk", candidates = new[] { MannequinClip("Walk") } },
                new ClipSlot { name = "Run", candidates = new AnimationClip[0], fallbackSlot = "Walk" },
            };
            layout.states = new[] { new LayoutState { name = "Run", motions = new[] { new LayoutMotion { motion = "Run" } } } };
            try
            {
                Assert.That(AnimatorControllerBuilder.Build(layout), Is.Not.Null);
                AnimatorState run = controller.layers[0].stateMachine.states.Single().state;
                Assert.That(run.motion, Is.SameAs(MannequinClip("Walk")), "With no run of its own, the run plays the walk.");
            }
            finally { Object.DestroyImmediate(layout); }
        }
    }
}
