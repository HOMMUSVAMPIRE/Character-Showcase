using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using Object = UnityEngine.Object;

namespace Character
{
    /// <summary>
    /// A copy of an object's state, and of every plain C# object it reaches (its fields' objects, theirs, and so on:
    /// arrays, lists, a character's ability states), that can be put back into those same objects later, so code
    /// holding them sees them as they were. Identity is kept: two fields that shared an object share it again. Unity
    /// objects (components, assets), delegates and strings are kept by reference, not copied: their own state isn't
    /// saved. For MovementSimulation, which saves a simulated character partway through a measurement and starts
    /// later attempts from there (see MovementSimulation.Save).
    /// </summary>
    /// <remarks>
    /// Saved and restored often (every few steps of a measurement), so each type's fields are copied by a method compiled
    /// for it once (an expression tree), not one reflection call per field: that cost as much as six steps of movement.
    /// A readonly field isn't written back: its value (a struct, or which object it holds) can't have changed since it
    /// was saved; an object it holds is saved and restored as an object of its own, so its contents are.
    /// </remarks>
    internal sealed class ObjectGraphSnapshot
    {
        // Each plain object reached and its copy, and back again.
        private readonly List<(object live, object copy)> pairs = new();
        private readonly Dictionary<object, object> copyOf = new(ReferenceComparer.Instance);
        private readonly Dictionary<object, object> liveOf = new(ReferenceComparer.Instance);
        // The root's own fields (it's restored in place, not copied: a MonoBehaviour can't be).
        private readonly object root;
        private readonly Accessor rootAccessor;
        private readonly object[] rootValues;
        private readonly Func<object, object> copyValue, liveValue;

        private ObjectGraphSnapshot(object root, Type stopAt)
        {
            this.root = root;
            copyValue = Copy;
            liveValue = Live;
            rootAccessor = Accessor.For(root.GetType(), stopAt);
            rootValues = rootAccessor.read(root, copyValue);
            rootAccessor.RegisterReadonly(root, copyValue);
        }

        /// <summary>
        /// Saves root's fields (those declared on its type and its bases, down to but not including stopAt: for a
        /// component, MonoBehaviour), and every plain object they reach.
        /// </summary>
        public static ObjectGraphSnapshot Capture(object root, Type stopAt) => new(root, stopAt);

        /// <summary>Puts root and every object saved back as they were when it was saved.</summary>
        public void Restore()
        {
            foreach (var (live, copy) in pairs)
            {
                if (live is Array liveArray)
                {
                    var copyArray = (Array)copy;
                    // Values (a buffer of hits, say) copied whole, unboxed; objects one by one, back to the live ones.
                    if (liveArray.GetType().GetElementType().IsValueType) { Array.Copy(copyArray, liveArray, liveArray.Length); }
                    else { for (int i = 0; i < liveArray.Length; i++) { liveArray.SetValue(Live(copyArray.GetValue(i)), i); } }
                    continue;
                }
                Accessor.For(live.GetType(), null).copy(copy, live, liveValue);
            }
            rootAccessor.write(root, rootValues, liveValue);
        }

        /// <summary>The copy of a value: itself, unless it's a plain object (copied, once however often it's reached).</summary>
        private object Copy(object value)
        {
            if (!IsPlain(value)) { return value; }
            if (copyOf.TryGetValue(value, out object done)) { return done; }
            if (value is Array array)
            {
                Type element = array.GetType().GetElementType();
                var copiedArray = Array.CreateInstance(element, array.Length);
                Remember(value, copiedArray);
                if (element.IsValueType)
                {
                    CheckStruct(element);
                    Array.Copy(array, copiedArray, array.Length);
                }
                else { for (int i = 0; i < array.Length; i++) { copiedArray.SetValue(Copy(array.GetValue(i)), i); } }
                return copiedArray;
            }
            // A shallow copy made natively (far quicker than making a blank one), its objects then mapped to their copies.
            object copy = shallowCopy(value);
            Remember(value, copy);
            Accessor accessor = Accessor.For(value.GetType(), null);
            accessor.copy(value, copy, copyValue);
            accessor.RegisterReadonly(value, copyValue);
            return copy;
        }

        private void Remember(object live, object copy)
        {
            copyOf[live] = copy;
            liveOf[copy] = live;
            pairs.Add((live, copy));
        }

        /// <summary>The live object a copy was made from (a value that isn't a copy is itself).</summary>
        private object Live(object value) => value != null && liveOf.TryGetValue(value, out object live) ? live : value;

        /// <summary>
        /// Whether a value is a plain object, copied rather than kept: not null, a struct or enum (copied by value
        /// already), a string, a delegate, a Unity object or a reflection object.
        /// </summary>
        private static bool IsPlain(object value)
        {
            if (value == null || value is string || value is Delegate || value is Object || value is MemberInfo) { return false; }
            Type type = value.GetType();
            if (!type.IsValueType) { return true; }
            CheckStruct(type);
            return false;
        }

        /// <summary>Whether a field of this type always holds what's kept, never a plain object (so needs no mapping).</summary>
        private static bool AlwaysKept(Type type) =>
            type.IsValueType || type == typeof(string) || typeof(Delegate).IsAssignableFrom(type)
            || typeof(Object).IsAssignableFrom(type) || typeof(MemberInfo).IsAssignableFrom(type);

        private static readonly HashSet<Type> checkedStructs = new();

        private static readonly Func<object, object> shallowCopy = (Func<object, object>)Delegate.CreateDelegate(typeof(Func<object, object>),
            typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic));

        /// <summary>
        /// A struct is copied by value with its field; one holding a plain object would share it between the live state
        /// and the copy, so isn't allowed: this throws, so it's found at once rather than corrupting a measurement.
        /// </summary>
        private static void CheckStruct(Type type)
        {
            if (type.IsPrimitive || type.IsEnum || checkedStructs.Contains(type)) { return; }
            foreach (FieldInfo field in Accessor.FieldsOf(type, null))
            {
                Type fieldType = field.FieldType;
                if (fieldType.IsValueType) { CheckStruct(fieldType); continue; }
                if (!AlwaysKept(fieldType))
                {
                    throw new NotSupportedException($"{nameof(ObjectGraphSnapshot)} can't save a {type.Name}: its field '{field.Name}' " +
                                                    $"holds a {fieldType.Name}, which a copy of the struct would share. Make it a class, or keep it out.");
                }
            }
            checkedStructs.Add(type);
        }

        /// <summary>A type's fields, and methods compiled to copy them (once per type, cached).</summary>
        private sealed class Accessor
        {
            private const BindingFlags InstanceFields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            private static readonly Dictionary<(Type, Type), Accessor> cache = new();

            /// <summary>Copies each writable field from one object to another of the type, mapping objects (Copy or Live).</summary>
            public Action<object, object, Func<object, object>> copy;
            /// <summary>Reads each writable field into an array (mapped), for the root, which isn't copied.</summary>
            public Func<object, Func<object, object>, object[]> read;
            /// <summary>Writes an array read so back into the fields (mapped).</summary>
            public Action<object, object[], Func<object, object>> write;
            // Readonly fields that may hold a plain object, whose objects are saved as objects of their own.
            private FieldInfo[] readonlyObjects;

            /// <summary>Saves the objects the readonly fields hold (their contents are restored with them).</summary>
            public void RegisterReadonly(object live, Func<object, object> copyValue)
            {
                foreach (FieldInfo field in readonlyObjects) { copyValue(field.GetValue(live)); }
            }

            public static Accessor For(Type type, Type stopAt)
            {
                if (cache.TryGetValue((type, stopAt), out Accessor found)) { return found; }
                FieldInfo[] fields = FieldsOf(type, stopAt);
                var writable = new List<FieldInfo>();
                var readonlyObjects = new List<FieldInfo>();
                foreach (FieldInfo field in fields)
                {
                    if (!field.IsInitOnly) { writable.Add(field); }
                    else if (!AlwaysKept(field.FieldType)) { readonlyObjects.Add(field); }
                }

                ParameterExpression from = Expression.Parameter(typeof(object), "from");
                ParameterExpression to = Expression.Parameter(typeof(object), "to");
                ParameterExpression map = Expression.Parameter(typeof(Func<object, object>), "map");
                ParameterExpression values = Expression.Parameter(typeof(object[]), "values");
                Expression typedFrom = Expression.Convert(from, type), typedTo = Expression.Convert(to, type);
                Expression Mapped(Expression value, Type fieldType) => AlwaysKept(fieldType)
                    ? value
                    : Expression.Convert(Expression.Invoke(map, Expression.Convert(value, typeof(object))), fieldType);

                var copies = new List<Expression>();
                var reads = new List<Expression>();
                var writes = new List<Expression>();
                for (int i = 0; i < writable.Count; i++)
                {
                    FieldInfo field = writable[i];
                    copies.Add(Expression.Assign(Expression.Field(typedTo, field), Mapped(Expression.Field(typedFrom, field), field.FieldType)));
                    Expression read = Expression.Field(typedFrom, field);
                    reads.Add(AlwaysKept(field.FieldType)
                        ? Expression.Convert(read, typeof(object))
                        : Expression.Invoke(map, Expression.Convert(read, typeof(object))));
                    Expression stored = Expression.ArrayIndex(values, Expression.Constant(i));
                    writes.Add(Expression.Assign(Expression.Field(typedTo, field),
                        AlwaysKept(field.FieldType) ? Expression.Convert(stored, field.FieldType) : Mapped(stored, field.FieldType)));
                }
                var accessor = new Accessor
                {
                    readonlyObjects = readonlyObjects.ToArray(),
                    copy = Expression.Lambda<Action<object, object, Func<object, object>>>(
                        copies.Count > 0 ? Expression.Block(copies) : Expression.Empty(), from, to, map).Compile(),
                    read = Expression.Lambda<Func<object, Func<object, object>, object[]>>(
                        Expression.NewArrayInit(typeof(object), reads), from, map).Compile(),
                    write = Expression.Lambda<Action<object, object[], Func<object, object>>>(
                        writes.Count > 0 ? Expression.Block(writes) : Expression.Empty(), to, values, map).Compile(),
                };
                cache[(type, stopAt)] = accessor;
                return accessor;
            }

            /// <summary>A type's instance fields, its bases' too (down to, not including, stopAt).</summary>
            public static FieldInfo[] FieldsOf(Type type, Type stopAt)
            {
                var fields = new List<FieldInfo>();
                for (Type t = type; t != null && t != stopAt && t != typeof(object); t = t.BaseType) { fields.AddRange(t.GetFields(InstanceFields)); }
                return fields.ToArray();
            }
        }

        private sealed class ReferenceComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceComparer Instance = new();
            public new bool Equals(object a, object b) => ReferenceEquals(a, b);
            public int GetHashCode(object value) => RuntimeHelpers.GetHashCode(value);
        }
    }
}
