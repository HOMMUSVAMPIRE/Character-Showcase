using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace MeridianMotion.Characters
{
    /// <summary>
    /// Marks a class of a project's own that declares CharacterState and MovementEvents flags (as New Ability's
    /// [Name].Flags.cs does for an ability made in the project), so their names are known (ToString, TryParse, the
    /// inspectors) as "[Class].[Field]": GroundPound.State, GroundPound.Started. Its flags take bits from
    /// Flags256.UserFirstBit on (the package's own are below), declared as the package's are: a bit number
    /// (public const int StartedBit = 130;) and its value (public static readonly MovementEvents Started =
    /// MovementEvents.Bit(StartedBit);). A bit is saved data (in recordings, and the translators' triggers): never
    /// renumber one. New Ability gives out bits from the project's ledger (Character Flags.json), which never gives one twice.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class MovementFlagsAttribute : Attribute
    {
    }

    /// <summary>
    /// Finds the names of a kind of flag (CharacterState or MovementEvents), for its name registry: the package's, its own
    /// static readonly fields (each named as the field), and a project's, those of every [MovementFlags] class (each named
    /// [Class].[Field]). Found once, by reflection: for names, not for anything a step does. Test assemblies' classes
    /// (those referencing NUnit) aren't looked in, so a test's flags never show in the inspectors.
    /// </summary>
    public static class FlagNames
    {
        /// <summary>
        /// Every flag of type T named, as (name, flag), lowest bit first, and the names by bit. A bit two fields share is
        /// named by the first found (the package's, then the [MovementFlags] classes by full name), with a warning for a
        /// project's; a project's flag on a bit of the package's is left out, with a warning. projectClasses are the
        /// [MovementFlags] classes to look in (null: every one in the project's assemblies).
        /// </summary>
        public static (string[] byBit, List<(string name, T flag)> named) Find<T>(Func<T, int> bitOf, IEnumerable<Type> projectClasses = null) where T : struct
        {
            var byBit = new string[Flags256.Size];
            var named = new List<(string name, T flag)>();
            void Add(Type declaring, string prefix, bool own)
            {
                foreach (FieldInfo field in declaring.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
                {
                    if (field.FieldType != typeof(T) || !field.IsInitOnly) { continue; }
                    var value = (T)field.GetValue(null);
                    int bit = bitOf(value);
                    if (bit < 0) { continue; }
                    string name = prefix + field.Name;
                    if (!own && bit < Flags256.UserFirstBit)
                    {
                        Debug.LogWarning($"[{typeof(T).Name}] {name} is on bit {bit}, one of the package's (below {Flags256.UserFirstBit}): " +
                                         $"a project's own flags take bits from {Flags256.UserFirstBit} on. Left out of the names.");
                        continue;
                    }
                    if (byBit[bit] != null)
                    {
                        if (!own) { Debug.LogWarning($"[{typeof(T).Name}] {name} and {byBit[bit]} share bit {bit}: give {name} a bit of its own (New Ability's ledger, Character Flags.json, says which are taken)."); }
                        continue;
                    }
                    byBit[bit] = name;
                    named.Add((name, value));
                }
            }

            Add(typeof(T), "", own: true);
            foreach (Type type in projectClasses ?? ProjectFlagClasses()) { Add(type, type.Name + ".", own: false); }
            named.Sort((a, b) => bitOf(a.flag).CompareTo(bitOf(b.flag)));
            return (byBit, named);
        }

        private static List<Type> projectFlagClasses;

        /// <summary>The [MovementFlags] classes of the assemblies that reference this one (but not tests'), by full name.</summary>
        private static List<Type> ProjectFlagClasses()
        {
            if (projectFlagClasses != null) { return projectFlagClasses; }
            var found = new List<Type>();
            string own = typeof(FlagNames).Assembly.GetName().Name;
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (!References(assembly, own) || References(assembly, "nunit.framework")) { continue; }
                Type[] types;
                try { types = assembly.GetTypes(); }
                catch (ReflectionTypeLoadException partial) { types = partial.Types; }
                foreach (Type type in types)
                {
                    if (type != null && type.IsDefined(typeof(MovementFlagsAttribute), false)) { found.Add(type); }
                }
            }
            found.Sort((a, b) => string.CompareOrdinal(a.FullName, b.FullName));
            return projectFlagClasses = found;
        }

        private static bool References(Assembly assembly, string name)
        {
            if (assembly.IsDynamic) { return false; }
            foreach (AssemblyName reference in assembly.GetReferencedAssemblies())
            {
                if (reference.Name == name) { return true; }
            }
            return false;
        }
    }
}
