using System;
using System.Collections.Generic;
using System.Linq;
using MeridianMotion.Characters;
using UnityEditor.Animations;
using UnityEditor;
using UnityEngine;

namespace MeridianMotion.EditorTools
{
    /// <summary>
    /// The Character Doctor's checks of a character prefab (one with an MMotionCharacter): each says whether it
    /// passes, warns or fails, why it matters, and, where a tool can fix it, how. They cover what goes wrong with a new
    /// model and shows up only in play otherwise: its model source and prefab, its rig, its measurements (height, the
    /// ground, the facing), its hitboxes, its body, its eye point, its translator's parameters, whether every ability its
    /// movement has turned on shows on it, and whether it's been prepared since its file changed.
    /// </summary>
    public static class CharacterChecks
    {
        public enum CheckStatus { Pass, Warn, Fail }

        /// <summary>One check's outcome: what, how it went, why it matters, and a fix (null without one).</summary>
        public sealed class Check
        {
            public string name;
            public CheckStatus status;
            public string message;
            public string fixLabel;
            public Action fix;

            public override string ToString() => $"[{status}] {name}: {message}";
        }

        /// <summary>
        /// Which character state each of the animation state's own bools shows (AnimatorParameterTranslator.AnimationBools),
        /// so a translator showing one of them shows that ability's state.
        /// </summary>
        private static readonly Dictionary<string, CharacterState> AnimationBoolStates = new()
        {
            ["Grounded"] = CharacterState.Grounded, ["Jumping"] = CharacterState.Jumping, ["Dashing"] = CharacterState.Dashing,
            ["Moving"] = CharacterState.Moving, ["Crouching"] = CharacterState.Crouching, ["Sliding"] = CharacterState.Sliding,
            ["Rolling"] = CharacterState.Rolling, ["Wall Running"] = CharacterState.WallRunning, ["Wall Sliding"] = CharacterState.WallSliding,
            ["Wall Climbing"] = CharacterState.WallClimbing, ["Climb Entering"] = CharacterState.WallClimbing, ["On Ladder"] = CharacterState.WallClimbing,
            ["Climb Exiting"] = CharacterState.WallClimbing, ["Hanging"] = CharacterState.LedgeHanging, ["Climbing"] = CharacterState.LedgeClimbing,
            ["Vaulting"] = CharacterState.Vaulting, ["Grabbing"] = CharacterState.Grabbing, ["Rope Swinging"] = CharacterState.RopeSwinging,
            ["Ziplining"] = CharacterState.Ziplining, ["Swimming"] = CharacterState.Swimming, ["Diving"] = CharacterState.Diving,
        };

        /// <summary>Every character prefab in the project (with an MMotionCharacter on its root), by path.</summary>
        public static List<string> CharacterPrefabs() =>
            AssetDatabase.FindAssets("t:Prefab").Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => AssetDatabase.LoadAssetAtPath<GameObject>(path)?.GetComponent<MMotionCharacter>() != null)
                .OrderBy(path => path, StringComparer.Ordinal).ToList();

        /// <summary>The checks of a character prefab, in order.</summary>
        public static List<Check> Run(GameObject prefab)
        {
            var checks = new List<Check>();
            void Add(string name, CheckStatus result, string message, string fixLabel = null, Action fix = null) =>
                checks.Add(new Check { name = name, status = result, message = message, fixLabel = fixLabel, fix = fix });

            var movement = prefab.GetComponent<MMotionMotor>();
            var host = prefab.GetComponent<CharacterModelSlot>();
            if (movement == null || host == null)
            {
                Add("A character", CheckStatus.Fail, "It needs a MMotionMotor and a CharacterModelSlot (make it as a variant of Character.prefab).");
                return checks;
            }

            CharacterModelSource source = host.ModelSource;
            if (source == null)
            {
                Add("Its model source", CheckStatus.Fail, "Its CharacterModelSlot has no Model Source, so it shows the placeholder (and its fallback). " +
                    "Choose a model's source (Prepare Humanoid Models makes one beside each model, \"[name].asset\").");
                return checks;
            }
            Add("Its model source", CheckStatus.Pass, $"{source.name}.");
            GameObject model = (source as PrefabModelSource)?.ModelPrefab;
            if (model == null)
            {
                Add("Its model", CheckStatus.Fail, $"{source.name} names no model prefab to load. Run Prepare Humanoid Models, which sets it.", "Prepare Humanoid Models", HumanoidModelImporter.PrepareAllFromMenu);
                return checks;
            }

            // The rig: a Humanoid avatar for the shared, humanoid animation; a Generic rig plays a controller made for it.
            var animator = model.GetComponentInChildren<Animator>();
            RuntimeAnimatorController controller = source.AnimatorController != null ? source.AnimatorController : animator != null ? animator.runtimeAnimatorController : null;
            Avatar avatar = source.Avatar != null ? source.Avatar : animator != null ? animator.avatar : null;
            bool sharedController = controller != null && AssetDatabase.GetAssetPath(BaseOf(controller)) == HumanoidModelImporter.ControllerPath;
            if (animator == null) { Add("Its rig", CheckStatus.Fail, "Its model prefab has no Animator, so it can't be animated. Run Prepare Humanoid Models.", "Prepare Humanoid Models", HumanoidModelImporter.PrepareAllFromMenu); }
            else if (avatar != null && avatar.isHuman) { Add("Its rig", CheckStatus.Pass, "A Humanoid, so it plays the humanoid animation."); }
            else if (sharedController)
            {
                Add("Its rig", CheckStatus.Fail, "It plays the humanoid animation, but Unity couldn't map its skeleton to a Humanoid: select the model file, " +
                    "open the Rig tab, set Animation Type to Humanoid, press Configure to check the bones, then Apply and run Prepare Humanoid Models.");
            }
            else { Add("Its rig", CheckStatus.Pass, "A Generic rig, with an Animator Controller of its own."); }

            // Its measurements: height (in range, or its units were lost), the ground offset and the facing.
            if (!source.HasMeasurements)
            {
                Add("Its measurements", source.MeasureGroundOnLoad ? CheckStatus.Warn : CheckStatus.Fail,
                    source.MeasureGroundOnLoad ? "Not measured yet: it'll be measured as it loads (Measure Ground On Load), which costs a moment then. Prepare Humanoid Models measures it ahead."
                                               : "Not measured, and not measured on load: its feet may float or sink. Run Prepare Humanoid Models.",
                    "Prepare Humanoid Models", HumanoidModelImporter.PrepareAllFromMenu);
            }
            else
            {
                float height = source.MeasuredHeight;
                bool inRange = height >= HumanoidModelImporter.MinHeight && height <= HumanoidModelImporter.MaxHeight;
                Add("Its measurements", inRange ? CheckStatus.Pass : CheckStatus.Warn,
                    (inRange ? $"{height:0.00} m tall" : $"{height:0.00} m tall, which is outside what a person is (its units may have been lost): check its source's Scale and Height") +
                    $", its soles {source.GroundOffset * 100f:0.0} cm off its origin" +
                    (Mathf.Abs(source.FacingCorrection) > 0.5f ? $", turned {source.FacingCorrection:0}° to face forward." : ", facing forward as it is."));
            }

            // Hitboxes.
            var hitboxes = model.GetComponentInChildren<CharacterHitboxes>(true);
            if (hitboxes == null || hitboxes.Hitboxes.Count == 0)
            {
                Add("Its hitboxes", CheckStatus.Warn, "It has no hitboxes, so nothing in the level meets it (the movement doesn't need them).", "Fit Hitboxes", () => HitboxFitter.FitAll(out _));
            }
            else { Add("Its hitboxes", CheckStatus.Pass, $"{hitboxes.Hitboxes.Count} hitboxes, on its bones."); }

            // Its body.
            switch (host.BodyFit)
            {
                case BodyFit.ModelsBody when source.BodyDefinition == null:
                    Add("Its body", CheckStatus.Warn, "Body Fit takes its model's body, but its source has none yet (Prepare Humanoid Models fits one): it's fitted as the model loads instead.",
                        "Prepare Humanoid Models", HumanoidModelImporter.PrepareAllFromMenu);
                    break;
                case BodyFit.ModelsBody:
                    Add("Its body", CheckStatus.Pass, $"Its model's own ({source.BodyDefinition.name}).");
                    break;
                case BodyFit.Height:
                    Add("Its body", CheckStatus.Pass, "Its own, scaled to its model's height.");
                    break;
                default:
                    Add("Its body", movement.BodyDefinition != null ? CheckStatus.Pass : CheckStatus.Warn,
                        movement.BodyDefinition != null ? $"Its own, as authored ({movement.BodyDefinition.name})." : "Its own, as its MMotionMotor's body is authored (no Body Definition).");
                    break;
            }

            // Its eye point: the first-person camera's.
            bool eyes = model.GetComponentsInChildren<Transform>(true).Any(t => t.name == CharacterModelSlot.EyesName)
                        || (avatar != null && avatar.isHuman);
            Add("Its eye point", eyes ? CheckStatus.Pass : CheckStatus.Warn, eyes ? "Found (its eyes, or a Humanoid's head)."
                : $"No Humanoid head and no child named {CharacterModelSlot.EyesName}: the first-person view is at its default height. Add an empty child named {CharacterModelSlot.EyesName} at its eyes.");

            // Its translator's parameters, against its controller.
            var translator = source.Translator as AnimatorParameterTranslator;
            var built = BaseOf(controller) as AnimatorController;
            if (translator == null || built == null)
            {
                Add("Its animation", translator == null && source.Translator != null ? CheckStatus.Pass : CheckStatus.Warn,
                    translator == null && source.Translator != null ? "Driven by a translator of its own kind (not checked here)." : "It has no Animator Parameter Translator or Animator Controller to check.");
                return checks;
            }
            var parameters = new HashSet<string>(built.parameters.Select(p => p.name));
            var missing = translator.FloatParameters.Select(f => f.parameter).Concat(translator.BoolParameters.Select(b => b.parameter))
                .Concat(translator.PlaybackParameters.Select(p => p.parameter)).Where(p => !string.IsNullOrEmpty(p) && !parameters.Contains(p)).Distinct().ToList();
            // A translator setting parameters its controller hasn't is fine (they're skipped: one translator serves several
            // controllers); one setting none of them is the wrong translator for it.
            int named = translator.FloatParameters.Select(f => f.parameter).Concat(translator.BoolParameters.Select(b => b.parameter))
                .Concat(translator.PlaybackParameters.Select(p => p.parameter)).Where(p => !string.IsNullOrEmpty(p)).Distinct().Count();
            bool noneThere = named > 0 && missing.Count == named;
            Add("Its translator's parameters", noneThere ? CheckStatus.Fail : CheckStatus.Pass, noneThere
                ? $"None of the parameters {translator.name} sets are in its controller: it's the wrong translator for it (its source's Translator)."
                : missing.Count == 0 ? $"Every parameter {translator.name} sets is in its controller."
                : $"{translator.name} sets {missing.Count} parameters its controller hasn't, which are skipped (one translator serves several controllers): {string.Join(", ", missing)}.");

            // Every ability its movement has turned on shows: a bool the translator sets from its state, which a transition reads.
            var mode = movement.movementMode as AbilityMovementMode;
            if (mode != null)
            {
                var read = new HashSet<string>(built.layers.SelectMany(l => AllTransitions(l.stateMachine)).SelectMany(t => t.conditions).Select(c => c.parameter));
                var shown = new HashSet<CharacterState>();
                foreach (AnimatorParameterTranslator.BoolParameter entry in translator.BoolParameters)
                {
                    if (!read.Contains(entry.parameter) || !AnimatorParameterTranslator.TryResolve(entry.source, out int animationBool, out CharacterState state)) { continue; }
                    if (animationBool >= 0 && AnimationBoolStates.TryGetValue(AnimatorParameterTranslator.AnimationBools[animationBool].name, out CharacterState shows)) { shown.Add(shows); }
                    else if (animationBool < 0) { shown.Add(state); }
                }
                var unshown = new List<string>();
                foreach (MovementComponent component in mode.Components.Where(c => c != null && c.Enabled))
                {
                    foreach (StateEventPair pair in component.StateEvents)
                    {
                        if (shown.Contains(pair.state) || (component.LocomotionState & pair.state) != 0 || !component.AllowsState(pair.state)) { continue; }
                        unshown.Add($"{MovementComponent.DisplayNameOf(component.GetType())} ({pair.state})");
                    }
                }
                Add("Its abilities' animation", unshown.Count == 0 ? CheckStatus.Pass : CheckStatus.Warn, unshown.Count == 0
                    ? "Every ability its movement has turned on has an animation state."
                    : $"Turned on in {mode.name} but with no animation state of their own on it: {string.Join(", ", unshown)}. They play its other animation (walking, say). " +
                      "Give them a state in its layout, or turn them off on its movement mode (its own, not the shared one).");
            }

            // Prepared since its file changed (models in Assets/Models/Humanoid).
            string file = AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromOriginalSource(model));
            if (!string.IsNullOrEmpty(file) && file.StartsWith(HumanoidModelImporter.ModelsFolder + "/") && HumanoidModelImporter.HasChangedSincePrepared(file))
            {
                Add("Prepared", CheckStatus.Warn, $"{file} (or the animation it plays) has changed since it was prepared.", "Prepare Humanoid Models", HumanoidModelImporter.PrepareAllFromMenu);
            }
            else { Add("Prepared", CheckStatus.Pass, "Up to date."); }
            return checks;
        }

        private static RuntimeAnimatorController BaseOf(RuntimeAnimatorController controller) =>
            controller is AnimatorOverrideController overrides ? BaseOf(overrides.runtimeAnimatorController) : controller;

        private static IEnumerable<AnimatorStateTransition> AllTransitions(AnimatorStateMachine machine) =>
            machine.anyStateTransitions.Concat(machine.states.SelectMany(s => s.state.transitions))
                .Concat(machine.stateMachines.SelectMany(m => AllTransitions(m.stateMachine)));
    }
}
