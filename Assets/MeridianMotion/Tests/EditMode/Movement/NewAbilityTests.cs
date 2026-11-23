using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using MeridianMotion.EditorTools;
using NUnit.Framework;
using UnityEngine;

namespace MeridianMotion.Tests.EditMode.Movement
{
    /// <summary>
    /// Tests of New Ability (AbilityScaffold), worked out against the project's own files without writing any: what it
    /// makes for each kind of answer is as recorded (NewAbilityExpected: record again with
    /// MERIDIAN_MOTION_RECORD_SCAFFOLDS=1 after changing a template, and check the diff); its state and events take the
    /// next unused bits at the flags' New Ability lines; it goes just after the component chosen, in the mode's order and
    /// in the composition test's; its docs are added; and answers it can't make are refused, saying why.
    /// </summary>
    [Category(TestArea.Movement)]
    [TestTiming]
    public class NewAbilityTests
    {
        private const string ExpectedFolder = "Assets/MeridianMotion/Tests/EditMode/Movement/NewAbilityExpected";
        private const string RecordVariable = "MERIDIAN_MOTION_RECORD_SCAFFOLDS";

        private static readonly (string label, AbilityScaffold.Answers answers)[] Cases =
        {
            ("TakesOverOnInteract", new AbilityScaffold.Answers { target = AbilityScaffold.Target.Package, name = "ZzHovering", kind = AbilityScaffold.Kind.TakesOver, button = AbilityScaffold.StartButton.Interact, after = "Rolling", needsIK = true }),
            ("OnTopOfWalkingOnJump", new AbilityScaffold.Answers { target = AbilityScaffold.Target.Package, name = "ZzBoosting", kind = AbilityScaffold.Kind.OnTopOfWalking, button = AbilityScaffold.StartButton.Jump, after = "Dashing" }),
            ("AtALevelElement", new AbilityScaffold.Answers { target = AbilityScaffold.Target.Package, name = "ZzFloating", kind = AbilityScaffold.Kind.TakesOver, button = AbilityScaffold.StartButton.None, levelElement = true, after = null, area = AbilityScaffold.Area.Traversal }),
            ("InTheProject", ProjectAnswers("ZzGliding", levelElement: true, needsIK: true)),
            ("InTheProjectOnJump", ProjectAnswers("ZzSkimming", levelElement: false, needsIK: false)),
        };

        // A project's ability is made in a folder of the test's (nothing is written), with a ledger that has given out bits
        // up to LedgerHighest, so the bits it gets don't depend on the project's own flags.
        private const string ProjectFolder = "Assets/ZzProjectAbilities";
        private const int LedgerHighest = 200;

        private static AbilityScaffold.Answers ProjectAnswers(string name, bool levelElement, bool needsIK) => new()
        {
            target = AbilityScaffold.Target.Project, projectFolder = ProjectFolder, name = name, levelElement = levelElement, needsIK = needsIK,
            kind = levelElement ? AbilityScaffold.Kind.TakesOver : AbilityScaffold.Kind.OnTopOfWalking,
            button = levelElement ? AbilityScaffold.StartButton.Interact : AbilityScaffold.StartButton.Jump,
        };

        private static string LedgerText()
        {
            var ledger = new CharacterFlagLedger();
            ledger.AddState("ZzOld.State", LedgerHighest);
            ledger.AddEvent("ZzOld.Started", LedgerHighest - 1);
            ledger.AddEvent("ZzOld.Ended", LedgerHighest);
            return ledger.ToJson();
        }

        // The project's files, with the test's ledger in the test's folder.
        private static string Read(string path) => path == $"{ProjectFolder}/{AbilityScaffold.LedgerFileName}" ? LedgerText() : NewAbilityWindow.ReadProjectFile(path);

        private static IEnumerable<string> CaseLabels => Cases.Select(c => c.label);

        private static AbilityScaffold.Answers AnswersFor(string label) => Cases.First(c => c.label == label).answers;

        private static List<AbilityScaffold.Change> Plan(AbilityScaffold.Answers answers)
        {
            Assert.That(AbilityScaffold.Problem(answers, Read), Is.Null, "Test setup: the answers should be ones it can make.");
            return AbilityScaffold.Plan(answers, Read, NewAbilityWindow.ReadTemplate);
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
            List<AbilityScaffold.Change> changes = Plan(AnswersFor("TakesOverOnInteract"));
            string states = NewAbilityWindow.ReadProjectFile(AbilityScaffold.StatePath);
            string events = NewAbilityWindow.ReadProjectFile(AbilityScaffold.EventsPath);
            int stateBit = AbilityScaffold.HighestBit(states.Substring(0, states.IndexOf(AbilityScaffold.EnumAnchor, StringComparison.Ordinal))) + 1;
            int eventBit = AbilityScaffold.HighestBit(events.Substring(0, events.IndexOf(AbilityScaffold.EnumAnchor, StringComparison.Ordinal))) + 1;

            string newStates = Changed(changes, AbilityScaffold.StatePath).text;
            Assert.That(newStates, Does.Contain($"public const int ZzHoveringBit = {stateBit};"));
            Assert.That(newStates, Does.Contain("public static readonly CharacterState ZzHovering = Bit(ZzHoveringBit);"));
            Assert.That(newStates.IndexOf("ZzHoveringBit =", StringComparison.Ordinal), Is.LessThan(newStates.IndexOf(AbilityScaffold.EnumAnchor, StringComparison.Ordinal)),
                "New members go above the New Ability line, so the next ability's go after them.");
            string newEvents = Changed(changes, AbilityScaffold.EventsPath).text;
            Assert.That(newEvents, Does.Contain($"public const int ZzHoveringStartedBit = {eventBit};"));
            Assert.That(newEvents, Does.Contain($"public const int ZzHoveringEndedBit = {eventBit + 1};"));
            Assert.That(newEvents, Does.Contain("public static readonly MovementEvents ZzHoveringEnded = Bit(ZzHoveringEndedBit);"));
        }

        [Test]
        public void ItGoesJustAfterTheComponentChosen_InTheModesOrder_AndTheTestsCopyOfIt()
        {
            List<AbilityScaffold.Change> changes = Plan(AnswersFor("TakesOverOnInteract"));
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
            // The docs are the showcase's (a project made from the package has none, and New Ability leaves them out).
            if (NewAbilityWindow.ReadProjectFile(AbilityScaffold.MovementDocPath) == null) { Assert.Ignore("This project has no docs folder to add to."); }
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
            string NoAnchor(string path) => path == AbilityScaffold.StatePath ? "public partial struct CharacterState { }" : NewAbilityWindow.ReadProjectFile(path);
            Assert.That(AbilityScaffold.Problem(new AbilityScaffold.Answers { target = AbilityScaffold.Target.Package, name = "ZzAnchorless" }, NoAnchor), Does.Contain("New Ability line"));
        }

        [Test]
        public void AProjectsAbility_IsMadeInItsOwnFolder_ChangingNothingOfThePackages()
        {
            foreach (string label in new[] { "InTheProject", "InTheProjectOnJump" })
            {
                foreach (AbilityScaffold.Change change in Plan(AnswersFor(label)))
                {
                    Assert.That(change.path, Does.StartWith(ProjectFolder + "/"), $"{label}: everything it makes should be in the project's abilities' folder.");
                }
            }
            List<AbilityScaffold.Change> changes = Plan(AnswersFor("InTheProject"));
            Assert.That(changes.Select(c => c.path), Does.Contain($"{ProjectFolder}/{AbilityScaffold.ProjectAssembly}.asmdef"), "Its assembly, the first time.");
            string component = Changed(changes, $"{ProjectFolder}/ZzGliding/ZzGliding.cs").text;
            Assert.That(component, Does.Contain("motor.StateOf<ZzGlidingState>()"), "Its state should be the motor's StateOf, not a part of the package's MMotionMotor.");
            Assert.That(component, Does.Not.Contain("partial class MMotionMotor"));
            Assert.That(component, Does.Contain("new(ZzGliding.State, ZzGliding.Started, ZzGliding.Ended)"), "Its state events should be its own flags.");
        }

        [Test]
        public void AProjectsFlags_TakeTheLedgersNextBits_AndAreRecordedInIt()
        {
            List<AbilityScaffold.Change> changes = Plan(AnswersFor("InTheProject"));
            string flags = Changed(changes, $"{ProjectFolder}/ZzGliding/ZzGliding.Flags.cs").text;
            Assert.That(flags, Does.Contain("[MovementFlags]"));
            Assert.That(flags, Does.Contain($"public const int StateBit = {LedgerHighest + 1};"));
            Assert.That(flags, Does.Contain($"public const int StartedBit = {LedgerHighest + 1};"));
            Assert.That(flags, Does.Contain($"public const int EndedBit = {LedgerHighest + 2};"));

            CharacterFlagLedger ledger = CharacterFlagLedger.Parse(Changed(changes, $"{ProjectFolder}/{AbilityScaffold.LedgerFileName}").text);
            Assert.That(ledger.states.Select(e => (e.name, e.bit)), Is.EqualTo(new[] { ("ZzOld.State", LedgerHighest), ("ZzGliding.State", LedgerHighest + 1) }),
                "The ledger keeps every bit it gave out, and adds the new ability's.");
            Assert.That(ledger.events.Select(e => e.bit), Is.EqualTo(new[] { LedgerHighest - 1, LedgerHighest, LedgerHighest + 1, LedgerHighest + 2 }));

            // Bits are never below the project's own, however empty the ledger.
            Assert.That(new CharacterFlagLedger().NextStateBit(), Is.GreaterThanOrEqualTo(MeridianMotion.Characters.Flags256.UserFirstBit));
        }

        [Test]
        public void AProjectsAbility_IsRefused_WithALedgerItCantRead_OrNoBitsLeft()
        {
            AbilityScaffold.Answers answers = ProjectAnswers("ZzRefused", levelElement: false, needsIK: false);
            string ledgerPath = $"{ProjectFolder}/{AbilityScaffold.LedgerFileName}";
            string Broken(string path) => path == ledgerPath ? "{ not json" : NewAbilityWindow.ReadProjectFile(path);
            Assert.That(AbilityScaffold.Problem(answers, Broken), Does.Contain("can't be read"));

            var full = new CharacterFlagLedger();
            full.AddState("ZzLast.State", MeridianMotion.Characters.Flags256.Size - 1);
            string Full(string path) => path == ledgerPath ? full.ToJson() : NewAbilityWindow.ReadProjectFile(path);
            Assert.That(AbilityScaffold.Problem(answers, Full), Does.Contain("no flag bits left"));
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
