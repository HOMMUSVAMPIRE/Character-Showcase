using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using MeridianMotion.Characters;
using MeridianMotion.EditorTools;
using UnityEditor.Animations;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace MeridianMotion.Showcase.Pipeline
{
    // The mannequin's clips: its importer settings, and one looping in-place clip per take. The libraries' clips are made ready by the shipped Prepare Animations (AnimationPreparation).
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
        /// Root motion is baked into the pose: the character is moved by MMotionMotor, not by animation.
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
    }
}
