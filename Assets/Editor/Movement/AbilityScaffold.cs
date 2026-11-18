using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Character;
using UnityEditor;

namespace CharacterShowcase.EditorTools
{
    /// <summary>
    /// What New Ability (NewAbilityWindow) makes from its answers, worked out as a list of file changes before anything is
    /// written, so the window can show them and a test can check them without touching the project. It makes:
    /// - the ability's component, a small working ability to build on (from NewAbilityTemplates/Component.txt): its
    ///   settings, its hooks, its state and that state's place on the MovementController;
    /// - its CharacterState flag and its two MovementEvents, each the next unused bit (at the enums' New Ability lines);
    /// - its place in BasicMovementMode.DefaultComponents, and in MovementCompositionTests' copy of that order;
    /// - its PlayMode test fixture, on the ability test kit (AbilityFixture);
    /// - optionally, a level element it starts at (Assets/Scripts/Level);
    /// - a section for it in docs/abilities.md or docs/traversal.md, and its name in docs/movement.md's component list.
    /// The movement mode assets are given it once it's compiled (MovementModeSync.AddToAllModes: turned on for the
    /// default character's mode, off for the others). See docs/adding-an-ability.md.
    /// </summary>
    public static class AbilityScaffold
    {
        /// <summary>Where it acts: taking the character over, or on top of walking.</summary>
        public enum Kind { TakesOver, OnTopOfWalking }

        /// <summary>The button a press of which starts it (one Inputs has: a new button is a step of its own, see AGENTS.md).</summary>
        public enum StartButton { None, Jump, Dash, Crouch, Roll, Grab }

        /// <summary>Which docs page and test folder it belongs to.</summary>
        public enum Area { Abilities, Traversal }

        /// <summary>The window's answers.</summary>
        public sealed class Answers
        {
            /// <summary>Its class name (PascalCase, a gerund as the others are: Gliding).</summary>
            public string name = "";
            public Kind kind = Kind.TakesOver;
            public StartButton button = StartButton.Grab;
            /// <summary>Whether it starts at a level element of its own (made with it).</summary>
            public bool levelElement;
            /// <summary>The default component it goes just after in the order (its class name), or null for first.</summary>
            public string after = nameof(Rolling);
            public Area area = Area.Abilities;
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

        public const string ComponentsFolder = "Assets/Scripts/MovementModes/Components";
        public const string StatePath = "Assets/Scripts/Character/CharacterState.cs";
        public const string EventsPath = "Assets/Scripts/Character/MovementEvents.cs";
        public const string ModePath = "Assets/Scripts/MovementModes/BasicMovementMode.cs";
        public const string CompositionTestPath = "Assets/Tests/EditMode/Movement/MovementCompositionTests.cs";
        public const string FixturesFolder = "Assets/Tests/PlayMode";
        public const string LevelFolder = "Assets/Scripts/Level";
        public const string MovementDocPath = "docs/movement.md";
        /// <summary>The line in CharacterState.cs and MovementEvents.cs that new members go above.</summary>
        public const string EnumAnchor = "// New Ability (NewAbilityWindow) adds";
        private const string CompositionTestName = "ANewBasicMovementMode_StartsWithItsComponents_InOrder";

        /// <summary>The names derived from the answers: the class's, its state's, its events', its settings' prefix.</summary>
        public readonly struct Names
        {
            public readonly string type, field, display, state, started, ended, element;

            public Names(string name)
            {
                type = name;
                field = name.Length > 0 ? char.ToLowerInvariant(name[0]) + name.Substring(1) : name;
                display = MovementComponent.SpacedName(name);
                state = name;
                started = name + "Started";
                ended = name + "Ended";
                element = name + "Point";
            }
        }

        /// <summary>
        /// Why the answers can't be made, or null if they can: a name that isn't a class name or is taken (as a type, a
        /// state, an event, a member of the MovementController or a setting), no way of starting it, a place in the
        /// order that isn't one, or a file it must change missing its New Ability line.
        /// </summary>
        public static string Problem(Answers answers, Func<string, string> read)
        {
            string name = answers.name?.Trim() ?? "";
            if (!Regex.IsMatch(name, "^[A-Z][A-Za-z0-9]*$")) { return "Give it a name: a class name, starting with a capital letter (Gliding, say)."; }
            var names = new Names(name);
            if (answers.button == StartButton.None && !answers.levelElement) { return "Choose what starts it: a button, a level element, or both."; }
            if (TypeNames.Value.Contains(name) || (answers.levelElement && TypeNames.Value.Contains(names.element)))
            {
                return $"There's already a type called {name}{(answers.levelElement ? $" or {names.element}" : "")}: choose another name.";
            }
            if (Enum.GetNames(typeof(CharacterState)).Contains(names.state)) { return $"There's already a state called {names.state}: choose another name."; }
            if (Enum.GetNames(typeof(MovementEvents)).Any(e => e == names.started || e == names.ended)) { return $"There's already a movement event called {names.started} or {names.ended}: choose another name."; }
            if (typeof(MovementController).GetMember(names.field).Length > 0) { return $"The MovementController already has a member called {names.field}: choose another name."; }
            HashSet<string> settings = ExistingSettingNames();
            foreach (string setting in SettingNames(answers, names))
            {
                if (settings.Contains(setting)) { return $"There's already a movement setting called {setting} (setting names are unique): choose another name."; }
            }
            if (answers.after != null && !DefaultComponentNames().Contains(answers.after)) { return $"{answers.after} isn't one of the default components."; }
            if (read(ComponentPath(names)) != null) { return $"{ComponentPath(names)} is already there."; }
            foreach (string path in new[] { StatePath, EventsPath })
            {
                string text = read(path);
                if (text == null || !text.Contains(EnumAnchor)) { return $"{path} has no New Ability line ('{EnumAnchor} …') to add to: put it back above the enum's closing brace."; }
            }
            string mode = read(ModePath);
            if (mode == null || !mode.Contains("DefaultComponents()")) { return $"{ModePath} has no DefaultComponents to add it to."; }
            return null;
        }

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

        /// <summary>The default components' class names, in order (BasicMovementMode.DefaultComponents).</summary>
        public static List<string> DefaultComponentNames()
        {
            var mode = UnityEngine.ScriptableObject.CreateInstance<BasicMovementMode>();
            try { return mode.DefaultComponentTypes().Select(t => t.Name).ToList(); }
            finally { UnityEngine.Object.DestroyImmediate(mode); }
        }

        private static HashSet<string> ExistingSettingNames()
        {
            const System.Reflection.BindingFlags Fields = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public |
                                                          System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.DeclaredOnly;
            var names = new HashSet<string>();
            foreach (Type type in TypeCache.GetTypesDerivedFrom<MovementComponent>().Append(typeof(BasicMovementMode)).Append(typeof(ComposedMovementMode)))
            {
                foreach (var field in type.GetFields(Fields)) { names.Add(field.Name); }
            }
            return names;
        }

        public static string ComponentPath(Names names) => $"{ComponentsFolder}/{names.type}.cs";
        public static string FixturePath(Answers answers, Names names) => $"{FixturesFolder}/{answers.area}/{names.type}Tests.cs";
        public static string ElementPath(Names names) => $"{LevelFolder}/{names.element}.cs";
        public static string AreaDocPath(Answers answers) => answers.area == Area.Abilities ? "docs/abilities.md" : "docs/traversal.md";

        /// <summary>
        /// Every file change the answers make (call Problem first). read gives a file's text by its project-relative
        /// path (null if it isn't there), template a template's text by its name (Component, Fixture, LevelElement).
        /// </summary>
        public static List<Change> Plan(Answers answers, Func<string, string> read, Func<string, string> template)
        {
            var names = new Names(answers.name.Trim());
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
            };
            var flags = new HashSet<string>();
            flags.Add(answers.kind == Kind.TakesOver ? "takesOver" : "onTopOfWalking");
            if (answers.button != StartButton.None) { flags.Add("button"); }
            if (answers.levelElement) { flags.Add("levelElement"); }

            var changes = new List<Change>
            {
                new() { path = ComponentPath(names), isNew = true, text = Render(template("Component"), values, flags), summary = $"The {names.display} component: a small working ability to build on" },
            };
            if (answers.levelElement)
            {
                changes.Add(new Change { path = ElementPath(names), isNew = true, text = Render(template("LevelElement"), values, flags), summary = $"The level element it starts at ({names.element})" });
            }
            changes.Add(new Change { path = StatePath, text = AddEnumMembers(read(StatePath), $"{names.display} (see {names.type})", names.state), summary = $"Its state, CharacterState.{names.state}" });
            changes.Add(new Change { path = EventsPath, text = AddEnumMembers(read(EventsPath), $"{names.display} starting and ending (see {names.type})", names.started, names.ended), summary = $"Its events, {names.started} and {names.ended}" });
            changes.Add(new Change { path = ModePath, text = AddToDefaultComponents(read(ModePath), names.type, answers.after), summary = answers.after == null ? "First in BasicMovementMode's default components" : $"After {answers.after} in BasicMovementMode's default components" });

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

        /// <summary>The enum's text with the given members added at its New Ability line, each the next unused bit.</summary>
        public static string AddEnumMembers(string text, string comment, params string[] members)
        {
            int anchor = text.IndexOf(EnumAnchor, StringComparison.Ordinal);
            int lineStart = text.LastIndexOf('\n', anchor) + 1;
            string indent = text.Substring(lineStart, anchor - lineStart);
            int next = HighestBit(text.Substring(0, lineStart)) + 1;
            var added = new StringBuilder();
            added.Append(indent).Append("// ").Append(comment).Append('\n');
            foreach (string member in members)
            {
                if (next >= 64) { throw new InvalidOperationException("The enum has no unused bits left."); }
                added.Append(indent).Append(member).Append(" = 1L << ").Append(next++).Append(",\n");
            }
            return text.Insert(lineStart, added.ToString());
        }

        /// <summary>The highest bit any member's value takes (written as a number or 1L &lt;&lt; n), or -1.</summary>
        public static int HighestBit(string enumText)
        {
            int highest = -1;
            foreach (Match match in Regex.Matches(enumText, @"=\s*(?:1[uUlL]*\s*<<\s*(\d+)|(\d+)[uUlL]*)\s*,"))
            {
                int bit = match.Groups[1].Success ? int.Parse(match.Groups[1].Value)
                    : (int)Math.Floor(Math.Log(Math.Max(1d, double.Parse(match.Groups[2].Value)), 2));
                highest = Math.Max(highest, bit);
            }
            return highest;
        }

        /// <summary>BasicMovementMode's text with the component in DefaultComponents, just after the given one (or first).</summary>
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
                   $"{starts} starts {names.display}, which lasts its {names.display} Duration (on the BasicMovementMode asset's " +
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
