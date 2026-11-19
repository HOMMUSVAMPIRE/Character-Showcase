using System;
using System.Reflection;
using MeridianMotion.Characters;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MeridianMotion.Tests.EditMode.Movement
{
    /// <summary>
    /// Checks of MovementStep's Reset, which lets a character reuse its movement data step after step without allocating
    /// (see MMotionMotor.StartStep): a field Reset misses would carry one step's value into the next. Every field
    /// (backing fields too) is set to something other than a new object's, then Reset must put each back.
    /// </summary>
    [Category(TestArea.Movement)]
    [TestTiming]
    public class MovementStepTests
    {
        private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private GameObject ground;

        [TearDown]
        public void TearDown()
        {
            if (ground != null) { Object.DestroyImmediate(ground); }
        }

        [Test]
        public void Reset_PutsEveryFieldBackAsANewOnesIs()
        {
            ground = new GameObject("Test Ground");
            var used = new MovementStep();
            foreach (FieldInfo field in typeof(MovementStep).GetFields(Fields))
            {
                field.SetValue(used, NotDefault(field.FieldType, field.Name));
            }

            used.Reset();

            var fresh = new MovementStep();
            foreach (FieldInfo field in typeof(MovementStep).GetFields(Fields))
            {
                Assert.That(field.GetValue(used), Is.EqualTo(field.GetValue(fresh)),
                    $"MovementStep.Reset should put '{field.Name}' back as a new MovementStep has it.");
            }
        }

        /// <summary>
        /// A value of the type other than its default (and other than the fields' initializers: none are this). A struct is
        /// filled field by field. Fails on a type it doesn't know, so a new kind of field gets a value here.
        /// </summary>
        private object NotDefault(Type type, string name)
        {
            if (type == typeof(float)) { return 1.5f; }
            if (type == typeof(int)) { return 3; }
            // A word of the flags' bits (Flags256), the highest bit set: a reset clears every word.
            if (type == typeof(ulong)) { return 1UL << 63; }
            // MovementStep's own bools are the opposite of what a new one has (dirty starts true); a struct's start false.
            if (type == typeof(bool)) { return name == null || !(bool)typeof(MovementStep).GetField(name, Fields).GetValue(new MovementStep()); }
            if (type.IsEnum) { return Enum.ToObject(type, 1L); }
            if (type == typeof(Vector3)) { return new Vector3(1f, 2f, 3f); }
            if (type == typeof(Quaternion)) { return new Quaternion(0.1f, 0.2f, 0.3f, 0.9f); }
            if (type == typeof(Vector3?)) { return (Vector3?)new Vector3(4f, 5f, 6f); }
            if (type == typeof(Transform)) { return ground.transform; }
            if (type.IsValueType && !type.IsPrimitive)
            {
                object filled = Activator.CreateInstance(type);
                foreach (FieldInfo inner in type.GetFields(Fields)) { inner.SetValue(filled, NotDefault(inner.FieldType, null)); }
                return filled;
            }
            Assert.Fail($"MovementStepTests doesn't know how to fill a {type.Name} ('{name}'): add it to NotDefault.");
            return null;
        }
    }
}
