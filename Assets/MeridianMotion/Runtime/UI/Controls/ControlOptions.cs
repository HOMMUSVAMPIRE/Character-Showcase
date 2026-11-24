using System;
using System.Collections.Generic;

namespace MeridianMotion.UI
{
    /// <summary>
    /// An on/off option of how the game's controls work (Sprint held or toggled, say), shown on the controls page (a
    /// ControlOptionToggle names it). The UI knows nothing of the game: the game's side implements it and registers it
    /// (ControlOptions.Register), and keeps it itself (saving it, if it's to last).
    /// </summary>
    public interface IControlOption
    {
        /// <summary>Its name, which a ControlOptionToggle finds it by, and shows ("Sprint").</summary>
        string Name { get; }
        /// <summary>What it's called while it's off ("Hold").</summary>
        string OffText { get; }
        /// <summary>What it's called while it's on ("Toggle").</summary>
        string OnText { get; }
        /// <summary>Whether it's on.</summary>
        bool Value { get; set; }
    }

    /// <summary>The control options the game's registered (see IControlOption), by name.</summary>
    public static class ControlOptions
    {
        private static readonly List<IControlOption> options = new();

        /// <summary>Raised when an option's registered or unregistered, or its value changes through Set.</summary>
        public static event Action Changed;

        /// <summary>Registers an option (a second of the same name replaces the first while it's registered).</summary>
        public static void Register(IControlOption option)
        {
            if (option == null || options.Contains(option)) { return; }
            options.Add(option);
            Changed?.Invoke();
        }

        /// <summary>Unregisters an option.</summary>
        public static void Unregister(IControlOption option)
        {
            if (options.Remove(option)) { Changed?.Invoke(); }
        }

        /// <summary>The option of the name registered last, or null if there's none.</summary>
        public static IControlOption Find(string name)
        {
            for (int i = options.Count - 1; i >= 0; i--) { if (options[i].Name == name) { return options[i]; } }
            return null;
        }

        /// <summary>Sets an option's value and raises Changed.</summary>
        public static void Set(IControlOption option, bool value)
        {
            if (option == null) { return; }
            option.Value = value;
            Changed?.Invoke();
        }
    }
}
