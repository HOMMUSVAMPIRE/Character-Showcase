using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Character;
using Level;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CharacterShowcase.Tests
{
    // The rig's settings: reading and changing the movement mode's settings and components, for a test only.
    public sealed partial class CharacterTestRig
    {
        /// <summary>
        /// Reads a tuning value the movement mode interfaces don't have (BasicMovementMode's gravity, coyote time and dash
        /// cooldown, say) from the character's movement mode, or one of its components, by name (setting names are unique
        /// across a mode and its components). Ignores the test if the mode has no such value: it tests a behaviour of
        /// BasicMovementMode, which another mode may not have. Fails it if the value is there but of another type than T.
        /// </summary>
        public T MovementSetting<T>(string fieldName)
        {
            (object owner, FieldInfo field) = FindSetting<T>(fieldName);
            return (T)field.GetValue(owner);
        }

        /// <summary>
        /// Changes a setting on the character's movement mode, or one of its components, by name, for this test only: the
        /// mode is the rig's own copy of the asset (see SpawnCharacter). Ignores the test if the mode has no such setting,
        /// as MovementSetting does.
        /// </summary>
        public void SetMovementSetting<T>(string fieldName, T value)
        {
            (object owner, FieldInfo field) = FindSetting<T>(fieldName);
            field.SetValue(owner, value);
        }

        private (object owner, FieldInfo field) FindSetting<T>(string fieldName)
        {
            const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
            object handler = Movement.movementHandler;
            var owners = new List<object> { handler };
            if (handler is ComposedMovementMode composed) { owners.AddRange(composed.Components.Where(c => c != null)); }
            FieldInfo otherType = null;
            object otherOwner = null;
            foreach (object owner in owners)
            {
                for (Type type = owner?.GetType(); type != null; type = type.BaseType)
                {
                    FieldInfo field = type.GetField(fieldName, Fields | BindingFlags.DeclaredOnly);
                    if (field == null) { continue; }
                    if (field.FieldType == typeof(T)) { return (owner, field); }
                    if (otherType == null) { (otherType, otherOwner) = (field, owner); }
                }
            }
            // The setting is there, but the test asks for it as another type (an int literal for a float setting, say, or
            // a setting whose type changed): that's the test's mistake, not a mode without the setting, so it fails. Ignored,
            // the test would quietly stop testing anything.
            if (otherType != null)
            {
                Assert.Fail($"'{fieldName}' (on {otherOwner.GetType().Name}) is a {otherType.FieldType.Name} setting, but the test reads or sets it " +
                            $"as a {typeof(T).Name}: use a {otherType.FieldType.Name} (a float literal needs an f, say).");
            }
            Assert.Ignore($"The character's movement mode ({handler?.GetType().Name ?? "none"}) has no {typeof(T).Name} '{fieldName}' setting.");
            return default;
        }

        /// <summary>
        /// The character's movement mode's component of type T (the rig's own copy of it, for this test only), turned on
        /// or not. Ignores the test if the mode has none: a test of riding ziplines has nothing to test on a character
        /// that can't.
        /// </summary>
        public T Component<T>() where T : MovementComponent
        {
            T component = (Movement.movementHandler as ComposedMovementMode)?.Get<T>(includeDisabled: true);
            if (component == null) { Assert.Ignore($"The character's movement mode has no {typeof(T).Name} component."); }
            return component;
        }

        /// <summary>The rig's own copy of the character's movement mode, as one made of components (ignores the test if it isn't).</summary>
        public ComposedMovementMode ComposedMode => Mode<ComposedMovementMode>();
    }
}
