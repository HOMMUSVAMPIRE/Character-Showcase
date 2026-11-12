using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace CharacterShowcase.EditorTools
{
    /// <summary>
    /// The character's walk, run, rise and fall animations, cut from the motion-capture clips in Assets/Animation/CMU,
    /// for MannequinGenerator to build the shared Animator Controller from. (Standing idle is the mannequin's own: a
    /// performer standing still is never quite still or evenly balanced, which foot placement and the camera notice.)
    ///
    /// Walking and running are made to loop: in the steady middle of each candidate take (away from the
    /// performer setting off and stopping), the pair of frames a cycle apart whose poses, and how the poses are moving,
    /// match best is found, and the loop runs between them. The candidate with the best match is used. Rising and
    /// falling are cut from a standing jump: from leaving the ground to the top, and from the top to touching down.
    /// Each is added to its take's file as a clip of its own (named for its role), which MotionCaptureClipImporter keeps.
    ///
    /// How fast each loop's planted feet move backward, played on a model, is how fast the character must go for them
    /// to stay put, so the Locomotion blend tree blends at those speeds (see StrideSpeed).
    /// </summary>
    public static class MotionCaptureLocomotion
    {
        public const string WalkName = "Walk Cycle";
        public const string RunName = "Run Cycle";
        public const string RiseName = "Jump Rise";
        public const string FallName = "Jump Fall";
        private static readonly string[] Names = { WalkName, RunName, RiseName, FallName };

        // Candidate takes, relative to the clips folder: steady, straight-ahead walking and running, and jumping on the spot.
        private static readonly string[] WalkTakes = { "Walk/Walk (07_01).fbx", "Walk/Walk forward (69_01).fbx", "Walk/Normal walk (137_29).fbx" };
        private static readonly string[] RunTakes = { "Run/Run (09_01).fbx", "Run/Run or jog (35_17).fbx", "Run/Running straight (78_06).fbx" };
        private static readonly string[] JumpTakes = { "Jump Hop/Jump (16_01).fbx", "Jump Hop/Jump (118_01).fbx", "Jump Hop/Forward jump (16_05).fbx" };

        /// <summary>A loop's length range in seconds: one full cycle (two steps) for walking and running.</summary>
        private readonly struct LoopKind
        {
            public readonly float minSeconds, maxSeconds;
            public LoopKind(float min, float max) { minSeconds = min; maxSeconds = max; }
        }
        private static readonly LoopKind WalkLoop = new(0.8f, 1.5f), RunLoop = new(0.5f, 0.95f);

        public sealed class Result
        {
            public AnimationClip walk, run, rise, fall;
            /// <summary>The speeds (m/s) the walk and run loops' strides are made for, on the model they were measured on.</summary>
            public float walkSpeed, runSpeed;
            public readonly List<string> notes = new();
            public string problem;
            public bool Succeeded => problem == null;
        }

        /// <summary>
        /// Cuts the clips (adding them to their takes' files if they aren't there already), and measures the loops'
        /// stride speeds on the given model (an instance of it, posed by the clips alone).
        /// </summary>
        public static Result Prepare(GameObject measuringModel)
        {
            var result = new Result();
            var chosen = new Dictionary<string, string>(); // Clip name -> path of the take it's cut from.

            bool Loop(string name, string[] takes, LoopKind kind)
            {
                (string path, int first, int last, float score) best = (null, 0, 0, float.PositiveInfinity);
                foreach (string take in takes)
                {
                    string path = $"{MotionCaptureClipImporter.ClipsFolder}/{take}";
                    if (!PrepareTake(path, result)) { return false; }
                    if (!TryFindLoop(MotionCaptureClipImporter.WholeClip(path), kind, out int first, out int last, out float score)) { continue; }
                    if (score < best.score) { best = (path, first, last, score); }
                }
                if (best.path == null)
                {
                    result.problem = $"None of the takes for {name} had a steady stretch long enough to loop.";
                    return false;
                }
                AddClip(best.path, name, best.first, best.last, loop: true);
                chosen[name] = best.path;
                result.notes.Add($"{name}: '{Path.GetFileNameWithoutExtension(best.path)}', frames {best.first} to {best.last} " +
                                 $"of the clip ({(best.last - best.first) / Frames(best.path):0.00} s), pose mismatch {best.score:0.0000}");
                return true;
            }

            if (!Loop(WalkName, WalkTakes, WalkLoop) || !Loop(RunName, RunTakes, RunLoop))
            {
                return result;
            }

            // Rise and fall, from the first jump that has a clean flight.
            bool jumped = false;
            foreach (string take in JumpTakes)
            {
                string path = $"{MotionCaptureClipImporter.ClipsFolder}/{take}";
                if (!PrepareTake(path, result)) { return result; }
                if (!TryFindFlight(measuringModel, MotionCaptureClipImporter.WholeClip(path), out int takeOff, out int top, out int landing)) { continue; }
                AddClip(path, RiseName, takeOff, top, loop: false);
                AddClip(path, FallName, top, landing, loop: false);
                chosen[RiseName] = chosen[FallName] = path;
                result.notes.Add($"{RiseName} and {FallName}: '{Path.GetFileNameWithoutExtension(path)}', leaving the ground at frame " +
                                 $"{takeOff}, at the top at {top}, touching down at {landing}");
                jumped = true;
                break;
            }
            if (!jumped)
            {
                result.problem = "None of the jump takes had a clean flight (both feet off the ground) to cut rising and falling from.";
                return result;
            }

            // Each clip only in the file it was cut from: one cut from another take before is taken out.
            foreach (string path in MotionCaptureClipImporter.FindClipFiles())
            {
                RemoveClips(path, Names.Where(n => !chosen.TryGetValue(n, out string from) || from != path));
            }

            AnimationClip Load(string name) =>
                AssetDatabase.LoadAllAssetsAtPath(chosen[name]).OfType<AnimationClip>().FirstOrDefault(c => c.name == name);
            result.walk = Load(WalkName);
            result.run = Load(RunName);
            result.rise = Load(RiseName);
            result.fall = Load(FallName);
            if (new[] { result.walk, result.run, result.rise, result.fall }.Any(c => c == null || !c.humanMotion))
            {
                result.problem = "A clip cut from the motion capture didn't import as a Humanoid clip.";
                return result;
            }
            result.walkSpeed = StrideSpeed(measuringModel, result.walk);
            result.runSpeed = StrideSpeed(measuringModel, result.run);
            if (!(result.walkSpeed > 0.1f) || !(result.runSpeed > result.walkSpeed))
            {
                result.notes.ForEach(n => Debug.Log($"[{nameof(MotionCaptureLocomotion)}] {n}"));
                result.problem = $"The loops' stride speeds don't make sense: walking {result.walkSpeed:0.00} m/s, running {result.runSpeed:0.00} m/s.";
                return result;
            }
            result.notes.Add($"Strides made for {result.walkSpeed:0.00} m/s walking and {result.runSpeed:0.00} m/s running, on '{measuringModel.name}'");
            return result;
        }

        private static bool PrepareTake(string path, Result result)
        {
            string problem = MotionCaptureClipImporter.Prepare(path);
            if (problem == null) { return true; }
            result.problem = $"{path}: {problem}";
            return false;
        }

        private static float Frames(string path) => MotionCaptureClipImporter.WholeClip(path).frameRate;

        // ---- Loops ----

        /// <summary>
        /// Every muscle's value at every frame of a Humanoid clip (fingers left out), and the body's height: the pose,
        /// as Unity plays it on any model.
        /// </summary>
        private static float[][] SamplePose(AnimationClip clip, out int frames)
        {
            var muscles = new HashSet<string>(HumanTrait.MuscleName);
            EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(clip)
                .Where(b => b.type == typeof(Animator) && (muscles.Contains(b.propertyName) || b.propertyName == "RootT.y"))
                .Where(b => !b.propertyName.Contains("Thumb") && !b.propertyName.Contains("Index") && !b.propertyName.Contains("Middle")
                            && !b.propertyName.Contains("Ring") && !b.propertyName.Contains("Little"))
                .ToArray();
            frames = Mathf.FloorToInt(clip.length * clip.frameRate + 1e-3f) + 1;
            var values = new float[bindings.Length][];
            for (int k = 0; k < bindings.Length; k++)
            {
                AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, bindings[k]);
                values[k] = new float[frames];
                for (int i = 0; i < frames; i++) { values[k][i] = curve.Evaluate(i / clip.frameRate); }
            }
            return values;
        }

        /// <summary>
        /// The frames (of the clip) a loop of the given length should run between: in its steady middle, the pair whose
        /// poses match best, counting how the poses are moving too (so a leg swinging forward isn't joined to one
        /// swinging back). Score: the mean squared difference per muscle.
        /// </summary>
        private static bool TryFindLoop(AnimationClip clip, LoopKind kind, out int first, out int last, out float score)
        {
            first = last = 0;
            score = float.PositiveInfinity;
            float[][] pose = SamplePose(clip, out int frames);
            if (pose.Length == 0) { return false; }
            float fps = clip.frameRate;
            int minLength = Mathf.CeilToInt(kind.minSeconds * fps), maxLength = Mathf.FloorToInt(kind.maxSeconds * fps);
            // Away from setting off and stopping.
            int margin = Mathf.Max(Mathf.CeilToInt(0.3f * fps), frames * 15 / 100);
            int from = Mathf.Max(1, margin), to = frames - 2 - margin;
            const float movingWeight = 4f;
            for (int a = from; a <= to - minLength; a++)
            {
                for (int b = a + minLength; b <= Mathf.Min(to, a + maxLength); b++)
                {
                    float d = 0f;
                    for (int k = 0; k < pose.Length; k++)
                    {
                        float[] m = pose[k];
                        float difference = m[a] - m[b];
                        float moving = (m[a + 1] - m[a - 1]) - (m[b + 1] - m[b - 1]);
                        d += difference * difference + movingWeight * moving * moving;
                    }
                    d /= pose.Length;
                    if (d < score) { score = d; first = a; last = b; }
                }
            }
            return first < last;
        }

        // ---- Rise and fall ----

        /// <summary>
        /// A jump's flight, in frames of the clip, played on the model: the longest stretch with both feet off the ground
        /// (each toe more than a few centimetres above the lowest it gets), and the top of it, where the hips are highest.
        /// </summary>
        public static bool TryFindFlight(GameObject model, AnimationClip clip, out int takeOff, out int top, out int landing)
        {
            takeOff = top = landing = 0;
            float fps = clip.frameRate;
            int frames = Mathf.FloorToInt(clip.length * fps + 1e-3f) + 1;
            Vector3[][] p = SampleBones(model, clip, Enumerable.Range(0, frames).Select(i => i / fps).ToArray(),
                HumanBodyBones.LeftToes, HumanBodyBones.RightToes, HumanBodyBones.Hips);
            if (p == null) { return false; }
            const float clearance = 0.04f;
            float leftGround = p[0].Min(v => v.y), rightGround = p[1].Min(v => v.y);
            bool Off(int i) => p[0][i].y > leftGround + clearance && p[1][i].y > rightGround + clearance;
            int bestStart = -1, bestLength = 0;
            for (int i = 0; i < frames; i++)
            {
                if (!Off(i)) { continue; }
                int end = i;
                while (end + 1 < frames && Off(end + 1)) { end++; }
                if (end - i + 1 > bestLength) { bestStart = i; bestLength = end - i + 1; }
                i = end;
            }
            if (bestLength < Mathf.CeilToInt(0.15f * fps)) { return false; }
            takeOff = bestStart;
            landing = bestStart + bestLength - 1;
            top = takeOff;
            for (int i = takeOff; i <= landing; i++) { if (p[2][i].y > p[2][top].y) { top = i; } }
            return top > takeOff && landing > top;
        }

        /// <summary>
        /// Where the given bones are (in the model's space) at each of the times, with the clip played on its own on an
        /// instance of the model. A toe the model doesn't have is taken from its foot. Null if the model has no Animator.
        /// </summary>
        private static Vector3[][] SampleBones(GameObject model, AnimationClip clip, float[] times, params HumanBodyBones[] bones)
        {
            GameObject instance = Object.Instantiate(model);
            PlayableGraph graph = PlayableGraph.Create("Motion Capture Sampling");
            try
            {
                instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                Animator animator = instance.GetComponentInChildren<Animator>();
                if (animator == null) { return null; }
                animator.runtimeAnimatorController = null;
                animator.applyRootMotion = false;
                var playable = AnimationClipPlayable.Create(graph, clip);
                AnimationPlayableOutput.Create(graph, "Clip", animator).SetSourcePlayable(playable);
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                Transform Bone(HumanBodyBones bone)
                {
                    Transform t = animator.GetBoneTransform(bone);
                    if (t != null) { return t; }
                    return bone switch
                    {
                        HumanBodyBones.LeftToes => animator.GetBoneTransform(HumanBodyBones.LeftFoot),
                        HumanBodyBones.RightToes => animator.GetBoneTransform(HumanBodyBones.RightFoot),
                        _ => null,
                    };
                }
                Transform[] transforms = bones.Select(Bone).ToArray();
                if (transforms.Any(t => t == null)) { return null; }
                var result = bones.Select(_ => new Vector3[times.Length]).ToArray();
                for (int i = 0; i < times.Length; i++)
                {
                    playable.SetTime(times[i]);
                    graph.Evaluate();
                    for (int b = 0; b < transforms.Length; b++) { result[b][i] = instance.transform.InverseTransformPoint(transforms[b].position); }
                }
                return result;
            }
            finally
            {
                graph.Destroy();
                Object.DestroyImmediate(instance);
            }
        }

        // ---- Clips in the takes' files ----

        /// <summary>Adds (or updates) a clip cut from frames of a take's whole clip, set up as the take is.</summary>
        private static void AddClip(string path, string name, int first, int last, bool loop)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            List<ModelImporterClipAnimation> clips = importer.clipAnimations.ToList();
            ModelImporterClipAnimation whole = clips[0];
            var clip = new ModelImporterClipAnimation
            {
                name = name,
                takeName = whole.takeName,
                firstFrame = whole.firstFrame + first,
                lastFrame = whole.firstFrame + last,
                loopTime = loop,
                // Unity blends what's left of the mismatch at the join over the loop, so it doesn't pop.
                loopPose = loop,
            };
            MotionCaptureClipImporter.SetRoot(clip);
            int index = clips.FindIndex(c => c.name == name);
            if (index >= 0)
            {
                ModelImporterClipAnimation current = clips[index];
                if (current.takeName == clip.takeName && Mathf.Approximately(current.firstFrame, clip.firstFrame)
                    && Mathf.Approximately(current.lastFrame, clip.lastFrame) && current.loopTime == loop && current.loopPose == loop
                    && MotionCaptureClipImporter.HasRootSetUp(current)) { return; }
                clips[index] = clip;
            }
            else
            {
                clips.Add(clip);
            }
            importer.clipAnimations = clips.ToArray();
            importer.SaveAndReimport();
        }

        private static void RemoveClips(string path, IEnumerable<string> names)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            var unwanted = new HashSet<string>(names);
            ModelImporterClipAnimation[] clips = importer.clipAnimations;
            if (clips.Length == 0 || !clips.Skip(1).Any(c => unwanted.Contains(c.name))) { return; }
            importer.clipAnimations = clips.Take(1).Concat(clips.Skip(1).Where(c => !unwanted.Contains(c.name))).ToArray();
            importer.SaveAndReimport();
        }

        // ---- Floor ----

        /// <summary>
        /// How high (m) a clip's lowest foot gets, played on the model: the lowest its ankles or toe joints get over the
        /// clip. For a clip with a planted foot, that's where it plants it: the clip's floor, as the model plays it.
        /// </summary>
        public static float LowestFoot(GameObject model, AnimationClip clip)
        {
            const int samples = 120;
            Vector3[][] p = SampleBones(model, clip, Enumerable.Range(0, samples).Select(i => clip.length * i / samples).ToArray(),
                HumanBodyBones.LeftFoot, HumanBodyBones.LeftToes, HumanBodyBones.RightFoot, HumanBodyBones.RightToes);
            return p == null ? 0f : p.Min(bone => bone.Min(v => v.y));
        }

        // ---- Phase ----

        /// <summary>
        /// When, as a fraction of a looping clip (0 to 1), its left foot touches down, played on the model: the start of
        /// the longest stretch (wrapping round the loop) with its toe or ankle within a couple of centimetres of the lowest
        /// they get and moving back under the body, as a planted foot does. (A swinging foot moves forward, however low it
        /// skims, and the longest stretch is the stance, not a brief skim.) Blend trees
        /// offset each loop by this, so every loop lands its left foot at the same moment and blending between them
        /// doesn't mix one foot's stride with the other's.
        /// </summary>
        public static float TouchDownPhase(GameObject model, AnimationClip clip)
        {
            const int samples = 240;
            Vector3[][] p = SampleBones(model, clip, Enumerable.Range(0, samples).Select(i => clip.length * i / samples).ToArray(),
                HumanBodyBones.LeftFoot, HumanBodyBones.LeftToes);
            if (p == null) { return 0f; }
            float lowestAnkle = p[0].Min(v => v.y), lowestToe = p[1].Min(v => v.y);
            bool Down(int i) => Mathf.Min(p[0][i].y - lowestAnkle, p[1][i].y - lowestToe) < 0.02f
                                && p[0][(i + 1) % samples].z < p[0][i].z;
            int bestStart = -1, bestLength = 0;
            for (int i = 0; i < samples; i++)
            {
                if (!Down(i) || Down((i + samples - 1) % samples)) { continue; }
                int length = 1;
                while (length < samples && Down((i + length) % samples)) { length++; }
                if (length > bestLength) { bestStart = i; bestLength = length; }
            }
            return bestStart < 0 ? 0f : (float)bestStart / samples;
        }

        // ---- Stride speed ----

        /// <summary>
        /// How fast (m/s) a looping clip's planted feet move backward under the body, played on the model: the speed the
        /// character must move at for them to stay put on the ground. Measured over two loops: wherever a foot's toe is
        /// within a couple of centimetres of the lowest it gets, how fast it's moving backward; the median of those.
        /// </summary>
        public static float StrideSpeed(GameObject model, AnimationClip clip)
        {
            const int samples = 240;
            float dt = 2f * clip.length / samples;
            Vector3[][] toes = SampleBones(model, clip, Enumerable.Range(0, samples + 1).Select(i => i * dt).ToArray(),
                HumanBodyBones.LeftToes, HumanBodyBones.RightToes);
            if (toes == null) { return 0f; }
            var speeds = new List<float>();
            for (int foot = 0; foot < 2; foot++)
            {
                float lowest = toes[foot].Min(v => v.y);
                for (int i = 0; i < samples; i++)
                {
                    // Not across the loop's join, where the clip wraps.
                    float t = i * dt % clip.length;
                    if (t > clip.length - dt) { continue; }
                    if (toes[foot][i].y < lowest + 0.02f && toes[foot][i + 1].y < lowest + 0.02f)
                    {
                        speeds.Add(-(toes[foot][i + 1].z - toes[foot][i].z) / dt);
                    }
                }
            }
            if (speeds.Count == 0) { return 0f; }
            speeds.Sort();
            return speeds[speeds.Count / 2];
        }
    }
}
