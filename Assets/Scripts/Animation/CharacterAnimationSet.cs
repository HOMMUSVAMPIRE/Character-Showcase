using System;
using UnityEngine;

namespace Character
{
    /// <summary>
    /// A character's own clips, chosen by role: an Animator Layout to start from (the mannequin's, say), and the clips it
    /// plays in place of the layout's in the roles (the layout's clip slots) it fills. Built into an Animator Controller of
    /// its own (Build, in its inspector, or Build Animator Controllers), which a model source then uses (its Animator
    /// Controller): the layout's states and transitions, with these clips, measured as they play (a walk of its own gets
    /// the speed its strides are made for). Roles left empty keep the layout's clips. So a model gets its own idle or run
    /// without an Animator Controller made by hand. See docs/animation.md, Animation sets.
    /// </summary>
    [CreateAssetMenu(fileName = "Animation Set", menuName = "Character Showcase/Animation/Animation Set")]
    public class CharacterAnimationSet : ScriptableObject
    {
        [Tooltip("The layout it starts from: its states, transitions and clips, the clips here replacing its own")]
        public AnimatorLayout layout;
        [Tooltip("The Animator Controller it's built into (made beside it the first time it's built): set it as a model " +
                 "source's Animator Controller to play it")]
        public RuntimeAnimatorController controller;
        [Tooltip("The clips it plays in the layout's place, each in a role (one of the layout's clip slots)")]
        public RoleClip[] clips = Array.Empty<RoleClip>();
        [Tooltip("Why it's as it is: for whoever changes it next")]
        [TextArea(1, 6)] public string notes;

        /// <summary>A clip in a role: one of the layout's clip slots.</summary>
        [Serializable]
        public struct RoleClip
        {
            [Tooltip("The role: the layout's clip slot it fills")]
            public string role;
            [Tooltip("The clip it plays there (empty: the layout's own)")]
            public AnimationClip clip;
        }

        /// <summary>The set's clip for a role, or null if it leaves the role to the layout.</summary>
        public AnimationClip ClipFor(string role)
        {
            foreach (RoleClip entry in clips)
            {
                if (entry.role == role && entry.clip != null) { return entry.clip; }
            }
            return null;
        }
    }
}
