#!/usr/bin/env python3
"""
Runs the project's Unity tests from the command line, without knowing Unity's command line, and prints a plain summary.

Run from anywhere (it finds the project from where it lives):
  python3 Tools/run_tests.py --changed                      what your change can affect (the git diff, by area)
  python3 Tools/run_tests.py --changed --base main          ... and everything your branch changed since main
  python3 Tools/run_tests.py --area Abilities Traversal     one or more areas (TestArea's categories)
  python3 Tools/run_tests.py --filter "LedgeTests|VaultTests"   tests whose names match a regex
  python3 Tools/run_tests.py --all                          both suites in full (before committing)
  add --platform EditMode|PlayMode|both (both by default), --exclude Performance (leave a category out), --strict
  (an inconclusive test fails the run), --unity <path> (which Unity to use), --dry-run (print the Unity commands and
  start nothing).

What it does:
  1. Finds the Unity the project is made with (ProjectSettings/ProjectVersion.txt's m_EditorVersion): --unity, else the
     UNITY_PATH environment variable, else the Unity Hub's install folders (its default ones, and the one set in the
     Hub's preferences). With none, it says what to install or set.
  2. Refuses if the Unity editor has the project open (it holds Temp/UnityLockfile while it does), since a second Unity
     can't open a project one already has.
  3. Runs each platform in batch mode with the chosen categories or filter, leaving the results and logs in
     Logs/TestRuns/<timestamp>/ (Logs/ is git-ignored). A platform with no fixture in the chosen areas is skipped: the
     areas each platform has are read from its fixtures' [Category(TestArea.…)].
  4. Reads the NUnit results and prints, per platform, how many passed, failed, were inconclusive and were skipped,
     then each test that didn't pass, with its message and the first line of its stack trace. If Unity stopped on an
     error of its own (a compile error, say), it prints the errors from the log instead.

Exit codes: 0 when nothing failed; 1 when a test failed (or, with --strict, was inconclusive), or Unity couldn't run
them (a compile error); 2 when it couldn't start (no Unity, the editor has the project open, a bad argument).

The areas are TestArea's constants (Assets/MeridianMotion/Tests/Shared/TestArea.cs), read from that file, so a new area needs no
change here unless --changed should map files to it (CHANGE_RULES). Standard library only, Python 3.9 or later.
"""
import argparse
import datetime
import json
import os
import re
import shutil
import subprocess
import sys
import xml.etree.ElementTree as ElementTree
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
PLATFORMS = ("EditMode", "PlayMode")
TEST_AREA_FILE = Path("Assets/MeridianMotion/Tests/Shared/TestArea.cs")
# Where the tests are: the package's, and the showcase's (the level builders' and checks').
PACKAGE_TESTS = "Assets/MeridianMotion/Tests/"
SHOWCASE_TESTS = "Assets/Showcase/Tests/"
VERSION_FILE = Path("ProjectSettings/ProjectVersion.txt")
LOCK_FILE = Path("Temp/UnityLockfile")
RUNS_FOLDER = Path("Logs/TestRuns")
EVERYTHING = "everything"

# Exit codes (see the module's docstring).
EXIT_PASSED, EXIT_FAILED, EXIT_COULD_NOT_START = 0, 1, 2

# How a changed file maps to the areas it can affect, AGENTS.md's "How to verify your work" (rule 5): the first rule
# whose path (a folder, ending in "/", or a file) the changed file is in wins, so the more specific come first. Each says
# why, for the run's report. The test files themselves are mapped by their own categories (test_file_areas), not here.
# A file under Assets/ that no rule names can affect anything, so it runs everything; so do Packages/ and
# ProjectSettings/. Keep this in step with rule 5: the test test_every_rule_names_a_real_area checks the areas exist.
MOVEMENT = ("Movement", "Abilities", "Traversal")  # the movement reaches its own, the abilities' and the scale fixtures
ANIMATION = ("Animation", "Models")
LEVELS = ("Levels", "Traversal")
CHANGE_RULES = (
    ("Assets/MeridianMotion/Runtime/Cameras/", ("Camera",), "the camera"),
    ("Assets/MeridianMotion/Runtime/UI/", ("UI",), "the UI"),
    ("Assets/MeridianMotion/Editor/UI/", ("UI",), "the UI's editor tools (making its prefabs)"),
    ("Assets/MeridianMotion/Content/UI/", ("UI",), "the UI's prefabs"),
    ("Assets/Showcase/HUD/", ("UI",), "the showcase's HUD"),
    ("Assets/Showcase/Editor/HUD/", ("UI", "Spawning"), "the showcase HUD's Set Up Scene step"),
    ("Assets/MeridianMotion/Runtime/PhysicsQueries/", MOVEMENT + ("Camera",), "the query buffers the movement and the camera share"),
    ("Assets/MeridianMotion/Runtime/Characters/Character/CharacterSpawner.cs", ("Spawning",), "spawning"),
    ("Assets/MeridianMotion/Runtime/Characters/Character/", MOVEMENT, "the movement"),
    ("Assets/MeridianMotion/Runtime/Characters/Motor/", MOVEMENT, "the movement"),
    ("Assets/MeridianMotion/Runtime/Characters/Body/", MOVEMENT, "the character's body"),
    ("Assets/MeridianMotion/Runtime/Characters/Input/", MOVEMENT, "the input"),
    ("Assets/MeridianMotion/Runtime/Characters/Determinism/", MOVEMENT, "saving and restoring the movement"),
    ("Assets/MeridianMotion/Runtime/Characters/MovementModes/", MOVEMENT, "the movement"),
    ("Assets/MeridianMotion/Runtime/Characters/Animation/", ANIMATION, "animation"),
    ("Assets/MeridianMotion/Runtime/Characters/IK/", ANIMATION, "IK"),
    ("Assets/MeridianMotion/Runtime/Characters/Models/", ANIMATION, "models"),
    ("Assets/MeridianMotion/Runtime/Characters/Effects/", ("Traversal",), "the water effects, played from the movement's events"),
    ("Assets/MeridianMotion/Runtime/LevelElements/", LEVELS, "the level elements"),
    ("Assets/Showcase/Editor/Levels/", LEVELS, "the level builders"),
    ("Assets/Showcase/Grass/", ("Levels",), "the levels' grass"),
    ("Assets/Showcase/Flora/", ("Levels",), "the flora's shaders"),
    ("Assets/Showcase/Editor/Grass/", ("Levels",), "the grass's editor tools (the benchmark)"),
    ("Assets/Showcase/Editor/LevelChecks/", ("Levels", "Movement"), "the level checks (the movement envelope)"),
    ("Assets/MeridianMotion/Editor/Scene/", ("Spawning",), "Set Up Scene"),
    ("Assets/MeridianMotion/Editor/Movement/", ("Movement",), "the movement's editor tools (tuning presets)"),
    ("Assets/MeridianMotion/Editor/Models/", ANIMATION, "the model tools"),
    ("Assets/MeridianMotion/Editor/Animation/", ANIMATION, "the animation editor tools (clip importers, the translator's drawers)"),
    ("Assets/Showcase/Editor/Pipeline/", ANIMATION, "the model pipeline"),
    ("Assets/MeridianMotion/Editor/Effects/", ("Traversal",), "the water effects"),
    ("Assets/MeridianMotion/Content/Movement/", MOVEMENT, "the movement's assets"),
    ("Assets/MeridianMotion/Content/Input/InputSystem_Actions.inputactions", ("Movement",), "the input actions"),
    ("Assets/MeridianMotion/Content/Animation/", ANIMATION, "animation assets"),
    ("Assets/MeridianMotion/Content/Models/", ANIMATION, "models"),
    ("Assets/Showcase/Models/", ANIMATION, "models"),
    ("Assets/Showcase/Movement/", MOVEMENT, "the fox's movement"),
    ("Assets/Showcase/Animation/", ANIMATION, "animation assets"),
    ("Assets/MeridianMotion/Content/Prefabs/Effects/", ("Traversal",), "the water effects"),
    ("Assets/Showcase/Levels/", LEVELS, "the level specs"),
    ("Assets/Showcase/Scenes/", LEVELS, "the level scenes"),
    ("Assets/ClimbablePaint/", LEVELS, "climbable paint"),
    ("Assets/Showcase/Materials/Level/", ("Levels",), "the levels' materials"),
    ("Tools/LevelSpec/envelope.json", ("Levels", "Movement"), "the movement envelope"),
    ("Tools/LevelSpec/level-spec.schema.json", ("Levels",), "the level spec schema"),
)
# Folders whose files can affect every area: the shared test code, the packages and the project's settings.
EVERYTHING_RULES = (
    ("Assets/MeridianMotion/Tests/Shared/", "the tests' shared code"),
    ("Packages/", "the packages"),
    ("ProjectSettings/", "the project's settings"),
)
# A test file's area, from its folder when it can't be read (deleted): the folders are named for the docs' sections, not
# all of them for their category (AGENTS.md's folder table).
TEST_FOLDER_AREAS = {
    "Movement": "Movement", "InputAndDeterminism": "Movement", "GoldenPaths": "Movement", "Abilities": "Abilities",
    "Traversal": "Traversal", "Animations": "Animation", "Models": "Models", "Levels": "Levels",
    "LevelElements": "Levels", "Cameras": "Camera", "Spawning": "Spawning", "UI": "UI", "HUD": "UI",
}


class Problem(Exception):
    """Something that stops the run before Unity starts: its message is for the person running the script."""


# --- The project ------------------------------------------------------------------------------------------------------

def read_editor_version(root):
    """The Unity version the project is made with (m_EditorVersion), which decides which installed Unity to use."""
    path = root / VERSION_FILE
    try:
        text = path.read_text(encoding="utf-8", errors="replace")
    except OSError:
        raise Problem(f"Couldn't read {VERSION_FILE} in {root}: is that the project?")
    match = re.search(r"^m_EditorVersion:\s*(\S+)", text, re.M)
    if not match:
        raise Problem(f"{VERSION_FILE} doesn't say which Unity the project uses (no m_EditorVersion).")
    return match.group(1)


def read_test_areas(root):
    """TestArea's categories, in the file's order, as {constant name: category}: the one list of areas."""
    path = root / TEST_AREA_FILE
    try:
        text = path.read_text(encoding="utf-8", errors="replace")
    except OSError:
        raise Problem(f"Couldn't read the test areas from {TEST_AREA_FILE}.")
    areas = dict(re.findall(r'public\s+const\s+string\s+(\w+)\s*=\s*"([^"]+)"', text))
    if not areas:
        raise Problem(f"{TEST_AREA_FILE} has no areas (public const string X = \"X\";).")
    return areas


def declared_areas(text, areas):
    """The areas a test file puts its fixtures in ([Category(TestArea.X)]), as category names."""
    return {areas[name] for name in re.findall(r"\bTestArea\.(\w+)", text) if name in areas}


def platform_areas(root, areas):
    """The areas each platform has fixtures in, read from the fixtures: EditMode has only some of them."""
    found = {}
    for platform in PLATFORMS:
        found[platform] = set()
        # The showcase's tests (the level builders' and checks') are EditMode tests.
        folders = [root / PACKAGE_TESTS / platform] + ([root / SHOWCASE_TESTS] if platform == "EditMode" else [])
        for path in (p for folder in folders for p in folder.rglob("*.cs")):
            found[platform] |= declared_areas(path.read_text(encoding="utf-8", errors="replace"), areas)
    return found


# --- Finding Unity ----------------------------------------------------------------------------------------------------

def hub_install_folders(home, platform, environ):
    """
    Where the Unity Hub installs editors on this system: its default folder, and the one set in its preferences
    (Installs > Locations), which it keeps in secondaryInstallPath.json, holding the folder as a JSON string.
    """
    if platform == "darwin":
        folders = [Path("/Applications/Unity/Hub/Editor")]
        settings = home / "Library" / "Application Support" / "UnityHub"
    elif platform == "win32":
        program_files = environ.get("ProgramFiles", r"C:\Program Files")
        folders = [Path(program_files) / "Unity" / "Hub" / "Editor"]
        settings = Path(environ.get("APPDATA", str(home / "AppData" / "Roaming"))) / "UnityHub"
    else:
        folders = [home / "Unity" / "Hub" / "Editor"]
        settings = home / ".config" / "UnityHub"
    try:
        chosen = json.loads((settings / "secondaryInstallPath.json").read_text(encoding="utf-8"))
        if isinstance(chosen, str) and chosen.strip():
            folders.append(Path(chosen))
    except (OSError, ValueError):
        pass  # The Hub's never been told another folder, or isn't installed: its default is all there is.
    return folders


def editor_executable(folder, platform):
    """The Unity executable inside one version's install folder, laid out as the Hub installs it on this system."""
    if platform == "darwin":
        return folder / "Unity.app" / "Contents" / "MacOS" / "Unity"
    if platform == "win32":
        return folder / "Editor" / "Unity.exe"
    return folder / "Editor" / "Unity"


def as_executable(path):
    """A path given for Unity, made the executable: on macOS, people point at Unity.app (as BlenderRunner allows)."""
    path = Path(path).expanduser()
    if path.suffix == ".app":
        return path / "Contents" / "MacOS" / "Unity"
    return path


def find_unity(version, explicit=None, environ=None, home=None, platform=None):
    """
    The Unity to run: explicit (--unity), else UNITY_PATH, else the version installed by the Hub. A path given that
    isn't there is a mistake to report, not a reason to look elsewhere (the person meant that one).
    """
    environ = os.environ if environ is None else environ
    home = Path.home() if home is None else home
    platform = sys.platform if platform is None else platform
    for given, source in ((explicit, "--unity"), (environ.get("UNITY_PATH"), "UNITY_PATH")):
        if given:
            path = as_executable(given)
            if path.is_file():
                return path
            raise Problem(f"Unity wasn't found at {path} (from {source}). Point {source} at the Unity executable"
                          f"{' or Unity.app' if platform == 'darwin' else ''}.")
    looked = [editor_executable(folder / version, platform) for folder in hub_install_folders(home, platform, environ)]
    for path in looked:
        if path.is_file():
            return path
    raise Problem(
        f"Unity {version} (the version in {VERSION_FILE}) wasn't found. Looked in:\n"
        + "".join(f"  {path}\n" for path in looked)
        + f"Install Unity {version} with the Unity Hub (Installs > Install Editor), or, if it's installed somewhere else,"
          f" pass --unity <path to Unity> or set the UNITY_PATH environment variable to it.")


# --- Whether the editor has the project open --------------------------------------------------------------------------

def lock_holder(root):
    """
    Why the Unity editor seems to have the project open, or None if it doesn't. Unity keeps Temp/UnityLockfile open,
    locked, while it has a project open, and leaves it behind if it crashes, so the file being there isn't enough: the
    test is whether something holds it. On Windows Unity opens it unshared, so opening it fails. Elsewhere, any Unity
    process with it open counts (lsof, if there is one), and so does a lock on it of either kind (flock, or fcntl's),
    tried without waiting and let go at once.
    """
    path = root / LOCK_FILE
    if not path.exists():
        return None
    if sys.platform == "win32":
        try:
            with open(path, "a"):
                return None
        except PermissionError:
            return f"{LOCK_FILE} is held by another program"
        except OSError:
            return None
    holder = unity_process_with_file_open(path)
    if holder:
        return holder
    return posix_lock_conflict(path)


def unity_process_with_file_open(path):
    """A Unity process that has path open, by lsof's list (only Unity's: this script, say, may have it open itself)."""
    lsof = shutil.which("lsof")
    if not lsof:
        return None
    try:
        listing = subprocess.run([lsof, "-F", "pc", "--", str(path)], capture_output=True, text=True, timeout=20)
    except (OSError, subprocess.SubprocessError):
        return None
    pid = None
    for line in listing.stdout.splitlines():
        if line.startswith("p"):
            pid = line[1:]
        elif line.startswith("c") and "unity" in line[1:].lower() and pid != str(os.getpid()):
            return f"Unity (process {pid}, {line[1:]}) has {LOCK_FILE} open"
    return None


def posix_lock_conflict(path):
    """Whether something holds a lock on path, of either kind: trying both, as nothing says which one Unity takes."""
    import fcntl  # POSIX only: imported here so the script loads on Windows.
    try:
        fd = os.open(str(path), os.O_RDWR)
    except OSError:
        return None  # Not ours to open (another user's?): nothing to test it with, so it doesn't stop the run.
    try:
        for name, lock, unlock in (("flock", lambda: fcntl.flock(fd, fcntl.LOCK_EX | fcntl.LOCK_NB),
                                    lambda: fcntl.flock(fd, fcntl.LOCK_UN)),
                                   ("fcntl", lambda: fcntl.lockf(fd, fcntl.LOCK_EX | fcntl.LOCK_NB),
                                    lambda: fcntl.lockf(fd, fcntl.LOCK_UN))):
            try:
                lock()
            except OSError:
                return f"something holds a lock ({name}) on {LOCK_FILE}"
            unlock()
        return None
    finally:
        os.close(fd)


# --- Choosing the tests from a change ---------------------------------------------------------------------------------

def changed_files(root, base=None):
    """
    The files changed: in the working tree and staged, against HEAD, and new ones not yet added; with base, also what
    the branch committed since it left base (from their merge base). Renames count as both paths (--no-renames).
    """
    def git(*args):
        try:
            result = subprocess.run(["git", "-C", str(root)] + list(args), capture_output=True, text=True)
        except OSError:
            raise Problem("git wasn't found: --changed needs git to list your changes.")
        if result.returncode != 0:
            raise Problem(f"git {' '.join(args)} failed: {result.stderr.strip()}")
        return result.stdout

    against = "HEAD"
    if base:
        against = git("merge-base", base, "HEAD").strip()
    names = git("diff", "--name-only", "--no-renames", against).splitlines()
    names += git("ls-files", "--others", "--exclude-standard").splitlines()
    return sorted(set(name.strip() for name in names if name.strip()))


def in_path(path, rule):
    """Whether path is the rule's file, or in its folder (a folder's own .meta, stripped, is the folder's path)."""
    return path == rule.rstrip("/") or (rule.endswith("/") and path.startswith(rule))


def test_file_areas(root, path, areas):
    """A test file's areas: those its fixtures declare, or, when it can't be read (deleted) or declares none, its folder's."""
    parts = path.split("/")
    try:
        found = declared_areas((root / path).read_text(encoding="utf-8", errors="replace"), areas)
        if found:
            return found
    except OSError:
        pass
    # Assets/MeridianMotion/Tests/<Platform>/<Folder>/...: a file at a platform's root (the rig, the fixtures' bases) reaches
    # them all. The showcase's tests (Assets/Showcase/Tests/<Folder>/...) are the level builders' and checks'.
    if path.startswith(PACKAGE_TESTS) and len(parts) > 5 and parts[4] in TEST_FOLDER_AREAS:
        return {TEST_FOLDER_AREAS[parts[4]]}
    if path.startswith(SHOWCASE_TESTS) and len(parts) > 4 and parts[3] in TEST_FOLDER_AREAS:
        return {TEST_FOLDER_AREAS[parts[3]]}
    return None


def areas_for_changes(root, paths, areas):
    """
    What each changed file can affect, AGENTS.md's rule 5: returns (the areas chosen, the reasons), each reason a line
    saying which file led to which areas. The areas are EVERYTHING when a file can affect anything. None of the files
    reaching the Unity tests, it's an empty set, and the reasons say what they were (docs, the Python tools).
    """
    chosen, reasons = set(), []
    for original in paths:
        path = original.replace("\\", "/")
        if path.endswith(".meta"):
            path = path[: -len(".meta")]
        found, why = classify(root, path, areas)
        if found == EVERYTHING:
            chosen = EVERYTHING
        elif found and chosen != EVERYTHING:
            chosen |= set(found)
        reasons.append((original, found, why))
    return chosen, reasons


def classify(root, path, areas):
    """One file's areas (a set, EVERYTHING, or None for none) and why."""
    for rule, why in EVERYTHING_RULES:
        if in_path(path, rule):
            return EVERYTHING, why
    if path.startswith((PACKAGE_TESTS, SHOWCASE_TESTS)):
        found = test_file_areas(root, path, areas)
        if found:
            return found, "its own tests"
        return EVERYTHING, "the tests' shared code (the rig, the fixtures' bases)"
    for rule, found, why in CHANGE_RULES:
        if in_path(path, rule):
            return set(found), why
    if path.startswith("Assets/"):
        return EVERYTHING, "not mapped to an area, so it may affect any"
    if path.endswith(".md") or path.startswith("docs/"):
        return None, "documentation: no tests"
    if path.startswith("Tools/") and (path.endswith(".py") or "/tests/" in path or path.startswith("Tools/CI/")):
        return None, "a Python tool: the hygiene check covers it (python3 Tools/CI/check_project_hygiene.py)"
    return None, "not read by the Unity tests"


def nothing_to_run(reasons):
    """What to say when no change reaches the Unity tests: only docs, only the Python tools, or neither."""
    whys = {why for _, _, why in reasons}
    if not whys:
        return "Nothing to run: there are no changes."
    if all(why.startswith("documentation") for why in whys):
        return "Nothing to run: only documentation changed."
    if all(why.startswith(("documentation", "a Python tool")) for why in whys):
        return "Nothing to run in Unity: the Python tools' changes are covered by the hygiene check" \
               " (python3 Tools/CI/check_project_hygiene.py), which also runs their tests in CI."
    return "Nothing to run: no change reaches the Unity tests."


# --- Running Unity ----------------------------------------------------------------------------------------------------

def category_argument(chosen, excluded):
    """-testCategory's value: the areas (any of them), less the excluded ("!X"); None for no category filter."""
    parts = sorted(chosen) + [f"!{name}" for name in excluded]
    return ";".join(parts) if parts else None


def unity_command(unity, root, platform, categories, test_filter, results, log):
    """The batch-mode command for one platform. -runTests quits by itself when done (-quit would stop it early)."""
    command = [str(unity), "-batchmode", "-projectPath", str(root), "-runTests", "-testPlatform", platform,
               "-testResults", str(results), "-logFile", str(log)]
    if categories:
        command += ["-testCategory", categories]
    if test_filter:
        command += ["-testFilter", test_filter]
    return command


class PlatformResult:
    """One platform's run: its counts, the tests that didn't pass, and, if Unity couldn't run them, why."""

    def __init__(self, platform, results, log):
        self.platform, self.results, self.log = platform, results, log
        self.counts = {"passed": 0, "failed": 0, "inconclusive": 0, "skipped": 0}
        self.problems = []  # (outcome, test name, message, first stack line)
        self.errors = []  # Unity's own errors, when it ran no tests (compile errors, say).
        self.exit_code = None

    def failed(self, strict):
        return bool(self.errors) or self.counts["failed"] > 0 or (strict and self.counts["inconclusive"] > 0)


def text_of(element, path):
    """The text at path under element (a CDATA message, say), stripped, or empty."""
    found = element.find(path)
    return (found.text or "").strip() if found is not None else ""


def parse_results(path, outcome):
    """
    Reads an NUnit 3 results file into outcome (a PlatformResult): the test-run's totals, and each test-case that didn't
    pass. A fixture whose own set-up or tear-down failed (a test-suite failing at a site other than its children) is
    reported too, as a failure, since its tests may not say why.
    """
    run = ElementTree.parse(str(path)).getroot()
    cases = list(run.iter("test-case"))
    for key in outcome.counts:
        value = run.get(key)
        outcome.counts[key] = int(value) if value is not None and value.isdigit() else \
            sum(1 for case in cases if case.get("result", "").lower() == key)
    for case in cases:
        result = case.get("result", "")
        if result == "Passed":
            continue
        message = text_of(case, "failure/message") or text_of(case, "reason/message")
        stack = text_of(case, "failure/stack-trace")
        outcome.problems.append((result_name(case), case.get("fullname") or case.get("name", "?"), message,
                                 stack.splitlines()[0].strip() if stack else ""))
    for suite in run.iter("test-suite"):
        site = suite.get("site")
        if suite.get("result") == "Failed" and site and site != "Child" and suite.find("failure") is not None:
            stack = text_of(suite, "failure/stack-trace")
            outcome.problems.append((f"Failed ({site})", suite.get("fullname") or suite.get("name", "?"),
                                     text_of(suite, "failure/message"), stack.splitlines()[0].strip() if stack else ""))
            outcome.counts["failed"] += 1


def result_name(case):
    """A test-case's outcome as the summary says it: Failed, Inconclusive, Skipped, with NUnit's label if it has one."""
    label = case.get("label")
    return f"{case.get('result', '?')} ({label})" if label else case.get("result", "?")


def unity_errors(log):
    """
    Why Unity ran no tests, from its log: the compile errors (lines with "error CS", each once), else the other errors
    it printed, else the end of the log.
    """
    try:
        lines = Path(log).read_text(encoding="utf-8", errors="replace").splitlines()
    except OSError:
        return [f"Unity left no log at {log}."]
    errors = list(dict.fromkeys(line.strip() for line in lines if "error CS" in line))
    if not errors:
        errors = list(dict.fromkeys(line.strip() for line in lines
                                    if re.search(r"\b(error|exception|aborting)\b", line, re.I)))[:20]
    return errors or [line for line in lines[-20:] if line.strip()]


def run_platform(unity, root, platform, categories, test_filter, folder):
    """Runs one platform's tests in Unity, then reads what it left: its results, or its log's errors."""
    outcome = PlatformResult(platform, folder / f"{platform}Results.xml", folder / f"{platform}.log")
    command = unity_command(unity, root, platform, categories, test_filter, outcome.results, outcome.log)
    print(f"\nRunning the {platform} tests (Unity's log: {outcome.log})...", flush=True)
    try:
        outcome.exit_code = subprocess.run(command).returncode
    except OSError as e:
        outcome.errors = [f"Couldn't run Unity at {unity}: {e}"]
        return outcome
    # Unity exits 2 when a test failed or was inconclusive, and 1 on an error of its own; the results, when it wrote
    # them, say which tests, so they're read whatever it exited with.
    if outcome.results.is_file():
        try:
            parse_results(outcome.results, outcome)
            return outcome
        except ElementTree.ParseError as e:
            outcome.errors = [f"Couldn't read the results at {outcome.results}: {e}"]
    outcome.errors += unity_errors(outcome.log)
    if outcome.exit_code == 0 and not outcome.errors:
        outcome.errors = ["Unity exited without writing results."]
    return outcome


def print_summary(outcomes, folder, strict):
    """Per platform, the counts; then each test that didn't pass; then where the full results and logs are."""
    print("\n=== Summary ===")
    for outcome in outcomes:
        if outcome.errors:
            print(f"{outcome.platform}: Unity couldn't run the tests (exit code {outcome.exit_code}):")
            for line in outcome.errors:
                print(f"  {line}")
            continue
        counts = outcome.counts
        print(f"{outcome.platform}: {counts['passed']} passed, {counts['failed']} failed, "
              f"{counts['inconclusive']} inconclusive, {counts['skipped']} skipped")
    for outcome in outcomes:
        shown = [p for p in outcome.problems if not p[0].startswith("Skipped")]
        skipped = [p for p in outcome.problems if p[0].startswith("Skipped")]
        for kind, items in (("didn't pass", shown), ("were skipped", skipped)):
            if not items:
                continue
            print(f"\n{outcome.platform} tests that {kind}:")
            for result, name, message, stack in items:
                print(f"  [{result}] {name}")
                if message:
                    for line in message.splitlines()[:5]:
                        print(f"      {line}")
                if stack:
                    print(f"      at {stack}")
    print(f"\nFull results and logs: {folder}")
    inconclusive = sum(o.counts["inconclusive"] for o in outcomes)
    if inconclusive and not strict:
        print(f"{inconclusive} inconclusive (not counted as failing; --strict counts them).")


# --- The command line -------------------------------------------------------------------------------------------------

def parse_arguments(argv, area_names):
    parser = argparse.ArgumentParser(
        prog="run_tests.py", description="Runs the project's Unity tests in batch mode and summarises the results.",
        epilog="Areas: " + ", ".join(area_names) + ". Close the Unity editor first.")
    choose = parser.add_argument_group("what to run (at least one)")
    choose.add_argument("--area", nargs="+", metavar="AREA", help="one or more areas (TestArea's categories)")
    choose.add_argument("--filter", metavar="REGEX", help="tests whose full names match this regex (-testFilter)")
    choose.add_argument("--changed", action="store_true", help="the areas your change can affect (git diff against HEAD)")
    choose.add_argument("--all", action="store_true", help="every test")
    parser.add_argument("--base", metavar="REF", help="with --changed, also what the branch changed since REF (main, say)")
    parser.add_argument("--platform", choices=PLATFORMS + ("both",), default="both", help="which suite (both by default)")
    parser.add_argument("--exclude", nargs="+", default=[], metavar="CATEGORY",
                        help="categories to leave out (Performance, SampleContent)")
    parser.add_argument("--strict", action="store_true", help="count inconclusive tests as failing")
    parser.add_argument("--unity", metavar="PATH", help="the Unity executable (or Unity.app) to use")
    parser.add_argument("--dry-run", action="store_true", help="print the Unity commands it would run, and start nothing")
    parser.add_argument("--project", metavar="PATH", help=argparse.SUPPRESS)  # Another project: the script's tests use it.
    args = parser.parse_args(argv)
    if not (args.area or args.filter or args.changed or args.all):
        parser.error("say what to run: --changed, --area AREA, --filter REGEX or --all")
    if args.all and (args.area or args.changed):
        parser.error("--all runs everything: leave out --area and --changed")
    if args.base and not args.changed:
        parser.error("--base goes with --changed")
    return args


def main(argv=None):
    root = ROOT
    try:
        # The project's needed before the arguments, for the areas the help lists.
        early = list(argv if argv is not None else sys.argv[1:])
        if "--project" in early:
            index = early.index("--project")
            if index + 1 < len(early):
                root = Path(early[index + 1]).resolve()
        areas = read_test_areas(root)
        area_names = list(areas.values())
        args = parse_arguments(argv, area_names)

        chosen = set()
        if args.area:
            unknown = [name for name in args.area if name not in area_names]
            if unknown:
                raise Problem(f"Unknown area{'s' if len(unknown) > 1 else ''}: {', '.join(unknown)}. "
                              f"The areas are: {', '.join(area_names)}.")
            chosen |= set(args.area)
        if args.changed:
            paths = changed_files(root, args.base)
            found, reasons = areas_for_changes(root, paths, areas)
            print("Changed files, and what they can affect:" if reasons else "Nothing has changed.")
            for path, reached, why in reasons:
                reach = "every area" if reached == EVERYTHING else ", ".join(sorted(reached)) if reached else "nothing"
                print(f"  {path}: {reach} ({why})")
            if found == EVERYTHING:
                chosen = EVERYTHING
            elif found:
                chosen |= found
            elif not args.area and not args.filter:
                print(nothing_to_run(reasons))
                return EXIT_PASSED

        everything = args.all or chosen == EVERYTHING
        selected = set() if everything else chosen
        if selected:
            print(f"Areas: {', '.join(sorted(selected))}")
        elif everything:
            print("Running every test.")

        available = platform_areas(root, areas)
        wanted = PLATFORMS if args.platform == "both" else (args.platform,)
        platforms = []
        for platform in wanted:
            areas_here = selected & available[platform]
            if selected and not areas_here:
                print(f"{platform}: no fixtures in {', '.join(sorted(selected))}, so it's skipped.")
                continue
            platforms.append((platform, category_argument(areas_here, args.exclude)))
        if not platforms:
            print("Nothing to run.")
            return EXIT_PASSED

        unity = find_unity(read_editor_version(root), args.unity)
        folder = root / RUNS_FOLDER / datetime.datetime.now().strftime("%Y-%m-%d_%H-%M-%S")
        if args.dry_run:
            # Nothing started and nothing written, so it's safe with the editor open (and says if it is).
            for platform, categories in platforms:
                command = unity_command(unity, root, platform, categories, args.filter,
                                        folder / f"{platform}Results.xml", folder / f"{platform}.log")
                print(" ".join(f'"{part}"' if " " in part or ";" in part or "|" in part else part for part in command))
            holder = lock_holder(root)
            if holder:
                print("The Unity editor has this project open: close it before running these.")
            return EXIT_PASSED
        holder = lock_holder(root)
        if holder:
            raise Problem(f"Close the Unity editor (it has this project open), then run this again. ({holder}.)")

        folder.mkdir(parents=True, exist_ok=True)
        print(f"Unity: {unity}")
        outcomes = [run_platform(unity, root, platform, categories, args.filter, folder)
                    for platform, categories in platforms]
        print_summary(outcomes, folder, args.strict)
        return EXIT_FAILED if any(o.failed(args.strict) for o in outcomes) else EXIT_PASSED
    except Problem as problem:
        print(problem, file=sys.stderr)
        return EXIT_COULD_NOT_START


if __name__ == "__main__":
    sys.exit(main())
