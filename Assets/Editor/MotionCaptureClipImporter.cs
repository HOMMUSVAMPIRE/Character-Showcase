using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CharacterShowcase.EditorTools
{
    /// <summary>
    /// Sets up the motion-capture clips in Assets/Animation/CMU as Humanoid animations, so they play on any humanoid
    /// model. Each clip gets its own avatar, as each was captured from a performer of their own proportions, mapped by
    /// the CMU skeleton's bone names (not left to Unity's guesswork, which can take a buttock for a thigh) and with the
    /// T-pose the file rests in as its reference pose (see Tools/Animation/tpose_rest.py). The clip starts after its
    /// first frame, which is that T-pose rather than part of the motion. Its root motion has already been taken out (see
    /// Tools/Animation/remove_root_motion.py); the height and sway that are left are baked into the pose, and its facing
    /// is taken from the body, so every clip faces the way the character does, whichever way it was captured.
    /// </summary>
    public static class MotionCaptureClipImporter
    {
        public const string ClipsFolder = "Assets/Animation/CMU";

        /// <summary>The CMU skeleton's bones, by the Humanoid bone each is. Its buttock bones (between hip and thigh) aren't any.</summary>
        public static readonly IReadOnlyDictionary<string, HumanBodyBones> BoneMap = new Dictionary<string, HumanBodyBones>
        {
            ["hip"] = HumanBodyBones.Hips,
            ["abdomen"] = HumanBodyBones.Spine,
            ["chest"] = HumanBodyBones.Chest,
            ["neck"] = HumanBodyBones.Neck,
            ["head"] = HumanBodyBones.Head,
            ["leftEye"] = HumanBodyBones.LeftEye,
            ["rightEye"] = HumanBodyBones.RightEye,
            ["lCollar"] = HumanBodyBones.LeftShoulder,
            ["lShldr"] = HumanBodyBones.LeftUpperArm,
            ["lForeArm"] = HumanBodyBones.LeftLowerArm,
            ["lHand"] = HumanBodyBones.LeftHand,
            ["rCollar"] = HumanBodyBones.RightShoulder,
            ["rShldr"] = HumanBodyBones.RightUpperArm,
            ["rForeArm"] = HumanBodyBones.RightLowerArm,
            ["rHand"] = HumanBodyBones.RightHand,
            ["lThigh"] = HumanBodyBones.LeftUpperLeg,
            ["lShin"] = HumanBodyBones.LeftLowerLeg,
            ["lFoot"] = HumanBodyBones.LeftFoot,
            ["rThigh"] = HumanBodyBones.RightUpperLeg,
            ["rShin"] = HumanBodyBones.RightLowerLeg,
            ["rFoot"] = HumanBodyBones.RightFoot,
            ["lThumb1"] = HumanBodyBones.LeftThumbProximal,
            ["lThumb2"] = HumanBodyBones.LeftThumbIntermediate,
            ["lIndex1"] = HumanBodyBones.LeftIndexProximal,
            ["lIndex2"] = HumanBodyBones.LeftIndexIntermediate,
            ["lMid1"] = HumanBodyBones.LeftMiddleProximal,
            ["lMid2"] = HumanBodyBones.LeftMiddleIntermediate,
            ["lRing1"] = HumanBodyBones.LeftRingProximal,
            ["lRing2"] = HumanBodyBones.LeftRingIntermediate,
            ["lPinky1"] = HumanBodyBones.LeftLittleProximal,
            ["lPinky2"] = HumanBodyBones.LeftLittleIntermediate,
            ["rThumb1"] = HumanBodyBones.RightThumbProximal,
            ["rThumb2"] = HumanBodyBones.RightThumbIntermediate,
            ["rIndex1"] = HumanBodyBones.RightIndexProximal,
            ["rIndex2"] = HumanBodyBones.RightIndexIntermediate,
            ["rMid1"] = HumanBodyBones.RightMiddleProximal,
            ["rMid2"] = HumanBodyBones.RightMiddleIntermediate,
            ["rRing1"] = HumanBodyBones.RightRingProximal,
            ["rRing2"] = HumanBodyBones.RightRingIntermediate,
            ["rPinky1"] = HumanBodyBones.RightLittleProximal,
            ["rPinky2"] = HumanBodyBones.RightLittleIntermediate,
        };

        [MenuItem("Tools/Character Showcase/Prepare Motion Capture Clips", priority = 4)]
        public static void PrepareAllFromMenu()
        {
            List<string> problems = PrepareAll();
            if (problems.Count == 0) { Debug.Log($"[{nameof(MotionCaptureClipImporter)}] Every clip in {ClipsFolder} is set up as a Humanoid animation."); }
        }

        /// <summary>For -executeMethod. Exits with code 1 if any clip couldn't be set up.</summary>
        public static void PrepareAllFromCommandLine()
        {
            if (PrepareAll().Count > 0) { EditorApplication.Exit(1); }
        }

        public static IEnumerable<string> FindClipFiles() =>
            AssetDatabase.FindAssets("t:Model", new[] { ClipsFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
                .Distinct()
                .OrderBy(path => path, System.StringComparer.Ordinal);

        /// <summary>Sets up every clip that isn't already, and returns what went wrong, a line per clip.</summary>
        public static List<string> PrepareAll()
        {
            var problems = new List<string>();
            List<string> paths = FindClipFiles().ToList();
            for (int i = 0; i < paths.Count; i++)
            {
                if (EditorUtility.DisplayCancelableProgressBar("Preparing motion capture clips", Path.GetFileName(paths[i]), (float)i / paths.Count)) { break; }
                string problem = Prepare(paths[i]);
                if (problem != null)
                {
                    problems.Add($"{paths[i]}: {problem}");
                    Debug.LogWarning($"[{nameof(MotionCaptureClipImporter)}] {paths[i]}: {problem}");
                }
            }
            EditorUtility.ClearProgressBar();
            return problems;
        }

        /// <summary>Sets up one clip as a Humanoid animation if it isn't already. Returns what went wrong, or null.</summary>
        public static string Prepare(string path)
        {
            if (AssetImporter.GetAtPath(path) is not ModelImporter importer) { return "It isn't a model file."; }
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (model == null) { return "It didn't import."; }

            bool changed = false;
            void Set<T>(T current, T wanted, System.Action<T> apply)
            {
                if (!EqualityComparer<T>.Default.Equals(current, wanted)) { apply(wanted); changed = true; }
            }
            Set(importer.importAnimation, true, v => importer.importAnimation = v);
            Set(importer.importCameras, false, v => importer.importCameras = v);
            Set(importer.importLights, false, v => importer.importLights = v);
            Set(importer.materialImportMode, ModelImporterMaterialImportMode.None, v => importer.materialImportMode = v);
            Set(importer.animationType, ModelImporterAnimationType.Human, v => importer.animationType = v);
            Set(importer.avatarSetup, ModelImporterAvatarSetup.CreateFromThisModel, v => importer.avatarSetup = v);

            if (!HasMapping(importer.humanDescription))
            {
                if (!TryDescribe(model, out HumanDescription description, out string problem)) { return problem; }
                importer.humanDescription = description;
                changed = true;
            }
            // The clip settings need the take's frame range, which the importer only knows once the file has been read
            // with animation on.
            if (changed) { importer.SaveAndReimport(); changed = false; }

            ModelImporterClipAnimation[] clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0) { clips = importer.defaultClipAnimations; }
            if (clips == null || clips.Length == 0) { return "It has no animation."; }
            // One take per file; the first is the one.
            ModelImporterClipAnimation clip = clips[0];
            ModelImporterClipAnimation take = importer.defaultClipAnimations.FirstOrDefault() ?? clip;
            string name = Path.GetFileNameWithoutExtension(path);
            var wanted = new ClipSettings(name, take.firstFrame + 1f, take.lastFrame);
            if (!wanted.Matches(clip))
            {
                wanted.Apply(clip, take);
                importer.clipAnimations = new[] { clip };
                importer.SaveAndReimport();
            }

            Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
            if (avatar == null || !avatar.isValid || !avatar.isHuman) { return "Unity couldn't make a Humanoid avatar from its skeleton."; }
            AnimationClip imported = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                .FirstOrDefault(c => !c.name.StartsWith("__preview__"));
            if (imported == null || !imported.humanMotion) { return "Its animation didn't import as a Humanoid clip."; }
            return null;
        }

        private static bool HasMapping(HumanDescription description) =>
            description.human != null && description.human.Length > 0 && description.skeleton != null && description.skeleton.Length > 0;

        /// <summary>
        /// The clip's avatar: its bones mapped by name, and its skeleton as the file stores it (the T-pose) for the
        /// reference pose. Twist, stretch and spacing at Unity's defaults (as the Rig tab sets them).
        /// </summary>
        public static bool TryDescribe(GameObject model, out HumanDescription description, out string problem)
        {
            description = default;
            Transform[] transforms = model.GetComponentsInChildren<Transform>(true);
            var human = new List<HumanBone>();
            foreach (Transform t in transforms)
            {
                if (!BoneMap.TryGetValue(t.name, out HumanBodyBones bone)) { continue; }
                human.Add(new HumanBone { boneName = t.name, humanName = HumanTrait.BoneName[(int)bone], limit = new HumanLimit { useDefaultValues = true } });
            }
            var mapped = new HashSet<string>(human.Select(h => h.humanName));
            string[] missing = Enumerable.Range(0, HumanTrait.BoneCount)
                .Where(HumanTrait.RequiredBone)
                .Select(i => HumanTrait.BoneName[i])
                .Where(name => !mapped.Contains(name))
                .ToArray();
            if (missing.Length > 0)
            {
                problem = $"Its skeleton is missing bones a Humanoid needs: {string.Join(", ", missing)}.";
                return false;
            }

            description = new HumanDescription
            {
                human = human.ToArray(),
                skeleton = transforms.Select(t => new SkeletonBone
                {
                    name = t.name,
                    position = t.localPosition,
                    rotation = t.localRotation,
                    scale = t.localScale,
                }).ToArray(),
                upperArmTwist = 0.5f,
                lowerArmTwist = 0.5f,
                upperLegTwist = 0.5f,
                lowerLegTwist = 0.5f,
                armStretch = 0.05f,
                legStretch = 0.05f,
                feetSpacing = 0f,
                hasTranslationDoF = false,
            };
            problem = null;
            return true;
        }

        /// <summary>How each clip is set up: named for its file, from after the T-pose frame, not looped, its root as described above.</summary>
        private readonly struct ClipSettings
        {
            private readonly string name;
            private readonly float firstFrame, lastFrame;

            public ClipSettings(string name, float firstFrame, float lastFrame)
            {
                this.name = name;
                this.firstFrame = firstFrame;
                this.lastFrame = lastFrame;
            }

            public bool Matches(ModelImporterClipAnimation clip) =>
                clip.name == name && Mathf.Approximately(clip.firstFrame, firstFrame) && Mathf.Approximately(clip.lastFrame, lastFrame)
                && !clip.loopTime && !clip.lockRootRotation && !clip.keepOriginalOrientation
                && clip.lockRootHeightY && clip.keepOriginalPositionY && !clip.heightFromFeet
                && clip.lockRootPositionXZ && clip.keepOriginalPositionXZ && !clip.mirror;

            public void Apply(ModelImporterClipAnimation clip, ModelImporterClipAnimation take)
            {
                clip.name = name;
                clip.takeName = take.takeName;
                clip.firstFrame = firstFrame;
                clip.lastFrame = lastFrame;
                clip.loopTime = false;
                clip.mirror = false;
                // Facing from the body, so the clip faces the character's forward whichever way it was captured.
                clip.lockRootRotation = false;
                clip.keepOriginalOrientation = false;
                // Height and the sway left within each step (its travel is already gone) stay in the pose.
                clip.lockRootHeightY = true;
                clip.keepOriginalPositionY = true;
                clip.heightFromFeet = false;
                clip.lockRootPositionXZ = true;
                clip.keepOriginalPositionXZ = true;
            }
        }
    }
}
