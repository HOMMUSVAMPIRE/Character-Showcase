using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace MeridianMotion.EditorTools
{
    /// <summary>
    /// An Animator Controller described as text, for telling whether two controllers work the same: its parameters and
    /// their defaults, and each layer's states (sorted by name), their motions (clips, and blend trees with every child's
    /// threshold, position, speed and offset), their settings, and the transitions out of each (in their order, which is
    /// their priority: the first whose conditions hold is taken), with their timings and conditions. Where the states sit
    /// in the graph, and the order they were added in, are left out: they don't change what the controller does.
    /// Floats are written exactly (round-trip), so a difference in the last bit shows.
    /// </summary>
    public static class AnimatorControllerDescription
    {
        /// <summary>The controller described (see the class).</summary>
        public static string Describe(AnimatorController controller)
        {
            var text = new StringBuilder();
            text.Append("controller ").Append(controller.name).Append('\n');
            foreach (AnimatorControllerParameter parameter in controller.parameters.OrderBy(p => p.name, System.StringComparer.Ordinal))
            {
                text.Append("parameter ").Append(parameter.name).Append(' ').Append(parameter.type).Append(" default=");
                text.Append(parameter.type switch
                {
                    AnimatorControllerParameterType.Float => F(parameter.defaultFloat),
                    AnimatorControllerParameterType.Int => parameter.defaultInt.ToString(CultureInfo.InvariantCulture),
                    AnimatorControllerParameterType.Bool => parameter.defaultBool ? "true" : "false",
                    _ => "-",
                }).Append('\n');
            }
            foreach (AnimatorControllerLayer layer in controller.layers)
            {
                text.Append("layer ").Append(layer.name).Append(" weight=").Append(F(layer.defaultWeight)).Append(" ikPass=").Append(layer.iKPass)
                    .Append(" blending=").Append(layer.blendingMode).Append(" default=").Append(layer.stateMachine.defaultState?.name ?? "-").Append('\n');
                DescribeMachine(text, layer.stateMachine, "  ");
            }
            return text.ToString();
        }

        private static void DescribeMachine(StringBuilder text, AnimatorStateMachine machine, string indent)
        {
            foreach (ChildAnimatorState child in machine.states.OrderBy(s => s.state.name, System.StringComparer.Ordinal))
            {
                AnimatorState state = child.state;
                text.Append(indent).Append("state ").Append(state.name)
                    .Append(" speed=").Append(F(state.speed)).Append(state.speedParameterActive ? " speedParameter=" + state.speedParameter : "")
                    .Append(" cycleOffset=").Append(F(state.cycleOffset)).Append(state.cycleOffsetParameterActive ? " cycleOffsetParameter=" + state.cycleOffsetParameter : "")
                    .Append(state.timeParameterActive ? " timeParameter=" + state.timeParameter : "")
                    .Append(" mirror=").Append(state.mirror).Append(" footIK=").Append(state.iKOnFeet).Append(" writeDefaults=").Append(state.writeDefaultValues)
                    .Append(string.IsNullOrEmpty(state.tag) ? "" : " tag=" + state.tag).Append('\n');
                DescribeMotion(text, state.motion, indent + "    ");
                foreach (AnimatorStateTransition transition in state.transitions) { DescribeTransition(text, transition, indent + "  ", "to"); }
            }
            foreach (AnimatorStateTransition transition in machine.anyStateTransitions) { DescribeTransition(text, transition, indent, "any state to"); }
            foreach (AnimatorTransition transition in machine.entryTransitions)
            {
                text.Append(indent).Append("entry to ").Append(transition.destinationState?.name ?? "-").Append(Conditions(transition.conditions)).Append('\n');
            }
            foreach (ChildAnimatorStateMachine sub in machine.stateMachines.OrderBy(m => m.stateMachine.name, System.StringComparer.Ordinal))
            {
                text.Append(indent).Append("state machine ").Append(sub.stateMachine.name).Append('\n');
                DescribeMachine(text, sub.stateMachine, indent + "  ");
            }
        }

        private static void DescribeTransition(StringBuilder text, AnimatorStateTransition transition, string indent, string what)
        {
            string to = transition.destinationState != null ? transition.destinationState.name
                : transition.destinationStateMachine != null ? "machine " + transition.destinationStateMachine.name
                : transition.isExit ? "exit" : "-";
            text.Append(indent).Append(what).Append(' ').Append(to)
                .Append(" exitTime=").Append(transition.hasExitTime ? F(transition.exitTime) : "no")
                .Append(" duration=").Append(F(transition.duration)).Append(transition.hasFixedDuration ? "s" : "x")
                .Append(" offset=").Append(F(transition.offset))
                .Append(" interruption=").Append(transition.interruptionSource).Append(transition.orderedInterruption ? "/ordered" : "")
                .Append(" toSelf=").Append(transition.canTransitionToSelf)
                .Append(transition.mute ? " mute" : "").Append(transition.solo ? " solo" : "")
                .Append(Conditions(transition.conditions)).Append('\n');
        }

        private static string Conditions(AnimatorCondition[] conditions)
        {
            if (conditions.Length == 0) { return ""; }
            return " when " + string.Join(" and ", conditions.Select(c => c.mode switch
            {
                AnimatorConditionMode.If => c.parameter,
                AnimatorConditionMode.IfNot => "!" + c.parameter,
                AnimatorConditionMode.Greater => c.parameter + ">" + F(c.threshold),
                AnimatorConditionMode.Less => c.parameter + "<" + F(c.threshold),
                AnimatorConditionMode.Equals => c.parameter + "==" + F(c.threshold),
                AnimatorConditionMode.NotEqual => c.parameter + "!=" + F(c.threshold),
                _ => c.mode + " " + c.parameter,
            }));
        }

        private static void DescribeMotion(StringBuilder text, Motion motion, string indent)
        {
            switch (motion)
            {
                case null:
                    text.Append(indent).Append("motion none\n");
                    break;
                case AnimationClip clip:
                    text.Append(indent).Append("clip ").Append(clip.name).Append(" (").Append(AssetDatabase.GetAssetPath(clip)).Append(") loop=")
                        .Append(clip.isLooping).Append('\n');
                    break;
                case BlendTree tree:
                    text.Append(indent).Append("blend tree ").Append(tree.name).Append(' ').Append(tree.blendType).Append(" by ").Append(tree.blendParameter)
                        .Append(tree.blendType == BlendTreeType.Simple1D ? "" : ", " + tree.blendParameterY)
                        .Append(tree.useAutomaticThresholds ? " automatic" : "").Append('\n');
                    foreach (ChildMotion child in tree.children)
                    {
                        text.Append(indent).Append("  child")
                            .Append(tree.blendType == BlendTreeType.Simple1D ? " threshold=" + F(child.threshold) : $" position=({F(child.position.x)}, {F(child.position.y)})")
                            .Append(" timeScale=").Append(F(child.timeScale)).Append(" cycleOffset=").Append(F(child.cycleOffset))
                            .Append(" mirror=").Append(child.mirror).Append('\n');
                        DescribeMotion(text, child.motion, indent + "    ");
                    }
                    break;
                default:
                    text.Append(indent).Append("motion ").Append(motion.name).Append('\n');
                    break;
            }
        }

        private static string F(float value) => value.ToString("R", CultureInfo.InvariantCulture);

        /// <summary>
        /// For -executeMethod: writes the description of each controller given by -controller [path] (more than one with
        /// commas) to -out [folder], one file each named for the controller, then exits.
        /// </summary>
        public static void DescribeFromCommandLine()
        {
            string paths = CommandLine.Argument("-controller"), folder = CommandLine.Argument("-out");
            bool succeeded = paths != null && folder != null;
            if (succeeded)
            {
                Directory.CreateDirectory(folder);
                foreach (string path in paths.Split(','))
                {
                    var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path.Trim());
                    if (controller == null) { Debug.LogError($"[{nameof(AnimatorControllerDescription)}] No Animator Controller at '{path}'."); succeeded = false; continue; }
                    File.WriteAllText(Path.Combine(folder, controller.name + ".txt"), Describe(controller));
                }
            }
            else { Debug.LogError($"[{nameof(AnimatorControllerDescription)}] Give -controller [path] and -out [folder]."); }
            CommandLine.Finish(succeeded);
        }
    }
}
