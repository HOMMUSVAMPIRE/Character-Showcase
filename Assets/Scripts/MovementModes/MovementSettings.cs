using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Character
{
    /// <summary>
    /// The settings of a movement component, as Unity saves them: its serialized fields (public, or [SerializeField], and
    /// not [NonSerialized]), its on/off switch ("enabled") included, each named by its field name. Field names are how
    /// settings are found everywhere outside the asset (presets, the tests, spreadsheets), so they're kept stable (see
    /// AGENTS.md). Settings are numbers, toggles and curves; copying one copies a curve's keys, so nothing shares a curve.
    /// </summary>
    public static class MovementSettings
    {
        /// <summary>The name of a component's on/off switch, as a setting.</summary>
        public const string EnabledSetting = "enabled";

        private static readonly Dictionary<Type, FieldInfo[]> cache = new();

        /// <summary>A component type's settings, its on/off switch first, then in the order they're declared (its base's first).</summary>
        public static IReadOnlyList<FieldInfo> Of(Type componentType)
        {
            if (cache.TryGetValue(componentType, out FieldInfo[] fields)) { return fields; }
            var found = new List<FieldInfo>();
            var hierarchy = new List<Type>();
            for (Type t = componentType; t != null && typeof(MovementComponent).IsAssignableFrom(t); t = t.BaseType) { hierarchy.Insert(0, t); }
            foreach (Type t in hierarchy)
            {
                foreach (FieldInfo field in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (field.IsNotSerialized || field.IsInitOnly || !(field.IsPublic || field.IsDefined(typeof(SerializeField), false))) { continue; }
                    found.Add(field);
                }
            }
            fields = found.ToArray();
            cache[componentType] = fields;
            return fields;
        }

        /// <summary>The setting of a component type with this field name, or null if it has none.</summary>
        public static FieldInfo Find(Type componentType, string setting)
        {
            foreach (FieldInfo field in Of(componentType)) { if (field.Name == setting) { return field; } }
            return null;
        }

        /// <summary>Whether a setting has the same value on both components (curves compared key by key).</summary>
        public static bool Same(FieldInfo setting, MovementComponent a, MovementComponent b)
        {
            object x = setting.GetValue(a), y = setting.GetValue(b);
            if (x is AnimationCurve curveX && y is AnimationCurve curveY) { return SameCurve(curveX, curveY); }
            return Equals(x, y);
        }

        /// <summary>
        /// Sets a setting on one component to its value on another, returning whether it changed. A curve is copied, not
        /// shared.
        /// </summary>
        public static bool Copy(FieldInfo setting, MovementComponent from, MovementComponent to)
        {
            if (Same(setting, from, to)) { return false; }
            object value = setting.GetValue(from);
            setting.SetValue(to, value is AnimationCurve curve ? CopyOf(curve) : value);
            return true;
        }

        /// <summary>A copy of a component, every setting copied (its curves too), belonging to no mode.</summary>
        public static MovementComponent Clone(MovementComponent component)
        {
            var copy = (MovementComponent)Activator.CreateInstance(component.GetType());
            foreach (FieldInfo setting in Of(component.GetType())) { Copy(setting, component, copy); }
            return copy;
        }

        /// <summary>A curve's copy: its keys and how it carries on past them.</summary>
        public static AnimationCurve CopyOf(AnimationCurve curve) =>
            curve == null ? null : new AnimationCurve(curve.keys) { preWrapMode = curve.preWrapMode, postWrapMode = curve.postWrapMode };

        private static bool SameCurve(AnimationCurve a, AnimationCurve b)
        {
            if (a.length != b.length || a.preWrapMode != b.preWrapMode || a.postWrapMode != b.postWrapMode) { return false; }
            for (int i = 0; i < a.length; i++)
            {
                Keyframe p = a[i], q = b[i];
                if (p.time != q.time || p.value != q.value || p.inTangent != q.inTangent || p.outTangent != q.outTangent
                    || p.inWeight != q.inWeight || p.outWeight != q.outWeight || p.weightedMode != q.weightedMode) { return false; }
            }
            return true;
        }
    }
}
