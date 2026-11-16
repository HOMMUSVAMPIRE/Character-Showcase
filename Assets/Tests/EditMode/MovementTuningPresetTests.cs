using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Character;
using NUnit.Framework;
using UnityEngine;

namespace CharacterShowcase.Tests.EditMode
{
    /// <summary>
    /// Tests of movement tuning presets (MovementTuningPreset) and the settings they're made of (MovementSettings): a
    /// preset saved from a mode gives it back exactly the same tuning, curves and on/off switches included; one holding
    /// only some settings changes only those; a preset never adds, removes or reorders a mode's components, and reports
    /// what it couldn't set; and a component's settings are the fields Unity saves, all of kinds a preset can copy.
    /// </summary>
    public class MovementTuningPresetTests
    {
        private readonly List<Object> created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (Object obj in created) { Object.DestroyImmediate(obj); }
            created.Clear();
        }

        private T New<T>() where T : ScriptableObject
        {
            var obj = ScriptableObject.CreateInstance<T>();
            created.Add(obj);
            return obj;
        }

        /// <summary>Each component's settings as Unity saves them, in the mode's order.</summary>
        private static string[] Tuning(ComposedMovementMode mode) =>
            mode.Components.Select(c => $"{c.GetType().Name} {JsonUtility.ToJson(c)}").ToArray();

        /// <summary>Changes every setting of every component of the mode, so each differs from a new mode's.</summary>
        private static void ChangeEverything(ComposedMovementMode mode)
        {
            foreach (MovementComponent component in mode.Components)
            {
                foreach (FieldInfo setting in MovementSettings.Of(component.GetType()))
                {
                    object value = setting.GetValue(component);
                    setting.SetValue(component, value switch
                    {
                        float f => f + 1.5f,
                        int n => n + 2,
                        bool b => !b,
                        AnimationCurve _ => new AnimationCurve(new Keyframe(0f, 0.25f, 1f, -1f), new Keyframe(0.7f, 3f)) { postWrapMode = WrapMode.Loop },
                        _ => value,
                    });
                }
            }
        }

        [Test]
        public void ASettingsFields_AreTheFieldsUnitySaves_OfKindsAPresetCopies()
        {
            foreach (MovementComponent component in New<BasicMovementMode>().Components)
            {
                string[] saved = Regex.Matches(JsonUtility.ToJson(component), @"""(\w+)"":").Select(m => m.Groups[1].Value)
                    .Where(key => !Regex.IsMatch(key, "^(serializedVersion|m_\\w+|time|value|inSlope|outSlope|tangentMode|weightedMode|inWeight|outWeight)$"))
                    .ToArray();
                string[] settings = MovementSettings.Of(component.GetType()).Select(f => f.Name).ToArray();
                Assert.That(settings, Is.EquivalentTo(saved), $"{component.GetType().Name}'s settings should be the fields Unity saves.");
                Assert.That(settings[0], Is.EqualTo(MovementSettings.EnabledSetting), "The on/off switch comes first.");
                foreach (FieldInfo setting in MovementSettings.Of(component.GetType()))
                {
                    Assert.That(setting.FieldType.IsPrimitive || setting.FieldType.IsEnum || setting.FieldType == typeof(AnimationCurve), Is.True,
                        $"{component.GetType().Name}.{setting.Name} is a {setting.FieldType.Name}: MovementSettings.Copy would share it between a preset and a mode. Teach it to copy one.");
                }
            }
        }

        [Test]
        public void APresetSavedFromAMode_GivesItBackTheSameTuning()
        {
            var tuned = New<BasicMovementMode>();
            ChangeEverything(tuned);
            var preset = New<MovementTuningPreset>();
            preset.Capture(tuned);

            var other = New<BasicMovementMode>();
            Assume.That(Tuning(other), Is.Not.EqualTo(Tuning(tuned)));
            int changed = preset.ApplyTo(other);
            Assert.That(Tuning(other), Is.EqualTo(Tuning(tuned)), "Every setting, curves and on/off switches included, should come back.");
            Assert.That(changed, Is.GreaterThan(0));
            Assert.That(preset.ApplyTo(other), Is.EqualTo(0), "Applying it again changes nothing.");
        }

        [Test]
        public void APresetOfSomeSettings_ChangesOnlyThose()
        {
            var preset = New<MovementTuningPreset>();
            preset.Include(typeof(Jumping), "maxJumps", true);
            preset.PartFor(typeof(Jumping)).values.GetType().GetField("maxJumps", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(preset.PartFor(typeof(Jumping)).values, 5);
            preset.Include(typeof(Ziplines), MovementSettings.EnabledSetting, true);
            preset.PartFor(typeof(Ziplines)).values.Enabled = false;

            var mode = New<BasicMovementMode>();
            ChangeEverything(mode);
            mode.Get<Ziplines>(includeDisabled: true).Enabled = true;
            string[] before = Tuning(mode);
            Assert.That(preset.ApplyTo(mode), Is.EqualTo(2));
            Assert.That(mode.Get<Jumping>(includeDisabled: true).MaxJumps, Is.EqualTo(5));
            Assert.That(mode.Get<Ziplines>(includeDisabled: true).Enabled, Is.False);
            string[] after = Tuning(mode);
            for (int i = 0; i < before.Length; i++)
            {
                if (mode.Components[i] is Jumping or Ziplines) { continue; }
                Assert.That(after[i], Is.EqualTo(before[i]), $"{mode.Components[i].GetType().Name} wasn't in the preset, so it shouldn't change.");
            }
        }

        [Test]
        public void APreset_NeverAddsRemovesOrReordersComponents_AndReportsWhatItCantSet()
        {
            var source = New<BasicMovementMode>();
            var preset = New<MovementTuningPreset>();
            preset.Capture(source);
            preset.PartFor(typeof(Dashing)).settings.Add("noSuchSetting");

            var mode = New<BasicMovementMode>();
            mode.Remove<Ziplines>();
            mode.Remove<BouncePads>();
            mode.Add<BouncePads>(); // Now last.
            var order = mode.Components.Select(c => c.GetType()).ToArray();
            var problems = new List<string>();
            preset.ApplyTo(mode, problems);
            Assert.That(mode.Components.Select(c => c.GetType()), Is.EqualTo(order), "Applying a preset shouldn't change the mode's components or their order.");
            Assert.That(problems, Has.Some.Contains("Ziplines"), "A component the mode doesn't have is reported.");
            Assert.That(problems, Has.Some.Contains("noSuchSetting"), "A setting its component doesn't have is reported.");
        }

        [Test]
        public void APresetsCurves_AreCopied_NotShared()
        {
            var source = New<BasicMovementMode>();
            var preset = New<MovementTuningPreset>();
            preset.Capture(source);
            var mode = New<BasicMovementMode>();
            ChangeEverything(mode);
            preset.ApplyTo(mode);
            FieldInfo curve = MovementSettings.Find(typeof(Dashing), "dashSpeedCurve");
            Assume.That(curve, Is.Not.Null, "Test setup: Dashing should have a speed curve.");
            var inPreset = (AnimationCurve)curve.GetValue(preset.PartFor(typeof(Dashing)).values);
            var inMode = (AnimationCurve)curve.GetValue(mode.Get<Dashing>(includeDisabled: true));
            Assert.That(inMode, Is.Not.SameAs(inPreset));
            Assert.That(curve.GetValue(preset.PartFor(typeof(Dashing)).values), Is.Not.SameAs(curve.GetValue(source.Get<Dashing>())),
                "Saving a preset copies the mode's curves too.");
        }
    }
}
