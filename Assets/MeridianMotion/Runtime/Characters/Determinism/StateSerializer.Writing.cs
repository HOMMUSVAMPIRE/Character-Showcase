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
    // Writing a graph of objects to bytes.
    public static partial class StateSerializer
    {
        private sealed class GraphWriter
        {
            private readonly BinaryWriter writer;
            private readonly Transform own;
            private readonly Dictionary<object, int> written = new(ReferenceComparer.Instance);

            public GraphWriter(BinaryWriter writer, Transform ownRoot)
            {
                this.writer = writer;
                own = ownRoot;
            }

            public void WriteRoot(object root, Type stopAt)
            {
                written[root] = written.Count;
                WriteFields(root, root.GetType(), stopAt);
            }

            private void WriteFields(object obj, Type type, Type stopAt)
            {
                foreach (FieldInfo field in FieldsOf(type, stopAt)) { WriteValue(field.FieldType, field.GetValue(obj)); }
            }

            private void WriteValue(Type type, object value)
            {
                // A T? boxes as a T, or null for none.
                if (Nullable.GetUnderlyingType(type) is { } underlying)
                {
                    writer.Write(value != null);
                    if (value != null) { WriteValue(underlying, value); }
                    return;
                }
                if (type.IsEnum) { WritePrimitive(Enum.GetUnderlyingType(type), value); return; }
                if (type.IsPrimitive) { WritePrimitive(type, value); return; }
                if (type.IsValueType) { WriteFields(value, type, null); return; }
                WriteReference(value);
            }

            private void WritePrimitive(Type type, object value)
            {
                switch (Type.GetTypeCode(type))
                {
                    case TypeCode.Boolean: writer.Write((bool)value); break;
                    case TypeCode.Byte: writer.Write(Convert.ToByte(value)); break;
                    case TypeCode.SByte: writer.Write(Convert.ToSByte(value)); break;
                    case TypeCode.Int16: writer.Write(Convert.ToInt16(value)); break;
                    case TypeCode.UInt16: writer.Write(Convert.ToUInt16(value)); break;
                    case TypeCode.Int32: writer.Write(Convert.ToInt32(value)); break;
                    case TypeCode.UInt32: writer.Write(Convert.ToUInt32(value)); break;
                    case TypeCode.Int64: writer.Write(Convert.ToInt64(value)); break;
                    case TypeCode.UInt64: writer.Write(Convert.ToUInt64(value)); break;
                    case TypeCode.Single: writer.Write((float)value); break;
                    case TypeCode.Double: writer.Write((double)value); break;
                    case TypeCode.Char: writer.Write((char)value); break;
                    default:
                        // IntPtr and the like: process-local, written as nothing (the reader keeps its own).
                        break;
                }
            }

            private void WriteReference(object value)
            {
                if (value == null || (value is Object unityNull && unityNull == null)) { writer.Write((byte)Tag.Null); return; }
                if (value is Object unity) { WriteUnity(unity); return; }
                if (IsKept(value.GetType())) { writer.Write((byte)Tag.Kept); return; }
                if (written.TryGetValue(value, out int index))
                {
                    writer.Write((byte)Tag.Shared);
                    writer.Write(index);
                    return;
                }
                if (value is string text)
                {
                    writer.Write((byte)Tag.String);
                    writer.Write(text);
                    return;
                }
                Type type = value.GetType();
                if (type.IsValueType)
                {
                    writer.Write((byte)Tag.Boxed);
                    writer.Write(NameOf(type));
                    WriteValue(type, value);
                    return;
                }
                written[value] = written.Count;
                if (value is Array array)
                {
                    writer.Write((byte)Tag.Array);
                    Type element = type.GetElementType();
                    writer.Write(NameOf(element));
                    writer.Write(array.Length);
                    for (int i = 0; i < array.Length; i++) { WriteValue(element, array.GetValue(i)); }
                    return;
                }
                writer.Write((byte)Tag.Object);
                writer.Write(NameOf(type));
                WriteFields(value, type, null);
            }

            /// <summary>One of the character's own objects by its path; a level object by its StableId; anything else (an asset, a setting) kept.</summary>
            private void WriteUnity(Object value)
            {
                Transform transform = value is GameObject go ? go.transform : value is Component c ? c.transform : null;
                if (transform == null || !transform.gameObject.scene.IsValid())
                {
                    writer.Write((byte)Tag.Kept);
                    return;
                }
                bool isOwn = own != null && transform.IsChildOf(own);
                writer.Write((byte)(isOwn ? Tag.Own : Tag.Level));
                if (isOwn) { writer.Write(PathFrom(own, transform)); }
                else { writer.Write(StableId.Of(transform)); }
                switch (value)
                {
                    case GameObject:
                        writer.Write((byte)UnityKind.GameObject);
                        break;
                    case Transform:
                        writer.Write((byte)UnityKind.Transform);
                        break;
                    case Component component:
                        writer.Write((byte)UnityKind.Component);
                        Type type = component.GetType();
                        writer.Write(NameOf(type));
                        writer.Write(Array.IndexOf(component.GetComponents(type), component));
                        break;
                }
            }

            public static void Write(BinaryWriter writer, Vector3 v)
            {
                writer.Write(v.x);
                writer.Write(v.y);
                writer.Write(v.z);
            }

            public static void Write(BinaryWriter writer, Quaternion q)
            {
                writer.Write(q.x);
                writer.Write(q.y);
                writer.Write(q.z);
                writer.Write(q.w);
            }
        }
    }
}
