using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using MeridianMotion.Characters;
using UnityEditor;

namespace MeridianMotion.EditorTools
{
    /// <summary>
    /// What New Ability (NewAbilityWindow) makes from its answers, worked out as a list of file changes before anything is
    /// written, so the window can show them and a test can check them without touching the project. Where it goes is the
    /// first answer (Target). In the project (the default): everything in the project's own folder ([output folder]/Abilities,
    /// its own assembly, Abilities, and its tests' assembly, Abilities.Tests), so a package upgrade can't overwrite it and
    /// nothing of the package's is changed:
    /// - the ability's component (its state kept with motor.StateOf), and its flags ([Name].Flags.cs: its state and two
    ///   events, on bits from Flags256.UserFirstBit that the project's ledger, Character Flags.json, gives out and records);
    /// - optionally, a level element it starts at and an IK component of its own, beside it;
    /// - its PlayMode test fixture, on the ability test kit, if the package's tests are in the project;
    /// - the two assemblies' definitions, the first time.
    /// Once it's compiled, the project's own movement assets that were chosen are given it, turned on
    /// (MovementModeSync.AddToProjectModes). In the package (for developing Meridian Motion itself), it makes:
    /// - the ability's component, a small working ability to build on (from NewAbilityTemplates/Component.txt): its
    ///   settings, its hooks, its state and that state's place on the MMotionMotor;
    /// - its CharacterState flag and its two MovementEvents, each the next unused bit (at the flags' New Ability lines);
    /// - its place in StandardMovementMode.DefaultComponents, and in MovementCompositionTests' copy of that order;
    /// - its PlayMode test fixture, on the ability test kit (AbilityFixture);
    /// - optionally, a level element it starts at (Assets/MeridianMotion/Runtime/LevelElements), and an IK component of its own (Assets/MeridianMotion/Runtime/Characters/IK);
    /// - a section for it in docs/abilities.md or docs/traversal.md, and its name in docs/movement.md's component list.
    /// The movement mode assets are given it once it's compiled (MovementModeSync.AddToAllModes: turned on for the
    /// default character's mode, off for the others). See docs/adding-an-ability.md.
    /// </summary>
    public static class AbilityScaffold
    {
        /// <summary>Where it acts: taking the character over, or on top of walking.</summary>
        public enum Kind { TakesOver, OnTopOfWalking }

        /// <summary>The button a press of which starts it (one Inputs has: a new button is a step of its own, see AGENTS.md).</summary>
        public enum StartButton { None, Jump, Dash, Crouch, Roll, Interact }

        /// <summary>Which docs page and test folder it belongs to.</summary>
        public enum Area { Abilities, Traversal }

        /// <summary>
        /// Where it's made: in the project, its own folder and assembly (what a package user does), or in the package
        /// itself (developing Meridian Motion: its files, flags and default components).
        /// </summary>
        public enum Target { Project, Package }

        /// <summary>The window's answers.</summary>
        public sealed class Answers
        {
            /// <summary>Its class name (PascalCase, a gerund as the others are: Gliding).</summary>
            public string name = "";
            public Kind kind = Kind.TakesOver;
            public StartButton button = StartButton.Interact;
            /// <summary>Whether it starts at a level element of its own (made with it).</summary>
            public bool levelElement;
            /// <summary>Whether it gets an IK component of its own, placing the limbs while it's under way (made with it).</summary>
            public bool needsIK;
            /// <summary>The default component it goes just after in the order (its class name), or null for first.</summary>
            public string after = nameof(Rolling);
            public Area area = Area.Abilities;
            public Target target = Target.Project;
            /// <summary>The folder a project's abilities go in (Target.Project), or null for ProjectAbilitiesFolder.</summary>
            public string projectFolder;
            /// <summary>
            /// The project's own movement assets (paths) to give it to once it's compiled (Target.Project), or null for all of
            /// them (MovementModeSync.ProjectModes).
            /// </summary>
            public List<string> modes;

            /// <summary>The folder a project's abilities go in.</summary>
            public string ProjectFolder => string.IsNullOrEmpty(projectFolder) ? ProjectAbilitiesFolder : projectFolder.TrimEnd('/');
        }

        /// <summary>A file to write: a new one, or an existing one's new text.</summary>
        public sealed class Change
        {
            public string path;
            public string text;
            public bool isNew;
            /// <summary>Whether it can be left out if its file isn't there (docs and tests, which a package may not have).</summary>
            public bool optional;
            /// <summary>What it does, for the window's list.</summary>
            public string summary;
        }

        public const string ComponentsFolder = "Assets/MeridianMotion/Runtime/Characters/MovementModes/Components";
        public const string StatePath = "Assets/MeridianMotion/Runtime/Characters/Character/CharacterState.cs";
        public const string EventsPath = "Assets/MeridianMotion/Runtime/Characters/Character/MovementEvents.cs";
        public const string ModePath = "Assets/MeridianMotion/Runtime/Characters/MovementModes/StandardMovementMode.cs";
        public const string CompositionTestPath = "Assets/MeridianMotion/Tests/EditMode/Movement/MovementCompositionTests.cs";
        public const string FixturesFolder = "Assets/MeridianMotion/Tests/PlayMode";
        public const string LevelFolder = "Assets/MeridianMotion/Runtime/LevelElements";
        public const string IKFolder = "Assets/MeridianMotion/Runtime/Characters/IK";
        public const string MovementDocPath = "docs/movement.md";
        /// <summary>A project's abilities' assembly and namespace, and its tests'.</summary>
        public const string ProjectAssembly = "Abilities";
        public const string ProjectTestsAssembly = "Abilities.Tests";
        /// <summary>The ledger of a project's flags' bits, in its abilities' folder (see CharacterFlagLedger).</summary>
        public const string LedgerFileName = "Character Flags.json";
        // The ability test kit, if the package's PlayMode tests are in the project: a project's ability gets tests on it only then.
        private const string TestKitType = "MeridianMotion.Tests.AbilityFixture`1, MeridianMotion.Tests.PlayMode";

        /// <summary>The folder New Ability puts a project's abilities in: Abilities in the output folder (MeridianMotionSettings).</summary>
        public static string ProjectAbilitiesFolder => MeridianMotionSettings.OutputFolder + "/" + ProjectAssembly;
        /// <summary>The line in CharacterState.cs and MovementEvents.cs that new members go above.</summary>
        public const string EnumAnchor = "// New Ability (NewAbilityWindow) adds";
        private const string CompositionTestName = "ANewStandardMovementMode_StartsWithItsComponents_InOrder";

        /// <summary>The names derived from the answers: the class's, its state's, its events', its settings' prefix.</summary>
        public readonly struct Names
        {
            public readonly string type, field, display, state, started, ended, element, ik;

            public Names(string name)
            {
                type = name;
                field = name.Length > 0 ? char.ToLowerInvariant(name[0]) + name.Substring(1) : name;
                display = MovementComponent.SpacedName(name);
                state = name;
                started = name + "Started";
                ended = name + "Ended";
                element = name + "Point";
                ik = name + "IK";
            }
        }

        /// <summary>
        /// Why the answers can't be made, or null if they can: a name that isn't a class name or is taken (as a type, a
        /// state, an event, a member of the MMotionMotor or a setting), no way of starting it, a place in the
        /// order that isn't one, a file it must change missing its New Ability line (in the package), or a ledger it
        /// can't read or with no bits left (in the project).
        /// </summary>
        public static string Problem(Answers answers, Func<string, string> read)
        {
            string name = answers.name?.Trim() ?? "";
            if (!Regex.IsMatch(name, "^[A-Z][A-Za-z0-9]*$")) { return "Give it a name: a class name, starting with a capital letter (Gliding, say)."; }
            var names = new Names(name);
            if (answers.button == StartButton.None && !answers.levelElement) { return "Choose what starts it: a button, a level element, or both."; }
            if (TypeNames.Value.Contains(name) || TypeNames.Value.Contains(names.type + "State") || (answers.levelElement && TypeNames.Value.Contains(names.element))
                || (answers.needsIK && TypeNames.Value.Contains(names.ik)))
            {
                return $"There's already a type called {name}, {name}State{(answers.levelElement ? $" or {names.element}" : "")}{(answers.needsIK ? $" or {names.ik}" : "")}: choose another name.";
            }
            bool package = answers.target == Target.Package;
            string state = package ? names.state : ProjectFlag(names, "State");
            string started = package ? names.started : ProjectFlag(names, "Started");
            string ended = package ? names.ended : ProjectFlag(names, "Ended");
            if (CharacterState.TryParse(state, out _)) { return $"There's already a state called {state}: choose another name."; }
            if (MovementEvents.TryParse(started, out _) || MovementEvents.TryParse(ended, out _)) { return $"There's already a movement event called {started} or {ended}: choose another name."; }
            if (package && typeof(MMotionMotor).GetMember(names.field).Length > 0) { return $"The MMotionMotor already has a member called {names.field}: choose another name."; }
            HashSet<string> settings = ExistingSettingNames();
            foreach (string setting in SettingNames(answers, names))
            {
                if (settings.Contains(setting)) { return $"There's already a movement setting called {setting} (setting names are unique): choose another name."; }
            }
            if (answers.after != null && !DefaultComponentNames().Contains(answers.after)) { return $"{answers.after} isn't one of the default components."; }
            if (read(ComponentPath(answers, names)) != null) { return $"{ComponentPath(answers, names)} is already there."; }
            if (!package)
            {
                if (MeridianMotionAssets.InPackage(answers.ProjectFolder)) { return $"{answers.ProjectFolder} is in the package, which an upgrade replaces: set an output folder outside it (Project Settings > Meridian Motion)."; }
                CharacterFlagLedger ledger;
                try { ledger = CharacterFlagLedger.Parse(read(LedgerPath(answers))); }
                catch (ArgumentException exception) { return $"{LedgerPath(answers)} can't be read ({exception.Message}): put it right, or back as it was."; }
                if (ledger.NextStateBit() >= Flags256.Size || ledger.NextEventBit() + 1 >= Flags256.Size)
                {
                    return $"The project has no flag bits left for another ability (its own are {Flags256.UserFirstBit} to {Flags256.Size - 1}: see {LedgerPath(answers)}).";
                }
                return null;
            }
            foreach (string path in new[] { StatePath, EventsPath })
            {
                string text = read(path);
                if (text == null || !text.Contains(EnumAnchor)) { return $"{path} has no New Ability line ('{EnumAnchor} …') to add to: put it back above the struct's closing brace."; }
            }
            string mode = read(ModePath);
            if (mode == null || !mode.Contains("DefaultComponents()")) { return $"{ModePath} has no DefaultComponents to add it to."; }
            return null;
        }

        /// <summary>A project's ability's flag's name, as the registry gives it ([MovementFlags]): GroundPound.State.</summary>
        public static string ProjectFlag(Names names, string field) => names.type + "." + field;

        // Every type's name, found once a domain (the window asks on every repaint): a new ability's name mustn't be one.
        private static readonly Lazy<HashSet<string>> TypeNames = new(() => new HashSet<string>(TypeCache.GetTypesDerivedFrom<object>().Select(t => t.Name)));

        /// <summary>The settings it's made with, by name (their names are unique across a mode and its components).</summary>
        public static IEnumerable<string> SettingNames(Answers answers, Names names)
        {
            yield return names.field + "Duration";
            if (answers.kind == Kind.OnTopOfWalking) { yield return names.field + "Speed"; }
            if (answers.button != StartButton.None) { yield return names.field + "InputBuffer"; }
            if (answers.levelElement) { yield return names.field + "Reach"; }
        }

        /// <summary>The default components' class names, in order (StandardMovementMode.DefaultComponents).</summary>
        public static List<string> DefaultComponentNames()
        {
            var mode = UnityEngine.ScriptableObject.CreateInstance<StandardMovementMode>();
            try { return mode.DefaultComponentTypes().Select(t => t.Name).ToList(); }
            finally { UnityEngine.Object.DestroyImmediate(mode); }
        }

        private static HashSet<string> ExistingSettingNames()
        {
            const System.Reflection.BindingFlags Fields = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public |
                                                          System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.DeclaredOnly;
            var names = new HashSet<string>();
            foreach (Type type in TypeCache.GetTypesDerivedFrom<MovementComponent>().Append(typeof(StandardMovementMode)).Append(typeof(AbilityMovementMode)))
            {
                foreach (var field in type.GetFields(Fields)) { names.Add(field.Name); }
            }
            return names;
        }

        public static string ComponentPath(Answers answers, Names names) =>
            answers.target == Target.Package ? $"{ComponentsFolder}/{names.type}.cs" : $"{answers.ProjectFolder}/{names.type}/{names.type}.cs";
        public static string FlagsPath(Answers answers, Names names) => $"{answers.ProjectFolder}/{names.type}/{names.type}.Flags.cs";
        public static string FixturePath(Answers answers, Names names) =>
            answers.target == Target.Package ? $"{FixturesFolder}/{answers.area}/{names.type}Tests.cs" : $"{answers.ProjectFolder}/Tests/{names.type}Tests.cs";
        public static string ElementPath(Answers answers, Names names) =>
            answers.target == Target.Package ? $"{LevelFolder}/{names.element}.cs" : $"{answers.ProjectFolder}/{names.type}/{names.element}.cs";
        public static string IKPath(Answers answers, Names names) =>
            answers.target == Target.Package ? $"{IKFolder}/{names.ik}.cs" : $"{answers.ProjectFolder}/{names.type}/{names.ik}.cs";
        public static string LedgerPath(Answers answers) => $"{answers.ProjectFolder}/{LedgerFileName}";
        public static string AssemblyPath(Answers answers) => $"{answers.ProjectFolder}/{ProjectAssembly}.asmdef";
        public static string TestsAssemblyPath(Answers answers) => $"{answers.ProjectFolder}/Tests/{ProjectTestsAssembly}.asmdef";
        public static string AreaDocPath(Answers answers) => answers.area == Area.Abilities ? "docs/abilities.md" : "docs/traversal.md";

        /// <summary>Whether the package's ability test kit (its PlayMode tests) is in the project, for a project's ability's tests.</summary>
        public static bool HasTestKit => Type.GetType(TestKitType) != null;

        /// <summary>
        /// Every file change the answers make (call Problem first). read gives a file's text by its project-relative
        /// path (null if it isn't there), template a template's text by its name (Component, Flags, Fixture, LevelElement, IK).
        /// </summary>
        public static List<Change> Plan(Answers answers, Func<string, string> read, Func<string, string> template)
        {
            var names = new Names(answers.name.Trim());
            bool package = answers.target == Target.Package;
            var values = new Dictionary<string, string>
            {
                ["Name"] = names.type,
                ["name"] = names.field,
                ["Display"] = names.display,
                ["StateFlag"] = names.state,
                ["Started"] = names.started,
                ["Ended"] = names.ended,
                ["Element"] = names.element,
                ["ElementDisplayTail"] = "Point",
                ["Area"] = answers.area.ToString(),
                ["Button"] = answers.button.ToString().ToLowerInvariant(),
                ["ButtonDisplay"] = answers.button.ToString(),
                // What differs between the package's abilities and a project's: where they are, how they reach their
                // state (a part of the partial MMotionMotor, or StateOf) and their flags (CharacterState's, or their own).
                ["Namespace"] = package ? "MeridianMotion.Characters" : ProjectAssembly,
                ["TestNamespace"] = package ? $"MeridianMotion.Tests.{answers.area}" : $"{ProjectAssembly}.Tests",
                ["ElementNamespace"] = package ? "MeridianMotion.Levels" : ProjectAssembly,
                ["ElementFull"] = package ? $"MeridianMotion.Levels.{names.element}" : names.element,
                ["MenuRoot"] = package ? "Meridian Motion" : ProjectAssembly,
                ["Partial"] = package ? "" : "partial ",
                ["StateAccess"] = package ? $"motor.{names.field}" : $"motor.StateOf<{names.type}State>()",
                ["StateRef"] = package ? $"CharacterState.{names.state}" : ProjectFlag(names, "State"),
                ["StartedRef"] = package ? $"MovementEvents.{names.started}" : ProjectFlag(names, "Started"),
                ["EndedRef"] = package ? $"MovementEvents.{names.ended}" : ProjectFlag(names, "Ended"),
            };
            var flags = new HashSet<string>();
            flags.Add(answers.kind == Kind.TakesOver ? "takesOver" : "onTopOfWalking");
            if (answers.button != StartButton.None) { flags.Add("button"); }
            if (answers.levelElement) { flags.Add("levelElement"); }
            if (package) { flags.Add("package"); }

            var changes = new List<Change>
            {
                new() { path = ComponentPath(answers, names), isNew = true, text = Render(template("Component"), values, flags), summary = $"The {names.display} component: a small working ability to build on" },
            };
            if (!package) { AddProjectFlags(changes, answers, names, values, flags, read, template); }
            if (answers.levelElement)
            {
                changes.Add(new Change { path = ElementPath(answers, names), isNew = true, text = Render(template("LevelElement"), values, flags), summary = $"The level element it starts at ({names.element})" });
            }
            if (answers.needsIK)
            {
                changes.Add(new Change { path = IKPath(answers, names), isNew = true, text = Render(template("IK"), values, flags), summary = $"Its IK ({names.ik}), on every humanoid model once Prepare Humanoid Models runs" });
            }
            if (!package)
            {
                AddProjectAssemblies(changes, answers, names, values, flags, read, template);
                return changes;
            }
            changes.Add(new Change { path = StatePath, text = AddEnumMembers(read(StatePath), $"{names.display} (see {names.type})", names.state), summary = $"Its state, CharacterState.{names.state}" });
            changes.Add(new Change { path = EventsPath, text = AddEnumMembers(read(EventsPath), $"{names.display} starting and ending (see {names.type})", names.started, names.ended), summary = $"Its events, {names.started} and {names.ended}" });
            changes.Add(new Change { path = ModePath, text = AddToDefaultComponents(read(ModePath), names.type, answers.after), summary = answers.after == null ? "First in StandardMovementMode's default components" : $"After {answers.after} in StandardMovementMode's default components" });

            string test = read(CompositionTestPath);
            if (test != null) { changes.Add(new Change { path = CompositionTestPath, optional = true, text = AddToCompositionTest(test, names.type, answers.after), summary = "The same place in MovementCompositionTests' copy of the order" }); }
            if (read($"{FixturesFolder}/AbilityFixture.cs") != null)
            {
                changes.Add(new Change { path = FixturePath(answers, names), isNew = true, optional = true, text = Render(template("Fixture"), values, flags), summary = $"Its tests, {names.type}Tests, on the ability test kit" });
            }
            string areaDoc = read(AreaDocPath(answers));
            if (areaDoc != null) { changes.Add(new Change { path = AreaDocPath(answers), optional = true, text = AddDocSection(areaDoc, answers, names), summary = $"A section for it in {AreaDocPath(answers)}, to write" }); }
            string movementDoc = read(MovementDocPath);
            string listed = movementDoc != null ? AddToComponentList(movementDoc, names.display, answers.after) : null;
            if (listed != null) { changes.Add(new Change { path = MovementDocPath, optional = true, text = listed, summary = $"Its name in {MovementDocPath}'s component list" }); }
            return changes;
        }

        // A project's ability's flags: the next bits the ledger gives out, written into its [Name].Flags.cs and recorded.
        private static void AddProjectFlags(List<Change> changes, Answers answers, Names names, Dictionary<string, string> values,
                                            HashSet<string> flags, Func<string, string> read, Func<string, string> template)
        {
            string ledgerText = read(LedgerPath(answers));
            CharacterFlagLedger ledger = CharacterFlagLedger.Parse(ledgerText);
            int stateBit = ledger.NextStateBit();
            int startedBit = ledger.NextEventBit();
            int endedBit = startedBit + 1;
            ledger.AddState(ProjectFlag(names, "State"), stateBit);
            ledger.AddEvent(ProjectFlag(names, "Started"), startedBit);
            ledger.AddEvent(ProjectFlag(names, "Ended"), endedBit);
            values["StateBit"] = stateBit.ToString();
            values["StartedBit"] = startedBit.ToString();
            values["EndedBit"] = endedBit.ToString();
            values["Ledger"] = LedgerFileName;
            changes.Add(new Change { path = FlagsPath(answers, names), isNew = true, text = Render(template("Flags"), values, flags),
                                     summary = $"Its state and events ({ProjectFlag(names, "State")}, bit {stateBit}; {ProjectFlag(names, "Started")} and {ProjectFlag(names, "Ended")}, bits {startedBit} and {endedBit})" });
            changes.Add(new Change { path = LedgerPath(answers), isNew = ledgerText == null, text = ledger.ToJson(),
                                     summary = "The project's ledger of its flags' bits, with its three" });
        }

        // A project's ability's tests (if the kit is in the project), and the assemblies it and they are in, the first time.
        private static void AddProjectAssemblies(List<Change> changes, Answers answers, Names names, Dictionary<string, string> values,
                                                 HashSet<string> flags, Func<string, string> read, Func<string, string> template)
        {
            if (read(AssemblyPath(answers)) == null)
            {
                changes.Add(new Change { path = AssemblyPath(answers), isNew = true, text = AssemblyDefinition(ProjectAssembly, tests: false),
                                         summary = $"The {ProjectAssembly} assembly the project's abilities are in" });
            }
            if (!HasTestKit) { return; }
            changes.Add(new Change { path = FixturePath(answers, names), isNew = true, text = Render(template("Fixture"), values, flags), summary = $"Its tests, {names.type}Tests, on the ability test kit" });
            if (read(TestsAssemblyPath(answers)) == null)
            {
                changes.Add(new Change { path = TestsAssemblyPath(answers), isNew = true, text = AssemblyDefinition(ProjectTestsAssembly, tests: true),
                                         summary = $"The {ProjectTestsAssembly} assembly their tests are in (PlayMode, on the package's ability test kit)" });
            }
        }

        /// <summary>The assembly definition of a project's abilities (or, with tests, of their PlayMode tests), as text.</summary>
        public static string AssemblyDefinition(string name, bool tests)
        {
            var references = tests
                ? new[] { ProjectAssembly, "MeridianMotion.Characters", "MeridianMotion.Levels", "MeridianMotion.Tests.PlayMode", "MeridianMotion.Tests.Shared", "UnityEngine.TestRunner", "UnityEditor.TestRunner" }
                : new[] { "MeridianMotion.Characters", "MeridianMotion.Levels" };
            var text = new StringBuilder();
            text.Append("{\n");
            text.Append($"    \"name\": \"{name}\",\n");
            text.Append($"    \"rootNamespace\": \"{name}\",\n");
            text.Append("    \"references\": [\n");
            text.Append(string.Join(",\n", references.Select(r => $"        \"{r}\""))).Append("\n    ],\n");
            text.Append("    \"includePlatforms\": [],\n");
            text.Append("    \"excludePlatforms\": [],\n");
            text.Append("    \"allowUnsafeCode\": false,\n");
            text.Append($"    \"overrideReferences\": {(tests ? "true" : "false")},\n");
            text.Append($"    \"precompiledReferences\": [{(tests ? "\n        \"nunit.framework.dll\"\n    " : "")}],\n");
            text.Append($"    \"autoReferenced\": {(tests ? "false" : "true")},\n");
            text.Append($"    \"defineConstraints\": [{(tests ? "\n        \"UNITY_INCLUDE_TESTS\"\n    " : "")}],\n");
            text.Append("    \"versionDefines\": [],\n");
            text.Append("    \"noEngineReferences\": false\n");
            text.Append("}\n");
            return text.ToString();
        }

        /// <summary>
        /// A template with its {{Key}}s filled in, and its blocks kept or left out: the lines between {{#flag}} and
        /// {{/flag}} are kept with the flag, those between {{^flag}} and {{/flag}} without it (each marker on a line of its own).
        /// </summary>
        public static string Render(string template, IReadOnlyDictionary<string, string> values, ICollection<string> flags)
        {
            var output = new StringBuilder();
            var keeping = new Stack<bool>();
            foreach (string line in template.Replace("\r\n", "\n").Split('\n'))
            {
                Match marker = Regex.Match(line, @"^\s*\{\{([#^/])(\w+)\}\}\s*$");
                if (marker.Success)
                {
                    char kind = marker.Groups[1].Value[0];
                    bool set = flags.Contains(marker.Groups[2].Value);
                    if (kind == '/') { keeping.Pop(); }
                    else { keeping.Push((keeping.Count == 0 || keeping.Peek()) && (kind == '#' ? set : !set)); }
                    continue;
                }
                if (keeping.Count > 0 && !keeping.Peek()) { continue; }
                output.Append(Regex.Replace(line, @"\{\{(\w+)\}\}", m => values.TryGetValue(m.Groups[1].Value, out string v) ? v : m.Value)).Append('\n');
            }
            return output.ToString().TrimEnd('\n') + "\n";
        }

        /// <summary>
        /// The flags' text (CharacterState.cs or MovementEvents.cs) with the given flags added at its New Ability line,
        /// each the next unused bit of the package's (below Flags256.PackageBits): its bit number (XBit) and its value (X).
        /// </summary>
        public static string AddEnumMembers(string text, string comment, params string[] members)
        {
            int anchor = text.IndexOf(EnumAnchor, StringComparison.Ordinal);
            int lineStart = text.LastIndexOf('\n', anchor) + 1;
            string indent = text.Substring(lineStart, anchor - lineStart);
            string type = Regex.Match(text, @"public partial struct (\w+)").Groups[1].Value;
            int next = HighestBit(text.Substring(0, lineStart)) + 1;
            var added = new StringBuilder();
            added.Append(indent).Append("// ").Append(comment).Append('\n');
            foreach (string member in members)
            {
                if (next >= Flags256.PackageBits) { throw new InvalidOperationException($"{type} has no unused bits of the package's left."); }
                added.Append(indent).Append("public const int ").Append(member).Append("Bit = ").Append(next++).Append(";\n");
                added.Append(indent).Append("public static readonly ").Append(type).Append(' ').Append(member).Append(" = Bit(").Append(member).Append("Bit);\n");
            }
            return text.Insert(lineStart, added.ToString());
        }

        /// <summary>The highest bit any flag takes (its bit number: public const int XBit = n;), or -1.</summary>
        public static int HighestBit(string flagsText)
        {
            int highest = -1;
            foreach (Match match in Regex.Matches(flagsText, @"public const int \w+Bit = (\d+);"))
            {
                highest = Math.Max(highest, int.Parse(match.Groups[1].Value));
            }
            return highest;
        }

        /// <summary>StandardMovementMode's text with the component in DefaultComponents, just after the given one (or first).</summary>
        public static string AddToDefaultComponents(string text, string type, string after)
        {
            Match method = Regex.Match(text, @"DefaultComponents\(\)\s*\{");
            Match target = after == null
                ? Regex.Match(text.Substring(method.Index), @"^([ \t]*)yield return new \w+\(\);", RegexOptions.Multiline)
                : Regex.Match(text.Substring(method.Index), $@"^([ \t]*)yield return new {Regex.Escape(after)}\(\);[^\n]*\n", RegexOptions.Multiline);
            if (!target.Success) { throw new InvalidOperationException($"{ModePath}: no 'yield return new {after ?? "…"}();' in DefaultComponents."); }
            string line = $"{target.Groups[1].Value}yield return new {type}();\n";
            int at = method.Index + target.Index + (after == null ? 0 : target.Length);
            return text.Insert(at, line);
        }

        /// <summary>MovementCompositionTests' text with the component's type in its copy of the order, just after the given one.</summary>
        public static string AddToCompositionTest(string text, string type, string after)
        {
            int method = text.IndexOf(CompositionTestName, StringComparison.Ordinal);
            if (method < 0) { throw new InvalidOperationException($"{CompositionTestPath}: no {CompositionTestName}."); }
            if (after == null)
            {
                int first = text.IndexOf("typeof(", method, StringComparison.Ordinal);
                return text.Insert(first, $"typeof({type}), ");
            }
            string mark = $"typeof({after})";
            int at = text.IndexOf(mark, method, StringComparison.Ordinal);
            if (at < 0) { throw new InvalidOperationException($"{CompositionTestPath}: no {mark} in {CompositionTestName}."); }
            return text.Insert(at + mark.Length, $", typeof({type})");
        }

        /// <summary>The docs page with a section for the ability added at its end, for its author to write.</summary>
        public static string AddDocSection(string text, Answers answers, Names names)
        {
            string starts = answers.button != StartButton.None && answers.levelElement ? $"A press of {answers.button} at a {names.element}"
                : answers.button != StartButton.None ? $"A press of {answers.button}" : $"Being at a {names.element}";
            return text.TrimEnd('\n') + "\n\n" +
                   $"## {names.display}\n" +
                   $"{starts} starts {names.display}, which lasts its {names.display} Duration (on the StandardMovementMode asset's " +
                   $"{names.display} component; 0 turns it off). Say here what it does, and name each of its settings by its " +
                   "inspector name and the component it's on, never by its value (see [Adding a Movement Ability](adding-an-ability.md)).\n";
        }

        /// <summary>
        /// movement.md's text with the component's inspector name in its list of components ("They're …."), after the given
        /// one's (or first), or null if the list isn't found.
        /// </summary>
        public static string AddToComponentList(string text, string display, string after)
        {
            Match list = Regex.Match(text, @"They're ([^\n]*?)\.(\s|$)");
            if (!list.Success) { return null; }
            string items = list.Groups[1].Value;
            string updated;
            if (after == null) { updated = $"{display}, {items}"; }
            else
            {
                string afterDisplay = MovementComponent.SpacedName(after);
                var named = TypeCache.GetTypesDerivedFrom<MovementComponent>().FirstOrDefault(t => t.Name == after);
                if (named != null) { afterDisplay = MovementComponent.DisplayNameOf(named); }
                Match item = Regex.Match(items, $@"{Regex.Escape(afterDisplay)}(\s*\([^)]*\))?");
                if (!item.Success) { return null; }
                int end = item.Index + item.Length;
                bool last = end == items.Length;
                updated = last
                    ? Regex.Replace(items, $@" and {Regex.Escape(afterDisplay)}$", $", {afterDisplay} and {display}")
                    : items.Insert(end, $", {display}");
            }
            return text.Substring(0, list.Groups[1].Index) + updated + text.Substring(list.Groups[1].Index + items.Length);
        }

        /// <summary>
        /// Writes the changes (new files, and existing ones' new text), leaving out optional ones whose file isn't there.
        /// root is the project's folder. Returns the paths written.
        /// </summary>
        public static List<string> Write(IEnumerable<Change> changes, string root)
        {
            var written = new List<string>();
            foreach (Change change in changes)
            {
                string full = Path.Combine(root, change.path);
                Directory.CreateDirectory(Path.GetDirectoryName(full));
                File.WriteAllText(full, change.text.Replace("\r\n", "\n"));
                written.Add(change.path);
            }
            return written;
        }
    }
}
