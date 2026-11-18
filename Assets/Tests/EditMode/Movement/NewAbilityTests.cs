using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using CharacterShowcase.EditorTools;
using NUnit.Framework;
using UnityEngine;

namespace CharacterShowcase.Tests.EditMode.Movement
{
    /// <summary>
    /// Tests of New Ability (AbilityScaffold), worked out against the project's own files without writing any: what it
    /// makes for each kind of answer is as recorded (NewAbilityExpected: record again with
    /// CHARACTER_SHOWCASE_RECORD_SCAFFOLDS=1 after changing a template, and check the diff); its state and events take the
    /// next unused bits at the enums' New Ability lines; it goes just after the component chosen, in the mode's order and
    /// in the composition test's; its docs are added; and answers it can't make are refused, saying why.
    /// </summary>
    [Category(TestArea.Movement)]
    [TestTiming]
    public class NewAbilityTests
    {
        private const string ExpectedFolder = "Assets/Tests/EditMode/Movement/NewAbilityExpected";
        private const string RecordVariable = "CHARACTER_SHOWCASE_RECORD_SCAFFOLDS";

        private static readonly (string label, AbilityScaffold.Answers answers)[] Cases =
        {
            ("TakesOverOnGrab", new AbilityScaffold.Answers { name = "ZzHovering", kind = AbilityScaffold.Kind.TakesOver, button = AbilityScaffold.StartButton.Grab, after = "Rolling" }),
            ("OnTopOfWalkingOnJump", new AbilityScaffold.Answers { name = "ZzBoosting", kind = AbilityScaffold.Kind.OnTopOfWalking, button = AbilityScaffold.StartButton.Jump, after = "Dashing" }),
            ("AtALevelElement", new AbilityScaffold.Answers { name = "ZzFloating", kind = AbilityScaffold.Kind.TakesOver, button = AbilityScaffold.StartButton.None, levelElement = true, after = null, area = AbilityScaffold.Area.Traversal }),
        };

        private static IEnumerable<string> CaseLabels => Cases.Select(c => c.label);

        private static AbilityScaffold.Answers AnswersFor(string label) => Cases.First(c => c.label == label).answers;

        private static List<AbilityScaffold.Change> Plan(AbilityScaffold.Answers answers)
        {
            Assert.That(AbilityScaffold.Problem(answers, NewAbilityWindow.ReadProjectFile), Is.Null, "Test setup: the answers should be ones it can make.");
            return AbilityScaffold.Plan(answers, NewAbilityWindow.ReadProjectFile, NewAbilityWindow.ReadTemplate);
        }

        private static AbilityScaffold.Change Changed(List<AbilityScaffold.Change> changes, string path)
        {
            AbilityScaffold.Change change = changes.FirstOrDefault(c => c.path == path);
            Assert.That(change, Is.Not.Null, $"It should change {path}.");
            return change;
        }

        [Test]
        public void WhatItMakes_IsAsRecorded([ValueSource(nameof(CaseLabels))] string label)
        {
            string root = Path.GetDirectoryName(Application.dataPath);
            bool record = Environment.GetEnvironmentVariable(RecordVariable) == "1";
            foreach (AbilityScaffold.Change change in Plan(AnswersFor(label)).Where(c => c.isNew))
            {
                string expectedPath = Path.Combine(root, ExpectedFolder, label, Path.GetFileName(change.path) + ".txt");
                if (record)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(expectedPath));
                    File.WriteAllText(expectedPath, change.text);
                    continue;
                }
                Assert.That(File.Exists(expectedPath), Is.True, $"There's no recorded {Path.GetFileName(change.path)} for {label}: record them ({RecordVariable}=1).");
                Assert.That(change.text, Is.EqualTo(File.ReadAllText(expectedPath).Replace("\r\n", "\n")),
                    $"{Path.GetFileName(change.path)} for {label} differs from what's recorded: if a template changed on purpose, record them again ({RecordVariable}=1) and check the diff.");
            }
            if (record) { Assert.Inconclusive("Recorded New Ability's output: check the diff, then run again without " + RecordVariable + "."); }
        }

        [Test]
        public void ItsStateAndEvents_TakeTheNextUnusedBits_AtTheEnumsNewAbilityLines()
        {
            List<AbilityScaffold.Change> changes = Plan(AnswersFor("TakesOverOnGrab"));
            string states = NewAbilityWindow.ReadProjectFile(AbilityScaffold.StatePath);
            string events = NewAbilityWindow.ReadProjectFile(AbilityScaffold.EventsPath);
            int stateBit = AbilityScaffold.HighestBit(states.Substring(0, states.IndexOf(AbilityScaffold.EnumAnchor, StringComparison.Ordinal))) + 1;
            int eventBit = AbilityScaffold.HighestBit(events.Substring(0, events.IndexOf(AbilityScaffold.EnumAnchor, StringComparison.Ordinal))) + 1;

            string newStates = Changed(changes, AbilityScaffold.StatePath).text;
            Assert.That(newStates, Does.Contain($"ZzHovering = 1L << {stateBit},"));
            Assert.That(newStates.IndexOf("ZzHovering =", StringComparison.Ordinal), Is.LessThan(newStates.IndexOf(AbilityScaffold.EnumAnchor, StringComparison.Ordinal)),
                "New members go above the New Ability line, so the next ability's go after them.");
            string newEvents = Changed(changes, AbilityScaffold.EventsPath).text;
            Assert.That(newEvents, Does.Contain($"ZzHoveringStarted = 1L << {eventBit},"));
            Assert.That(newEvents, Does.Contain($"ZzHoveringEnded = 1L << {eventBit + 1},"));
        }

        [Test]
        public void ItGoesJustAfterTheComponentChosen_InTheModesOrder_AndTheTestsCopyOfIt()
        {
            List<AbilityScaffold.Change> changes = Plan(AnswersFor("TakesOverOnGrab"));
            string mode = Changed(changes, AbilityScaffold.ModePath).text;
            Assert.That(Regex.IsMatch(mode, @"yield return new Rolling\(\);\s*\n\s*yield return new ZzHovering\(\);"), Is.True, "It should follow Rolling in DefaultComponents.");
            string test = Changed(changes, AbilityScaffold.CompositionTestPath).text;
            Assert.That(test, Does.Contain("typeof(Rolling), typeof(ZzHovering)"));

            List<AbilityScaffold.Change> first = Plan(AnswersFor("AtALevelElement"));
            Assert.That(Regex.IsMatch(Changed(first, AbilityScaffold.ModePath).text, @"\{\s*yield return new ZzFloating\(\);\s*\n\s*yield return new BouncePads\(\);"), Is.True,
                "First in the order, it should come before Bounce Pads.");
            Assert.That(Changed(first, AbilityScaffold.CompositionTestPath).text, Does.Contain("typeof(ZzFloating), typeof(BouncePads)"));
        }

        [Test]
        public void ItsDocs_AreAdded_ToItsAreasPage_AndTheComponentList()
        {
            List<AbilityScaffold.Change> changes = Plan(AnswersFor("OnTopOfWalkingOnJump"));
            Assert.That(Changed(changes, "docs/abilities.md").text, Does.Contain("\n## Zz Boosting\n"));
            Assert.That(Changed(changes, AbilityScaffold.MovementDocPath).text, Does.Contain("Dashing, Zz Boosting and Jumping"));
            Assert.That(Changed(Plan(AnswersFor("AtALevelElement")), "docs/traversal.md").text, Does.Contain("\n## Zz Floating\n"));
        }

        [Test]
        public void AnswersItCantMake_AreRefused_SayingWhy()
        {
            string Problem(AbilityScaffold.Answers answers) => AbilityScaffold.Problem(answers, NewAbilityWindow.ReadProjectFile);
            Assert.That(Problem(new AbilityScaffold.Answers { name = "not a name" }), Does.Contain("class name"));
            Assert.That(Problem(new AbilityScaffold.Answers { name = "Dashing" }), Does.Contain("already"), "A name that's taken.");
            Assert.That(Problem(new AbilityScaffold.Answers { name = "ZzNothing", button = AbilityScaffold.StartButton.None }), Does.Contain("what starts it"));
            Assert.That(Problem(new AbilityScaffold.Answers { name = "ZzNowhere", after = "NoSuchComponent" }), Does.Contain("isn't one of the default components"));
            string NoAnchor(string path) => path == AbilityScaffold.StatePath ? "public enum CharacterState : long { Idle = 0, }" : NewAbilityWindow.ReadProjectFile(path);
            Assert.That(AbilityScaffold.Problem(new AbilityScaffold.Answers { name = "ZzAnchorless" }, NoAnchor), Does.Contain("New Ability line"));
        }

        [Test]
        public void ATemplatesBlocks_AreKeptOrLeftOut_ByItsAnswers()
        {
            const string template = "a {{Name}}\n{{#on}}\nkept\n{{^off}}\nnested\n{{/off}}\n{{/on}}\n{{^on}}\nleft out\n{{/on}}\nend\n";
            string rendered = AbilityScaffold.Render(template, new Dictionary<string, string> { ["Name"] = "X" }, new HashSet<string> { "on" });
            Assert.That(rendered, Is.EqualTo("a X\nkept\nnested\nend\n"));
        }
    }
}
