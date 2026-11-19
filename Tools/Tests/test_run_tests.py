"""Tests of Tools/run_tests.py: run with  python3 -m unittest discover -s Tools/Tests  (CI does, in the hygiene job).

They never start Unity: each run is of a made-up project (a temporary folder with the files the script reads) and a fake
Unity (fake_unity.py) that writes canned results and logs (data/) and exits as Unity would. They check finding Unity,
refusing while the editor holds the project's lock, which areas a change reaches (AGENTS.md's rule 5), reading the
results (passed, failed, inconclusive, skipped, a fixture's set-up failing, a compile error) and the exit codes.
"""

import contextlib
import io
import json
import os
import shutil
import subprocess
import sys
import tempfile
import textwrap
import unittest
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE.parent))

import run_tests  # noqa: E402
from run_tests import EVERYTHING, Problem  # noqa: E402

DATA = HERE / "data"
FAKE_UNITY = HERE / "fake_unity.py"
POSIX = os.name != "nt"


def make_project(folder):
    """A made-up project: the version, the real TestArea.cs (so the areas are the project's), and a fixture or two."""
    root = Path(folder)
    (root / "ProjectSettings").mkdir(parents=True)
    (root / "ProjectSettings" / "ProjectVersion.txt").write_text("m_EditorVersion: 1234.5.6f1\n")
    shared = root / "Assets" / "Tests" / "Shared"
    shared.mkdir(parents=True)
    shutil.copyfile(run_tests.ROOT / run_tests.TEST_AREA_FILE, shared / "TestArea.cs")
    for platform, folder_name, area in (("EditMode", "Movement", "Movement"), ("PlayMode", "Movement", "Movement"),
                                        ("PlayMode", "Abilities", "Abilities"), ("PlayMode", "Cameras", "Camera"),
                                        ("EditMode", "Levels", "Movement")):
        path = root / "Assets" / "Tests" / platform / folder_name
        path.mkdir(parents=True, exist_ok=True)
        (path / f"{folder_name}{platform}Fixture.cs").write_text(
            f"[TestTiming, Category(TestArea.{area})]\npublic class {folder_name}{platform}Fixture {{ }}\n")
    return root


def hold_lock(path, kind):
    """A process holding a lock (flock or fcntl's) on path until it's told to stop, as Unity holds its lockfile."""
    script = textwrap.dedent(f"""
        import fcntl, os, sys
        fd = os.open({str(path)!r}, os.O_RDWR)
        if {kind!r} == "flock":
            fcntl.flock(fd, fcntl.LOCK_EX)
        else:
            fcntl.lockf(fd, fcntl.LOCK_EX)
        print("locked", flush=True)
        sys.stdin.read()
    """)
    holder = subprocess.Popen([sys.executable, "-c", script], stdin=subprocess.PIPE, stdout=subprocess.PIPE, text=True)
    assert holder.stdout.readline().strip() == "locked"
    return holder


class ProjectCase(unittest.TestCase):
    """A test with a made-up project of its own, removed after it."""

    def setUp(self):
        self.folder = tempfile.mkdtemp()
        self.addCleanup(shutil.rmtree, self.folder, True)
        self.root = make_project(self.folder)
        self.areas = run_tests.read_test_areas(self.root)


class FindingUnity(ProjectCase):
    def test_the_version_is_read_from_project_version(self):
        self.assertEqual("1234.5.6f1", run_tests.read_editor_version(self.root))

    def test_the_areas_are_test_areas_constants(self):
        self.assertIn("Movement", self.areas.values())
        self.assertIn("Camera", self.areas.values())

    def test_each_platform_has_the_areas_its_fixtures_declare(self):
        found = run_tests.platform_areas(self.root, self.areas)
        self.assertEqual({"Movement"}, found["EditMode"])
        self.assertEqual({"Movement", "Abilities", "Camera"}, found["PlayMode"])

    def test_an_explicit_path_is_used(self):
        unity = self.root / "MyUnity"
        unity.write_text("")
        self.assertEqual(unity, run_tests.find_unity("1234.5.6f1", str(unity), environ={}, home=self.root))

    def test_unity_path_is_used(self):
        unity = self.root / "MyUnity"
        unity.write_text("")
        self.assertEqual(unity, run_tests.find_unity("1234.5.6f1", None, environ={"UNITY_PATH": str(unity)},
                                                     home=self.root, platform="linux"))

    def test_a_path_given_that_isnt_there_is_reported(self):
        with self.assertRaisesRegex(Problem, "--unity"):
            run_tests.find_unity("1234.5.6f1", str(self.root / "Missing"), environ={}, home=self.root)
        with self.assertRaisesRegex(Problem, "UNITY_PATH"):
            run_tests.find_unity("1234.5.6f1", None, environ={"UNITY_PATH": str(self.root / "Missing")}, home=self.root)

    def test_unity_app_means_its_executable(self):
        self.assertEqual(Path("/Apps/Unity.app/Contents/MacOS/Unity"), run_tests.as_executable("/Apps/Unity.app"))

    def test_the_hubs_folder_is_searched_for_the_projects_version(self):
        unity = self.root / "Unity" / "Hub" / "Editor" / "1234.5.6f1" / "Editor" / "Unity"
        unity.parent.mkdir(parents=True)
        unity.write_text("")
        self.assertEqual(unity, run_tests.find_unity("1234.5.6f1", environ={}, home=self.root, platform="linux"))

    def test_the_hubs_chosen_install_folder_is_searched_too(self):
        elsewhere = self.root / "Elsewhere"
        unity = elsewhere / "1234.5.6f1" / "Editor" / "Unity"
        unity.parent.mkdir(parents=True)
        unity.write_text("")
        settings = self.root / ".config" / "UnityHub"
        settings.mkdir(parents=True)
        (settings / "secondaryInstallPath.json").write_text(json.dumps(str(elsewhere)))
        self.assertEqual(unity, run_tests.find_unity("1234.5.6f1", environ={}, home=self.root, platform="linux"))

    def test_each_systems_hub_layout(self):
        home = Path("/home/someone")
        self.assertEqual(Path("/Applications/Unity/Hub/Editor/V/Unity.app/Contents/MacOS/Unity"),
                         run_tests.editor_executable(run_tests.hub_install_folders(home, "darwin", {})[0] / "V", "darwin"))
        self.assertEqual(Path(r"C:\Program Files") / "Unity" / "Hub" / "Editor" / "V" / "Editor" / "Unity.exe",
                         run_tests.editor_executable(run_tests.hub_install_folders(home, "win32", {})[0] / "V", "win32"))
        self.assertEqual(home / "Unity" / "Hub" / "Editor" / "V" / "Editor" / "Unity",
                         run_tests.editor_executable(run_tests.hub_install_folders(home, "linux", {})[0] / "V", "linux"))

    def test_none_found_says_what_to_install_or_set(self):
        with self.assertRaises(Problem) as raised:
            run_tests.find_unity("1234.5.6f1", environ={}, home=self.root, platform="linux")
        message = str(raised.exception)
        for expected in ("1234.5.6f1", "Unity Hub", "--unity", "UNITY_PATH", "Looked in"):
            self.assertIn(expected, message)


class TheEditorsLock(ProjectCase):
    def lockfile(self):
        path = self.root / run_tests.LOCK_FILE
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text("")
        return path

    def test_no_lockfile_is_no_editor(self):
        self.assertIsNone(run_tests.lock_holder(self.root))

    def test_a_lockfile_left_behind_doesnt_stop_the_run(self):
        self.lockfile()
        self.assertIsNone(run_tests.lock_holder(self.root))

    @unittest.skipUnless(POSIX, "flock and fcntl's locks are POSIX")
    def test_a_held_lock_of_either_kind_is_the_editor(self):
        for kind in ("flock", "fcntl"):
            with self.subTest(kind):
                holder = hold_lock(self.lockfile(), kind)
                try:
                    self.assertIn("lock", run_tests.lock_holder(self.root))
                finally:
                    holder.communicate("")
                self.assertIsNone(run_tests.lock_holder(self.root))

    @unittest.skipUnless(POSIX, "flock and fcntl's locks are POSIX")
    def test_the_run_is_refused_while_its_held(self):
        holder = hold_lock(self.lockfile(), "flock")
        try:
            code, output, errors = run_main(self.root, ["--all"], {"EditMode": {}, "PlayMode": {}}, self)
        finally:
            holder.communicate("")
        self.assertEqual(run_tests.EXIT_COULD_NOT_START, code)
        self.assertIn("Close the Unity editor (it has this project open), then run this again.", errors)
        self.assertEqual([], calls(self))


class WhatAChangeReaches(ProjectCase):
    def reach(self, *paths, root=None):
        return run_tests.areas_for_changes(root or run_tests.ROOT, list(paths), run_tests.read_test_areas(run_tests.ROOT))

    def test_the_movement_reaches_movement_abilities_and_traversal(self):
        self.assertEqual({"Movement", "Abilities", "Traversal"},
                         self.reach("Assets/Scripts/Character/MMotionMotor.cs")[0])
        self.assertEqual({"Movement", "Abilities", "Traversal"},
                         self.reach("Assets/Scripts/MovementModes/Components/Crouching.cs")[0])

    def test_animation_ik_and_models_reach_animation_and_models(self):
        for path in ("Assets/Scripts/Animation/MMotionAnimator.cs", "Assets/Scripts/IK/HumanoidIK.cs",
                     "Assets/Scripts/Models/CharacterModelSlot.cs", "Assets/Editor/Models/HitboxFitter.cs"):
            with self.subTest(path):
                self.assertEqual({"Animation", "Models"}, self.reach(path)[0])

    def test_the_camera_reaches_camera(self):
        self.assertEqual({"Camera"}, self.reach("Assets/Scripts/CameraSystem/MMotionCameraRig.cs")[0])

    def test_the_levels_reach_levels_and_traversal(self):
        self.assertEqual({"Levels", "Traversal"}, self.reach("Assets/Scripts/Level/SwingRope.cs")[0])
        self.assertEqual({"Levels", "Traversal"}, self.reach("Assets/Editor/Level/Graybox/Graybox.cs")[0])

    def test_spawning_reaches_spawning(self):
        self.assertEqual({"Spawning"}, self.reach("Assets/Scripts/Character/CharacterSpawner.cs")[0])
        self.assertEqual({"Spawning"}, self.reach("Assets/Editor/Scene/CharacterSceneSetup.cs")[0])

    def test_a_meta_file_reaches_what_its_asset_does(self):
        self.assertEqual({"Camera"}, self.reach("Assets/Scripts/CameraSystem/MMotionCameraRig.cs.meta")[0])
        self.assertEqual({"Camera"}, self.reach("Assets/Scripts/CameraSystem.meta")[0])

    def test_a_test_file_reaches_the_areas_it_declares(self):
        # In the Levels folder but in the Movement category, as MovementEnvelopeTests is.
        self.assertEqual({"Movement"}, self.reach("Assets/Tests/EditMode/Levels/LevelsEditModeFixture.cs",
                                                  root=self.root)[0])

    def test_a_deleted_test_file_reaches_its_folders_area(self):
        for folder, area in (("InputAndDeterminism", "Movement"), ("Animations", "Animation"), ("Cameras", "Camera"),
                             ("LevelElements", "Levels"), ("Abilities", "Abilities")):
            with self.subTest(folder):
                self.assertEqual({area}, self.reach(f"Assets/Tests/PlayMode/{folder}/Gone.cs", root=self.root)[0])

    def test_the_tests_shared_code_reaches_everything(self):
        self.assertEqual(EVERYTHING, self.reach("Assets/Tests/PlayMode/CharacterTestRig.cs", root=self.root)[0])
        self.assertEqual(EVERYTHING, self.reach("Assets/Tests/Shared/TestArea.cs", root=self.root)[0])

    def test_anything_unmapped_under_assets_reaches_everything(self):
        chosen, reasons = self.reach("Assets/Scripts/Character/MMotionMotor.cs", "Assets/Prefabs/Character.prefab")
        self.assertEqual(EVERYTHING, chosen)
        self.assertEqual(EVERYTHING, reasons[1][1])
        self.assertEqual(EVERYTHING, self.reach("ProjectSettings/DynamicsManager.asset")[0])

    def test_only_docs_reach_nothing_and_say_so(self):
        chosen, reasons = self.reach("README.md", "docs/testing.md")
        self.assertEqual(set(), chosen)
        self.assertIn("only documentation", run_tests.nothing_to_run(reasons))

    def test_only_python_tools_say_the_hygiene_check_covers_them(self):
        chosen, reasons = self.reach("Tools/LevelSpec/levelspec/checks.py", "Tools/run_tests.py", "docs/testing.md")
        self.assertEqual(set(), chosen)
        self.assertIn("hygiene check", run_tests.nothing_to_run(reasons))

    def test_each_file_says_why(self):
        _, reasons = self.reach("Assets/Scripts/CameraSystem/MMotionCameraRig.cs")
        self.assertEqual([("Assets/Scripts/CameraSystem/MMotionCameraRig.cs", {"Camera"}, "the camera")], reasons)

    def test_every_rule_names_a_real_area(self):
        areas = set(run_tests.read_test_areas(run_tests.ROOT).values())
        for rule, reached, _ in run_tests.CHANGE_RULES:
            with self.subTest(rule):
                self.assertTrue(set(reached) <= areas, f"{rule} reaches {set(reached) - areas}, not TestArea's")
        self.assertTrue(set(run_tests.TEST_FOLDER_AREAS.values()) <= areas)

    @unittest.skipUnless(shutil.which("git"), "needs git")
    def test_the_changes_are_gits(self):
        def git(*args):
            subprocess.run(["git", "-C", str(self.root), "-c", "user.name=T", "-c", "user.email=t@t"] + list(args),
                           check=True, capture_output=True)
        git("init", "-q", "-b", "main")
        git("add", "-A")
        git("commit", "-q", "-m", "first")
        git("checkout", "-q", "-b", "work")
        (self.root / "committed.txt").write_text("on the branch")
        git("add", "committed.txt")
        git("commit", "-q", "-m", "second")
        (self.root / "ProjectSettings" / "ProjectVersion.txt").write_text("changed")
        (self.root / "new.txt").write_text("untracked")
        (self.root / "staged.txt").write_text("staged")
        git("add", "staged.txt")
        self.assertEqual(["ProjectSettings/ProjectVersion.txt", "new.txt", "staged.txt"],
                         run_tests.changed_files(self.root))
        self.assertEqual(["ProjectSettings/ProjectVersion.txt", "committed.txt", "new.txt", "staged.txt"],
                         run_tests.changed_files(self.root, "main"))


class ReadingTheResults(unittest.TestCase):
    def parse(self, name):
        outcome = run_tests.PlatformResult("PlayMode", DATA / name, None)
        run_tests.parse_results(DATA / name, outcome)
        return outcome

    def test_a_passing_run(self):
        outcome = self.parse("passed.xml")
        self.assertEqual({"passed": 2, "failed": 0, "inconclusive": 0, "skipped": 0}, outcome.counts)
        self.assertEqual([], outcome.problems)
        self.assertFalse(outcome.failed(strict=True))

    def test_a_failing_run(self):
        outcome = self.parse("failed.xml")
        # The test-run's one failure, and VaultTests' set-up, which its test-run total doesn't count.
        self.assertEqual({"passed": 1, "failed": 2, "inconclusive": 1, "skipped": 1}, outcome.counts)
        problems = {name: (result, message, stack) for result, name, message, stack in outcome.problems}
        result, message, stack = problems["MeridianMotion.Tests.Abilities.LedgeTests.ALedge_IsClimbed"]
        self.assertEqual("Failed (Error)", result)
        self.assertTrue(message.startswith("The character didn't reach the top."))
        self.assertTrue(stack.startswith("at MeridianMotion.Tests.Abilities.LedgeTests"))
        self.assertNotIn("TestEnumerator", stack)
        self.assertEqual(("Inconclusive", "The character has no ledge hang.", ""),
                         problems["MeridianMotion.Tests.Abilities.LedgeTests.ALedge_OnAFox"])
        self.assertEqual("Skipped (Ignored)", problems["MeridianMotion.Tests.Abilities.LedgeTests.ALedge_Later"][0])
        self.assertEqual("Failed (SetUp)", problems["MeridianMotion.Tests.Abilities.VaultTests"][0])
        self.assertTrue(outcome.failed(strict=False))

    def test_compile_errors_are_read_from_the_log_each_once(self):
        errors = run_tests.unity_errors(DATA / "compile_error.log")
        self.assertEqual(2, len(errors))
        self.assertTrue(all("error CS" in line for line in errors))


INCONCLUSIVE_ONLY = """<?xml version="1.0" encoding="utf-8"?>
<test-run id="2" testcasecount="2" result="Passed" total="2" passed="1" failed="0" inconclusive="1" skipped="0">
  <test-suite type="TestFixture" name="T" fullname="T" result="Passed" total="2">
    <test-case name="A" fullname="T.A" result="Passed" />
    <test-case name="B" fullname="T.B" result="Inconclusive"><reason><message>Not on this model.</message></reason></test-case>
  </test-suite>
</test-run>
"""


def run_main(root, arguments, config, case):
    """Runs the script's main on root with the fake Unity, configured per platform: (exit code, stdout, stderr)."""
    folder = Path(root) / "fake"
    folder.mkdir(exist_ok=True)
    (folder / "config.json").write_text(json.dumps(config))
    case.calls_file = folder / "calls.jsonl"
    if POSIX:
        unity = folder / "Unity"
        unity.write_text(f'#!/bin/sh\nexec "{sys.executable}" "{FAKE_UNITY}" "$@"\n')
        unity.chmod(0o755)
    else:
        unity = folder / "Unity.cmd"
        unity.write_text(f'@"{sys.executable}" "{FAKE_UNITY}" %*\n')
    environ = {"FAKE_UNITY_CONFIG": str(folder / "config.json"), "FAKE_UNITY_CALLS": str(case.calls_file)}
    output, errors = io.StringIO(), io.StringIO()
    old = {key: os.environ.get(key) for key in environ}
    os.environ.update(environ)
    try:
        with contextlib.redirect_stdout(output), contextlib.redirect_stderr(errors):
            code = run_tests.main(arguments + ["--unity", str(unity), "--project", str(root)])
    finally:
        for key, value in old.items():
            if value is None:
                os.environ.pop(key, None)
            else:
                os.environ[key] = value
    return code, output.getvalue(), errors.getvalue()


def calls(case):
    """The arguments the fake Unity was run with, one list per run."""
    if not case.calls_file.exists():
        return []
    return [json.loads(line) for line in case.calls_file.read_text().splitlines()]


def argument(call, name):
    return call[call.index(name) + 1] if name in call else None


@unittest.skipUnless(POSIX, "the fake Unity is started as a shell script")
class Running(ProjectCase):
    def test_passing_exits_0_with_a_summary(self):
        both = {"results": str(DATA / "passed.xml")}
        code, output, _ = run_main(self.root, ["--all"], {"EditMode": both, "PlayMode": both}, self)
        self.assertEqual(run_tests.EXIT_PASSED, code)
        self.assertIn("EditMode: 2 passed, 0 failed, 0 inconclusive, 0 skipped", output)
        self.assertIn("PlayMode: 2 passed", output)
        self.assertIn("Full results and logs:", output)
        made = calls(self)
        self.assertEqual(["EditMode", "PlayMode"], [argument(call, "-testPlatform") for call in made])
        self.assertIsNone(argument(made[0], "-testCategory"))
        results = Path(argument(made[0], "-testResults"))
        self.assertEqual(self.root.resolve() / "Logs" / "TestRuns", results.parent.parent)

    def test_a_failure_exits_1_and_lists_it(self):
        config = {"PlayMode": {"results": str(DATA / "failed.xml"), "exit": 2}}
        code, output, _ = run_main(self.root, ["--area", "Abilities"], config, self)
        self.assertEqual(run_tests.EXIT_FAILED, code)
        self.assertIn("PlayMode: 1 passed, 2 failed, 1 inconclusive, 1 skipped", output)
        self.assertIn("[Failed (Error)] MeridianMotion.Tests.Abilities.LedgeTests.ALedge_IsClimbed", output)
        self.assertIn("The character didn't reach the top.", output)
        self.assertIn("at at MeridianMotion.Tests.Abilities.LedgeTests", output)
        self.assertIn("The character has no ledge hang.", output)
        # EditMode has no Abilities fixture, so only PlayMode ran, for that category.
        self.assertIn("EditMode: no fixtures in Abilities", output)
        made = calls(self)
        self.assertEqual(1, len(made))
        self.assertEqual("Abilities", argument(made[0], "-testCategory"))

    def test_inconclusive_passes_unless_strict(self):
        results = Path(self.folder) / "inconclusive.xml"
        results.write_text(INCONCLUSIVE_ONLY)
        config = {"PlayMode": {"results": str(results), "exit": 2}}
        code, output, _ = run_main(self.root, ["--area", "Camera"], config, self)
        self.assertEqual(run_tests.EXIT_PASSED, code)
        self.assertIn("1 inconclusive (not counted as failing; --strict counts them)", output)
        code, _, _ = run_main(self.root, ["--area", "Camera", "--strict"], config, self)
        self.assertEqual(run_tests.EXIT_FAILED, code)

    def test_a_compile_error_exits_1_and_prints_the_errors(self):
        config = {"EditMode": {"log": str(DATA / "compile_error.log"), "exit": 1},
                  "PlayMode": {"log": str(DATA / "compile_error.log"), "exit": 1}}
        code, output, _ = run_main(self.root, ["--area", "Movement", "--exclude", "Performance"], config, self)
        self.assertEqual(run_tests.EXIT_FAILED, code)
        self.assertIn("Unity couldn't run the tests (exit code 1)", output)
        self.assertIn("error CS0103: The name 'velocty' does not exist", output)
        self.assertEqual(["Movement;!Performance"] * 2, [argument(call, "-testCategory") for call in calls(self)])

    def test_a_filter_and_one_platform(self):
        config = {"EditMode": {"results": str(DATA / "passed.xml")}}
        code, _, _ = run_main(self.root, ["--filter", "LedgeTests|VaultTests", "--platform", "EditMode"], config, self)
        self.assertEqual(run_tests.EXIT_PASSED, code)
        made = calls(self)
        self.assertEqual(1, len(made))
        self.assertEqual("LedgeTests|VaultTests", argument(made[0], "-testFilter"))

    def test_dry_run_starts_nothing(self):
        code, output, _ = run_main(self.root, ["--all", "--dry-run"], {}, self)
        self.assertEqual(run_tests.EXIT_PASSED, code)
        self.assertIn("-testPlatform PlayMode", output)
        self.assertEqual([], calls(self))

    def test_an_unknown_area_is_refused_with_the_list(self):
        code, _, errors = run_main(self.root, ["--area", "Swimming"], {}, self)
        self.assertEqual(run_tests.EXIT_COULD_NOT_START, code)
        self.assertIn("Unknown area: Swimming", errors)
        self.assertIn("Movement, Abilities", errors)
        self.assertEqual([], calls(self))


if __name__ == "__main__":
    unittest.main()
