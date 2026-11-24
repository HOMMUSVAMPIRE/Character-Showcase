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
    /// <summary>
    /// Writes a character's movement state, or the level's moving parts' (its ropes and pushables), to bytes, and reads
    /// them back into a character or level built the same way, on another machine even: what a server sends so a client
    /// can put its character exactly where the server has it (and step on from there identically), or a player joining
    /// can start a character mid-ability. The state is what MovementStateSnapshot saves in this process (the controller and
    /// every plain object it reaches, its MMotionCharacter's, its input source's if that's on it, its Rigidbody's
    /// pose), written out. A reference to one of the character's own objects is written as its path from the character;
    /// to a level object (its ground, a rope it holds) as the object's StableId; an asset or a setting (its movement
    /// mode) isn't written, the reader keeping its own. Shared objects stay shared. Every float is written as its bits,
    /// so what's read is exactly what was written.
    /// </summary>
    /// <remarks>
    /// Read into the character it's for in the same order its fields are written (by declaring type, then name), so the
    /// reader needs the same types (the same build), not the same objects: one it has of the right type is filled in;
    /// otherwise one is made. Not for every step: it reflects over every field, and allocates.
    /// </remarks>
    public static partial class StateSerializer
    {
        private static readonly byte[] CharacterMagic = Encoding.ASCII.GetBytes("CSCS");
        private static readonly byte[] WorldMagic = Encoding.ASCII.GetBytes("CSWS");
        private const int FormatVersion = 1;

        /// <summary>Writes the character's state (see the class summary). Between steps.</summary>
        public static byte[] WriteCharacter(MMotionMotor character)
        {
            using var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
            {
                writer.Write(CharacterMagic);
                writer.Write(FormatVersion);
                var graph = new GraphWriter(writer, character.transform);
                foreach (object root in Roots(character)) { graph.WriteRoot(root, typeof(MonoBehaviour)); }
                Rigidbody body = character.GetComponent<Rigidbody>();
                GraphWriter.Write(writer, body != null ? body.position : character.transform.position);
                GraphWriter.Write(writer, body != null ? body.rotation : character.transform.rotation);
            }
            return stream.ToArray();
        }

        /// <summary>
        /// Reads a state WriteCharacter wrote into the character (one of the same prefab, in a level built the same way),
        /// between its steps: afterwards it steps on exactly as the written one did. Throws InvalidDataException for what
        /// isn't one.
        /// </summary>
        public static void ReadCharacter(MMotionMotor character, byte[] state)
        {
            using var stream = new MemoryStream(state);
            using var reader = new BinaryReader(stream, Encoding.UTF8);
            Expect(reader, CharacterMagic, "a character's state");
            var graph = new GraphReader(reader, character.transform);
            foreach (object root in Roots(character)) { graph.ReadRoot(root, typeof(MonoBehaviour)); }
            Vector3 position = GraphReader.ReadVector3(reader);
            Quaternion rotation = GraphReader.ReadQuaternion(reader);
            Rigidbody body = character.GetComponent<Rigidbody>();
            // Moved only if it's somewhere else: moving an interpolated Rigidbody's transform is a teleport, which drops
            // its interpolation for a frame (the model and the camera jump to the physics pose), so putting back the pose
            // it already has (a replay's keyframe on the build that recorded it) would make it stutter for nothing.
            if (body != null && body.position == position && body.rotation == rotation) { return; }
            if (body != null)
            {
                body.position = position;
                body.rotation = rotation;
            }
            character.transform.SetPositionAndRotation(position, rotation);
            Physics.SyncTransforms();
        }

        /// <summary>The objects a character's state is read from: its controller, its MMotionCharacter, and its input source if that's on it.</summary>
        private static IEnumerable<object> Roots(MMotionMotor character)
        {
            yield return character;
            MMotionCharacter controller = character.GetComponent<MMotionCharacter>();
            if (controller == null) { yield break; }
            yield return controller;
            if (controller.InputSource is CharacterInputSource source && source != null && source.transform.IsChildOf(character.transform))
            {
                yield return source;
            }
        }

        /// <summary>
        /// Writes the level's moving parts' own state: each rope (how it swings, who holds it) and each pushable object
        /// (where it is, how fast it's falling, who holds it), by StableId. Platforms need none: they go by the level's
        /// clock (LevelClock), which the reader sets itself.
        /// </summary>
        public static byte[] WriteWorld()
        {
            using var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
            {
                writer.Write(WorldMagic);
                writer.Write(FormatVersion);
                List<MonoBehaviour> parts = WorldStateSnapshot.MovingParts();
                writer.Write(parts.Count);
                var graph = new GraphWriter(writer, null);
                foreach (MonoBehaviour part in parts)
                {
                    writer.Write(StableId.Of(part.transform));
                    writer.Write(part is SwingRope);
                    graph.WriteRoot(part, typeof(MonoBehaviour));
                    Rigidbody body = part.GetComponent<Rigidbody>();
                    GraphWriter.Write(writer, body != null ? body.position : part.transform.position);
                    GraphWriter.Write(writer, body != null ? body.rotation : part.transform.rotation);
                }
            }
            return stream.ToArray();
        }

        /// <summary>Reads what WriteWorld wrote into the level's ropes and pushables (found by StableId; one not found is skipped).</summary>
        public static void ReadWorld(byte[] state)
        {
            using var stream = new MemoryStream(state);
            using var reader = new BinaryReader(stream, Encoding.UTF8);
            Expect(reader, WorldMagic, "a level's state");
            int count = reader.ReadInt32();
            var graph = new GraphReader(reader, null);
            for (int i = 0; i < count; i++)
            {
                ulong id = reader.ReadUInt64();
                bool rope = reader.ReadBoolean();
                Transform found = StableId.Resolve(id);
                MonoBehaviour part = found == null ? null : rope ? found.GetComponent<SwingRope>() : found.GetComponent<PushableBlock>();
                if (part == null) { throw new InvalidDataException($"The level has no {(rope ? "rope" : "pushable object")} with ID {id:X16}: it isn't built the same way."); }
                graph.ReadRoot(part, typeof(MonoBehaviour));
                Vector3 position = GraphReader.ReadVector3(reader);
                Quaternion rotation = GraphReader.ReadQuaternion(reader);
                Rigidbody body = part.GetComponent<Rigidbody>();
                // As a character's: moved only if it's somewhere else, so putting back where it is doesn't stutter it.
                bool there = body != null ? body.position == position && body.rotation == rotation
                    : part.transform.position == position && part.transform.rotation == rotation;
                if (there) { continue; }
                if (body != null)
                {
                    body.position = position;
                    body.rotation = rotation;
                }
                part.transform.SetPositionAndRotation(position, rotation);
            }
            Physics.SyncTransforms();
        }

        private static void Expect(BinaryReader reader, byte[] magic, string what)
        {
            byte[] read = reader.ReadBytes(magic.Length);
            if (read.Length != magic.Length || Encoding.ASCII.GetString(read) != Encoding.ASCII.GetString(magic)) { throw new InvalidDataException($"Not {what}."); }
            int version = reader.ReadInt32();
            if (version != FormatVersion) { throw new InvalidDataException($"{what} of format {version}; this reads {FormatVersion}."); }
        }

        // ---- What's written for each value ----

        private enum Tag : byte
        {
            Null, Shared, Object, Array, String, Own, Level, Kept, Boxed,
        }

        private enum UnityKind : byte { GameObject, Transform, Component }

        /// <summary>A type's instance fields, its bases' too (down to, not including, stopAt), in a fixed order: base first, then by name.</summary>
        private static FieldInfo[] FieldsOf(Type type, Type stopAt)
        {
            if (Fields.TryGetValue((type, stopAt), out FieldInfo[] cached)) { return cached; }
            var chain = new List<Type>();
            for (Type t = type; t != null && t != stopAt && t != typeof(object) && t != typeof(ValueType); t = t.BaseType) { chain.Add(t); }
            chain.Reverse();
            var fields = new List<FieldInfo>();
            foreach (Type t in chain)
            {
                FieldInfo[] declared = t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                Array.Sort(declared, (a, b) => string.CompareOrdinal(a.Name, b.Name));
                fields.AddRange(declared);
            }
            return Fields[(type, stopAt)] = fields.ToArray();
        }

        private static readonly Dictionary<(Type, Type), FieldInfo[]> Fields = new();

        /// <summary>
        /// Whether objects of the type are left as they are, not written: delegates and reflection, the query buffers
        /// (scratch space, refilled every query), and anything holding a native pointer (an AnimationCurve, say).
        /// </summary>
        private static bool IsKept(Type type)
        {
            if (typeof(Delegate).IsAssignableFrom(type) || typeof(MemberInfo).IsAssignableFrom(type)) { return true; }
            if (type.Namespace == "MeridianMotion.PhysicsQueries") { return true; }
            return HoldsPointer(type);
        }

        private static readonly Dictionary<Type, bool> PointerHolders = new();

        private static bool HoldsPointer(Type type)
        {
            if (PointerHolders.TryGetValue(type, out bool holds)) { return holds; }
            holds = false;
            foreach (FieldInfo field in FieldsOf(type, null))
            {
                if (field.FieldType == typeof(IntPtr) || field.FieldType == typeof(UIntPtr)) { holds = true; break; }
            }
            return PointerHolders[type] = holds;
        }

        private static string NameOf(Type type) => $"{type.FullName}, {type.Assembly.GetName().Name}";

        private static readonly Dictionary<string, Type> TypesByName = new();

        private static Type TypeNamed(string name)
        {
            if (TypesByName.TryGetValue(name, out Type type)) { return type; }
            type = Type.GetType(name);
            if (type == null) { throw new InvalidDataException($"No type '{name}' to read a state into: it's from another build."); }
            return TypesByName[name] = type;
        }

        /// <summary>The path from root to a transform under it: each step "name#k", k its place among same-named siblings.</summary>
        private static string PathFrom(Transform root, Transform transform)
        {
            if (transform == root) { return ""; }
            string parent = PathFrom(root, transform.parent);
            int before = 0;
            for (int i = 0; i < transform.GetSiblingIndex(); i++)
            {
                if (transform.parent.GetChild(i).name == transform.name) { before++; }
            }
            string step = $"{transform.name}#{before}";
            return parent.Length > 0 ? $"{parent}/{step}" : step;
        }

        private static Transform Follow(Transform root, string path)
        {
            if (path.Length == 0) { return root; }
            Transform at = root;
            foreach (string step in path.Split('/'))
            {
                int hash = step.LastIndexOf('#');
                string name = step.Substring(0, hash);
                int wanted = int.Parse(step.Substring(hash + 1)), seen = 0;
                Transform next = null;
                for (int i = 0; i < at.childCount && next == null; i++)
                {
                    Transform child = at.GetChild(i);
                    if (child.name != name) { continue; }
                    if (seen++ == wanted) { next = child; }
                }
                if (next == null) { return null; }
                at = next;
            }
            return at;
        }

        private sealed class ReferenceComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceComparer Instance = new();
            public new bool Equals(object a, object b) => ReferenceEquals(a, b);
            public int GetHashCode(object value) => RuntimeHelpers.GetHashCode(value);
        }
    }
}
