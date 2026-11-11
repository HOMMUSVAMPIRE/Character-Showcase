using System.Collections.Generic;
using System.IO;
using System.Linq;
using CharacterShowcase.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace CharacterShowcase.Tests.EditMode
{
    /// <summary>
    /// Checks of the motion-capture clips in Assets/Animation/CMU, run in the editor: each is set up as a Humanoid
    /// animation (Prepare Motion Capture Clips), and played on the mannequin, a clip that moves about stays on the spot
    /// and faces forward, as its root motion has been taken out and its facing comes from the body.
    /// </summary>
    public class MotionCaptureClipTests
    {
        private const string MannequinPrefab = "Assets/Models/CharacterModels/Mannequin/Mannequin Model.prefab";
        // Folders whose clips move about on their feet, so should stay on the spot and face forward. Getting up, sitting
        // down, rolling, falling and climbing shift the body over the feet, or turn it, as part of the motion.
        private static readonly string[] OnTheSpotFolders = { "Walk", "Run", "Jump Hop", "Stairs Steps Slopes", "Crouch Sneak Crawl", "Idle", "Carry Push Pull" };

        private static IEnumerable<string> Clips() => MotionCaptureClipImporter.FindClipFiles().ToArray();

        private static IEnumerable<string> OnTheSpotClips() =>
            Clips().Where(path => OnTheSpotFolders.Contains(Path.GetFileName(Path.GetDirectoryName(path)))).ToArray();

        [Test]
        public void ThereAreClips() => Assert.That(Clips(), Is.Not.Empty, $"Test setup: no clips in {MotionCaptureClipImporter.ClipsFolder}.");

        [TestCaseSource(nameof(Clips))]
        public void EveryClip_IsAHumanoidAnimation_FromAfterItsTPoseFrame(string path)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            Assert.That(importer.animationType, Is.EqualTo(ModelImporterAnimationType.Human), "Run Tools > Character Showcase > Prepare Motion Capture Clips.");
            Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
            Assert.That(avatar != null && avatar.isValid && avatar.isHuman, Is.True, "It should have a valid Humanoid avatar.");
            HumanBone[] human = importer.humanDescription.human;
            Assert.That(human.Select(h => h.boneName), Is.EquivalentTo(human.Select(h => h.boneName).Distinct()));
            Assert.That(human.Select(h => (h.boneName, h.humanName)),
                Is.SubsetOf(MotionCaptureClipImporter.BoneMap.Select(m => (m.Key, HumanTrait.BoneName[(int)m.Value]))),
                "Its bones should be mapped by the CMU skeleton's names.");

            ModelImporterClipAnimation clip = importer.clipAnimations.Single();
            ModelImporterClipAnimation take = importer.defaultClipAnimations.First();
            Assert.That(clip.name, Is.EqualTo(Path.GetFileNameWithoutExtension(path)));
            Assert.That(clip.firstFrame, Is.EqualTo(take.firstFrame + 1f), "It should start after the T-pose frame.");
            AnimationClip imported = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview__"));
            Assert.That(imported.humanMotion, Is.True);
        }

        /// <summary>
        /// Played on the mannequin from start to end, the hips stay within a little of the spot (their sway within each
        /// step, scaled to the mannequin), and on average the body faces forward.
        /// </summary>
        [TestCaseSource(nameof(OnTheSpotClips))]
        public void ClipsThatMoveAbout_StayOnTheSpot_AndFaceForward_OnTheMannequin(string path)
        {
            AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview__"));
            GameObject model = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(MannequinPrefab));
            PlayableGraph graph = PlayableGraph.Create("Motion Capture Clip Test");
            try
            {
                model.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                Animator animator = model.GetComponentInChildren<Animator>();
                animator.runtimeAnimatorController = null;
                animator.applyRootMotion = false;
                var playable = AnimationClipPlayable.Create(graph, clip);
                AnimationPlayableOutput.Create(graph, "Clip", animator).SetSourcePlayable(playable);
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                Transform leftLeg = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg), rightLeg = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);

                float furthest = 0f;
                Vector3 facing = Vector3.zero;
                const int samples = 60;
                for (int i = 0; i <= samples; i++)
                {
                    playable.SetTime(clip.length * i / samples);
                    graph.Evaluate();
                    furthest = Mathf.Max(furthest, new Vector2(hips.position.x, hips.position.z).magnitude);
                    // The body's forward: square to the line from its right hip joint to its left, along the ground.
                    Vector3 toLeft = Vector3.ProjectOnPlane(leftLeg.position - rightLeg.position, Vector3.up);
                    facing += Vector3.Cross(Vector3.up, toLeft).normalized;
                }
                float turned = Vector3.SignedAngle(Vector3.forward, Vector3.ProjectOnPlane(facing, Vector3.up), Vector3.up);
                TestContext.WriteLine($"Hips at most {furthest * 100f:0} cm from the spot; facing {turned:0} degrees from forward on average.");
                Assert.That(furthest, Is.LessThan(0.35f), "The hips should stay on the spot.");
                Assert.That(Mathf.Abs(turned), Is.LessThan(30f), "The body should face forward.");
            }
            finally
            {
                graph.Destroy();
                Object.DestroyImmediate(model);
            }
        }
    }
}
