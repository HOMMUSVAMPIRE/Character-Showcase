using System;
using UnityEngine;

namespace Character
{
    /// <summary>
    /// A character's Animator Controller described as data, which the Animator Controller Builder (Tools > Character
    /// Showcase > Build Animator Controllers) builds it from, so a state, a clip or a transition is changed here, in the
    /// inspector, not in code. It holds:
    /// - clip slots: each a name and its clips in order of preference, the first that's there used, so a clip from a
    ///   library that isn't installed falls back to one that is;
    /// - the controller's parameters;
    /// - blend trees, by name: clips (or other trees) blended by a parameter or two;
    /// - states: each with its motions in order of preference, the first whose clips are all there used, each with its own
    ///   speed, time and playback settings;
    /// - transitions, from a state or from any state, with their timings and conditions.
    /// Anything can require clip slots or states to be there (or not), so one layout makes the right controller with a
    /// clip library installed and without it. Numbers can be measured from the clips as they play on the Measuring Model
    /// (how fast a loop's strides are made for, where its left foot lands, where a jump takes off), so the controller fits
    /// its clips without hand-tuning. See docs/animation.md, Animator layouts.
    /// </summary>
    [CreateAssetMenu(fileName = "Animator Layout", menuName = "Character Showcase/Animation/Animator Layout")]
    public class AnimatorLayout : ScriptableObject
    {
        [Tooltip("The Animator Controller this layout is built into (its contents are replaced each build; the asset, and " +
                 "everything referring to it, is kept)")]
        public RuntimeAnimatorController controller;
        [Tooltip("The model the clips are measured on (its prefab, with an Animator): the measured numbers are as the clips " +
                 "play on it")]
        public GameObject measuringModel;
        [Tooltip("Whether the base layer runs IK (OnAnimatorIK), which humanoid IK (CharacterIK) needs")]
        public bool ikPass = true;
        [Tooltip("A starting point for animation sets only (the creature layout: its slots have no clips of their own): " +
                 "never built by itself, only into the sets that start from it, with their clips")]
        public bool onlyForSets;
        [Tooltip("The state the controller starts in")]
        public string defaultState = "Locomotion";
        [Tooltip("Why the layout is as it is, as a whole: for whoever changes it next")]
        [TextArea(2, 10)] public string notes;

        [Tooltip("The clips, each by a name the motions use, with its candidates in order of preference")]
        public ClipSlot[] clips = Array.Empty<ClipSlot>();
        [Tooltip("The controller's parameters")]
        public LayoutParameter[] parameters = Array.Empty<LayoutParameter>();
        [Tooltip("The blend trees, by name, which motions and other trees use")]
        public LayoutBlendTree[] blendTrees = Array.Empty<LayoutBlendTree>();
        [Tooltip("The states")]
        public LayoutState[] states = Array.Empty<LayoutState>();
        [Tooltip("The transitions, in order: from the same state, the first whose conditions hold is taken")]
        public LayoutTransition[] transitions = Array.Empty<LayoutTransition>();

        /// <summary>What a motion names to use a blend tree rather than a clip slot: "tree:" and the tree's name.</summary>
        public const string TreePrefix = "tree:";
        /// <summary>What a transition's From names to go from any state.</summary>
        public const string AnyState = "Any State";
    }

    /// <summary>A clip by name, and the clips it can be, in order of preference: the first that's there is used.</summary>
    [Serializable]
    public struct ClipSlot
    {
        [Tooltip("The name motions use for it")]
        public string name;
        [Tooltip("Its clips in order of preference (a library's, then a fallback): the first that's there is used")]
        public AnimationClip[] candidates;
        [Tooltip("Another slot whose clip it uses when none of its own is there (a run with no clip of its own uses the walk's)")]
        public string fallbackSlot;
    }

    /// <summary>What must be there, and what mustn't, for part of a layout to be built.</summary>
    [Serializable]
    public struct LayoutRequirement
    {
        [Tooltip("Clip slots that must have a clip")]
        public string[] clips;
        [Tooltip("Clip slots that must have none")]
        public string[] missingClips;
        [Tooltip("States that must be built")]
        public string[] states;
        [Tooltip("States that mustn't be")]
        public string[] missingStates;
    }

    /// <summary>How a number in a layout is found: given, or measured from a clip on the Measuring Model.</summary>
    public enum LayoutValueKind
    {
        /// <summary>The number given (Constant).</summary>
        Constant,
        /// <summary>The speed (m/s) a loop's strides are made for: its planted feet stay put at it.</summary>
        StrideSpeed,
        /// <summary>How far through a loop (0 to 1) its left foot lands.</summary>
        TouchDownPhase,
        /// <summary>How far through a jump (0 to 1) its feet leave the ground.</summary>
        TakeOffPhase,
        /// <summary>The speed (m/s) a climbing loop's hands are made for, the way given (Direction).</summary>
        ClimbSpeed,
    }

    /// <summary>
    /// A number: given, or measured from a clip (then multiplied, inverted or eased toward another as asked). One that
    /// can't be measured (its clip isn't there, or the measurement fails) is its Fallback, or, without one, leaves out what
    /// it's part of (a motion falls back to the next).
    /// </summary>
    [Serializable]
    public struct LayoutValue
    {
        [Tooltip("Given, or measured from a clip")]
        public LayoutValueKind kind;
        [Tooltip("The number, for Constant")]
        public float constant;
        [Tooltip("The clip slot measured")]
        public string clip;
        [Tooltip("For Climb Speed: the way climbed (x right, y up)")]
        public Vector2 direction;
        [Tooltip("Multiplies what's measured (0 or 1: as it is)")]
        public float multiply;
        [Tooltip("Uses one over what's measured (for a state's speed against a speed parameter)")]
        public bool reciprocal;
        [Tooltip("Eases what's measured this far toward Lerp To (0: as it is)")]
        public float lerp;
        [Tooltip("What Lerp eases toward")]
        public float lerpTo;
        [Tooltip("Whether a measurement that fails uses Fallback instead")]
        public bool hasFallback;
        [Tooltip("What a measurement that fails is taken to be, with Has Fallback (then multiplied, inverted and eased as a measured one)")]
        public float fallback;
        [Tooltip("The smallest a measured speed may be and still count as measured (below it, it's failed)")]
        public float minimum;

        /// <summary>A given number.</summary>
        public static LayoutValue Of(float value) => new() { kind = LayoutValueKind.Constant, constant = value };

        /// <summary>A number measured from a clip slot.</summary>
        public static LayoutValue Measured(LayoutValueKind kind, string clip) => new() { kind = kind, clip = clip };
    }

    /// <summary>An Animator parameter and its default.</summary>
    [Serializable]
    public struct LayoutParameter
    {
        public string name;
        [Tooltip("Why it's as it is (what it shows, and anything learned tuning it): for whoever changes it next")]
        [TextArea(1, 6)] public string note;
        public AnimatorControllerParameterType type;
        [Tooltip("A bool's default")]
        public bool defaultBool;
        [Tooltip("A float's default (a playback parameter's top speed is measured: see Animator Parameter Translator, Playback)")]
        public LayoutValue defaultFloat;
        [Tooltip("What must be there for it to be added")]
        public LayoutRequirement requires;
    }

    /// <summary>How a blend tree blends its children.</summary>
    public enum LayoutBlendType { Simple1D, FreeformDirectional2D, FreeformCartesian2D, SimpleDirectional2D }

    /// <summary>A blend tree: its children blended by one parameter (1D) or two (2D).</summary>
    [Serializable]
    public struct LayoutBlendTree
    {
        [Tooltip("Its name (motions use it as tree:Name)")]
        public string name;
        [Tooltip("Why it's as it is (what it shows, and anything learned tuning it): for whoever changes it next")]
        [TextArea(1, 6)] public string note;
        public LayoutBlendType type;
        [Tooltip("The parameter it blends by (across, for 2D)")]
        public string parameter;
        [Tooltip("For 2D: the parameter it blends by up and down")]
        public string parameterY;
        public LayoutChild[] children;
    }

    /// <summary>A blend tree's child: a clip slot or another tree, at a threshold (1D) or position (2D).</summary>
    [Serializable]
    public struct LayoutChild
    {
        [Tooltip("A clip slot's name, or tree:Name for another blend tree")]
        public string motion;
        [Tooltip("Why it's as it is (what it shows, and anything learned tuning it): for whoever changes it next")]
        [TextArea(1, 6)] public string note;
        [Tooltip("1D: where it's blended in")]
        public LayoutValue threshold;
        [Tooltip("2D: the way it's at (its position is this times Position Scale)")]
        public Vector2 position;
        [Tooltip("2D: how far out it's at that way (a climbing loop at its speed: measured)")]
        public LayoutValue positionScale;
        [Tooltip("How fast it plays (0 or 1: as it is)")]
        public float timeScale;
        [Tooltip("Where in its cycle it starts (0 to 1)")]
        public LayoutValue cycleOffset;
        [Tooltip("What must be there for it to be a child")]
        public LayoutRequirement requires;
    }

    /// <summary>One of a state's motions, with how it plays: the first of a state's whose clips are all there is used.</summary>
    [Serializable]
    public struct LayoutMotion
    {
        [Tooltip("A clip slot's name, or tree:Name for a blend tree")]
        public string motion;
        [Tooltip("Why it's as it is (what it shows, and anything learned tuning it): for whoever changes it next")]
        [TextArea(1, 6)] public string note;
        [Tooltip("How fast it plays (with a Speed Parameter, multiplied by it); a measured one that fails leaves it at 1 and the speed parameter off")]
        public LayoutValue speed;
        [Tooltip("A float parameter multiplying its speed (a playback parameter, or a speed it plays in step with)")]
        public string speedParameter;
        [Tooltip("A float parameter driving how far through it plays (0 to 1), rather than time")]
        public string timeParameter;
        [Tooltip("Where in its cycle it starts (0 to 1)")]
        public LayoutValue cycleOffset;
        [Tooltip("Float parameters this motion sets the defaults of (a playback parameter's top speeds), as it's chosen")]
        public LayoutParameter[] parameters;
        [Tooltip("What must be there for it to be used (beyond its own clips)")]
        public LayoutRequirement requires;
    }

    /// <summary>A state: its name and its motions, in order of preference.</summary>
    [Serializable]
    public struct LayoutState
    {
        public string name;
        [Tooltip("Why it's as it is (what it shows, and anything learned tuning it): for whoever changes it next")]
        [TextArea(1, 6)] public string note;
        [Tooltip("Its motions, in order of preference: the first whose clips are all there (and whose measurements work) is used")]
        public LayoutMotion[] motions;
        [Tooltip("What must be there for it to be built")]
        public LayoutRequirement requires;
    }

    /// <summary>How a condition compares its parameter.</summary>
    public enum LayoutConditionMode { If, IfNot, Greater, Less, Equals, NotEqual }

    /// <summary>A transition's condition.</summary>
    [Serializable]
    public struct LayoutCondition
    {
        public LayoutConditionMode mode;
        public string parameter;
        [Tooltip("Why it's as it is (what it shows, and anything learned tuning it): for whoever changes it next")]
        [TextArea(1, 6)] public string note;
        [Tooltip("For Greater, Less, Equals and Not Equal")]
        public float threshold;
        [Tooltip("What must be there for it to be one of the transition's conditions")]
        public LayoutRequirement requires;
    }

    /// <summary>A transition: from a state (or Any State) to another, when its conditions all hold.</summary>
    [Serializable]
    public struct LayoutTransition
    {
        [Tooltip("The state it's from, or Any State")]
        public string from;
        [Tooltip("The state it's to")]
        public string to;
        [Tooltip("Why it's as it is (what it shows, and anything learned tuning it): for whoever changes it next")]
        [TextArea(1, 6)] public string note;
        [Tooltip("Whether it waits for its state's Exit Time")]
        public bool hasExitTime;
        [Tooltip("How far through its state (0 to 1) it's taken, with Has Exit Time")]
        public LayoutValue exitTime;
        [Tooltip("How long the blend into the next state takes")]
        public float duration;
        [Tooltip("Whether Duration is a share of the state's length (else seconds, as Unity's own default)")]
        public bool durationIsShare;
        [Tooltip("From Any State only: whether it can be taken into the state it's already in (a transition from a state " +
                 "always can, as Unity makes them)")]
        public bool canTransitionToSelf;
        public LayoutCondition[] conditions;
        [Tooltip("What must be there for it to be built (its two states always must)")]
        public LayoutRequirement requires;
    }
}
