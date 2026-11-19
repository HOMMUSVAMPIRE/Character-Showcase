using System;
using UnityEngine;

namespace Character
{
    /// <summary>
    /// What a creature (a model that isn't a humanoid: a fox, a dog, a bird) is made of, for the Creature Model Importer
    /// (Prepare Creatures) to make it ready to play: its skeleton's bones by name (the one the body hangs from, each leg's,
    /// the head's), the bones its paw placement turns, the bones its hitboxes go on, and its animation. One sits beside each
    /// creature's model in Assets/Models/Creatures/[name] ("[name] Setup.asset"): made when the model first arrives, its
    /// bones guessed from their names (what couldn't be found is listed in Unfound, and shown in its inspector), then
    /// the creature's to correct. Changing it prepares the creature again by itself. See docs/creatures.md.
    /// </summary>
    [CreateAssetMenu(fileName = "Creature Setup", menuName = "Character Showcase/Creature Setup")]
    public class CreatureSetup : ScriptableObject
    {
        [Tooltip("The creature's model file (a rigged model, imported as a Generic rig): what's prepared")]
        public GameObject model;

        [Header("Skeleton")]
        [Tooltip("The bone everything else hangs from (the hips or pelvis): the body's pitch and roll turn about it")]
        public string rootBone;
        [Tooltip("Its legs, each its three bones, the way its middle joint bends, and where its paw touches the ground")]
        public Leg[] legs = Array.Empty<Leg>();

        [Header("Eyes")]
        [Tooltip("The bone its eyes are on (its head): the character's eye point, and the camera, follow it")]
        public string eyesBone;
        [Tooltip("Where its eyes are, in the model's space (m, standing in its bind pose: across, up, ahead of its origin)")]
        public Vector3 eyesPosition;

        [Header("Paw placement")]
        [Tooltip("Bones turned back against the body's pitch on slopes (a tail, a neck), so they don't dig into the slope or point at the sky")]
        public CounterPitchBone[] counterPitchBones = Array.Empty<CounterPitchBone>();
        [Tooltip("Bones bent into a turn (its spine, neck and head toward it, its tail away), so it curves into it")]
        public TurnBendBone[] turnBendBones = Array.Empty<TurnBendBone>();

        [Header("Hitboxes")]
        [Tooltip("The bones its hitboxes go on (each a capsule round the mesh the bone carries). Empty: every bone carrying " +
                 "enough of the mesh")]
        public string[] hitboxBones = Array.Empty<string>();

        [Header("Animation")]
        [Tooltip("Its clips, by role (an Animation Set starting from Assets/Animation/Creature Layout.asset): built into its " +
                 "Animator Controller. Takes the place of Layout")]
        public CharacterAnimationSet animationSet;
        [Tooltip("Or an Animator Layout of its own, whose controller it plays (as the fox's, Fox Layout.asset), when it has no Animation Set")]
        public AnimatorLayout layout;

        [Header("Notes")]
        [Tooltip("What preparing couldn't find (bones guessing missed, or the model doesn't have): set those first. Written " +
                 "each time it's prepared")]
        public string[] unfound = Array.Empty<string>();
        [Tooltip("Why it's as it is: for whoever changes it next")]
        [TextArea(1, 6)] public string notes;

        /// <summary>A leg: its bones, from the hip or shoulder down (see PawPlacementIK's Legs).</summary>
        [Serializable]
        public struct Leg
        {
            [Tooltip("Its upper bone (the thigh or upper arm), turned at the hip or shoulder")]
            public string upper;
            [Tooltip("Its lower bone (the shin or forearm), turned at the knee or elbow")]
            public string lower;
            [Tooltip("The bone below the lower one (the paw, or a hind leg's foot below the hock), which keeps the animation's turn")]
            public string foot;
            [Tooltip("The way its middle joint points, in the model's space: back (0, 0, -1) for a front leg's elbow, " +
                     "forward (0, 0, 1) for a hind leg's stifle. Zero: the way the animation bends it")]
            public Vector3 bendDirection;
            [Tooltip("Where its paw touches the ground, in the model's space (m, standing in its bind pose): its toes, for a " +
                     "paw that rolls over them")]
            public Vector3 contact;
            [Tooltip("How far (m) its paw's mesh reaches beyond its contact toward a wall it's put on, running along one")]
            [Min(0f)] public float wallClearance;
        }

        /// <summary>A bone turned back against the body's pitch (see PawPlacementIK's Counter Pitch Bones).</summary>
        [Serializable]
        public struct CounterPitchBone
        {
            [Tooltip("The bone (a tail's first bone, the neck)")]
            public string bone;
            [Tooltip("How much of the body's pitch it's turned back by, nose up (0 to 1)")]
            [Range(0f, 1f)] public float noseUpShare;
            [Tooltip("How much of the body's pitch it's turned back by, nose down (0 to 1)")]
            [Range(0f, 1f)] public float noseDownShare;
            [Tooltip("How far (m) its tip is kept above the ground under it (0: not kept)")]
            [Min(0f)] public float groundClearance;
        }

        /// <summary>A bone bent into a turn (see PawPlacementIK's Turn Bend Bones).</summary>
        [Serializable]
        public struct TurnBendBone
        {
            [Tooltip("The bone (a spine bone, the neck, the head, a tail bone)")]
            public string bone;
            [Tooltip("Its share of the bend: positive toward the turn, negative away from it (the tail)")]
            [Range(-1f, 1f)] public float share;
        }
    }
}
