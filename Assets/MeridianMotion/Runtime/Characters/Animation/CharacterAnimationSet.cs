using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace MeridianMotion.Characters
{
    /// <summary>
    /// A character's own clips, chosen by slot: an Animator Layout to start from (the mannequin's, say), and the clips it
    /// plays in place of the layout's in the slots (the layout's clip slots) it fills. Built into an Animator Controller of
    /// its own (Build, in its inspector, or Build Animator Controllers), which a model source then uses (its Animator
    /// Controller): the layout's states and transitions, with these clips, measured as they play (a walk of its own gets
    /// the speed its strides are made for). Slots left empty keep the layout's clips. So a model gets its own idle or run
    /// without an Animator Controller made by hand. See docs/animation.md, Animation sets.
    /// </summary>
    [CreateAssetMenu(fileName = "Animation Set", menuName = "Meridian Motion/Animation/Animation Set")]
    public class CharacterAnimationSet : ScriptableObject
    {
        [Tooltip("The layout it starts from: its states, transitions and clips, the clips here replacing its own")]
        public AnimatorLayout layout;
        [Tooltip("The Animator Controller it's built into (made beside it the first time it's built): set it as a model " +
                 "source's Animator Controller to play it")]
        public RuntimeAnimatorController controller;
        [Tooltip("The clips it plays in the layout's place, each in a slot (one of the layout's clip slots)")]
        public SlotClip[] clips = Array.Empty<SlotClip>();
        [Tooltip("Why it's as it is: for whoever changes it next")]
        [TextArea(1, 6)] public string notes;

        /// <summary>A clip in a slot: one of the layout's clip slots.</summary>
        [Serializable]
        public struct SlotClip
        {
            [Tooltip("The slot: the layout's clip slot it fills")]
            [FormerlySerializedAs("slot")] public string slot;
            [Tooltip("The clip it plays there (empty: the layout's own)")]
            public AnimationClip clip;
        }

        /// <summary>The set's clip for a slot, or null if it leaves the slot to the layout.</summary>
        public AnimationClip ClipForSlot(string slot)
        {
            foreach (SlotClip entry in clips)
            {
                if (entry.slot == slot && entry.clip != null) { return entry.clip; }
            }
            return null;
        }
    }
}
