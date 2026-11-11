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
    /// Checks of the package's motion-capture clips in Assets/Animation/CMU, run in the editor: each is set up as a
    /// Humanoid animation (Prepare Motion Capture Clips), and played on the mannequin, a clip that moves about stays on
    /// the spot and faces forward, as its root motion has been taken out and its facing comes from the body. Each check
    /// goes through every clip and lists every one that fails it. In the SampleContent category, and skipped where the
    /// clips (or the mannequin) aren't there.
    /// </summary>
    [Category(PreparedModelTests.SampleContent)]
    public class MotionCaptureClipTests
    {
        // Folders whose clips move about on their feet, so should stay on the spot and face forward. Getting up, sitting
        // down, rolling, falling and climbing shift the body over the feet, or turn it, as part of the motion.
        private static readonly string[] OnTheSpotFolders = { "Walk", "Run", "Jump Hop", "Stairs Steps Slopes", "Crouch Sneak Crawl", "Idle", "Carry Push Pull" };

        private static string[] Clips()
        {
            string[] clips = AssetDatabase.IsValidFolder(MotionCaptureClipImporter.ClipsFolder)
                ? MotionCaptureClipImporter.FindClipFiles().ToArray()
                : System.Array.Empty<string>();
            if (clips.Length == 0) { Assert.Ignore($"There are no clips in {MotionCaptureClipImporter.ClipsFolder}."); }
            return clips;
        }

        /// <summary>Runs a check on each clip, and fails listing every clip it failed for, and why.</summary>
        private static void CheckEvery(IEnumerable<string> clips, System.Func<string, string> problemWith)
        {
            var problems = new List<string>();
            int checkedClips = 0;
            foreach (string path in clips)
            {
                checkedClips++;
                string problem = problemWith(path);
                if (problem != null) { problems.Add($"{Path.GetFileNameWithoutExtension(path)}: {problem}"); }
            }
            Assert.That(problems, Is.Empty, $"{problems.Count} of {checkedClips} clips:\n" + string.Join("\n", problems));
        }

        [Test]
        public void EveryClip_IsAHumanoidAnimation_FromAfterItsTPoseFrame()
        {
            var bonesByName = new HashSet<(string, string)>(MotionCaptureClipImporter.BoneMap.Select(m => (m.Key, HumanTrait.BoneName[(int)m.Value])));
            CheckEvery(Clips(), path =>
            {
                var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                if (importer.animationType != ModelImporterAnimationType.Human) { return "not Humanoid (run Tools > Character Showcase > Prepare Motion Capture Clips)"; }
                Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
                if (avatar == null || !avatar.isValid || !avatar.isHuman) { return "no valid Humanoid avatar"; }
                HumanBone[] human = importer.humanDescription.human;
                if (human.Select(h => h.boneName).Distinct().Count() != human.Length) { return "a bone mapped twice"; }
                if (!human.All(h => bonesByName.Contains((h.boneName, h.humanName)))) { return "bones not mapped by the CMU skeleton's names"; }
                ModelImporterClipAnimation clip = importer.clipAnimations.FirstOrDefault();
                ModelImporterClipAnimation take = importer.defaultClipAnimations.FirstOrDefault();
                if (clip == null || take == null) { return "no clip"; }
                if (clip.name != Path.GetFileNameWithoutExtension(path)) { return $"its clip is named '{clip.name}', not for its file"; }
                if (!Mathf.Approximately(clip.firstFrame, take.firstFrame + 1f)) { return "it doesn't start after the T-pose frame"; }
                AnimationClip imported = MotionCaptureClipImporter.WholeClip(path);
                if (imported == null || !imported.humanMotion) { return "it doesn't import as a Humanoid clip"; }
                return null;
            });
        }

        /// <summary>
        /// Played on the mannequin from start to end, the hips stay within a little of the spot (their sway within each
        /// step, scaled to the mannequin), and on average the body faces forward.
        /// </summary>
        [Test]
        public void ClipsThatMoveAbout_StayOnTheSpot_AndFaceForward_OnTheMannequin()
        {
            string[] clips = Clips().Where(path => OnTheSpotFolders.Contains(Path.GetFileName(Path.GetDirectoryName(path)))).ToArray();
            var mannequin = AssetDatabase.LoadAssetAtPath<GameObject>(MannequinGenerator.ModelPrefabPath);
            if (mannequin == null) { Assert.Ignore($"There's no mannequin at {MannequinGenerator.ModelPrefabPath} to play them on."); }
            CheckEvery(clips, path =>
            {
                AnimationClip clip = MotionCaptureClipImporter.WholeClip(path);
                if (clip == null) { return "no clip"; }
                GameObject model = Object.Instantiate(mannequin);
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
                    if (furthest >= 0.35f) { return $"the hips get {furthest * 100f:0} cm from the spot"; }
                    if (Mathf.Abs(turned) >= 30f) { return $"the body faces {turned:0} degrees from forward on average"; }
                    return null;
                }
                finally
                {
                    graph.Destroy();
                    Object.DestroyImmediate(model);
                }
            });
        }
    }
}
