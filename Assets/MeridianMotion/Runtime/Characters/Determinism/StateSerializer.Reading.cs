using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using MeridianMotion.Levels;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MeridianMotion.Characters
{
    // Reading a graph of objects back from bytes, onto what's there.
    public static partial class StateSerializer
    {
        private sealed class GraphReader
        {
            private readonly BinaryReader reader;
            private readonly Transform own;
            private readonly List<object> read = new();

            public GraphReader(BinaryReader reader, Transform ownRoot)
            {
                this.reader = reader;
                own = ownRoot;
            }

            public void ReadRoot(object root, Type stopAt)
            {
                read.Add(root);
                ReadFields(root, root.GetType(), stopAt);
            }

            private void ReadFields(object obj, Type type, Type stopAt)
            {
                foreach (FieldInfo field in FieldsOf(type, stopAt)) { field.SetValue(obj, ReadValue(field.FieldType, field.GetValue(obj))); }
            }

            /// <summary>The value written for a field of the type, given what the field holds now (filled in, if it can be).</summary>
            private object ReadValue(Type type, object live)
            {
                if (Nullable.GetUnderlyingType(type) is { } underlying) { return reader.ReadBoolean() ? ReadValue(underlying, live) : null; }
                if (type.IsEnum) { return Enum.ToObject(type, ReadPrimitive(Enum.GetUnderlyingType(type), live)); }
                if (type.IsPrimitive) { return ReadPrimitive(type, live); }
                if (type.IsValueType)
                {
                    // Read into a box of it (fields set on the box), unboxed when it's set on the field.
                    object boxed = live ?? Activator.CreateInstance(type);
                    ReadFields(boxed, type, null);
                    return boxed;
                }
                return ReadReference(live);
            }

            private object ReadPrimitive(Type type, object live)
            {
                switch (Type.GetTypeCode(type))
                {
                    case TypeCode.Boolean: return reader.ReadBoolean();
                    case TypeCode.Byte: return reader.ReadByte();
                    case TypeCode.SByte: return reader.ReadSByte();
                    case TypeCode.Int16: return reader.ReadInt16();
                    case TypeCode.UInt16: return reader.ReadUInt16();
                    case TypeCode.Int32: return reader.ReadInt32();
                    case TypeCode.UInt32: return reader.ReadUInt32();
                    case TypeCode.Int64: return reader.ReadInt64();
                    case TypeCode.UInt64: return reader.ReadUInt64();
                    case TypeCode.Single: return reader.ReadSingle();
                    case TypeCode.Double: return reader.ReadDouble();
                    case TypeCode.Char: return reader.ReadChar();
                    default: return live;
                }
            }

            private object ReadReference(object live)
            {
                var tag = (Tag)reader.ReadByte();
                switch (tag)
                {
                    case Tag.Null: return null;
                    case Tag.Kept: return live;
                    case Tag.Shared: return read[reader.ReadInt32()];
                    case Tag.String: return reader.ReadString();
                    case Tag.Boxed:
                    {
                        Type type = TypeNamed(reader.ReadString());
                        object current = live != null && live.GetType() == type ? live : null;
                        return ReadValue(type, current);
                    }
                    case Tag.Array:
                    {
                        Type element = TypeNamed(reader.ReadString());
                        int length = reader.ReadInt32();
                        Array array = live is Array existing && existing.GetType().GetElementType() == element && existing.Length == length
                            ? existing : Array.CreateInstance(element, length);
                        read.Add(array);
                        for (int i = 0; i < length; i++) { array.SetValue(ReadValue(element, array.GetValue(i)), i); }
                        return array;
                    }
                    case Tag.Object:
                    {
                        Type type = TypeNamed(reader.ReadString());
                        object obj = live != null && live.GetType() == type && !(live is Object) ? live : RuntimeHelpers.GetUninitializedObject(type);
                        read.Add(obj);
                        ReadFields(obj, type, null);
                        return obj;
                    }
                    case Tag.Own:
                    case Tag.Level:
                    {
                        Transform transform = tag == Tag.Own
                            ? (own != null ? Follow(own, reader.ReadString()) : null)
                            : StableId.Resolve(reader.ReadUInt64());
                        var kind = (UnityKind)reader.ReadByte();
                        Object found = null;
                        if (kind == UnityKind.Component)
                        {
                            Type type = TypeNamed(reader.ReadString());
                            int index = reader.ReadInt32();
                            Component[] components = transform != null ? transform.GetComponents(type) : Array.Empty<Component>();
                            found = index >= 0 && index < components.Length ? components[index] : null;
                        }
                        else if (transform != null)
                        {
                            found = kind == UnityKind.GameObject ? transform.gameObject : transform;
                        }
                        // One of the character's own that this one hasn't made yet (a cache it makes when needed): its own kept.
                        if (found == null && tag == Tag.Own) { return live; }
                        if (found == null) { throw new InvalidDataException("A level object the state names isn't in this level: it isn't built the same way."); }
                        return found;
                    }
                    default: throw new InvalidDataException($"Unknown value in a state ({tag}).");
                }
            }

            public static Vector3 ReadVector3(BinaryReader reader) => new(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());

            public static Quaternion ReadQuaternion(BinaryReader reader) =>
                new(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
        }
    }
}
