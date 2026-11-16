using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Character
{
    /// <summary>
    /// A saved movement tuning: settings of a movement mode's components (some or all of them, on/off switches included),
    /// to apply to a mode again. Saved and edited in the editor (Tools: the preset's inspector); applied there, or to a
    /// character's own copy of its mode at runtime. A preset only sets values: it never adds, removes or reorders a mode's
    /// components, and a setting of a component the mode doesn't have is left out (and reported). Settings are found by
    /// their field names (see MovementSettings).
    /// </summary>
    [CreateAssetMenu(fileName = "MovementTuningPreset", menuName = "Scriptable Objects/MovementModes/Tuning Preset")]
    public class MovementTuningPreset : ScriptableObject
    {
        /// <summary>One component's part of a preset: its settings' values, and which of them the preset sets.</summary>
        [Serializable]
        public class Part
        {
            [Tooltip("The component's settings as saved. Only those listed under Settings are applied")]
            [SerializeReference] public MovementComponent values;
            [Tooltip("The settings (field names) this preset sets on the component; the rest are left as they are")]
            public List<string> settings = new();
        }

        [Tooltip("Notes for designers: what this tuning is for, what it changes")]
        [TextArea] [SerializeField] private string notes;
        [Tooltip("The components whose settings this preset sets, and which settings")]
        [SerializeField] private List<Part> parts = new();

        /// <summary>The preset's parts, one per component it sets settings of.</summary>
        public IReadOnlyList<Part> Parts => parts;

        /// <summary>
        /// Replaces the preset's contents with the mode's tuning now: every setting of every component (or only the
        /// given component types), so applying it gives those components exactly these values again.
        /// </summary>
        public void Capture(ComposedMovementMode mode, ICollection<Type> onlyComponents = null)
        {
            parts.Clear();
            foreach (MovementComponent component in mode.Components)
            {
                if (component == null || (onlyComponents != null && !onlyComponents.Contains(component.GetType()))) { continue; }
                var part = new Part { values = MovementSettings.Clone(component) };
                foreach (FieldInfo setting in MovementSettings.Of(component.GetType())) { part.settings.Add(setting.Name); }
                parts.Add(part);
            }
        }

        /// <summary>
        /// Sets the preset's settings on the mode's components (turned on or off), returning how many changed. What it
        /// can't set (a component the mode doesn't have, a setting its component no longer has) is added to problems.
        /// </summary>
        public int ApplyTo(ComposedMovementMode mode, List<string> problems = null)
        {
            int changed = 0;
            foreach (Part part in parts)
            {
                if (part?.values == null) { problems?.Add($"{name}: a part's component type is missing (renamed or deleted?)"); continue; }
                Type type = part.values.GetType();
                MovementComponent target = mode.Get(type, includeDisabled: true);
                if (target == null) { problems?.Add($"{name}: {mode.name} has no {MovementComponent.DisplayNameOf(type)}, so its settings were left out"); continue; }
                foreach (string settingName in part.settings)
                {
                    FieldInfo setting = MovementSettings.Find(type, settingName);
                    if (setting == null) { problems?.Add($"{name}: {MovementComponent.DisplayNameOf(type)} has no setting '{settingName}' any more"); continue; }
                    if (MovementSettings.Copy(setting, part.values, target)) { changed++; }
                }
            }
            return changed;
        }

        /// <summary>The part for a component type, if the preset has one.</summary>
        public Part PartFor(Type componentType)
        {
            foreach (Part part in parts) { if (part?.values != null && part.values.GetType() == componentType) { return part; } }
            return null;
        }

        /// <summary>
        /// Includes or leaves out one setting of a component type, at the value it has in the preset (adding a part for
        /// the component, at its defaults, if the preset has none).
        /// </summary>
        public void Include(Type componentType, string setting, bool include)
        {
            if (MovementSettings.Find(componentType, setting) == null) { throw new ArgumentException($"{componentType.Name} has no setting '{setting}'."); }
            Part part = PartFor(componentType);
            if (part == null)
            {
                if (!include) { return; }
                part = new Part { values = (MovementComponent)Activator.CreateInstance(componentType) };
                parts.Add(part);
            }
            if (include && !part.settings.Contains(setting)) { part.settings.Add(setting); }
            else if (!include) { part.settings.Remove(setting); }
        }
    }
}
