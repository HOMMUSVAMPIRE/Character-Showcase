using System.Collections.Generic;
using System.Linq;
using Character;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace CharacterShowcase.EditorTools
{
    // Building the shared Animator Controller: from its layout (CharacterModelImporter.LayoutPath), which holds its states,
    // clips and transitions (edited there, not here), once the clips it plays are ready (AnimationPreparation).
    public static partial class MannequinGenerator
    {
        private static AnimatorController BuildAnimatorController()
        {
            Dictionary<string, AnimationClip> clips = LoadClips();
            string[] required = { "Idle", "Walk", "Brisk Walk", "Run", "Sprint", "Rise", "Apex", "Fall", "Dash", "Crouch", "Hang", "Wall Slide", "Rope Swing", "Zipline", "Swim" };
            string[] missing = required.Where(n => !clips.ContainsKey(n)).ToArray();
            if (missing.Length > 0)
            {
                Debug.LogError($"[MannequinGenerator] '{ModelPath}' is missing clips: {string.Join(", ", missing)}. " +
                               "Run Tools > Character Showcase > Generate Mannequin with Blender to regenerate it. Nothing was changed.");
                return null;
            }

            // The clips made ready (the libraries' stood on the mannequin's floor and their swims floated at the swim depth),
            // and every controller built from its layout: the shipped Prepare Animations, which needs nothing of this one's.
            if (!AnimationPreparation.Prepare(showProgress: false))
            {
                Debug.LogError("[MannequinGenerator] Prepare Animations failed (see the Console above). Nothing more was changed.");
                return null;
            }
            var layout = AssetDatabase.LoadAssetAtPath<AnimatorLayout>(CharacterModelImporter.LayoutPath);
            if (layout == null)
            {
                Debug.LogError($"[MannequinGenerator] There's no Animator Layout at '{CharacterModelImporter.LayoutPath}' to build the controller from. Nothing was changed.");
                return null;
            }
            return layout.controller as AnimatorController;
        }
    }
}
