using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using MeridianMotion.EditorTools;
using NUnit.Framework;
using UnityEditor.Compilation;
using UnityEngine.TestTools;
using UnityEngine;

namespace MeridianMotion.Tests.EditMode.Movement
{
    /// <summary>
    /// Keeps docs/tutorial-first-ability.md true: New Ability (AbilityScaffold) is run, without writing anything into the
    /// project, with the answers in the page's table; each code block is applied where the marker before it says (a
    /// "tutorial:" comment naming the file and the members it replaces); and the result is compiled in throwaway
    /// assemblies, out of the project (Temp/TutorialCheck). For an ability of the project's own (the tutorial's, Where:
    /// Project): its files as an assembly of their own against the package's built assemblies, and its fixture against
    /// that and the PlayMode tests. For one in the package: the character's code with the ability, its flag and events,
    /// and the PlayMode tests with its fixture. So a change to New Ability's templates, to the code the tutorial uses or to the
    /// page itself that breaks the tutorial fails here. Only compiling is checked, not what the ability does (the
    /// tutorial's ability isn't one of the project's).
    /// </summary>
    [Category(TestArea.Movement)]
    [TestTiming]
    public class TutorialTests
    {
        private const string PagePath = "docs/tutorial-first-ability.md";
        private const string CheckFolder = "Temp/TutorialCheck";
        private const string CharacterAssembly = "MeridianMotion.Characters";
        private const string PlayModeTestsAssembly = "MeridianMotion.Tests.PlayMode";
        private const string LevelsAssembly = "MeridianMotion.Levels";
        // Where the tutorial's project ability is planned (nothing is written there), so the check doesn't depend on the
        // project's own abilities or ledger.
        private const string ProjectFolder = "Assets/ZzTutorialAbilities";

        private static string Root => Path.GetDirectoryName(Application.dataPath);

        // ---- The page ----

        /// <summary>One of the page's code blocks, and where it goes: the members of a file from one to another.</summary>
        public struct Edit
        {
            public string file, from, to, code;
            public override string ToString() => $"{file}: \"{from}\" .. \"{to}\"";
        }

        private static readonly Regex EditPattern = new(
            "<!-- tutorial: (?<file>[^,]+), replace \"(?<from>[^\"]+)\" \\.\\. \"(?<to>[^\"]+)\" -->\\s*```csharp\\n(?<code>.*?)```",
            RegexOptions.Singleline);

        /// <summary>The page's code blocks, each with where its marker says it goes.</summary>
        public static List<Edit> EditsOf(string page) =>
            EditPattern.Matches(page.Replace("\r\n", "\n")).Cast<Match>()
                .Select(m => new Edit { file = m.Groups["file"].Value.Trim(), from = m.Groups["from"].Value, to = m.Groups["to"].Value, code = m.Groups["code"].Value })
                .ToList();

        /// <summary>New Ability's answers, from the page's table (each row: | Question | Answer | Why |).</summary>
        public static AbilityScaffold.Answers AnswersOf(string page)
        {
            var rows = new Dictionary<string, string>();
            foreach (Match row in Regex.Matches(page, @"^\| (?<question>[^|]+?) \| (?<answer>[^|]+?) \|", RegexOptions.Multiline))
            {
                rows[row.Groups["question"].Value] = row.Groups["answer"].Value.Trim('`', ' ');
            }
            string Row(string question)
            {
                Assert.That(rows.ContainsKey(question), Is.True, $"The tutorial's table should answer New Ability's {question}.");
                return rows[question];
            }
            static T Parsed<T>(string answer) where T : struct => (T)Enum.Parse(typeof(T), answer.Replace(" ", ""), ignoreCase: true);
            return new AbilityScaffold.Answers
            {
                target = Parsed<AbilityScaffold.Target>(Row("Where")),
                name = Row("Name"),
                kind = Parsed<AbilityScaffold.Kind>(Row("What It Does")),
                button = Parsed<AbilityScaffold.StartButton>(Row("Started By")),
                levelElement = Row("At A Level Element") == "on",
                area = Parsed<AbilityScaffold.Area>(Row("Kind")),
                after = Row("Place In The Order"),
                needsIK = Row("Needs IK") == "on",
            };
        }

        /// <summary>
        /// What New Ability makes and changes for the page's answers, with the page's code blocks applied to the files it
        /// makes: each a project path and its text. Fails, saying which, if a block's members can't be found.
        /// </summary>
        public static List<AbilityScaffold.Change> TutorialChanges()
        {
            string page = File.ReadAllText(Path.Combine(Root, PagePath));
            AbilityScaffold.Answers answers = AnswersOf(page);
            answers.projectFolder = ProjectFolder;
            string problem = AbilityScaffold.Problem(answers, NewAbilityWindow.ReadProjectFile);
            Assert.That(problem, Is.Null, "New Ability should make the tutorial's ability (is its name taken now?).");
            List<AbilityScaffold.Change> changes = AbilityScaffold.Plan(answers, NewAbilityWindow.ReadProjectFile, NewAbilityWindow.ReadTemplate);
            List<Edit> edits = EditsOf(page);
            Assert.That(edits, Is.Not.Empty, "The tutorial should have code blocks with markers.");
            foreach (Edit edit in edits)
            {
                AbilityScaffold.Change change = changes.FirstOrDefault(c => c.isNew && Path.GetFileName(c.path) == edit.file);
                Assert.That(change, Is.Not.Null, $"New Ability should make {edit.file}, which the tutorial edits.");
                change.text = Replace(change.text, edit);
            }
            return changes;
        }

        /// <summary>
        /// The text with the members from the one declared on the line holding edit.from to the one on the line holding
        /// edit.to replaced by the block (indented as the first was): each member with the comments and attributes above
        /// it, and to its closing brace (or its line's end, for one on one line).
        /// </summary>
        public static string Replace(string text, Edit edit)
        {
            List<string> lines = text.Replace("\r\n", "\n").Split('\n').ToList();
            int from = lines.FindIndex(l => l.Contains(edit.from));
            Assert.That(from, Is.GreaterThanOrEqualTo(0), $"{edit}: nothing in it holds \"{edit.from}\" (New Ability's output changed?).");
            int to = lines.FindIndex(from, l => l.Contains(edit.to));
            Assert.That(to, Is.GreaterThanOrEqualTo(0), $"{edit}: nothing after \"{edit.from}\" holds \"{edit.to}\".");
            int start = from;
            while (start > 0 && IsAbove(lines[start - 1])) { start--; }
            int end = EndOfMember(lines, to);
            Assert.That(end, Is.GreaterThanOrEqualTo(0), $"{edit}: the member holding \"{edit.to}\" doesn't end.");
            string indent = new string(' ', lines[start].Length - lines[start].TrimStart().Length);
            IEnumerable<string> code = edit.code.TrimEnd('\n').Split('\n').Select(l => l.Length == 0 ? l : indent + l);
            lines.RemoveRange(start, end - start + 1);
            lines.InsertRange(start, code);
            return string.Join("\n", lines);
        }

        // A comment or an attribute, which belongs to the member under it.
        private static bool IsAbove(string line)
        {
            string trimmed = line.TrimStart();
            return trimmed.StartsWith("///") || trimmed.StartsWith("//") || (trimmed.StartsWith("[") && !trimmed.Contains(";"));
        }

        // The last line of the member starting at the line: to its braces' close, or the line ending its declaration.
        private static int EndOfMember(List<string> lines, int first)
        {
            int depth = 0;
            bool opened = false;
            for (int i = first; i < lines.Count; i++)
            {
                foreach (char c in lines[i])
                {
                    if (c == '{') { depth++; opened = true; }
                    else if (c == '}') { depth--; }
                }
                if (opened && depth <= 0) { return i; }
                if (!opened && lines[i].TrimEnd().EndsWith(";")) { return i; }
            }
            return -1;
        }

        // ---- The checks ----

        [Test]
        public void TheTutorialsBlocks_ApplyToWhatNewAbilityMakes()
        {
            List<AbilityScaffold.Change> changes = TutorialChanges();
            string ability = changes.First(c => c.isNew && c.path.EndsWith("/GroundPound.cs")).text;
            Assert.That(ability, Does.Contain("groundPoundHangTime"), "Its settings are the tutorial's.");
            Assert.That(ability, Does.Not.Contain("groundPoundDuration"), "Nothing of the settings it replaced is left.");
        }

        [UnityTest]
        public IEnumerator TheTutorialsAbility_Compiles()
        {
            List<AbilityScaffold.Change> changes = TutorialChanges();
            string folder = Path.Combine(Root, CheckFolder);
            if (Directory.Exists(folder)) { Directory.Delete(folder, recursive: true); }
            Directory.CreateDirectory(folder);
            bool inProject = changes.All(c => c.path.StartsWith(ProjectFolder + "/"));
            yield return inProject ? CompileInProject(changes, folder) : CompileInPackage(changes, folder);
        }

        // A project's ability: its files as the Abilities assembly, against the package's as built; then its fixture as
        // Abilities.Tests, against that and the PlayMode tests (the kit).
        private static IEnumerator CompileInProject(List<AbilityScaffold.Change> changes, string folder)
        {
            Assembly character = ProjectAssembly(CharacterAssembly);
            Assembly levels = ProjectAssembly(LevelsAssembly);
            string abilitiesDll = Path.Combine(folder, AbilityScaffold.ProjectAssembly + ".dll");
            string testsFolder = ProjectFolder + "/Tests/";
            List<string> abilitySources = Written(changes.Where(c => c.path.EndsWith(".cs") && !c.path.StartsWith(testsFolder)), folder);
            string[] messages = null;
            yield return Build(character, abilitySources, abilitiesDll, null, new[] { character.outputPath, levels.outputPath }, m => messages = m);
            Assert.That(messages, Is.Empty, "The tutorial's ability should compile:\n" + string.Join("\n", messages ?? new string[0]));

            Assembly tests = ProjectAssembly(PlayModeTestsAssembly);
            List<string> testSources = Written(changes.Where(c => c.path.EndsWith(".cs") && c.path.StartsWith(testsFolder)), folder);
            Assert.That(testSources, Is.Not.Empty, "New Ability should make the tutorial's tests (are the package's PlayMode tests here?).");
            messages = null;
            yield return Build(tests, testSources, Path.Combine(folder, AbilityScaffold.ProjectTestsAssembly + ".dll"), null,
                               new[] { tests.outputPath, abilitiesDll }, m => messages = m);
            Assert.That(messages, Is.Empty, "The tutorial's tests should compile:\n" + string.Join("\n", messages ?? new string[0]));
        }

        // A package's ability: the character's code with New Ability's changes and the ability; then the PlayMode tests
        // with its fixture, against that.
        private static IEnumerator CompileInPackage(List<AbilityScaffold.Change> changes, string folder)
        {
            Assembly character = ProjectAssembly(CharacterAssembly);
            string characterDll = Path.Combine(folder, CharacterAssembly + ".dll");
            List<string> characterSources = SourcesWith(character, changes, folder, "Assets/MeridianMotion/Runtime/");
            string[] messages = null;
            yield return Build(character, characterSources, characterDll, null, null, m => messages = m);
            Assert.That(messages, Is.Empty, "The character's code with the tutorial's ability should compile:\n" + string.Join("\n", messages ?? new string[0]));

            Assembly tests = ProjectAssembly(PlayModeTestsAssembly);
            List<string> testSources = SourcesWith(tests, changes, folder, "Assets/MeridianMotion/Tests/PlayMode/");
            messages = null;
            yield return Build(tests, testSources, Path.Combine(folder, PlayModeTestsAssembly + ".dll"), (character.outputPath, characterDll), null, m => messages = m);
            Assert.That(messages, Is.Empty, "The PlayMode tests with the tutorial's fixture should compile:\n" + string.Join("\n", messages ?? new string[0]));
        }

        // The changes' texts, written under folder: their paths there.
        private static List<string> Written(IEnumerable<AbilityScaffold.Change> changes, string folder)
        {
            var paths = new List<string>();
            foreach (AbilityScaffold.Change change in changes)
            {
                string copy = Path.Combine(folder, "src", change.path);
                Directory.CreateDirectory(Path.GetDirectoryName(copy));
                File.WriteAllText(copy, change.text);
                paths.Add(copy);
            }
            return paths;
        }

        private static Assembly ProjectAssembly(string name)
        {
            Assembly assembly = CompilationPipeline.GetAssemblies(AssembliesType.Editor).FirstOrDefault(a => a.name == name);
            Assert.That(assembly, Is.Not.Null, $"Test setup: the {name} assembly should be there.");
            return assembly;
        }

        /// <summary>
        /// The assembly's source files, those New Ability changes swapped for their changed copies (written under folder),
        /// with the new ones under the prefix added.
        /// </summary>
        private static List<string> SourcesWith(Assembly assembly, List<AbilityScaffold.Change> changes, string folder, string prefix)
        {
            var sources = assembly.sourceFiles.ToList();
            foreach (AbilityScaffold.Change change in changes.Where(c => c.path.EndsWith(".cs")))
            {
                int index = sources.IndexOf(change.path);
                if (index < 0 && !(change.isNew && change.path.StartsWith(prefix))) { continue; }
                string copy = Path.Combine(folder, "src", change.path);
                Directory.CreateDirectory(Path.GetDirectoryName(copy));
                File.WriteAllText(copy, change.text);
                if (index >= 0) { sources[index] = copy; }
                else { sources.Add(copy); }
            }
            return sources;
        }

        /// <summary>
        /// Compiles the sources as the assembly is compiled (its defines, options and references: no other of the project's
        /// own), with one reference swapped (the project's character assembly for the one just built) and others added
        /// (the assemblies a project's ability builds on), then gives its errors. Warnings don't count.
        /// </summary>
        private static IEnumerator Build(Assembly like, List<string> sources, string output, (string from, string to)? swap, string[] extra, Action<string[]> done)
        {
            string[] references = like.allReferences.Select(r => swap.HasValue && SamePath(r, swap.Value.from) ? swap.Value.to : r)
                .Concat(extra ?? Array.Empty<string>()).ToArray();
            var builder = new AssemblyBuilder(output, sources.ToArray())
            {
                flags = AssemblyBuilderFlags.EditorAssembly,
                referencesOptions = ReferencesOptions.UseEngineModules,
                additionalDefines = like.defines,
                compilerOptions = like.compilerOptions,
            };
            // Only its own references: the project's other assemblies (the character's among them) would clash with these.
            builder.excludeReferences = builder.defaultReferences.Where(r => !references.Any(own => SamePath(own, r))).ToArray();
            builder.additionalReferences = references;
            string[] errors = null;
            builder.buildFinished += (_, compiled) =>
                errors = compiled.Where(m => m.type == CompilerMessageType.Error).Select(m => m.message).ToArray();
            Assert.That(builder.Build(), Is.True, "The check's compile should start (is Unity compiling already?).");
            while (errors == null) { yield return null; }
            done(errors);
        }

        private static bool SamePath(string a, string b) => Path.GetFullPath(a) == Path.GetFullPath(b);
    }
}
