using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Character;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace CharacterShowcase.EditorTools
{
    // The mannequin's clips: its importer settings, one looping in-place clip per take, and the Quaternius libraries' clips stood on its floor.
    public static partial class MannequinGenerator
    {
        private static bool ConfigureImporter()
        {
            if (AssetImporter.GetAtPath(ModelPath) is not ModelImporter importer)
            {
                Debug.LogError($"[MannequinGenerator] No model found at '{ModelPath}'. Generate it with Blender.");
                return false;
            }

            bool changed = false;
            void Set<T>(T current, T wanted, System.Action<T> apply)
            {
                if (!Equals(current, wanted)) { apply(wanted); changed = true; }
            }
            Set(importer.animationType, ModelImporterAnimationType.Human, v => importer.animationType = v);
            Set(importer.avatarSetup, ModelImporterAvatarSetup.CreateFromThisModel, v => importer.avatarSetup = v);
            Set(importer.importAnimation, true, v => importer.importAnimation = v);
            if (changed)
            {
                // Reimport first so the FBX's takes are known before configuring clips from them.
                importer.SaveAndReimport();
                changed = false;
            }

            ModelImporterClipAnimation[] clips = BuildClipSettings(importer.defaultClipAnimations);
            if (!ClipSettingsMatch(importer.clipAnimations, clips))
            {
                importer.clipAnimations = clips;
                changed = true;
            }
            if (changed) { importer.SaveAndReimport(); }
            return true;
        }

        /// <summary>
        /// One looping, in-place clip per FBX take. Blender names takes "Armature|Walk"; Unity clips get the short name.
        /// Root motion is baked into the pose: the character is moved by MovementController, not by animation.
        /// </summary>
        private static ModelImporterClipAnimation[] BuildClipSettings(ModelImporterClipAnimation[] takes)
        {
            // The bind pose take only sets the file's default pose (see mannequin_animations.py); it isn't a clip.
            return takes.Where(take => take.takeName.Split('|').Last() != BindPoseTake).Select(take =>
            {
                var clip = take;
                clip.name = take.takeName.Split('|').Last();
                clip.loopTime = true;
                clip.loopPose = false; // The clips are authored to loop seamlessly already.
                clip.lockRootRotation = true;
                clip.keepOriginalOrientation = true;
                clip.lockRootHeightY = true;
                clip.keepOriginalPositionY = true; // Keep the authored hip bob relative to the feet.
                clip.heightFromFeet = false;
                clip.lockRootPositionXZ = true;
                clip.keepOriginalPositionXZ = true;
                return clip;
            }).ToArray();
        }

        private static bool ClipSettingsMatch(ModelImporterClipAnimation[] current, ModelImporterClipAnimation[] wanted)
        {
            if (current.Length != wanted.Length) { return false; }
            for (int i = 0; i < current.Length; i++)
            {
                ModelImporterClipAnimation a = current[i], b = wanted[i];
                if (a.name != b.name || a.takeName != b.takeName || a.loopTime != b.loopTime || a.loopPose != b.loopPose
                    || a.lockRootRotation != b.lockRootRotation || a.keepOriginalOrientation != b.keepOriginalOrientation
                    || a.lockRootHeightY != b.lockRootHeightY || a.keepOriginalPositionY != b.keepOriginalPositionY
                    || a.heightFromFeet != b.heightFromFeet || a.lockRootPositionXZ != b.lockRootPositionXZ
                    || a.keepOriginalPositionXZ != b.keepOriginalPositionXZ
                    || !Mathf.Approximately(a.firstFrame, b.firstFrame) || !Mathf.Approximately(a.lastFrame, b.lastFrame))
                {
                    return false;
                }
            }
            return true;
        }

        private static Dictionary<string, AnimationClip> LoadClips() =>
            AssetDatabase.LoadAllAssetsAtPath(ModelPath)
                .OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview__"))
                .ToDictionary(c => c.name);

        /// <summary>
        /// Builds the controller from scratch each time so it always matches this code:
        /// Locomotion (Idle / Walk / Brisk Walk / Run / Sprint by Speed) and Airborne (Fall / Apex / Rise by VerticalSpeed), switched by Grounded,
        /// plus Dash, entered from any state while Dashing, and the movement abilities' states (see BuildAbilityStates).
        /// </summary>
        /// <summary>
        /// The Quaternius libraries' clips the controller uses, set up first if they aren't already
        /// (QuaterniusAnimationImporter), or null if the libraries aren't installed or any clip is missing.
        /// </summary>
        private static Dictionary<string, AnimationClip> LoadLibraryClips()
        {
            if (!QuaterniusAnimationImporter.FindLibraryFiles().Any()) { return null; }
            List<string> problems = QuaterniusAnimationImporter.PrepareAll();
            if (problems.Count > 0) { return null; }
            var clips = LibraryClips.ToDictionary(name => name, QuaterniusAnimationImporter.FindClip);
            string[] missing = clips.Where(c => c.Value == null).Select(c => c.Key).ToArray();
            if (missing.Length > 0)
            {
                Debug.LogWarning($"[MannequinGenerator] The Quaternius libraries are missing {string.Join(", ", missing)}: using the mannequin's own clips.");
                return null;
            }
            return clips;
        }

        /// <summary>
        /// Stands the libraries' clips the controller uses on the mannequin's floor: where its own idle stands its feet.
        /// Played on the mannequin, each clip's lowest foot (its planted one) should be as low as the idle's, as foot
        /// placement keeps each foot's height above its clip's floor; the libraries' clips stood about 3 cm lower, so
        /// the feet went through the floor. Each is raised by the difference (its root height offset, stored in the
        /// clip as the library's mannequin is sized, so it scales to every model). The jump's loop is raised as its
        /// start is (it has no floor of its own: its feet are in the air).
        /// </summary>
        private static void StandLibraryOnMannequinsFloor(GameObject measuringModel, AnimationClip idle)
        {
            float idleFloor = MotionCaptureLocomotion.LowestFoot(measuringModel, idle);
            // How the library's mannequin's sizes compare with this one's: the offsets are in its units.
            float scale = LibraryHumanScale() / HumanScale(measuringModel);
            for (int pass = 0; pass < 2; pass++)
            {
                var raise = new Dictionary<string, float>();
                foreach (string name in new[] { "Walk_Loop", "Jog_Fwd_Loop", "Sprint_Loop", "Jump_Start", "Jump_Land" }.Concat(GroundedAbilityLibraryClips))
                {
                    AnimationClip clip = QuaterniusAnimationImporter.FindClip(name);
                    if (clip == null) { continue; } // An ability's clip that isn't installed: the mannequin's own is used.
                    raise[name] = (idleFloor - MotionCaptureLocomotion.LowestFoot(measuringModel, clip)) * scale;
                }
                raise["Jump_Loop"] = raise["Jump_Start"];
                Debug.Log($"[MannequinGenerator] Standing the libraries' clips on the mannequin's floor: raising " +
                          string.Join(", ", raise.Select(r => $"{r.Key} {r.Value * 100f:0.0} cm")));
                if (raise.Values.All(r => Mathf.Abs(r) < 0.002f) || !QuaterniusAnimationImporter.RaiseClips(raise)) { break; }
            }
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
    }
}
