#!/usr/bin/env python3
"""
Fast checks that catch the mistakes agents most often make in a Unity project, without needing Unity.
Run from the repository root:  python3 Tools/CI/check_project_hygiene.py
Exits non-zero and prints every problem found.

Checks:
  1. Every file and folder under Assets/ has a .meta, and every .meta has its asset (no orphans).
  2. No two .meta files share a GUID (copy-pasted .meta files break references silently).
  3. Every assembly references only what AGENTS.md's assembly table allows it (ALLOWED_REFERENCES), and no
     assembly is missing from the table.
  4. The camera and the level builders use no character code: no `using Character`, `Level` or the like.
  5. Every serialized field the tests read or set by name (rig.MovementSetting<T>("name"),
     rig.SetMovementSetting("name", value)) still exists, and is of the type the test asks for it as (<T>).
  6. No two movement settings share a name (the tests find a setting by its name alone).
  7. Every test fixture has [TestTiming] and a test area ([Category(TestArea.…)]), itself or through a base class.
  8. Every movement component a movement asset names ([SerializeReference]: "type: {class: X, ns: Y, asm: Z}")
     still exists by that name, or names it in a [MovedFrom]: renamed without one, it drops out of the asset.
"""
import json
import os
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
ASSETS = ROOT / "Assets"
problems = []


def check_meta_files():
    for dirpath, dirnames, filenames in os.walk(ASSETS):
        here = Path(dirpath)
        # Unity ignores hidden entries and names ending in "~"; they get no .meta.
        dirnames[:] = [d for d in dirnames if not d.startswith(".") and not d.endswith("~")]
        entries = [d for d in dirnames] + [f for f in filenames if not f.startswith(".")]
        names = set(entries)
        for name in entries:
            if name.endswith(".meta"):
                if name[: -len(".meta")] not in names:
                    problems.append(f"Orphan .meta (its asset is gone): {(here / name).relative_to(ROOT)}")
            elif f"{name}.meta" not in names:
                problems.append(f"Missing .meta: {(here / name).relative_to(ROOT)}")


def check_guids():
    seen = {}
    for meta in ASSETS.rglob("*.meta"):
        match = re.search(r"^guid:\s*([0-9a-f]{32})\s*$", meta.read_text(errors="replace"), re.M)
        if not match:
            problems.append(f"No GUID in {meta.relative_to(ROOT)}")
            continue
        guid = match.group(1)
        if guid in seen:
            problems.append(f"Duplicate GUID {guid}: {seen[guid]} and {meta.relative_to(ROOT)}")
        else:
            seen[guid] = meta.relative_to(ROOT)


# What each assembly may reference, as AGENTS.md's assembly table says (keep the two in step). The test assemblies also
# reference Unity's test runner, which every test assembly needs.
TEST_RUNNER = {"UnityEngine.TestRunner", "UnityEditor.TestRunner"}
ALLOWED_REFERENCES = {
    "PhysicsQueries": set(),
    "CharacterShowcase": {"Unity.InputSystem", "PhysicsQueries"},
    "CameraSystem": {"Unity.InputSystem", "PhysicsQueries"},
    "CharacterShowcase.Editor": {"CharacterShowcase", "CameraSystem"},
    "CharacterShowcase.Level.Editor": {"Unity.RenderPipelines.Core.Runtime", "Unity.RenderPipelines.Universal.Runtime",
                                       "Unity.TextMeshPro"},
    "CharacterShowcase.Tests.EditMode": {"CharacterShowcase", "CharacterShowcase.Editor", "CharacterShowcase.Level.Editor",
                                         "CharacterShowcase.Tests.Shared", "PhysicsQueries"} | TEST_RUNNER,
    "CharacterShowcase.Tests.PlayMode": {"CharacterShowcase", "CameraSystem", "CharacterShowcase.Tests.Shared",
                                         "PhysicsQueries"} | TEST_RUNNER,
    "CharacterShowcase.Tests.Shared": {"CharacterShowcase"} | TEST_RUNNER,
}


def assemblies():
    """Every assembly definition under Assets/: its name, folder and references."""
    found = []
    for asmdef in sorted(ASSETS.rglob("*.asmdef")):
        data = json.loads(asmdef.read_text())
        found.append((data.get("name", asmdef.stem), asmdef.parent, data.get("references", [])))
    return found


def check_assembly_references():
    for name, folder, refs in assemblies():
        where = (folder / f"{name}.asmdef").relative_to(ROOT) if (folder / f"{name}.asmdef").exists() else folder.relative_to(ROOT)
        if name not in ALLOWED_REFERENCES:
            problems.append(f"{where}: assembly {name} isn't in AGENTS.md's assembly table (nor ALLOWED_REFERENCES in "
                            f"{Path(__file__).relative_to(ROOT)}): add it to both")
            continue
        for ref in refs:
            if ref.startswith("GUID:"):
                problems.append(f"{where}: references an assembly by GUID ({ref}), which this check can't read: "
                                f"untick Use GUIDs in the assembly definition's inspector")
            elif ref not in ALLOWED_REFERENCES[name]:
                problems.append(f"{where}: {name} may not reference {ref} (see AGENTS.md's assembly table)")


# Folders whose code must know nothing of the character's: the namespaces they may not use.
INDEPENDENT_FOLDERS = {
    ASSETS / "Scripts" / "CameraSystem": "the camera follows a plain Transform",
    ASSETS / "Editor" / "Level": "the level builders make geometry, lighting and markers only",
}
CHARACTER_NAMESPACES = r"(Character|Level|CameraSystem|CharacterShowcase\.EditorTools)\s*;|(Character|Level)\."


def check_independent_folders():
    for folder, why in INDEPENDENT_FOLDERS.items():
        for source in folder.rglob("*.cs"):
            text = source.read_text(errors="replace")
            for match in re.finditer(rf"^\s*using\s+(?:static\s+)?(?:\w+\s*=\s*)?(?:{CHARACTER_NAMESPACES})", text, re.M):
                used = match.group(0).strip()
                # A folder's own namespace is its to use (CameraSystem's camera code is in CameraSystem).
                if folder.name == "CameraSystem" and re.search(r"\bCameraSystem\b", used):
                    continue
                problems.append(f"{source.relative_to(ROOT)}: '{used}': {why}, so it uses no character code")


def movement_setting_sources():
    # BasicMovementMode is a partial class: its fields are spread over BasicMovementMode.cs and
    # BasicMovementMode.<Ability>.cs. Its components (MovementModes/Components) hold the rest, and the base
    # classes (MovementComponent, ComposedMovementMode) a few more.
    modes = ASSETS / "Scripts" / "MovementModes"
    return (sorted(modes.glob("BasicMovementMode*.cs")) + sorted((modes / "Components").glob("*.cs"))
            + [modes / "MovementComponent.cs", modes / "ComposedMovementMode.cs"])


# C#'s keyword names for types, as a field may be declared with either.
TYPE_ALIASES = {"Single": "float", "Int32": "int", "Boolean": "bool", "Double": "double", "String": "string"}


def check_reflected_settings():
    sources = movement_setting_sources()
    source = "\n".join(p.read_text(errors="replace") for p in sources)
    for test in (ASSETS / "Tests").rglob("*.cs"):
        text = test.read_text(errors="replace")
        for name in set(re.findall(r'MovementSetting(?:<\w+>)?\("(\w+)"', text)):
            # A field declaration: "<type> name;" or "<type> name = ..."
            declared = re.search(rf"\b(\w[\w<>\[\],]*)\s+{name}\s*(=|;)", source)
            if not declared:
                problems.append(
                    f"{test.relative_to(ROOT)} reads or sets movement setting '{name}' by reflection, but no such "
                    f"field is declared in BasicMovementMode (or its BasicMovementMode.*.cs parts) or its components"
                )
                continue
            # Asked for as a type (MovementSetting<float>("name")), it must be the field's: as another, the rig fails
            # the test (CharacterTestRig.FindSetting). SetMovementSetting's type is the value's, which this can't see.
            field = re.search(rf"\b(?:public|private|protected|internal)\s+(?:readonly\s+)?([\w<>\[\],.]+)\s+{name}\s*(=|;)", source)
            if not field:
                continue  # Declared some other way: only its name is checked.
            field_type = TYPE_ALIASES.get(field.group(1), field.group(1))
            for asked in set(re.findall(rf'MovementSetting<(\w+)>\("{name}"', text)):
                if asked != "T" and TYPE_ALIASES.get(asked, asked) != field_type:
                    problems.append(f"{test.relative_to(ROOT)} reads movement setting '{name}' as a {asked}, but it's a "
                                    f"{field_type}")


def check_unique_movement_settings():
    # The tests (CharacterTestRig.MovementSetting) find a setting by its name alone, across
    # the mode and its components, so no two may share one.
    declared = {}
    for path in movement_setting_sources():
        if path.name == "MovementComponent.cs":
            continue  # Its fields (enabled) are every component's.
        for name in re.findall(r"\[SerializeField\][^;]*?\b(\w+)\s*(?:=[^;]*)?;", path.read_text(errors="replace")):
            if name in declared and declared[name] != path:
                problems.append(
                    f"Movement setting '{name}' is declared in both {declared[name].relative_to(ROOT)} and "
                    f"{path.relative_to(ROOT)}: setting names must be unique across a mode and its components"
                )
            declared.setdefault(name, path)


def check_test_fixtures():
    # Every test fixture logs how long each test ran and where the time went (TestTimingAttribute), and is in an area
    # ([Category(TestArea.X)], so an area can be run on its own): each on the fixture or on a class it derives from
    # (CharacterRigFixture carries the timing).
    classes = {}  # name -> (base, attributes, has tests, file)
    for path in sorted((ASSETS / "Tests").rglob("*.cs")):
        text = path.read_text(encoding="utf-8")
        for match in re.finditer(r"((?:^[ \t]*\[[^\n]*\][ \t]*\n)*)^[ \t]*(?:public |internal )?(?:abstract |sealed )?class (\w+)(?:\s*:\s*(\w+))?", text, re.M):
            body_start = match.end()
            next_class = re.search(r"^[ \t]*(?:public |internal )?(?:abstract |sealed )?class \w+", text[body_start:], re.M)
            body = text[body_start: body_start + next_class.start()] if next_class else text[body_start:]
            classes[match.group(2)] = (match.group(3), match.group(1),
                                       bool(re.search(r"^[ \t]*\[(Test|UnityTest|TestCase)\b", body, re.M)), path)

    def has(name, attribute, seen=()):
        if name not in classes or name in seen:
            return False
        base, attributes, _, _ = classes[name]
        return attribute in attributes or has(base, attribute, seen + (name,))

    for name, (_, _, has_tests, path) in classes.items():
        if not has_tests:
            continue
        if not has(name, "TestTiming"):
            problems.append(f"{path.relative_to(ROOT)}: test fixture {name} has no [TestTiming] (nor a base class with it)")
        if not has(name, "Category(TestArea."):
            problems.append(f"{path.relative_to(ROOT)}: test fixture {name} is in no area: give it [Category(TestArea.…)] (see TestArea)")


def check_serialized_components():
    # [SerializeReference] saves each object's class by name: "type: {class: X, ns: Y, asm: Z}". Renamed or moved without
    # a [MovedFrom] naming the old one, it shows as a missing component in the asset (its settings lost).
    ours = {name: folder for name, folder, _ in assemblies()}
    for asset in sorted(ASSETS.rglob("*.asset")):
        text = asset.read_text(errors="replace")
        if "type: {class:" not in text:
            continue
        for cls, ns, asm in sorted(set(re.findall(r"type: \{class: (\w+), ns: ([\w.]*), asm: ([\w.]+)\}", text))):
            if asm not in ours:
                continue  # Unity's or a package's
            code = "\n".join(p.read_text(errors="replace") for p in ours[asm].rglob("*.cs"))
            declared = re.search(rf"\bnamespace\s+{re.escape(ns)}\b[\s\S]*?\bclass\s+{cls}\b", code) if ns else \
                re.search(rf"\bclass\s+{cls}\b", code)
            moved = re.search(rf'MovedFrom\([^)]*sourceClassName:\s*"{cls}"', code)
            if not declared and not moved:
                problems.append(f"{asset.relative_to(ROOT)} names the class {ns}.{cls} ({asm}), which no longer exists: "
                                f"give its new name [MovedFrom(... sourceClassName: \"{cls}\")], or the asset loses it")


def main():
    if not ASSETS.is_dir():
        print(f"No Assets folder at {ASSETS}; run this from the repository.", file=sys.stderr)
        return 2
    check_meta_files()
    check_guids()
    check_assembly_references()
    check_independent_folders()
    check_reflected_settings()
    check_unique_movement_settings()
    check_test_fixtures()
    check_serialized_components()
    if problems:
        print(f"{len(problems)} problem(s) found:")
        for problem in problems:
            print(f"  - {problem}")
        return 1
    print("Project hygiene checks passed.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
