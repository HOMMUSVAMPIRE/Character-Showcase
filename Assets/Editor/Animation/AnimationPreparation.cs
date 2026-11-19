using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using MeridianMotion.Characters;
using UnityEditor;
using UnityEngine;

namespace MeridianMotion.EditorTools
{
    /// <summary>
    /// Prepare Animations (Tools > Meridian Motion > Prepare > Animations): gets the clips the characters play ready, then
    /// builds every Animator Controller from its layout (AnimatorControllerBuilder). Getting the clips ready is:
    /// - the Quaternius libraries' clips imported as clips (QuaterniusAnimationImporter), where they're installed, and the
    ///   motion-capture clips (MotionCaptureClipImporter);
    /// - those that stand on the floor stood on the mannequin's (StandLibraryOnTheMannequinsFloor);
    /// - the swimming clips floated at the default character's swim depth (FloatSwimsAtTheSwimDepth).
    /// The swim depth is the character's tuning (Swimming's Swim Depth, a share of its standing height), so when either
    /// changes, the swims are floated again by themselves, with a progress bar (see SwimDepthWatcher): nothing to remember.
    /// </summary>
    public static class AnimationPreparation
    {
        private const string LogName = "Prepare Animations";
        // The libraries' clips that stand on the floor, raised onto the mannequin's (StandLibraryOnTheMannequinsFloor).
        // The jump's loop is raised as its start is: it has no floor of its own (its feet are in the air).
        private static readonly string[] StandingLibraryClips = { "Walk_Loop", "Jog_Fwd_Loop", "Sprint_Loop", "Jump_Start", "Jump_Land", "Crouch_Idle_Loop", "Crouch_Fwd_Loop", "Push_Loop" };
        private const string TreadingClip = "Swim_Idle_Loop", StrokeClip = "Swim_Fwd_Loop";
        // Treading water, how far (m, on the mannequin) its head's pivot (the top of its neck) stands above the surface: the
        // chin just out of the water. Swimming along, how far below the surface its hips' pivot is: the back just breaking
        // it, the head at the water line.
        private const float HeadAboveWater = 0.15f, HipsBelowWater = 0.05f;
        // Where the swim depth the swims were last floated at is kept (per machine: the floats themselves are in the
        // libraries' import settings, which are shared), so a change to it is noticed (see SwimDepthWatcher).
        private const string FloatedAtPath = "Library/CharacterShowcase/SwimsFloatedAt.txt";

        [MenuItem("Tools/Meridian Motion/Prepare/Animations", priority = 4)]
        public static void PrepareFromMenu() => Prepare(showProgress: true);

        /// <summary>For -executeMethod: Prepare, then exits (code 1 if it failed).</summary>
        public static void PrepareFromCommandLine() => CommandLine.Finish(Prepare(showProgress: false));

        /// <summary>Gets the clips ready and builds every controller (see the class). Returns whether it all worked.</summary>
        public static bool Prepare(bool showProgress)
        {
            try
            {
                bool library = QuaterniusAnimationImporter.FindLibraryFiles().Any();
                if (library)
                {
                    Progress(showProgress, "Importing the clip libraries", 0.05f);
                    List<string> problems = QuaterniusAnimationImporter.PrepareAll();
                    if (problems.Count > 0) { Debug.LogWarning($"[{LogName}] The clip libraries: {string.Join("; ", problems)}"); }
                }
                if (AssetDatabase.IsValidFolder(MotionCaptureClipImporter.ClipsFolder))
                {
                    Progress(showProgress, "Importing the motion-capture clips", 0.15f);
                    List<string> problems = MotionCaptureClipImporter.PrepareAll();
                    if (problems.Count > 0) { Debug.LogWarning($"[{LogName}] The motion-capture clips: {string.Join("; ", problems)}"); }
                }
                GameObject mannequin = MeasuringModel();
                if (library && mannequin != null)
                {
                    Progress(showProgress, "Standing the libraries' clips on the mannequin's floor", 0.25f);
                    StandLibraryOnTheMannequinsFloor(mannequin);
                    Progress(showProgress, "Floating the swimming clips at the swim depth", 0.5f);
                    FloatSwimsAtTheSwimDepth(mannequin);
                }
                Progress(showProgress, "Building the Animator Controllers from their layouts", 0.75f);
                return AnimatorControllerBuilder.BuildAll();
            }
            finally
            {
                if (showProgress) { EditorUtility.ClearProgressBar(); }
            }
        }

        private static void Progress(bool show, string what, float done)
        {
            if (show) { EditorUtility.DisplayProgressBar(LogName, what + "…", done); }
        }

        /// <summary>The mannequin, measured on as the clips play on it: its prepared prefab if there is one yet, else its file.</summary>
        private static GameObject MeasuringModel()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(HumanoidModelImporter.MannequinPrefabPath);
            return model != null ? model : AssetDatabase.LoadAssetAtPath<GameObject>(HumanoidModelImporter.MannequinModelPath);
        }

        /// <summary>
        /// Stands the libraries' clips that stand on the floor on the mannequin's: where its own idle stands its feet.
        /// Played on the mannequin, each clip's lowest foot (its planted one) should be as low as the idle's, as foot
        /// placement keeps each foot's height above its clip's floor; the libraries' clips stood about 3 cm lower, so the
        /// feet went through the floor. Each is raised by the difference (its root height offset, stored in the clip as the
        /// library's mannequin is sized, so it scales to every model).
        /// </summary>
        public static void StandLibraryOnTheMannequinsFloor(GameObject mannequin)
        {
            AnimationClip idle = AssetDatabase.LoadAllAssetsAtPath(HumanoidModelImporter.MannequinModelPath).OfType<AnimationClip>().FirstOrDefault(c => c.name == "Idle");
            if (idle == null) { Debug.LogWarning($"[{LogName}] The mannequin has no Idle clip to stand the libraries' clips by."); return; }
            float idleFloor = MotionCaptureLocomotion.LowestFoot(mannequin, idle);
            // How the library's mannequin's sizes compare with this one's: the offsets are in its units.
            float scale = LibraryHumanScale() / HumanScale(mannequin);
            for (int pass = 0; pass < 2; pass++)
            {
                var raise = new Dictionary<string, float>();
                foreach (string name in StandingLibraryClips)
                {
                    AnimationClip clip = QuaterniusAnimationImporter.FindClip(name);
                    if (clip == null) { continue; } // Not installed: the mannequin's own is used.
                    raise[name] = (idleFloor - MotionCaptureLocomotion.LowestFoot(mannequin, clip)) * scale;
                }
                if (raise.TryGetValue("Jump_Start", out float jump)) { raise["Jump_Loop"] = jump; }
                Debug.Log($"[{LogName}] Standing the libraries' clips on the mannequin's floor: raising " +
                          string.Join(", ", raise.Select(r => $"{r.Key} {r.Value * 100f:0.0} cm")));
                if (raise.Values.All(r => Mathf.Abs(r) < 0.002f) || !QuaterniusAnimationImporter.RaiseClips(raise)) { break; }
            }
        }

        /// <summary>
        /// Floats the libraries' swimming clips (treading water and the swim stroke) at the default character's swimming
        /// depth. They're made around the water line: where a grounded clip has its floor, their swimmer has the water's
        /// surface. But a swimming character's origin is still its feet, hanging Swimming's swim depth below the surface, so
        /// as made the swimmer was that much too deep. Each is raised (its root height offset), measured on the mannequin
        /// with its feet the swim depth below the surface: treading water so that its head stands HeadAboveWater above the
        /// surface, and the stroke so that its hips are HipsBelowWater under it (the library's stroke, at its treading's
        /// water line, had the swimmer's back under the water). Remembers the depth it floated them at (SwimDepthWatcher).
        /// </summary>
        public static void FloatSwimsAtTheSwimDepth(GameObject mannequin)
        {
            if (QuaterniusAnimationImporter.FindClip(TreadingClip) == null || QuaterniusAnimationImporter.FindClip(StrokeClip) == null) { return; }
            float surface = DefaultSwimDepth();
            // How the library's mannequin's sizes compare with this one's: the offsets are in its units.
            float scale = LibraryHumanScale() / HumanScale(mannequin);
            for (int pass = 0; pass < 2; pass++)
            {
                var raise = new Dictionary<string, float>
                {
                    [TreadingClip] = (surface + HeadAboveWater - MotionCaptureLocomotion.MeanBoneHeight(mannequin, QuaterniusAnimationImporter.FindClip(TreadingClip), HumanBodyBones.Head)) * scale,
                    [StrokeClip] = (surface - HipsBelowWater - MotionCaptureLocomotion.MeanBoneHeight(mannequin, QuaterniusAnimationImporter.FindClip(StrokeClip), HumanBodyBones.Hips)) * scale,
                };
                Debug.Log($"[{LogName}] Floating the libraries' swimming at the swim depth ({surface:0.00} m over the feet): raising " +
                          string.Join(", ", raise.Select(r => $"{r.Key} {r.Value * 100f:0.0} cm")));
                if (raise.Values.All(r => Mathf.Abs(r) < 0.002f) || !QuaterniusAnimationImporter.RaiseClips(raise)) { break; }
            }
            RememberFloatedAt(surface);
        }

        /// <summary>
        /// How far (m) below the surface the Character prefab's feet hang as it swims: its movement mode's Swimming's swim
        /// depth (whether or not it's turned on; Swimming's default without one), a share of its standing height. That's
        /// the height of the body it has with its model, which the movement measures by: the model's own body, when its
        /// host fits its body to its model (CharacterModelSlot's Body Fit), else its body as authored (its Body
        /// Definition's). Floated for the authored body's height while the character took its model's, a little shorter,
        /// the swimmer was drawn that much too high.
        /// </summary>
        public static float DefaultSwimDepth()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterSceneSetup.CharacterPrefabPath);
            MMotionMotor movement = prefab != null ? prefab.GetComponent<MMotionMotor>() : null;
            if (movement == null) { return 0f; }
            float share = movement.movementMode is AbilityMovementMode mode ? mode.SettingsOf<Swimming>().SwimDepth : new Swimming().SwimDepth;
            BodyShape? withModel = prefab.TryGetComponent(out CharacterModelSlot host) ? host.BodyFor(host.ModelSource, movement) : null;
            return share * BodyFitting.StandingHeightOf(withModel ?? movement.AuthoredBody);
        }

        /// <summary>The swim depth (m) the swims were last floated at on this machine, or NaN if they haven't been.</summary>
        public static float FloatedAt()
        {
            string path = Path.Combine(Path.GetDirectoryName(Application.dataPath), FloatedAtPath);
            return File.Exists(path) && float.TryParse(File.ReadAllText(path).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float depth) ? depth : float.NaN;
        }

        private static void RememberFloatedAt(float depth)
        {
            string path = Path.Combine(Path.GetDirectoryName(Application.dataPath), FloatedAtPath);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, depth.ToString("R", CultureInfo.InvariantCulture));
        }

        private static float HumanScale(GameObject model)
        {
            GameObject instance = Object.Instantiate(model);
            try
            {
                Animator animator = instance.GetComponentInChildren<Animator>();
                return animator != null && animator.isHuman ? animator.humanScale : 1f;
            }
            finally { Object.DestroyImmediate(instance); }
        }

        private static float LibraryHumanScale()
        {
            string path = QuaterniusAnimationImporter.FindLibraryFiles().First();
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            GameObject instance = Object.Instantiate(model);
            try
            {
                Animator animator = instance.GetComponentInChildren<Animator>() ?? instance.AddComponent<Animator>();
                animator.avatar = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().First();
                return animator.humanScale;
            }
            finally { Object.DestroyImmediate(instance); }
        }

        /// <summary>
        /// Floats the swims again by themselves when the default character's swim depth changes: Swimming's Swim Depth on
        /// its movement asset, or its standing height (its body, or its model's). Each time one of those assets is saved,
        /// the depth it makes is compared with the depth the swims were floated at, and if they differ by more than a
        /// millimetre, the swims are floated again with a progress bar (it reimports the swimming library, which takes a
        /// moment). Not in batch mode (a test or a build changing them leaves the clips as they are), nor while playing.
        /// </summary>
        private sealed class SwimDepthWatcher : AssetPostprocessor
        {
            private static bool pending;

            private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
            {
                if (pending || Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode || imported.Length == 0) { return; }
                HashSet<string> watched = WatchedPaths();
                if (!imported.Any(watched.Contains)) { return; }
                pending = true;
                EditorApplication.delayCall += () =>
                {
                    pending = false;
                    float depth = DefaultSwimDepth(), floatedAt = FloatedAt();
                    if (!float.IsNaN(floatedAt) && Mathf.Abs(depth - floatedAt) < 0.001f) { return; }
                    GameObject mannequin = MeasuringModel();
                    if (mannequin == null || QuaterniusAnimationImporter.FindClip(TreadingClip) == null) { return; }
                    try
                    {
                        EditorUtility.DisplayProgressBar(LogName, $"The swim depth is now {depth:0.00} m: floating the swimming clips at it…", 0.5f);
                        FloatSwimsAtTheSwimDepth(mannequin);
                    }
                    finally { EditorUtility.ClearProgressBar(); }
                };
            }

            // The default character's prefab, its movement asset and its body: what its swim depth comes from.
            private static HashSet<string> WatchedPaths()
            {
                var paths = new HashSet<string> { CharacterSceneSetup.CharacterPrefabPath };
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterSceneSetup.CharacterPrefabPath);
                if (prefab != null && prefab.TryGetComponent(out MMotionMotor movement))
                {
                    if (movement.movementMode != null) { paths.Add(AssetDatabase.GetAssetPath(movement.movementMode)); }
                    if (movement.BodyDefinition != null) { paths.Add(AssetDatabase.GetAssetPath(movement.BodyDefinition)); }
                }
                if (prefab != null && prefab.TryGetComponent(out CharacterModelSlot host) && host.ModelSource != null && host.ModelSource.BodyDefinition != null)
                {
                    paths.Add(AssetDatabase.GetAssetPath(host.ModelSource.BodyDefinition));
                }
                return paths;
            }
        }
    }
}
