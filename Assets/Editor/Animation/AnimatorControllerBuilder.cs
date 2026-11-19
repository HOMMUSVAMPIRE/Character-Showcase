using System.Collections.Generic;
using System.Linq;
using System.Text;
using Character;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace CharacterShowcase.EditorTools
{
    /// <summary>
    /// Builds an Animator Controller from its layout (AnimatorLayout): Tools > Character Showcase > Build Animator
    /// Controllers builds every layout in the project, as Prepare Animations does once the clips are ready (and the
    /// showcase's model generators, after making their clips). The controller asset is emptied and filled again in place, so everything referring to it (every model
    /// prefab) keeps it. Clip slots fall back through their candidates, a state's motions through theirs, and anything whose
    /// requirements aren't met is left out (see AnimatorLayout); measured numbers are measured on the layout's Measuring
    /// Model, as its clips play on it (MotionCaptureLocomotion). Says what it chose and measured in the Console.
    /// </summary>
    public static class AnimatorControllerBuilder
    {
        private const string LogName = "Animator Controller Builder";

        /// <summary>
        /// Builds the layout's controller, returning it, or null (with an error in the Console) if it can't: no controller
        /// to build into, a state the default names isn't built, a transition between states that aren't there.
        /// </summary>
        public static AnimatorController Build(AnimatorLayout layout) => Build(layout, null, layout.controller as AnimatorController, layout);

        /// <summary>
        /// Builds an animation set's controller: its layout, with the set's clips in the slots it fills (see
        /// CharacterAnimationSet). Its controller is made beside it the first time ("[set] Controller.controller").
        /// </summary>
        public static AnimatorController Build(CharacterAnimationSet set)
        {
            if (set.layout == null)
            {
                Debug.LogError($"[{LogName}] '{set.name}' names no Animator Layout to start from (its Layout).", set);
                return null;
            }
            var controller = set.controller as AnimatorController;
            if (controller == null)
            {
                string folder = System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(set));
                controller = AnimatorController.CreateAnimatorControllerAtPath(AssetDatabase.GenerateUniqueAssetPath($"{folder}/{set.name} Controller.controller"));
                set.controller = controller;
                EditorUtility.SetDirty(set);
            }
            return Build(set.layout, set, controller, set);
        }

        private static AnimatorController Build(AnimatorLayout layout, CharacterAnimationSet set, AnimatorController controller, Object context)
        {
            if (controller == null)
            {
                Debug.LogError($"[{LogName}] '{layout.name}' names no Animator Controller to build into (its Controller).", layout);
                return null;
            }
            var build = new Construction(layout, set);
            if (!build.Plan(out string problem))
            {
                Debug.LogError($"[{LogName}] '{context.name}': {problem} Nothing was changed.", context);
                return null;
            }
            build.Into(controller);
            AssetDatabase.SaveAssets();
            Debug.Log($"[{LogName}] Built '{AssetDatabase.GetAssetPath(controller)}' from '{context.name}':\n{build.Report}", context);
            return controller;
        }

        [MenuItem("Tools/Character Showcase/Build Animator Controllers", priority = 5)]
        private static void BuildAllFromMenu() => BuildAll();

        /// <summary>
        /// Builds every Animator Layout in the project into its controller, then every animation set into its own (after
        /// the layouts they start from). Returns whether every one built.
        /// </summary>
        public static bool BuildAll()
        {
            bool all = true;
            foreach (string guid in AssetDatabase.FindAssets("t:" + nameof(AnimatorLayout)))
            {
                var layout = AssetDatabase.LoadAssetAtPath<AnimatorLayout>(AssetDatabase.GUIDToAssetPath(guid));
                all &= layout != null && Build(layout) != null;
            }
            foreach (string guid in AssetDatabase.FindAssets("t:" + nameof(CharacterAnimationSet)))
            {
                var set = AssetDatabase.LoadAssetAtPath<CharacterAnimationSet>(AssetDatabase.GUIDToAssetPath(guid));
                all &= set != null && Build(set) != null;
            }
            return all;
        }

        /// <summary>For -executeMethod: builds every layout (or only -layout [path]), then exits (code 1 if one failed).</summary>
        public static void BuildFromCommandLine()
        {
            string path = CommandLine.Argument("-layout");
            bool succeeded;
            if (path != null)
            {
                var layout = AssetDatabase.LoadAssetAtPath<AnimatorLayout>(path);
                succeeded = layout != null && Build(layout) != null;
                if (layout == null) { Debug.LogError($"[{LogName}] No Animator Layout at '{path}'."); }
            }
            else { succeeded = BuildAll(); }
            CommandLine.Finish(succeeded);
        }

        /// <summary>One build: what's chosen and measured (Plan), then the controller made from it (Into).</summary>
        private sealed class Construction
        {
            private readonly AnimatorLayout layout;
            private readonly CharacterAnimationSet set;
            private readonly Dictionary<string, AnimationClip> clips = new();
            private readonly Dictionary<(LayoutValueKind, string, Vector2), (bool, float)> measured = new();
            private readonly HashSet<string> builtStates = new();
            private readonly Dictionary<string, LayoutMotion> chosenMotion = new();
            private readonly StringBuilder report = new();

            public Construction(AnimatorLayout layout, CharacterAnimationSet set)
            {
                this.layout = layout;
                this.set = set;
            }

            public string Report => report.ToString();

            // ---- Planning: which clips, states and motions, and the numbers measured ----

            public bool Plan(out string problem)
            {
                problem = null;
                // Each slot's clip: the set's for it, else its first candidate that's there; then, for a slot with neither,
                // its fallback slot's (followed as far as it goes).
                foreach (ClipSlot slot in layout.clips)
                {
                    AnimationClip clip = set != null ? set.ClipFor(slot.name) : null;
                    if (clip == null) { clip = slot.candidates?.FirstOrDefault(c => c != null); }
                    else { report.Append($"  {slot.name}: the set's {clip.name}\n"); }
                    if (clip != null) { clips[slot.name] = clip; }
                }
                foreach (ClipSlot slot in layout.clips)
                {
                    string fallback = slot.fallbackSlot;
                    for (int hops = 0; !clips.ContainsKey(slot.name) && !string.IsNullOrEmpty(fallback) && hops < layout.clips.Length; hops++)
                    {
                        if (clips.TryGetValue(fallback, out AnimationClip theirs))
                        {
                            clips[slot.name] = theirs;
                            report.Append($"  {slot.name}: no clip of its own, so {fallback}'s ({theirs.name})\n");
                        }
                        fallback = layout.clips.FirstOrDefault(s => s.name == fallback).fallbackSlot;
                    }
                }
                // States can require other states: built until nothing more can be.
                bool more = true;
                while (more)
                {
                    more = false;
                    foreach (LayoutState state in layout.states)
                    {
                        if (builtStates.Contains(state.name) || !Meets(state.requires, ignoreStates: false)) { continue; }
                        builtStates.Add(state.name);
                        more = true;
                    }
                }
                // A state's requirement on another left out (missingStates) is only checked once every state is known.
                builtStates.RemoveWhere(name => !Meets(layout.states.First(s => s.name == name).requires, ignoreStates: false));
                foreach (LayoutState state in layout.states.Where(s => builtStates.Contains(s.name)))
                {
                    LayoutMotion? chosen = null;
                    foreach (LayoutMotion motion in state.motions ?? System.Array.Empty<LayoutMotion>())
                    {
                        if (Meets(motion.requires, ignoreStates: false) && MotionWorks(motion.motion)) { chosen = motion; break; }
                    }
                    if (chosen == null)
                    {
                        report.Append($"  {state.name}: none of its motions' clips are there, so it has no motion\n");
                        continue;
                    }
                    chosenMotion[state.name] = chosen.Value;
                    report.Append($"  {state.name}: {chosen.Value.motion}\n");
                }
                if (!builtStates.Contains(layout.defaultState)) { problem = $"its default state, '{layout.defaultState}', isn't built."; return false; }
                foreach (LayoutTransition transition in layout.transitions)
                {
                    bool fromKnown = transition.from == AnimatorLayout.AnyState || layout.states.Any(s => s.name == transition.from);
                    if (!fromKnown || !layout.states.Any(s => s.name == transition.to))
                    {
                        problem = $"a transition from '{transition.from}' to '{transition.to}' names a state the layout hasn't.";
                        return false;
                    }
                }
                return true;
            }

            /// <summary>Whether what the requirement asks for is there (its states' part left out while states are still being found).</summary>
            private bool Meets(LayoutRequirement requirement, bool ignoreStates)
            {
                if (requirement.clips != null && requirement.clips.Any(c => !clips.ContainsKey(c))) { return false; }
                if (requirement.missingClips != null && requirement.missingClips.Any(clips.ContainsKey)) { return false; }
                if (ignoreStates) { return true; }
                if (requirement.states != null && requirement.states.Any(s => !builtStates.Contains(s))) { return false; }
                if (requirement.missingStates != null && requirement.missingStates.Any(builtStates.Contains)) { return false; }
                return true;
            }

            /// <summary>Whether a motion (a clip slot, or a tree) can be made: its clips are there and its numbers measure.</summary>
            private bool MotionWorks(string motion)
            {
                if (string.IsNullOrEmpty(motion)) { return false; }
                if (!motion.StartsWith(AnimatorLayout.TreePrefix)) { return clips.ContainsKey(motion); }
                LayoutBlendTree? tree = Tree(motion);
                if (tree == null) { return false; }
                foreach (LayoutChild child in tree.Value.children ?? System.Array.Empty<LayoutChild>())
                {
                    if (!Meets(child.requires, ignoreStates: false)) { continue; }
                    if (!MotionWorks(child.motion)) { return false; }
                    bool twoD = tree.Value.type != LayoutBlendType.Simple1D;
                    if (!(twoD ? Value(child.positionScale, 1f).ok : Value(child.threshold, 0f).ok) || !Value(child.cycleOffset, 0f).ok) { return false; }
                }
                return true;
            }

            private LayoutBlendTree? Tree(string motion)
            {
                string name = motion.Substring(AnimatorLayout.TreePrefix.Length);
                foreach (LayoutBlendTree tree in layout.blendTrees) { if (tree.name == name) { return tree; } }
                return null;
            }

            /// <summary>
            /// A layout number: given, or measured (then multiplied, inverted and eased as asked), or its fallback if the
            /// measurement fails, or not ok. A given number left at 0 is empty, and gives empty (0 for most, 1 for a speed).
            /// </summary>
            private (bool ok, float value) Value(LayoutValue value, float empty)
            {
                float v;
                if (value.kind == LayoutValueKind.Constant)
                {
                    v = value.constant;
                    if (v == 0f && empty != 0f) { return (true, empty); }
                }
                else
                {
                    // A fallback stands in for what would have been measured: it's multiplied, inverted and eased the same.
                    (bool ok, float raw) = Measure(value);
                    if (!ok && !value.hasFallback) { return (false, 0f); }
                    v = ok ? raw : value.fallback;
                }
                if (value.multiply != 0f) { v *= value.multiply; }
                if (value.reciprocal) { v = 1f / v; }
                if (value.lerp != 0f) { v = Mathf.Lerp(v, value.lerpTo, value.lerp); }
                return (true, v);
            }

            private (bool ok, float value) Measure(LayoutValue value)
            {
                var key = (value.kind, value.clip ?? "", value.direction);
                if (measured.TryGetValue(key, out (bool, float) known)) { return Valid(value, known); }
                (bool, float) result = (false, 0f);
                if (layout.measuringModel != null && clips.TryGetValue(value.clip ?? "", out AnimationClip clip))
                {
                    GameObject model = layout.measuringModel;
                    switch (value.kind)
                    {
                        case LayoutValueKind.StrideSpeed: result = (true, MotionCaptureLocomotion.StrideSpeed(model, clip)); break;
                        case LayoutValueKind.TouchDownPhase: result = (true, MotionCaptureLocomotion.TouchDownPhase(model, clip)); break;
                        case LayoutValueKind.ClimbSpeed:
                            result = (true, MotionCaptureLocomotion.ClimbSpeed(model, clip, new Vector3(value.direction.x, value.direction.y, 0f)));
                            break;
                        case LayoutValueKind.TakeOffPhase:
                            result = MotionCaptureLocomotion.TryFindFlight(model, clip, out int takeOff, out _, out _)
                                ? (true, takeOff / (clip.length * clip.frameRate)) : (false, 0f);
                            break;
                    }
                    report.Append($"    measured {value.kind} of {clip.name}{(value.kind == LayoutValueKind.ClimbSpeed ? $" {value.direction}" : "")}: " +
                                  $"{result.Item2:0.000}\n");
                }
                measured[key] = result;
                return Valid(value, result);
            }

            // A measurement counts if it's a number, and (for one with a Minimum) above it.
            private static (bool, float) Valid(LayoutValue value, (bool ok, float v) result) =>
                result.ok && float.IsFinite(result.v) && (value.minimum <= 0f || result.v > value.minimum) ? (true, result.v) : (false, 0f);

            // ---- Making the controller ----

            private AnimatorController controller;
            private readonly Dictionary<string, BlendTree> trees = new();

            public void Into(AnimatorController target)
            {
                controller = target;
                Empty(target);
                foreach (LayoutParameter parameter in layout.parameters)
                {
                    if (Meets(parameter.requires, ignoreStates: false)) { AddParameter(parameter); }
                }
                AnimatorStateMachine machine = target.layers[0].stateMachine;
                var states = new Dictionary<string, AnimatorState>();
                foreach (LayoutState spec in layout.states.Where(s => builtStates.Contains(s.name)))
                {
                    AnimatorState state = machine.AddState(spec.name);
                    states[spec.name] = state;
                    if (!chosenMotion.TryGetValue(spec.name, out LayoutMotion motion)) { continue; }
                    state.motion = MakeMotion(motion.motion);
                    (bool speedOk, float speed) = Value(motion.speed, 1f);
                    if (!string.IsNullOrEmpty(motion.speedParameter) && speedOk)
                    {
                        state.speedParameterActive = true;
                        state.speedParameter = motion.speedParameter;
                    }
                    state.speed = speedOk ? speed : 1f;
                    if (!string.IsNullOrEmpty(motion.timeParameter))
                    {
                        state.timeParameterActive = true;
                        state.timeParameter = motion.timeParameter;
                    }
                    state.cycleOffset = Value(motion.cycleOffset, 0f).value;
                    foreach (LayoutParameter parameter in motion.parameters ?? System.Array.Empty<LayoutParameter>())
                    {
                        if (Meets(parameter.requires, ignoreStates: false)) { AddParameter(parameter); }
                    }
                }
                machine.defaultState = states[layout.defaultState];

                foreach (LayoutTransition spec in layout.transitions)
                {
                    if (!Meets(spec.requires, ignoreStates: false) || !states.TryGetValue(spec.to, out AnimatorState to)) { continue; }
                    AnimatorStateTransition transition;
                    if (spec.from == AnimatorLayout.AnyState)
                    {
                        transition = machine.AddAnyStateTransition(to);
                        transition.canTransitionToSelf = spec.canTransitionToSelf;
                    }
                    else if (states.TryGetValue(spec.from, out AnimatorState from)) { transition = from.AddTransition(to); }
                    else { continue; }
                    transition.hasExitTime = spec.hasExitTime;
                    if (spec.hasExitTime) { transition.exitTime = Value(spec.exitTime, 0f).value; }
                    transition.hasFixedDuration = !spec.durationIsShare;
                    transition.duration = spec.duration;
                    foreach (LayoutCondition condition in spec.conditions ?? System.Array.Empty<LayoutCondition>())
                    {
                        if (Meets(condition.requires, ignoreStates: false)) { transition.AddCondition((AnimatorConditionMode)ConditionMode(condition.mode), condition.threshold, condition.parameter); }
                    }
                }

                // OnAnimatorIK (and so CharacterIK) only runs on layers with IK Pass enabled. Layers are returned by
                // copy, so modify the array and assign it back.
                AnimatorControllerLayer[] layers = target.layers;
                layers[0].iKPass = layout.ikPass;
                target.layers = layers;
                EditorUtility.SetDirty(target);
            }

            private static int ConditionMode(LayoutConditionMode mode) => mode switch
            {
                LayoutConditionMode.If => (int)AnimatorConditionMode.If,
                LayoutConditionMode.IfNot => (int)AnimatorConditionMode.IfNot,
                LayoutConditionMode.Greater => (int)AnimatorConditionMode.Greater,
                LayoutConditionMode.Less => (int)AnimatorConditionMode.Less,
                LayoutConditionMode.Equals => (int)AnimatorConditionMode.Equals,
                _ => (int)AnimatorConditionMode.NotEqual,
            };

            /// <summary>Adds a parameter, or (a float the controller has) sets its default.</summary>
            private void AddParameter(LayoutParameter spec)
            {
                AnimatorControllerParameter[] parameters = controller.parameters;
                for (int i = 0; i < parameters.Length; i++)
                {
                    if (parameters[i].name != spec.name) { continue; }
                    if (spec.type == AnimatorControllerParameterType.Float) { parameters[i].defaultFloat = Value(spec.defaultFloat, 0f).value; }
                    controller.parameters = parameters;
                    return;
                }
                controller.AddParameter(new AnimatorControllerParameter
                {
                    name = spec.name,
                    type = spec.type,
                    defaultBool = spec.defaultBool,
                    defaultFloat = spec.type == AnimatorControllerParameterType.Float ? Value(spec.defaultFloat, 0f).value : 0f,
                });
            }

            /// <summary>A clip, or a blend tree (made once, and shared by every motion naming it).</summary>
            private Motion MakeMotion(string motion)
            {
                if (!motion.StartsWith(AnimatorLayout.TreePrefix)) { return clips[motion]; }
                if (trees.TryGetValue(motion, out BlendTree made)) { return made; }
                LayoutBlendTree spec = Tree(motion).Value;
                var tree = new BlendTree
                {
                    name = spec.name,
                    blendType = spec.type switch
                    {
                        LayoutBlendType.FreeformDirectional2D => BlendTreeType.FreeformDirectional2D,
                        LayoutBlendType.FreeformCartesian2D => BlendTreeType.FreeformCartesian2D,
                        LayoutBlendType.SimpleDirectional2D => BlendTreeType.SimpleDirectional2D,
                        _ => BlendTreeType.Simple1D,
                    },
                    blendParameter = spec.parameter,
                    hideFlags = HideFlags.HideInHierarchy,
                };
                if (spec.type == LayoutBlendType.Simple1D) { tree.useAutomaticThresholds = false; }
                else { tree.blendParameterY = spec.parameterY; }
                trees[motion] = tree;
                var children = (spec.children ?? System.Array.Empty<LayoutChild>()).Where(c => Meets(c.requires, ignoreStates: false)).ToArray();
                foreach (LayoutChild child in children)
                {
                    Motion childMotion = MakeMotion(child.motion);
                    if (spec.type == LayoutBlendType.Simple1D) { tree.AddChild(childMotion, Value(child.threshold, 0f).value); }
                    else { tree.AddChild(childMotion, child.position * Value(child.positionScale, 1f).value); }
                }
                // Playback speeds and cycle offsets can only be set on the children array, which is returned by copy.
                ChildMotion[] motions = tree.children;
                for (int i = 0; i < motions.Length; i++)
                {
                    motions[i].timeScale = children[i].timeScale != 0f ? children[i].timeScale : 1f;
                    motions[i].cycleOffset = Value(children[i].cycleOffset, 0f).value;
                }
                tree.children = motions;
                AssetDatabase.AddObjectToAsset(tree, controller);
                return tree;
            }

            /// <summary>
            /// Empties the controller of its parameters, layers, states and blend trees, in place: every model prefab refers
            /// to it, and deleting it would leave them without one until it was made again (with a new GUID they wouldn't find).
            /// </summary>
            private static void Empty(AnimatorController controller)
            {
                string path = AssetDatabase.GetAssetPath(controller);
                foreach (Object part in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (part != null && part != controller) { Object.DestroyImmediate(part, true); }
                }
                controller.parameters = System.Array.Empty<AnimatorControllerParameter>();
                controller.layers = System.Array.Empty<AnimatorControllerLayer>();
                controller.AddLayer("Base Layer");
            }
        }
    }
}
